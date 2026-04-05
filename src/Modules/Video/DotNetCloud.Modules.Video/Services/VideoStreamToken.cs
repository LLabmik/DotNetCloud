namespace DotNetCloud.Modules.Video.Services;

/// <summary>
/// Represents a video stream access token.
/// </summary>
public sealed class VideoStreamToken
{
    /// <summary>The video ID this token grants access to.</summary>
    public required Guid VideoId { get; init; }

    /// <summary>The user ID this token is scoped to.</summary>
    public required Guid UserId { get; init; }

    /// <summary>When this token expires (UTC).</summary>
    public required DateTime ExpiresAt { get; init; }
}
