using PaymentBatchProcessingService.App.Exceptions;
using PaymentBatchProcessingService.App.Interfaces;
using PaymentBatchProcessingService.App.PaymentBatches;

namespace PaymentBatchProcessingService.TenantContext;

public sealed class HttpTenantContext(IHttpContextAccessor httpContextAccessor) : ITenantContext
{
    public Guid TenantId
    {
        get
        {
            var context = httpContextAccessor.HttpContext ?? throw new InvalidOperationException("A HTTP context is required.");

            if (!context.Request.Headers.TryGetValue("X-Tenant-Id", out var values))
            {
                throw new BatchValidationException("X-Tenant-Id header is required.");
            }

            if (values.Count != 1 || !Guid.TryParse(values[0], out var tenantId) || tenantId == Guid.Empty)
            {
                throw new BatchValidationException("X-Tenant-Id must contain a valid, non-empty GUID.");
            }

            return tenantId;
        }
    }
}
