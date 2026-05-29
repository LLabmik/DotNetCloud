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
/// Uses canonical subtitle model for content deduplication.
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
    /// Uploads a subtitle for a video — creates a canonical subtitle record.
    /// </summary>
    public async Task<SubtitleDto> UploadSubtitleAsync(Guid videoId, UploadSubtitleDto dto, CallerContext caller, CancellationToken cancellationToken = default)
    {
        // Resolve canonical content hash from UserVideo
        var userVideo = await _db.UserVideos
            .FirstOrDefaultAsync(uv => uv.Id == videoId && uv.OwnerId == caller.UserId, cancellationToken)
            ?? throw new BusinessRuleException(ErrorCodes.VideoNotFound, "Video not found.");

        if (!ValidFormats.Contains(dto.Format))
            throw new BusinessRuleException(ErrorCodes.InvalidSubtitleFormat,
                $"Invalid subtitle format '{dto.Format}'. Supported: srt, vtt.");

        var contentHash = userVideo.CanonicalContentHash;

        // If setting as default, unset any existing defaults for this content hash
        if (dto.IsDefault)
        {
            var existingDefaults = await _db.CanonicalSubtitles
                .Where(s => s.VideoContentHash == contentHash && s.IsDefault)
                .ToListAsync(cancellationToken);
            foreach (var existing in existingDefaults)
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

        await _db.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("CanonicalSubtitle {SubtitleId} ({Language}/{Format}) for video {VideoId} (contentHash={ContentHash})",
            canonicalSubtitle.Id, dto.Language, dto.Format, videoId, contentHash);

        return MapCanonicalToDto(canonicalSubtitle);
    }

    /// <summary>
    /// Gets subtitles for a video — queries canonical subtitles by ContentHash.
    /// </summary>
    public async Task<IReadOnlyList<SubtitleDto>> GetSubtitlesAsync(Guid videoId, CallerContext caller, CancellationToken cancellationToken = default)
    {
        var contentHash = await _db.UserVideos
            .Where(uv => uv.Id == videoId)
            .Select(uv => uv.CanonicalContentHash)
            .FirstOrDefaultAsync(cancellationToken);

        if (string.IsNullOrEmpty(contentHash))
            return [];

        var canonicalSubtitles = await _db.CanonicalSubtitles
            .Where(s => s.VideoContentHash == contentHash)
            .OrderBy(s => s.Language)
            .ToListAsync(cancellationToken);

        return canonicalSubtitles.Select(MapCanonicalToDto).ToList();
    }

    /// <summary>
    /// Gets a specific subtitle with content.
    /// </summary>
    public async Task<SubtitleDto?> GetSubtitleAsync(Guid subtitleId, CancellationToken cancellationToken = default)
    {
        var canonical = await _db.CanonicalSubtitles
            .FirstOrDefaultAsync(s => s.Id == subtitleId, cancellationToken);

        return canonical is not null ? MapCanonicalToDto(canonical) : null;
    }

    /// <summary>
    /// Gets the subtitle content for serving to the player.
    /// </summary>
    public async Task<(string Content, string Format)?> GetSubtitleContentAsync(Guid subtitleId, CancellationToken cancellationToken = default)
    {
        var canonical = await _db.CanonicalSubtitles
            .FirstOrDefaultAsync(s => s.Id == subtitleId, cancellationToken);

        return canonical is not null ? (canonical.Content, canonical.Format) : null;
    }

    /// <summary>
    /// Deletes a subtitle.
    /// </summary>
    public async Task DeleteSubtitleAsync(Guid subtitleId, CallerContext caller, CancellationToken cancellationToken = default)
    {
        var canonical = await _db.CanonicalSubtitles
            .FirstOrDefaultAsync(s => s.Id == subtitleId, cancellationToken)
            ?? throw new BusinessRuleException(ErrorCodes.SubtitleNotFound, "Subtitle not found.");

        _db.CanonicalSubtitles.Remove(canonical);
        await _db.SaveChangesAsync(cancellationToken);
        _logger.LogInformation("CanonicalSubtitle {SubtitleId} deleted", subtitleId);
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
