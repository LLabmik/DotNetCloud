using DotNetCloud.Modules.Files.DTOs;

namespace DotNetCloud.Modules.Files.Services;

/// <summary>
/// Provides storage usage and deduplication metrics for the Files module.
/// </summary>
public interface IStorageMetricsService
{
    /// <summary>
    /// Returns storage metrics including physical vs. logical size and deduplication savings.
    /// </summary>
    Task<StorageMetricsDto> GetDeduplicationMetricsAsync(CancellationToken cancellationToken = default);
}
