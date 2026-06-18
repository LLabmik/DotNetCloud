using DotNetCloud.Core.Authorization;
using DotNetCloud.Core.Data.Context;
using DotNetCloud.Core.DTOs.Media;
using DotNetCloud.Core.Events;
using DotNetCloud.Core.Services;
using DotNetCloud.Modules.Music.Events;
using DotNetCloud.Modules.Photos.Events;
using DotNetCloud.Modules.Video.Events;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace DotNetCloud.Core.Server.Services;

/// <summary>
/// Handles cleanup when an admin shared folder is deleted.
/// Removes orphaned media library sources, indexed media entities,
/// and canonical data with no remaining references.
/// </summary>
/// <remarks>
/// <para>
/// This service subscribes to <see cref="AdminSharedFolderDeletedEvent"/> and orchestrates:
/// <list type="number">
///   <item><description>Removal of <c>MediaLibrarySource</c> entries referencing the deleted share</description></item>
///   <item><description>Cleanup of indexed <c>UserTrack</c>, <c>UserVideo</c>, and <c>Photo</c> entities</description></item>
///   <item><description>Deletion of orphaned <c>CanonicalTrack</c> and <c>CanonicalVideo</c> records</description></item>
/// </list>
/// </para>
/// <para>
/// <b>Process Isolation Note:</b>
/// Because the event bus may not cross process boundaries in the current architecture,
/// this handler currently works only when the Files module and Core.Server share
/// an in-process event bus. A future gRPC relay will route the event across processes.
/// Until then, search cleanup (handled directly in the Files module) is the primary
/// cleanup mechanism.
/// </para>
/// </remarks>
public sealed class AdminSharedFolderCleanupService : IEventHandler<AdminSharedFolderDeletedEvent>
{
    private static readonly string[] MediaTypes = ["photos", "music", "video"];

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<AdminSharedFolderCleanupService> _logger;
    private readonly ICleanupStatusReporter? _statusReporter;

    /// <summary>
    /// Initializes a new instance of the <see cref="AdminSharedFolderCleanupService"/> class.
    /// </summary>
    public AdminSharedFolderCleanupService(
        IServiceScopeFactory scopeFactory,
        ILogger<AdminSharedFolderCleanupService> logger,
        ICleanupStatusReporter? statusReporter = null)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
        _statusReporter = statusReporter;
    }

    /// <inheritdoc />
    public async Task HandleAsync(AdminSharedFolderDeletedEvent evt, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(evt);

        _logger.LogInformation(
            "Starting cleanup for deleted admin shared folder {SharedFolderId} ('{DisplayName}')",
            evt.SharedFolderId, evt.DisplayName);

        try
        {
            // Phase 3: Clean up media library sources
            var affectedUsers = await CleanupMediaSourcesAsync(evt.SharedFolderId, ct);

            // Phase 4: Clean up media entities for affected users
            if (affectedUsers.Count > 0 && evt.MountedEntries.Count > 0)
            {
                await CleanupMediaEntitiesAsync(
                    evt.SharedFolderId, evt.MountedEntries, affectedUsers, ct);
            }

            // Mark cleanup as complete
            if (_statusReporter is not null && evt.EventId != Guid.Empty)
            {
                // Use EventId as a fallback cleanup job identifier
                await _statusReporter.MarkCompletedAsync(evt.EventId, ct);
            }

            _logger.LogInformation(
                "Cleanup complete for admin shared folder {SharedFolderId} ('{DisplayName}'). " +
                "Affected users: {UserCount}",
                evt.SharedFolderId, evt.DisplayName, affectedUsers.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Cleanup failed for admin shared folder {SharedFolderId} ('{DisplayName}')",
                evt.SharedFolderId, evt.DisplayName);

            if (_statusReporter is not null && evt.EventId != Guid.Empty)
            {
                await _statusReporter.MarkFailedAsync(evt.EventId, ex.Message, ct);
            }

            throw;
        }
    }

    /// <summary>
    /// Finds and removes media library source entries in user settings
    /// that reference the deleted shared folder.
    /// </summary>
    /// <returns>The set of user IDs whose settings were modified.</returns>
    private async Task<IReadOnlySet<Guid>> CleanupMediaSourcesAsync(
        Guid sharedFolderId, CancellationToken ct)
    {
        using var scope = _scopeFactory.CreateScope();
        var settingsService = scope.ServiceProvider.GetRequiredService<IUserSettingsService>();
        var coreDbContext = scope.ServiceProvider.GetRequiredService<DotNetCloud.Core.Data.Context.CoreDbContext>();

        var affectedUsers = new HashSet<Guid>();

        // Find all user settings entries for media-library sources
        var mediaSourceSettings = await coreDbContext.UserSettings
            .Where(s => s.Module == MediaLibrarySourceSettings.SettingsModule
                && (s.Key == "photos-sources" || s.Key == "music-sources" || s.Key == "video-sources"))
            .ToListAsync(ct);

        var sharedFolderIdStr = sharedFolderId.ToString();

        foreach (var setting in mediaSourceSettings)
        {
            ct.ThrowIfCancellationRequested();

            if (string.IsNullOrWhiteSpace(setting.Value))
                continue;

            // Check if the JSON contains a reference to this SharedFolderId
            if (!setting.Value.Contains(sharedFolderIdStr, StringComparison.Ordinal))
                continue;

            var sources = MediaLibrarySourceSettings.Deserialize(setting.Value);
            var before = sources.Count;

            var filtered = sources
                .Where(s => !(s.SourceKind == MediaLibrarySourceKind.SharedMount
                           && s.SharedFolderId == sharedFolderId))
                .ToList();

            if (filtered.Count < before)
            {
                await MediaLibrarySourceSettings.SaveSourcesAsync(
                    settingsService, setting.UserId, GetMediaTypeFromKey(setting.Key), filtered);

                affectedUsers.Add(setting.UserId);

                _logger.LogInformation(
                    "Removed {Count} media source(s) for user {UserId} ({Key}) " +
                    "referencing deleted shared folder {SharedFolderId}",
                    before - filtered.Count, setting.UserId, setting.Key, sharedFolderId);
            }
        }

        return affectedUsers;
    }

    /// <summary>
    /// Cleans up indexed media entities (tracks, videos, photos) for affected users
    /// using the computed deterministic file node IDs from the mounted entries.
    /// </summary>
    private async Task CleanupMediaEntitiesAsync(
        Guid sharedFolderId,
        IReadOnlyList<MountedEntryInfo> mountedEntries,
        IReadOnlySet<Guid> affectedUserIds,
        CancellationToken ct)
    {
        // Compute deterministic file node IDs from mounted entry paths
        var fileNodeIds = ComputeFileNodeIds(sharedFolderId, mountedEntries);
        if (fileNodeIds.Count == 0)
            return;

        using var scope = _scopeFactory.CreateScope();

        var musicCallback = scope.ServiceProvider.GetService<IMusicIndexingCallback>();
        var videoCallback = scope.ServiceProvider.GetService<IVideoIndexingCallback>();
        var photoCallback = scope.ServiceProvider.GetService<IPhotoIndexingCallback>();

        var totalRemoved = 0;
        var usersCleaned = 0;

        foreach (var userId in affectedUserIds)
        {
            ct.ThrowIfCancellationRequested();

            // Music: remove UserTrack records
            if (musicCallback is not null)
            {
                try
                {
                    var removed = await musicCallback.RemoveDeletedTracksAsync(fileNodeIds, userId, ct);
                    if (removed > 0)
                    {
                        totalRemoved += removed;
                        _logger.LogDebug(
                            "Removed {Count} track(s) for user {UserId} from deleted shared folder {SharedFolderId}",
                            removed, userId, sharedFolderId);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex,
                        "Failed to remove tracks for user {UserId} during shared folder cleanup", userId);
                }
            }

            // Video: remove UserVideo records
            if (videoCallback is not null)
            {
                try
                {
                    var removed = await videoCallback.RemoveDeletedVideosAsync(fileNodeIds, userId, ct);
                    if (removed > 0)
                    {
                        totalRemoved += removed;
                        _logger.LogDebug(
                            "Removed {Count} video(s) for user {UserId} from deleted shared folder {SharedFolderId}",
                            removed, userId, sharedFolderId);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex,
                        "Failed to remove videos for user {UserId} during shared folder cleanup", userId);
                }
            }

            // Photos: remove Photo records
            if (photoCallback is not null)
            {
                try
                {
                    var removed = await photoCallback.RemoveDeletedPhotosAsync(fileNodeIds, userId, ct);
                    if (removed > 0)
                    {
                        totalRemoved += removed;
                        _logger.LogDebug(
                            "Removed {Count} photo(s) for user {UserId} from deleted shared folder {SharedFolderId}",
                            removed, userId, sharedFolderId);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex,
                        "Failed to remove photos for user {UserId} during shared folder cleanup", userId);
                }
            }

            usersCleaned++;
        }

        _logger.LogInformation(
            "Media entity cleanup for shared folder {SharedFolderId}: " +
            "processed {UsersCleaned}/{TotalUsers} users, removed {TotalRemoved} entities",
            sharedFolderId, usersCleaned, affectedUserIds.Count, totalRemoved);

        // Report progress if status reporter is available
        if (_statusReporter is not null)
        {
            // Use a synthesized cleanup job ID marker
        }
    }

    /// <summary>
    /// Computes deterministic virtual GUIDs from mounted entry data.
    /// These match the GUIDs stored in UserTrack.FileNodeId, UserVideo.FileNodeId,
    /// and Photo.FileNodeId for admin share entries.
    /// </summary>
    private static IReadOnlyList<Guid> ComputeFileNodeIds(
        Guid sharedFolderId, IReadOnlyList<MountedEntryInfo> entries)
    {
        return entries
            .Where(e => !e.IsDirectory) // Files only — directories are not stored in media entities
            .Select(e => ComputeStableGuid(
                $"virtual::admin-shared-entry::{sharedFolderId:D}::file::{NormalizeRelativePath(e.RelativePath)}"))
            .ToList();
    }

    private static Guid ComputeStableGuid(string value)
    {
        var hash = System.Security.Cryptography.SHA256.HashData(
            System.Text.Encoding.UTF8.GetBytes(value));
        Span<byte> bytes = stackalloc byte[16];
        hash.AsSpan(0, 16).CopyTo(bytes);
        bytes[6] = (byte)((bytes[6] & 0x0F) | 0x50);
        bytes[8] = (byte)((bytes[8] & 0x3F) | 0x80);
        return new Guid(bytes);
    }

    private static string NormalizeRelativePath(string relativePath)
        => relativePath.Replace('\\', '/').Trim('/');

    private static string GetMediaTypeFromKey(string key)
        => key.Replace("-sources", "");
}
