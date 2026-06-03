using DotNetCloud.Modules.Files.Data.Services.Background;
using DotNetCloud.Modules.Search.Services;

namespace DotNetCloud.Core.Server.Services;

/// <summary>
/// Reindex dispatcher that triggers the Search module's reindex endpoint via gRPC.
/// The Search module is process-isolated, so reindex requests are sent over gRPC
/// using <see cref="ISearchApiClient"/>.
/// </summary>
internal sealed class InProcessAdminSharedFolderReindexDispatcher : IAdminSharedFolderReindexDispatcher
{
    private const string FilesModuleId = "files";
    private readonly ISearchApiClient _searchApiClient;

    /// <summary>
    /// Initializes a new instance of the <see cref="InProcessAdminSharedFolderReindexDispatcher"/> class.
    /// </summary>
    public InProcessAdminSharedFolderReindexDispatcher(ISearchApiClient searchApiClient)
    {
        _searchApiClient = searchApiClient;
    }

    /// <inheritdoc />
    public async Task<bool> RequestFilesReindexAsync(CancellationToken cancellationToken = default)
    {
        return await _searchApiClient.ReindexModuleAsync(FilesModuleId, cancellationToken);
    }
}
