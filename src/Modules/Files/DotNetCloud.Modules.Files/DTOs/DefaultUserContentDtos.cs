namespace DotNetCloud.Modules.Files.DTOs;

/// <summary>Validation finding shown by the admin default-content page.</summary>
/// <param name="RelativePath">Entry path.</param>
/// <param name="Code">Stable issue code.</param>
/// <param name="Message">Description.</param>
/// <param name="Severity"><c>Warning</c> or <c>Error</c>.</param>
public sealed record DefaultContentIssueDto(
    string RelativePath,
    string Code,
    string Message,
    string Severity);

/// <summary>Effective state of the default-content feature.</summary>
public sealed record DefaultUserContentStatusDto
{
    /// <summary>Whether provisioning is enabled.</summary>
    public bool Enabled { get; init; }

    /// <summary>Resolved template root.</summary>
    public required string RootPath { get; init; }

    /// <summary>Whether the template directory exists.</summary>
    public bool RootExists { get; init; }

    /// <summary>Whether the service can read the template.</summary>
    public bool Readable { get; init; }

    /// <summary>Number of folders in the template.</summary>
    public int FolderCount { get; init; }

    /// <summary>Number of files in the template.</summary>
    public int FileCount { get; init; }

    /// <summary>Total template bytes.</summary>
    public long TotalBytes { get; init; }

    /// <summary>Aggregate pending updates across users.</summary>
    public int PendingUpdates { get; init; }

    /// <summary>Aggregate pending deletions across users.</summary>
    public int PendingDeletions { get; init; }

    /// <summary>Users whose last pass completed.</summary>
    public int UsersConverged { get; init; }

    /// <summary>Users whose last pass failed.</summary>
    public int UsersFailed { get; init; }

    /// <summary>When any user was last converged.</summary>
    public DateTime? LastAppliedAtUtc { get; init; }

    /// <summary>Validation findings.</summary>
    public IReadOnlyList<DefaultContentIssueDto> Issues { get; init; } = [];
}

/// <summary>One entry in the template directory browser.</summary>
/// <param name="Name">File or folder name.</param>
/// <param name="RelativePath">Path relative to the template root.</param>
/// <param name="Kind">Folder or File.</param>
/// <param name="Size">Size in bytes.</param>
/// <param name="ValidationState">Validation state summary.</param>
public sealed record DefaultContentBrowseEntryDto(
    string Name,
    string RelativePath,
    string Kind,
    long Size,
    string ValidationState);

/// <summary>Directory listing of the template.</summary>
/// <param name="Path">Relative directory path.</param>
/// <param name="Entries">Child entries.</param>
public sealed record DefaultContentBrowseResultDto(
    string Path,
    IReadOnlyList<DefaultContentBrowseEntryDto> Entries);

/// <summary>What a convergence run would do (or did).</summary>
public sealed record DefaultContentReconcileResultDto
{
    /// <summary>True when this was a preview.</summary>
    public bool DryRun { get; init; }

    /// <summary>True when a destructive action needs an explicit acknowledgement.</summary>
    public bool RequiresConfirmation { get; init; }

    /// <summary>Reason confirmation is required.</summary>
    public string? Reason { get; init; }

    /// <summary>Items that would be / were added.</summary>
    public int Adds { get; init; }

    /// <summary>Unmodified copies that would be / were replaced.</summary>
    public int Updates { get; init; }

    /// <summary>User-modified copies that would be / were replaced.</summary>
    public int Overwrites { get; init; }

    /// <summary>Copies that would be / were deleted.</summary>
    public int Deletes { get; init; }

    /// <summary>Users queued for an additive pass.</summary>
    public int UsersQueued { get; init; }

    /// <summary>Users processed synchronously.</summary>
    public int UsersProcessed { get; init; }

    /// <summary>Sample affected paths (maximum 20).</summary>
    public IReadOnlyList<string> SamplePaths { get; init; } = [];
}

/// <summary>Counts of users by provisioning status.</summary>
public sealed record DefaultContentQueueStatusDto(
    int Pending,
    int InProgress,
    int Completed,
    int Partial,
    int Failed,
    int Skipped);

/// <summary>A user whose convergence failed repeatedly.</summary>
/// <param name="UserId">User id.</param>
/// <param name="Attempts">Retry count.</param>
/// <param name="LastError">Last failure reason.</param>
/// <param name="UpdatedAtUtc">Last update.</param>
public sealed record DefaultContentFailedUserDto(Guid UserId, int Attempts, string? LastError, DateTime UpdatedAtUtc);
