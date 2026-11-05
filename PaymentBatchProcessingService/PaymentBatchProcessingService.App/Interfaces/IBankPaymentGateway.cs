using PaymentBatchProcessingService.App.PaymentProcessing;

namespace PaymentBatchProcessingService.App.Interfaces;

public interface IBankPaymentGateway
{
    Task<BankSubmissionResult> SubmitAsync(
        Guid paymentId,
        decimal amount,
        string beneficiaryAccountIdentifier,
        string reference,
        CancellationToken cancellationToken);
}