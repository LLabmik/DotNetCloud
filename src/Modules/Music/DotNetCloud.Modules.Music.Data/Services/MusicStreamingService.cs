using DotNetCloud.Core.Errors;
using DotNetCloud.Modules.Music.Models;
using DotNetCloud.Modules.Music.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System.Collections.Concurrent;
using System.Security.Cryptography;

namespace DotNetCloud.Modules.Music.Data.Services;

/// <summary>
/// Service for serving audio files with HTTP Range support, stream token generation,
/// and concurrent stream limiting.
/// </summary>
public sealed class MusicStreamingService : IMusicStreamingService
{
    private readonly MusicDbContext _db;
    private readonly ILogger<MusicStreamingService> _logger;
    private readonly ConcurrentDictionary<Guid, int> _activeStreams = new();
    private readonly ConcurrentDictionary<string, StreamToken> _streamTokens = new();

    /// <summary>Maximum concurrent streams per user (configurable).</summary>
    public int MaxConcurrentStreams { get; set; } = 3;

    /// <summary>Stream token validity duration.</summary>
    public TimeSpan StreamTokenLifetime { get; set; } = TimeSpan.FromHours(1);

    /// <summary>
    /// Initializes a new instance of the <see cref="MusicStreamingService"/> class.
    /// </summary>
    public MusicStreamingService(MusicDbContext db, ILogger<MusicStreamingService> logger)
    {
        _db = db;
        _logger = logger;
    }

    /// <summary>
    /// Gets a track by ID, verifying the user has access.
    /// </summary>
    public async Task<Track?> GetTrackForStreamingAsync(Guid trackId, Guid userId, CancellationToken cancellationToken = default)
    {
        return await _db.Tracks
            .FirstOrDefaultAsync(t => t.Id == trackId && t.OwnerId == userId, cancellationToken);
    }

    /// <summary>
    /// Generates a time-limited, user-scoped stream token for a track.
    /// </summary>
    public string GenerateStreamToken(Guid trackId, Guid userId)
    {
        var tokenBytes = RandomNumberGenerator.GetBytes(32);
        var token = Convert.ToBase64String(tokenBytes);

        _streamTokens[token] = new StreamToken
        {
            TrackId = trackId,
            UserId = userId,
            ExpiresAt = DateTime.UtcNow + StreamTokenLifetime
        };

        // Clean up expired tokens periodically
        CleanExpiredTokens();

        return token;
    }

    /// <summary>
    /// Validates a stream token and returns the associated track/user info.
    /// </summary>
    public StreamToken? ValidateStreamToken(string token)
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
                ErrorCodes.StreamLimitExceeded,
                $"Maximum concurrent streams ({MaxConcurrentStreams}) exceeded.");
        }

        _activeStreams.AddOrUpdate(userId, 1, (_, count) => count + 1);
        _logger.LogDebug("Stream slot acquired for user {UserId}, active: {Count}", userId, currentCount + 1);
    }

    /// <summary>
    /// Releases a stream slot for a user.
    /// </summary>
    public void ReleaseStreamSlot(Guid userId)
    {
        _activeStreams.AddOrUpdate(userId, 0, (_, count) => Math.Max(0, count - 1));
        _logger.LogDebug("Stream slot released for user {UserId}", userId);
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
            // Suffix range: bytes=-500 means last 500 bytes
            var start2 = totalLength - suffixLength;
            if (start2 < 0) start2 = 0;
            return (start2, totalLength - 1);
        }

        return null;
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
