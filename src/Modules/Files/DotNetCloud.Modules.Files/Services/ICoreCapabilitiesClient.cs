using DotNetCloud.Core.Events;

namespace DotNetCloud.Modules.Files.Services;

/// <summary>
/// Client for calling Core.Server's CoreCapabilities gRPC service.
/// Modules use this to trigger cross-process operations on the core server.
/// </summary>
public interface ICoreCapabilitiesClient
{
    /// <summary>
    /// Indicates whether the core server gRPC endpoint is configured and available.
    /// </summary>
    bool IsAvailable { get; }

    /// <summary>
    /// Triggers cleanup of orphaned media sources and entities after an
    /// admin shared folder is deleted.
    /// </summary>
    /// <returns><see langword="true"/> if the cleanup was successfully triggered.</returns>
    Task<bool> CleanupAdminSharedFolderAsync(
        AdminSharedFolderDeletedEvent evt,
        CancellationToken cancellationToken = default);
}
