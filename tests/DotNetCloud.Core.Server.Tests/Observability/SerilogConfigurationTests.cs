using DotNetCloud.Core.ServiceDefaults.Logging;
using Serilog.Events;

namespace DotNetCloud.Core.Server.Tests.Observability;

[TestClass]
public class SerilogConfigurationTests
{
    [TestMethod]
    public void SerilogOptions_Defaults_AreCorrect()
    {
        var options = new SerilogOptions();

        Assert.AreEqual(LogEventLevel.Information, options.ConsoleMinimumLevel);
        Assert.AreEqual(LogEventLevel.Warning, options.FileMinimumLevel);
        Assert.AreEqual("logs/dotnetcloud-.log", options.FilePath);
        Assert.IsTrue(options.RollingDaily);
        Assert.AreEqual(31, options.RetainedFileCountLimit);
        Assert.AreEqual(100 * 1024 * 1024, options.FileSizeLimitBytes);
        Assert.IsTrue(options.UseStructuredFormat);
    }

    [TestMethod]
    public void SerilogOptions_ExcludedModules_DefaultsToEmpty()
    {
        var options = new SerilogOptions();

        Assert.IsNotNull(options.ExcludedModules);
        Assert.AreEqual(0, options.ExcludedModules.Count);
    }

    [TestMethod]
    public void SerilogOptions_ModuleLogLevels_DefaultsToEmpty()
    {
        var options = new SerilogOptions();

        Assert.IsNotNull(options.ModuleLogLevels);
        Assert.AreEqual(0, options.ModuleLogLevels.Count);
    }

    [TestMethod]
    public void SerilogOptions_ConsoleMinimumLevel_CanBeSet()
    {
        var options = new SerilogOptions { ConsoleMinimumLevel = LogEventLevel.Debug };

        Assert.AreEqual(LogEventLevel.Debug, options.ConsoleMinimumLevel);
    }

    [TestMethod]
    public void SerilogOptions_FileMinimumLevel_CanBeSet()
    {
        var options = new SerilogOptions { FileMinimumLevel = LogEventLevel.Error };

        Assert.AreEqual(LogEventLevel.Error, options.FileMinimumLevel);
    }

    [TestMethod]
    public void SerilogOptions_FilePath_CanBeCustomized()
    {
        var options = new SerilogOptions { FilePath = "custom/path/app-.log" };

        Assert.AreEqual("custom/path/app-.log", options.FilePath);
    }

    [TestMethod]
    public void SerilogOptions_RetainedFileCountLimit_CanBeCustomized()
    {
        var options = new SerilogOptions { RetainedFileCountLimit = 7 };

        Assert.AreEqual(7, options.RetainedFileCountLimit);
    }

    [TestMethod]
    public void SerilogOptions_FileSizeLimitBytes_CanBeNull()
    {
        var options = new SerilogOptions { FileSizeLimitBytes = null };

        Assert.IsNull(options.FileSizeLimitBytes);
    }

    [TestMethod]
    public void SerilogOptions_RollingDaily_CanBeDisabled()
    {
        var options = new SerilogOptions { RollingDaily = false };

        Assert.IsFalse(options.RollingDaily);
    }

    [TestMethod]
    public void SerilogOptions_ModuleLogLevels_CanBePopulated()
    {
        var options = new SerilogOptions();
        options.ModuleLogLevels["DotNetCloud.Files"] = LogEventLevel.Debug;
        options.ModuleLogLevels["DotNetCloud.Chat"] = LogEventLevel.Warning;

        Assert.AreEqual(2, options.ModuleLogLevels.Count);
        Assert.AreEqual(LogEventLevel.Debug, options.ModuleLogLevels["DotNetCloud.Files"]);
        Assert.AreEqual(LogEventLevel.Warning, options.ModuleLogLevels["DotNetCloud.Chat"]);
    }

    [TestMethod]
    public void SerilogOptions_ExcludedModules_CanBePopulated()
    {
        var options = new SerilogOptions();
        options.ExcludedModules.Add("DotNetCloud.Debug");

        Assert.AreEqual(1, options.ExcludedModules.Count);
        Assert.IsTrue(options.ExcludedModules.Contains("DotNetCloud.Debug"));
    }
}
