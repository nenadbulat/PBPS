using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using PaymentBatchProcessingService.App.Exceptions;
using PaymentBatchProcessingService.App.PaymentBatches;

namespace PaymentBatchProcessingService.ExceptionHandling;

public sealed class GlobalExceptionHandler(
    IProblemDetailsService problemDetailsService,
    ILogger<GlobalExceptionHandler> logger)
    : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        if (httpContext.Response.HasStarted)
        {
            return false;
        }

        if (exception is OperationCanceledException && httpContext.RequestAborted.IsCancellationRequested)
        {
            return true;
        }

        var (statusCode, title) = exception switch
        {
            BatchValidationException => (StatusCodes.Status400BadRequest, "Invalid request"),
            BatchNotFoundException => (StatusCodes.Status404NotFound, "Resource not found"),
            IdempotencyConflictException => (StatusCodes.Status409Conflict, "Idempotency conflict"),
            BadHttpRequestException bad => (bad.StatusCode, "Bad request"),
            _ => (StatusCodes.Status500InternalServerError, "Internal server error")
        };

        if (statusCode >= 500)
        {
            logger.LogError(
                exception,
                "Unhandled HTTP exception. TraceId: {TraceId}",
                httpContext.TraceIdentifier);
        }
        else if (statusCode == StatusCodes.Status409Conflict)
        {
            logger.LogInformation(
                "HTTP request rejected due to conflict. TraceId: {TraceId}",
                httpContext.TraceIdentifier);
        }

        var problem = new ProblemDetails
        {
            Status = statusCode,
            Title = title,
            Detail = statusCode >= 500 ? "An unexpected error occurred." : exception.Message,
            Instance = httpContext.Request.Path
        };

        httpContext.Response.StatusCode = statusCode;

        var written = await problemDetailsService.TryWriteAsync(
            new ProblemDetailsContext
            {
                HttpContext = httpContext,
                ProblemDetails = problem,
                Exception = exception
            });

        if (!written)
        {
            httpContext.Response.ContentType = "text/plain";

            await httpContext.Response.WriteAsync("An error occurred.", cancellationToken);
        }

        return true;
    }
}