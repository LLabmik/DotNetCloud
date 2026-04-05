using DotNetCloud.Core.DTOs;
using DotNetCloud.Modules.Video.Models;
using DotNetCloud.Modules.Video.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace DotNetCloud.Modules.Video.Data.Services;

/// <summary>
/// Service for managing video metadata — extraction and retrieval.
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
    /// Gets metadata for a video.
    /// </summary>
    public async Task<VideoMetadataDto?> GetMetadataAsync(Guid videoId, CancellationToken cancellationToken = default)
    {
        var metadata = await _db.VideoMetadata
            .FirstOrDefaultAsync(m => m.VideoId == videoId, cancellationToken);

        if (metadata is null) return null;

        var subtitleCount = await _db.Subtitles
            .CountAsync(s => s.VideoId == videoId, cancellationToken);

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
    /// </summary>
    public async Task SaveMetadataAsync(Guid videoId, VideoMetadata metadata, CancellationToken cancellationToken = default)
    {
        var existing = await _db.VideoMetadata
            .FirstOrDefaultAsync(m => m.VideoId == videoId, cancellationToken);

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
            metadata.VideoId = videoId;
            _db.VideoMetadata.Add(metadata);
        }

        await _db.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Metadata saved for video {VideoId}: {Width}x{Height} {Codec}",
            videoId, metadata.Width, metadata.Height, metadata.VideoCodec);
    }
}
