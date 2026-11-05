using Microsoft.Extensions.Logging.Abstractions;
using NUnit.Framework;
using PaymentBatchProcessingService.App.Interfaces;
using PaymentBatchProcessingService.App.PaymentProcessing;
using PaymentBatchProcessingService.Domain.Entities;
using PaymentBatchProcessingService.Domain.Enums;

namespace PaymentBatchProcessingService.UnitTests.PaymentProcessing;

[TestFixture]
public sealed class ProcessPaymentServiceTests
{
    [Test]
    public async Task ProcessNextAsync_WhenBankAccepts_MarksPaymentAsSubmitted()
    {
        var store = new FakeProcessingStore();

        var gateway = new FakeBankPaymentGateway
        {
            Result = new BankSubmissionResult(
                BankSubmissionOutcome.Accepted,
                BankReference: "BANK-123")
        };

        var service = CreateService(store, gateway);

        var processed = await service.ProcessNextAsync(
            CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(processed, Is.True);
            Assert.That(store.Payment.Status, Is.EqualTo(PaymentStatus.Submitted));
            Assert.That(store.Attempt.Outcome, Is.EqualTo(PaymentAttemptOutcome.Accepted));
            Assert.That(store.Payment.BankReference, Is.EqualTo("BANK-123"));
            Assert.That(store.CompletionCount, Is.EqualTo(1));
            Assert.That(store.History?.FromStatus, Is.EqualTo(PaymentStatus.Processing));
            Assert.That(store.History?.ToStatus, Is.EqualTo(PaymentStatus.Submitted));
        });
    }

    [Test]
    public async Task ProcessNextAsync_WhenBankRejects_MarksPaymentAsRejected()
    {
        var store = new FakeProcessingStore();

        var gateway = new FakeBankPaymentGateway
        {
            Result = new BankSubmissionResult(
                BankSubmissionOutcome.Rejected,
                RejectionReason: "Insufficient funds.")
        };

        var service = CreateService(store, gateway);

        var processed = await service.ProcessNextAsync(
            CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(processed, Is.True);
            Assert.That(store.Payment.Status, Is.EqualTo(PaymentStatus.Rejected));
            Assert.That(store.Attempt.Outcome, Is.EqualTo(PaymentAttemptOutcome.Rejected));
            Assert.That(store.Payment.LastStatusReason, Is.EqualTo("Insufficient funds."));
            Assert.That(store.CompletionCount, Is.EqualTo(1));
            Assert.That(store.History?.ToStatus, Is.EqualTo(PaymentStatus.Rejected));
        });
    }

    [Test]
    public async Task ProcessNextAsync_WhenBankTimesOut_MarksPaymentAsUnknown()
    {
        var store = new FakeProcessingStore();

        var gateway = new FakeBankPaymentGateway
        {
            ExceptionToThrow = new TimeoutException(
                "The bank did not respond.")
        };

        var service = CreateService(store, gateway);

        var processed = await service.ProcessNextAsync(
            CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(processed, Is.True);
            Assert.That(store.Payment.Status, Is.EqualTo(PaymentStatus.Unknown));
            Assert.That(store.Attempt.Outcome, Is.EqualTo(PaymentAttemptOutcome.Unknown));
            Assert.That(store.CompletionCount, Is.EqualTo(1));
            Assert.That(store.History?.ToStatus, Is.EqualTo(PaymentStatus.Unknown));
        });
    }

    private static ProcessPaymentService CreateService(
        IPaymentProcessingStore store,
        IBankPaymentGateway gateway)
    {
        return new ProcessPaymentService(
            store,
            gateway,
            TimeProvider.System,
            NullLogger<ProcessPaymentService>.Instance);
    }

    private sealed class FakeProcessingStore : IPaymentProcessingStore
    {
        public Payment Payment { get; } = new(
            Guid.NewGuid(),
            Guid.NewGuid(),
            125.50m,
            "Test Beneficiary",
            "GB82WEST12345698765432",
            "TEST-REFERENCE",
            DateTime.UtcNow.AddMinutes(-1));

        public PaymentAttempt Attempt { get; private set; } = null!;

        public PaymentStatusHistory? History { get; private set; }

        public int CompletionCount { get; private set; }

        private bool _started;

        public Task<(Payment Payment, PaymentAttempt Attempt)?>
            TryStartNextPaymentAsync(CancellationToken cancellationToken)
        {
            if (_started)
            {
                return Task.FromResult<
                    (Payment Payment, PaymentAttempt Attempt)?>(null);
            }

            _started = true;

            Payment.MarkAsProcessing();

            Attempt = new PaymentAttempt(
                Guid.NewGuid(),
                Payment.Id,
                DateTime.UtcNow.AddSeconds(-1));

            return Task.FromResult<
                (Payment Payment, PaymentAttempt Attempt)?>(
                    (Payment, Attempt));
        }

        public Task CompletePaymentAsync(
            Payment payment,
            PaymentAttempt attempt,
            PaymentStatusHistory history,
            CancellationToken cancellationToken)
        {
            History = history;
            CompletionCount++;

            return Task.CompletedTask;
        }
    }

    private sealed class FakeBankPaymentGateway : IBankPaymentGateway
    {
        public BankSubmissionResult? Result { get; init; }

        public Exception? ExceptionToThrow { get; init; }

        public Task<BankSubmissionResult> SubmitAsync(
            Guid paymentId,
            decimal amount,
            string beneficiaryAccountIdentifier,
            string reference,
            CancellationToken cancellationToken)
        {
            if (ExceptionToThrow is not null)
            {
                return Task.FromException<BankSubmissionResult>(
                    ExceptionToThrow);
            }

            return Task.FromResult(
                Result ?? throw new InvalidOperationException(
                    "No fake bank result configured."));
        }
    }
}