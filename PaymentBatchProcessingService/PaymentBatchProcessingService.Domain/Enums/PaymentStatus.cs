namespace PaymentBatchProcessingService.Domain.Enums;

public enum PaymentStatus
{
    Accepted = 0, //system accepted and saved the payment, but not yet processed by the bank
    Processing = 1, //payment is being processed by the worker
    Submitted = 2, //bank aknowledged the payment and is processing it
    Rejected = 3, //bank rejected the payment, it will not be processed
    Unknown = 4, //we don't know if the bank accepted the payment
    Settled = 5, //bank processed the payment and it was successful
}
