using Microsoft.Extensions.Logging;
using PaymentBatchProcessingService.App.Exceptions;
using PaymentBatchProcessingService.App.Interfaces;
using PaymentBatchProcessingService.Domain.Entities;
using PaymentBatchProcessingService.Domain.Helpers;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace PaymentBatchProcessingService.App.PaymentBatches;

public sealed class CreateBatchService(
    IPaymentBatchStore store,
    ITenantContext tenantContext,
    TimeProvider timeProvider,
    ILogger<CreateBatchService> logger)
{
    public async Task<CreateBatchResult> ExecuteAsync(
        CreateBatchRequest request,
        string idempotencyKey,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var tenantId = tenantContext.TenantId;
        if (tenantId == Guid.Empty)
        {
            throw new InvalidOperationException("A valid tenant context is required.");
        }

        ValidateRequest(request);

        try
        {
            idempotencyKey = Guard.ValidateRequiredText(idempotencyKey, Constants.IdempotencyKeyLength, nameof(idempotencyKey));
        }
        catch (ArgumentException exception)
        {
            throw new BatchValidationException(exception.Message);
        }

        var normalizedPayments = NormalizePayments(request.Payments);
        var fingerprint = CalculateFingerprint(tenantId, request, normalizedPayments);
        var existing = await store.GetByIdempotencyKeyAsync(tenantId, idempotencyKey, cancellationToken);

        if (existing is not null)
        {
            EnsureMatchingFingerprint(existing, fingerprint);

            var result = await GetExistingResultAsync(tenantId, existing.BatchId, cancellationToken);

            logger.LogInformation(
                "Payment batch replayed. BatchId: {BatchId}, TenantId: {TenantId}",
                result.BatchId,
                tenantId);

            return result with { IsReplay = true };
        }

        var legalEntity = await store.GetLegalEntityAsync(tenantId, request.LegalEntityId, cancellationToken);

        if (legalEntity is null)
        {
            throw new BatchNotFoundException();
        }

        var bankAccount = await store.GetBankAccountAsync(request.LegalEntityId, request.SourceBankAccountId, cancellationToken);

        if (bankAccount is null)
        {
            throw new BatchNotFoundException();
        }

        if (bankAccount.Currency != request.Currency)
        {
            throw new BatchValidationException("Source bank account currency does not match the batch currency.");
        }

        var createdAtUtc = timeProvider.GetUtcNow().UtcDateTime;
        var batchId = Guid.NewGuid();
        var batch = new PaymentBatch(batchId, request.LegalEntityId, request.SourceBankAccountId, request.Currency, createdAtUtc);

        var payments = normalizedPayments
            .Select(payment => new Payment(Guid.NewGuid(), batchId, payment.Amount, payment.BeneficiaryName, payment.BeneficiaryAccountIdentifier, payment.Reference, createdAtUtc))
            .ToArray();

        var saved = await store.CreateBatchAsync(batch, payments, tenantId, idempotencyKey, fingerprint, cancellationToken);

        EnsureMatchingFingerprint(saved, fingerprint);

        if (saved.BatchId != batchId)
        {
            var result = await GetExistingResultAsync(tenantId, saved.BatchId, cancellationToken);

            logger.LogInformation(
                "Payment batch replayed after concurrent creation. BatchId: {BatchId}, TenantId: {TenantId}",
                result.BatchId,
                tenantId);

            return result with { IsReplay = true };
        }

        logger.LogInformation(
            "Payment batch created. BatchId: {BatchId}, TenantId: {TenantId}, PaymentCount: {PaymentCount}",
            batch.Id,
            tenantId,
            payments.Length);

        return new CreateBatchResult(batch.Id, batch.LegalEntityId, batch.Currency, batch.Status, payments.Length, batch.CreatedAtUtc);
    }

    private static void ValidateRequest(CreateBatchRequest request)
    {
        if (request.LegalEntityId == Guid.Empty)
        {
            throw new BatchValidationException("Legal entity ID is required.");
        }

        if (request.SourceBankAccountId == Guid.Empty)
        {
            throw new BatchValidationException("Source bank account ID is required.");
        }

        if (request.Payments is null || request.Payments.Count == 0)
        {
            throw new BatchValidationException("At least one payment is required.");
        }

        if (request.Payments.Count > Constants.MaxPaymentsPerBatch)
        {
            throw new BatchValidationException($"A batch cannot contain more than {Constants.MaxPaymentsPerBatch} payments.");
        }

        try
        {
            Guard.ValidateCurrency(request.Currency);
        }
        catch (ArgumentException exception)
        {
            throw new BatchValidationException(exception.Message);
        }
    }

    private static IReadOnlyList<NormalizedPayment> NormalizePayments(IReadOnlyList<CreatePaymentRequest> payments)
    {
        var result = new List<NormalizedPayment>(payments.Count);

        for (var index = 0; index < payments.Count; index++)
        {
            var payment = payments[index];

            if (payment is null)
            {
                throw new BatchValidationException($"Payment at index {index} is required.");
            }

            try
            {
                Guard.ValidateAmount(payment.Amount);

                result.Add(new NormalizedPayment(
                    payment.Amount,
                    Guard.ValidateRequiredText(payment.BeneficiaryName, Constants.MaxNameLength, nameof(payment.BeneficiaryName)),
                    Guard.ValidateAccountIdentifier(payment.BeneficiaryAccountIdentifier),
                    Guard.ValidateRequiredText(payment.Reference, Constants.MaxReferenceLength, nameof(payment.Reference))));
            }
            catch (ArgumentException exception)
            {
                throw new BatchValidationException($"Payment at index {index}: {exception.Message}");
            }
        }

        return result;
    }

    private static string CalculateFingerprint(Guid tenantId, CreateBatchRequest request, IReadOnlyList<NormalizedPayment> payments)
    {
        var canonicalRequest = new
        {
            TenantId = tenantId,
            request.LegalEntityId,
            request.SourceBankAccountId,
            Currency = request.Currency.ToString(),
            Payments = payments.Select(payment => new
            {
                Amount = payment.Amount.ToString("F4", CultureInfo.InvariantCulture),
                payment.BeneficiaryName,
                payment.BeneficiaryAccountIdentifier,
                payment.Reference
            })
        };

        var json = JsonSerializer.Serialize(canonicalRequest);
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(json));

        return Convert.ToHexString(hash);
    }

    private static void EnsureMatchingFingerprint(BatchIdempotencyResult existing, string fingerprint)
    {
        if (!string.Equals(existing.RequestFingerprint, fingerprint, StringComparison.Ordinal))
        {
            throw new IdempotencyConflictException();
        }
    }

    private async Task<CreateBatchResult> GetExistingResultAsync(Guid tenantId, Guid batchId, CancellationToken cancellationToken)
    {
        var result = await store.GetBatchResultAsync(tenantId, batchId, cancellationToken);

        return result ?? throw new InvalidOperationException("The idempotency record references a missing payment batch.");
    }

    private sealed record NormalizedPayment(
        decimal Amount,
        string BeneficiaryName,
        string BeneficiaryAccountIdentifier,
        string Reference);
}