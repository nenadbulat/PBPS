using PaymentBatchProcessingService.Domain.Helpers;

namespace PaymentBatchProcessingService.Infrastructure.DB.Models;

public sealed class BatchIdempotencyRecord
{
    public Guid Id { get; private set; }
    public Guid TenantId { get; private set; }
    public Guid BatchId { get; private set; }
    public string IdempotencyKey { get; private set; } = null!;
    public string RequestFingerprint { get; private set; } = null!;
    public DateTime CreatedAtUtc { get; private set; }

    private BatchIdempotencyRecord()
    {
    }

    public BatchIdempotencyRecord(Guid tenantId, Guid batchId, string idempotencyKey, string requestFingerprint, DateTime createdAtUtc)
    {
        if (tenantId == Guid.Empty)
        {
            throw new ArgumentException("Tenant ID is required.", nameof(tenantId));
        }

        if (batchId == Guid.Empty)
        {
            throw new ArgumentException("Batch ID is required.", nameof(batchId));
        }

        IdempotencyKey = Guard.ValidateRequiredText(idempotencyKey, Constants.IdempotencyKeyLength, nameof(idempotencyKey));
        RequestFingerprint = Guard.ValidateRequiredText(requestFingerprint, Constants.RequestFingerprintLength, nameof(requestFingerprint));

        if (RequestFingerprint.Length != Constants.RequestFingerprintLength || !RequestFingerprint.All(Uri.IsHexDigit))
        {
            throw new ArgumentException("Request fingerprint must be a 64-character SHA-256 hexadecimal value.", nameof(requestFingerprint));
        }

        Guard.ValidateUtc(createdAtUtc);

        Id = Guid.NewGuid();
        TenantId = tenantId;
        BatchId = batchId;
        IdempotencyKey = idempotencyKey;
        RequestFingerprint = requestFingerprint;
        CreatedAtUtc = createdAtUtc;
    }
}
