namespace DotNetCloud.Modules.Chat.Services;

/// <summary>
/// Delivers real-time chat notification events from the SignalR hub to Blazor UI components.
/// </summary>
/// <remarks>
/// Register a concrete implementation backed by
/// <c>Microsoft.AspNetCore.SignalR.Client.HubConnection</c> in the application host.
/// The <see cref="NullSignalRChatService"/> stub is registered by default so that
/// components remain functional without a live hub connection.
/// </remarks>
public interface ISignalRChatService
{
    /// <summary>Whether the service is actively delivering events from a hub connection.</summary>
    bool IsActive { get; }

    /// <summary>
    /// Raised when the server pushes an unread-count update for a channel.
    /// </summary>
    /// <remarks>
    /// The first argument is the channel ID; the second is the reported count.
    /// The server sends the current total after a <c>MarkRead</c> operation, or an
    /// increment of 1 for each new mention notification — callers should treat the
    /// value as the authoritative total for that channel and replace any cached value.
    /// </remarks>
    event Action<Guid, int>? UnreadCountUpdated;
}

/// <summary>
/// No-op implementation used when no real-time hub connection is configured.
/// All event subscriptions are silently ignored.
/// </summary>
internal sealed class NullSignalRChatService : ISignalRChatService
{
    /// <inheritdoc />
    public bool IsActive => false;

    /// <inheritdoc />
#pragma warning disable CS0067 // Event is never used — intentional for null-object stub
    public event Action<Guid, int>? UnreadCountUpdated;
#pragma warning restore CS0067
}
