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
/// Uses canonical subtitle model with dual-write to old per-user table for backward compatibility.
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
    /// Uploads a subtitle for a video — creates both canonical and legacy subtitle records.
    /// </summary>
    public async Task<SubtitleDto> UploadSubtitleAsync(Guid videoId, UploadSubtitleDto dto, CallerContext caller, CancellationToken cancellationToken = default)
    {
        // Resolve canonical content hash from UserVideo or old Video
        var userVideo = await _db.UserVideos
            .FirstOrDefaultAsync(uv => uv.Id == videoId && uv.OwnerId == caller.UserId, cancellationToken);

        var video = userVideo is not null
            ? null
            : await _db.Videos
                .FirstOrDefaultAsync(v => v.Id == videoId && v.OwnerId == caller.UserId, cancellationToken);

        if (userVideo is null && video is null)
            throw new BusinessRuleException(ErrorCodes.VideoNotFound, "Video not found.");

        if (!ValidFormats.Contains(dto.Format))
            throw new BusinessRuleException(ErrorCodes.InvalidSubtitleFormat,
                $"Invalid subtitle format '{dto.Format}'. Supported: srt, vtt.");

        var contentHash = userVideo?.CanonicalContentHash ?? video!.ContentHash ?? video!.Id.ToString();

        // If setting as default, unset any existing default for this video (both tables)
        if (dto.IsDefault)
        {
            // Canonical defaults
            var existingCanonicalDefaults = await _db.CanonicalSubtitles
                .Where(s => s.VideoContentHash == contentHash && s.IsDefault)
                .ToListAsync(cancellationToken);
            foreach (var existing in existingCanonicalDefaults)
                existing.IsDefault = false;

            // Old table defaults
            var existingOldDefaults = await _db.Subtitles
                .Where(s => s.VideoId == videoId && s.IsDefault)
                .ToListAsync(cancellationToken);
            foreach (var existing in existingOldDefaults)
                existing.IsDefault = false;
        }

        // ── Canonical subtitle (shared by ContentHash) ──
        var canonicalSubtitle = new CanonicalSubtitle
        {
            VideoContentHash = contentHash,
            Language = dto.Language,
            Label = dto.Label,
            Format = dto.Format,
            Content = dto.Content,
            IsDefault = dto.IsDefault
        };
        _db.CanonicalSubtitles.Add(canonicalSubtitle);

        // ── Dual-write: old Subtitle record ──
        var oldSubtitle = new Subtitle
        {
            VideoId = videoId,
            Language = dto.Language,
            Label = dto.Label,
            Format = dto.Format,
            Content = dto.Content,
            IsDefault = dto.IsDefault
        };
        _db.Subtitles.Add(oldSubtitle);

        await _db.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("CanonicalSubtitle {SubtitleId} / old Subtitle {OldSubtitleId} ({Language}/{Format}) for video {VideoId}",
            canonicalSubtitle.Id, oldSubtitle.Id, dto.Language, dto.Format, videoId);

        return MapToDto(oldSubtitle);
    }

    /// <summary>
    /// Gets subtitles for a video — queries canonical subtitles by ContentHash.
    /// </summary>
    public async Task<IReadOnlyList<SubtitleDto>> GetSubtitlesAsync(Guid videoId, CallerContext caller, CancellationToken cancellationToken = default)
    {
        // Resolve content hash
        var contentHash = await _db.UserVideos
            .Where(uv => uv.Id == videoId)
            .Select(uv => uv.CanonicalContentHash)
            .FirstOrDefaultAsync(cancellationToken);

        if (!string.IsNullOrEmpty(contentHash))
        {
            var canonicalSubtitles = await _db.CanonicalSubtitles
                .Where(s => s.VideoContentHash == contentHash)
                .OrderBy(s => s.Language)
                .ToListAsync(cancellationToken);

            if (canonicalSubtitles.Count > 0)
                return canonicalSubtitles.Select(MapCanonicalToDto).ToList();
        }

        // Fallback: old Subtitle table
        var oldSubtitles = await _db.Subtitles
            .Where(s => s.VideoId == videoId)
            .OrderBy(s => s.Language)
            .ToListAsync(cancellationToken);

        return oldSubtitles.Select(MapToDto).ToList();
    }

    /// <summary>
    /// Gets a specific subtitle with content.
    /// </summary>
    public async Task<SubtitleDto?> GetSubtitleAsync(Guid subtitleId, CancellationToken cancellationToken = default)
    {
        // Try canonical first
        var canonical = await _db.CanonicalSubtitles
            .FirstOrDefaultAsync(s => s.Id == subtitleId, cancellationToken);

        if (canonical is not null)
            return MapCanonicalToDto(canonical);

        // Fallback: old table
        var old = await _db.Subtitles
            .FirstOrDefaultAsync(s => s.Id == subtitleId, cancellationToken);

        return old is null ? null : MapToDto(old);
    }

    /// <summary>
    /// Gets the subtitle content for serving to the player.
    /// </summary>
    public async Task<(string Content, string Format)?> GetSubtitleContentAsync(Guid subtitleId, CancellationToken cancellationToken = default)
    {
        // Try canonical first
        var canonical = await _db.CanonicalSubtitles
            .FirstOrDefaultAsync(s => s.Id == subtitleId, cancellationToken);

        if (canonical is not null)
            return (canonical.Content, canonical.Format);

        // Fallback: old table
        var old = await _db.Subtitles
            .FirstOrDefaultAsync(s => s.Id == subtitleId, cancellationToken);

        return old is null ? null : (old.Content, old.Format);
    }

    /// <summary>
    /// Deletes a subtitle.
    /// </summary>
    public async Task DeleteSubtitleAsync(Guid subtitleId, CallerContext caller, CancellationToken cancellationToken = default)
    {
        // Try canonical first (no owner check — shared subtitle)
        var canonical = await _db.CanonicalSubtitles
            .FirstOrDefaultAsync(s => s.Id == subtitleId, cancellationToken);

        if (canonical is not null)
        {
            _db.CanonicalSubtitles.Remove(canonical);

            // Dual-write: remove old subtitle
            var old = await _db.Subtitles
                .FirstOrDefaultAsync(s => s.Id == subtitleId, cancellationToken);
            if (old is not null)
                _db.Subtitles.Remove(old);

            await _db.SaveChangesAsync(cancellationToken);
            _logger.LogInformation("CanonicalSubtitle {SubtitleId} deleted", subtitleId);
            return;
        }

        // Fallback: old table
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

    private static SubtitleDto MapCanonicalToDto(CanonicalSubtitle subtitle)
    {
        return new SubtitleDto
        {
            Id = subtitle.Id,
            VideoId = Guid.Empty, // Canonical subtitles are keyed by ContentHash, not VideoId
            Language = subtitle.Language,
            Label = subtitle.Label,
            Format = subtitle.Format,
            IsDefault = subtitle.IsDefault,
            CreatedAt = subtitle.CreatedAt
        };
    }
}
