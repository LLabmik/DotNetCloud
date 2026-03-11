using DotNetCloud.Modules.Chat.Services;
using Microsoft.AspNetCore.Components;

namespace DotNetCloud.Modules.Chat.UI;

/// <summary>
/// Code-behind for the chat notification badge.
/// Subscribes to <see cref="ISignalRChatService.UnreadCountUpdated"/> and accumulates
/// unread counts across all channels, updating the badge in real time.
/// </summary>
public partial class ChatNotificationBadge : ComponentBase, IDisposable
{
    [Inject] private ISignalRChatService SignalR { get; set; } = default!;

    private readonly Dictionary<Guid, int> _unreadByChannel = [];
    private readonly Dictionary<Guid, int> _mentionsByChannel = [];

    /// <summary>Total unread message count across all channels.</summary>
    protected int TotalUnread => _unreadByChannel.Values.Sum();

    /// <summary>Total mention count across all channels.</summary>
    protected int TotalMentions => _mentionsByChannel.Values.Sum();

    /// <summary>Whether any channels have unread mentions.</summary>
    protected bool HasMentions => TotalMentions > 0;

    /// <inheritdoc />
    protected override void OnInitialized()
    {
        SignalR.UnreadCountUpdated += OnUnreadCountUpdated;
        SignalR.MentionCountUpdated += OnMentionCountUpdated;
    }

    private void OnUnreadCountUpdated(Guid channelId, int count)
    {
        ApplyUnreadCountUpdate(channelId, count);
        _ = InvokeAsync(StateHasChanged);
    }

    private void OnMentionCountUpdated(Guid channelId, int count)
    {
        ApplyMentionCountUpdate(channelId, count);
        _ = InvokeAsync(StateHasChanged);
    }

    /// <summary>
    /// Applies a server-pushed unread-count value for a single channel.
    /// The <paramref name="count"/> replaces any previously stored value for that channel.
    /// </summary>
    internal void ApplyUnreadCountUpdate(Guid channelId, int count)
    {
        _unreadByChannel[channelId] = count;
    }

    /// <summary>
    /// Applies a server-pushed mention-count value for a single channel.
    /// The <paramref name="count"/> replaces any previously stored value for that channel.
    /// </summary>
    internal void ApplyMentionCountUpdate(Guid channelId, int count)
    {
        _mentionsByChannel[channelId] = count;
    }

    /// <inheritdoc />
    public void Dispose()
    {
        SignalR.UnreadCountUpdated -= OnUnreadCountUpdated;
        SignalR.MentionCountUpdated -= OnMentionCountUpdated;
    }
}
