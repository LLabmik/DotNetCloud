using DotNetCloud.Core.Errors;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System.Collections.Concurrent;
using System.Security.Cryptography;

namespace DotNetCloud.Modules.Video.Data.Services;

/// <summary>
/// Service for HTTP range-request video streaming, token generation, and concurrent stream limiting.
/// </summary>
public sealed class VideoStreamingService
{
    private readonly VideoDbContext _db;
    private readonly ILogger<VideoStreamingService> _logger;
    private readonly ConcurrentDictionary<Guid, int> _activeStreams = new();
    private readonly ConcurrentDictionary<string, VideoStreamToken> _streamTokens = new();

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
    public async Task<Models.Video?> GetVideoForStreamingAsync(Guid videoId, Guid userId, CancellationToken cancellationToken = default)
    {
        return await _db.Videos
            .FirstOrDefaultAsync(v => v.Id == videoId && v.OwnerId == userId, cancellationToken);
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
            if (start2 < 0) start2 = 0;
            return (start2, totalLength - 1);
        }

        return null;
    }

    /// <summary>
    /// Gets the content type for a video MIME type, with browser compatibility mapping.
    /// </summary>
    public static string GetContentType(string mimeType)
    {
        return mimeType.ToLowerInvariant() switch
        {
            "video/mp4" => "video/mp4",
            "video/webm" => "video/webm",
            "video/ogg" => "video/ogg",
            "video/x-matroska" => "video/x-matroska",
            "video/quicktime" => "video/mp4", // Browsers handle QT as MP4
            "video/x-m4v" => "video/mp4",
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

/// <summary>
/// Represents a video stream access token.
/// </summary>
public sealed class VideoStreamToken
{
    /// <summary>The video ID this token grants access to.</summary>
    public required Guid VideoId { get; init; }

    /// <summary>The user ID this token is scoped to.</summary>
    public required Guid UserId { get; init; }

    /// <summary>When this token expires (UTC).</summary>
    public required DateTime ExpiresAt { get; init; }
}
