using DotNetCloud.Core.DTOs;
using DotNetCloud.Modules.Video.Models;
using DotNetCloud.Modules.Video.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace DotNetCloud.Modules.Video.Data.Services;

/// <summary>
/// Service for managing video metadata — extraction and retrieval using canonical tables.
/// </summary>
public sealed class VideoMetadataService : IVideoMetadataService
{
    private readonly VideoDbContext _db;
    private readonly ILogger<VideoMetadataService> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="VideoMetadataService"/> class.
    /// </summary>
    public VideoMetadataService(VideoDbContext db, ILogger<VideoMetadataService> logger)
    {
        _db = db;
        _logger = logger;
    }

    /// <summary>
    /// Gets metadata for a video by resolving content hash from UserVideo.
    /// </summary>
    public async Task<VideoMetadataDto?> GetMetadataAsync(Guid videoId, CancellationToken cancellationToken = default)
    {
        var contentHash = await _db.UserVideos
            .Where(uv => uv.Id == videoId)
            .Select(uv => uv.CanonicalContentHash)
            .FirstOrDefaultAsync(cancellationToken);

        if (contentHash is null)
            return null;

        var metadata = await _db.CanonicalVideoMetadata
            .FirstOrDefaultAsync(m => m.VideoContentHash == contentHash, cancellationToken);

        if (metadata is null)
            return null;

        var subtitleCount = await _db.CanonicalSubtitles
            .CountAsync(s => s.VideoContentHash == contentHash, cancellationToken);

        return new VideoMetadataDto
        {
            VideoId = videoId,
            Width = metadata.Width,
            Height = metadata.Height,
            FrameRate = metadata.FrameRate,
            VideoCodec = metadata.VideoCodec,
            AudioCodec = metadata.AudioCodec,
            Bitrate = metadata.Bitrate,
            AudioTrackCount = metadata.AudioTrackCount,
            SubtitleTrackCount = metadata.SubtitleTrackCount + subtitleCount,
            ContainerFormat = metadata.ContainerFormat
        };
    }

    /// <summary>
    /// Saves or updates metadata for a video.
    /// Stores on CanonicalVideoMetadata keyed by content hash.
    /// </summary>
    public async Task SaveMetadataAsync(Guid videoId, VideoMetadata metadata, CancellationToken cancellationToken = default)
    {
        var contentHash = await _db.UserVideos
            .Where(uv => uv.Id == videoId)
            .Select(uv => uv.CanonicalContentHash)
            .FirstOrDefaultAsync(cancellationToken);

        if (contentHash is null)
        {
            _logger.LogWarning("Cannot save metadata for video {VideoId}: no content hash found", videoId);
            return;
        }

        var existing = await _db.CanonicalVideoMetadata
            .FirstOrDefaultAsync(m => m.VideoContentHash == contentHash, cancellationToken);

        if (existing is not null)
        {
            existing.Width = metadata.Width;
            existing.Height = metadata.Height;
            existing.FrameRate = metadata.FrameRate;
            existing.VideoCodec = metadata.VideoCodec;
            existing.AudioCodec = metadata.AudioCodec;
            existing.Bitrate = metadata.Bitrate;
            existing.AudioTrackCount = metadata.AudioTrackCount;
            existing.SubtitleTrackCount = metadata.SubtitleTrackCount;
            existing.ContainerFormat = metadata.ContainerFormat;
            existing.ExtractedAt = DateTime.UtcNow;
        }
        else
        {
            _db.CanonicalVideoMetadata.Add(new CanonicalVideoMetadata
            {
                VideoContentHash = contentHash,
                Width = metadata.Width,
                Height = metadata.Height,
                FrameRate = metadata.FrameRate,
                VideoCodec = metadata.VideoCodec,
                AudioCodec = metadata.AudioCodec,
                Bitrate = metadata.Bitrate,
                AudioTrackCount = metadata.AudioTrackCount,
                SubtitleTrackCount = metadata.SubtitleTrackCount,
                ContainerFormat = metadata.ContainerFormat,
                ExtractedAt = DateTime.UtcNow
            });
        }

        await _db.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Metadata saved for video {VideoId} (contentHash={ContentHash}): {Width}x{Height} {Codec}",
            videoId, contentHash, metadata.Width, metadata.Height, metadata.VideoCodec);
    }
}
