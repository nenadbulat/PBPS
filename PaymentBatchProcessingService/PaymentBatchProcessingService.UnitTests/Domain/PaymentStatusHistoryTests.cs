using PaymentBatchProcessingService.Domain.Entities;
using PaymentBatchProcessingService.Domain.Enums;
using PaymentBatchProcessingService.Domain.Helpers;

namespace PaymentBatchProcessingService.UnitTests.Domain;

[TestFixture]
public sealed class PaymentStatusHistoryTests
{
    private static readonly DateTime ChangedAtUtc = new(2026, 11, 9, 12, 0, 0, DateTimeKind.Utc);

    [Test]
    public void Constructor_WithValidArguments_ShouldCreateHistory()
    {
        var id = Guid.NewGuid();
        var paymentId = Guid.NewGuid();
        var history = new PaymentStatusHistory(id, paymentId, PaymentStatus.Accepted, PaymentStatus.Processing, ChangedAtUtc, "Worker claimed payment");

        Assert.Multiple(() =>
        {
            Assert.That(history.Id, Is.EqualTo(id));
            Assert.That(history.PaymentId, Is.EqualTo(paymentId));
            Assert.That(history.FromStatus, Is.EqualTo(PaymentStatus.Accepted));
            Assert.That(history.ToStatus, Is.EqualTo(PaymentStatus.Processing));
            Assert.That(history.ChangedAtUtc, Is.EqualTo(ChangedAtUtc));
            Assert.That(history.Reason, Is.EqualTo("Worker claimed payment"));
        });
    }

    [Test]
    public void Constructor_WithoutReason_ShouldAllowNull()
    {
        var history = CreateHistory(reason: null);

        Assert.That(history.Reason, Is.Null);
    }

    [Test]
    public void Constructor_ShouldTrimReason()
    {
        var history = CreateHistory(reason: "  Bank confirmed receipt  ");

        Assert.That(history.Reason, Is.EqualTo("Bank confirmed receipt"));
    }

    [Test]
    public void Constructor_WithEmptyId_ShouldThrow()
    {
        Assert.Throws<ArgumentException>(() =>
            new PaymentStatusHistory(Guid.Empty, Guid.NewGuid(), PaymentStatus.Accepted, PaymentStatus.Processing, ChangedAtUtc));
    }

    [Test]
    public void Constructor_WithEmptyPaymentId_ShouldThrow()
    {
        Assert.Throws<ArgumentException>(() =>
            new PaymentStatusHistory(Guid.NewGuid(), Guid.Empty, PaymentStatus.Accepted, PaymentStatus.Processing, ChangedAtUtc));
    }

    [Test]
    public void Constructor_WithSameStatuses_ShouldThrow()
    {
        Assert.Throws<ArgumentException>(() =>
            new PaymentStatusHistory(Guid.NewGuid(), Guid.NewGuid(), PaymentStatus.Processing, PaymentStatus.Processing, ChangedAtUtc));
    }

    [Test]
    public void Constructor_WithUndefinedFromStatus_ShouldThrow()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new PaymentStatusHistory(Guid.NewGuid(), Guid.NewGuid(), (PaymentStatus)999, PaymentStatus.Processing, ChangedAtUtc));
    }

    [Test]
    public void Constructor_WithUndefinedToStatus_ShouldThrow()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new PaymentStatusHistory(Guid.NewGuid(), Guid.NewGuid(), PaymentStatus.Accepted, (PaymentStatus)999, ChangedAtUtc));
    }

    [TestCase(DateTimeKind.Local)]
    [TestCase(DateTimeKind.Unspecified)]
    public void Constructor_WithNonUtcDate_ShouldThrow(DateTimeKind kind)
    {
        var date = new DateTime(2026, 11, 9, 12, 0, 0, kind);

        Assert.Throws<ArgumentException>(() =>
            new PaymentStatusHistory(Guid.NewGuid(), Guid.NewGuid(), PaymentStatus.Accepted, PaymentStatus.Processing, date));
    }

    [TestCase("")]
    [TestCase(" ")]
    [TestCase("   ")]
    public void Constructor_WithWhitespaceReason_ShouldThrow(string reason)
    {
        Assert.Throws<ArgumentException>(() => CreateHistory(reason));
    }

    [Test]
    public void Constructor_WithReasonExceedingMaxLength_ShouldThrow()
    {
        var reason = new string('A', Constants.MaxReasonLength + 1);

        Assert.Throws<ArgumentException>(() => CreateHistory(reason));
    }

    [Test]
    public void Constructor_WithReasonAtMaxLength_ShouldSucceed()
    {
        var reason = new string('A', Constants.MaxReasonLength);
        var history = CreateHistory(reason);

        Assert.That(history.Reason, Is.EqualTo(reason));
    }

    [Test]
    public void Constructor_ShouldPreserveUnknownStatus()
    {
        var history = new PaymentStatusHistory(Guid.NewGuid(), Guid.NewGuid(), PaymentStatus.Processing, PaymentStatus.Unknown, ChangedAtUtc, "Bank request timed out");

        Assert.Multiple(() =>
        {
            Assert.That(history.FromStatus, Is.EqualTo(PaymentStatus.Processing));
            Assert.That(history.ToStatus, Is.EqualTo(PaymentStatus.Unknown));
        });
    }

    private static PaymentStatusHistory CreateHistory(string? reason = "Status changed")
        => new(Guid.NewGuid(), Guid.NewGuid(), PaymentStatus.Accepted, PaymentStatus.Processing, ChangedAtUtc, reason);
}
