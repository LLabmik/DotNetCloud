using DotNetCloud.Core.Models;

namespace DotNetCloud.Core.Services;

/// <summary>
/// Reports cleanup progress for deleted admin shared folders.
/// Implemented by the Files module to update persistent cleanup status records.
/// </summary>
public interface ICleanupStatusReporter
{
    /// <summary>
    /// Updates the phase of a cleanup job.
    /// </summary>
    Task UpdatePhaseAsync(Guid cleanupJobId, CleanupPhase phase, CancellationToken cancellationToken = default);

    /// <summary>
    /// Updates media cleanup progress for a cleanup job.
    /// </summary>
    Task UpdateMediaProgressAsync(
        Guid cleanupJobId,
        int affectedUsers,
        int usersCleaned,
        int mediaEntitiesRemoved,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Marks a cleanup job as completed.
    /// </summary>
    Task MarkCompletedAsync(Guid cleanupJobId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Marks a cleanup job as failed with an error message.
    /// </summary>
    Task MarkFailedAsync(Guid cleanupJobId, string errorMessage, CancellationToken cancellationToken = default);
}
