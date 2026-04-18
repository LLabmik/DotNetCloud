using DotNetCloud.Core.DTOs;

namespace DotNetCloud.Modules.Video.Services;

/// <summary>
/// Provides video technical metadata (codec, resolution, etc.).
/// </summary>
public interface IVideoMetadataService
{
    /// <summary>Gets metadata for a video.</summary>
    Task<VideoMetadataDto?> GetMetadataAsync(Guid videoId, CancellationToken cancellationToken = default);
}
