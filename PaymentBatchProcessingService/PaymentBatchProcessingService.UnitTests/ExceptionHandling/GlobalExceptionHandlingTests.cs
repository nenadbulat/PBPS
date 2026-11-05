using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using PaymentBatchProcessingService.App.Exceptions;
using PaymentBatchProcessingService.ExceptionHandling;
using System.Text.Json;

namespace PaymentBatchProcessingService.UnitTests.ExceptionHandling;

[TestFixture]
public sealed class GlobalExceptionHandlerTests
{
    [Test]
    public async Task TryHandleAsync_WhenValidationFails_Returns400()
    {
        var services = new ServiceCollection();

        services.AddLogging();
        services.AddProblemDetails();

        using var provider = services.BuildServiceProvider();

        var context = new DefaultHttpContext
        {
            RequestServices = provider
        };

        context.Request.Path = "/payment-batches";
        context.Request.Headers.Accept = "application/json";

        await using var responseBody = new MemoryStream();
        context.Response.Body = responseBody;

        var handler = new GlobalExceptionHandler(
            provider.GetRequiredService<IProblemDetailsService>(),
            NullLogger<GlobalExceptionHandler>.Instance);

        var handled = await handler.TryHandleAsync(
            context,
            new BatchValidationException("Invalid payment batch."),
            CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(handled, Is.True);
            Assert.That(
                context.Response.StatusCode,
                Is.EqualTo(StatusCodes.Status400BadRequest));
            Assert.That(
                context.Response.ContentType,
                Does.StartWith("application/problem+json"));
        });

        responseBody.Position = 0;

        using var document = await System.Text.Json.JsonDocument
            .ParseAsync(responseBody);

        Assert.Multiple(() =>
        {
            Assert.That(
                document.RootElement.GetProperty("status").GetInt32(),
                Is.EqualTo(400));

            Assert.That(
                document.RootElement.GetProperty("title").GetString(),
                Is.EqualTo("Invalid request"));

            Assert.That(
                document.RootElement.GetProperty("detail").GetString(),
                Is.EqualTo("Invalid payment batch."));
        });
    }

    [Test]
    public async Task TryHandleAsync_WhenBatchNotFound_Returns404()
    {
        using var provider = CreateServiceProvider();
        var context = CreateHttpContext(provider);

        var handled = await CreateHandler(provider).TryHandleAsync(
            context,
            new BatchNotFoundException(),
            CancellationToken.None);

        using var json = await ReadResponseAsync(context);

        Assert.Multiple(() =>
        {
            Assert.That(handled, Is.True);
            Assert.That(context.Response.StatusCode, Is.EqualTo(404));
            Assert.That(context.Response.ContentType,
                Does.StartWith("application/problem+json"));
            Assert.That(json.RootElement.GetProperty("status").GetInt32(),
                Is.EqualTo(404));
            Assert.That(json.RootElement.GetProperty("title").GetString(),
                Is.EqualTo("Resource not found"));
            Assert.That(json.RootElement.GetProperty("detail").GetString(),
                Is.EqualTo("Payment batch was not found."));
        });
    }

    [Test]
    public async Task TryHandleAsync_WhenIdempotencyConflict_Returns409()
    {
        using var provider = CreateServiceProvider();
        var context = CreateHttpContext(provider);

        var handled = await CreateHandler(provider).TryHandleAsync(
            context,
            new IdempotencyConflictException(),
            CancellationToken.None);

        using var json = await ReadResponseAsync(context);

        Assert.Multiple(() =>
        {
            Assert.That(handled, Is.True);
            Assert.That(context.Response.StatusCode, Is.EqualTo(409));
            Assert.That(json.RootElement.GetProperty("status").GetInt32(),
                Is.EqualTo(409));
            Assert.That(json.RootElement.GetProperty("title").GetString(),
                Is.EqualTo("Idempotency conflict"));
        });
    }

    [Test]
    public async Task TryHandleAsync_WhenBadHttpRequest_ReturnsExceptionStatusCode()
    {
        using var provider = CreateServiceProvider();
        var context = CreateHttpContext(provider);

        var exception = new BadHttpRequestException(
            "Malformed request body.",
            StatusCodes.Status400BadRequest);

        var handled = await CreateHandler(provider).TryHandleAsync(
            context,
            exception,
            CancellationToken.None);

        using var json = await ReadResponseAsync(context);

        Assert.Multiple(() =>
        {
            Assert.That(handled, Is.True);
            Assert.That(context.Response.StatusCode, Is.EqualTo(400));
            Assert.That(json.RootElement.GetProperty("status").GetInt32(),
                Is.EqualTo(400));
            Assert.That(json.RootElement.GetProperty("title").GetString(),
                Is.EqualTo("Bad request"));
        });
    }

    [Test]
    public async Task TryHandleAsync_WhenUnexpectedException_Returns500WithoutSensitiveDetails()
    {
        using var provider = CreateServiceProvider();
        var context = CreateHttpContext(provider);

        const string sensitiveMessage =
            "SQL connection failed: secret-connection-string";

        var handled = await CreateHandler(provider).TryHandleAsync(
            context,
            new InvalidOperationException(sensitiveMessage),
            CancellationToken.None);

        using var json = await ReadResponseAsync(context);

        var responseText = json.RootElement.GetRawText();

        Assert.Multiple(() =>
        {
            Assert.That(handled, Is.True);
            Assert.That(context.Response.StatusCode, Is.EqualTo(500));
            Assert.That(json.RootElement.GetProperty("status").GetInt32(),
                Is.EqualTo(500));
            Assert.That(json.RootElement.GetProperty("title").GetString(),
                Is.EqualTo("Internal server error"));
            Assert.That(json.RootElement.GetProperty("detail").GetString(),
                Is.EqualTo("An unexpected error occurred."));
            Assert.That(responseText, Does.Not.Contain(sensitiveMessage));
        });
    }

    [Test]
    public async Task TryHandleAsync_WhenRequestIsAborted_DoesNotWriteResponse()
    {
        using var provider = CreateServiceProvider();
        var context = CreateHttpContext(provider);

        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        context.RequestAborted = cancellation.Token;

        var handled = await CreateHandler(provider).TryHandleAsync(
            context,
            new OperationCanceledException(cancellation.Token),
            cancellation.Token);

        Assert.Multiple(() =>
        {
            Assert.That(handled, Is.True);
            Assert.That(
                ((MemoryStream)context.Response.Body).Length,
                Is.EqualTo(0));
        });
    }

    [Test]
    public async Task TryHandleAsync_WhenResponseHasStarted_ReturnsFalse()
    {
        using var provider = CreateServiceProvider();
        var context = CreateHttpContext(provider);

        // DefaultHttpContext does not simulate a started response merely
        // by setting the status code. We supply a feature that does.
        context.Features.Set<IHttpResponseFeature>(
            new StartedHttpResponseFeature());

        var handled = await CreateHandler(provider).TryHandleAsync(
            context,
            new InvalidOperationException("Unexpected error."),
            CancellationToken.None);

        Assert.That(handled, Is.False);
    }

    private static ServiceProvider CreateServiceProvider()
    {
        var services = new ServiceCollection();

        services.AddLogging();
        services.AddProblemDetails();

        return services.BuildServiceProvider();
    }

    private static DefaultHttpContext CreateHttpContext(ServiceProvider provider)
    {
        var context = new DefaultHttpContext
        {
            RequestServices = provider
        };

        context.Request.Path = "/payment-batches";
        context.Request.Headers.Accept = "application/json";
        context.Response.Body = new MemoryStream();

        return context;
    }

    private static GlobalExceptionHandler CreateHandler(ServiceProvider provider)
    {
        return new GlobalExceptionHandler(
            provider.GetRequiredService<IProblemDetailsService>(),
            NullLogger<GlobalExceptionHandler>.Instance);
    }

    private static async Task<JsonDocument> ReadResponseAsync(HttpContext context)
    {
        var stream = (MemoryStream)context.Response.Body;

        stream.Position = 0;

        return await JsonDocument.ParseAsync(stream);
    }

    private sealed class StartedHttpResponseFeature : HttpResponseFeature
    {
        public override bool HasStarted => true;
    }
}