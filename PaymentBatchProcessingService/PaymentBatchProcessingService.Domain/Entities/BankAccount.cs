
using PaymentBatchProcessingService.Domain.Enums;
using PaymentBatchProcessingService.Domain.Helpers;

namespace PaymentBatchProcessingService.Domain.Entities;

public sealed class BankAccount
{
    public Guid Id { get; private set; }
    public Guid LegalEntityId { get; private set; }
    public Guid BankId { get; private set; }
    public string AccountIdentifier { get; private set; } = null!;
    public Currency Currency { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }

    private BankAccount()
    {
    }

    public BankAccount(Guid id, Guid legalEntityId, Guid bankId, string accountIdentifier, Currency currency, DateTime createdAtUtc)
    {
        if (id == Guid.Empty)
        {
            throw new ArgumentException("Bank account ID cannot be empty.", nameof(id));
        }

        if (legalEntityId == Guid.Empty)
        {
            throw new ArgumentException("Legal entity ID cannot be empty.", nameof(legalEntityId));
        }

        if (bankId == Guid.Empty)
        {
            throw new ArgumentException("Bank ID cannot be empty.", nameof(bankId));
        }

        Guard.ValidateCurrency(currency);
        Guard.ValidateUtc(createdAtUtc);

        Id = id;
        LegalEntityId = legalEntityId;
        BankId = bankId;
        AccountIdentifier = Guard.ValidateAccountIdentifier(accountIdentifier);
        Currency = currency;
        CreatedAtUtc = createdAtUtc;
    }

    
}
