using DotNetCloud.Core.Server.Configuration;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging.Abstractions;

namespace DotNetCloud.Core.Server.Tests.Configuration;

[TestClass]
public class ApiVersionMiddlewareTests
{
    private ApiVersioningOptions _options = null!;

    [TestInitialize]
    public void Setup()
    {
        _options = new ApiVersioningOptions
        {
            CurrentVersion = "2",
            MinimumVersion = "1",
            DeprecatedVersions = ["1"],
            DeprecationDates = new Dictionary<string, DateTime>
            {
                ["1"] = new DateTime(2027, 1, 1)
            }
        };
    }

    [TestMethod]
    public async Task InvokeAsync_NonApiPath_PassesThroughWithoutHeaders()
    {
        var context = CreateHttpContext("/health");
        var nextCalled = false;

        var middleware = new ApiVersionMiddleware(
            _ => { nextCalled = true; return Task.CompletedTask; },
            _options,
            NullLogger<ApiVersionMiddleware>.Instance);

        await middleware.InvokeAsync(context);

        Assert.IsTrue(nextCalled);
        Assert.IsFalse(context.Response.Headers.ContainsKey("X-Api-Version"));
    }

    [TestMethod]
    public async Task InvokeAsync_CurrentVersion_AddsVersionHeader()
    {
        var context = CreateHttpContext("/api/v2/users");
        var nextCalled = false;

        var middleware = new ApiVersionMiddleware(
            _ => { nextCalled = true; return Task.CompletedTask; },
            _options,
            NullLogger<ApiVersionMiddleware>.Instance);

        await middleware.InvokeAsync(context);

        Assert.IsTrue(nextCalled);
        Assert.AreEqual("2", context.Response.Headers["X-Api-Version"].ToString());
        Assert.IsFalse(context.Response.Headers.ContainsKey("X-Api-Deprecated"));
    }

    [TestMethod]
    public async Task InvokeAsync_DeprecatedVersion_AddsDeprecationHeaders()
    {
        var context = CreateHttpContext("/api/v1/users");
        var nextCalled = false;

        var middleware = new ApiVersionMiddleware(
            _ => { nextCalled = true; return Task.CompletedTask; },
            _options,
            NullLogger<ApiVersionMiddleware>.Instance);

        await middleware.InvokeAsync(context);

        Assert.IsTrue(nextCalled);
        Assert.AreEqual("1", context.Response.Headers["X-Api-Version"].ToString());
        Assert.AreEqual("true", context.Response.Headers["X-Api-Deprecated"].ToString());
        Assert.IsTrue(context.Response.Headers.ContainsKey("X-Api-Deprecation-Warning"));
        Assert.AreEqual("2027-01-01", context.Response.Headers["Sunset"].ToString());
    }

    [TestMethod]
    public async Task InvokeAsync_VersionBelowMinimum_Returns400()
    {
        _options.MinimumVersion = "2";
        var context = CreateHttpContext("/api/v1/users");

        var middleware = new ApiVersionMiddleware(
            _ => Task.CompletedTask,
            _options,
            NullLogger<ApiVersionMiddleware>.Instance);

        await middleware.InvokeAsync(context);

        Assert.AreEqual(StatusCodes.Status400BadRequest, context.Response.StatusCode);
    }

    [TestMethod]
    public async Task InvokeAsync_MajorMinorVersion_ParsesCorrectly()
    {
        var context = CreateHttpContext("/api/v2.1/users");

        var middleware = new ApiVersionMiddleware(
            _ => Task.CompletedTask,
            _options,
            NullLogger<ApiVersionMiddleware>.Instance);

        await middleware.InvokeAsync(context);

        Assert.AreEqual("2.1", context.Response.Headers["X-Api-Version"].ToString());
    }

    private static HttpContext CreateHttpContext(string path)
    {
        var context = new DefaultHttpContext();
        context.Request.Path = path;
        context.Response.Body = new MemoryStream();
        return context;
    }
}
