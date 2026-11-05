namespace PaymentBatchProcessingService.App.PaymentProcessing;

public enum BankSubmissionOutcome
{
    Accepted,
    Rejected
}

public sealed record BankSubmissionResult(
    BankSubmissionOutcome Outcome,
    string? BankReference = null,
    string? RejectionReason = null);