using PaymentBatchProcessingService.Domain.Entities;

namespace PaymentBatchProcessingService.UnitTests.Domain;

[TestFixture]
public sealed class LegalEntityTests
{
    private static readonly DateTime CreatedAtUtc = new(2026, 11, 9, 12, 0, 0, DateTimeKind.Utc);

    [Test]
    public void Constructor_WithValidArguments_ShouldCreateLegalEntity()
    {
        var id = Guid.NewGuid();
        var tenantId = Guid.NewGuid();
        var entity = new LegalEntity(id, tenantId, "Acme UK Ltd", "12345678", "GB", CreatedAtUtc);

        Assert.Multiple(() =>
        {
            Assert.That(entity.Id, Is.EqualTo(id));
            Assert.That(entity.TenantId, Is.EqualTo(tenantId));
            Assert.That(entity.LegalName, Is.EqualTo("Acme UK Ltd"));
            Assert.That(entity.RegistrationNumber, Is.EqualTo("12345678"));
            Assert.That(entity.CountryCode, Is.EqualTo("GB"));
            Assert.That(entity.CreatedAtUtc, Is.EqualTo(CreatedAtUtc));
        });
    }

    [Test]
    public void Constructor_ShouldNormalizeTextAndCountryCode()
    {
        var entity = new LegalEntity(Guid.NewGuid(), Guid.NewGuid(), "  Acme UK Ltd  ", "  00123456  ", " gb ", CreatedAtUtc);

        Assert.Multiple(() =>
        {
            Assert.That(entity.LegalName, Is.EqualTo("Acme UK Ltd"));
            Assert.That(entity.RegistrationNumber, Is.EqualTo("00123456"));
            Assert.That(entity.CountryCode, Is.EqualTo("GB"));
        });
    }

    [Test]
    public void Constructor_WithEmptyId_ShouldThrow()
    {
        Assert.Throws<ArgumentException>(() =>
            new LegalEntity(Guid.Empty, Guid.NewGuid(), "Acme UK Ltd", "12345678", "GB", CreatedAtUtc));
    }

    [Test]
    public void Constructor_WithEmptyTenantId_ShouldThrow()
    {
        Assert.Throws<ArgumentException>(() =>
            new LegalEntity(Guid.NewGuid(), Guid.Empty, "Acme UK Ltd", "12345678", "GB", CreatedAtUtc));
    }

    [TestCase(null)]
    [TestCase("")]
    [TestCase("   ")]
    public void Constructor_WithInvalidLegalName_ShouldThrow(string? legalName)
    {
        Assert.Throws<ArgumentException>(() =>
            new LegalEntity(Guid.NewGuid(), Guid.NewGuid(), legalName!, "12345678", "GB", CreatedAtUtc));
    }

    [Test]
    public void Constructor_WithLegalNameExceedingMaxLength_ShouldThrow()
    {
        var legalName = new string('A', LegalEntity.MaxLegalNameLength + 1);

        Assert.Throws<ArgumentException>(() =>
            new LegalEntity(Guid.NewGuid(), Guid.NewGuid(), legalName, "12345678", "GB", CreatedAtUtc));
    }

    [TestCase(null)]
    [TestCase("")]
    [TestCase("   ")]
    public void Constructor_WithInvalidRegistrationNumber_ShouldThrow(string? registrationNumber)
    {
        Assert.Throws<ArgumentException>(() =>
            new LegalEntity(Guid.NewGuid(), Guid.NewGuid(), "Acme UK Ltd", registrationNumber!, "GB", CreatedAtUtc));
    }

    [Test]
    public void Constructor_WithRegistrationNumberExceedingMaxLength_ShouldThrow()
    {
        var registrationNumber = new string('1', LegalEntity.MaxRegistrationNumberLength + 1);

        Assert.Throws<ArgumentException>(() =>
            new LegalEntity(Guid.NewGuid(), Guid.NewGuid(), "Acme UK Ltd", registrationNumber, "GB", CreatedAtUtc));
    }

    [TestCase(null)]
    [TestCase("")]
    [TestCase("G")]
    [TestCase("GBR")]
    [TestCase("G1")]
    [TestCase("12")]
    public void Constructor_WithInvalidCountryCode_ShouldThrow(string? countryCode)
    {
        Assert.Throws<ArgumentException>(() =>
            new LegalEntity(Guid.NewGuid(), Guid.NewGuid(), "Acme UK Ltd", "12345678", countryCode!, CreatedAtUtc));
    }

    [TestCase(DateTimeKind.Local)]
    [TestCase(DateTimeKind.Unspecified)]
    public void Constructor_WithNonUtcDate_ShouldThrow(DateTimeKind kind)
    {
        var date = new DateTime(2026, 11, 3, 12, 0, 0, kind);

        Assert.Throws<ArgumentException>(() =>
            new LegalEntity(Guid.NewGuid(), Guid.NewGuid(), "Acme UK Ltd", "12345678", "GB", date));
    }

    [Test]
    public void Rename_WithValidName_ShouldUpdateLegalName()
    {
        var entity = CreateLegalEntity();
        entity.Rename("Acme International Ltd");

        Assert.That(entity.LegalName,Is.EqualTo("Acme International Ltd"));
    }

    [Test]
    public void Rename_WithInvalidName_ShouldPreserveOriginalName()
    {
        var entity = CreateLegalEntity();

        Assert.Throws<ArgumentException>(() => entity.Rename(" "));
        Assert.That(entity.LegalName, Is.EqualTo("Acme UK Ltd"));
    }

    [Test]
    public void UpdateRegistrationDetails_WithValidValues_ShouldUpdateBoth()
    {
        var entity = CreateLegalEntity();
        entity.UpdateRegistrationDetails("DE-98765", "de");

        Assert.Multiple(() =>
        {
            Assert.That(entity.RegistrationNumber, Is.EqualTo("DE-98765"));
            Assert.That(entity.CountryCode, Is.EqualTo("DE"));
        });
    }

    [Test]
    public void UpdateRegistrationDetails_WithInvalidCountryCode_ShouldPreserveBoth()
    {
        var entity = CreateLegalEntity();

        Assert.Throws<ArgumentException>(() => entity.UpdateRegistrationDetails("DE-98765", "INVALID"));
        Assert.Multiple(() =>
        {
            Assert.That(entity.RegistrationNumber, Is.EqualTo("12345678"));
            Assert.That(entity.CountryCode, Is.EqualTo("GB"));
        });
    }

    [Test]
    public void UpdateRegistrationDetails_WithInvalidRegistrationNumber_ShouldPreserveBoth()
    {
        var entity = CreateLegalEntity();

        Assert.Throws<ArgumentException>(() => entity.UpdateRegistrationDetails(" ", "DE"));
        Assert.Multiple(() =>
        {
            Assert.That(entity.RegistrationNumber, Is.EqualTo("12345678"));
            Assert.That(entity.CountryCode, Is.EqualTo("GB"));
        });
    }

    [Test]
    public void UpdatingDetails_ShouldNotChangeIdentityOrTenant()
    {
        var entity = CreateLegalEntity();
        var originalId = entity.Id;
        var originalTenantId = entity.TenantId;
        var originalCreatedAt = entity.CreatedAtUtc;
        entity.Rename("Acme International Ltd");
        entity.UpdateRegistrationDetails("98765432", "DE");

        Assert.Multiple(() =>
        {
            Assert.That(entity.Id, Is.EqualTo(originalId));
            Assert.That(entity.TenantId, Is.EqualTo(originalTenantId));
            Assert.That(entity.CreatedAtUtc, Is.EqualTo(originalCreatedAt));
        });
    }

    private static LegalEntity CreateLegalEntity()
        => new(Guid.NewGuid(), Guid.NewGuid(), "Acme UK Ltd", "12345678", "GB", CreatedAtUtc);
}
