using DotNetCloud.Core.Modules.Supervisor;
using DotNetCloud.Core.Server.Supervisor;

namespace DotNetCloud.Core.Server.Tests.Supervisor;

[TestClass]
public class ModuleProcessHandleTests
{
    private ModuleProcessHandle CreateHandle(
        RestartPolicy policy = RestartPolicy.ExponentialBackoff,
        int maxRestartAttempts = 5)
    {
        return new ModuleProcessHandle
        {
            ModuleId = "test.module",
            ModuleName = "Test Module",
            Version = "1.0.0",
            ExecutablePath = "/opt/modules/test.module/test.module.dll",
            GrpcEndpoint = "unix:///run/dotnetcloud/test.module.sock",
            RestartPolicy = policy,
            MaxRestartAttempts = maxRestartAttempts
        };
    }

    [TestMethod]
    public void WhenCreatedThenStatusIsStopped()
    {
        using var handle = CreateHandle();

        Assert.AreEqual(ModuleProcessStatus.Stopped, handle.Status);
    }

    [TestMethod]
    public void WhenCreatedThenStartedAtIsNull()
    {
        using var handle = CreateHandle();

        Assert.IsNull(handle.StartedAt);
    }

    [TestMethod]
    public void WhenCreatedThenIsRunningIsFalse()
    {
        using var handle = CreateHandle();

        Assert.IsFalse(handle.IsRunning);
    }

    [TestMethod]
    public void WhenCreatedThenProcessIdIsNull()
    {
        using var handle = CreateHandle();

        Assert.IsNull(handle.ProcessId);
    }

    [TestMethod]
    public void WhenCreatedThenConsecutiveRestartsIsZero()
    {
        using var handle = CreateHandle();

        Assert.AreEqual(0, handle.ConsecutiveRestarts);
    }

    [TestMethod]
    public void WhenCreatedThenTotalRestartsIsZero()
    {
        using var handle = CreateHandle();

        Assert.AreEqual(0L, handle.TotalRestarts);
    }

    [TestMethod]
    public void WhenCreatedThenRequiredPropertiesAreSet()
    {
        using var handle = CreateHandle();

        Assert.AreEqual("test.module", handle.ModuleId);
        Assert.AreEqual("Test Module", handle.ModuleName);
        Assert.AreEqual("1.0.0", handle.Version);
        Assert.AreEqual("/opt/modules/test.module/test.module.dll", handle.ExecutablePath);
        Assert.AreEqual("unix:///run/dotnetcloud/test.module.sock", handle.GrpcEndpoint);
        Assert.AreEqual(RestartPolicy.ExponentialBackoff, handle.RestartPolicy);
        Assert.AreEqual(5, handle.MaxRestartAttempts);
    }

    [TestMethod]
    public void SetStatusToCrashedThenStatusIsCrashed()
    {
        using var handle = CreateHandle();

        handle.SetStatus(ModuleProcessStatus.Crashed);

        Assert.AreEqual(ModuleProcessStatus.Crashed, handle.Status);
    }

    [TestMethod]
    public void SetStatusWithErrorThenLastErrorIsSet()
    {
        using var handle = CreateHandle();

        handle.SetStatus(ModuleProcessStatus.Failed, "out of memory");

        Assert.AreEqual("out of memory", handle.LastError);
    }

    [TestMethod]
    public void SetStatusToStoppedThenStartedAtIsCleared()
    {
        using var handle = CreateHandle();
        handle.SetStatus(ModuleProcessStatus.Running);

        handle.SetStatus(ModuleProcessStatus.Stopped);

        Assert.IsNull(handle.StartedAt);
    }

    [TestMethod]
    public void SetStatusWithoutErrorThenPreviousErrorIsPreserved()
    {
        using var handle = CreateHandle();
        handle.SetStatus(ModuleProcessStatus.Failed, "first error");

        handle.SetStatus(ModuleProcessStatus.Starting);

        Assert.AreEqual("first error", handle.LastError);
    }

    [TestMethod]
    public void RecordHealthCheckThenLastHealthCheckAtIsSet()
    {
        using var handle = CreateHandle();
        var before = DateTime.UtcNow;

        handle.RecordHealthCheck();

        Assert.IsNotNull(handle.LastHealthCheckAt);
        Assert.IsTrue(handle.LastHealthCheckAt >= before);
    }

    [TestMethod]
    public void RecordHealthCheckWhenDegradedThenStatusIsRunning()
    {
        using var handle = CreateHandle();
        handle.SetStatus(ModuleProcessStatus.Degraded);

        handle.RecordHealthCheck();

        Assert.AreEqual(ModuleProcessStatus.Running, handle.Status);
    }

    [TestMethod]
    public void RecordHealthCheckWhenStoppedThenStatusIsUnchanged()
    {
        using var handle = CreateHandle();
        handle.SetStatus(ModuleProcessStatus.Stopped);

        handle.RecordHealthCheck();

        Assert.AreEqual(ModuleProcessStatus.Stopped, handle.Status);
    }

    [TestMethod]
    public void IncrementRestartCountThenBothCountersIncrease()
    {
        using var handle = CreateHandle();

        handle.IncrementRestartCount();

        Assert.AreEqual(1, handle.ConsecutiveRestarts);
        Assert.AreEqual(1L, handle.TotalRestarts);
    }

    [TestMethod]
    public void IncrementRestartCountMultipleTimesThenCountsAccumulate()
    {
        using var handle = CreateHandle();

        handle.IncrementRestartCount();
        handle.IncrementRestartCount();
        handle.IncrementRestartCount();

        Assert.AreEqual(3, handle.ConsecutiveRestarts);
        Assert.AreEqual(3L, handle.TotalRestarts);
    }

    [TestMethod]
    public void ResetRestartCountThenConsecutiveIsZeroButTotalIsPreserved()
    {
        using var handle = CreateHandle();
        handle.IncrementRestartCount();
        handle.IncrementRestartCount();

        handle.ResetRestartCount();

        Assert.AreEqual(0, handle.ConsecutiveRestarts);
        Assert.AreEqual(2L, handle.TotalRestarts);
    }

    [TestMethod]
    public void ToProcessInfoThenAllFieldsAreMapped()
    {
        using var handle = CreateHandle(RestartPolicy.Immediate, maxRestartAttempts: 10);
        handle.SetStatus(ModuleProcessStatus.Running);
        handle.RecordHealthCheck();
        handle.IncrementRestartCount();

        var info = handle.ToProcessInfo();

        Assert.AreEqual("test.module", info.ModuleId);
        Assert.AreEqual("Test Module", info.ModuleName);
        Assert.AreEqual("1.0.0", info.Version);
        Assert.AreEqual(ModuleProcessStatus.Running, info.Status);
        Assert.AreEqual("unix:///run/dotnetcloud/test.module.sock", info.GrpcEndpoint);
        Assert.AreEqual(RestartPolicy.Immediate, info.RestartPolicy);
        Assert.AreEqual(10, info.MaxRestartAttempts);
        Assert.AreEqual(1, info.ConsecutiveRestarts);
        Assert.AreEqual(1L, info.TotalRestarts);
        Assert.IsNotNull(info.LastHealthCheckAt);
    }

    [TestMethod]
    public void DisposeThenDoubleDisposeDoesNotThrow()
    {
        var handle = CreateHandle();

        handle.Dispose();
        handle.Dispose();
    }
}
