using System.Diagnostics;

namespace PaymentBatchProcessingService.Middleware;

public sealed class LoggingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<LoggingMiddleware> _logger;

    public LoggingMiddleware(RequestDelegate next, ILogger<LoggingMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var stopwatch = Stopwatch.StartNew();

        var traceId = Activity.Current?.Id ?? context.TraceIdentifier;


        //BeginScope, share the traceId with all log messages for this request
        using (_logger.BeginScope("CorrelationId: {CorrelationId}", traceId))
        try
        {
            await _next(context);
        }
        finally
        {
            stopwatch.Stop();

            if (!context.RequestAborted.IsCancellationRequested)
            {
                _logger.LogInformation(
                    "HTTP {Method} {Path} responded {StatusCode} in {ElapsedMilliseconds} ms",
                    context.Request.Method,
                    context.Request.Path.Value,
                    context.Response.StatusCode,
                    stopwatch.Elapsed.TotalMilliseconds);
            }
        }
    }
}