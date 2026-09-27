using System.Collections.Concurrent;
using DotNetCloud.Modules.Files.Options;
using DotNetCloud.Modules.Files.Services;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace DotNetCloud.Modules.Files.Data.Services;

/// <summary>
/// In-memory tracker for active Collabora document editing sessions.
/// Enforces <see cref="CollaboraOptions.MaxConcurrentSessions"/> and expires idle sessions.
/// </summary>
internal sealed class WopiSessionTracker : IWopiSessionTracker
{
    /// <summary>A session expires if no WOPI activity is seen within this window.</summary>
    private static readonly TimeSpan DefaultSessionTimeout = TimeSpan.FromHours(9);

    private readonly ICollaboraSettingsProvider _collaboraSettings;

    /// <summary>Effective options: administrator edits layered over configuration.</summary>
    private CollaboraOptions _options => _collaboraSettings.Current;

    /// <summary>
    /// Session timeout is slightly longer than the token lifetime so tokens expire before sessions.
    /// Derived per use so a changed token lifetime applies without restarting the module host.
    /// </summary>
    private TimeSpan SessionTimeout => _options.TokenLifetimeMinutes > 0
        ? TimeSpan.FromMinutes(_options.TokenLifetimeMinutes + 30)
        : DefaultSessionTimeout;

    private readonly ILogger<WopiSessionTracker> _logger;

    // Key: (fileId, userId) → last-activity UTC
    private readonly ConcurrentDictionary<(Guid FileId, Guid UserId), DateTime> _sessions = new();

    /// <summary>
    /// Initializes a new instance of <see cref="WopiSessionTracker"/>.
    /// </summary>
    public WopiSessionTracker(ICollaboraSettingsProvider collaboraSettings, ILogger<WopiSessionTracker> logger)
    {
        _collaboraSettings = collaboraSettings;
        _logger = logger;
    }

    /// <inheritdoc />
    public bool TryBeginSession(Guid fileId, Guid userId)
    {
        var key = (fileId, userId);

        // If this user already has an active session for this file, refresh it
        if (_sessions.ContainsKey(key))
        {
            _sessions[key] = DateTime.UtcNow;
            _logger.LogDebug("WOPI session refreshed for file {FileId}, user {UserId}.", fileId, userId);
            return true;
        }

        PruneExpiredSessions();

        int max = _options.MaxConcurrentSessions;
        if (max > 0 && _sessions.Count >= max)
        {
            _logger.LogWarning(
                "WOPI session denied for file {FileId}, user {UserId}: at capacity ({Count}/{Max}).",
                fileId, userId, _sessions.Count, max);
            return false;
        }

        _sessions.TryAdd(key, DateTime.UtcNow);
        _logger.LogInformation("WOPI session started for file {FileId}, user {UserId}. Active: {Count}.",
            fileId, userId, _sessions.Count);
        return true;
    }

    /// <inheritdoc />
    public void HeartbeatSession(Guid fileId, Guid userId)
    {
        var key = (fileId, userId);
        if (_sessions.ContainsKey(key))
        {
            _sessions[key] = DateTime.UtcNow;
        }
    }

    /// <inheritdoc />
    public void EndSession(Guid fileId, Guid userId)
    {
        var key = (fileId, userId);
        if (_sessions.TryRemove(key, out _))
        {
            _logger.LogInformation("WOPI session ended for file {FileId}, user {UserId}. Active: {Count}.",
                fileId, userId, _sessions.Count);
        }
    }

    /// <inheritdoc />
    public int GetActiveSessionCount()
    {
        PruneExpiredSessions();
        return _sessions.Count;
    }

    private void PruneExpiredSessions()
    {
        var cutoff = DateTime.UtcNow - SessionTimeout;
        foreach (var (key, lastActivity) in _sessions)
        {
            if (lastActivity < cutoff)
            {
                if (_sessions.TryRemove(key, out _))
                {
                    _logger.LogDebug("WOPI session expired for file {FileId}, user {UserId}.",
                        key.FileId, key.UserId);
                }
            }
        }
    }
}
