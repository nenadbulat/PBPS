using PaymentBatchProcessingService.Domain.Enums;
using PaymentBatchProcessingService.Domain.Helpers;

namespace PaymentBatchProcessingService.Domain.Entities;

public sealed class PaymentStatusHistory
{
    public Guid Id { get; private set; }
    public Guid PaymentId { get; private set; }
    public PaymentStatus FromStatus { get; private set; }
    public PaymentStatus ToStatus { get; private set; }
    public string? Reason { get; private set; }
    public DateTime ChangedAtUtc { get; private set; }

    private PaymentStatusHistory()
    {
    }

    public PaymentStatusHistory(Guid id, Guid paymentId, PaymentStatus fromStatus, PaymentStatus toStatus, DateTime changedAtUtc, string? reason = null)
    {
        if (id == Guid.Empty)
        {
            throw new ArgumentException("Payment status history ID cannot be empty.", nameof(id));
        }

        if (paymentId == Guid.Empty)
        {
            throw new ArgumentException("Payment ID cannot be empty.", nameof(paymentId));
        }

        if (!Enum.IsDefined(fromStatus))
        {
            throw new ArgumentOutOfRangeException(nameof(fromStatus));
        }

        if (!Enum.IsDefined(toStatus))
        {
            throw new ArgumentOutOfRangeException(nameof(toStatus));
        }

        if (fromStatus == toStatus)
        {
            throw new ArgumentException("Previous and new payment statuses must differ.", nameof(toStatus));
        }

        Guard.ValidateUtc(changedAtUtc);

        Id = id;
        PaymentId = paymentId;
        FromStatus = fromStatus;
        ToStatus = toStatus;
        ChangedAtUtc = changedAtUtc;
        Reason = Guard.ValidateReason(reason);
    }
}
