using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using PaymentBatchProcessingService.App.Exceptions;
using PaymentBatchProcessingService.App.Interfaces;
using PaymentBatchProcessingService.App.PaymentBatches;
using PaymentBatchProcessingService.Domain.Entities;
using PaymentBatchProcessingService.Infrastructure.DB;
using PaymentBatchProcessingService.Infrastructure.DB.Models;

namespace PaymentBatchProcessingService.Infrastructure.Stores;

public sealed class PaymentBatchStore(PaymentsDbContext dbContext) : IPaymentBatchStore
{
    private readonly PaymentsDbContext _dbContext = dbContext;

    public Task<LegalEntity?> GetLegalEntityAsync(
        Guid tenantId,
        Guid legalEntityId,
        CancellationToken cancellationToken)
    {
        return _dbContext.Set<LegalEntity>()
            .AsNoTracking()
            .SingleOrDefaultAsync(
                entity => entity.Id == legalEntityId && entity.TenantId == tenantId,
                cancellationToken);
    }

    public Task<BankAccount?> GetBankAccountAsync(
        Guid legalEntityId,
        Guid bankAccountId,
        CancellationToken cancellationToken)
    {
        return _dbContext.Set<BankAccount>()
            .AsNoTracking()
            .SingleOrDefaultAsync(
                account => account.Id == bankAccountId && account.LegalEntityId == legalEntityId,
                cancellationToken);
    }

    public Task<BatchIdempotencyResult?> GetByIdempotencyKeyAsync(
        Guid tenantId,
        string idempotencyKey,
        CancellationToken cancellationToken)
    {
        return _dbContext.Set<BatchIdempotencyRecord>()
            .AsNoTracking()
            .Where(record =>
                record.TenantId == tenantId
                && record.IdempotencyKey == idempotencyKey)
            .Select(record => new BatchIdempotencyResult(
                record.BatchId,
                record.RequestFingerprint))
            .SingleOrDefaultAsync(cancellationToken);
    }

    public async Task<BatchIdempotencyResult> CreateBatchAsync(
        PaymentBatch batch,
        IReadOnlyCollection<Payment> payments,
        Guid tenantId,
        string idempotencyKey,
        string requestFingerprint,
        CancellationToken cancellationToken)
    {
        var idempotencyRecord = new BatchIdempotencyRecord(
            tenantId,
            batch.Id,
            idempotencyKey,
            requestFingerprint,
            batch.CreatedAtUtc);

        _dbContext.Set<PaymentBatch>().Add(batch);
        _dbContext.Set<Payment>().AddRange(payments);
        _dbContext.Set<BatchIdempotencyRecord>().Add(idempotencyRecord);

        try
        {
            await _dbContext.SaveChangesAsync(cancellationToken);

            return new BatchIdempotencyResult(batch.Id, requestFingerprint);
        }
        catch (DbUpdateException exception) when (IsUniqueConstraintViolation(exception))
        {
            DetachAddedEntities(batch, payments, idempotencyRecord);

            var existing = await GetByIdempotencyKeyAsync(
                tenantId,
                idempotencyKey,
                cancellationToken);

            if (existing is not null)
            {
                if (!string.Equals(existing.RequestFingerprint, requestFingerprint, StringComparison.Ordinal))
                {
                    throw new IdempotencyConflictException();
                }

                return existing;
            }

            //The unique constraint violation was unrelated to the requested idempotency key
            throw;
        }
    }

    public Task<CreateBatchResult?> GetBatchResultAsync(
        Guid tenantId,
        Guid batchId,
        CancellationToken cancellationToken)
    {
        return _dbContext.Set<PaymentBatch>()
            .AsNoTracking()
            .Where(batch => batch.Id == batchId)
            .Where(batch => _dbContext.Set<LegalEntity>()
                .Any(entity =>
                    entity.Id == batch.LegalEntityId &&
                    entity.TenantId == tenantId))
            .Select(batch => new CreateBatchResult(
                batch.Id,
                batch.LegalEntityId,
                batch.Currency,
                batch.Status,
                _dbContext.Set<Payment>().Count(payment => payment.PaymentBatchId == batch.Id),
                batch.CreatedAtUtc))
            .SingleOrDefaultAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<PaymentDetailsResult>?> GetBatchPaymentsAsync(
        Guid tenantId,
        Guid batchId,
        CancellationToken cancellationToken)
    {
        var batchExists = await _dbContext.Set<PaymentBatch>()
            .AsNoTracking()
            .AnyAsync(
                batch => batch.Id == batchId && _dbContext.Set<LegalEntity>().Any(
                    entity => entity.Id == batch.LegalEntityId && entity.TenantId == tenantId),
                cancellationToken);

        if (!batchExists)
        {
            return null;
        }

        return await _dbContext.Set<Payment>()
            .AsNoTracking()
            .Where(payment => payment.PaymentBatchId == batchId)
            .OrderBy(payment => payment.CreatedAtUtc)
            .ThenBy(payment => payment.Id)
            .Select(payment => new PaymentDetailsResult(
                payment.Id,
                payment.PaymentBatchId,
                payment.Amount,
                payment.BeneficiaryName,
                payment.BeneficiaryAccountIdentifier,
                payment.Reference,
                payment.Status,
                payment.BankReference,
                payment.SettlementReference,
                payment.LastStatusReason,
                payment.CreatedAtUtc))
            .ToListAsync(cancellationToken);
    }

    private static bool IsUniqueConstraintViolation(DbUpdateException exception)
    {
        return exception.InnerException is SqlException sqlException && sqlException.Number is 2601 or 2627;
    }

    private void DetachAddedEntities(
        PaymentBatch batch,
        IReadOnlyCollection<Payment> payments,
        BatchIdempotencyRecord idempotencyRecord)
    {
        _dbContext.Entry(batch).State = EntityState.Detached;

        foreach (var payment in payments)
        {
            _dbContext.Entry(payment).State = EntityState.Detached;
        }

        _dbContext.Entry(idempotencyRecord).State = EntityState.Detached;
    }
}
