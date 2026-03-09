namespace DotNetCloud.Client.Core.Conflict;

/// <summary>
/// Configuration for conflict auto-resolution behavior.
/// Maps to the <c>conflictResolution</c> section of <c>sync-settings.json</c>.
/// </summary>
public sealed class ConflictResolutionSettings
{
    /// <summary>Whether auto-resolution is enabled. Default <c>true</c>.</summary>
    public bool AutoResolveEnabled { get; set; } = true;

    /// <summary>
    /// Threshold in minutes for the newer-wins (Strategy 4) heuristic.
    /// When both timestamps differ by more than this value, the newer version wins.
    /// Default: 5 minutes.
    /// </summary>
    public int NewerWinsThresholdMinutes { get; set; } = 5;

    /// <summary>
    /// List of enabled auto-resolution strategy names.
    /// Valid values: <c>identical</c>, <c>fast-forward</c>, <c>clean-merge</c>,
    /// <c>newer-wins</c>, <c>append-only</c>.
    /// </summary>
    public List<string> EnabledStrategies { get; set; } =
    [
        "identical",
        "fast-forward",
        "clean-merge",
        "newer-wins",
        "append-only",
    ];

    /// <summary>Returns <c>true</c> if the named strategy is enabled.</summary>
    public bool IsStrategyEnabled(string name) =>
        EnabledStrategies.Contains(name, StringComparer.OrdinalIgnoreCase);
}
