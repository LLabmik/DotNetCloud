namespace DotNetCloud.UI.Web.Client.Services;

/// <summary>Validation finding shown by the default-content admin page.</summary>
public sealed record DefaultContentIssueDto(
    string RelativePath,
    string Code,
    string Message,
    string Severity);

/// <summary>Effective state of the default user content template.</summary>
public sealed record DefaultUserContentStatusDto
{
    public bool Enabled { get; init; }
    public string RootPath { get; init; } = string.Empty;
    public bool RootExists { get; init; }
    public bool Readable { get; init; }
    public int FolderCount { get; init; }
    public int FileCount { get; init; }
    public long TotalBytes { get; init; }
    public int PendingUpdates { get; init; }
    public int PendingDeletions { get; init; }
    public int UsersConverged { get; init; }
    public int UsersFailed { get; init; }
    public DateTime? LastAppliedAtUtc { get; init; }
    public IReadOnlyList<DefaultContentIssueDto> Issues { get; init; } = [];
}

/// <summary>One entry in the template directory browser.</summary>
public sealed record DefaultContentBrowseEntryDto(
    string Name,
    string RelativePath,
    string Kind,
    long Size,
    string ValidationState);

/// <summary>Directory listing of the template.</summary>
public sealed record DefaultContentBrowseResultDto(
    string Path,
    IReadOnlyList<DefaultContentBrowseEntryDto> Entries);

/// <summary>Preview or result of a convergence run.</summary>
public sealed record DefaultContentReconcileResultDto
{
    public bool DryRun { get; init; }
    public bool RequiresConfirmation { get; init; }
    public string? Reason { get; init; }
    public int Adds { get; init; }
    public int Updates { get; init; }
    public int Overwrites { get; init; }
    public int Deletes { get; init; }
    public int UsersQueued { get; init; }
    public int UsersProcessed { get; init; }
    public IReadOnlyList<string> SamplePaths { get; init; } = [];
}

/// <summary>Queue counts by status.</summary>
public sealed record DefaultContentQueueStatusDto(
    int Pending,
    int InProgress,
    int Completed,
    int Partial,
    int Failed,
    int Skipped);
