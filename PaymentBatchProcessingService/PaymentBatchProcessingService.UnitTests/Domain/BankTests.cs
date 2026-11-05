using PaymentBatchProcessingService.Domain.Entities;
using PaymentBatchProcessingService.Domain.Helpers;

namespace PaymentBatchProcessingService.UnitTests.Domain;

[TestFixture]
public sealed class BankTests
{
    private static readonly DateTime CreatedAtUtc = new(2026, 11, 9, 12, 0, 0, DateTimeKind.Utc);

    [Test]
    public void Constructor_WithValidArguments_ShouldCreateBank()
    {
        var id = Guid.NewGuid();

        var bank = new Bank(id, "HSBC UK", "HBUKGB4B", CreatedAtUtc);

        Assert.Multiple(() =>
        {
            Assert.That(bank.Id, Is.EqualTo(id));
            Assert.That(bank.Name, Is.EqualTo("HSBC UK"));
            Assert.That(bank.Bic, Is.EqualTo("HBUKGB4B"));
            Assert.That(bank.CreatedAtUtc, Is.EqualTo(CreatedAtUtc));
        });
    }

    [Test]
    public void Constructor_ShouldNormalizeNameAndBic()
    {
        var bank = new Bank(Guid.NewGuid(), "  HSBC UK  ", " hbukgb4b ", CreatedAtUtc);

        Assert.Multiple(() =>
        {
            Assert.That(bank.Name, Is.EqualTo("HSBC UK"));
            Assert.That(bank.Bic, Is.EqualTo("HBUKGB4B"));
        });
    }

    [TestCase("HBUKGB4B")]
    [TestCase("HBUKGB4BXXX")]
    public void Constructor_WithValidBic_ShouldSucceed(string bic)
    {
        var bank = new Bank(Guid.NewGuid(), "HSBC UK", bic, CreatedAtUtc);

        Assert.That(bank.Bic, Is.EqualTo(bic));
    }

    [TestCase(null)]
    [TestCase("")]
    [TestCase("ABC")]
    [TestCase("HBUKGB4")]
    [TestCase("HBUKGB4BXX")]
    [TestCase("1234GB4B")]
    [TestCase("HBUK12AB")]
    [TestCase("HBUKGB@B")]
    public void Constructor_WithInvalidBic_ShouldThrow(string? bic)
    {
        Assert.Throws<ArgumentException>(() => new Bank(Guid.NewGuid(), "HSBC UK", bic!, CreatedAtUtc));
    }

    [Test]
    public void Constructor_WithEmptyId_ShouldThrow()
    {
        Assert.Throws<ArgumentException>(() => new Bank(Guid.Empty, "HSBC UK", "HBUKGB4B", CreatedAtUtc));
    }

    [TestCase(null)]
    [TestCase("")]
    [TestCase("   ")]
    public void Constructor_WithInvalidName_ShouldThrow(string? name)
    {
        Assert.Throws<ArgumentException>(() => new Bank(Guid.NewGuid(), name!, "HBUKGB4B", CreatedAtUtc));
    }

    [Test]
    public void Constructor_WithNameExceedingMaxLength_ShouldThrow()
    {
        var name = new string('A', Constants.MaxNameLength + 1);

        Assert.Throws<ArgumentException>(() => new Bank(Guid.NewGuid(), name, "HBUKGB4B", CreatedAtUtc));
    }

    [TestCase(DateTimeKind.Local)]
    [TestCase(DateTimeKind.Unspecified)]
    public void Constructor_WithNonUtcDate_ShouldThrow(DateTimeKind kind)
    {
        var date = new DateTime(2026, 11, 9, 12, 0, 0, kind);

        Assert.Throws<ArgumentException>(() => new Bank(Guid.NewGuid(), "HSBC UK", "HBUKGB4B", date));
    }

    [Test]
    public void Rename_WithValidName_ShouldUpdateName()
    {
        var bank = CreateBank();
        bank.Rename("HSBC Bank UK");

        Assert.That(bank.Name, Is.EqualTo("HSBC Bank UK"));
    }

    [Test]
    public void Rename_WithInvalidName_ShouldPreserveOriginalName()
    {
        var bank = CreateBank();

        Assert.Throws<ArgumentException>(() => bank.Rename(" "));
        Assert.That(bank.Name, Is.EqualTo("HSBC UK"));
    }

    [Test]
    public void Rename_ShouldNotChangeIdentityOrBic()
    {
        var bank = CreateBank();
        var originalId = bank.Id;
        var originalBic = bank.Bic;
        var originalCreatedAt = bank.CreatedAtUtc;
        bank.Rename("HSBC Bank UK");

        Assert.Multiple(() =>
        {
            Assert.That(bank.Id, Is.EqualTo(originalId));
            Assert.That(bank.Bic, Is.EqualTo(originalBic));
            Assert.That(bank.CreatedAtUtc, Is.EqualTo(originalCreatedAt));
        });
    }

    private static Bank CreateBank() => new(Guid.NewGuid(), "HSBC UK", "HBUKGB4B", CreatedAtUtc);
}
