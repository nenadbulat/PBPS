using PaymentBatchProcessingService.Domain.Enums;
using PaymentBatchProcessingService.Domain.Helpers;

namespace PaymentBatchProcessingService.Domain.Entities;

public sealed class PaymentBatch
{
    public Guid Id { get; private set; }
    public Guid LegalEntityId { get; private set; }
    public Guid SourceBankAccountId { get; private set; }
    public Currency Currency { get; private set; }
    public PaymentBatchStatus Status { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }

    private PaymentBatch()
    {
    }

    public PaymentBatch(Guid id, Guid legalEntityId, Guid sourceBankAccountId, Currency currency, DateTime createdAtUtc)
    {
        if (id == Guid.Empty)
        {
            throw new ArgumentException("Payment batch ID cannot be empty.", nameof(id));
        }

        if (legalEntityId == Guid.Empty)
        {
            throw new ArgumentException("Legal entity ID cannot be empty.", nameof(legalEntityId));
        }

        if (sourceBankAccountId == Guid.Empty)
        {
            throw new ArgumentException("Source bank account ID cannot be empty.", nameof(sourceBankAccountId));
        }

        Guard.ValidateCurrency(currency);
        Guard.ValidateUtc(createdAtUtc);

        Id = id;
        LegalEntityId = legalEntityId;
        SourceBankAccountId = sourceBankAccountId;
        Currency = currency;
        Status = PaymentBatchStatus.Accepted;
        CreatedAtUtc = createdAtUtc;
    }

    public void MarkAsProcessing()
    {
        if (Status != PaymentBatchStatus.Accepted)
        {
            throw new InvalidOperationException("Only an accepted batch can start processing.");
        }

        Status = PaymentBatchStatus.Processing;
    }

    public void MarkAsCompleted()
    {
        if (Status != PaymentBatchStatus.Processing)
        {
            throw new InvalidOperationException("Only a processing batch can be completed.");
        }

        Status = PaymentBatchStatus.Completed;
    }
}
