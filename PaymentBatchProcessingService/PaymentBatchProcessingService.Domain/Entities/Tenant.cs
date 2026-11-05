using PaymentBatchProcessingService.Domain.Helpers;

namespace PaymentBatchProcessingService.Domain.Entities;

public sealed class Tenant
{
    public Guid Id { get; private set; }
    public string Name { get; private set; } = null!;
    public DateTime CreatedAtUtc { get; private set; }

    private Tenant()
    {
    }

    public Tenant(Guid id, string name, DateTime createdAtUtc)
    {
        if (id == Guid.Empty)
        {
            throw new ArgumentException("Tenant ID cannot be empty.", nameof(id));
        }

        Guard.ValidateUtc(createdAtUtc);

        Id = id;
        Name = Guard.ValidateName(name);
        CreatedAtUtc = createdAtUtc;
    }

    public void Rename(string name)
    {
        Name = Guard.ValidateName(name);
    }
}
