using PaymentBatchProcessingService.Domain.Helpers;

namespace PaymentBatchProcessingService.Domain.Entities;

public sealed class LegalEntity
{
    public const int MaxLegalNameLength = 200;
    public const int MaxRegistrationNumberLength = 100;
    public Guid Id { get; private set; }
    public Guid TenantId { get; private set; }
    public string LegalName { get; private set; } = null!;
    public string RegistrationNumber { get; private set; } = null!;
    public string CountryCode { get; private set; } = null!;
    public DateTime CreatedAtUtc { get; private set; }

    private LegalEntity()
    {
    }

    public LegalEntity(Guid id, Guid tenantId, string legalName, string registrationNumber, string countryCode, DateTime createdAtUtc)
    {
        if (id == Guid.Empty)
        {
            throw new ArgumentException("Legal entity ID cannot be empty.", nameof(id));
        }

        if (tenantId == Guid.Empty)
        {
            throw new ArgumentException("Tenant ID cannot be empty.", nameof(tenantId));
        }

        Guard.ValidateUtc(createdAtUtc);

        Id = id;
        TenantId = tenantId;

        LegalName = Guard.ValidateRequiredText(legalName, MaxLegalNameLength, nameof(legalName));

        RegistrationNumber = Guard.ValidateRequiredText(
            registrationNumber,
            MaxRegistrationNumberLength,
            nameof(registrationNumber));

        CountryCode = Guard.ValidateCountryCode(countryCode);
        CreatedAtUtc = createdAtUtc;
    }

    public void Rename(string legalName)
    {
        LegalName = Guard.ValidateRequiredText(legalName, MaxLegalNameLength, nameof(legalName));
    }

    public void UpdateRegistrationDetails(string registrationNumber, string countryCode)
    {
        // Validate everything before changing the entity.
        var validatedRegistrationNumber = Guard.ValidateRequiredText(
            registrationNumber,
            MaxRegistrationNumberLength,
            nameof(registrationNumber));

        var validatedCountryCode = Guard.ValidateCountryCode(countryCode);

        RegistrationNumber = validatedRegistrationNumber;
        CountryCode = validatedCountryCode;
    }
}
