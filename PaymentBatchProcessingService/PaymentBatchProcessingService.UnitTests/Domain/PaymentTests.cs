using PaymentBatchProcessingService.Domain.Entities;
using PaymentBatchProcessingService.Domain.Enums;
using PaymentBatchProcessingService.Domain.Helpers;
using System.Globalization;

namespace PaymentBatchProcessingService.UnitTests.Domain;

[TestFixture]
public sealed class PaymentTests
{
    private static readonly DateTime CreatedAtUtc = new(2026, 11, 9, 12, 0, 0, DateTimeKind.Utc);

    [Test]
    public void Constructor_WithValidArguments_ShouldCreateAcceptedPayment()
    {
        var id = Guid.NewGuid();
        var batchId = Guid.NewGuid();
        var payment = new Payment(id, batchId, 125.50m, "Example Supplier Ltd", "GB82WEST12345698765432", "INV-2026-001", CreatedAtUtc);

        Assert.Multiple(() =>
        {
            Assert.That(payment.Id, Is.EqualTo(id));
            Assert.That(payment.PaymentBatchId, Is.EqualTo(batchId));
            Assert.That(payment.Amount, Is.EqualTo(125.50m));
            Assert.That(payment.BeneficiaryName, Is.EqualTo("Example Supplier Ltd"));
            Assert.That(payment.BeneficiaryAccountIdentifier, Is.EqualTo("GB82WEST12345698765432"));
            Assert.That(payment.Reference, Is.EqualTo("INV-2026-001"));
            Assert.That(payment.Status, Is.EqualTo(PaymentStatus.Accepted));
            Assert.That(payment.BankReference, Is.Null);
            Assert.That(payment.SettlementReference, Is.Null);
            Assert.That(payment.LastStatusReason, Is.Null);
            Assert.That(payment.CreatedAtUtc, Is.EqualTo(CreatedAtUtc));
        });
    }

    [TestCase(0)]
    [TestCase(-1)]
    public void Constructor_WithNonPositiveAmount_ShouldThrow(decimal amount)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => CreatePayment(amount));
    }

    [Test]
    public void Constructor_WithEmptyId_ShouldThrow()
    {
        Assert.Throws<ArgumentException>(() =>
            new Payment(Guid.Empty, Guid.NewGuid(), 100m, "Supplier", "GB82WEST12345698765432", "REF-001", CreatedAtUtc));
    }

    [Test]
    public void Constructor_WithEmptyBatchId_ShouldThrow()
    {
        Assert.Throws<ArgumentException>(() =>
            new Payment(Guid.NewGuid(), Guid.Empty, 100m, "Supplier", "GB82WEST12345698765432", "REF-001", CreatedAtUtc));
    }

    [TestCase(null)]
    [TestCase("")]
    [TestCase("   ")]
    public void Constructor_WithInvalidBeneficiaryName_ShouldThrow(string? name)
    {
        Assert.Throws<ArgumentException>(() =>
            new Payment(Guid.NewGuid(), Guid.NewGuid(), 100m, name!, "GB82WEST12345698765432", "REF-001", CreatedAtUtc));
    }

    [TestCase(null)]
    [TestCase("")]
    [TestCase("GB1")]
    [TestCase("GBXXWEST12345698765432")]
    public void Constructor_WithInvalidBeneficiaryIban_ShouldThrow(string? iban)
    {
        Assert.Throws<ArgumentException>(() =>
            new Payment(Guid.NewGuid(), Guid.NewGuid(), 100m, "Supplier", iban!, "REF-001", CreatedAtUtc));
    }

    [Test]
    public void Constructor_WithAmountExceedingMax_ShouldThrow()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => CreatePayment(Constants.MaxAmount + 1m));
    }

    [Test]
    public void Constructor_WithTooManyDecimalPlaces_ShouldThrow()
    {
        var amount = 1m + 1m / (decimal)Math.Pow(10, Constants.AmountDecimalPlaces + 1);

        Assert.Throws<ArgumentException>(() => CreatePayment(amount));
    }

    [TestCase(null)]
    [TestCase("")]
    [TestCase("   ")]
    public void Constructor_WithInvalidReference_ShouldThrow(string? reference)
    {
        Assert.Throws<ArgumentException>(() =>
            new Payment(Guid.NewGuid(), Guid.NewGuid(), 100m, "Supplier", "GB82WEST12345698765432", reference!, CreatedAtUtc));
    }

    [Test]
    public void Constructor_WithReferenceExceedingMaxLength_ShouldThrow()
    {
        var reference = new string('R', Constants.MaxReferenceLength + 1);

        Assert.Throws<ArgumentException>(() =>
            new Payment(Guid.NewGuid(), Guid.NewGuid(), 100m, "Supplier", "GB82WEST12345698765432", reference, CreatedAtUtc));
    }

    [Test]
    public void Constructor_WithBeneficiaryNameExceedingMaxLength_ShouldThrow()
    {
        var name = new string('N', Constants.MaxNameLength + 1);

        Assert.Throws<ArgumentException>(() =>
            new Payment(Guid.NewGuid(), Guid.NewGuid(), 100m, name, "GB82WEST12345698765432", "REF-001", CreatedAtUtc));
    }

    [TestCase(DateTimeKind.Local)]
    [TestCase(DateTimeKind.Unspecified)]
    public void Constructor_WithNonUtcDate_ShouldThrow(DateTimeKind kind)
    {
        var createdAt = DateTime.SpecifyKind(CreatedAtUtc, kind);

        Assert.Throws<ArgumentException>(() =>
            new Payment(Guid.NewGuid(), Guid.NewGuid(), 100m, "Supplier", "GB82WEST12345698765432", "REF-001", createdAt));
    }

    [Test]
    public void Constructor_ShouldNormalizeTextAndIban()
    {
        var payment = new Payment(Guid.NewGuid(), Guid.NewGuid(), 100m, "  Supplier Ltd  ", "gb82 west 1234 5698 7654 32", "  INV-001  ", CreatedAtUtc);

        Assert.Multiple(() =>
        {
            Assert.That(payment.BeneficiaryName, Is.EqualTo("Supplier Ltd"));
            Assert.That(payment.BeneficiaryAccountIdentifier, Is.EqualTo("GB82WEST12345698765432"));
            Assert.That(payment.Reference, Is.EqualTo("INV-001"));
        });
    }

    [Test]
    public void MarkAsProcessing_FromAccepted_ShouldSucceed()
    {
        var payment = CreatePayment();
        payment.MarkAsProcessing();

        Assert.That(payment.Status, Is.EqualTo(PaymentStatus.Processing));
    }

    [Test]
    public void MarkAsSubmitted_FromProcessing_ShouldStoreBankReference()
    {
        var payment = CreatePayment();
        payment.MarkAsProcessing();
        payment.MarkAsSubmitted("BANK-123");

        Assert.Multiple(() =>
        {
            Assert.That(payment.Status, Is.EqualTo(PaymentStatus.Submitted));
            Assert.That(payment.BankReference, Is.EqualTo("BANK-123"));
        });
    }

    [Test]
    public void MarkAsRejected_FromProcessing_ShouldStoreReason()
    {
        var payment = CreatePayment();
        payment.MarkAsProcessing();
        payment.MarkAsRejected("Invalid beneficiary account");

        Assert.Multiple(() =>
        {
            Assert.That(payment.Status, Is.EqualTo(PaymentStatus.Rejected));
            Assert.That(payment.LastStatusReason, Is.EqualTo("Invalid beneficiary account"));
        });
    }

    [Test]
    public void MarkAsOutcomeUnknown_FromProcessing_ShouldSucceed()
    {
        var payment = CreatePayment();
        payment.MarkAsProcessing();
        payment.MarkAsUnknown("Bank request timed out");

        Assert.That(payment.Status, Is.EqualTo(PaymentStatus.Unknown));
    }

    [Test]
    public void MarkAsSubmitted_FromOutcomeUnknown_ShouldSucceed()
    {
        var payment = CreatePayment();
        payment.MarkAsProcessing();
        payment.MarkAsUnknown("Timeout");
        payment.MarkAsSubmitted("BANK-456");

        Assert.That(payment.Status, Is.EqualTo(PaymentStatus.Submitted));
    }

    [Test]
    public void ResumeAfterVerifiedNonExecution_ShouldReturnToProcessing()
    {
        var payment = CreatePayment();
        payment.MarkAsProcessing();
        payment.MarkAsUnknown("Timeout");
        payment.ResumeAfterVerifiedNonExecution("VERIFY-123");

        Assert.That(payment.Status, Is.EqualTo(PaymentStatus.Processing));
    }

    [Test]
    public void MarkAsSettled_ShouldPreserveBankReference()
    {
        var payment = CreatePayment();
        payment.MarkAsProcessing();
        payment.MarkAsSubmitted("BANK-123");
        payment.MarkAsSettled("SETTLEMENT-456");

        Assert.Multiple(() =>
        {
            Assert.That(payment.Status, Is.EqualTo(PaymentStatus.Settled));
            Assert.That(payment.BankReference, Is.EqualTo("BANK-123"));
            Assert.That(payment.SettlementReference, Is.EqualTo("SETTLEMENT-456"));
        });
    }

    [Test]
    public void MarkAsRejected_FromSubmitted_ShouldSucceed()
    {
        var payment = CreatePayment();
        payment.MarkAsProcessing();
        payment.MarkAsSubmitted("BANK-123");
        payment.MarkAsRejected("Bank rejected instruction");

        Assert.That(payment.Status, Is.EqualTo(PaymentStatus.Rejected));
    }

    [Test]
    public void RejectedPayment_ShouldNotAllowFurtherTransitions()
    {
        var payment = CreatePayment();
        payment.MarkAsProcessing();
        payment.MarkAsRejected("Rejected");

        Assert.Multiple(() =>
        {
            Assert.Throws<InvalidOperationException>(payment.MarkAsProcessing);
            Assert.Throws<InvalidOperationException>(() => payment.MarkAsSubmitted("BANK-123"));
            Assert.Throws<InvalidOperationException>(() => payment.MarkAsSettled("SETTLEMENT-123"));
        });
    }

    [Test]
    public void SettledPayment_ShouldNotAllowFurtherTransitions()
    {
        var payment = CreatePayment();
        payment.MarkAsProcessing();
        payment.MarkAsSubmitted("BANK-123");
        payment.MarkAsSettled("SETTLEMENT-123");

        Assert.Multiple(() =>
        {
            Assert.Throws<InvalidOperationException>(payment.MarkAsProcessing);
            Assert.Throws<InvalidOperationException>(() => payment.MarkAsRejected("Rejected"));
        });
    }

    [Test]
    public void MarkAsSubmitted_WithEmptyBankReference_ShouldPreserveStatus()
    {
        var payment = CreatePayment();
        payment.MarkAsProcessing();

        Assert.Throws<ArgumentException>(() => payment.MarkAsSubmitted(" "));
        Assert.That(payment.Status, Is.EqualTo(PaymentStatus.Processing));
    }

    [Test]
    public void MarkAsRejected_WithEmptyReason_ShouldPreserveStatus()
    {
        var payment = CreatePayment();
        payment.MarkAsProcessing();

        Assert.Throws<ArgumentException>(() => payment.MarkAsRejected(" "));
        Assert.That(payment.Status, Is.EqualTo(PaymentStatus.Processing));
    }

    [TestCase("0.0001")]
    [TestCase("12.1234")]
    [TestCase("125.50")]
    [TestCase("999999999999999.9999")]
    public void Constructor_WithValidAmount_ShouldAcceptAmount(string amountText)
    {
        var amount = decimal.Parse(amountText, CultureInfo.InvariantCulture);
        var payment = CreatePayment(amount);

        Assert.That(payment.Amount, Is.EqualTo(amount));
    }

    [TestCase("0")]
    [TestCase("-1")]
    [TestCase("1000000000000000")]
    public void Constructor_WithAmountOutsideAllowedRange_ShouldThrow(string amountText)
    {
        var amount = decimal.Parse(amountText, CultureInfo.InvariantCulture);

        Assert.Throws<ArgumentOutOfRangeException>(() => CreatePayment(amount));
    }

    [TestCase("0.00001")]
    [TestCase("12.12345")]
    public void Constructor_WithMoreThanFourSignificantDecimalPlaces_ShouldThrow(string amountText)
    {
        var amount = decimal.Parse(amountText, CultureInfo.InvariantCulture);

        Assert.Throws<ArgumentException>(() => CreatePayment(amount));
    }

    [TestCase("999999999999999.99999")]
    [TestCase("1000000000000000")]
    public void Constructor_OutOfRange_ShouldThrow(string amountText)
    {
        var amount = decimal.Parse(amountText, CultureInfo.InvariantCulture);

        Assert.Throws<ArgumentOutOfRangeException>(() => CreatePayment(amount));
    }

    private static Payment CreatePayment(decimal amount = 100m)
        => new(Guid.NewGuid(), Guid.NewGuid(), amount, "Example Supplier Ltd", "GB82WEST12345698765432", "INV-2026-001", CreatedAtUtc);
}
