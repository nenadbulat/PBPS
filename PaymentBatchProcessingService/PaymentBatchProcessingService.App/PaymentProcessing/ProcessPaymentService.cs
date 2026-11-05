using Microsoft.Extensions.Logging;
using PaymentBatchProcessingService.App.Interfaces;
using PaymentBatchProcessingService.Domain.Entities;

namespace PaymentBatchProcessingService.App.PaymentProcessing;

public sealed class ProcessPaymentService(
    IPaymentProcessingStore store,
    IBankPaymentGateway bankGateway,
    TimeProvider timeProvider,
    ILogger<ProcessPaymentService> logger)
{
    public async Task<bool> ProcessNextAsync(CancellationToken cancellationToken)
    {
        var work = await store.TryStartNextPaymentAsync(cancellationToken);

        if (work is null)
        {
            return false;
        }

        var (payment, attempt) = work.Value;

        BankSubmissionResult? result = null;
        Exception? bankException = null;

        try
        {
            result = await bankGateway.SubmitAsync(
                payment.Id,
                payment.Amount,
                payment.BeneficiaryAccountIdentifier,
                payment.Reference,
                cancellationToken);
        }
        catch (OperationCanceledException)
            when (cancellationToken.IsCancellationRequested)
        {
            // The payment remains Processing with a Pending attempt.
            // It must not be submitted again without investigation.
            throw;
        }
        catch (Exception exception)
        {
            bankException = exception;

            logger.LogWarning(
                exception,
                "Bank submission outcome could not be confirmed. PaymentId: {PaymentId}",
                payment.Id);
        }

        var previousStatus = payment.Status;
        var completedAtUtc = timeProvider.GetUtcNow().UtcDateTime;

        if (bankException is not null)
        {
            const string reason =
                "Bank submission outcome could not be confirmed.";

            payment.MarkAsUnknown(reason);
            attempt.MarkAsUnknown(reason, completedAtUtc);
        }
        else
        {
            switch (result?.Outcome)
            {
                case BankSubmissionOutcome.Accepted:
                    {
                        if (string.IsNullOrWhiteSpace(result!.BankReference))
                        {
                            const string reason =
                                "Bank accepted the submission but did not provide a reference. The outcome requires investigation.";

                            logger.LogWarning(
                                "Bank returned an accepted response without a reference. PaymentId: {PaymentId}",
                                payment.Id);

                            payment.MarkAsUnknown(reason);
                            attempt.MarkAsUnknown(reason, completedAtUtc);
                        }
                        else
                        {
                            payment.MarkAsSubmitted(result.BankReference);
                            attempt.MarkAsAccepted(result.BankReference, completedAtUtc);
                        }

                        break;
                    }

                case BankSubmissionOutcome.Rejected:
                    {
                        var reason = result.RejectionReason
                            ?? "Payment rejected by the bank.";

                        payment.MarkAsRejected(reason);
                        attempt.MarkAsRejected(reason, completedAtUtc);
                        break;
                    }

                default:
                    {
                        const string reason = "Bank returned an unsupported or missing submission outcome.";

                        logger.LogWarning("Bank returned an unsupported or missing submission outcome. PaymentId: {PaymentId}", payment.Id);

                        payment.MarkAsUnknown(reason);
                        attempt.MarkAsUnknown(reason, completedAtUtc);
                        break;
                    }
            }
        }

        var history = new PaymentStatusHistory(
            Guid.NewGuid(),
            payment.Id,
            previousStatus,
            payment.Status,
            completedAtUtc,
            payment.LastStatusReason);

        // Persist the known bank outcome even if the worker is shutting down.
        // Use a separate bounded timeout rather than the worker's cancellation token.
        using var persistenceTimeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));

        await store.CompletePaymentAsync(
            payment,
            attempt,
            history,
            persistenceTimeout.Token);

        logger.LogInformation(
            "Payment processing finished. PaymentId: {PaymentId}, Status: {Status}",
            payment.Id,
            payment.Status);

        return true;
    }
}