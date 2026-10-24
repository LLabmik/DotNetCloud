using DotNetCloud.Core.Modules.Supervisor;
using DotNetCloud.Core.Server.Diagnostics;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace DotNetCloud.Core.Server.Tests.Diagnostics;

/// <summary>
/// Tests for <see cref="SystemMetricsSampler"/> and <see cref="CpuUsageCalculator"/>.
/// </summary>
[TestClass]
public sealed class SystemMetricsSamplerTests
{
    [TestMethod]
    public void Compute_OneCore_UsesSingleCoreCapacity()
    {
        var percent = CpuUsageCalculator.Compute(TimeSpan.FromMilliseconds(500), TimeSpan.FromSeconds(1), 1);

        Assert.AreEqual(50d, percent);
    }

    [TestMethod]
    public void Compute_MultipleCores_DividesByProcessorCount()
    {
        var percent = CpuUsageCalculator.Compute(TimeSpan.FromMilliseconds(500), TimeSpan.FromSeconds(1), 4);

        Assert.AreEqual(12.5d, percent);
    }

    [TestMethod]
    public void Compute_ZeroWallWindow_ReturnsNull()
    {
        Assert.IsNull(CpuUsageCalculator.Compute(TimeSpan.FromSeconds(1), TimeSpan.Zero, 4));
    }

    [TestMethod]
    public void Compute_ZeroProcessorCount_ReturnsNull()
    {
        Assert.IsNull(CpuUsageCalculator.Compute(TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(1), 0));
    }

    [TestMethod]
    public void Compute_ClampsToHundred()
    {
        var percent = CpuUsageCalculator.Compute(TimeSpan.FromSeconds(10), TimeSpan.FromSeconds(1), 1);

        Assert.AreEqual(100d, percent);
    }

    [TestMethod]
    public void Sample_FirstCall_ReportsCpuUnavailable()
    {
        var sampler = CreateSampler(out _);

        var snapshot = sampler.Sample();

        Assert.IsFalse(snapshot.CpuAvailable);
        Assert.IsNull(snapshot.CpuPercent);
        Assert.IsTrue(snapshot.Processes.Count >= 1);
        Assert.AreEqual(SystemMetricsSampler.CoreProcessId, snapshot.Processes[0].Id);
    }

    [TestMethod]
    public void Sample_SecondCall_ComputesModuleCpuFromDelta()
    {
        var clock = new ManualTimeProvider();
        var moduleCpu = TimeSpan.FromSeconds(1);
        var supervisor = CreateSupervisor(new ModuleProcessInfo
        {
            ModuleId = "dotnetcloud.files",
            ModuleName = "Files",
            Version = "1.0.0",
            Status = ModuleProcessStatus.Running,
            ProcessId = 4321,
            RestartPolicy = RestartPolicy.Immediate,
            CpuTime = moduleCpu,
            MemoryUsageBytes = 100 * 1024 * 1024,
        });

        var sampler = CreateSampler(supervisor, clock);
        sampler.Sample();

        clock.Advance(TimeSpan.FromSeconds(2));
        moduleCpu += TimeSpan.FromSeconds(1);
        supervisor.Setup(s => s.GetAllModuleInfo()).Returns([CreateModule(moduleCpu)]);

        var snapshot = sampler.Sample();
        var module = snapshot.Processes.Single(p => p.Id == "dotnetcloud.files");

        var expected = 1d / (2 * Environment.ProcessorCount) * 100d;
        Assert.IsNotNull(module.CpuPercent);
        Assert.AreEqual(expected, module.CpuPercent!.Value, 0.01);
        Assert.AreEqual(100L * 1024 * 1024, module.WorkingSetBytes);
        Assert.IsTrue(snapshot.CpuAvailable);
    }

    [TestMethod]
    public void Sample_ProcessIdChange_ResetsBaseline()
    {
        var clock = new ManualTimeProvider();
        var supervisor = CreateSupervisor(CreateModule(TimeSpan.FromSeconds(5), pid: 1111));

        var sampler = CreateSampler(supervisor, clock);
        sampler.Sample();

        clock.Advance(TimeSpan.FromSeconds(1));
        supervisor.Setup(s => s.GetAllModuleInfo()).Returns([CreateModule(TimeSpan.FromSeconds(9), pid: 2222)]);

        var snapshot = sampler.Sample();
        var module = snapshot.Processes.Single(p => p.Id == "dotnetcloud.files");

        Assert.IsNull(module.CpuPercent, "A restarted module has no baseline for the new PID.");
    }

    [TestMethod]
    public void Sample_ExitedModuleWithNullCpuTime_IsTolerated()
    {
        var supervisor = CreateSupervisor(new ModuleProcessInfo
        {
            ModuleId = "dotnetcloud.video",
            ModuleName = "Video",
            Version = "1.0.0",
            Status = ModuleProcessStatus.Stopped,
            ProcessId = 999_999,
            RestartPolicy = RestartPolicy.Immediate,
            CpuTime = null,
            MemoryUsageBytes = null,
        });

        var sampler = CreateSampler(supervisor, new ManualTimeProvider());

        var snapshot = sampler.Sample();
        var module = snapshot.Processes.Single(p => p.Id == "dotnetcloud.video");

        Assert.IsNull(module.CpuPercent);
        Assert.IsNull(module.WorkingSetBytes);
        Assert.IsNull(module.GcHeapBytes);
    }

    [TestMethod]
    public void Sample_ModuleWithoutProcess_IsSkipped()
    {
        var supervisor = CreateSupervisor(new ModuleProcessInfo
        {
            ModuleId = "dotnetcloud.notes",
            ModuleName = "Notes",
            Version = "1.0.0",
            Status = ModuleProcessStatus.Stopped,
            ProcessId = null,
            RestartPolicy = RestartPolicy.Immediate,
        });

        var sampler = CreateSampler(supervisor, new ManualTimeProvider());

        var snapshot = sampler.Sample();

        Assert.IsFalse(snapshot.Processes.Any(p => p.Id == "dotnetcloud.notes"));
    }

    [TestMethod]
    public void Sample_ExposesUptimeAndProcessorCount()
    {
        var sampler = CreateSampler(out _);

        var snapshot = sampler.Sample();

        Assert.AreEqual(Environment.ProcessorCount, snapshot.ProcessorCount);
        Assert.IsTrue(snapshot.UptimeSeconds >= 0);
        Assert.IsTrue(snapshot.WorkingSetBytes > 0);
        Assert.IsTrue(snapshot.GcHeapBytes > 0);
    }

    private static SystemMetricsSampler CreateSampler(out Mock<IProcessSupervisor> supervisor)
    {
        supervisor = new Mock<IProcessSupervisor>();
        supervisor.Setup(s => s.GetAllModuleInfo()).Returns([]);

        return CreateSampler(supervisor, new ManualTimeProvider());
    }

    private static SystemMetricsSampler CreateSampler(Mock<IProcessSupervisor> supervisor, TimeProvider timeProvider) =>
        new(
            supervisor.Object,
            new TrafficAccounting(timeProvider),
            timeProvider,
            NullLogger<SystemMetricsSampler>.Instance);

    private static Mock<IProcessSupervisor> CreateSupervisor(params ModuleProcessInfo[] modules)
    {
        var supervisor = new Mock<IProcessSupervisor>();
        supervisor.Setup(s => s.GetAllModuleInfo()).Returns(modules);
        return supervisor;
    }

    private static ModuleProcessInfo CreateModule(TimeSpan cpuTime, int pid = 4321) => new()
    {
        ModuleId = "dotnetcloud.files",
        ModuleName = "Files",
        Version = "1.0.0",
        Status = ModuleProcessStatus.Running,
        ProcessId = pid,
        RestartPolicy = RestartPolicy.Immediate,
        CpuTime = cpuTime,
        MemoryUsageBytes = 100 * 1024 * 1024,
    };

    /// <summary>Deterministic clock for delta math.</summary>
    private sealed class ManualTimeProvider : TimeProvider
    {
        private DateTimeOffset _utcNow = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
        private long _timestamp;

        public override DateTimeOffset GetUtcNow() => _utcNow;

        public override long GetTimestamp() => _timestamp;

        public override long TimestampFrequency => TimeSpan.TicksPerSecond;

        public void Advance(TimeSpan by)
        {
            _utcNow += by;
            _timestamp += by.Ticks;
        }
    }
}
