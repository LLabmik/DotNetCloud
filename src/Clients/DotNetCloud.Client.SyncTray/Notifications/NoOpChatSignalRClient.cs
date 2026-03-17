using DotNetCloud.Client.Core;

namespace DotNetCloud.Client.SyncTray.Notifications;

/// <summary>
/// No-op chat SignalR client used until a concrete desktop chat transport is wired.
/// </summary>
internal sealed class NoOpChatSignalRClient : IChatSignalRClient
{
    /// <inheritdoc/>
    public event EventHandler<ChatUnreadCountUpdatedEventArgs>? OnUnreadCountUpdated;

    /// <inheritdoc/>
    public event EventHandler<ChatMessageReceivedEventArgs>? OnNewChatMessage;

    /// <inheritdoc/>
    public Task ConnectAsync(CancellationToken cancellationToken = default)
    {
        // Intentionally no-op: chat integration is opt-in and may be unavailable.
        _ = OnUnreadCountUpdated;
        _ = OnNewChatMessage;
        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    public Task JoinChannelGroupAsync(Guid channelId, CancellationToken cancellationToken = default)
        => Task.CompletedTask;

    /// <inheritdoc/>
    public Task LeaveChannelGroupAsync(Guid channelId, CancellationToken cancellationToken = default)
        => Task.CompletedTask;
}
