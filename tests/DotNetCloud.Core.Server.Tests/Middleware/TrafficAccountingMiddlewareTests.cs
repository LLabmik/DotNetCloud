using System.Security.Claims;
using DotNetCloud.Core.Server.Diagnostics;
using DotNetCloud.Core.Server.Middleware;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace DotNetCloud.Core.Server.Tests.Middleware;

/// <summary>
/// Tests for <see cref="TrafficAccountingMiddleware"/> — specifically that counted bytes are
/// attributed to the authenticated user and that non-user traffic is skipped.
/// </summary>
[TestClass]
public sealed class TrafficAccountingMiddlewareTests
{
    [TestMethod]
    public async Task InvokeAsync_AuthenticatedRequest_AttributesBytesToUser()
    {
        var accounting = new TrafficAccounting(TimeProvider.System);
        var userId = Guid.NewGuid();
        var middleware = CreateMiddleware(accounting, byteCount: 100);

        var context = CreateAuthenticatedContext(userId);

        await middleware.InvokeAsync(context);

        var entry = accounting.GetPerUserSnapshot().Single();
        Assert.AreEqual(userId, entry.UserId);
        Assert.AreEqual(100, entry.BytesOut);
        Assert.AreEqual(100, accounting.BytesOut);
    }

    [TestMethod]
    public async Task InvokeAsync_AnonymousRequest_IsNotCounted()
    {
        var accounting = new TrafficAccounting(TimeProvider.System);
        var middleware = CreateMiddleware(accounting, byteCount: 100);

        var context = new DefaultHttpContext
        {
            User = new ClaimsPrincipal(new ClaimsIdentity()),
        };
        _ = context.Response.Body;

        await middleware.InvokeAsync(context);

        Assert.AreEqual(0, accounting.BytesOut);
        Assert.AreEqual(0, accounting.GetPerUserSnapshot().Count);
    }

    [TestMethod]
    public async Task InvokeAsync_AdminSystemPolling_IsExcluded()
    {
        var accounting = new TrafficAccounting(TimeProvider.System);
        var middleware = CreateMiddleware(accounting, byteCount: 100);

        var context = CreateAuthenticatedContext(Guid.NewGuid());
        context.Request.Path = "/api/v1/core/admin/system";

        await middleware.InvokeAsync(context);

        Assert.AreEqual(0, accounting.BytesOut);
        Assert.AreEqual(0, accounting.GetPerUserSnapshot().Count);
    }

    [TestMethod]
    public async Task InvokeAsync_AuthenticatedWithoutParseableUserId_CountsGloballyOnly()
    {
        var accounting = new TrafficAccounting(TimeProvider.System);
        var middleware = CreateMiddleware(accounting, byteCount: 100);

        var context = new DefaultHttpContext
        {
            User = new ClaimsPrincipal(new ClaimsIdentity(
                [new Claim(ClaimTypes.NameIdentifier, "not-a-guid")], "Test")),
        };
        _ = context.Response.Body;

        await middleware.InvokeAsync(context);

        Assert.AreEqual(100, accounting.BytesOut);
        Assert.AreEqual(0, accounting.GetPerUserSnapshot().Count);
    }

    [TestMethod]
    public async Task InvokeAsync_FeatureInstallFails_LogsSanitizedPathAndContinues()
    {
        var accounting = new TrafficAccounting(TimeProvider.System);
        var logger = new CapturingLogger<TrafficAccountingMiddleware>();
        var middleware = new TrafficAccountingMiddleware(
            _ => Task.CompletedTask,
            accounting,
            logger);

        // Materialise the standard features first, then hand the context a collection whose first
        // Set() fails — the middleware must log and carry on instead of failing the request.
        var inner = CreateAuthenticatedContext(Guid.NewGuid());
        _ = inner.Items;
        var context = new DefaultHttpContext(new ThrowFirstSetFeatureCollection(inner.Features));
        context.Request.Method = inner.Request.Method;
        context.Request.Path = "/admin\r\nFORGED ENTRY";
        context.Request.ContentType = inner.Request.ContentType;
        context.User = inner.User;

        await middleware.InvokeAsync(context);

        Assert.AreEqual(1, logger.Messages.Count);
        var message = logger.Messages[0];
        Assert.IsFalse(message.Contains('\r'), "Log entry must not contain a carriage return.");
        Assert.IsFalse(message.Contains('\n'), "Log entry must not contain a line feed.");
    }

    private static TrafficAccountingMiddleware CreateMiddleware(TrafficAccounting accounting, int byteCount)
    {
        Task Next(HttpContext context) => context.Response.Body.WriteAsync(new byte[byteCount]).AsTask();

        return new TrafficAccountingMiddleware(
            Next,
            accounting,
            NullLogger<TrafficAccountingMiddleware>.Instance);
    }

    private static DefaultHttpContext CreateAuthenticatedContext(Guid userId)
    {
        var context = new DefaultHttpContext
        {
            User = new ClaimsPrincipal(new ClaimsIdentity(
                [new Claim(ClaimTypes.NameIdentifier, userId.ToString())], "Test")),
        };

        // Materialise the default response-body feature so the middleware can wrap it.
        _ = context.Response.Body;
        return context;
    }

    private sealed class CapturingLogger<T> : ILogger<T>
    {
        public List<string> Messages { get; } = [];

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
            => Messages.Add(formatter(state, exception));
    }
}
