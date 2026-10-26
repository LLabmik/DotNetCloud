using System.Reflection;
using System.Text.Json;
using DotNetCloud.Core.Auth.Authorization;
using DotNetCloud.Core.Capabilities;
using DotNetCloud.Core.DTOs;
using DotNetCloud.Core.Modules.Supervisor;
using DotNetCloud.Core.Server.Controllers;
using DotNetCloud.Core.Server.Diagnostics;
using DotNetCloud.Core.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace DotNetCloud.Core.Server.Tests.Controllers;

/// <summary>
/// Tests for <see cref="AdminSystemController"/>.
/// </summary>
[TestClass]
public sealed class AdminSystemControllerTests
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    [TestMethod]
    public void Controller_RequiresAdminPolicy()
    {
        var authorize = typeof(AdminSystemController).GetCustomAttribute<AuthorizeAttribute>();

        Assert.IsNotNull(authorize);
        Assert.AreEqual(AuthorizationPolicies.RequireAdmin, authorize!.Policy);
    }

    [TestMethod]
    public async Task GetSystemStatusAsync_ReturnsPresenceTrafficAndProcessPayload()
    {
        var presence = new Mock<IPresenceTracker>();
        presence.Setup(p => p.GetOnlineUsersAsync()).ReturnsAsync(new HashSet<Guid> { Guid.NewGuid(), Guid.NewGuid() } as IReadOnlySet<Guid>);
        presence.Setup(p => p.GetActiveConnectionCountAsync()).ReturnsAsync(3);

        var tracker = new Mock<IBackgroundServiceTracker>();
        tracker.Setup(t => t.GetAll()).Returns(new Dictionary<string, BackgroundServiceStatus>
        {
            ["Backup Hosted Service"] = new()
            {
                ServiceName = "Backup Hosted Service",
                LastRunAt = DateTimeOffset.UtcNow.AddMinutes(-5),
                LastRunDuration = TimeSpan.FromSeconds(3),
                LastRunSuccess = true,
                LastRunMessage = "done",
                TotalRuns = 4,
            },
        });

        var traffic = new TrafficAccounting(TimeProvider.System);
        traffic.AddIn(1000);
        traffic.AddOut(2500);

        var controller = CreateController(tracker.Object, traffic, presence.Object, out var sampler);

        // Populate the cached snapshot the way the hosted service does.
        sampler.Sample();

        var result = await controller.GetSystemStatusAsync();

        var ok = result as OkObjectResult;
        Assert.IsNotNull(ok);

        using var document = JsonDocument.Parse(JsonSerializer.Serialize(ok!.Value, JsonOptions));
        var root = document.RootElement;

        Assert.IsTrue(root.GetProperty("success").GetBoolean());
        var data = root.GetProperty("data");

        Assert.AreEqual(2, data.GetProperty("users").GetProperty("online").GetInt32());
        Assert.AreEqual(3, data.GetProperty("users").GetProperty("connections").GetInt32());

        var trafficJson = data.GetProperty("traffic");
        Assert.AreEqual(1000, trafficJson.GetProperty("bytesIn").GetInt64());
        Assert.AreEqual(2500, trafficJson.GetProperty("bytesOut").GetInt64());
        Assert.AreEqual(3500, trafficJson.GetProperty("bytesTotal").GetInt64());

        var tasks = data.GetProperty("tasks");
        Assert.AreEqual(1, tasks.GetArrayLength());
        Assert.AreEqual("core", tasks[0].GetProperty("module").GetString());
        Assert.AreEqual("Backup Hosted Service", tasks[0].GetProperty("serviceName").GetString());

        var processes = data.GetProperty("processes");
        Assert.IsTrue(processes.GetArrayLength() >= 1);
        Assert.AreEqual("core", processes[0].GetProperty("id").GetString());
    }

    [TestMethod]
    public async Task GetSystemStatusAsync_WhenModuleCallFails_ReportsUnavailableModule()
    {
        var module = new ModuleProcessInfo
        {
            ModuleId = "dotnetcloud.video",
            ModuleName = "Video",
            Version = "1.0.0",
            Status = ModuleProcessStatus.Running,
            ProcessId = 4242,
            RestartPolicy = RestartPolicy.Immediate,
            // Nothing listens here, so the gRPC call fails (or times out).
            GrpcEndpoint = "http://127.0.0.1:1",
        };

        var supervisor = new Mock<IProcessSupervisor>();
        supervisor.Setup(s => s.GetAllModuleInfo()).Returns([module]);

        var presence = new Mock<IPresenceTracker>();
        presence.Setup(p => p.GetOnlineUsersAsync()).ReturnsAsync(new HashSet<Guid>() as IReadOnlySet<Guid>);
        presence.Setup(p => p.GetActiveConnectionCountAsync()).ReturnsAsync(0);

        var controller = CreateController(new BackgroundServiceTracker(), new TrafficAccounting(TimeProvider.System), presence.Object, out _, supervisor);

        var result = await controller.GetSystemStatusAsync();

        var ok = result as OkObjectResult;
        Assert.IsNotNull(ok);

        using var document = JsonDocument.Parse(JsonSerializer.Serialize(ok!.Value, JsonOptions));
        var unavailable = document.RootElement.GetProperty("data").GetProperty("unavailableModules");

        Assert.AreEqual(1, unavailable.GetArrayLength());
        Assert.AreEqual("dotnetcloud.video", unavailable[0].GetString());
    }

    [TestMethod]
    public async Task GetSystemStatusAsync_WhenNoSnapshotYet_StillReturns200()
    {
        var presence = new Mock<IPresenceTracker>();
        presence.Setup(p => p.GetOnlineUsersAsync()).ReturnsAsync(new HashSet<Guid>() as IReadOnlySet<Guid>);
        presence.Setup(p => p.GetActiveConnectionCountAsync()).ReturnsAsync(0);

        var controller = CreateController(new BackgroundServiceTracker(), new TrafficAccounting(TimeProvider.System), presence.Object, out _);

        var result = await controller.GetSystemStatusAsync();

        var ok = result as OkObjectResult;
        Assert.IsNotNull(ok);

        using var document = JsonDocument.Parse(JsonSerializer.Serialize(ok!.Value, JsonOptions));
        var data = document.RootElement.GetProperty("data");

        Assert.AreEqual(Environment.ProcessorCount, data.GetProperty("processorCount").GetInt32());
        Assert.IsFalse(data.GetProperty("cpuAvailable").GetBoolean());
    }

    [TestMethod]
    public async Task GetSystemStatusAsync_WithTrackedUsers_ReturnsPerUserRowsWithPresence()
    {
        var busyUser = Guid.NewGuid();
        var quietUser = Guid.NewGuid();

        var presence = new Mock<IPresenceTracker>();
        presence.Setup(p => p.GetOnlineUsersAsync()).ReturnsAsync(new HashSet<Guid>() as IReadOnlySet<Guid>);
        presence.Setup(p => p.GetActiveConnectionCountAsync()).ReturnsAsync(0);
        presence.Setup(p => p.GetOnlineStatusAsync(It.IsAny<IEnumerable<Guid>>()))
            .ReturnsAsync(new Dictionary<Guid, PresenceState>
            {
                [busyUser] = PresenceState.Online,
                [quietUser] = PresenceState.Offline,
            });

        var userDirectory = new Mock<IUserDirectory>();
        userDirectory
            .Setup(u => u.GetDisplayNamesAsync(It.IsAny<IEnumerable<Guid>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<Guid, string> { [busyUser] = "Ben", [quietUser] = "Ada" });

        var traffic = new TrafficAccounting(TimeProvider.System);
        traffic.AddOut(quietUser, 100);
        traffic.AddOut(busyUser, 9_000);
        traffic.AddIn(busyUser, 1_000);

        var controller = CreateController(
            new BackgroundServiceTracker(),
            traffic,
            presence.Object,
            out _,
            userDirectory: userDirectory.Object);

        var result = await controller.GetSystemStatusAsync();

        var ok = result as OkObjectResult;
        Assert.IsNotNull(ok);

        using var document = JsonDocument.Parse(JsonSerializer.Serialize(ok!.Value, JsonOptions));
        var usersTraffic = document.RootElement.GetProperty("data").GetProperty("usersTraffic");

        Assert.AreEqual(2, usersTraffic.GetProperty("total").GetInt32());
        Assert.AreEqual(2, usersTraffic.GetProperty("showing").GetInt32());

        var entries = usersTraffic.GetProperty("entries");
        Assert.AreEqual(2, entries.GetArrayLength());

        // Ordered by total bytes descending.
        Assert.AreEqual("Ben", entries[0].GetProperty("displayName").GetString());
        Assert.AreEqual(10_000, entries[0].GetProperty("bytesTotal").GetInt64());
        Assert.AreEqual("Online", entries[0].GetProperty("presenceState").GetString());
        Assert.IsFalse(entries[0].GetProperty("isIdle").GetBoolean());

        // A user transferring without a hub connection reports NotConnected, not an error.
        Assert.AreEqual("Ada", entries[1].GetProperty("displayName").GetString());
        Assert.AreEqual("NotConnected", entries[1].GetProperty("presenceState").GetString());
    }

    [TestMethod]
    public async Task GetSystemStatusAsync_MoreThan25TrackedUsers_TruncatesToTop25()
    {
        var presence = new Mock<IPresenceTracker>();
        presence.Setup(p => p.GetOnlineUsersAsync()).ReturnsAsync(new HashSet<Guid>() as IReadOnlySet<Guid>);
        presence.Setup(p => p.GetActiveConnectionCountAsync()).ReturnsAsync(0);

        var traffic = new TrafficAccounting(TimeProvider.System);
        for (var i = 0; i < 30; i++)
        {
            traffic.AddOut(Guid.NewGuid(), (i + 1) * 100L);
        }

        var controller = CreateController(new BackgroundServiceTracker(), traffic, presence.Object, out _);

        var result = await controller.GetSystemStatusAsync();

        var ok = result as OkObjectResult;
        Assert.IsNotNull(ok);

        using var document = JsonDocument.Parse(JsonSerializer.Serialize(ok!.Value, JsonOptions));
        var usersTraffic = document.RootElement.GetProperty("data").GetProperty("usersTraffic");

        Assert.AreEqual(30, usersTraffic.GetProperty("total").GetInt32());
        Assert.AreEqual(AdminSystemController.MaxTrafficRows, usersTraffic.GetProperty("showing").GetInt32());
        Assert.AreEqual(AdminSystemController.MaxTrafficRows, usersTraffic.GetProperty("entries").GetArrayLength());
    }

    private static AdminSystemController CreateController(
        IBackgroundServiceTracker tracker,
        TrafficAccounting traffic,
        IPresenceTracker presence,
        out SystemMetricsSampler sampler,
        Mock<IProcessSupervisor>? supervisor = null,
        IUserDirectory? userDirectory = null)
    {
        if (supervisor is null)
        {
            supervisor = new Mock<IProcessSupervisor>();
            supervisor.Setup(s => s.GetAllModuleInfo()).Returns([]);
        }

        if (userDirectory is null)
        {
            var userDirectoryMock = new Mock<IUserDirectory>();
            userDirectoryMock
                .Setup(u => u.GetDisplayNamesAsync(It.IsAny<IEnumerable<Guid>>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((IReadOnlyDictionary<Guid, string>)new Dictionary<Guid, string>());
            userDirectory = userDirectoryMock.Object;
        }

        sampler = new SystemMetricsSampler(
            supervisor.Object,
            traffic,
            TimeProvider.System,
            NullLogger<SystemMetricsSampler>.Instance);

        return new AdminSystemController(
            sampler,
            traffic,
            presence,
            userDirectory,
            tracker,
            supervisor.Object,
            Mock.Of<DotNetCloud.Core.Server.Supervisor.IModuleGrpcChannelProvider>(),
            NullLogger<AdminSystemController>.Instance);
    }
}
