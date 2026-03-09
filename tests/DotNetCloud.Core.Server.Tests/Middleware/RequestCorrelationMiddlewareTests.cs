using DotNetCloud.Core.ServiceDefaults.Middleware;
using Microsoft.AspNetCore.Http;
using Serilog;
using Serilog.Core;
using Serilog.Events;

namespace DotNetCloud.Core.Server.Tests.Middleware;

[TestClass]
public sealed class RequestCorrelationMiddlewareTests
{
    [TestMethod]
    public async Task InvokeAsync_PushesRequestIdIntoLogContext()
    {
        // Arrange
        var capturedEvents = new List<LogEvent>();
        var logger = new LoggerConfiguration()
            .Enrich.FromLogContext()
            .WriteTo.Sink(new ListSink(capturedEvents))
            .CreateLogger();

        var requestId = "test-request-id-123";
        var middleware = new RequestCorrelationMiddleware(ctx =>
        {
            logger.Information("Test log inside middleware pipeline");
            return Task.CompletedTask;
        });

        var context = new DefaultHttpContext();
        context.Request.Headers["X-Request-ID"] = requestId;

        // Act
        await middleware.InvokeAsync(context);

        // Assert
        Assert.AreEqual(1, capturedEvents.Count);
        var logEvent = capturedEvents[0];
        Assert.IsTrue(logEvent.Properties.ContainsKey("RequestId"),
            "Log event should contain RequestId property");
        Assert.AreEqual($"\"{requestId}\"", logEvent.Properties["RequestId"].ToString());
    }

    [TestMethod]
    public async Task InvokeAsync_GeneratedRequestId_AppearsInLogContext()
    {
        // Arrange — no X-Request-ID header, middleware generates one
        var capturedEvents = new List<LogEvent>();
        var logger = new LoggerConfiguration()
            .Enrich.FromLogContext()
            .WriteTo.Sink(new ListSink(capturedEvents))
            .CreateLogger();

        var middleware = new RequestCorrelationMiddleware(ctx =>
        {
            logger.Information("Log with generated ID");
            return Task.CompletedTask;
        });

        var context = new DefaultHttpContext();

        // Act
        await middleware.InvokeAsync(context);

        // Assert
        Assert.AreEqual(1, capturedEvents.Count);
        var logEvent = capturedEvents[0];
        Assert.IsTrue(logEvent.Properties.ContainsKey("RequestId"),
            "Log event should contain RequestId property even when auto-generated");
        var value = logEvent.Properties["RequestId"].ToString().Trim('"');
        Assert.AreEqual(32, value.Length, "Auto-generated RequestId should be a 32-char hex GUID");
    }

    /// <summary>
    /// Simple in-memory Serilog sink for test assertions.
    /// </summary>
    private sealed class ListSink : ILogEventSink
    {
        private readonly List<LogEvent> _events;

        public ListSink(List<LogEvent> events) => _events = events;

        public void Emit(LogEvent logEvent) => _events.Add(logEvent);
    }
}
