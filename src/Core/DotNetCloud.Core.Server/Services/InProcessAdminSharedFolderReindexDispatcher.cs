using DotNetCloud.Modules.Files.Data.Services.Background;
using DotNetCloud.Modules.Search.Services;

namespace DotNetCloud.Core.Server.Services;

/// <summary>
/// Reindex dispatcher that triggers the in-process Search background service.
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