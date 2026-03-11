namespace DotNetCloud.Client.Core.Api;

/// <summary>
/// HTTP API client for chat operations against a DotNetCloud server.
/// </summary>
/// <remarks>
/// Each method accepts an explicit <c>serverBaseUrl</c> and optional
/// <c>accessToken</c> so the SyncTray can call the server on behalf of
/// the signed-in account without needing to hold a shared token store.
/// </remarks>
public interface IChatApiClient
{
    /// <summary>
    /// Sends a chat message to the specified channel.
    /// </summary>
    /// <param name="serverBaseUrl">The base URL of the DotNetCloud server.</param>
    /// <param name="accessToken">Bearer token, or <c>null</c> for no-op implementations.</param>
    /// <param name="channelId">The target channel identifier.</param>
    /// <param name="content">The message text to send.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task SendMessageAsync(
        string serverBaseUrl,
        string? accessToken,
        Guid channelId,
        string content,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Marks a channel as read up to the current moment.
    /// </summary>
    /// <param name="serverBaseUrl">The base URL of the DotNetCloud server.</param>
    /// <param name="accessToken">Bearer token, or <c>null</c> for no-op implementations.</param>
    /// <param name="channelId">The channel to mark as read.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task MarkAsReadAsync(
        string serverBaseUrl,
        string? accessToken,
        Guid channelId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Sends a typing-indicator notification for the given channel.
    /// </summary>
    /// <param name="serverBaseUrl">The base URL of the DotNetCloud server.</param>
    /// <param name="accessToken">Bearer token, or <c>null</c> for no-op implementations.</param>
    /// <param name="channelId">The channel in which the user is typing.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task NotifyTypingAsync(
        string serverBaseUrl,
        string? accessToken,
        Guid channelId,
        CancellationToken cancellationToken = default);
}
