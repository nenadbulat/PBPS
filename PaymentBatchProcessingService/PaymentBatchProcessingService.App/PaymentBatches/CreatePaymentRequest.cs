namespace PaymentBatchProcessingService.App.PaymentBatches;

public sealed record CreatePaymentRequest(
    decimal Amount,
    string BeneficiaryName,
    string BeneficiaryAccountIdentifier,
    string Reference);
