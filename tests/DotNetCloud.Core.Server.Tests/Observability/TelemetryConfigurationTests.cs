using DotNetCloud.Core.ServiceDefaults.Telemetry;

namespace DotNetCloud.Core.Server.Tests.Observability;

[TestClass]
public class TelemetryConfigurationTests
{
    [TestMethod]
    public void TelemetryOptions_Defaults_AreCorrect()
    {
        var options = new TelemetryOptions();

        Assert.AreEqual("DotNetCloud", options.ServiceName);
        Assert.AreEqual("1.0.0", options.ServiceVersion);
        Assert.IsTrue(options.EnableMetrics);
        Assert.IsTrue(options.EnableTracing);
        Assert.IsNull(options.OtlpEndpoint);
        Assert.IsFalse(options.EnableConsoleExporter);
        Assert.IsFalse(options.EnablePrometheusExporter);
    }

    [TestMethod]
    public void TelemetryOptions_AdditionalSources_DefaultsToEmpty()
    {
        var options = new TelemetryOptions();

        Assert.IsNotNull(options.AdditionalSources);
        Assert.AreEqual(0, options.AdditionalSources.Count);
    }

    [TestMethod]
    public void TelemetryOptions_AdditionalMeters_DefaultsToEmpty()
    {
        var options = new TelemetryOptions();

        Assert.IsNotNull(options.AdditionalMeters);
        Assert.AreEqual(0, options.AdditionalMeters.Count);
    }

    [TestMethod]
    public void TelemetryOptions_ServiceName_CanBeCustomized()
    {
        var options = new TelemetryOptions { ServiceName = "MyService" };

        Assert.AreEqual("MyService", options.ServiceName);
    }

    [TestMethod]
    public void TelemetryOptions_OtlpEndpoint_CanBeSet()
    {
        var options = new TelemetryOptions { OtlpEndpoint = "http://localhost:4317" };

        Assert.AreEqual("http://localhost:4317", options.OtlpEndpoint);
    }

    [TestMethod]
    public void TelemetryOptions_EnablePrometheusExporter_CanBeEnabled()
    {
        var options = new TelemetryOptions { EnablePrometheusExporter = true };

        Assert.IsTrue(options.EnablePrometheusExporter);
    }

    [TestMethod]
    public void TelemetryOptions_EnableConsoleExporter_CanBeEnabled()
    {
        var options = new TelemetryOptions { EnableConsoleExporter = true };

        Assert.IsTrue(options.EnableConsoleExporter);
    }

    [TestMethod]
    public void TelemetryOptions_MetricsAndTracing_CanBeDisabled()
    {
        var options = new TelemetryOptions
        {
            EnableMetrics = false,
            EnableTracing = false
        };

        Assert.IsFalse(options.EnableMetrics);
        Assert.IsFalse(options.EnableTracing);
    }

    [TestMethod]
    public void TelemetryOptions_AdditionalSources_CanBePopulated()
    {
        var options = new TelemetryOptions();
        options.AdditionalSources.Add("MyModule.Operations");
        options.AdditionalSources.Add("MyModule.DataAccess");

        Assert.AreEqual(2, options.AdditionalSources.Count);
    }

    [TestMethod]
    public void TelemetryOptions_AdditionalMeters_CanBePopulated()
    {
        var options = new TelemetryOptions();
        options.AdditionalMeters.Add("MyModule.Requests");

        Assert.AreEqual(1, options.AdditionalMeters.Count);
        Assert.AreEqual("MyModule.Requests", options.AdditionalMeters[0]);
    }

    [TestMethod]
    public void TelemetryActivitySources_Core_IsNotNull()
    {
        Assert.IsNotNull(TelemetryActivitySources.Core);
        Assert.AreEqual("DotNetCloud.Core", TelemetryActivitySources.Core.Name);
    }

    [TestMethod]
    public void TelemetryActivitySources_Modules_IsNotNull()
    {
        Assert.IsNotNull(TelemetryActivitySources.Modules);
        Assert.AreEqual("DotNetCloud.Modules", TelemetryActivitySources.Modules.Name);
    }

    [TestMethod]
    public void TelemetryActivitySources_Authentication_IsNotNull()
    {
        Assert.IsNotNull(TelemetryActivitySources.Authentication);
        Assert.AreEqual("DotNetCloud.Authentication", TelemetryActivitySources.Authentication.Name);
    }

    [TestMethod]
    public void TelemetryActivitySources_Authorization_IsNotNull()
    {
        Assert.IsNotNull(TelemetryActivitySources.Authorization);
        Assert.AreEqual("DotNetCloud.Authorization", TelemetryActivitySources.Authorization.Name);
    }
}
