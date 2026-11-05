using PaymentBatchProcessingService.Domain.Enums;

namespace PaymentBatchProcessingService.Endpoints;

public sealed record CreateBatchResponse(
    Guid BatchId,
    Guid LegalEntityId,
    Currency Currency,
    PaymentBatchStatus Status,
    int PaymentCount,
    DateTime CreatedAtUtc);