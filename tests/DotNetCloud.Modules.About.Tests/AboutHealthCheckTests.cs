using Microsoft.Extensions.Diagnostics.HealthChecks;
using DotNetCloud.Modules.About.Host.Services;

namespace DotNetCloud.Modules.About.Tests;

/// <summary>
/// Tests for <see cref="AboutHealthCheck"/>.
/// </summary>
[TestClass]
public class AboutHealthCheckTests
{
    [TestMethod]
    public async Task CheckHealthAsync_ReturnsHealthy()
    {
        var module = new AboutModule();
        var healthCheck = new AboutHealthCheck(module);
        var context = new HealthCheckContext();

        var result = await healthCheck.CheckHealthAsync(context);

        Assert.IsNotNull(result);
        Assert.AreEqual(HealthStatus.Healthy, result.Status);
    }

    [TestMethod]
    public async Task CheckHealthAsync_ContainsModuleMetadata()
    {
        var module = new AboutModule();
        var healthCheck = new AboutHealthCheck(module);
        var context = new HealthCheckContext();

        var result = await healthCheck.CheckHealthAsync(context);

        Assert.IsNotNull(result.Data);
        Assert.IsTrue(result.Data.ContainsKey("module_id"));
        Assert.AreEqual("dotnetcloud.about", result.Data["module_id"]);
    }

    [TestMethod]
    public async Task CheckHealthAsync_DataContainsModuleVersion()
    {
        var module = new AboutModule();
        var healthCheck = new AboutHealthCheck(module);
        var context = new HealthCheckContext();

        var result = await healthCheck.CheckHealthAsync(context);

        Assert.IsNotNull(result.Data);
        Assert.IsTrue(result.Data.ContainsKey("version"));
        Assert.IsFalse(string.IsNullOrWhiteSpace(result.Data["version"]?.ToString()));
    }
}
