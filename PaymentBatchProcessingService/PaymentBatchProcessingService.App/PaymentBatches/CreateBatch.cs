using PaymentBatchProcessingService.Domain.Enums;

namespace PaymentBatchProcessingService.App.PaymentBatches;

public sealed record CreateBatchResult(
    Guid BatchId,
    Guid LegalEntityId,
    Currency Currency,
    PaymentBatchStatus Status,
    int PaymentCount,
    DateTime CreatedAtUtc,
    bool IsReplay = false);
