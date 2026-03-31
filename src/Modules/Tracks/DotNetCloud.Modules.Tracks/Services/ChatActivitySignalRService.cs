namespace DotNetCloud.Modules.Tracks.Services;

/// <summary>
/// Delivers real-time Chat activity signals to Tracks Blazor UI components.
/// Board views subscribe to events to display chat activity indicators.
/// </summary>
/// <remarks>
/// <para>
/// This service is an <b>optional</b> integration point — when the Chat module
/// is not installed, the <see cref="NullChatActivitySignalRService"/> stub is used
/// and all events are silently ignored. Components degrade gracefully by hiding
/// Chat-related UI elements when <see cref="IsActive"/> is <c>false</c>.
/// </para>
/// </remarks>
public interface IChatActivitySignalRService
{
    /// <summary>Whether the service is actively delivering Chat events.</summary>
    bool IsActive { get; }

    /// <summary>
    /// Raised when a chat message is sent somewhere in the platform.
    /// Args: channelId, senderUserId, timestamp.
    /// </summary>
    event Action<Guid, Guid, DateTime>? MessageReceived;

    /// <summary>
    /// Raised when a chat channel is created or deleted.
    /// Args: channelId, action ("created" or "deleted").
    /// </summary>
    event Action<Guid, string>? ChannelChanged;
}

/// <summary>
/// No-op implementation used when the Chat module is not installed.
/// All event subscriptions are silently ignored.
/// </summary>
internal sealed class NullChatActivitySignalRService : IChatActivitySignalRService
{
    /// <inheritdoc />
    public bool IsActive => false;

    /// <inheritdoc />
#pragma warning disable CS0067 // Event is never used — intentional for null-object stub
    public event Action<Guid, Guid, DateTime>? MessageReceived;
    /// <inheritdoc />
    public event Action<Guid, string>? ChannelChanged;
#pragma warning restore CS0067
}
