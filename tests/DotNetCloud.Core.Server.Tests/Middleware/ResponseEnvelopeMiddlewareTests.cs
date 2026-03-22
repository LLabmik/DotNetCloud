using DotNetCloud.Core.Server.Middleware;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;

namespace DotNetCloud.Core.Server.Tests.Middleware;

[TestClass]
public class ResponseEnvelopeMiddlewareTests
{
    private ResponseEnvelopeOptions _options = null!;

    [TestInitialize]
    public void Setup()
    {
        _options = new ResponseEnvelopeOptions
        {
            EnableForAll = true,
            IncludePaths = ["/api/"],
            ExcludePaths = ["/health", "/swagger"]
        };
    }

    [TestMethod]
    public async Task InvokeAsync_NonApiPath_PassesThroughUnmodified()
    {
        var context = CreateHttpContext("/health");
        var responseBody = "{\"status\":\"healthy\"}";

        var middleware = new ResponseEnvelopeMiddleware(
            async ctx =>
            {
                ctx.Response.ContentType = "application/json";
                await ctx.Response.WriteAsync(responseBody);
            },
            _options,
            NullLogger<ResponseEnvelopeMiddleware>.Instance,
            CreateHostEnvironment());

        await middleware.InvokeAsync(context);

        var body = await ReadResponseBodyAsync(context);
        Assert.AreEqual(responseBody, body);
    }

    [TestMethod]
    public async Task InvokeAsync_AlreadyEnveloped_PassesThroughUnmodified()
    {
        var context = CreateHttpContext("/api/v1/users");
        var responseBody = "{\"success\":true,\"data\":{\"id\":1}}";

        var middleware = new ResponseEnvelopeMiddleware(
            async ctx =>
            {
                ctx.Response.ContentType = "application/json";
                await ctx.Response.WriteAsync(responseBody);
            },
            _options,
            NullLogger<ResponseEnvelopeMiddleware>.Instance,
            CreateHostEnvironment());

        await middleware.InvokeAsync(context);

        var body = await ReadResponseBodyAsync(context);
        Assert.IsTrue(body.Contains("\"success\""));
    }

    [TestMethod]
    public async Task InvokeAsync_204NoContent_DoesNotEnvelope()
    {
        var context = CreateHttpContext("/api/v1/users/1");

        var middleware = new ResponseEnvelopeMiddleware(
            ctx =>
            {
                ctx.Response.StatusCode = StatusCodes.Status204NoContent;
                return Task.CompletedTask;
            },
            _options,
            NullLogger<ResponseEnvelopeMiddleware>.Instance,
            CreateHostEnvironment());

        await middleware.InvokeAsync(context);

        Assert.AreEqual(StatusCodes.Status204NoContent, context.Response.StatusCode);
    }

    [TestMethod]
    public async Task InvokeAsync_ExcludedPath_PassesThroughUnmodified()
    {
        var context = CreateHttpContext("/swagger/index.html");
        var responseBody = "<html></html>";

        var middleware = new ResponseEnvelopeMiddleware(
            async ctx =>
            {
                ctx.Response.ContentType = "text/html";
                await ctx.Response.WriteAsync(responseBody);
            },
            _options,
            NullLogger<ResponseEnvelopeMiddleware>.Instance,
            CreateHostEnvironment());

        await middleware.InvokeAsync(context);

        var body = await ReadResponseBodyAsync(context);
        Assert.AreEqual(responseBody, body);
    }

    [TestMethod]
    public async Task InvokeAsync_DisabledEnvelope_PassesThroughUnmodified()
    {
        _options.EnableForAll = false;
        var context = CreateHttpContext("/api/v1/users");
        var responseBody = "{\"id\":1,\"name\":\"Test\"}";

        var middleware = new ResponseEnvelopeMiddleware(
            async ctx =>
            {
                ctx.Response.ContentType = "application/json";
                await ctx.Response.WriteAsync(responseBody);
            },
            _options,
            NullLogger<ResponseEnvelopeMiddleware>.Instance,
            CreateHostEnvironment());

        await middleware.InvokeAsync(context);

        var body = await ReadResponseBodyAsync(context);
        Assert.AreEqual(responseBody, body);
    }

    [TestMethod]
    public async Task InvokeAsync_BinaryApiResponse_PreservesRawBytes()
    {
        var context = CreateHttpContext("/api/v1/files/abc/download");
        var expectedBytes = new byte[] { 0x50, 0x4B, 0x03, 0x04, 0xFF, 0x00, 0xD8, 0xAA, 0x7F };

        var middleware = new ResponseEnvelopeMiddleware(
            async ctx =>
            {
                ctx.Response.ContentType = "application/octet-stream";
                await ctx.Response.Body.WriteAsync(expectedBytes);
            },
            _options,
            NullLogger<ResponseEnvelopeMiddleware>.Instance,
            CreateHostEnvironment());

        await middleware.InvokeAsync(context);

        var actualBytes = ReadResponseBodyBytes(context);
        CollectionAssert.AreEqual(expectedBytes, actualBytes);
    }

    [TestMethod]
    public void ResponseEnvelopeOptions_HasCorrectDefaults()
    {
        var options = new ResponseEnvelopeOptions();

        Assert.IsTrue(options.EnableForAll);
        Assert.AreEqual(1, options.IncludePaths.Length);
        Assert.AreEqual("/api/", options.IncludePaths[0]);
        Assert.IsTrue(options.ExcludePaths.Length > 0);
        Assert.IsTrue(options.ExcludePaths.Contains("/health"));
    }

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

    private static byte[] ReadResponseBodyBytes(HttpContext context)
    {
        context.Response.Body.Position = 0;
        using var ms = new MemoryStream();
        context.Response.Body.CopyTo(ms);
        return ms.ToArray();
    }

    private static IHostEnvironment CreateHostEnvironment(string environmentName = "Development")
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
