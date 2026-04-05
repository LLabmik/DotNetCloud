namespace DotNetCloud.Modules.Music.Services;

/// <summary>
/// Represents a stream access token.
/// </summary>
public sealed class StreamToken
{
    /// <summary>The track ID this token grants access to.</summary>
    public required Guid TrackId { get; init; }

    /// <summary>The user ID this token is scoped to.</summary>
    public required Guid UserId { get; init; }

    /// <summary>When this token expires (UTC).</summary>
    public required DateTime ExpiresAt { get; init; }
}
