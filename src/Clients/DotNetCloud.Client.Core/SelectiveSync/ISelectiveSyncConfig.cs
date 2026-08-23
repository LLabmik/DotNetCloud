using DotNetCloud.Client.Core.LocalState;

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

    /// <summary>
    /// Adds or replaces a rule with an explicit <paramref name="source"/> (e.g. <c>"SizeLimit"</c>).
    /// </summary>
    void SetRule(Guid contextId, string folderPath, bool isInclude, string source);

    /// <summary>Removes all rules for a context.</summary>
    void ClearRules(Guid contextId);

    /// <summary>Gets all include/exclude rules for a context.</summary>
    IReadOnlyList<SelectiveSyncRule> GetRules(Guid contextId);

    /// <summary>Persists the rules for <paramref name="contextId"/> to the per-context state database.</summary>
    Task SaveAsync(ILocalStateDb stateDb, string dbPath, Guid contextId, CancellationToken cancellationToken = default);

    /// <summary>Loads the rules for <paramref name="contextId"/> from the per-context state database.</summary>
    Task LoadAsync(ILocalStateDb stateDb, string dbPath, Guid contextId, CancellationToken cancellationToken = default);
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

    /// <summary>Rule origin: <c>"Manual"</c> (selective-sync UI) or <c>"SizeLimit"</c> (folder size limit).</summary>
    public string Source { get; init; } = "Manual";
}
