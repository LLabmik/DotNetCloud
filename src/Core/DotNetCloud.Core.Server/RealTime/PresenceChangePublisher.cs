using DotNetCloud.Core.DTOs;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using ChatMessageNotifier = DotNetCloud.Modules.Chat.Services.IChatMessageNotifier;
using UserPresenceChangedNotification = DotNetCloud.Modules.Chat.Services.UserPresenceChangedNotification;

namespace DotNetCloud.Core.Server.RealTime;

/// <summary>
/// Subscribes to <see cref="PresenceService.PresenceStateChanged"/> (derived transitions from
/// the idle sweep, activity reports, and DND toggles) and fans each change out to CoreHub
/// clients — <c>"UserPresence" { UserId, Status, Timestamp }</c> on <c>Clients.All</c> — and to
/// in-process Blazor subscribers via <see cref="ChatMessageNotifier"/>.
/// </summary>
/// <remarks>
/// <para>
/// Connect/disconnect transitions are NOT raised through <see cref="PresenceService.PresenceStateChanged"/>
/// (they are broadcast by <see cref="CoreHub"/> / <see cref="PresenceCircuitHandler"/> with the
/// correct audience), so this publisher only fires for state changes that have no specific
/// connection context — broadcasting to all connected clients is correct and self-echo is
/// harmless because the UIs filter by the peer user id.
/// </para>
/// <para>
/// Registered as a hosted service so it is instantiated (and subscribes) at startup and stays
/// subscribed for the process lifetime — DND toggles and activity reports can arrive at any time,
/// not just during an idle sweep.
/// </para>
/// </remarks>
internal sealed class PresenceChangePublisher : IHostedService, IDisposable
{
    private readonly PresenceService _presenceService;
    private readonly IHubContext<CoreHub> _hubContext;
    private readonly ChatMessageNotifier? _chatMessageNotifier;
    private readonly ILogger<PresenceChangePublisher> _logger;
    private bool _subscribed;

    public PresenceChangePublisher(
        PresenceService presenceService,
        IHubContext<CoreHub> hubContext,
        ILogger<PresenceChangePublisher> logger,
        ChatMessageNotifier? chatMessageNotifier = null)
    {
        _presenceService = presenceService;
        _hubContext = hubContext;
        _chatMessageNotifier = chatMessageNotifier;
        _logger = logger;
    }

    /// <inheritdoc />
    public Task StartAsync(CancellationToken cancellationToken)
    {
        if (!_subscribed)
        {
            _presenceService.PresenceStateChanged += OnPresenceStateChanged;
            _subscribed = true;
        }

        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task StopAsync(CancellationToken cancellationToken)
    {
        Unsubscribe();
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public void Dispose()
    {
        Unsubscribe();
    }

    private void Unsubscribe()
    {
        if (_subscribed)
        {
            _presenceService.PresenceStateChanged -= OnPresenceStateChanged;
            _subscribed = false;
        }
    }

    private void OnPresenceStateChanged(Guid userId, PresenceState state)
    {
        // Fire-and-forget: a failed broadcast must never break the presence sweep/state engine.
        _ = PublishAsync(userId, state);
    }

    private async Task PublishAsync(Guid userId, PresenceState state)
    {
        try
        {
            await _hubContext.Clients.All.SendAsync(
                "UserPresence",
                new { UserId = userId, Status = state, Timestamp = DateTime.UtcNow });

            _chatMessageNotifier?.NotifyUserPresenceChanged(
                new UserPresenceChangedNotification(userId, state));
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to publish presence state {State} for user {UserId}", state, userId);
        }
    }
}
