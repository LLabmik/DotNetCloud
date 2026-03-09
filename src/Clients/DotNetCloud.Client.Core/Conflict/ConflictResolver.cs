using DiffPlex;
using DiffPlex.DiffBuilder;
using DiffPlex.DiffBuilder.Model;
using DiffPlex.Model;
using DotNetCloud.Client.Core.LocalState;
using Microsoft.Extensions.Logging;

namespace DotNetCloud.Client.Core.Conflict;

/// <summary>
/// Resolves sync conflicts by running a 5-strategy auto-resolution pipeline.
/// If no strategy succeeds, a conflict copy is created and the user is notified.
/// </summary>
public sealed class ConflictResolver : IConflictResolver
{
    private readonly ILocalStateDb _stateDb;
    private readonly ILogger<ConflictResolver> _logger;
    private ConflictResolutionSettings _settings = new();

    /// <inheritdoc/>
    public event EventHandler<ConflictAutoResolvedEventArgs>? AutoResolved;

    /// <inheritdoc/>
    public event EventHandler<ConflictDetectedEventArgs>? ConflictDetected;

    /// <summary>Gets or sets the conflict resolution settings (config-driven).</summary>
    public ConflictResolutionSettings Settings
    {
        get => _settings;
        set => _settings = value ?? new ConflictResolutionSettings();
    }

    /// <summary>Initializes a new <see cref="ConflictResolver"/>.</summary>
    public ConflictResolver(ILocalStateDb stateDb, ILogger<ConflictResolver> logger)
    {
        _stateDb = stateDb;
        _logger = logger;
    }

    /// <inheritdoc/>
    public async Task<ConflictResolutionOutcome> ResolveAsync(
        ConflictInfo conflict,
        CancellationToken cancellationToken = default)
    {
        if (!File.Exists(conflict.LocalPath))
            return ConflictResolutionOutcome.ConflictCopyCreated;

        // If auto-resolution is disabled, skip directly to conflict copy.
        if (!_settings.AutoResolveEnabled)
        {
            _logger.LogInformation("Auto-resolution disabled by settings for {Path}. Creating conflict copy.", conflict.LocalPath);
            return await CreateConflictCopyAsync(conflict, cancellationToken);
        }

        // ── Strategy 1: Identical content (hash match) ─────────────────────
        if (_settings.IsStrategyEnabled("identical") &&!string.IsNullOrEmpty(conflict.LocalContentHash) &&
            !string.IsNullOrEmpty(conflict.RemoteContentHash) &&
            conflict.LocalContentHash.Equals(conflict.RemoteContentHash, StringComparison.OrdinalIgnoreCase))
        {
            return await AutoResolveAsync(
                conflict,
                strategy: "Strategy 1 (identical content)",
                resolution: "auto-identical",
                outcome: ConflictResolutionOutcome.AutoResolvedIdentical,
                conflictCopyPath: null,
                cancellationToken);
        }

        // ── Strategy 2: One side unchanged (fast-forward) ──────────────────
        if (_settings.IsStrategyEnabled("fast-forward") &&
            !string.IsNullOrEmpty(conflict.BaseContentHash))
        {
            // If local = base, the local copy hasn't changed → server version wins.
            if (!string.IsNullOrEmpty(conflict.LocalContentHash) &&
                conflict.LocalContentHash.Equals(conflict.BaseContentHash, StringComparison.OrdinalIgnoreCase))
            {
                return await AutoResolveAsync(
                    conflict,
                    strategy: "Strategy 2 (fast-forward: local unchanged)",
                    resolution: "auto-fast-forward",
                    outcome: ConflictResolutionOutcome.AutoResolvedServerWins,
                    conflictCopyPath: null,
                    cancellationToken);
            }

            // If remote = base, the remote copy hasn't changed → local version wins.
            if (!string.IsNullOrEmpty(conflict.RemoteContentHash) &&
                conflict.RemoteContentHash.Equals(conflict.BaseContentHash, StringComparison.OrdinalIgnoreCase))
            {
                return await AutoResolveAsync(
                    conflict,
                    strategy: "Strategy 2 (fast-forward: server unchanged)",
                    resolution: "auto-fast-forward",
                    outcome: ConflictResolutionOutcome.AutoResolvedLocalWins,
                    conflictCopyPath: null,
                    cancellationToken);
            }
        }

        // ── Strategy 3: Non-overlapping text merge (three-way) ─────────────
        // Requires base content, local content, and server content (all as text).
        if (_settings.IsStrategyEnabled("clean-merge") &&
            conflict.BaseContent is not null &&
            conflict.LocalContent is not null &&
            conflict.ServerContent is not null &&
            FileTypeClassifier.IsTextBased(conflict.LocalPath))
        {
            var mergeResult = TryThreeWayMerge(conflict.BaseContent, conflict.LocalContent, conflict.ServerContent);
            if (mergeResult is not null)
            {
                try
                {
                    await File.WriteAllTextAsync(conflict.LocalPath, mergeResult, cancellationToken);
                    return await AutoResolveAsync(
                        conflict,
                        strategy: "Strategy 3 (clean text merge)",
                        resolution: "auto-merged",
                        outcome: ConflictResolutionOutcome.AutoResolvedLocalWins,
                        conflictCopyPath: null,
                        cancellationToken);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex,
                        "Strategy 3 merge succeeded but failed to write merged content for {Path}.", conflict.LocalPath);
                }
            }
        }

        // ── Strategy 4: Timestamp + single-user heuristic ──────────────────
        // If both timestamps are set and differ by >threshold minutes, keep the newer one.
        if (_settings.IsStrategyEnabled("newer-wins") &&
            conflict.LocalModifiedAt != default && conflict.RemoteUpdatedAt != default)
        {
            var diff = (conflict.LocalModifiedAt - conflict.RemoteUpdatedAt).Duration();
            if (diff > TimeSpan.FromMinutes(_settings.NewerWinsThresholdMinutes))
            {
                var localIsNewer = conflict.LocalModifiedAt > conflict.RemoteUpdatedAt;
                return await AutoResolveAsync(
                    conflict,
                    strategy: "Strategy 4 (newer-wins)",
                    resolution: "auto-newer-wins",
                    outcome: localIsNewer
                        ? ConflictResolutionOutcome.AutoResolvedLocalWins
                        : ConflictResolutionOutcome.AutoResolvedServerWins,
                    conflictCopyPath: null,
                    cancellationToken);
            }
        }

        // ── Strategy 5: Append-only file detection ──────────────────────────
        // Requires both local and server content as text.
        if (_settings.IsStrategyEnabled("append-only") &&
            conflict.LocalContent is not null &&
            conflict.ServerContent is not null &&
            FileTypeClassifier.IsTextBased(conflict.LocalPath))
        {
            var appendResult = TryAppendOnlyResolve(
                conflict.LocalContent, conflict.ServerContent, conflict.LocalUserId);

            if (appendResult is not null)
            {
                try
                {
                    await File.WriteAllTextAsync(conflict.LocalPath, appendResult.MergedContent, cancellationToken);
                    return await AutoResolveAsync(
                        conflict,
                        strategy: "Strategy 5 (append-only)",
                        resolution: appendResult.Resolution,
                        outcome: ConflictResolutionOutcome.AutoResolvedLocalWins,
                        conflictCopyPath: null,
                        cancellationToken);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex,
                        "Strategy 5 append resolve failed to write for {Path}.", conflict.LocalPath);
                }
            }
        }

        // ── All strategies failed — create conflict copy ────────────────────
        return await CreateConflictCopyAsync(conflict, cancellationToken);
    }

    /// <summary>Creates a conflict copy and notifies listeners.</summary>
    private async Task<ConflictResolutionOutcome> CreateConflictCopyAsync(
        ConflictInfo conflict, CancellationToken cancellationToken)
    {
        var conflictCopyPath = BuildConflictCopyPath(conflict.LocalPath);
        File.Move(conflict.LocalPath, conflictCopyPath, overwrite: false);

        _logger.LogWarning(
            "Conflict detected for {Path}. No auto-resolution strategy applied. " +
            "Local copy saved as {ConflictCopy}.",
            conflict.LocalPath, conflictCopyPath);

        if (!string.IsNullOrEmpty(conflict.StateDatabasePath))
        {
            await _stateDb.SaveConflictRecordAsync(conflict.StateDatabasePath, new ConflictRecord
            {
                OriginalPath = conflict.LocalPath,
                ConflictCopyPath = conflictCopyPath,
                NodeId = conflict.NodeId,
                LocalModifiedAt = conflict.LocalModifiedAt,
                RemoteModifiedAt = conflict.RemoteUpdatedAt,
                DetectedAt = DateTime.UtcNow,
                BaseContentHash = conflict.BaseContentHash,
                AutoResolved = false,
            }, cancellationToken);
        }

        ConflictDetected?.Invoke(this, new ConflictDetectedEventArgs
        {
            OriginalPath = conflict.LocalPath,
            ConflictCopyPath = conflictCopyPath,
        });

        return ConflictResolutionOutcome.ConflictCopyCreated;
    }

    // ── Auto-resolution helper ────────────────────────────────────────────

    private async Task<ConflictResolutionOutcome> AutoResolveAsync(
        ConflictInfo conflict,
        string strategy,
        string resolution,
        ConflictResolutionOutcome outcome,
        string? conflictCopyPath,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation(
            "Conflict auto-resolved for {Path} using {Strategy}. Resolution={Resolution} Outcome={Outcome}.",
            conflict.LocalPath, strategy, resolution, outcome);

        if (!string.IsNullOrEmpty(conflict.StateDatabasePath))
        {
            await _stateDb.SaveConflictRecordAsync(conflict.StateDatabasePath, new ConflictRecord
            {
                OriginalPath = conflict.LocalPath,
                ConflictCopyPath = conflictCopyPath ?? string.Empty,
                NodeId = conflict.NodeId,
                LocalModifiedAt = conflict.LocalModifiedAt,
                RemoteModifiedAt = conflict.RemoteUpdatedAt,
                DetectedAt = DateTime.UtcNow,
                ResolvedAt = DateTime.UtcNow,
                Resolution = resolution,
                BaseContentHash = conflict.BaseContentHash,
                AutoResolved = true,
            }, cancellationToken);
        }

        AutoResolved?.Invoke(this, new ConflictAutoResolvedEventArgs
        {
            LocalPath = conflict.LocalPath,
            Strategy = strategy,
            Resolution = resolution,
            Outcome = outcome,
        });

        return outcome;
    }

    // ── Strategy 3: Three-way text merge (DiffPlex) ───────────────────────

    /// <summary>
    /// Attempts a non-overlapping three-way merge of base, local, and server text.
    /// Returns the merged content if clean (no overlapping changes), otherwise null.
    /// </summary>
    private static string? TryThreeWayMerge(string baseText, string localText, string serverText)
    {
        var differ = new Differ();
        var localDiff = differ.CreateLineDiffs(baseText, localText, ignoreWhitespace: false);
        var serverDiff = differ.CreateLineDiffs(baseText, serverText, ignoreWhitespace: false);

        // Collect base-line ranges modified by each side.
        var localRanges = GetModifiedBaseRanges(localDiff.DiffBlocks);
        var serverRanges = GetModifiedBaseRanges(serverDiff.DiffBlocks);

        // Check for overlaps between local and server modifications.
        foreach (var lr in localRanges)
        {
            foreach (var sr in serverRanges)
            {
                if (lr.End >= sr.Start && sr.End >= lr.Start)
                    return null; // Overlapping change → can't auto-merge.
            }
        }

        // No overlaps → apply both sets of changes to produce merged result.
        var baseLines = baseText.Split('\n');
        var localLines = localText.Split('\n');
        var serverLines = serverText.Split('\n');

        var result = new List<string>();
        int baseIdx = 0;

        // Merge by interleaving local and server changes at non-overlapping positions.
        var allBlocks = BuildMergedBlockList(localDiff.DiffBlocks, serverDiff.DiffBlocks,
            localLines, serverLines);

        foreach (var block in allBlocks)
        {
            // Emit base lines before this block.
            while (baseIdx < block.DeleteStartA)
                result.Add(baseLines[baseIdx++]);

            // Skip deleted base lines.
            baseIdx += block.DeleteCountA;

            // Emit inserted lines.
            for (int i = 0; i < block.InsertCountB; i++)
                result.Add(block.InsertedLines[i]);
        }

        // Emit remaining base lines.
        while (baseIdx < baseLines.Length)
            result.Add(baseLines[baseIdx++]);

        return string.Join('\n', result);
    }

    private static List<(int Start, int End)> GetModifiedBaseRanges(IList<DiffBlock> blocks) =>
        blocks.Select(b => (b.DeleteStartA, b.DeleteStartA + Math.Max(b.DeleteCountA - 1, 0))).ToList();

    private sealed class MergeBlock
    {
        public int DeleteStartA { get; init; }
        public int DeleteCountA { get; init; }
        public int InsertCountB { get; init; }
        public string[] InsertedLines { get; init; } = [];
    }

    private static List<MergeBlock> BuildMergedBlockList(
        IList<DiffBlock> localBlocks,
        IList<DiffBlock> serverBlocks,
        string[] localLines,
        string[] serverLines)
    {
        var result = new List<MergeBlock>();

        foreach (var b in localBlocks)
        {
            result.Add(new MergeBlock
            {
                DeleteStartA = b.DeleteStartA,
                DeleteCountA = b.DeleteCountA,
                InsertCountB = b.InsertCountB,
                InsertedLines = Enumerable.Range(b.InsertStartB, b.InsertCountB)
                    .Select(i => localLines[i]).ToArray(),
            });
        }

        foreach (var b in serverBlocks)
        {
            result.Add(new MergeBlock
            {
                DeleteStartA = b.DeleteStartA,
                DeleteCountA = b.DeleteCountA,
                InsertCountB = b.InsertCountB,
                InsertedLines = Enumerable.Range(b.InsertStartB, b.InsertCountB)
                    .Select(i => serverLines[i]).ToArray(),
            });
        }

        return [.. result.OrderBy(b => b.DeleteStartA)];
    }

    // ── Strategy 5: Append-only detection ────────────────────────────────

    private sealed class AppendResolveResult
    {
        public required string MergedContent { get; init; }
        public required string Resolution { get; init; }
    }

    private static AppendResolveResult? TryAppendOnlyResolve(
        string localContent,
        string serverContent,
        Guid localUserId)
    {
        // Single-user: if one is a clean prefix of the other, keep the longer one.
        if (localContent.StartsWith(serverContent, StringComparison.Ordinal))
        {
            return new AppendResolveResult
            {
                MergedContent = localContent,
                Resolution = "auto-append",
            };
        }

        if (serverContent.StartsWith(localContent, StringComparison.Ordinal))
        {
            return new AppendResolveResult
            {
                MergedContent = serverContent,
                Resolution = "auto-append",
            };
        }

        // Multi-user: find common prefix (must be ≥ 90% of the shorter version).
        var shorter = localContent.Length <= serverContent.Length ? localContent : serverContent;
        var prefixLen = CommonPrefixLength(localContent, serverContent);
        if (prefixLen < shorter.Length * 0.9)
            return null; // Not a clean append pattern.

        // Concatenate local appendage then server appendage onto the shared prefix.
        var sharedBase = localContent[..prefixLen];
        var localAppend = localContent[prefixLen..];
        var serverAppend = serverContent[prefixLen..];

        return new AppendResolveResult
        {
            MergedContent = sharedBase + localAppend + (localAppend.EndsWith('\n') ? "" : "\n") + serverAppend,
            Resolution = "auto-append-combined",
        };
    }

    private static int CommonPrefixLength(string a, string b)
    {
        int len = Math.Min(a.Length, b.Length);
        for (int i = 0; i < len; i++)
        {
            if (a[i] != b[i]) return i;
        }
        return len;
    }

    // ── Conflict copy path builder ────────────────────────────────────────

    /// <summary>
    /// Builds a conflict copy path using the pattern:
    /// <c>{baseName} (conflict - {user} - {date}){ext}</c>
    /// </summary>
    public static string BuildConflictCopyPath(string originalPath)
    {
        var directory = Path.GetDirectoryName(originalPath) ?? string.Empty;
        var ext = Path.GetExtension(originalPath);
        var baseName = Path.GetFileNameWithoutExtension(originalPath);
        var user = Environment.UserName;
        var date = DateTime.Now.ToString("yyyy-MM-dd");

        var candidate = Path.Combine(directory, $"{baseName} (conflict - {user} - {date}){ext}");
        var n = 1;
        while (File.Exists(candidate))
        {
            candidate = Path.Combine(directory, $"{baseName} (conflict - {user} - {date} {n}){ext}");
            n++;
        }
        return candidate;
    }
}
