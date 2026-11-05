namespace PaymentBatchProcessingService.Domain.Enums;

public enum PaymentAttemptOutcome
{
    Pending = 0, //the payment attempt is still being processed
    Accepted = 1, //the payment attempt was accepted by the bank
    Rejected = 2, //the payment attempt was rejected by the bank
    Unknown = 3, //there is no reliable information from the bank
}
