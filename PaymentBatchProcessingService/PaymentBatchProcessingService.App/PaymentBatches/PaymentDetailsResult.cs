using PaymentBatchProcessingService.Domain.Enums;

namespace PaymentBatchProcessingService.App.PaymentBatches;

public sealed record PaymentDetailsResult(
    Guid Id,
    Guid PaymentBatchId,
    decimal Amount,
    string BeneficiaryName,
    string BeneficiaryAccountIdentifier,
    string Reference,
    PaymentStatus Status,
    string? BankReference,
    string? SettlementReference,
    string? LastStatusReason,
    DateTime CreatedAtUtc);