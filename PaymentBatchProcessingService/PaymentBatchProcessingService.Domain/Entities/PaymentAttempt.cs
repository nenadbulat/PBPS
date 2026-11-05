using PaymentBatchProcessingService.Domain.Enums;
using PaymentBatchProcessingService.Domain.Helpers;

namespace PaymentBatchProcessingService.Domain.Entities;

public sealed class PaymentAttempt
{
    public Guid Id { get; private set; }
    public Guid PaymentId { get; private set; }
    public PaymentAttemptOutcome Outcome { get; private set; }
    public string? BankReference { get; private set; }
    public string? FailureReason { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }
    public DateTime? CompletedAtUtc { get; private set; }

    private PaymentAttempt()
    {
    }

    public PaymentAttempt(Guid id, Guid paymentId, DateTime createdAtUtc)
    {
        if (id == Guid.Empty)
        {
            throw new ArgumentException("Payment attempt ID cannot be empty.", nameof(id));
        }

        if (paymentId == Guid.Empty)
        {
            throw new ArgumentException("Payment ID cannot be empty.", nameof(paymentId));
        }

        Guard.ValidateUtc(createdAtUtc);

        Id = id;
        PaymentId = paymentId;
        Outcome = PaymentAttemptOutcome.Pending;
        CreatedAtUtc = createdAtUtc;
    }

    public void MarkAsRejected(string reason, DateTime completedAtUtc)
    {
        EnsurePending();
        Guard.ValidateCompletionTime(completedAtUtc, CreatedAtUtc);
        var validatedReason = Guard.ValidateRequiredText(reason, Constants.MaxReasonLength, nameof(reason));

        FailureReason = validatedReason;
        Outcome = PaymentAttemptOutcome.Rejected;
        CompletedAtUtc = completedAtUtc;
    }

    public void MarkAsUnknown(string reason, DateTime completedAtUtc)
    {
        EnsurePending();
        Guard.ValidateCompletionTime(completedAtUtc, CreatedAtUtc);
        var validatedReason = Guard.ValidateRequiredText(reason, Constants.MaxReasonLength, nameof(reason));

        FailureReason = validatedReason;
        Outcome = PaymentAttemptOutcome.Unknown;
        CompletedAtUtc = completedAtUtc;
    }

    public void MarkAsAccepted(string bankReference, DateTime completedAtUtc)
    {
        EnsurePending();
        Guard.ValidateCompletionTime(completedAtUtc, CreatedAtUtc);
        var validatedReference = Guard.ValidateRequiredText(bankReference, Constants.MaxBankReferenceLength, nameof(bankReference));

        BankReference = validatedReference;
        Outcome = PaymentAttemptOutcome.Accepted;
        CompletedAtUtc = completedAtUtc;
    }

    private void EnsurePending()
    {
        if (Outcome != PaymentAttemptOutcome.Pending)
        {
            throw new InvalidOperationException($"Attempt is already completed with outcome {Outcome}.");
        }
    }
}
