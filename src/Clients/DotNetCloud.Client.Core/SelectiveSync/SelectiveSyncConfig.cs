using System.Text.Json;

namespace DotNetCloud.Client.Core.SelectiveSync;

/// <summary>
/// In-memory selective sync configuration with JSON persistence.
/// Rules are evaluated with exclude taking precedence over include.
/// </summary>
public sealed class SelectiveSyncConfig : ISelectiveSyncConfig
{
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    private readonly Dictionary<Guid, List<SelectiveSyncRule>> _rules = new();

    /// <inheritdoc/>
    public bool IsIncluded(Guid contextId, string localPath)
    {
        if (!_rules.TryGetValue(contextId, out var rules) || rules.Count == 0)
            return true; // No rules = include everything

        // Find the longest-matching rule (most specific wins)
        SelectiveSyncRule? bestMatch = null;
        var bestLength = -1;

        foreach (var rule in rules)
        {
            if (localPath.StartsWith(rule.FolderPath, StringComparison.OrdinalIgnoreCase)
                && rule.FolderPath.Length > bestLength)
            {
                bestMatch = rule;
                bestLength = rule.FolderPath.Length;
            }
        }

        // If no rule matches, default to include
        return bestMatch?.IsInclude ?? true;
    }

    /// <inheritdoc/>
    public void Include(Guid contextId, string folderPath)
    {
        GetOrCreateList(contextId).RemoveAll(r => r.FolderPath.Equals(folderPath, StringComparison.OrdinalIgnoreCase));
        GetOrCreateList(contextId).Add(new SelectiveSyncRule { FolderPath = folderPath, IsInclude = true });
    }

    /// <inheritdoc/>
    public void Exclude(Guid contextId, string folderPath)
    {
        GetOrCreateList(contextId).RemoveAll(r => r.FolderPath.Equals(folderPath, StringComparison.OrdinalIgnoreCase));
        GetOrCreateList(contextId).Add(new SelectiveSyncRule { FolderPath = folderPath, IsInclude = false });
    }

    /// <inheritdoc/>
    public void ClearRules(Guid contextId) => _rules.Remove(contextId);

    /// <inheritdoc/>
    public IReadOnlyList<SelectiveSyncRule> GetRules(Guid contextId) =>
        _rules.TryGetValue(contextId, out var list) ? list.AsReadOnly() : [];

    /// <inheritdoc/>
    public async Task SaveAsync(string filePath, CancellationToken cancellationToken = default)
    {
        var serializable = _rules.ToDictionary(
            kvp => kvp.Key.ToString(),
            kvp => kvp.Value);
        await using var stream = File.Create(filePath);
        await JsonSerializer.SerializeAsync(stream, serializable, JsonOptions, cancellationToken);
    }

    /// <inheritdoc/>
    public async Task LoadAsync(string filePath, CancellationToken cancellationToken = default)
    {
        if (!File.Exists(filePath)) return;

        await using var stream = File.OpenRead(filePath);
        var loaded = await JsonSerializer.DeserializeAsync<Dictionary<string, List<SelectiveSyncRule>>>(
            stream, JsonOptions, cancellationToken);

        if (loaded is null) return;

        _rules.Clear();
        foreach (var (key, value) in loaded)
        {
            if (Guid.TryParse(key, out var id))
                _rules[id] = value;
        }
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
}
