using PaymentBatchProcessingService.Domain.Enums;
using PaymentBatchProcessingService.Domain.Helpers;

namespace PaymentBatchProcessingService.Domain.Entities;

public sealed class Payment
{
    public Guid Id { get; private set; }
    public Guid PaymentBatchId { get; private set; }
    public decimal Amount { get; private set; }
    public string BeneficiaryName { get; private set; } = null!;
    public string BeneficiaryAccountIdentifier { get; private set; } = null!;
    public string Reference { get; private set; } = null!;
    public PaymentStatus Status { get; private set; }
    public string? BankReference { get; private set; }
    public string? SettlementReference { get; private set; }
    public string? LastStatusReason { get; private set; }

    public DateTime CreatedAtUtc { get; private set; }

    private Payment()
    {
    }

    public Payment(Guid id, Guid paymentBatchId, decimal amount, string beneficiaryName, string beneficiaryAccountIdentifier, string reference, DateTime createdAtUtc)
    {
        if (id == Guid.Empty)
        {
            throw new ArgumentException("Payment ID cannot be empty.", nameof(id));
        }

        if (paymentBatchId == Guid.Empty)
        {
            throw new ArgumentException("Payment batch ID cannot be empty.", nameof(paymentBatchId));
        }

        Guard.ValidateAmount(amount);
        Guard.ValidateUtc(createdAtUtc);

        Id = id;
        PaymentBatchId = paymentBatchId;
        Amount = amount;
        BeneficiaryName = Guard.ValidateRequiredText(beneficiaryName, Constants.MaxNameLength, nameof(beneficiaryName));
        BeneficiaryAccountIdentifier = Guard.ValidateAccountIdentifier(beneficiaryAccountIdentifier);
        Reference = Guard.ValidateRequiredText(reference, Constants.MaxReferenceLength, nameof(reference));
        Status = PaymentStatus.Accepted;
        CreatedAtUtc = createdAtUtc;
    }

    public void MarkAsProcessing()
    {
        EnsureStatus(PaymentStatus.Accepted);
        Status = PaymentStatus.Processing;
        LastStatusReason = null;
    }

    public void MarkAsSubmitted(string bankReference)
    {
        EnsureStatus(PaymentStatus.Processing, PaymentStatus.Unknown);
        BankReference = Guard.ValidateRequiredText(bankReference,Constants.MaxBankReferenceLength, nameof(bankReference));
        Status = PaymentStatus.Submitted;
        LastStatusReason = null;
    }

    public void MarkAsRejected(string reason)
    {
        EnsureStatus(PaymentStatus.Processing, PaymentStatus.Submitted, PaymentStatus.Unknown);
        var validatedReason = Guard.ValidateRequiredText(reason, Constants.MaxReasonLength, nameof(reason));
        Status = PaymentStatus.Rejected;
        LastStatusReason = validatedReason;
    }

    public void MarkAsUnknown(string reason)
    {
        EnsureStatus(PaymentStatus.Processing);
        var validatedReason = Guard.ValidateRequiredText(reason, Constants.MaxReasonLength, nameof(reason));
        Status = PaymentStatus.Unknown;
        LastStatusReason = validatedReason;
    }

    public void MarkAsSettled(string settlementReference)
    {
        EnsureStatus(PaymentStatus.Submitted, PaymentStatus.Unknown);
        SettlementReference = Guard.ValidateRequiredText(settlementReference, Constants.MaxBankReferenceLength, nameof(settlementReference));
        Status = PaymentStatus.Settled;
        LastStatusReason = null;
    }

    public void ResumeAfterVerifiedNonExecution(string verificationReference)
    {
        EnsureStatus(PaymentStatus.Unknown);
        _ = Guard.ValidateRequiredText(verificationReference, Constants.MaxBankReferenceLength, nameof(verificationReference));
        Status = PaymentStatus.Processing;
        LastStatusReason = null;
    }

    private void EnsureStatus(params PaymentStatus[] allowedStatuses)
    {
        if (!allowedStatuses.Contains(Status))
        {
            throw new InvalidOperationException($"Cannot perform this operation while payment status is {Status}.");
        }
    }
}
