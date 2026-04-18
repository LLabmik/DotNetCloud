using DotNetCloud.Modules.Tracks.Services;
using Microsoft.AspNetCore.Components;

namespace DotNetCloud.Modules.Tracks.UI;

/// <summary>
/// Code-behind for the Chat activity indicator component.
/// Subscribes to <see cref="IChatActivitySignalRService"/> and displays
/// compact notifications of chat activity within the Tracks board view.
/// </summary>
/// <remarks>
/// Gracefully hidden when the Chat module is not installed — the injected
/// <see cref="IChatActivitySignalRService"/> defaults to a null-object stub.
/// </remarks>
public partial class ChatActivityIndicator : ComponentBase, IDisposable
{
    [Inject] private IChatActivitySignalRService ChatActivity { get; set; } = default!;

    private MessageEvent? _latestMessage;
    private ChannelEvent? _channelEvent;
    private bool _isAnimating;

    /// <summary>Whether the Chat module is available and delivering events.</summary>
    protected bool IsChatAvailable => ChatActivity.IsActive;

    /// <inheritdoc />
    protected override void OnInitialized()
    {
        ChatActivity.MessageReceived += OnMessageReceived;
        ChatActivity.ChannelChanged += OnChannelChanged;
    }

    private void OnMessageReceived(Guid channelId, Guid senderUserId, DateTime timestamp)
    {
        _latestMessage = new MessageEvent
        {
            ChannelId = channelId,
            SenderUserId = senderUserId,
            Timestamp = timestamp
        };
        _isAnimating = true;

        RequestRender();
    }

    private void OnChannelChanged(Guid channelId, string action)
    {
        _channelEvent = new ChannelEvent
        {
            ChannelId = channelId,
            Action = action
        };

        RequestRender();
    }

    /// <summary>Request a UI re-render if the component is attached to a renderer.</summary>
    private void RequestRender()
    {
        try { _ = InvokeAsync(StateHasChanged); }
        catch (InvalidOperationException) { /* No render handle — unit test or disposed component */ }
    }

    /// <summary>Dismisses the channel event notification.</summary>
    protected void DismissChannelEvent()
    {
        _channelEvent = null;
    }

    /// <summary>Formats a UTC timestamp as a relative time string.</summary>
    protected static string FormatTime(DateTime utcTime)
    {
        var elapsed = DateTime.UtcNow - utcTime;

        if (elapsed.TotalSeconds < 30) return "just now";
        if (elapsed.TotalMinutes < 1) return $"{(int)elapsed.TotalSeconds}s ago";
        if (elapsed.TotalHours < 1) return $"{(int)elapsed.TotalMinutes}m ago";
        if (elapsed.TotalDays < 1) return $"{(int)elapsed.TotalHours}h ago";
        return $"{(int)elapsed.TotalDays}d ago";
    }

    /// <inheritdoc />
    public void Dispose()
    {
        ChatActivity.MessageReceived -= OnMessageReceived;
        ChatActivity.ChannelChanged -= OnChannelChanged;
    }

    /// <summary>A received chat message event.</summary>
    internal sealed class MessageEvent
    {
        public required Guid ChannelId { get; init; }
        public required Guid SenderUserId { get; init; }
        public required DateTime Timestamp { get; init; }
    }

    /// <summary>A channel lifecycle event.</summary>
    internal sealed class ChannelEvent
    {
        public required Guid ChannelId { get; init; }
        public required string Action { get; init; }
    }
}
