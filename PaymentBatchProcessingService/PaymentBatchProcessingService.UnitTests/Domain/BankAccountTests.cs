using PaymentBatchProcessingService.Domain.Entities;
using PaymentBatchProcessingService.Domain.Enums;

namespace PaymentBatchProcessingService.UnitTests.Domain;

[TestFixture]
public sealed class BankAccountTests
{
    private static readonly DateTime CreatedAtUtc = new(2026, 11, 9, 12, 0, 0, DateTimeKind.Utc);

    [Test]
    public void Constructor_WithValidArguments_ShouldCreateBankAccount()
    {
        var id = Guid.NewGuid();
        var legalEntityId = Guid.NewGuid();
        var bankId = Guid.NewGuid();
        var account = new BankAccount(id, legalEntityId, bankId, "GB82WEST12345698765432", Currency.GBP, CreatedAtUtc);

        Assert.Multiple(() =>
        {
            Assert.That(account.Id, Is.EqualTo(id));
            Assert.That(account.LegalEntityId, Is.EqualTo(legalEntityId));
            Assert.That(account.BankId, Is.EqualTo(bankId));
            Assert.That(account.AccountIdentifier, Is.EqualTo("GB82WEST12345698765432"));
            Assert.That(account.Currency, Is.EqualTo(Currency.GBP));
            Assert.That(account.CreatedAtUtc, Is.EqualTo(CreatedAtUtc));
        });
    }

    [Test]
    public void Constructor_ShouldNormalizeIban()
    {
        var account = CreateBankAccount("gb82 west 1234 5698 7654 32", Currency.GBP);

        Assert.Multiple(() =>
        {
            Assert.That(account.AccountIdentifier, Is.EqualTo("GB82WEST12345698765432"));
        });
    }

    [TestCase(null)]
    [TestCase("")]
    [TestCase("   ")]
    [TestCase("123456789012345")]
    [TestCase("GB1")]
    [TestCase("GBXXWEST12345698765432")]
    [TestCase("GB82WEST1234569876543@")]
    public void Constructor_WithInvalidIban_ShouldThrow(string? iban)
    {
        Assert.Throws<ArgumentException>(() => CreateBankAccount(iban!, Currency.RSD));
    }

    [Test]
    public void Constructor_WithIbanExceedingMaxLength_ShouldThrow()
    {
        var iban = "GB82" + new string('A', 31);

        Assert.Throws<ArgumentException>(() => CreateBankAccount(iban, Currency.RSD));
    }

    [Test]
    public void Constructor_WithEmptyId_ShouldThrow()
    {
        Assert.Throws<ArgumentException>(() =>
            new BankAccount(Guid.Empty, Guid.NewGuid(), Guid.NewGuid(), "GB82WEST12345698765432", Currency.GBP, CreatedAtUtc));
    }

    [Test]
    public void Constructor_WithEmptyLegalEntityId_ShouldThrow()
    {
        Assert.Throws<ArgumentException>(() =>
            new BankAccount(Guid.NewGuid(), Guid.Empty, Guid.NewGuid(), "GB82WEST12345698765432", Currency.GBP, CreatedAtUtc));
    }

    [Test]
    public void Constructor_WithEmptyBankId_ShouldThrow()
    {
        Assert.Throws<ArgumentException>(() =>
            new BankAccount(Guid.NewGuid(), Guid.NewGuid(), Guid.Empty, "GB82WEST12345698765432", Currency.GBP,CreatedAtUtc));
    }

    [TestCase(DateTimeKind.Local)]
    [TestCase(DateTimeKind.Unspecified)]
    public void Constructor_WithNonUtcDate_ShouldThrow(DateTimeKind kind)
    {
        var date = new DateTime(2026, 11, 9, 12, 0, 0, kind);

        Assert.Throws<ArgumentException>(() =>
            new BankAccount(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "GB82WEST12345698765432", Currency.GBP, date));
    }

    [Test]
    public void Constructor_WithUndefinedCurrency_ShouldThrow()
    {
        var date = new DateTime(2026, 11, 9, 12, 0, 0, DateTimeKind.Utc);

        Assert.Throws<ArgumentOutOfRangeException>(() => new BankAccount(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "GB82WEST12345698765432", (Currency)999, date));
    }

    private static BankAccount CreateBankAccount(string iban, Currency currency)
        => new(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), iban, currency, CreatedAtUtc);
}
