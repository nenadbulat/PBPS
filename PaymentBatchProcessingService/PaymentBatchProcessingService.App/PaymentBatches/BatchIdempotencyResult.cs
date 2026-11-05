namespace PaymentBatchProcessingService.App.PaymentBatches;

public sealed record BatchIdempotencyResult(
    Guid BatchId,
    string RequestFingerprint);
