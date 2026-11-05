using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using PaymentBatchProcessingService.App.Interfaces;
using PaymentBatchProcessingService.App.PaymentProcessing;
using PaymentBatchProcessingService.Infrastructure.Banking;
using PaymentBatchProcessingService.Infrastructure.DB;
using PaymentBatchProcessingService.Infrastructure.Stores;

namespace PaymentBatchProcessingService.Infrastructure;

public static class DependencyInjection
{
    private static readonly string DefaultConnectionStringName = "DefaultConnection";

    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString =
            configuration.GetConnectionString(DefaultConnectionStringName)
            ?? throw new InvalidOperationException($"Database connection string '{DefaultConnectionStringName}' is missing.");

        services.AddDbContext<PaymentsDbContext>(options => options.UseSqlServer(connectionString));
        services.AddScoped<IPaymentBatchStore, PaymentBatchStore>();
        services.AddScoped<IPaymentProcessingStore, PaymentProcessingStore>();
        services.AddScoped<ProcessPaymentService>();

        return services;
    }
}
