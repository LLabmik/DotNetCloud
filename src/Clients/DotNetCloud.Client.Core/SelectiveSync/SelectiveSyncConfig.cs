using DotNetCloud.Client.Core.LocalState;

namespace DotNetCloud.Client.Core.SelectiveSync;

/// <summary>
/// In-memory selective sync configuration with per-context SQLite persistence.
/// Rules are evaluated with exclude taking precedence over include.
/// </summary>
public sealed class SelectiveSyncConfig : ISelectiveSyncConfig
{
    private const string ReservedExcludedRoot = "_DotNetCloud";

    private readonly Dictionary<Guid, List<SelectiveSyncRule>> _rules = new();

    /// <inheritdoc/>
    public bool IsIncluded(Guid contextId, string localPath)
    {
        var normalizedPath = NormalizePath(localPath);
        if (IsReservedExcludedPath(normalizedPath))
        {
            return false;
        }

        if (!_rules.TryGetValue(contextId, out var rules) || rules.Count == 0)
            return true; // No rules = include everything

        // Find the longest-matching rule (most specific wins)
        SelectiveSyncRule? bestMatch = null;
        var bestLength = -1;

        foreach (var rule in rules)
        {
            var normalizedRulePath = NormalizePath(rule.FolderPath);
            if (MatchesRule(normalizedPath, normalizedRulePath)
                && normalizedRulePath.Length > bestLength)
            {
                bestMatch = rule;
                bestLength = normalizedRulePath.Length;
            }
        }

        // If no rule matches, default to include
        return bestMatch?.IsInclude ?? true;
    }

    /// <inheritdoc/>
    public void Include(Guid contextId, string folderPath)
    {
        var normalizedPath = NormalizePath(folderPath);
        var rules = GetOrCreateList(contextId);
        rules.RemoveAll(r => NormalizePath(r.FolderPath).Equals(normalizedPath, StringComparison.OrdinalIgnoreCase));

        if (IsReservedExcludedPath(normalizedPath))
        {
            return;
        }

        rules.Add(new SelectiveSyncRule { FolderPath = normalizedPath, IsInclude = true });
    }

    /// <inheritdoc/>
    public void Exclude(Guid contextId, string folderPath)
    {
        var normalizedPath = NormalizePath(folderPath);
        var rules = GetOrCreateList(contextId);
        rules.RemoveAll(r => NormalizePath(r.FolderPath).Equals(normalizedPath, StringComparison.OrdinalIgnoreCase));

        if (IsReservedExcludedPath(normalizedPath))
        {
            return;
        }

        rules.Add(new SelectiveSyncRule { FolderPath = normalizedPath, IsInclude = false });
    }

    /// <inheritdoc/>
    public void SetRule(Guid contextId, string folderPath, bool isInclude, string source)
    {
        var normalizedPath = NormalizePath(folderPath);
        var rules = GetOrCreateList(contextId);
        rules.RemoveAll(r => NormalizePath(r.FolderPath).Equals(normalizedPath, StringComparison.OrdinalIgnoreCase));

        if (IsReservedExcludedPath(normalizedPath))
        {
            return;
        }

        rules.Add(new SelectiveSyncRule { FolderPath = normalizedPath, IsInclude = isInclude, Source = source });
    }

    /// <inheritdoc/>
    public void ClearRules(Guid contextId) => _rules.Remove(contextId);

    /// <inheritdoc/>
    public IReadOnlyList<SelectiveSyncRule> GetRules(Guid contextId) =>
        _rules.TryGetValue(contextId, out var list) ? list.AsReadOnly() : [];

    /// <inheritdoc/>
    public async Task SaveAsync(ILocalStateDb stateDb, string dbPath, Guid contextId, CancellationToken cancellationToken = default)
    {
        var rules = _rules.TryGetValue(contextId, out var list) ? list : [];
        var rows = rules
            .Where(r => !IsReservedExcludedPath(r.FolderPath))
            .Select(r => new SyncFolderRule
            {
                RelativePath = r.FolderPath.TrimStart('/'),
                IsInclude = r.IsInclude,
                Source = r.Source,
                UpdatedAt = DateTime.UtcNow,
            })
            .ToList();

        await stateDb.ReplaceSyncFolderRulesAsync(dbPath, rows, cancellationToken);
    }

    /// <inheritdoc/>
    public async Task LoadAsync(ILocalStateDb stateDb, string dbPath, Guid contextId, CancellationToken cancellationToken = default)
    {
        var rows = await stateDb.GetSyncFolderRulesAsync(dbPath, cancellationToken);
        _rules[contextId] = rows
            .Select(r => new SelectiveSyncRule
            {
                FolderPath = "/" + r.RelativePath.TrimStart('/'),
                IsInclude = r.IsInclude,
                Source = r.Source,
            })
            .ToList();
    }

    private List<SelectiveSyncRule> GetOrCreateList(Guid contextId)
    {
        if (!_rules.TryGetValue(contextId, out var list))
        {
            list = new List<SelectiveSyncRule>();
            _rules[contextId] = list;
        }
        return list;
    }

    /// <summary>
    /// Returns true when the path targets the reserved virtual shared-folder root.
    /// </summary>
    public static bool IsReservedExcludedPath(string path)
    {
        var normalizedPath = NormalizePath(path);
        if (string.IsNullOrEmpty(normalizedPath))
        {
            return false;
        }

        return normalizedPath.Equals($"/{ReservedExcludedRoot}", StringComparison.OrdinalIgnoreCase)
            || normalizedPath.StartsWith($"/{ReservedExcludedRoot}/", StringComparison.OrdinalIgnoreCase);
    }

    private static bool MatchesRule(string path, string rulePath)
    {
        if (string.IsNullOrEmpty(path) || string.IsNullOrEmpty(rulePath))
        {
            return false;
        }

        return path.Equals(rulePath, StringComparison.OrdinalIgnoreCase)
            || path.StartsWith(rulePath + "/", StringComparison.OrdinalIgnoreCase);
    }

    private static string NormalizePath(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return string.Empty;
        }

        var normalized = path.Replace('\\', '/').Trim();
        normalized = normalized.Trim('/');
        return normalized.Length == 0 ? "/" : "/" + normalized;
    }
}
