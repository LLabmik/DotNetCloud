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
    public async Task SaveMetadataAsync(Guid videoId, VideoMetadataDto dto, CancellationToken cancellationToken = default)
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
            existing.Width = dto.Width;
            existing.Height = dto.Height;
            existing.FrameRate = dto.FrameRate;
            existing.VideoCodec = dto.VideoCodec;
            existing.AudioCodec = dto.AudioCodec;
            existing.Bitrate = dto.Bitrate;
            existing.AudioTrackCount = dto.AudioTrackCount;
            existing.SubtitleTrackCount = dto.SubtitleTrackCount;
            existing.ContainerFormat = dto.ContainerFormat;
            existing.ExtractedAt = DateTime.UtcNow;
        }
        else
        {
            _db.CanonicalVideoMetadata.Add(new CanonicalVideoMetadata
            {
                VideoContentHash = contentHash,
                Width = dto.Width,
                Height = dto.Height,
                FrameRate = dto.FrameRate,
                VideoCodec = dto.VideoCodec,
                AudioCodec = dto.AudioCodec,
                Bitrate = dto.Bitrate,
                AudioTrackCount = dto.AudioTrackCount,
                SubtitleTrackCount = dto.SubtitleTrackCount,
                ContainerFormat = dto.ContainerFormat,
                ExtractedAt = DateTime.UtcNow
            });
        }

        await _db.SaveChangesAsync(cancellationToken);

        var safeCodecForLog = SanitizeForLog(dto.VideoCodec);
        _logger.LogInformation("Metadata saved for video {VideoId} (contentHash={ContentHash}): {Width}x{Height} {Codec}",
            videoId, contentHash, dto.Width, dto.Height, safeCodecForLog);
    }

    private static string SanitizeForLog(string? value)
    {
        if (string.IsNullOrEmpty(value))
            return string.Empty;
        return value.Replace("\r", "", StringComparison.Ordinal)
            .Replace("\n", "", StringComparison.Ordinal);
    }
}
