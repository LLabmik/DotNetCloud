using System.Text.Json;
using DotNetCloud.Core.Authorization;
using DotNetCloud.Core.Events;
using DotNetCloud.Core.Errors;
using DotNetCloud.Core.Grpc.Capabilities;
using DotNetCloud.Core.Server.Grpc.Services;
using DotNetCloud.Core.Server.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace DotNetCloud.Core.Server.Tests.Services;

/// <summary>
/// Tests for the core-side inbox that receives platform events from module hosts over the
/// <c>PublishEvent</c> capability, and for the caller-identity handling of that rpc.
/// </summary>
/// <remarks>
/// Background jobs used to fabricate a <see cref="CallerType.System"/> caller inside a module process
/// in order to deliver their own notifications. The module now only raises the event; the core
/// publishes it on its own bus with the <b>module</b> as the caller, and its notification pipeline
/// performs delivery. These tests pin the two properties that make that safe: only whitelisted event
/// types are accepted, and the caller identity is always the authenticated module.
/// </remarks>
[TestClass]
public class ModuleEventInboxTests
{
    private const string ModuleId = "dotnetcloud.calendar";

    private static (ModuleEventInbox Inbox, Mock<IEventBus> EventBus) CreateInbox()
    {
        var eventBus = new Mock<IEventBus>();
        var inbox = new ModuleEventInbox(
            eventBus.Object, NullLogger<ModuleEventInbox>.Instance);
        return (inbox, eventBus);
    }

    private static CallerContext ModuleCaller() => new(
        ModuleCallerIdentity.ToUserId(ModuleId), [], CallerType.Module);

    private static ReminderTriggeredEvent CreateReminder() => new()
    {
        EventId = Guid.CreateVersion7(),
        CreatedAt = new DateTime(2026, 10, 25, 8, 0, 0, DateTimeKind.Utc),
        UserId = Guid.CreateVersion7(),
        SourceModuleId = "dotnetcloud.files",
        EntityType = "CalendarEvent",
        EntityId = Guid.CreateVersion7(),
        Title = "Team Sync",
        DueAtUtc = new DateTime(2026, 10, 25, 9, 0, 0, DateTimeKind.Utc)
    };

    private static string Serialize<TEvent>(TEvent @event)
        where TEvent : class, IEvent
        => JsonSerializer.Serialize(@event, typeof(TEvent), PlatformEventContract.JsonOptions);

    [TestMethod]
    public void PublishableEventTypes_ContainsTheNotificationProducingEvents()
    {
        var (inbox, _) = CreateInbox();

        CollectionAssert.Contains(
            inbox.PublishableEventTypes.ToList(), nameof(ReminderTriggeredEvent));
        CollectionAssert.Contains(
            inbox.PublishableEventTypes.ToList(), nameof(ShareExpiringEvent));
    }

    [TestMethod]
    public async Task TryPublishAsync_ReminderTriggered_PublishesAsModuleCaller()
    {
        var (inbox, eventBus) = CreateInbox();
        var reminder = CreateReminder();
        var caller = ModuleCaller();

        var (success, error) = await inbox.TryPublishAsync(
            nameof(ReminderTriggeredEvent), Serialize(reminder), caller, ModuleId);

        Assert.IsTrue(success, error);
        eventBus.Verify(bus => bus.PublishAsync(
            It.Is<ReminderTriggeredEvent>(e => e.EntityId == reminder.EntityId),
            It.Is<CallerContext>(c => c.Type == CallerType.Module && c.UserId == caller.UserId),
            It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [TestMethod]
    public async Task TryPublishAsync_ReminderTriggered_OverridesClaimedSourceModule()
    {
        var (inbox, eventBus) = CreateInbox();
        var reminder = CreateReminder();

        var (success, _) = await inbox.TryPublishAsync(
            nameof(ReminderTriggeredEvent), Serialize(reminder), ModuleCaller(), ModuleId);

        Assert.IsTrue(success);

        // The payload claimed "dotnetcloud.files"; the transport said dotnetcloud.calendar.
        eventBus.Verify(bus => bus.PublishAsync(
            It.Is<ReminderTriggeredEvent>(e => e.SourceModuleId == ModuleId),
            It.IsAny<CallerContext>(),
            It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [TestMethod]
    public async Task TryPublishAsync_ShareExpiring_PublishesOnTheCoreBus()
    {
        var (inbox, eventBus) = CreateInbox();
        var shareExpiring = new ShareExpiringEvent
        {
            EventId = Guid.CreateVersion7(),
            CreatedAt = DateTime.UtcNow,
            FileNodeId = Guid.CreateVersion7(),
            FileName = "expiring.txt",
            ShareId = Guid.CreateVersion7(),
            CreatedByUserId = Guid.CreateVersion7(),
            ExpiresAt = DateTime.UtcNow.AddHours(12)
        };

        var (success, error) = await inbox.TryPublishAsync(
            nameof(ShareExpiringEvent),
            Serialize(shareExpiring),
            new CallerContext(ModuleCallerIdentity.ToUserId("dotnetcloud.files"), [], CallerType.Module),
            "dotnetcloud.files");

        Assert.IsTrue(success, error);
        eventBus.Verify(bus => bus.PublishAsync(
            It.Is<ShareExpiringEvent>(e => e.ShareId == shareExpiring.ShareId),
            It.IsAny<CallerContext>(),
            It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [TestMethod]
    public async Task TryPublishAsync_UnregisteredEventType_IsRejectedAndNotPublished()
    {
        var (inbox, eventBus) = CreateInbox();

        var (success, error) = await inbox.TryPublishAsync(
            nameof(UserCreatedEvent),
            """{"EventId":"00000000-0000-0000-0000-000000000001"}""",
            ModuleCaller(),
            ModuleId);

        Assert.IsFalse(success);
        StringAssert.Contains(error!, "not module-publishable");
        eventBus.Verify(bus => bus.PublishAsync(
            It.IsAny<UserCreatedEvent>(),
            It.IsAny<CallerContext>(),
            It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [TestMethod]
    public async Task TryPublishAsync_UserCaller_IsRejected()
    {
        var (inbox, eventBus) = CreateInbox();

        // A module acting for an end user must not be able to fire platform notifications.
        var userCaller = new CallerContext(Guid.CreateVersion7(), ["user"], CallerType.User);

        var (success, error) = await inbox.TryPublishAsync(
            nameof(ReminderTriggeredEvent), Serialize(CreateReminder()), userCaller, ModuleId);

        Assert.IsFalse(success);
        StringAssert.Contains(error!, "module acting for itself");
        eventBus.Verify(bus => bus.PublishAsync(
            It.IsAny<ReminderTriggeredEvent>(),
            It.IsAny<CallerContext>(),
            It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [TestMethod]
    public async Task TryPublishAsync_MalformedPayload_IsRejected()
    {
        var (inbox, eventBus) = CreateInbox();

        var (success, error) = await inbox.TryPublishAsync(
            nameof(ReminderTriggeredEvent), "{ not json", ModuleCaller(), ModuleId);

        Assert.IsFalse(success);
        StringAssert.Contains(error!, "not a valid event");
        eventBus.Verify(bus => bus.PublishAsync(
            It.IsAny<ReminderTriggeredEvent>(),
            It.IsAny<CallerContext>(),
            It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [TestMethod]
    public async Task TryPublishAsync_PayloadOverTheLimit_IsRejected()
    {
        var (inbox, _) = CreateInbox();
        var oversized = new string('x', ModuleEventInbox.MaxPayloadBytes + 1);

        var (success, error) = await inbox.TryPublishAsync(
            nameof(ShareExpiringEvent), oversized, ModuleCaller(), ModuleId);

        Assert.IsFalse(success);
        StringAssert.Contains(error!, "exceeds");
    }

    [TestMethod]
    public async Task PublishEvent_AuthenticatedModule_PublishesOnTheCoreBus()
    {
        // End-to-end through the RPC handler, with the caller context the interceptor would inject.
        var (inbox, eventBus) = CreateInbox();
        var service = CreateCapabilitiesService(inbox);
        var context = new TestServerCallContext();
        context.UserState["ModuleId"] = ModuleId;
        context.UserState["CallerContext"] = ModuleCaller();

        var reminder = CreateReminder();
        var response = await service.PublishEvent(
            new PublishEventRequest
            {
                EventType = nameof(ReminderTriggeredEvent),
                PayloadJson = Serialize(reminder)
            },
            context);

        Assert.IsTrue(response.Success, response.Error);
        eventBus.Verify(bus => bus.PublishAsync(
            It.Is<ReminderTriggeredEvent>(e => e.SourceModuleId == ModuleId),
            It.Is<CallerContext>(c => c.Type == CallerType.Module),
            It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [TestMethod]
    public async Task PublishEvent_NoCallerContextOnTheTransport_FailsClosed()
    {
        var (inbox, eventBus) = CreateInbox();
        var service = CreateCapabilitiesService(inbox);
        var context = new TestServerCallContext();
        context.UserState["ModuleId"] = ModuleId;

        var response = await service.PublishEvent(
            new PublishEventRequest
            {
                EventType = nameof(ReminderTriggeredEvent),
                PayloadJson = Serialize(CreateReminder())
            },
            context);

        Assert.IsFalse(response.Success);
        eventBus.Verify(bus => bus.PublishAsync(
            It.IsAny<ReminderTriggeredEvent>(),
            It.IsAny<CallerContext>(),
            It.IsAny<CancellationToken>()),
            Times.Never);
    }

    private static CoreCapabilitiesServiceImpl CreateCapabilitiesService(ModuleEventInbox inbox)
    {
        var provider = new ServiceCollection().BuildServiceProvider();

        var indexingService = new SearchIndexingService(
            Mock.Of<IServiceScopeFactory>(),
            NullLogger<SearchIndexingService>.Instance);

        return new CoreCapabilitiesServiceImpl(
            NullLogger<CoreCapabilitiesServiceImpl>.Instance,
            provider,
            indexingService,
            inbox);
    }
}
