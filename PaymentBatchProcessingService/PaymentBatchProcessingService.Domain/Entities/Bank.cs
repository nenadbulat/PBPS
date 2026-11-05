
using PaymentBatchProcessingService.Domain.Helpers;

namespace PaymentBatchProcessingService.Domain.Entities;

public sealed class Bank
{
    public Guid Id { get; private set; }
    public string Name { get; private set; } = null!;
    public string Bic { get; private set; } = null!;
    public DateTime CreatedAtUtc { get; private set; }

    private Bank()
    {
    }

    public Bank(Guid id, string name, string bic, DateTime createdAtUtc)
    {
        if (id == Guid.Empty)
        {
            throw new ArgumentException("Bank ID cannot be empty.", nameof(id));
        }

        Guard.ValidateUtc(createdAtUtc);

        Id = id;
        Name = Guard.ValidateName(name);
        Bic = Guard.ValidateBic(bic);
        CreatedAtUtc = createdAtUtc;
    }

    public void Rename(string name)
    {
        Name = Guard.ValidateName(name);
    }
}
