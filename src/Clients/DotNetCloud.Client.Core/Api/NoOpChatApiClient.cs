namespace DotNetCloud.Client.Core.Api;

/// <summary>
/// No-op implementation of <see cref="IChatApiClient"/> used while the live
/// chat transport is not yet connected.
/// </summary>
public sealed class NoOpChatApiClient : IChatApiClient
{
    /// <inheritdoc />
    public Task SendMessageAsync(
        string serverBaseUrl,
        string? accessToken,
        Guid channelId,
        string content,
        CancellationToken cancellationToken = default)
        => Task.CompletedTask;

    /// <inheritdoc />
    public Task MarkAsReadAsync(
        string serverBaseUrl,
        string? accessToken,
        Guid channelId,
        CancellationToken cancellationToken = default)
        => Task.CompletedTask;

    /// <inheritdoc />
    public Task NotifyTypingAsync(
        string serverBaseUrl,
        string? accessToken,
        Guid channelId,
        CancellationToken cancellationToken = default)
        => Task.CompletedTask;
}
