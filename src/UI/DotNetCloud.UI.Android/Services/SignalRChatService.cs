using DotNetCloud.Modules.Chat.DTOs;

namespace DotNetCloud.UI.Android.Services;

/// <summary>
/// Manages the SignalR connection for real-time chat updates.
/// Handles connection lifecycle, reconnection, and event dispatching.
/// </summary>
public interface ISignalRChatService
{
    /// <summary>Whether the SignalR connection is active.</summary>
    bool IsConnected { get; }

    /// <summary>Connects to the SignalR hub.</summary>
    Task ConnectAsync(string accessToken, CancellationToken cancellationToken = default);

    /// <summary>Disconnects from the SignalR hub.</summary>
    Task DisconnectAsync(CancellationToken cancellationToken = default);

    /// <summary>Joins a channel group for real-time updates.</summary>
    Task JoinChannelAsync(Guid channelId, CancellationToken cancellationToken = default);

    /// <summary>Leaves a channel group.</summary>
    Task LeaveChannelAsync(Guid channelId, CancellationToken cancellationToken = default);

    /// <summary>Sends a typing indicator.</summary>
    Task SendTypingAsync(Guid channelId, CancellationToken cancellationToken = default);

    /// <summary>Raised when a new message is received.</summary>
    event Action<MessageDto>? OnMessageReceived;

    /// <summary>Raised when a message is edited.</summary>
    event Action<MessageDto>? OnMessageEdited;

    /// <summary>Raised when a message is deleted.</summary>
    event Action<Guid, Guid>? OnMessageDeleted;

    /// <summary>Raised when a typing indicator is received.</summary>
    event Action<Guid, Guid, string?>? OnTypingReceived;

    /// <summary>Raised when the connection state changes.</summary>
    event Action<bool>? OnConnectionStateChanged;
}

/// <summary>
/// Stub SignalR chat service for initial skeleton.
/// </summary>
internal sealed class SignalRChatService : ISignalRChatService
{
    /// <inheritdoc />
    public bool IsConnected { get; private set; }

    /// <inheritdoc />
    public event Action<MessageDto>? OnMessageReceived;

    /// <inheritdoc />
    public event Action<MessageDto>? OnMessageEdited;

    /// <inheritdoc />
    public event Action<Guid, Guid>? OnMessageDeleted;

    /// <inheritdoc />
    public event Action<Guid, Guid, string?>? OnTypingReceived;

    /// <inheritdoc />
    public event Action<bool>? OnConnectionStateChanged;

    /// <inheritdoc />
    public Task ConnectAsync(string accessToken, CancellationToken cancellationToken = default)
    {
        IsConnected = true;
        OnConnectionStateChanged?.Invoke(true);
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task DisconnectAsync(CancellationToken cancellationToken = default)
    {
        IsConnected = false;
        OnConnectionStateChanged?.Invoke(false);
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task JoinChannelAsync(Guid channelId, CancellationToken cancellationToken = default) => Task.CompletedTask;

    /// <inheritdoc />
    public Task LeaveChannelAsync(Guid channelId, CancellationToken cancellationToken = default) => Task.CompletedTask;

    /// <inheritdoc />
    public Task SendTypingAsync(Guid channelId, CancellationToken cancellationToken = default) => Task.CompletedTask;
}
