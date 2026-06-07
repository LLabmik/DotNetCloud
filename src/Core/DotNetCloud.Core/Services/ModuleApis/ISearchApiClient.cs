namespace DotNetCloud.Core.Services.ModuleApis;

/// <summary>
/// gRPC API client interface for the Search module.
/// </summary>
public interface ISearchApiClient
{
    /// <summary>
    /// Triggers a full reindex of the specified module.
    /// </summary>
    /// <param name="moduleId">The module ID to reindex (e.g., "files", "tracks").</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>True if the reindex was successfully triggered.</returns>
    Task<bool> ReindexModuleAsync(string moduleId, CancellationToken ct = default);
}
