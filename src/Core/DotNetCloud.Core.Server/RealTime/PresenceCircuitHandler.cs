using DotNetCloud.Modules.Chat.Services;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Components.Server.Circuits;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;
using System.Security.Claims;

namespace DotNetCloud.Core.Server.RealTime;

/// <summary>
/// Scoped circuit handler that tracks Blazor Server circuit connections for presence.
/// Each Blazor circuit gets its own instance.
/// <para>
/// Presence follows the *connection* state, not just circuit open/close: Blazor Server
/// retains a disconnected circuit for a while (reconnect window), so closing a browser tab
/// only fires <see cref="OnCircuitClosedAsync"/> after that retention expires. To reflect a
/// web user going offline promptly (browser closed / connection lost) we also act on
/// <see cref="OnConnectionDownAsync"/> (offline) and <see cref="OnConnectionUpAsync"/>
/// (online again on reconnect). The user is registered under a stable per-circuit id so the
/// "first"/"last" transitions stay correct across reconnect.
/// </para>
/// </summary>
internal sealed class PresenceCircuitHandler : CircuitHandler
{
    private readonly UserConnectionTracker _connectionTracker;
    private readonly PresenceService _presenceService;
    private readonly AuthenticationStateProvider _authStateProvider;
    private readonly IChatMessageNotifier? _chatMessageNotifier;
    private readonly IHubContext<CoreHub>? _hubContext;
    private readonly ILogger<PresenceCircuitHandler> _logger;

    private string? _connectionId;
    private Guid _userId;

    public PresenceCircuitHandler(
        UserConnectionTracker connectionTracker,
        PresenceService presenceService,
        AuthenticationStateProvider authStateProvider,
        ILogger<PresenceCircuitHandler> logger,
        IHubContext<CoreHub>? hubContext = null,
        IChatMessageNotifier? chatMessageNotifier = null)
    {
        _connectionTracker = connectionTracker;
        _presenceService = presenceService;
        _authStateProvider = authStateProvider;
        _hubContext = hubContext;
        _chatMessageNotifier = chatMessageNotifier;
        _logger = logger;
    }

    /// <inheritdoc />
    public override async Task OnCircuitOpenedAsync(Circuit circuit, CancellationToken cancellationToken)
    {
        try
        {
            var authState = await _authStateProvider.GetAuthenticationStateAsync();
            var user = authState.User;

            var userIdClaim = user.FindFirst(ClaimTypes.NameIdentifier)?.Value
                ?? user.FindFirst("sub")?.Value;

            if (Guid.TryParse(userIdClaim, out var userId))
            {
                _userId = userId;
                _connectionId = $"blazor-{circuit.Id}";

                var isFirstConnection = _connectionTracker.AddConnection(userId, _connectionId);

                _logger.LogInformation(
                    "Blazor circuit opened for user {UserId} (circuit: {CircuitId}, first: {IsFirst})",
                    userId, circuit.Id, isFirstConnection);

                if (isFirstConnection)
                {
                    await MarkOnlineAsync(userId, circuit);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to register Blazor circuit for presence tracking");
        }

        await base.OnCircuitOpenedAsync(circuit, cancellationToken);
    }

    /// <inheritdoc />
    public override async Task OnConnectionUpAsync(Circuit circuit, CancellationToken cancellationToken)
    {
        // Invoked immediately after OnCircuitOpenedAsync on the initial connection AND on every
        // reconnect after a drop. Re-registering is idempotent (same connection id): on the
        // initial up the circuit is already registered by OnCircuitOpenedAsync, so AddConnection
        // returns false and nothing is broadcast; after a drop it returns true (the user was
        // offline) and we broadcast online again.
        if (_connectionId is not null && _userId != Guid.Empty)
        {
            var isFirstConnection = _connectionTracker.AddConnection(_userId, _connectionId);
            if (isFirstConnection)
            {
                _logger.LogInformation(
                    "Blazor connection re-established for user {UserId} (circuit: {CircuitId})",
                    _userId, circuit.Id);
                await MarkOnlineAsync(_userId, circuit);
            }
        }

        await base.OnConnectionUpAsync(circuit, cancellationToken);
    }

    /// <inheritdoc />
    public override async Task OnConnectionDownAsync(Circuit circuit, CancellationToken cancellationToken)
    {
        // The client connection dropped (browser closed, navigated away, or a network blip)
        // while the circuit is retained for reconnect. Reflect this promptly instead of waiting
        // for OnCircuitClosedAsync (which fires only after the reconnect-retention window).
        await HandleConnectionLostAsync(circuit);

        await base.OnConnectionDownAsync(circuit, cancellationToken);
    }

    /// <inheritdoc />
    public override async Task OnCircuitClosedAsync(Circuit circuit, CancellationToken cancellationToken)
    {
        // The circuit is being disposed. If OnConnectionDownAsync already removed the
        // connection, RemoveConnection returns null and this is a no-op (no double offline).
        await HandleConnectionLostAsync(circuit);

        await base.OnCircuitClosedAsync(circuit, cancellationToken);
    }

    /// <summary>
    /// Marks the circuit's user online: records presence, broadcasts to native CoreHub clients
    /// so their DM presence dots update when a web (Blazor circuit) user comes online, and
    /// notifies in-process subscribers (other Blazor circuits).
    /// </summary>
    private async Task MarkOnlineAsync(Guid userId, Circuit circuit)
    {
        await _presenceService.UserConnectedAsync(userId, _connectionId!);

        // Broadcast to native CoreHub clients. Safe on a first connection: the user has no
        // other live connection (CoreHub or circuit) to self-echo to.
        if (_hubContext is not null)
        {
            await _hubContext.Clients.All.SendAsync(
                "UserOnline",
                new { UserId = userId, Timestamp = DateTime.UtcNow });
        }

        _logger.LogDebug("Blazor user {UserId} is now online (circuit: {CircuitId})", userId, circuit.Id);

        // Notify in-process subscribers (other Blazor circuits) so their presence dots
        // and member lists update when this user comes online.
        _chatMessageNotifier?.NotifyUserPresenceChanged(
            new UserPresenceChangedNotification(userId, IsOnline: true));
    }

    /// <summary>
    /// Removes the circuit's connection and, if it was the user's last, marks them offline:
    /// records presence, broadcasts to native CoreHub clients, and notifies in-process
    /// subscribers. Shared by <see cref="OnConnectionDownAsync"/> and <see cref="OnCircuitClosedAsync"/>.
    /// </summary>
    private async Task HandleConnectionLostAsync(Circuit circuit)
    {
        if (_connectionId is null)
        {
            return;
        }

        var result = _connectionTracker.RemoveConnection(_connectionId);

        if (result is null)
        {
            return; // Not tracked (e.g. already removed by a prior connection-down).
        }

        var (userId, isLastConnection) = result.Value;

        if (!isLastConnection)
        {
            return; // Another connection (CoreHub or another circuit) remains — user still online.
        }

        _logger.LogInformation(
            "Blazor circuit connection lost for user {UserId} (circuit: {CircuitId})",
            userId, circuit.Id);

        await _presenceService.UserDisconnectedAsync(userId, _connectionId);

        // Broadcast to native CoreHub clients (mobile/desktop) so their DM presence dots update
        // when a web (Blazor circuit) user goes offline. Safe on last connection: the user has
        // no other live connection left.
        if (_hubContext is not null)
        {
            await _hubContext.Clients.All.SendAsync(
                "UserOffline",
                new { UserId = userId, Timestamp = DateTime.UtcNow });
        }

        // Notify in-process subscribers (other Blazor circuits) so their presence dots
        // and member lists update when this user goes offline.
        _chatMessageNotifier?.NotifyUserPresenceChanged(
            new UserPresenceChangedNotification(userId, IsOnline: false));
    }
}
