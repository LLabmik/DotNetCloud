using DotNetCloud.Modules.Files.Data.Services.Background;
using DotNetCloud.Modules.Search.Services;

namespace DotNetCloud.Core.Server.Services;

/// <summary>
/// Reindex dispatcher that triggers the in-process Search background service.
/// ⚠️ TODO (Phase 6): Refactor to use gRPC-based search reindexing.
/// The Search module is now process-isolated, so direct DI resolution of
/// SearchReindexBackgroundService will not work at runtime. This class needs
/// to be replaced with a gRPC-based implementation that calls the Search
/// module's reindex RPC endpoint.
/// Currently NOT registered in DI — see Program.cs TODO comment.
/// </summary>
internal sealed class InProcessAdminSharedFolderReindexDispatcher : IAdminSharedFolderReindexDispatcher
{
    private const string FilesModuleId = "files";
    private readonly SearchReindexBackgroundService? _reindexService;

    /// <summary>
    /// Initializes a new instance of the <see cref="InProcessAdminSharedFolderReindexDispatcher"/> class.
    /// </summary>
    public InProcessAdminSharedFolderReindexDispatcher(SearchReindexBackgroundService? reindexService)
    {
        _reindexService = reindexService;
    }

    /// <inheritdoc />
    public Task<bool> RequestFilesReindexAsync(CancellationToken cancellationToken = default)
    {
        if (_reindexService is null)
        {
            return Task.FromResult(false);
        }

        _reindexService.TriggerModuleReindex(FilesModuleId);
        return Task.FromResult(true);
    }
}
