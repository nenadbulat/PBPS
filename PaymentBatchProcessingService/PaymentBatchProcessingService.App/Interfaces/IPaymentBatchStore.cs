using PaymentBatchProcessingService.App.PaymentBatches;
using PaymentBatchProcessingService.Domain.Entities;

namespace PaymentBatchProcessingService.App.Interfaces;

public interface IPaymentBatchStore
{
    Task<LegalEntity?> GetLegalEntityAsync(
        Guid tenantId,
        Guid legalEntityId,
        CancellationToken cancellationToken);

    Task<BankAccount?> GetBankAccountAsync(
    Guid legalEntityId,
    Guid bankAccountId,
    CancellationToken cancellationToken);

    Task<BatchIdempotencyResult?> GetByIdempotencyKeyAsync(
        Guid tenantId,
        string idempotencyKey,
        CancellationToken cancellationToken);

    Task<BatchIdempotencyResult> CreateBatchAsync(
        PaymentBatch batch,
        IReadOnlyCollection<Payment> payments,
        Guid tenantId,
        string idempotencyKey,
        string requestFingerprint,
        CancellationToken cancellationToken);

    Task<CreateBatchResult?> GetBatchResultAsync(
        Guid tenantId,
        Guid batchId,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<PaymentDetailsResult>?> GetBatchPaymentsAsync(
        Guid tenantId,
        Guid batchId,
        CancellationToken cancellationToken);
}
