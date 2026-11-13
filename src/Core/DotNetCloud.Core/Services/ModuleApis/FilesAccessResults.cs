using DotNetCloud.Core.Capabilities;

namespace DotNetCloud.Core.Services.ModuleApis;

/// <summary>
/// Why a Files-module operation produced no content.
/// </summary>
/// <remarks>
/// The distinction matters to callers: <see cref="NotFound"/> means the user does not own that node
/// (or it was deleted), while <see cref="Unavailable"/> means the Files module is not running. A UI
/// should say "that file is gone" in the first case and "file access is temporarily unavailable" in
/// the second, and a caller must not treat either one as an empty document.
/// </remarks>
public enum FilesAccessStatus
{
    /// <summary>The operation succeeded.</summary>
    Success,

    /// <summary>The node does not exist, was deleted, or is not the user's.</summary>
    NotFound,

    /// <summary>The Files module is not installed, not running, or refused the call.</summary>
    Unavailable,

    /// <summary>The Files module reported an error.</summary>
    Failed,
}

/// <summary>
/// Result of listing one folder of a user's files.
/// </summary>
public sealed record FileListResult
{
    /// <summary>Gets the outcome of the call.</summary>
    public required FilesAccessStatus Status { get; init; }

    /// <summary>Gets the folder's children, folders first, as the Files module ordered them.</summary>
    public IReadOnlyList<FileNodeInfo> Nodes { get; init; } = [];

    /// <summary>Gets the total number of children in the folder, before paging.</summary>
    public int TotalCount { get; init; }

    /// <summary>Gets the module's error message, when it reported one.</summary>
    public string? ErrorMessage { get; init; }
}

/// <summary>
/// Result of opening one file's current content.
/// </summary>
public sealed record FileReadResult
{
    /// <summary>Gets the outcome of the call.</summary>
    public required FilesAccessStatus Status { get; init; }

    /// <summary>
    /// Gets the content, positioned at the start, when <see cref="Status"/> is
    /// <see cref="FilesAccessStatus.Success"/>. The caller owns and must dispose it.
    /// </summary>
    public Stream? Content { get; init; }

    /// <summary>Gets the module's error message, when it reported one.</summary>
    public string? ErrorMessage { get; init; }
}

/// <summary>
/// Result of storing a file in a user's Files tree.
/// </summary>
public sealed record FileWriteResult
{
    /// <summary>Gets the outcome of the call.</summary>
    public required FilesAccessStatus Status { get; init; }

    /// <summary>Gets the created (or newly versioned) node.</summary>
    public FileNodeInfo? Node { get; init; }

    /// <summary>Gets the module's error message, when it reported one.</summary>
    public string? ErrorMessage { get; init; }
}
