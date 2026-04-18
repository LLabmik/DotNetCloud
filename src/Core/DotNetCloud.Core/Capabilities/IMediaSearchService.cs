namespace DotNetCloud.Core.Capabilities;

using DTOs;

/// <summary>
/// Cross-module media search — aggregates search results across Photos, Music, and Video modules.
/// </summary>
public interface IMediaSearchService : ICapabilityInterface
{
    /// <summary>
    /// Searches across all media types for matching items.
    /// </summary>
    Task<MediaSearchResultDto> SearchAsync(Guid userId, string query, int maxResultsPerType = 10, CancellationToken cancellationToken = default);
}
