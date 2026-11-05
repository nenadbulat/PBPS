using PaymentBatchProcessingService.Domain.Entities;
using PaymentBatchProcessingService.Domain.Helpers;

namespace PaymentBatchProcessingService.UnitTests.Domain;

[TestFixture]
public sealed class TenantTests
{
    private static readonly DateTime CreatedAtUtc = new(2026, 11, 9, 12, 0, 0, DateTimeKind.Utc);

    [Test]
    public void Constructor_WithValidArguments_ShouldCreateTenant()
    {
        var id = Guid.NewGuid();
        var tenant = new Tenant(id, "Acme Group", CreatedAtUtc);

        Assert.Multiple(() =>
        {
            Assert.That(tenant.Id, Is.EqualTo(id));
            Assert.That(tenant.Name, Is.EqualTo("Acme Group"));
            Assert.That(tenant.CreatedAtUtc, Is.EqualTo(CreatedAtUtc));
        });
    }

    [Test]
    public void Constructor_ShouldTrimName()
    {
        var tenant = new Tenant(Guid.NewGuid(), "  Acme Group  ", CreatedAtUtc);

        Assert.That(tenant.Name, Is.EqualTo("Acme Group"));
    }

    [Test]
    public void Constructor_WithEmptyId_ShouldThrow()
    {
        Assert.Throws<ArgumentException>(() => new Tenant(Guid.Empty, "Acme Group", CreatedAtUtc));
    }

    [TestCase(null)]
    [TestCase("")]
    [TestCase("   ")]
    public void Constructor_WithInvalidName_ShouldThrow(string? name)
    {
        Assert.Throws<ArgumentException>(() => new Tenant(Guid.NewGuid(), name!, CreatedAtUtc));
    }

    [Test]
    public void Constructor_WithNameExceedingMaxLength_ShouldThrow()
    {
        var name = new string('A', Constants.MaxNameLength + 1);

        Assert.Throws<ArgumentException>(() => new Tenant(Guid.NewGuid(), name, CreatedAtUtc));
    }

    [Test]
    public void Constructor_WithNameAtMaxLength_ShouldSucceed()
    {
        var name = new string('A', Constants.MaxNameLength);
        var tenant = new Tenant(Guid.NewGuid(), name, CreatedAtUtc);

        Assert.That(tenant.Name, Has.Length.EqualTo(Constants.MaxNameLength));
    }

    [TestCase(DateTimeKind.Local)]
    [TestCase(DateTimeKind.Unspecified)]
    public void Constructor_WithNonUtcDate_ShouldThrow(DateTimeKind kind)
    {
        var createdAt = new DateTime(2026, 11, 3, 12, 0, 0, kind);

        Assert.Throws<ArgumentException>(() => new Tenant(Guid.NewGuid(), "Acme Group", createdAt));
    }

    [Test]
    public void Rename_WithValidName_ShouldUpdateName()
    {
        var tenant = CreateTenant();
        tenant.Rename("New Company Name");

        Assert.That(tenant.Name, Is.EqualTo("New Company Name"));
    }

    [Test]
    public void Rename_ShouldTrimName()
    {
        var tenant = CreateTenant();
        tenant.Rename("  New Company Name  ");

        Assert.That(tenant.Name, Is.EqualTo("New Company Name"));
    }

    [TestCase(null)]
    [TestCase("")]
    [TestCase("   ")]
    public void Rename_WithInvalidName_ShouldThrowAndPreserveName(string? name)
    {
        var tenant = CreateTenant();

        Assert.Throws<ArgumentException>(() => tenant.Rename(name!));
        Assert.That(tenant.Name, Is.EqualTo("Acme Group"));
    }

    [Test]
    public void Rename_WithNameExceedingMaxLength_ShouldThrowAndPreserveName()
    {
        var tenant = CreateTenant();
        var invalidName = new string('A', Constants.MaxNameLength + 1);

        Assert.Throws<ArgumentException>(() => tenant.Rename(invalidName));
        Assert.That(tenant.Name, Is.EqualTo("Acme Group"));
    }

    [Test]
    public void Rename_ShouldNotChangeIdentityOrCreationTime()
    {
        var tenant = CreateTenant();
        var originalId = tenant.Id;
        var originalCreatedAt = tenant.CreatedAtUtc;
        tenant.Rename("New Company Name");

        Assert.Multiple(() =>
        {
            Assert.That(tenant.Id, Is.EqualTo(originalId));
            Assert.That(tenant.CreatedAtUtc, Is.EqualTo(originalCreatedAt));
        });
    }

    private static Tenant CreateTenant() => new(Guid.NewGuid(), "Acme Group", CreatedAtUtc);
}
