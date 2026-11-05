using PaymentBatchProcessingService.App.Interfaces;
using PaymentBatchProcessingService.App.PaymentBatches;
using PaymentBatchProcessingService.App.PaymentProcessing;
using PaymentBatchProcessingService.Endpoints;
using PaymentBatchProcessingService.ExceptionHandling;
using PaymentBatchProcessingService.Infrastructure;
using PaymentBatchProcessingService.Infrastructure.Banking;
using PaymentBatchProcessingService.Infrastructure.DB;
using PaymentBatchProcessingService.Infrastructure.DB.Seed;
using PaymentBatchProcessingService.Middleware;
using PaymentBatchProcessingService.TenantContext;
using PaymentBatchProcessingService.Workers;
using System.Text.Json.Serialization;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddHttpContextAccessor();

builder.Services.AddScoped<ITenantContext, HttpTenantContext>();
builder.Services.AddScoped<CreateBatchService>();
builder.Services.AddScoped<ProcessPaymentService>();
builder.Services.AddHostedService<PaymentProcessingWorker>();
builder.Services.AddSingleton(TimeProvider.System);

builder.Services.AddExceptionHandler<GlobalExceptionHandler>();
builder.Services.AddProblemDetails();

builder.Services.ConfigureHttpJsonOptions(options =>
{
    options.SerializerOptions.Converters.Add(new JsonStringEnumConverter());
});

builder.Services.AddInfrastructure(builder.Configuration);
if (builder.Environment.IsDevelopment())
{
    builder.Services.AddScoped<IBankPaymentGateway, FakeBankPaymentGateway>();
}
else
{
    throw new InvalidOperationException("A real bank payment gateway must be configured outside Development.");
}

builder.Services.AddOpenApi();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();

    using var scope = app.Services.CreateScope();
    var dbContext = scope.ServiceProvider.GetRequiredService<PaymentsDbContext>();
    await DevelopmentDataSeeder.SeedAsync(dbContext);
}

app.UseMiddleware<LoggingMiddleware>();
app.UseExceptionHandler();
app.UseHttpsRedirection();
app.MapPaymentBatchEndpoints();
app.Run();