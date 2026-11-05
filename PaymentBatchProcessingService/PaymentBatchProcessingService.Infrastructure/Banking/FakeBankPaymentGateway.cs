using PaymentBatchProcessingService.App.Interfaces;
using PaymentBatchProcessingService.App.PaymentProcessing;

namespace PaymentBatchProcessingService.Infrastructure.Banking;

public sealed class FakeBankPaymentGateway : IBankPaymentGateway
{
    public Task<BankSubmissionResult> SubmitAsync(
        Guid paymentId,
        decimal amount,
        string beneficiaryAccountIdentifier,
        string reference,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var result = new BankSubmissionResult(
            BankSubmissionOutcome.Accepted,
            BankReference: $"FAKE-{paymentId:N}");

        return Task.FromResult(result);
    }
}