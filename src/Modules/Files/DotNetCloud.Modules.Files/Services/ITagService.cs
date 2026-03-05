using DotNetCloud.Core.Authorization;
using DotNetCloud.Modules.Files.DTOs;

namespace DotNetCloud.Modules.Files.Services;

/// <summary>
/// Manages file/folder tags.
/// </summary>
public interface ITagService
{
    /// <summary>Adds a tag to a file or folder.</summary>
    Task<FileTagDto> AddTagAsync(Guid fileNodeId, string name, string? color, CallerContext caller, CancellationToken cancellationToken = default);

    /// <summary>Removes a tag from a file or folder.</summary>
    Task RemoveTagAsync(Guid fileNodeId, Guid tagId, CallerContext caller, CancellationToken cancellationToken = default);

    /// <summary>Lists all tags on a file or folder.</summary>
    Task<IReadOnlyList<FileTagDto>> GetTagsAsync(Guid fileNodeId, CallerContext caller, CancellationToken cancellationToken = default);

    /// <summary>Lists all nodes with a specific tag name for a user.</summary>
    Task<IReadOnlyList<FileNodeDto>> GetNodesByTagAsync(string tagName, CallerContext caller, CancellationToken cancellationToken = default);

    /// <summary>Removes a tag from a node by tag name (instead of tag ID).</summary>
    Task RemoveTagByNameAsync(Guid fileNodeId, string tagName, CallerContext caller, CancellationToken cancellationToken = default);

    /// <summary>Lists all distinct tag names used by the caller.</summary>
    Task<IReadOnlyList<string>> GetAllUserTagsAsync(CallerContext caller, CancellationToken cancellationToken = default);

    /// <summary>Returns a summary of all tags used by the caller, including representative color and file count.</summary>
    Task<IReadOnlyList<UserTagSummaryDto>> GetUserTagSummariesAsync(CallerContext caller, CancellationToken cancellationToken = default);

    /// <summary>Adds a tag to multiple files/folders in a single batch.</summary>
    Task<BulkResultDto> BulkAddTagAsync(IReadOnlyList<Guid> nodeIds, string tagName, string? color, CallerContext caller, CancellationToken cancellationToken = default);

    /// <summary>Removes a tag by name from multiple files/folders in a single batch.</summary>
    Task<BulkResultDto> BulkRemoveTagByNameAsync(IReadOnlyList<Guid> nodeIds, string tagName, CallerContext caller, CancellationToken cancellationToken = default);
}

