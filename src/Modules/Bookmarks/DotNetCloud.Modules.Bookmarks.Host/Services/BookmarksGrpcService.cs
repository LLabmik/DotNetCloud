using DotNetCloud.Core.Grpc.Lifecycle;
using Grpc.Core;

namespace DotNetCloud.Modules.Bookmarks.Host.Services;

/// <summary>
/// gRPC service for Bookmarks module business operations.
/// </summary>
public sealed class BookmarksGrpcService : BookmarksService.BookmarksServiceBase
{
    private readonly ILogger<BookmarksGrpcService> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="BookmarksGrpcService"/> class.
    /// </summary>
    public BookmarksGrpcService(ILogger<BookmarksGrpcService> logger)
    {
        _logger = logger;
    }
}
