using DotNetCloud.Core.Errors;
using DotNetCloud.Modules.Video.Models;
using DotNetCloud.Modules.Video.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System.Collections.Concurrent;
using System.Security.Cryptography;

namespace DotNetCloud.Modules.Video.Data.Services;

/// <summary>
/// Service for HTTP range-request video streaming, token generation, and concurrent stream limiting.
/// </summary>
public sealed class VideoStreamingService : IVideoStreamingService
{
    private readonly VideoDbContext _db;
    private readonly ILogger<VideoStreamingService> _logger;
    private static readonly ConcurrentDictionary<Guid, int> _activeStreams = new();
    private static readonly ConcurrentDictionary<string, VideoStreamToken> _streamTokens = new();

    /// <summary>Maximum concurrent streams per user (configurable).</summary>
    public int MaxConcurrentStreams { get; set; } = 3;

    /// <summary>Stream token validity duration.</summary>
    public TimeSpan StreamTokenLifetime { get; set; } = TimeSpan.FromHours(2);

    /// <summary>
    /// Initializes a new instance of the <see cref="VideoStreamingService"/> class.
    /// </summary>
    public VideoStreamingService(VideoDbContext db, ILogger<VideoStreamingService> logger)
    {
        _db = db;
        _logger = logger;
    }

    /// <summary>
    /// Gets a video by ID, verifying the user has access.
    /// </summary>
    public async Task<UserVideo?> GetVideoForStreamingAsync(Guid videoId, Guid userId, CancellationToken cancellationToken = default)
    {
        return await _db.UserVideos
            .Include(uv => uv.CanonicalVideo)
            .FirstOrDefaultAsync(uv => uv.Id == videoId && uv.OwnerId == userId, cancellationToken);
    }

    /// <summary>
    /// Generates a time-limited, user-scoped stream token for a video.
    /// </summary>
    public string GenerateStreamToken(Guid videoId, Guid userId)
    {
        var tokenBytes = RandomNumberGenerator.GetBytes(32);
        var token = Convert.ToBase64String(tokenBytes);

        _streamTokens[token] = new VideoStreamToken
        {
            VideoId = videoId,
            UserId = userId,
            ExpiresAt = DateTime.UtcNow + StreamTokenLifetime
        };

        CleanExpiredTokens();
        return token;
    }

    /// <summary>
    /// Validates a stream token and returns the associated video/user info.
    /// </summary>
    public VideoStreamToken? ValidateStreamToken(string token)
    {
        if (!_streamTokens.TryGetValue(token, out var streamToken))
            return null;

        if (streamToken.ExpiresAt < DateTime.UtcNow)
        {
            _streamTokens.TryRemove(token, out _);
            return null;
        }

        return streamToken;
    }

    /// <summary>
    /// Acquires a stream slot for a user. Throws if the limit is exceeded.
    /// </summary>
    public void AcquireStreamSlot(Guid userId)
    {
        var currentCount = _activeStreams.GetOrAdd(userId, 0);
        if (currentCount >= MaxConcurrentStreams)
        {
            throw new BusinessRuleException(
                ErrorCodes.VideoStreamLimitExceeded,
                $"Maximum concurrent video streams ({MaxConcurrentStreams}) exceeded.");
        }

        _activeStreams.AddOrUpdate(userId, 1, (_, count) => count + 1);
        _logger.LogDebug("Video stream slot acquired for user {UserId}, active: {Count}", userId, currentCount + 1);
    }

    /// <summary>
    /// Releases a stream slot for a user.
    /// </summary>
    public void ReleaseStreamSlot(Guid userId)
    {
        _activeStreams.AddOrUpdate(userId, 0, (_, count) => Math.Max(0, count - 1));
        _logger.LogDebug("Video stream slot released for user {UserId}", userId);
    }

    /// <summary>
    /// Gets the number of active streams for a user.
    /// </summary>
    public int GetActiveStreamCount(Guid userId)
    {
        return _activeStreams.GetValueOrDefault(userId, 0);
    }

    /// <summary>
    /// Parses an HTTP Range header value.
    /// </summary>
    /// <param name="rangeHeader">The Range header value (e.g. "bytes=0-1023").</param>
    /// <param name="totalLength">The total file length in bytes.</param>
    /// <returns>The start and end byte positions, or null if invalid.</returns>
    public static (long Start, long End)? ParseRangeHeader(string? rangeHeader, long totalLength)
    {
        if (string.IsNullOrWhiteSpace(rangeHeader))
            return null;

        if (!rangeHeader.StartsWith("bytes=", StringComparison.OrdinalIgnoreCase))
            return null;

        var range = rangeHeader["bytes=".Length..];
        var parts = range.Split('-');

        if (parts.Length != 2)
            return null;

        if (long.TryParse(parts[0], out var start))
        {
            var end = string.IsNullOrEmpty(parts[1]) ? totalLength - 1 : long.Parse(parts[1]);
            end = Math.Min(end, totalLength - 1);

            if (start <= end && start < totalLength)
                return (start, end);
        }
        else if (!string.IsNullOrEmpty(parts[1]) && long.TryParse(parts[1], out var suffixLength))
        {
            var start2 = totalLength - suffixLength;
            if (start2 < 0)
                start2 = 0;
            return (start2, totalLength - 1);
        }

        return null;
    }

    /// <summary>
    /// Gets the content type for a video MIME type, with browser compatibility mapping.
    /// Maps non-standard and deprecated MIME types to their browser-compatible equivalents.
    /// </summary>
    public static string GetContentType(string mimeType)
    {
        return mimeType.ToLowerInvariant() switch
        {
            // ── Standard MP4 family ──
            "video/mp4" => "video/mp4",
            "video/quicktime" => "video/mp4",
            "video/x-m4v" => "video/mp4",

            // ── WebM family ──
            "video/webm" => "video/webm",

            // ── Matroska / MKV ── Chrome and Firefox support direct MKV playback ──
            "video/x-matroska" => "video/x-matroska",
            "video/x-mkv" => "video/x-matroska",

            // ── Ogg family ──
            "video/ogg" => "video/ogg",
            "video/ogv" => "video/ogg",

            // ── Legacy / scanner-produced types ──
            "video/mpeg" => "video/mpeg",
            "video/x-msvideo" => "video/x-msvideo",   // AVI
            "video/x-ms-wmv" => "video/x-ms-wmv",     // WMV
            "video/x-flv" => "video/x-flv",           // Flash video
            "video/3gpp" => "video/3gpp",
            "video/3gpp2" => "video/3gpp2",
            "video/mp2t" => "video/mp2t",             // MPEG-TS (HLS segments)

            // ── HEVC / H.265 ──
            "video/hevc" => "video/mp4",
            "video/h265" => "video/mp4",

            // Preserve any unrecognised video/* or audio/* MIME type so browsers
            // get a valid media Content-Type instead of application/octet-stream,
            // which would be rejected under X-Content-Type-Options: nosniff.
            var m when m.StartsWith("video/", StringComparison.Ordinal) => m,
            var m when m.StartsWith("audio/", StringComparison.Ordinal) => m,

            _ => "application/octet-stream"
        };
    }

    private void CleanExpiredTokens()
    {
        var now = DateTime.UtcNow;
        var expiredKeys = _streamTokens
            .Where(kvp => kvp.Value.ExpiresAt < now)
            .Select(kvp => kvp.Key)
            .ToList();

        foreach (var key in expiredKeys)
        {
            _streamTokens.TryRemove(key, out _);
        }
    }
}
