using DotNetCloud.Core.ServiceDefaults.Logging;
using Serilog.Events;
using Serilog.Parsing;

namespace DotNetCloud.Core.Server.Tests.Observability;

[TestClass]
public class ModuleLogFilterTests
{
    private static LogEvent CreateLogEvent(LogEventLevel level, string? moduleName = null)
    {
        var properties = new List<LogEventProperty>();
        if (moduleName is not null)
        {
            properties.Add(new LogEventProperty("ModuleName", new ScalarValue(moduleName)));
        }

        return new LogEvent(
            DateTimeOffset.UtcNow,
            level,
            exception: null,
            new MessageTemplate("Test message", []),
            properties);
    }

    [TestMethod]
    public void IsEnabled_NoModuleName_ReturnsTrue()
    {
        var filter = new ModuleLogFilter();
        var logEvent = CreateLogEvent(LogEventLevel.Information);

        Assert.IsTrue(filter.IsEnabled(logEvent));
    }

    [TestMethod]
    public void IsEnabled_ExcludedModule_ReturnsFalse()
    {
        var excluded = new HashSet<string> { "DotNetCloud.Debug" };
        var filter = new ModuleLogFilter(excluded);

        var logEvent = CreateLogEvent(LogEventLevel.Information, "DotNetCloud.Debug");

        Assert.IsFalse(filter.IsEnabled(logEvent));
    }

    [TestMethod]
    public void IsEnabled_NonExcludedModule_ReturnsTrue()
    {
        var excluded = new HashSet<string> { "DotNetCloud.Debug" };
        var filter = new ModuleLogFilter(excluded);

        var logEvent = CreateLogEvent(LogEventLevel.Information, "DotNetCloud.Files");

        Assert.IsTrue(filter.IsEnabled(logEvent));
    }

    [TestMethod]
    public void IsEnabled_ModuleLevelBelowMinimum_ReturnsFalse()
    {
        var levels = new Dictionary<string, LogEventLevel>
        {
            ["DotNetCloud.Files"] = LogEventLevel.Warning
        };
        var filter = new ModuleLogFilter(moduleLevels: levels);

        var logEvent = CreateLogEvent(LogEventLevel.Information, "DotNetCloud.Files");

        Assert.IsFalse(filter.IsEnabled(logEvent));
    }

    [TestMethod]
    public void IsEnabled_ModuleLevelAtMinimum_ReturnsTrue()
    {
        var levels = new Dictionary<string, LogEventLevel>
        {
            ["DotNetCloud.Files"] = LogEventLevel.Warning
        };
        var filter = new ModuleLogFilter(moduleLevels: levels);

        var logEvent = CreateLogEvent(LogEventLevel.Warning, "DotNetCloud.Files");

        Assert.IsTrue(filter.IsEnabled(logEvent));
    }

    [TestMethod]
    public void IsEnabled_ModuleLevelAboveMinimum_ReturnsTrue()
    {
        var levels = new Dictionary<string, LogEventLevel>
        {
            ["DotNetCloud.Files"] = LogEventLevel.Warning
        };
        var filter = new ModuleLogFilter(moduleLevels: levels);

        var logEvent = CreateLogEvent(LogEventLevel.Error, "DotNetCloud.Files");

        Assert.IsTrue(filter.IsEnabled(logEvent));
    }

    [TestMethod]
    public void IsEnabled_ModuleWithNoSpecificLevel_ReturnsTrue()
    {
        var levels = new Dictionary<string, LogEventLevel>
        {
            ["DotNetCloud.Files"] = LogEventLevel.Warning
        };
        var filter = new ModuleLogFilter(moduleLevels: levels);

        var logEvent = CreateLogEvent(LogEventLevel.Debug, "DotNetCloud.Chat");

        Assert.IsTrue(filter.IsEnabled(logEvent));
    }

    [TestMethod]
    public void IsEnabled_NullConstructorParams_DoesNotThrow()
    {
        var filter = new ModuleLogFilter(null, null);
        var logEvent = CreateLogEvent(LogEventLevel.Information, "AnyModule");

        Assert.IsTrue(filter.IsEnabled(logEvent));
    }

    [TestMethod]
    public void IsEnabled_ExcludedTakesPrecedence_OverModuleLevel()
    {
        var excluded = new HashSet<string> { "DotNetCloud.Files" };
        var levels = new Dictionary<string, LogEventLevel>
        {
            ["DotNetCloud.Files"] = LogEventLevel.Debug
        };
        var filter = new ModuleLogFilter(excluded, levels);

        var logEvent = CreateLogEvent(LogEventLevel.Fatal, "DotNetCloud.Files");

        Assert.IsFalse(filter.IsEnabled(logEvent));
    }
}
