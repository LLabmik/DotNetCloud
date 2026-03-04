using DotNetCloud.Core.Authorization;
using DotNetCloud.Core.ServiceDefaults.Logging;
using Serilog;
using Serilog.Core;
using Serilog.Events;

namespace DotNetCloud.Core.Server.Tests.Observability;

[TestClass]
public class LogEnricherTests
{
    [TestMethod]
    public void WithUserId_ReturnsDisposable()
    {
        using var enrichment = LogEnricher.WithUserId(Guid.NewGuid());

        Assert.IsNotNull(enrichment);
    }

    [TestMethod]
    public void WithRequestId_ReturnsDisposable()
    {
        using var enrichment = LogEnricher.WithRequestId("req-123");

        Assert.IsNotNull(enrichment);
    }

    [TestMethod]
    public void WithModuleName_ReturnsDisposable()
    {
        using var enrichment = LogEnricher.WithModuleName("DotNetCloud.Files");

        Assert.IsNotNull(enrichment);
    }

    [TestMethod]
    public void WithOperationName_ReturnsDisposable()
    {
        using var enrichment = LogEnricher.WithOperationName("UploadFile");

        Assert.IsNotNull(enrichment);
    }

    [TestMethod]
    public void WithCallerContext_ReturnsDisposable()
    {
        var context = new CallerContext(
            Guid.NewGuid(),
            ["Admin", "User"],
            CallerType.User);

        using var enrichment = LogEnricher.WithCallerContext(context);

        Assert.IsNotNull(enrichment);
    }

    [TestMethod]
    public void WithUserId_PushesPropertyToLogContext()
    {
        var userId = Guid.NewGuid();
        var sink = new CollectorSink();

        using var logger = new LoggerConfiguration()
            .WriteTo.Sink(sink)
            .Enrich.FromLogContext()
            .CreateLogger();

        using (LogEnricher.WithUserId(userId))
        {
            logger.Information("Test log");
        }

        Assert.AreEqual(1, sink.Events.Count);
        Assert.IsTrue(sink.Events[0].Properties.ContainsKey("UserId"));
        Assert.AreEqual(userId.ToString(), sink.Events[0].Properties["UserId"].ToString().Trim('"'));
    }

    [TestMethod]
    public void WithRequestId_PushesPropertyToLogContext()
    {
        var sink = new CollectorSink();

        using var logger = new LoggerConfiguration()
            .WriteTo.Sink(sink)
            .Enrich.FromLogContext()
            .CreateLogger();

        using (LogEnricher.WithRequestId("req-456"))
        {
            logger.Information("Test log");
        }

        Assert.AreEqual(1, sink.Events.Count);
        Assert.IsTrue(sink.Events[0].Properties.ContainsKey("RequestId"));
    }

    [TestMethod]
    public void WithModuleName_PushesPropertyToLogContext()
    {
        var sink = new CollectorSink();

        using var logger = new LoggerConfiguration()
            .WriteTo.Sink(sink)
            .Enrich.FromLogContext()
            .CreateLogger();

        using (LogEnricher.WithModuleName("DotNetCloud.Chat"))
        {
            logger.Information("Test log");
        }

        Assert.AreEqual(1, sink.Events.Count);
        Assert.IsTrue(sink.Events[0].Properties.ContainsKey("ModuleName"));
    }

    [TestMethod]
    public void WithCallerContext_PushesCallerContextPropertyToLogContext()
    {
        var context = new CallerContext(
            Guid.NewGuid(),
            ["Admin"],
            CallerType.User);

        var sink = new CollectorSink();

        using var logger = new LoggerConfiguration()
            .WriteTo.Sink(sink)
            .Enrich.FromLogContext()
            .CreateLogger();

        using (LogEnricher.WithCallerContext(context))
        {
            logger.Information("Test log");
        }

        Assert.AreEqual(1, sink.Events.Count);
        Assert.IsTrue(sink.Events[0].Properties.ContainsKey("CallerContext"));
    }

    [TestMethod]
    public void Enrichment_IsRemoved_AfterDispose()
    {
        var sink = new CollectorSink();

        using var logger = new LoggerConfiguration()
            .WriteTo.Sink(sink)
            .Enrich.FromLogContext()
            .CreateLogger();

        using (LogEnricher.WithUserId(Guid.NewGuid()))
        {
            logger.Information("Inside scope");
        }

        logger.Information("Outside scope");

        Assert.AreEqual(2, sink.Events.Count);
        Assert.IsTrue(sink.Events[0].Properties.ContainsKey("UserId"));
        Assert.IsFalse(sink.Events[1].Properties.ContainsKey("UserId"));
    }

    /// <summary>
    /// Test helper sink that collects log events for assertion.
    /// </summary>
    private sealed class CollectorSink : ILogEventSink
    {
        public List<LogEvent> Events { get; } = [];

        public void Emit(LogEvent logEvent) => Events.Add(logEvent);
    }
}
