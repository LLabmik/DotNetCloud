using DotNetCloud.Core.Authorization;
using DotNetCloud.Modules.Files.DTOs;

namespace DotNetCloud.Modules.Files.Services;

/// <summary>
/// Manages threaded comments on files and folders.
/// </summary>
public interface ICommentService
{
    /// <summary>Adds a comment to a file or folder.</summary>
    Task<FileCommentDto> AddCommentAsync(Guid fileNodeId, string content, Guid? parentCommentId, CallerContext caller, CancellationToken cancellationToken = default);

    /// <summary>Edits an existing comment.</summary>
    Task<FileCommentDto> EditCommentAsync(Guid commentId, string content, CallerContext caller, CancellationToken cancellationToken = default);

    /// <summary>Soft-deletes a comment.</summary>
    Task DeleteCommentAsync(Guid commentId, CallerContext caller, CancellationToken cancellationToken = default);

    /// <summary>Lists comments on a file or folder.</summary>
    Task<IReadOnlyList<FileCommentDto>> GetCommentsAsync(Guid fileNodeId, CallerContext caller, CancellationToken cancellationToken = default);

    /// <summary>Gets a single comment by ID.</summary>
    Task<FileCommentDto?> GetCommentAsync(Guid commentId, CallerContext caller, CancellationToken cancellationToken = default);

    /// <summary>Returns comment counts for a batch of file/folder node IDs.</summary>
    Task<IReadOnlyDictionary<Guid, int>> GetCommentCountsAsync(IReadOnlyList<Guid> nodeIds, CallerContext caller, CancellationToken cancellationToken = default);
}
