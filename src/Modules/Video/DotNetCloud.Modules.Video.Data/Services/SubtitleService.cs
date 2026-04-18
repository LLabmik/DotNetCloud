using DotNetCloud.Core.Authorization;
using DotNetCloud.Core.DTOs;
using DotNetCloud.Core.Errors;
using DotNetCloud.Modules.Video.Models;
using DotNetCloud.Modules.Video.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace DotNetCloud.Modules.Video.Data.Services;

/// <summary>
/// Service for managing subtitles — upload, parse SRT/VTT, associate with videos.
/// </summary>
public sealed class SubtitleService : ISubtitleService
{
    private static readonly HashSet<string> ValidFormats = new(StringComparer.OrdinalIgnoreCase) { "srt", "vtt" };

    private readonly VideoDbContext _db;
    private readonly ILogger<SubtitleService> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="SubtitleService"/> class.
    /// </summary>
    public SubtitleService(VideoDbContext db, ILogger<SubtitleService> logger)
    {
        _db = db;
        _logger = logger;
    }

    /// <summary>
    /// Uploads a subtitle for a video.
    /// </summary>
    public async Task<SubtitleDto> UploadSubtitleAsync(Guid videoId, UploadSubtitleDto dto, CallerContext caller, CancellationToken cancellationToken = default)
    {
        var video = await _db.Videos
            .FirstOrDefaultAsync(v => v.Id == videoId && v.OwnerId == caller.UserId, cancellationToken)
            ?? throw new BusinessRuleException(ErrorCodes.VideoNotFound, "Video not found.");

        if (!ValidFormats.Contains(dto.Format))
            throw new BusinessRuleException(ErrorCodes.InvalidSubtitleFormat,
                $"Invalid subtitle format '{dto.Format}'. Supported: srt, vtt.");

        // If setting as default, unset any existing default for this video
        if (dto.IsDefault)
        {
            var existingDefaults = await _db.Subtitles
                .Where(s => s.VideoId == videoId && s.IsDefault)
                .ToListAsync(cancellationToken);
            foreach (var existing in existingDefaults)
                existing.IsDefault = false;
        }

        var subtitle = new Subtitle
        {
            VideoId = videoId,
            Language = dto.Language,
            Label = dto.Label,
            Format = dto.Format,
            Content = dto.Content,
            IsDefault = dto.IsDefault
        };

        _db.Subtitles.Add(subtitle);
        await _db.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Subtitle {SubtitleId} ({Language}/{Format}) uploaded for video {VideoId}",
            subtitle.Id, subtitle.Language, subtitle.Format, videoId);

        return MapToDto(subtitle);
    }

    /// <summary>
    /// Gets subtitles for a video.
    /// </summary>
    public async Task<IReadOnlyList<SubtitleDto>> GetSubtitlesAsync(Guid videoId, CallerContext caller, CancellationToken cancellationToken = default)
    {
        var subtitles = await _db.Subtitles
            .Where(s => s.VideoId == videoId)
            .OrderBy(s => s.Language)
            .ToListAsync(cancellationToken);

        return subtitles.Select(MapToDto).ToList();
    }

    /// <summary>
    /// Gets a specific subtitle with content.
    /// </summary>
    public async Task<SubtitleDto?> GetSubtitleAsync(Guid subtitleId, CancellationToken cancellationToken = default)
    {
        var subtitle = await _db.Subtitles
            .FirstOrDefaultAsync(s => s.Id == subtitleId, cancellationToken);

        return subtitle is null ? null : MapToDto(subtitle);
    }

    /// <summary>
    /// Gets the subtitle content for serving to the player.
    /// </summary>
    public async Task<(string Content, string Format)?> GetSubtitleContentAsync(Guid subtitleId, CancellationToken cancellationToken = default)
    {
        var subtitle = await _db.Subtitles
            .FirstOrDefaultAsync(s => s.Id == subtitleId, cancellationToken);

        return subtitle is null ? null : (subtitle.Content, subtitle.Format);
    }

    /// <summary>
    /// Deletes a subtitle.
    /// </summary>
    public async Task DeleteSubtitleAsync(Guid subtitleId, CallerContext caller, CancellationToken cancellationToken = default)
    {
        var subtitle = await _db.Subtitles
            .Include(s => s.Video)
            .FirstOrDefaultAsync(s => s.Id == subtitleId, cancellationToken)
            ?? throw new BusinessRuleException(ErrorCodes.SubtitleNotFound, "Subtitle not found.");

        if (subtitle.Video?.OwnerId != caller.UserId)
            throw new BusinessRuleException(ErrorCodes.VideoAccessDenied, "Access denied.");

        _db.Subtitles.Remove(subtitle);
        await _db.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Subtitle {SubtitleId} deleted by user {UserId}", subtitleId, caller.UserId);
    }

    private static SubtitleDto MapToDto(Subtitle subtitle)
    {
        return new SubtitleDto
        {
            Id = subtitle.Id,
            VideoId = subtitle.VideoId,
            Language = subtitle.Language,
            Label = subtitle.Label,
            Format = subtitle.Format,
            IsDefault = subtitle.IsDefault,
            CreatedAt = subtitle.CreatedAt
        };
    }
}
