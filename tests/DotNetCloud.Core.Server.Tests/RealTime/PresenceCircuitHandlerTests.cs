using System.Reflection;
using System.Runtime.CompilerServices;
using System.Security.Claims;
using DotNetCloud.Core.Server.RealTime;
using DotNetCloud.Modules.Chat.Services;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Components.Server.Circuits;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace DotNetCloud.Core.Server.Tests.RealTime;

[TestClass]
public class PresenceCircuitHandlerTests
{
    private static readonly Type CircuitType = typeof(Circuit);
    private static readonly Type CircuitHostType = CircuitType.Assembly
        .GetType("Microsoft.AspNetCore.Components.Server.Circuits.CircuitHost", throwOnError: true)!;

    [TestMethod]
    public async Task OnCircuitOpenedAsync_FirstConnection_BroadcastsUserOnlineToCoreHubClients()
    {
        var userId = Guid.CreateVersion7();
        var (handler, hub, _, notifier) = CreateHandler(userId);

        await handler.OnCircuitOpenedAsync(CreateCircuit(), CancellationToken.None);

        var invocation = hub.StubClients.LastProxy.Invocations.Single(i => i.Method == "UserOnline");
        Assert.AreEqual(userId, ReadUserId(invocation.Args![0]!));
        notifier.Verify(n => n.NotifyUserPresenceChanged(
            It.Is<UserPresenceChangedNotification>(p => p.UserId == userId && p.IsOnline)),
            Times.Once);
    }

    [TestMethod]
    public async Task OnCircuitOpenedAsync_SubsequentConnection_DoesNotBroadcast()
    {
        var userId = Guid.CreateVersion7();
        var (handler, hub, _, notifier) = CreateHandler(userId, preSeedConnectionId: "existing-conn");

        await handler.OnCircuitOpenedAsync(CreateCircuit(), CancellationToken.None);

        Assert.IsFalse(hub.StubClients.LastProxy.Invocations.Any(i => i.Method is "UserOnline" or "UserOffline"));
        notifier.Verify(n => n.NotifyUserPresenceChanged(It.IsAny<UserPresenceChangedNotification>()), Times.Never);
    }

    [TestMethod]
    public async Task OnCircuitClosedAsync_LastConnection_BroadcastsUserOfflineToCoreHubClients()
    {
        var userId = Guid.CreateVersion7();
        var (handler, hub, _, notifier) = CreateHandler(userId);
        var circuit = CreateCircuit();

        // Open registers this circuit as the user's only connection.
        await handler.OnCircuitOpenedAsync(circuit, CancellationToken.None);
        hub.StubClients.LastProxy.Invocations.Clear();

        await handler.OnCircuitClosedAsync(circuit, CancellationToken.None);

        var invocation = hub.StubClients.LastProxy.Invocations.Single(i => i.Method == "UserOffline");
        Assert.AreEqual(userId, ReadUserId(invocation.Args![0]!));
        notifier.Verify(n => n.NotifyUserPresenceChanged(
            It.Is<UserPresenceChangedNotification>(p => p.UserId == userId && !p.IsOnline)),
            Times.Once);
    }

    [TestMethod]
    public async Task OnCircuitClosedAsync_RemainingConnection_DoesNotBroadcast()
    {
        var userId = Guid.CreateVersion7();
        var (handler, hub, _, notifier) = CreateHandler(userId, preSeedConnectionId: "existing-conn");
        var circuit = CreateCircuit();

        // The pre-seeded connection makes this open a *subsequent* connection (no broadcast).
        await handler.OnCircuitOpenedAsync(circuit, CancellationToken.None);
        hub.StubClients.LastProxy.Invocations.Clear();

        // Closing this circuit leaves "existing-conn" — the user stays online, no broadcast.
        await handler.OnCircuitClosedAsync(circuit, CancellationToken.None);

        Assert.IsFalse(hub.StubClients.LastProxy.Invocations.Any(i => i.Method is "UserOnline" or "UserOffline"));
        notifier.Verify(n => n.NotifyUserPresenceChanged(It.IsAny<UserPresenceChangedNotification>()), Times.Never);
    }

    [TestMethod]
    public async Task OnConnectionDownAsync_LastConnection_BroadcastsUserOfflineToCoreHubClients()
    {
        var userId = Guid.CreateVersion7();
        var (handler, hub, _, notifier) = CreateHandler(userId);
        var circuit = CreateCircuit();

        await handler.OnCircuitOpenedAsync(circuit, CancellationToken.None);
        hub.StubClients.LastProxy.Invocations.Clear();

        await handler.OnConnectionDownAsync(circuit, CancellationToken.None);

        var invocation = hub.StubClients.LastProxy.Invocations.Single(i => i.Method == "UserOffline");
        Assert.AreEqual(userId, ReadUserId(invocation.Args![0]!));
        notifier.Verify(n => n.NotifyUserPresenceChanged(
            It.Is<UserPresenceChangedNotification>(p => p.UserId == userId && !p.IsOnline)),
            Times.Once);
    }

    [TestMethod]
    public async Task OnConnectionDownAsync_RemainingConnection_DoesNotBroadcast()
    {
        var userId = Guid.CreateVersion7();
        var (handler, hub, _, notifier) = CreateHandler(userId, preSeedConnectionId: "existing-conn");
        var circuit = CreateCircuit();

        // The pre-seeded connection means this circuit is a *subsequent* connection (no broadcast).
        await handler.OnCircuitOpenedAsync(circuit, CancellationToken.None);
        hub.StubClients.LastProxy.Invocations.Clear();

        // Dropping this connection leaves "existing-conn" — the user stays online.
        await handler.OnConnectionDownAsync(circuit, CancellationToken.None);

        Assert.IsFalse(hub.StubClients.LastProxy.Invocations.Any(i => i.Method is "UserOnline" or "UserOffline"));
        notifier.Verify(n => n.NotifyUserPresenceChanged(It.IsAny<UserPresenceChangedNotification>()), Times.Never);
    }

    [TestMethod]
    public async Task OnConnectionUpAsync_ReconnectAfterDrop_BroadcastsUserOnlineToCoreHubClients()
    {
        var userId = Guid.CreateVersion7();
        var (handler, hub, tracker, _) = CreateHandler(userId);
        var circuit = CreateCircuit();

        // Open (online) → connection drops (offline) → connection re-established (online again).
        await handler.OnCircuitOpenedAsync(circuit, CancellationToken.None);
        await handler.OnConnectionDownAsync(circuit, CancellationToken.None);
        Assert.IsFalse(tracker.IsOnline(userId));
        hub.StubClients.LastProxy.Invocations.Clear();

        await handler.OnConnectionUpAsync(circuit, CancellationToken.None);

        Assert.IsTrue(tracker.IsOnline(userId));
        var invocation = hub.StubClients.LastProxy.Invocations.Single(i => i.Method == "UserOnline");
        Assert.AreEqual(userId, ReadUserId(invocation.Args![0]!));
    }

    [TestMethod]
    public async Task OnConnectionUpAsync_AlreadyRegistered_DoesNotBroadcast()
    {
        var userId = Guid.CreateVersion7();
        var (handler, hub, _, notifier) = CreateHandler(userId);
        var circuit = CreateCircuit();

        // On the initial connect the framework fires OnConnectionUpAsync right after
        // OnCircuitOpenedAsync; the circuit is already registered, so no duplicate online event.
        await handler.OnCircuitOpenedAsync(circuit, CancellationToken.None);
        hub.StubClients.LastProxy.Invocations.Clear();

        await handler.OnConnectionUpAsync(circuit, CancellationToken.None);

        Assert.IsFalse(hub.StubClients.LastProxy.Invocations.Any(i => i.Method is "UserOnline" or "UserOffline"));
        notifier.Verify(n => n.NotifyUserPresenceChanged(
            It.Is<UserPresenceChangedNotification>(p => p.UserId == userId && p.IsOnline)),
            Times.Once); // only the one from OnCircuitOpenedAsync
    }

    private static (
        PresenceCircuitHandler Handler,
        StubHubContext Hub,
        UserConnectionTracker Tracker,
        Mock<IChatMessageNotifier> Notifier)
        CreateHandler(Guid userId, string? preSeedConnectionId = null)
    {
        var tracker = new UserConnectionTracker();
        if (preSeedConnectionId is not null)
            tracker.AddConnection(userId, preSeedConnectionId);

        var presence = new PresenceService(tracker, NullLogger<PresenceService>.Instance);

        var auth = new Mock<AuthenticationStateProvider>();
        auth.Setup(a => a.GetAuthenticationStateAsync())
            .ReturnsAsync(new AuthenticationState(new ClaimsPrincipal(
                new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, userId.ToString())], "Test"))));

        var notifier = new Mock<IChatMessageNotifier>();
        var hub = new StubHubContext();
        var handler = new PresenceCircuitHandler(
            tracker,
            presence,
            auth.Object,
            NullLogger<PresenceCircuitHandler>.Instance,
            hubContext: hub,
            chatMessageNotifier: notifier.Object);

        return (handler, hub, tracker, notifier);
    }

    private static Guid ReadUserId(object payload)
    {
        var property = payload.GetType().GetProperty("UserId")
            ?? throw new InvalidOperationException("Presence broadcast payload is missing the UserId property.");
        return (Guid)property.GetValue(payload)!;
    }

    /// <summary>
    /// Creates a <see cref="Circuit"/> for tests. The public <see cref="Circuit"/> type is sealed
    /// and its constructor is internal (it takes a full <see cref="CircuitHost"/>), so we build a
    /// minimal stand-in via reflection: an uninitialized circuit pointing at an uninitialized host.
    /// The handler only reads <c>Circuit.Id</c>, which yields an empty id — fine because every test
    /// uses its own handler + connection tracker, so ids never need to be unique across tests.
    /// </summary>
    private static Circuit CreateCircuit()
    {
        var circuitHost = RuntimeHelpers.GetUninitializedObject(CircuitHostType);

        var circuit = (Circuit)RuntimeHelpers.GetUninitializedObject(CircuitType);
        var hostField = CircuitType.GetField("_circuitHost", BindingFlags.Instance | BindingFlags.NonPublic)
            ?? throw new InvalidOperationException("Unable to locate the Circuit host backing field.");
        hostField.SetValue(circuit, circuitHost);

        return circuit;
    }
}
