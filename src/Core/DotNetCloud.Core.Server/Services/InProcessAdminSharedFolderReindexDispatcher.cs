using DotNetCloud.Core.Services;

namespace DotNetCloud.Core.Server.Services;

/// <summary>
/// Reindex dispatcher that triggers the core-owned search reindex service for the files module.
/// Replaces the old gRPC call to the Search module's reindex endpoint.
/// </summary>
internal sealed class InProcessAdminSharedFolderReindexDispatcher : IAdminSharedFolderReindexDispatcher
{
    private const string FilesModuleId = "files";
    private readonly SearchReindexHostedService _reindexService;

    /// <summary>
    /// Initializes a new instance of the <see cref="InProcessAdminSharedFolderReindexDispatcher"/> class.
    /// </summary>
    public InProcessAdminSharedFolderReindexDispatcher(SearchReindexHostedService reindexService)
    {
        _reindexService = reindexService;
    }

    /// <inheritdoc />
    public Task<bool> RequestFilesReindexAsync(CancellationToken cancellationToken = default)
    {
        _reindexService.TriggerModuleReindex(FilesModuleId);
        return Task.FromResult(true);
    }
}
