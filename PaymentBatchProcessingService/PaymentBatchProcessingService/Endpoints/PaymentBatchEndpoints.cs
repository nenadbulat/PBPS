using PaymentBatchProcessingService.App.Exceptions;
using PaymentBatchProcessingService.App.Interfaces;
using PaymentBatchProcessingService.App.PaymentBatches;

namespace PaymentBatchProcessingService.Endpoints;

public static class PaymentBatchEndpoints
{
    public static IEndpointRouteBuilder MapPaymentBatchEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapPost("/payment-batches", CreateBatchAsync)
            .WithName("CreatePaymentBatch")
            .WithTags("Payment Batches")
            .Produces<CreateBatchResponse>(StatusCodes.Status201Created)
            .Produces<CreateBatchResponse>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);

        endpoints.MapGet("/payment-batches/{batchId:guid}",
            async (
                Guid batchId,
                ITenantContext tenantContext,
                IPaymentBatchStore store,
                CancellationToken cancellationToken) =>
            {
                var result = await store.GetBatchResultAsync(tenantContext.TenantId, batchId, cancellationToken);

                if (result is null)
                {
                    return Results.NotFound();
                }

                var response = new CreateBatchResponse(
                    result.BatchId,
                    result.LegalEntityId,
                    result.Currency,
                    result.Status,
                    result.PaymentCount,
                    result.CreatedAtUtc);

                return Results.Ok(response);
            })
            .WithName("GetPaymentBatch")
            .WithTags("Payment Batches")
            .Produces<CreateBatchResponse>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status404NotFound);

        endpoints.MapGet("/payment-batches/{batchId:guid}/payments",
            async (
                Guid batchId,
                ITenantContext tenantContext,
                IPaymentBatchStore store,
                CancellationToken cancellationToken) =>
            {
                var payments = await store.GetBatchPaymentsAsync(tenantContext.TenantId, batchId, cancellationToken);

                return payments is null
                    ? Results.NotFound()
                    : Results.Ok(payments);
            })
            .WithName("GetPaymentBatchPayments")
            .WithTags("Payment Batches")
            .Produces<IReadOnlyList<PaymentDetailsResult>>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status404NotFound);

        return endpoints;
    }

    private static async Task<IResult> CreateBatchAsync(
        CreateBatchRequest request,
        HttpContext httpContext,
        CreateBatchService service,
        CancellationToken cancellationToken)
    {
        if (!httpContext.Request.Headers.TryGetValue("Idempotency-Key", out var idempotencyKeyValues) || idempotencyKeyValues.Count != 1)
        {
            throw new BatchValidationException("Exactly one Idempotency-Key header is required.");
        }

        var result = await service.ExecuteAsync(
            request,
            idempotencyKeyValues[0]!,
            cancellationToken);

        var response = new CreateBatchResponse(
            result.BatchId,
            result.LegalEntityId,
            result.Currency,
            result.Status,
            result.PaymentCount,
            result.CreatedAtUtc);

        return result.IsReplay ? Results.Ok(response) : Results.Created($"/payment-batches/{result.BatchId}", response);
    }
}
