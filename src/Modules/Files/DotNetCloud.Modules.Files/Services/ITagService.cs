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
}

/// <summary>
/// Response DTO for a file tag.
/// </summary>
public sealed record FileTagDto
{
    /// <summary>Tag ID.</summary>
    public required Guid Id { get; init; }

    /// <summary>Tag name.</summary>
    public required string Name { get; init; }

    /// <summary>Tag color (hex).</summary>
    public string? Color { get; init; }

    /// <summary>When the tag was applied.</summary>
    public DateTime CreatedAt { get; init; }
}
