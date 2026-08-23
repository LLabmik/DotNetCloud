using DotNetCloud.Core.Authorization;
using DotNetCloud.Modules.Files.DTOs;

namespace DotNetCloud.Modules.Files.Services;

/// <summary>
/// Manages per-user registrations of remote sync folders (the server-side counterpart of
/// the SyncTray client's "folders to sync" feature). A registration maps a user to a single
/// remote folder that one of their devices syncs against.
/// </summary>
public interface ISyncFolderRegistrationService
{
    /// <summary>Lists the active sync folder registrations for the caller.</summary>
    Task<IReadOnlyList<SyncFolderRegistrationDto>> ListAsync(CallerContext caller, CancellationToken cancellationToken = default);

    /// <summary>
    /// Registers a remote folder as a sync target for the caller.
    /// Validates the folder exists, is owned by the caller, is a folder (not a file), and does
    /// not overlap (equal / descendant / ancestor) with an already-registered folder.
    /// </summary>
    Task<SyncFolderRegistrationDto> RegisterAsync(Guid remoteFolderNodeId, CallerContext caller, CancellationToken cancellationToken = default);

    /// <summary>Removes the registration for the given remote folder (soft-delete).</summary>
    Task UnregisterAsync(Guid remoteFolderNodeId, CallerContext caller, CancellationToken cancellationToken = default);
}
