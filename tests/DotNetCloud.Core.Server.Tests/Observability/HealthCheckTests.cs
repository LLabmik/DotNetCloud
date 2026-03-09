using DotNetCloud.Core.ServiceDefaults.HealthChecks;
using DotNetCloud.Core.Server.HealthChecks;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Logging.Abstractions;

namespace DotNetCloud.Core.Server.Tests.Observability;

[TestClass]
public class HealthCheckTests
{
    // -----------------------------------------------------------------------
    // StartupHealthCheck
    // -----------------------------------------------------------------------

    [TestMethod]
    public async Task StartupHealthCheck_BeforeMarkReady_ReturnsUnhealthy()
    {
        var check = new StartupHealthCheck();

        var result = await check.CheckHealthAsync(
            new HealthCheckContext(), CancellationToken.None);

        Assert.AreEqual(HealthCheckResult.Unhealthy().Status, result.Status);
    }

    [TestMethod]
    public async Task StartupHealthCheck_AfterMarkReady_ReturnsHealthy()
    {
        var check = new StartupHealthCheck();
        check.MarkReady();

        var result = await check.CheckHealthAsync(
            new HealthCheckContext(), CancellationToken.None);

        Assert.AreEqual(HealthStatus.Healthy, result.Status);
    }

    [TestMethod]
    public async Task StartupHealthCheck_MarkReadyMultipleTimes_StaysHealthy()
    {
        var check = new StartupHealthCheck();
        check.MarkReady();
        check.MarkReady();

        var result = await check.CheckHealthAsync(
            new HealthCheckContext(), CancellationToken.None);

        Assert.AreEqual(HealthStatus.Healthy, result.Status);
    }

    [TestMethod]
    public async Task StartupHealthCheck_Description_IndicatesState()
    {
        var check = new StartupHealthCheck();

        var notReady = await check.CheckHealthAsync(
            new HealthCheckContext(), CancellationToken.None);
        Assert.IsTrue(notReady.Description!.Contains("starting"));

        check.MarkReady();
        var ready = await check.CheckHealthAsync(
            new HealthCheckContext(), CancellationToken.None);
        Assert.IsTrue(ready.Description!.Contains("completed"));
    }

    // -----------------------------------------------------------------------
    // ModuleHealthCheckResult
    // -----------------------------------------------------------------------

    [TestMethod]
    public void ModuleHealthCheckResult_Healthy_ReturnsCorrectStatus()
    {
        var result = ModuleHealthCheckResult.Healthy("All good");

        Assert.AreEqual(ModuleHealthStatus.Healthy, result.Status);
        Assert.AreEqual("All good", result.Description);
        Assert.IsNull(result.Exception);
    }

    [TestMethod]
    public void ModuleHealthCheckResult_Degraded_ReturnsCorrectStatus()
    {
        var result = ModuleHealthCheckResult.Degraded("Slow response");

        Assert.AreEqual(ModuleHealthStatus.Degraded, result.Status);
        Assert.AreEqual("Slow response", result.Description);
    }

    [TestMethod]
    public void ModuleHealthCheckResult_Unhealthy_ReturnsCorrectStatus()
    {
        var ex = new InvalidOperationException("Connection failed");
        var result = ModuleHealthCheckResult.Unhealthy("Down", ex);

        Assert.AreEqual(ModuleHealthStatus.Unhealthy, result.Status);
        Assert.AreEqual("Down", result.Description);
        Assert.AreSame(ex, result.Exception);
    }

    [TestMethod]
    public void ModuleHealthCheckResult_Healthy_WithData()
    {
        var data = new Dictionary<string, object> { ["connections"] = 42 };
        var result = ModuleHealthCheckResult.Healthy("OK", data);

        Assert.IsNotNull(result.Data);
        Assert.AreEqual(42, result.Data["connections"]);
    }

    // -----------------------------------------------------------------------
    // ModuleHealthCheckAdapter
    // -----------------------------------------------------------------------

    [TestMethod]
    public async Task ModuleHealthCheckAdapter_Healthy_MapsToAspNetHealthy()
    {
        var moduleCheck = new FakeModuleHealthCheck("test-module",
            ModuleHealthCheckResult.Healthy("Module is healthy"));
        var adapter = new ModuleHealthCheckAdapter(moduleCheck);

        var result = await adapter.CheckHealthAsync(
            new HealthCheckContext(), CancellationToken.None);

        Assert.AreEqual(HealthStatus.Healthy, result.Status);
        Assert.AreEqual("Module is healthy", result.Description);
    }

    [TestMethod]
    public async Task ModuleHealthCheckAdapter_Degraded_MapsToAspNetDegraded()
    {
        var moduleCheck = new FakeModuleHealthCheck("test-module",
            ModuleHealthCheckResult.Degraded("Slow"));
        var adapter = new ModuleHealthCheckAdapter(moduleCheck);

        var result = await adapter.CheckHealthAsync(
            new HealthCheckContext(), CancellationToken.None);

        Assert.AreEqual(HealthStatus.Degraded, result.Status);
    }

    [TestMethod]
    public async Task ModuleHealthCheckAdapter_Unhealthy_MapsToAspNetUnhealthy()
    {
        var moduleCheck = new FakeModuleHealthCheck("test-module",
            ModuleHealthCheckResult.Unhealthy("Down"));
        var adapter = new ModuleHealthCheckAdapter(moduleCheck);

        var result = await adapter.CheckHealthAsync(
            new HealthCheckContext(), CancellationToken.None);

        Assert.AreEqual(HealthStatus.Unhealthy, result.Status);
    }

    [TestMethod]
    public async Task ModuleHealthCheckAdapter_WhenCheckThrows_ReturnsUnhealthy()
    {
        var moduleCheck = new ThrowingModuleHealthCheck("broken-module");
        var adapter = new ModuleHealthCheckAdapter(moduleCheck);

        var result = await adapter.CheckHealthAsync(
            new HealthCheckContext(), CancellationToken.None);

        Assert.AreEqual(HealthStatus.Unhealthy, result.Status);
        Assert.IsTrue(result.Description!.Contains("broken-module"));
    }

    [TestMethod]
    public void ModuleHealthCheckAdapter_NullCheck_ThrowsArgumentNullException()
    {
        Assert.ThrowsExactly<ArgumentNullException>(
            () => new ModuleHealthCheckAdapter(null!));
    }

    // -----------------------------------------------------------------------
    // ModuleHealthStatus enum
    // -----------------------------------------------------------------------

    [TestMethod]
    public void ModuleHealthStatus_HasExpectedValues()
    {
        var values = Enum.GetValues<ModuleHealthStatus>();

        Assert.AreEqual(3, values.Length);
        CollectionAssert.Contains(values, ModuleHealthStatus.Healthy);
        CollectionAssert.Contains(values, ModuleHealthStatus.Degraded);
        CollectionAssert.Contains(values, ModuleHealthStatus.Unhealthy);
    }

    // -----------------------------------------------------------------------
    // Test helpers
    // -----------------------------------------------------------------------

    private sealed class FakeModuleHealthCheck : IModuleHealthCheck
    {
        private readonly ModuleHealthCheckResult _result;

        public FakeModuleHealthCheck(string moduleName, ModuleHealthCheckResult result)
        {
            ModuleName = moduleName;
            _result = result;
        }

        public string ModuleName { get; }

        public Task<ModuleHealthCheckResult> CheckHealthAsync(CancellationToken cancellationToken = default)
            => Task.FromResult(_result);
    }

    private sealed class ThrowingModuleHealthCheck : IModuleHealthCheck
    {
        public ThrowingModuleHealthCheck(string moduleName) => ModuleName = moduleName;

        public string ModuleName { get; }

        public Task<ModuleHealthCheckResult> CheckHealthAsync(CancellationToken cancellationToken = default)
            => throw new InvalidOperationException("Module crashed");
    }
}

// -----------------------------------------------------------------------
// LinuxResourceHealthCheck (Task 4.4)
// -----------------------------------------------------------------------

[TestClass]
public class LinuxResourceHealthCheckTests
{
    [TestMethod]
    public async Task CheckHealthAsync_OnNonLinux_ReturnsHealthy()
    {
        if (OperatingSystem.IsLinux())
            Assert.Inconclusive("This test only runs on non-Linux platforms.");

        var check = new LinuxResourceHealthCheck("/tmp", NullLogger<LinuxResourceHealthCheck>.Instance);

        var result = await check.CheckHealthAsync(new HealthCheckContext(), CancellationToken.None);

        Assert.AreEqual(HealthStatus.Healthy, result.Status);
    }

    [TestMethod]
    public void ReadInotifyWatchLimit_OnLinux_ReturnsPositiveOrNegativeOne()
    {
        if (!OperatingSystem.IsLinux())
            Assert.Inconclusive("inotify is Linux-only.");

        var value = LinuxResourceHealthCheck.ReadInotifyWatchLimit();

        Assert.IsTrue(value == -1 || value > 0, $"Expected positive int or -1, got {value}");
    }

    [TestMethod]
    public void TryGetInodeInfo_OnNonLinux_ReturnsFalse()
    {
        if (OperatingSystem.IsLinux())
            Assert.Inconclusive("This test only runs on non-Linux platforms.");

        var result = LinuxResourceHealthCheck.TryGetInodeInfo("/tmp", out _, out _);

        Assert.IsFalse(result);
    }

    [TestMethod]
    public void MinRecommendedWatches_Is65536()
    {
        Assert.AreEqual(65536, LinuxResourceHealthCheck.MinRecommendedWatches);
    }

    [TestMethod]
    public void InodeDegradedThreshold_Is10Percent()
    {
        Assert.AreEqual(0.10, LinuxResourceHealthCheck.InodeDegradedThreshold, delta: 0.001);
    }

    [TestMethod]
    public void InodeUnhealthyThreshold_Is2Percent()
    {
        Assert.AreEqual(0.02, LinuxResourceHealthCheck.InodeUnhealthyThreshold, delta: 0.001);
    }
}
