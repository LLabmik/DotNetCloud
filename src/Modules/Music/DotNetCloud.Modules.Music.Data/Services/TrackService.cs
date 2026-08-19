using DotNetCloud.Core.Authorization;
using DotNetCloud.Core.DTOs;
using DotNetCloud.Core.Errors;
using DotNetCloud.Core.Events;
using DotNetCloud.Core.Events.Search;
using DotNetCloud.Modules.Music.Models;
using DotNetCloud.Modules.Music.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using IAuditLogger = DotNetCloud.Core.Capabilities.IAuditLogger;
using AuditEntry = DotNetCloud.Core.Capabilities.AuditEntry;
using AuditAction = DotNetCloud.Core.Capabilities.AuditAction;

namespace DotNetCloud.Modules.Music.Data.Services;

/// <summary>
/// Service for managing tracks — search, starred/favorites, recently added.
/// Uses UserTrack junction + CanonicalTrack (canonical/shared) tables.
/// </summary>
public sealed class TrackService : ITrackService
{
    private readonly MusicDbContext _db;
    private readonly IEventBus _eventBus;
    private readonly IAuditLogger _auditLogger;
    private readonly ILogger<TrackService> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="TrackService"/> class.
    /// </summary>
    public TrackService(MusicDbContext db, IEventBus eventBus, IAuditLogger auditLogger, ILogger<TrackService> logger)
    {
        _db = db;
        _eventBus = eventBus;
        _auditLogger = auditLogger;
        _logger = logger;
    }

    /// <summary>
    /// Gets a track by ID (UserTrack.Id).
    /// </summary>
    public async Task<TrackDto?> GetTrackAsync(Guid trackId, CallerContext caller, CancellationToken cancellationToken = default)
    {
        var userTrack = await BaseTrackQuery()
            .FirstOrDefaultAsync(ut => ut.Id == trackId && ut.OwnerId == caller.UserId, cancellationToken);

        return userTrack is null ? null : MapToDto(userTrack, caller.UserId);
    }

    /// <inheritdoc/>
    public async Task<TrackDto?> GetTrackByFileNodeIdAsync(Guid fileNodeId, CallerContext caller, CancellationToken cancellationToken = default)
    {
        var userTrack = await BaseTrackQuery()
            .FirstOrDefaultAsync(ut => ut.FileNodeId == fileNodeId && ut.OwnerId == caller.UserId, cancellationToken);

        return userTrack is null ? null : MapToDto(userTrack, caller.UserId);
    }

    /// <summary>
    /// Lists all tracks across all users — for search indexing only.
    /// Does not filter by OwnerId; each result includes the owning user's ID.
    /// </summary>
    public async Task<IReadOnlyList<TrackDto>> ListAllTracksAsync(int skip = 0, int take = int.MaxValue, CancellationToken cancellationToken = default)
    {
        var userTracks = await BaseTrackQuery()
            .OrderBy(ut => ut.CanonicalTrack!.Title)
            .Skip(skip)
            .Take(take)
            .ToListAsync(cancellationToken);

        // Use lightweight mapping — do NOT check IsStarred per track (N+1 queries would timeout for large datasets)
        return userTracks.Select(ut => MapToDtoLightweight(ut)).ToList();
    }

    /// <summary>
    /// Lists tracks for the authenticated user.
    /// </summary>
    public async Task<IReadOnlyList<TrackDto>> ListTracksAsync(CallerContext caller, int skip = 0, int take = 50, CancellationToken cancellationToken = default)
    {
        var userTracks = await BaseTrackQuery()
            .Where(ut => ut.OwnerId == caller.UserId)
            .OrderBy(ut => ut.CanonicalTrack!.Title)
            .Skip(skip)
            .Take(take)
            .ToListAsync(cancellationToken);

        return userTracks.Select(ut => MapToDto(ut, caller.UserId)).ToList();
    }

    /// <summary>
    /// Lists tracks by album (CanonicalAlbum.Id).
    /// </summary>
    public async Task<IReadOnlyList<TrackDto>> ListTracksByAlbumAsync(Guid albumId, CallerContext caller, CancellationToken cancellationToken = default)
    {
        var userTracks = await BaseTrackQuery()
            .Where(ut => ut.CanonicalAlbumId == albumId && ut.OwnerId == caller.UserId)
            .OrderBy(ut => ut.CanonicalTrack!.DiscNumber ?? int.MaxValue)
            .ThenBy(ut => ut.CanonicalTrack!.TrackNumber ?? int.MaxValue)
            .ThenBy(ut => ut.CanonicalTrack!.Title)
            .ToListAsync(cancellationToken);

        return userTracks.Select(ut => MapToDto(ut, caller.UserId)).ToList();
    }

    /// <summary>
    /// Searches tracks by title, artist name, or album title (case-insensitive).
    /// </summary>
    public async Task<IReadOnlyList<TrackDto>> SearchAsync(CallerContext caller, string query, int maxResults = 20, CancellationToken cancellationToken = default)
    {
        var queryLower = query.ToLowerInvariant();

        var userTracks = await BaseTrackQuery()
            .Where(ut => ut.OwnerId == caller.UserId && (
                ut.CanonicalTrack!.Title.ToLower().Contains(queryLower)
                || (ut.CanonicalAlbum != null && ut.CanonicalAlbum.Title.ToLower().Contains(queryLower))
                || ut.CanonicalTrack!.TrackArtists.Any(cta => cta.Artist != null && cta.Artist.Name.ToLower().Contains(queryLower))
            ))
            .OrderBy(ut => ut.CanonicalTrack!.Title)
            .Take(maxResults)
            .ToListAsync(cancellationToken);

        return userTracks.Select(ut => MapToDto(ut, caller.UserId)).ToList();
    }

    /// <summary>
    /// Gets recently added tracks.
    /// </summary>
    public async Task<IReadOnlyList<TrackDto>> GetRecentTracksAsync(CallerContext caller, int count = 20, CancellationToken cancellationToken = default)
    {
        var userTracks = await BaseTrackQuery()
            .Where(ut => ut.OwnerId == caller.UserId)
            .OrderByDescending(ut => ut.CreatedAt)
            .Take(count)
            .ToListAsync(cancellationToken);

        return userTracks.Select(ut => MapToDto(ut, caller.UserId)).ToList();
    }

    /// <summary>
    /// Gets random tracks, optionally by genre.
    /// </summary>
    public async Task<IReadOnlyList<TrackDto>> GetRandomTracksAsync(CallerContext caller, int count = 20, string? genre = null, CancellationToken cancellationToken = default)
    {
        var query = BaseTrackQuery()
            .Where(ut => ut.OwnerId == caller.UserId);

        if (!string.IsNullOrWhiteSpace(genre))
        {
            query = query.Where(ut => ut.CanonicalTrack!.TrackGenres.Any(ctg => ctg.Genre!.Name == genre));
        }

        var userTracks = await query
            .OrderBy(_ => Guid.CreateVersion7())
            .Take(count)
            .ToListAsync(cancellationToken);

        return userTracks.Select(ut => MapToDto(ut, caller.UserId)).ToList();
    }

    /// <summary>
    /// Hard-deletes a track (UserTrack) and all related user-owned records.
    /// Canonical data (CanonicalTrack, CanonicalAlbum, CanonicalArtist) is preserved.
    /// </summary>
    public async Task DeleteTrackAsync(Guid trackId, CallerContext caller, CancellationToken cancellationToken = default)
    {
        var userTrack = await _db.UserTracks
            .FirstOrDefaultAsync(ut => ut.Id == trackId && ut.OwnerId == caller.UserId, cancellationToken)
            ?? throw new BusinessRuleException(ErrorCodes.TrackNotFound, "Track not found.");

        // Delete related records first (FK dependencies)
        var playbackHistory = await _db.PlaybackHistories
            .Where(ph => ph.UserTrackId == trackId)
            .ToListAsync(cancellationToken);
        _db.PlaybackHistories.RemoveRange(playbackHistory);

        var scrobbleRecords = await _db.ScrobbleRecords
            .Where(sr => sr.UserTrackId == trackId)
            .ToListAsync(cancellationToken);
        _db.ScrobbleRecords.RemoveRange(scrobbleRecords);

        var starredItems = await _db.StarredItems
            .Where(si => si.ItemId == trackId && si.ItemType == StarredItemType.Track)
            .ToListAsync(cancellationToken);
        _db.StarredItems.RemoveRange(starredItems);

        var playlistTracks = await _db.PlaylistTracks
            .Where(pt => pt.UserTrackId == trackId)
            .ToListAsync(cancellationToken);
        _db.PlaylistTracks.RemoveRange(playlistTracks);

        _db.UserTracks.Remove(userTrack);
        await _db.SaveChangesAsync(cancellationToken);

        await _auditLogger.LogAsync(new AuditEntry
        {
            Caller = caller,
            ModuleId = "dotnetcloud.music",
            Action = AuditAction.Delete,
            EntityType = "Track",
            EntityId = trackId,
            Description = "delete-track",
        }, cancellationToken);

        _logger.LogInformation("Track {TrackId} hard-deleted by user {UserId}", trackId, caller.UserId);

        await _eventBus.PublishAsync(new SearchIndexRequestEvent
        {
            EventId = Guid.CreateVersion7(),
            CreatedAt = DateTime.UtcNow,
            ModuleId = "music",
            EntityId = trackId.ToString(),
            Action = SearchIndexAction.Remove
        }, caller, cancellationToken);
    }

    /// <summary>
    /// Gets starred (favorited) tracks for the current user.
    /// </summary>
    public async Task<IReadOnlyList<TrackDto>> GetStarredTracksAsync(CallerContext caller, CancellationToken cancellationToken = default)
    {
        var starredTrackIds = await _db.StarredItems
            .Where(s => s.UserId == caller.UserId && s.ItemType == StarredItemType.Track)
            .OrderByDescending(s => s.StarredAt)
            .Select(s => s.ItemId)
            .ToListAsync(cancellationToken);

        if (starredTrackIds.Count == 0)
            return [];

        var userTracks = await BaseTrackQuery()
            .Where(ut => starredTrackIds.Contains(ut.Id) && ut.OwnerId == caller.UserId)
            .ToListAsync(cancellationToken);

        var trackMap = userTracks.ToDictionary(ut => ut.Id);
        return starredTrackIds
            .Where(id => trackMap.ContainsKey(id))
            .Select(id => MapToDto(trackMap[id], caller.UserId))
            .ToList();
    }

    /// <summary>
    /// Lightweight mapping for search indexing — skips the per-track IsStarred DB query
    /// to avoid N+1 timeouts with large datasets (21K+ tracks).
    /// </summary>
    private static TrackDto MapToDtoLightweight(UserTrack userTrack)
    {
        var canonicalTrack = userTrack.CanonicalTrack;
        var primaryArtist = canonicalTrack?.TrackArtists
            .FirstOrDefault(cta => cta.IsPrimary)?.Artist
            ?? canonicalTrack?.TrackArtists.FirstOrDefault()?.Artist;

        var primaryGenre = canonicalTrack?.TrackGenres.FirstOrDefault()?.Genre?.Name;

        return new TrackDto
        {
            Id = userTrack.Id,
            OwnerId = userTrack.OwnerId,
            FileNodeId = userTrack.FileNodeId,
            Title = canonicalTrack?.Title ?? "Unknown",
            TrackNumber = canonicalTrack?.TrackNumber,
            DiscNumber = canonicalTrack?.DiscNumber,
            Duration = TimeSpan.FromTicks(canonicalTrack?.DurationTicks ?? 0),
            SizeBytes = 0,
            Bitrate = canonicalTrack?.Bitrate,
            MimeType = canonicalTrack?.MimeType ?? "audio/mpeg",
            AlbumId = userTrack.CanonicalAlbumId,
            AlbumTitle = userTrack.CanonicalAlbum?.Title,
            ArtistId = primaryArtist?.Id ?? Guid.Empty,
            ArtistName = primaryArtist?.Name ?? "Unknown Artist",
            Genre = primaryGenre,
            Year = canonicalTrack?.Year,
            IsStarred = false, // Not computed for indexing
            CreatedAt = userTrack.CreatedAt
        };
    }

    internal TrackDto MapToDto(UserTrack userTrack, Guid userId)
    {
        var canonicalTrack = userTrack.CanonicalTrack;
        var primaryArtist = canonicalTrack?.TrackArtists
            .FirstOrDefault(cta => cta.IsPrimary)?.Artist
            ?? canonicalTrack?.TrackArtists.FirstOrDefault()?.Artist;

        var primaryGenre = canonicalTrack?.TrackGenres.FirstOrDefault()?.Genre?.Name;

        var isStarred = _db.StarredItems.Any(s =>
            s.UserId == userId && s.ItemType == StarredItemType.Track && s.ItemId == userTrack.Id);

        return new TrackDto
        {
            Id = userTrack.Id,
            OwnerId = userTrack.OwnerId,
            FileNodeId = userTrack.FileNodeId,
            Title = canonicalTrack?.Title ?? "Unknown",
            TrackNumber = canonicalTrack?.TrackNumber,
            DiscNumber = canonicalTrack?.DiscNumber,
            Duration = TimeSpan.FromTicks(canonicalTrack?.DurationTicks ?? 0),
            SizeBytes = 0, // Legacy field — size is now on FileNode
            Bitrate = canonicalTrack?.Bitrate,
            MimeType = canonicalTrack?.MimeType ?? "audio/mpeg",
            AlbumId = userTrack.CanonicalAlbumId,
            AlbumTitle = userTrack.CanonicalAlbum?.Title,
            ArtistId = primaryArtist?.Id ?? Guid.Empty,
            ArtistName = primaryArtist?.Name ?? "Unknown Artist",
            Genre = primaryGenre,
            Year = canonicalTrack?.Year,
            IsStarred = isStarred,
            CreatedAt = userTrack.CreatedAt
        };
    }

    /// <summary>
    /// Base query for UserTrack with canonical includes — reusable across all track queries.
    /// </summary>
    private IQueryable<UserTrack> BaseTrackQuery()
    {
        return _db.UserTracks
            .Include(ut => ut.CanonicalTrack)
                .ThenInclude(ct => ct!.TrackArtists).ThenInclude(cta => cta.Artist)
            .Include(ut => ut.CanonicalTrack)
                .ThenInclude(ct => ct!.TrackGenres).ThenInclude(ctg => ctg.Genre)
            .Include(ut => ut.CanonicalAlbum);
    }

    /// <summary>
    /// Extracts a numeric track number from the beginning of a filename.
    /// Returns int.MaxValue if no digits are found, so tracks sort after numbered ones.
    /// </summary>
    private static int ExtractTrackNumberFromFileName(string fileName)
    {
        var name = Path.GetFileNameWithoutExtension(fileName);
        if (string.IsNullOrEmpty(name))
            return int.MaxValue;

        // Collect leading digits (handles "01", "01 - Title", "1. Title", etc.)
        int i = 0;
        while (i < name.Length && !char.IsDigit(name[i]))
            i++;

        if (i >= name.Length)
            return int.MaxValue;

        int start = i;
        while (i < name.Length && char.IsDigit(name[i]))
            i++;

        var span = name.AsSpan(start, i - start);
        return int.TryParse(span, out var num) ? num : int.MaxValue;
    }

    /// <summary>
    /// Compares filenames using natural (numeric-aware) ordering so "02" &lt; "11".
    /// </summary>
    private sealed class NaturalFileNameComparer : IComparer<string>
    {
        public static readonly NaturalFileNameComparer Instance = new();

        public int Compare(string? x, string? y)
        {
            if (x is null && y is null)
                return 0;
            if (x is null)
                return -1;
            if (y is null)
                return 1;

            var nameX = Path.GetFileNameWithoutExtension(x);
            var nameY = Path.GetFileNameWithoutExtension(y);

            int ix = 0, iy = 0;
            while (ix < nameX.Length && iy < nameY.Length)
            {
                if (char.IsDigit(nameX[ix]) && char.IsDigit(nameY[iy]))
                {
                    // Extract and compare full numbers
                    int sx = ix, sy = iy;
                    while (ix < nameX.Length && char.IsDigit(nameX[ix]))
                        ix++;
                    while (iy < nameY.Length && char.IsDigit(nameY[iy]))
                        iy++;

                    var numX = long.Parse(nameX.AsSpan(sx, ix - sx));
                    var numY = long.Parse(nameY.AsSpan(sy, iy - sy));
                    if (numX != numY)
                        return numX.CompareTo(numY);
                }
                else
                {
                    if (nameX[ix] != nameY[iy])
                        return nameX[ix].CompareTo(nameY[iy]);
                    ix++;
                    iy++;
                }
            }

            // Shorter string comes first if all else equal
            return (nameX.Length - ix).CompareTo(nameY.Length - iy);
        }
    }

    /// <inheritdoc />
    public async Task<List<string>> ListTrackAlphabetAsync(CallerContext caller, CancellationToken cancellationToken = default)
    {
        return await _db.UserTracks
            .Where(ut => ut.OwnerId == caller.UserId)
            .Select(ut => ut.CanonicalTrack!.Title.Substring(0, 1).ToUpper())
            .Distinct()
            .OrderBy(c => c)
            .ToListAsync(cancellationToken);
    }
}
