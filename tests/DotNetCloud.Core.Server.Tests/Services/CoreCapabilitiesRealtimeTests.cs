using DotNetCloud.Core.Capabilities;
using DotNetCloud.Core.Grpc.Capabilities;
using DotNetCloud.Core.Server.Grpc.Services;
using DotNetCloud.Core.Server.ModuleLoading;
using DotNetCloud.Core.Server.RealTime;
using DotNetCloud.Core.Server.Services;
using Grpc.Core;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace DotNetCloud.Core.Server.Tests.Services;

/// <summary>
/// Tests for the real-time broadcast allow-list: a module may only broadcast events its manifest
/// declares, and an unknown module fails closed.
/// </summary>
[TestClass]
public class CoreCapabilitiesRealtimeTests
{
    private const string ModuleId = "dotnetcloud.chat";

    [TestMethod]
    public async Task BroadcastRealtimeEvent_DeclaredEventName_IsForwarded()
    {
        var broadcaster = new Mock<IRealtimeBroadcaster>();
        var service = CreateService(broadcaster, ["NewMessage"]);

        var response = await service.BroadcastRealtimeEvent(
            Request("NewMessage"), AuthenticatedContext());

        Assert.IsTrue(response.Success);
        broadcaster.Verify(
            b => b.BroadcastAsync("chat-channel-1", "NewMessage", It.IsAny<object>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [TestMethod]
    public async Task BroadcastRealtimeEvent_UndeclaredEventName_IsRejectedAndAudited()
    {
        var broadcaster = new Mock<IRealtimeBroadcaster>();
        var audit = new RecordingAuditLogger();
        var service = CreateService(broadcaster, ["NewMessage"], audit);

        var ex = await Assert.ThrowsExactlyAsync<RpcException>(
            () => service.BroadcastRealtimeEvent(Request("TracksActivity"), AuthenticatedContext()));

        Assert.AreEqual(StatusCode.PermissionDenied, ex.StatusCode);
        Assert.AreEqual("MODULE_EVENT_REJECTED", ex.Trailers.GetValue("error-code"));
        Assert.AreEqual(AuditAction.ModuleEventRejected, audit.Entries.Single().Action);
        broadcaster.Verify(
            b => b.BroadcastAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<object>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [TestMethod]
    public async Task BroadcastRealtimeEvent_EventNameDiffersInCase_IsAccepted()
    {
        var broadcaster = new Mock<IRealtimeBroadcaster>();
        var service = CreateService(broadcaster, ["NewMessage"]);

        var response = await service.BroadcastRealtimeEvent(
            Request("newmessage"), AuthenticatedContext());

        Assert.IsTrue(response.Success);
    }

    [TestMethod]
    public async Task BroadcastRealtimeEvent_ManifestDeclaresNothing_KeepsWorking()
    {
        // Transitional: modules whose manifests predate the vocabulary are not broken.
        var broadcaster = new Mock<IRealtimeBroadcaster>();
        var service = CreateService(broadcaster, []);

        var response = await service.BroadcastRealtimeEvent(
            Request("AnyLegacyEvent"), AuthenticatedContext());

        Assert.IsTrue(response.Success);
    }

    [TestMethod]
    public async Task BroadcastRealtimeEvent_UnknownModule_FailsClosed()
    {
        var broadcaster = new Mock<IRealtimeBroadcaster>();
        var service = CreateService(broadcaster, declared: null);

        var ex = await Assert.ThrowsExactlyAsync<RpcException>(
            () => service.BroadcastRealtimeEvent(Request("NewMessage"), AuthenticatedContext()));

        Assert.AreEqual(StatusCode.PermissionDenied, ex.StatusCode);
        Assert.AreEqual("MODULE_EVENT_REJECTED", ex.Trailers.GetValue("error-code"));
    }

    private static BroadcastRealtimeEventRequest Request(string eventName) => new()
    {
        Group = "chat-channel-1",
        EventName = eventName,
        PayloadJson = "{\"channelId\":\"chat-channel-1\"}",
        TargetUserId = string.Empty,
    };

    private static TestServerCallContext AuthenticatedContext()
    {
        var context = new TestServerCallContext();
        context.UserState["ModuleId"] = ModuleId;
        context.UserState["CallerContext"] = DotNetCloud.Core.Authorization.CallerContext.CreateModuleContext(
            Guid.CreateVersion7());
        return context;
    }

    private static CoreCapabilitiesServiceImpl CreateService(
        Mock<IRealtimeBroadcaster> broadcaster,
        IReadOnlyCollection<string>? declared,
        RecordingAuditLogger? audit = null)
    {
        var registry = new Mock<IModuleManifestRegistry>();
        registry
            .Setup(r => r.GetRealtimeEvents(It.IsAny<string>()))
            .Returns(declared);

        var services = new ServiceCollection();
        services.AddSingleton(broadcaster.Object);
        services.AddSingleton<IAuditLogger>(audit ?? new RecordingAuditLogger());
        services.AddSingleton(registry.Object);
        var provider = services.BuildServiceProvider();

        return new CoreCapabilitiesServiceImpl(
            NullLogger<CoreCapabilitiesServiceImpl>.Instance,
            provider,
            new SearchIndexingService(
                Mock.Of<IServiceScopeFactory>(),
                NullLogger<SearchIndexingService>.Instance),
            new ModuleEventInbox(
                Mock.Of<DotNetCloud.Core.Events.IEventBus>(),
                registry.Object,
                NullLogger<ModuleEventInbox>.Instance),
            ConsentTestDoubles.NoConsent());
    }

    private sealed class RecordingAuditLogger : IAuditLogger
    {
        public List<AuditEntry> Entries { get; } = [];

        public Task LogAsync(AuditEntry entry, CancellationToken cancellationToken = default)
        {
            Entries.Add(entry);
            return Task.CompletedTask;
        }
    }
}
