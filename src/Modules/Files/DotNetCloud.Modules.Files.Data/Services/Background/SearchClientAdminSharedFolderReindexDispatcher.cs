using DotNetCloud.Modules.Search.Client;

namespace DotNetCloud.Modules.Files.Data.Services.Background;

/// <summary>
/// Reindex dispatcher that forwards Files-module reindex requests to the Search gRPC client.
/// </summary>
internal sealed class SearchClientAdminSharedFolderReindexDispatcher : IAdminSharedFolderReindexDispatcher
{
    private const string FilesModuleId = "files";
    private readonly ISearchFtsClient? _searchFtsClient;

    /// <summary>
    /// Initializes a new instance of the <see cref="SearchClientAdminSharedFolderReindexDispatcher"/> class.
    /// </summary>
    public SearchClientAdminSharedFolderReindexDispatcher(ISearchFtsClient? searchFtsClient)
    {
        _searchFtsClient = searchFtsClient;
    }

    /// <inheritdoc />
    public Task<bool> RequestFilesReindexAsync(CancellationToken cancellationToken = default)
    {
        return _searchFtsClient?.IsAvailable == true
            ? _searchFtsClient.RequestModuleReindexAsync(FilesModuleId, cancellationToken)
            : Task.FromResult(false);
    }
}