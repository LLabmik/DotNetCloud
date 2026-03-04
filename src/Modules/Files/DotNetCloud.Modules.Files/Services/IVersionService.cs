using DotNetCloud.Core.Authorization;
using DotNetCloud.Modules.Files.DTOs;

namespace DotNetCloud.Modules.Files.Services;

/// <summary>
/// Manages file version history.
/// </summary>
public interface IVersionService
{
    /// <summary>Lists all versions of a file.</summary>
    Task<IReadOnlyList<FileVersionDto>> ListVersionsAsync(Guid fileNodeId, CallerContext caller, CancellationToken cancellationToken = default);

    /// <summary>Gets a specific version of a file.</summary>
    Task<FileVersionDto?> GetVersionAsync(Guid versionId, CallerContext caller, CancellationToken cancellationToken = default);

    /// <summary>Restores a file to a previous version. Creates a new version pointing to the old content.</summary>
    Task<FileVersionDto> RestoreVersionAsync(Guid fileNodeId, Guid versionId, CallerContext caller, CancellationToken cancellationToken = default);

    /// <summary>Labels a version with a descriptive name.</summary>
    Task<FileVersionDto> LabelVersionAsync(Guid versionId, string label, CallerContext caller, CancellationToken cancellationToken = default);

    /// <summary>Deletes a specific version (decrements chunk refcounts).</summary>
    Task DeleteVersionAsync(Guid versionId, CallerContext caller, CancellationToken cancellationToken = default);
}
