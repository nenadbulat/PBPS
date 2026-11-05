using PaymentBatchProcessingService.Domain.Entities;

namespace PaymentBatchProcessingService.App.Interfaces;

public interface IPaymentProcessingStore
{
    Task<(Payment Payment, PaymentAttempt Attempt)?> TryStartNextPaymentAsync(CancellationToken cancellationToken);

    Task CompletePaymentAsync(
        Payment payment,
        PaymentAttempt attempt,
        PaymentStatusHistory history,
        CancellationToken cancellationToken);
}