using System.Text.Json;
using DotNetCloud.Core.Server.Middleware;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;

namespace DotNetCloud.Core.Server.Tests.Middleware;

/// <summary>
/// Security regression tests for ResponseEnvelopeMiddleware covering:
///   - Production environment strips error details and overrides messages for 5xx errors
///   - Development environment preserves error details for debugging
///   - 4xx error messages are preserved in all environments
/// </summary>
[TestClass]
public class ResponseEnvelopeSecurityTests
{
    private ResponseEnvelopeOptions _options = null!;

    [TestInitialize]
    public void Setup()
    {
        _options = new ResponseEnvelopeOptions
        {
            EnableForAll = true,
            IncludePaths = ["/api/"],
            ExcludePaths = ["/health"]
        };
    }

    // ──────────────────────────────────────────────────────────────────────────
    // Vulnerability 9: Error Detail Stripping in Production
    //
    // In production, 5xx error responses must NOT include internal error details
    // (stack traces, connection strings, SQL errors, etc.). The envelope middleware
    // must replace the message with a generic one and strip the "details" field.
    // ──────────────────────────────────────────────────────────────────────────

    [TestMethod]
    public async Task InvokeAsync_500Error_Production_StripsCustomMessageAndUsesGeneric()
    {
        var context = CreateHttpContext("/api/v1/users");
        var internalError = JsonSerializer.Serialize(new
        {
            code = "INTERNAL_ERROR",
            message = "NpgsqlException: connection to server at 192.168.1.5 timed out",
            details = new { stackTrace = "at Npgsql.NpgsqlConnection.Open()" }
        });

        var middleware = new ResponseEnvelopeMiddleware(
            async ctx =>
            {
                ctx.Response.StatusCode = 500;
                ctx.Response.ContentType = "application/json";
                await ctx.Response.WriteAsync(internalError);
            },
            _options,
            NullLogger<ResponseEnvelopeMiddleware>.Instance,
            CreateHostEnvironment("Production"));

        await middleware.InvokeAsync(context);

        var body = await ReadResponseBodyAsync(context);
        var doc = JsonDocument.Parse(body);
        var root = doc.RootElement;

        // Must have envelope structure
        Assert.IsTrue(root.TryGetProperty("success", out var success));
        Assert.IsFalse(success.GetBoolean());

        // Message must be the generic one, NOT the internal error message
        Assert.IsTrue(root.TryGetProperty("message", out var message));
        Assert.AreEqual("An internal server error occurred.", message.GetString());

        // Details must NOT be present in production
        Assert.IsFalse(root.TryGetProperty("details", out var details) && details.ValueKind != JsonValueKind.Null,
            "Error details must be stripped in production to prevent information disclosure");

        // Internal details must not leak anywhere in the response
        Assert.IsFalse(body.Contains("NpgsqlException"));
        Assert.IsFalse(body.Contains("192.168.1.5"));
        Assert.IsFalse(body.Contains("stackTrace"));
    }

    [TestMethod]
    public async Task InvokeAsync_503Error_Production_StripsDetails()
    {
        var context = CreateHttpContext("/api/v1/health");
        var internalError = JsonSerializer.Serialize(new
        {
            code = "SERVICE_UNAVAILABLE",
            message = "Redis connection to redis-cluster.internal:6379 failed",
        });

        var middleware = new ResponseEnvelopeMiddleware(
            async ctx =>
            {
                ctx.Response.StatusCode = 503;
                ctx.Response.ContentType = "application/json";
                await ctx.Response.WriteAsync(internalError);
            },
            _options,
            NullLogger<ResponseEnvelopeMiddleware>.Instance,
            CreateHostEnvironment("Production"));

        await middleware.InvokeAsync(context);

        var body = await ReadResponseBodyAsync(context);

        Assert.IsFalse(body.Contains("redis-cluster.internal"),
            "Internal infrastructure hostnames must not leak in production 5xx responses");
        Assert.IsTrue(body.Contains("The service is temporarily unavailable."));
    }

    [TestMethod]
    public async Task InvokeAsync_500Error_Development_PreservesDetailsForDebugging()
    {
        var context = CreateHttpContext("/api/v1/users");
        var internalError = JsonSerializer.Serialize(new
        {
            code = "INTERNAL_ERROR",
            message = "Something went wrong internally",
            details = new { query = "SELECT * FROM users WHERE..." }
        });

        var middleware = new ResponseEnvelopeMiddleware(
            async ctx =>
            {
                ctx.Response.StatusCode = 500;
                ctx.Response.ContentType = "application/json";
                await ctx.Response.WriteAsync(internalError);
            },
            _options,
            NullLogger<ResponseEnvelopeMiddleware>.Instance,
            CreateHostEnvironment("Development"));

        await middleware.InvokeAsync(context);

        var body = await ReadResponseBodyAsync(context);
        var doc = JsonDocument.Parse(body);
        var root = doc.RootElement;

        // In development, the original message should be preserved
        Assert.IsTrue(root.TryGetProperty("message", out var message));
        Assert.AreEqual("Something went wrong internally", message.GetString());

        // Details should be present in development
        Assert.IsTrue(root.TryGetProperty("details", out var details) && details.ValueKind != JsonValueKind.Null,
            "Error details should be preserved in development for debugging");
    }

    [TestMethod]
    public async Task InvokeAsync_400Error_Production_PreservesOriginalMessage()
    {
        var context = CreateHttpContext("/api/v1/users");
        var clientError = JsonSerializer.Serialize(new
        {
            code = "VALIDATION_ERROR",
            message = "The email field is required."
        });

        var middleware = new ResponseEnvelopeMiddleware(
            async ctx =>
            {
                ctx.Response.StatusCode = 400;
                ctx.Response.ContentType = "application/json";
                await ctx.Response.WriteAsync(clientError);
            },
            _options,
            NullLogger<ResponseEnvelopeMiddleware>.Instance,
            CreateHostEnvironment("Production"));

        await middleware.InvokeAsync(context);

        var body = await ReadResponseBodyAsync(context);
        var doc = JsonDocument.Parse(body);
        var root = doc.RootElement;

        // 4xx errors should preserve the original message even in production
        // since these are client-facing validation messages
        Assert.IsTrue(root.TryGetProperty("message", out var message));
        Assert.AreEqual("The email field is required.", message.GetString());
    }

    [TestMethod]
    public async Task InvokeAsync_404Error_Production_PreservesOriginalMessage()
    {
        var context = CreateHttpContext("/api/v1/users/nonexistent");
        var clientError = JsonSerializer.Serialize(new
        {
            code = "NOT_FOUND",
            message = "User not found."
        });

        var middleware = new ResponseEnvelopeMiddleware(
            async ctx =>
            {
                ctx.Response.StatusCode = 404;
                ctx.Response.ContentType = "application/json";
                await ctx.Response.WriteAsync(clientError);
            },
            _options,
            NullLogger<ResponseEnvelopeMiddleware>.Instance,
            CreateHostEnvironment("Production"));

        await middleware.InvokeAsync(context);

        var body = await ReadResponseBodyAsync(context);
        Assert.IsTrue(body.Contains("User not found."));
    }

    [TestMethod]
    public async Task InvokeAsync_500Error_Production_IncludesTraceId()
    {
        var context = CreateHttpContext("/api/v1/users");
        context.TraceIdentifier = "test-trace-id-123";

        var middleware = new ResponseEnvelopeMiddleware(
            async ctx =>
            {
                ctx.Response.StatusCode = 500;
                ctx.Response.ContentType = "application/json";
                await ctx.Response.WriteAsync("{\"message\":\"internal error\"}");
            },
            _options,
            NullLogger<ResponseEnvelopeMiddleware>.Instance,
            CreateHostEnvironment("Production"));

        await middleware.InvokeAsync(context);

        var body = await ReadResponseBodyAsync(context);
        Assert.IsTrue(body.Contains("test-trace-id-123"),
            "TraceId should be present in production error responses for support reference");
    }

    // ──── Helpers ─────────────────────────────────────────────────────────────

    private static HttpContext CreateHttpContext(string path)
    {
        var context = new DefaultHttpContext();
        context.Request.Path = path;
        context.Response.Body = new MemoryStream();
        return context;
    }

    private static async Task<string> ReadResponseBodyAsync(HttpContext context)
    {
        context.Response.Body.Position = 0;
        using var reader = new StreamReader(context.Response.Body);
        return await reader.ReadToEndAsync();
    }

    private static IHostEnvironment CreateHostEnvironment(string environmentName)
    {
        return new TestHostEnvironment { EnvironmentName = environmentName };
    }

    private sealed class TestHostEnvironment : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = "Development";
        public string ApplicationName { get; set; } = "Test";
        public string ContentRootPath { get; set; } = "";
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }
}
