namespace PaymentBatchProcessingService.App.Interfaces;

public interface ITenantContext
{
    Guid TenantId { get; }
}
