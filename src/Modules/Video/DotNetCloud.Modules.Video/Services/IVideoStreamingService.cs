namespace DotNetCloud.Modules.Video.Services;

/// <summary>
/// Provides video streaming tokens and slot management.
/// </summary>
public interface IVideoStreamingService
{
    /// <summary>Generates a time-limited streaming token for a video.</summary>
    string GenerateStreamToken(Guid videoId, Guid userId);

    /// <summary>Validates a streaming token and returns the decoded token if valid.</summary>
    VideoStreamToken? ValidateStreamToken(string token);

    /// <summary>Acquires a concurrent stream slot for a user.</summary>
    void AcquireStreamSlot(Guid userId);

    /// <summary>Releases a stream slot for a user.</summary>
    void ReleaseStreamSlot(Guid userId);

    /// <summary>Gets the number of active streams for a user.</summary>
    int GetActiveStreamCount(Guid userId);
}
