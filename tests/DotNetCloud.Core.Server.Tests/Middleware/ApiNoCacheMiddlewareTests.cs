using DotNetCloud.Core.Server.Middleware;
using Microsoft.AspNetCore.Http;

namespace DotNetCloud.Core.Server.Tests.Middleware;

[TestClass]
public sealed class ApiNoCacheMiddlewareTests
{
    [TestMethod]
    public async Task InvokeAsync_ApiPath_SetsNoStoreHeaders()
    {
        var context = new DefaultHttpContext();
        context.Request.Path = "/api/v1/core/auth/user";

        var middleware = new ApiNoCacheMiddleware(_ => Task.CompletedTask);
        await middleware.InvokeAsync(context);

        Assert.IsTrue(context.Response.Headers["Cache-Control"].ToString().Contains("no-store"),
            "API responses should be marked no-store");
        Assert.IsTrue(context.Response.Headers["Pragma"].ToString().Contains("no-cache"),
            "API responses should include Pragma: no-cache");
    }

    [TestMethod]
    public async Task InvokeAsync_VideoStreamPath_DoesNotSetNoStore()
    {
        var context = new DefaultHttpContext();
        context.Request.Path = "/api/v1/videos/abc123/stream";

        var middleware = new ApiNoCacheMiddleware(_ => Task.CompletedTask);
        await middleware.InvokeAsync(context);

        Assert.IsTrue(string.IsNullOrEmpty(context.Response.Headers["Cache-Control"].ToString()),
            "Media stream responses must remain cacheable");
    }

    [TestMethod]
    public async Task InvokeAsync_MusicStreamPath_DoesNotSetNoStore()
    {
        var context = new DefaultHttpContext();
        context.Request.Path = "/api/v1/music/track/xyz/stream";

        var middleware = new ApiNoCacheMiddleware(_ => Task.CompletedTask);
        await middleware.InvokeAsync(context);

        Assert.IsTrue(string.IsNullOrEmpty(context.Response.Headers["Cache-Control"].ToString()),
            "Media stream responses must remain cacheable");
    }

    [TestMethod]
    public async Task InvokeAsync_FileDownloadPath_DoesNotSetNoStore()
    {
        var context = new DefaultHttpContext();
        context.Request.Path = "/api/v1/files/abc123/download";

        var middleware = new ApiNoCacheMiddleware(_ => Task.CompletedTask);
        await middleware.InvokeAsync(context);

        Assert.IsTrue(string.IsNullOrEmpty(context.Response.Headers["Cache-Control"].ToString()),
            "Raw file content responses must remain cacheable");
    }

    [TestMethod]
    public async Task InvokeAsync_NonApiPath_DoesNotSetNoStore()
    {
        var context = new DefaultHttpContext();
        context.Request.Path = "/auth/login";

        var middleware = new ApiNoCacheMiddleware(_ => Task.CompletedTask);
        await middleware.InvokeAsync(context);

        Assert.IsTrue(string.IsNullOrEmpty(context.Response.Headers["Cache-Control"].ToString()),
            "Non-API responses should be untouched");
    }

    [TestMethod]
    public async Task InvokeAsync_CallsNextMiddleware()
    {
        var called = false;
        var context = new DefaultHttpContext();
        context.Request.Path = "/api/v1/some/endpoint";

        var middleware = new ApiNoCacheMiddleware(_ =>
        {
            called = true;
            return Task.CompletedTask;
        });
        await middleware.InvokeAsync(context);

        Assert.IsTrue(called, "Middleware should always call the next delegate");
    }
}
