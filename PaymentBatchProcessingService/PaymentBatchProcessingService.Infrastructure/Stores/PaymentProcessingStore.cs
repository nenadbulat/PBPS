using Microsoft.EntityFrameworkCore;
using PaymentBatchProcessingService.App.Interfaces;
using PaymentBatchProcessingService.Domain.Entities;
using PaymentBatchProcessingService.Domain.Enums;
using PaymentBatchProcessingService.Infrastructure.DB;

namespace PaymentBatchProcessingService.Infrastructure.Stores;

public sealed class PaymentProcessingStore(
    PaymentsDbContext dbContext,
    TimeProvider timeProvider) : IPaymentProcessingStore
{
    public async Task<(Payment Payment, PaymentAttempt Attempt)?>
        TryStartNextPaymentAsync(CancellationToken cancellationToken)
    {
        var payment = await dbContext.Payments
            .Where(p => p.Status == PaymentStatus.Accepted)
            .OrderBy(p => p.CreatedAtUtc)
            .ThenBy(p => p.Id)
            .FirstOrDefaultAsync(cancellationToken);

        if (payment is null)
            return null;

        var batch = await dbContext.PaymentBatches
            .SingleAsync(
                b => b.Id == payment.PaymentBatchId,
                cancellationToken);

        var previousStatus = payment.Status;
        payment.MarkAsProcessing();

        if (batch.Status == PaymentBatchStatus.Accepted)
            batch.MarkAsProcessing();

        var now = timeProvider.GetUtcNow().UtcDateTime;

        var attempt = new PaymentAttempt(
            Guid.NewGuid(),
            payment.Id,
            now);

        var history = new PaymentStatusHistory(
            Guid.NewGuid(),
            payment.Id,
            previousStatus,
            payment.Status,
            now);

        dbContext.PaymentAttempts.Add(attempt);
        dbContext.PaymentStatusHistories.Add(history);

        await dbContext.SaveChangesAsync(cancellationToken);

        return (payment, attempt);
    }

    public async Task CompletePaymentAsync(
        Payment payment,
        PaymentAttempt attempt,
        PaymentStatusHistory history,
        CancellationToken cancellationToken)
    {
        dbContext.PaymentStatusHistories.Add(history);

        var batch = await dbContext.PaymentBatches
            .SingleAsync(
                b => b.Id == payment.PaymentBatchId,
                cancellationToken);

        // The current payment has changed in memory but has not
        // yet been saved with its final status.
        var otherUnfinishedPayments = await dbContext.Payments
            .AsNoTracking()
            .AnyAsync(
                p => p.PaymentBatchId == payment.PaymentBatchId
                    && p.Id != payment.Id
                    && (p.Status == PaymentStatus.Accepted
                        || p.Status == PaymentStatus.Processing),
                cancellationToken);

        var currentPaymentFinished =
            payment.Status is PaymentStatus.Submitted
                or PaymentStatus.Rejected
                or PaymentStatus.Unknown;

        if (currentPaymentFinished &&
            !otherUnfinishedPayments &&
            batch.Status == PaymentBatchStatus.Processing)
        {
            batch.MarkAsCompleted();
        }

        await dbContext.SaveChangesAsync(cancellationToken);
    }
}