namespace PaymentBatchProcessingService.App.Exceptions;

public sealed class BatchNotFoundException() : Exception("Payment batch was not found.");
