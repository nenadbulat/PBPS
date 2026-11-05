using PaymentBatchProcessingService.Domain.Entities;
using PaymentBatchProcessingService.Domain.Enums;

namespace PaymentBatchProcessingService.UnitTests.Domain;

[TestFixture]
public sealed class PaymentBatchTests
{
    private static readonly DateTime CreatedAtUtc = new(2026, 11, 9, 12, 0, 0, DateTimeKind.Utc);

    [Test]
    public void Constructor_WithValidArguments_ShouldCreateAcceptedBatch()
    {
        var id = Guid.NewGuid();
        var legalEntityId = Guid.NewGuid();
        var accountId = Guid.NewGuid();
        var batch = new PaymentBatch(id, legalEntityId, accountId, Currency.GBP, CreatedAtUtc);

        Assert.Multiple(() =>
        {
            Assert.That(batch.Id, Is.EqualTo(id));
            Assert.That(batch.LegalEntityId, Is.EqualTo(legalEntityId));
            Assert.That(batch.SourceBankAccountId, Is.EqualTo(accountId));
            Assert.That(batch.Currency, Is.EqualTo(Currency.GBP));
            Assert.That(batch.Status, Is.EqualTo(PaymentBatchStatus.Accepted));
            Assert.That(batch.CreatedAtUtc, Is.EqualTo(CreatedAtUtc));
        });
    }

    [TestCase("id")]
    [TestCase("legalEntityId")]
    [TestCase("sourceBankAccountId")]
    public void Constructor_WithEmptyRequiredId_ShouldThrow(string parameter)
    {
        var id = Guid.NewGuid();
        var legalEntityId = Guid.NewGuid();
        var accountId = Guid.NewGuid();

        switch (parameter)
        {
            case "id":
                id = Guid.Empty;
                break;
            case "legalEntityId":
                legalEntityId = Guid.Empty;
                break;
            case "sourceBankAccountId":
                accountId = Guid.Empty;
                break;
        }

        Assert.Throws<ArgumentException>(() => new PaymentBatch(id, legalEntityId, accountId, Currency.GBP, CreatedAtUtc));
    }

    [Test]
    public void Constructor_WithUndefinedCurrency_ShouldThrow()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new PaymentBatch(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), (Currency)999, CreatedAtUtc));
    }

    [TestCase(DateTimeKind.Local)]
    [TestCase(DateTimeKind.Unspecified)]
    public void Constructor_WithNonUtcDate_ShouldThrow(DateTimeKind kind)
    {
        var date = new DateTime(2026, 11, 9, 12, 0, 0, kind);

        Assert.Throws<ArgumentException>(() =>
            new PaymentBatch(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Currency.GBP, date));
    }

    [Test]
    public void MarkAsProcessing_FromAccepted_ShouldChangeStatus()
    {
        var batch = CreateBatch();
        batch.MarkAsProcessing();

        Assert.That(batch.Status, Is.EqualTo(PaymentBatchStatus.Processing));
    }

    [Test]
    public void MarkAsProcessing_WhenAlreadyProcessing_ShouldThrow()
    {
        var batch = CreateBatch();
        batch.MarkAsProcessing();

        Assert.Throws<InvalidOperationException>(batch.MarkAsProcessing);
        Assert.That(batch.Status, Is.EqualTo(PaymentBatchStatus.Processing));
    }

    [Test]
    public void MarkAsCompleted_FromProcessing_ShouldChangeStatus()
    {
        var batch = CreateBatch();
        batch.MarkAsProcessing();
        batch.MarkAsCompleted();

        Assert.That(batch.Status, Is.EqualTo(PaymentBatchStatus.Completed));
    }

    [Test]
    public void MarkAsCompleted_FromAccepted_ShouldThrow()
    {
        var batch = CreateBatch();

        Assert.Throws<InvalidOperationException>(batch.MarkAsCompleted);

        Assert.That(batch.Status, Is.EqualTo(PaymentBatchStatus.Accepted));
    }

    [Test]
    public void CompletedBatch_ShouldNotAllowFurtherTransitions()
    {
        var batch = CreateBatch();
        batch.MarkAsProcessing();
        batch.MarkAsCompleted();

        Assert.Multiple(() =>
        {
            Assert.Throws<InvalidOperationException>(batch.MarkAsProcessing);
            Assert.Throws<InvalidOperationException>(batch.MarkAsCompleted);
        });

        Assert.That(batch.Status, Is.EqualTo(PaymentBatchStatus.Completed));
    }

    private static PaymentBatch CreateBatch()
        => new(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Currency.GBP, CreatedAtUtc);
}

