namespace PaymentBatchProcessingService.App.Exceptions;

public sealed class BatchValidationException(string message) : Exception(message);
