using PaymentBatchProcessingService.Domain.Entities;
using PaymentBatchProcessingService.Domain.Enums;

namespace PaymentBatchProcessingService.UnitTests.Domain;

[TestFixture]
public sealed class PaymentAttemptTests
{
    private static readonly DateTime CreatedAtUtc = new(2026, 11, 9, 12, 0, 0, DateTimeKind.Utc);

    private static readonly DateTime CompletedAtUtc = CreatedAtUtc.AddSeconds(5);

    [Test]
    public void Constructor_WithValidArguments_ShouldCreatePendingAttempt()
    {
        var id = Guid.NewGuid();
        var paymentId = Guid.NewGuid();
        var attempt = new PaymentAttempt(id, paymentId, CreatedAtUtc);

        Assert.Multiple(() =>
        {
            Assert.That(attempt.Id, Is.EqualTo(id));
            Assert.That(attempt.PaymentId, Is.EqualTo(paymentId));
            Assert.That(attempt.Outcome, Is.EqualTo(PaymentAttemptOutcome.Pending));
            Assert.That(attempt.BankReference, Is.Null);
            Assert.That(attempt.FailureReason, Is.Null);
            Assert.That(attempt.CreatedAtUtc, Is.EqualTo(CreatedAtUtc));
            Assert.That(attempt.CompletedAtUtc, Is.Null);
        });
    }

    [Test]
    public void Constructor_WithEmptyId_ShouldThrow()
    {
        Assert.Throws<ArgumentException>(() => new PaymentAttempt(Guid.Empty, Guid.NewGuid(), CreatedAtUtc));
    }

    [Test]
    public void Constructor_WithEmptyPaymentId_ShouldThrow()
    {
        Assert.Throws<ArgumentException>(() => new PaymentAttempt(Guid.NewGuid(), Guid.Empty, CreatedAtUtc));
    }

    [TestCase(DateTimeKind.Local)]
    [TestCase(DateTimeKind.Unspecified)]
    public void Constructor_WithNonUtcDate_ShouldThrow(DateTimeKind kind)
    {
        var date = new DateTime(2026, 11, 9, 12, 0, 0, kind);

        Assert.Throws<ArgumentException>(() => new PaymentAttempt(Guid.NewGuid(), Guid.NewGuid(), date));
    }

    [Test]
    public void MarkAsAccepted_ShouldStoreBankReferenceAndCompletionTime()
    {
        var attempt = CreateAttempt();
        attempt.MarkAsAccepted("BANK-123", CompletedAtUtc);

        Assert.Multiple(() =>
        {
            Assert.That(attempt.Outcome, Is.EqualTo(PaymentAttemptOutcome.Accepted));
            Assert.That(attempt.BankReference, Is.EqualTo("BANK-123"));
            Assert.That(attempt.FailureReason, Is.Null);
            Assert.That(attempt.CompletedAtUtc, Is.EqualTo(CompletedAtUtc));
        });
    }

    [Test]
    public void MarkAsRejected_ShouldStoreReasonAndCompletionTime()
    {
        var attempt = CreateAttempt();
        attempt.MarkAsRejected("Invalid beneficiary account", CompletedAtUtc);

        Assert.Multiple(() =>
        {
            Assert.That(attempt.Outcome, Is.EqualTo(PaymentAttemptOutcome.Rejected));
            Assert.That(attempt.FailureReason, Is.EqualTo("Invalid beneficiary account"));
            Assert.That(attempt.CompletedAtUtc, Is.EqualTo(CompletedAtUtc));
        });
    }

    [Test]
    public void MarkAsUnknown_ShouldStoreReasonAndCompletionTime()
    {
        var attempt = CreateAttempt();
        attempt.MarkAsUnknown("Bank request timed out", CompletedAtUtc);

        Assert.Multiple(() =>
        {
            Assert.That(attempt.Outcome, Is.EqualTo(PaymentAttemptOutcome.Unknown));
            Assert.That(attempt.FailureReason, Is.EqualTo("Bank request timed out"));
            Assert.That(attempt.CompletedAtUtc, Is.EqualTo(CompletedAtUtc));
        });
    }

    [Test]
    public void MarkAsAccepted_WithEmptyReference_ShouldPreservePending()
    {
        var attempt = CreateAttempt();

        Assert.Throws<ArgumentException>(() => attempt.MarkAsAccepted(" ", CompletedAtUtc));

        Assert.Multiple(() =>
        {
            Assert.That(attempt.Outcome, Is.EqualTo(PaymentAttemptOutcome.Pending));
            Assert.That(attempt.CompletedAtUtc, Is.Null);
            Assert.That(attempt.BankReference, Is.Null);
        });
    }

    [Test]
    public void MarkAsRejected_WithEmptyReason_ShouldPreservePending()
    {
        var attempt = CreateAttempt();

        Assert.Throws<ArgumentException>(() => attempt.MarkAsRejected(" ", CompletedAtUtc));

        Assert.That(attempt.Outcome, Is.EqualTo(PaymentAttemptOutcome.Pending));
    }

    [Test]
    public void MarkAsUnknown_WithEmptyReason_ShouldPreservePending()
    {
        var attempt = CreateAttempt();

        Assert.Throws<ArgumentException>(() => attempt.MarkAsUnknown(" ", CompletedAtUtc));
        Assert.That(attempt.Outcome, Is.EqualTo(PaymentAttemptOutcome.Pending));
    }

    [Test]
    public void CompletionTime_BeforeCreationTime_ShouldThrow()
    {
        var attempt = CreateAttempt();

        Assert.Throws<ArgumentException>(() => attempt.MarkAsAccepted("BANK-123", CreatedAtUtc.AddSeconds(-1)));
        Assert.That(attempt.Outcome, Is.EqualTo(PaymentAttemptOutcome.Pending));
    }

    [TestCase(DateTimeKind.Local)]
    [TestCase(DateTimeKind.Unspecified)]
    public void CompletionTime_WithNonUtcDate_ShouldThrow(DateTimeKind kind)
    {
        var attempt = CreateAttempt();
        var date = new DateTime(2026, 11, 4, 10, 0, 5, kind);

        Assert.Throws<ArgumentException>(() => attempt.MarkAsUnknown("Timeout", date));
    }

    [Test]
    public void CompletedAttempt_ShouldNotAllowAnotherOutcome()
    {
        var attempt = CreateAttempt();
        attempt.MarkAsAccepted("BANK-123", CompletedAtUtc);

        Assert.Multiple(() =>
        {
            Assert.Throws<InvalidOperationException>(() => attempt.MarkAsAccepted("BANK-456", CompletedAtUtc));
            Assert.Throws<InvalidOperationException>(() => attempt.MarkAsRejected("Rejected", CompletedAtUtc));
            Assert.Throws<InvalidOperationException>(() => attempt.MarkAsUnknown("Timeout", CompletedAtUtc));
        });

        Assert.Multiple(() =>
        {
            Assert.That(attempt.Outcome, Is.EqualTo(PaymentAttemptOutcome.Accepted));
            Assert.That(attempt.BankReference, Is.EqualTo("BANK-123"));
            Assert.That(attempt.CompletedAtUtc, Is.EqualTo(CompletedAtUtc));
        });
    }

    private static PaymentAttempt CreateAttempt()
        => new(Guid.NewGuid(), Guid.NewGuid(), CreatedAtUtc);
}
