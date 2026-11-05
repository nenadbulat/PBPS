using PaymentBatchProcessingService.Domain.Enums;

namespace PaymentBatchProcessingService.App.PaymentBatches;

public sealed record CreateBatchRequest(
    Guid LegalEntityId,
    Guid SourceBankAccountId,
    Currency Currency,
    IReadOnlyList<CreatePaymentRequest> Payments);
