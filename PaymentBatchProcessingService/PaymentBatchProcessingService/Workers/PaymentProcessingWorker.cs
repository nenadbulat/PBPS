using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using PaymentBatchProcessingService.App.PaymentProcessing;

namespace PaymentBatchProcessingService.Workers;

public sealed class PaymentProcessingWorker(
    IServiceScopeFactory scopeFactory,
    ILogger<PaymentProcessingWorker> logger)
    : BackgroundService
{
    private static readonly TimeSpan PollInterval =
        TimeSpan.FromSeconds(5);

    protected override async Task ExecuteAsync(
        CancellationToken stoppingToken)
    {
        logger.LogInformation("Payment processing worker started.");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = scopeFactory.CreateScope();

                var service = scope.ServiceProvider
                    .GetRequiredService<ProcessPaymentService>();

                var processed = await service.ProcessNextAsync(
                    stoppingToken);

                if (processed)
                {
                    // Process the next payment immediately.
                    continue;
                }

                await Task.Delay(PollInterval, stoppingToken);
            }
            catch (OperationCanceledException)
                when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception exception)
            {
                logger.LogError(
                    exception,
                    "Payment processing failed.");

                // Avoid a tight failure loop.
                try
                {
                    await Task.Delay(PollInterval, stoppingToken);
                }
                catch (OperationCanceledException)
                    when (stoppingToken.IsCancellationRequested)
                {
                    break;
                }
            }
        }

        logger.LogInformation("Payment processing worker stopped.");
    }
}