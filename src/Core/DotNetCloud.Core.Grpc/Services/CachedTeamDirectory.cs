using System.Collections.Concurrent;
using DotNetCloud.Core.Capabilities;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace DotNetCloud.Core.Grpc.Services;

/// <summary>
/// Decorator over <see cref="ITeamDirectory"/> that caches team and membership
/// lookups for a short duration. Module hosts wrap their gRPC-backed team
/// directory with this so per-access-query team lookups (which otherwise add a
/// Core.Server round trip) hit memory instead.
/// </summary>
/// <remarks>
/// <para>
/// Cached values:
/// <list type="bullet">
/// <item><c>GetTeamsForUserAsync(userId)</c> — keyed by user id.</item>
/// <item><c>GetTeamAsync(teamId)</c> — keyed by team id (including not-found results).</item>
/// </list>
/// </para>
/// <para>
/// Membership changes are infrequent and no cross-process coherence guarantee is
/// needed, so the default 30-second TTL is acceptable. The remaining interface
/// methods are passed through to the inner directory uncached.
/// </para>
/// </remarks>
public sealed class CachedTeamDirectory : ITeamDirectory
{
    private static readonly TimeSpan DefaultCacheDuration = TimeSpan.FromSeconds(30);

    private readonly ITeamDirectory _inner;
    private readonly TimeSpan _cacheDuration;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<CachedTeamDirectory> _logger;

    private readonly ConcurrentDictionary<Guid, CachedValue<IReadOnlyList<TeamInfo>>> _userTeams = new();
    private readonly ConcurrentDictionary<Guid, CachedValue<TeamInfo?>> _teams = new();

    /// <summary>
    /// Initializes a new instance of the <see cref="CachedTeamDirectory"/> class.
    /// </summary>
    /// <param name="inner">The underlying team directory to cache (usually a gRPC client).</param>
    /// <param name="cacheDuration">How long cached lookups remain valid (default 30 seconds).</param>
    /// <param name="logger">Optional logger; defaults to a null logger.</param>
    /// <param name="timeProvider">Optional time provider (for tests); defaults to the system clock.</param>
    public CachedTeamDirectory(
        ITeamDirectory inner,
        TimeSpan? cacheDuration = null,
        ILogger<CachedTeamDirectory>? logger = null,
        TimeProvider? timeProvider = null)
    {
        _inner = inner ?? throw new ArgumentNullException(nameof(inner));
        _cacheDuration = cacheDuration ?? DefaultCacheDuration;
        _logger = logger ?? NullLogger<CachedTeamDirectory>.Instance;
        _timeProvider = timeProvider ?? TimeProvider.System;
    }

    /// <inheritdoc />
    public async Task<TeamInfo?> GetTeamAsync(Guid teamId, CancellationToken cancellationToken = default)
    {
        if (_teams.TryGetValue(teamId, out var cached) && !IsExpired(cached))
        {
            _logger.LogDebug("GetTeam cache hit for {TeamId}", teamId);
            return cached.Value;
        }

        // Cache misses (including not-found) so a repeated lookup of an unknown team
        // doesn't hit Core.Server on every access query.
        var value = await _inner.GetTeamAsync(teamId, cancellationToken);
        _teams[teamId] = new CachedValue<TeamInfo?>(value, GetExpiration());
        return value;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<TeamInfo>> GetTeamsForUserAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        if (_userTeams.TryGetValue(userId, out var cached) && !IsExpired(cached))
        {
            _logger.LogDebug("GetTeamsForUser cache hit for {UserId}", userId);
            return cached.Value;
        }

        var teams = await _inner.GetTeamsForUserAsync(userId, cancellationToken);
        _userTeams[userId] = new CachedValue<IReadOnlyList<TeamInfo>>(teams, GetExpiration());
        return teams;
    }

    /// <inheritdoc />
    public Task<bool> IsTeamMemberAsync(Guid teamId, Guid userId, CancellationToken cancellationToken = default)
        => _inner.IsTeamMemberAsync(teamId, userId, cancellationToken);

    /// <inheritdoc />
    public Task<TeamMemberInfo?> GetTeamMemberAsync(Guid teamId, Guid userId, CancellationToken cancellationToken = default)
        => _inner.GetTeamMemberAsync(teamId, userId, cancellationToken);

    /// <inheritdoc />
    public Task<IReadOnlyList<TeamMemberInfo>> GetTeamMembersAsync(Guid teamId, CancellationToken cancellationToken = default)
        => _inner.GetTeamMembersAsync(teamId, cancellationToken);

    private long GetExpiration() => _timeProvider.GetTimestamp() + DurationToTimestampTicks(_cacheDuration);

    private bool IsExpired<T>(CachedValue<T> cached) => _timeProvider.GetTimestamp() >= cached.ExpiresAt;

    private long DurationToTimestampTicks(TimeSpan duration)
    {
        // TimeProvider.GetTimestamp() advances at TimestampFrequency ticks/second
        // (defaults to Stopwatch.Frequency). Convert the TimeSpan accordingly.
        var seconds = duration.TotalSeconds;
        return checked((long)(seconds * _timeProvider.TimestampFrequency));
    }

    private sealed record CachedValue<T>(T Value, long ExpiresAt);
}
