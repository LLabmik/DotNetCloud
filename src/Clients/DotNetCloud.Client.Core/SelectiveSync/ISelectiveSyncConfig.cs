namespace DotNetCloud.Client.Core.SelectiveSync;

/// <summary>
/// Manages folder include/exclude configuration for selective sync.
/// </summary>
public interface ISelectiveSyncConfig
{
    /// <summary>
    /// Returns true if the given local path is included in sync for the specified context.
    /// By default (no rules configured) all paths are included.
    /// </summary>
    bool IsIncluded(Guid contextId, string localPath);

    /// <summary>Adds an include rule for a folder path.</summary>
    void Include(Guid contextId, string folderPath);

    /// <summary>Adds an exclude rule for a folder path.</summary>
    void Exclude(Guid contextId, string folderPath);

    /// <summary>Removes all rules for a context.</summary>
    void ClearRules(Guid contextId);

    /// <summary>Gets all include/exclude rules for a context.</summary>
    IReadOnlyList<SelectiveSyncRule> GetRules(Guid contextId);

    /// <summary>Persists rules to the given file path.</summary>
    Task SaveAsync(string filePath, CancellationToken cancellationToken = default);

    /// <summary>Loads rules from the given file path.</summary>
    Task LoadAsync(string filePath, CancellationToken cancellationToken = default);
}

/// <summary>
/// A single include or exclude rule.
/// </summary>
public sealed class SelectiveSyncRule
{
    /// <summary>Folder path this rule applies to.</summary>
    public required string FolderPath { get; init; }

    /// <summary>True = include; false = exclude.</summary>
    public bool IsInclude { get; init; }
}
