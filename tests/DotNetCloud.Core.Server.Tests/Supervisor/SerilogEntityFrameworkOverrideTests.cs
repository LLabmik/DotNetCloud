using DotNetCloud.Core.ServiceDefaults.Logging;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Serilog;
using Serilog.Core;
using Serilog.Events;

namespace DotNetCloud.Core.Server.Tests.Supervisor;

/// <summary>
/// The configurable status reconcile interval is only cheap while
/// <c>Microsoft.EntityFrameworkCore</c> logs at <c>Warning</c> or above: at <c>Information</c>,
/// <c>Database.Command</c> emits a line per tick, which a one-second interval turns into a flood.
/// This asserts the override as configuration, so an edit that removes it fails here instead of
/// only in production logs.
/// </summary>
[TestClass]
public class SerilogEntityFrameworkOverrideTests
{
    private const string EntityFrameworkCommandCategory = "Microsoft.EntityFrameworkCore.Database.Command";

    [TestMethod]
    public void EntityFrameworkInformationIsFilteredAndWarningsSurvive()
    {
        var sink = new CollectingSink();
        var logPath = Path.Combine(Path.GetTempPath(), $"dnc-serilog-{Guid.NewGuid():N}-.log");

        try
        {
            var configuration = new LoggerConfiguration();
            SerilogConfigurationExtensions.ConfigureSerilog(
                configuration,
                new TestHostEnvironment(),
                new SerilogOptions
                {
                    ConsoleMinimumLevel = LogEventLevel.Fatal,
                    FilePath = logPath,
                    AuditFilePath = null,
                });
            configuration.WriteTo.Sink(sink);

            using var logger = configuration.CreateLogger();

            logger.ForContext("SourceContext", EntityFrameworkCommandCategory).Information("Executed DbCommand (1ms)");
            logger.ForContext("SourceContext", EntityFrameworkCommandCategory).Warning("A command took 400ms");
            logger.ForContext("SourceContext", "DotNetCloud.Core.Server.Supervisor").Information("reconcile pass");

            var messages = sink.Events.Select(evt => evt.RenderMessage()).ToList();

            Assert.AreEqual(
                2,
                messages.Count,
                "Only the EF warning and the DotNetCloud line may pass: " + string.Join(" | ", messages));
            Assert.IsTrue(
                messages.Any(message => message.Contains("A command took 400ms", StringComparison.Ordinal)),
                "A warning from the EF category must still be written.");
            Assert.IsFalse(
                messages.Any(message => message.Contains("Executed DbCommand", StringComparison.Ordinal)),
                "Information from Microsoft.EntityFrameworkCore must be filtered: a sub-five-second "
                + "reconcile interval assumes it is.");
        }
        finally
        {
            if (File.Exists(logPath))
            {
                File.Delete(logPath);
            }
        }
    }

    private sealed class CollectingSink : ILogEventSink
    {
        private readonly List<LogEvent> _events = [];

        public IReadOnlyList<LogEvent> Events => _events;

        public void Emit(LogEvent logEvent) => _events.Add(logEvent);
    }

    private sealed class TestHostEnvironment : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = Environments.Production;

        public string ApplicationName { get; set; } = "DotNetCloud.Core.Server.Tests";

        public string ContentRootPath { get; set; } = AppContext.BaseDirectory;

        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }
}
