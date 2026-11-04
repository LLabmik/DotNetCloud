using System.Diagnostics;
using System.IO.Compression;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.RegularExpressions;
using DotNetCloud.Core.DTOs;

namespace DotNetCloud.CLI.Infrastructure;

/// <summary>
/// Parses human-readable backup part sizes (for example <c>512MB</c> or <c>8TB</c>). Multipliers are
/// binary (1 KB = 1024 bytes).
/// </summary>
internal static class BackupSizes
{
    private static readonly Regex Pattern = new(
        @"^\s*(?<value>\d+(?:\.\d+)?)\s*(?<unit>B|KB|MB|GB|TB|KIB|MIB|GIB|TIB)?\s*$",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    /// <summary>
    /// Parses a size string. Returns <paramref name="defaultValue"/> for null/empty input and throws
    /// <see cref="FormatException"/> for invalid input.
    /// </summary>
    internal static long Parse(string? value, long defaultValue = 0)
    {
        if (string.IsNullOrWhiteSpace(value))
            return defaultValue;

        var match = Pattern.Match(value);
        if (!match.Success)
            throw new FormatException($"Invalid size '{value}'. Use a number optionally followed by KB, MB, GB or TB.");

        var number = double.Parse(match.Groups["value"].Value, System.Globalization.CultureInfo.InvariantCulture);
        var multiplier = match.Groups["unit"].Value.ToUpperInvariant() switch
        {
            "" or "B" => 1L,
            "KB" or "KIB" => 1024L,
            "MB" or "MIB" => 1024L * 1024L,
            "GB" or "GIB" => 1024L * 1024L * 1024L,
            "TB" or "TIB" => 1024L * 1024L * 1024L * 1024L,
            _ => throw new FormatException($"Unsupported size unit in '{value}'."),
        };

        var result = number * multiplier;
        if (result <= 0 || result > long.MaxValue)
            throw new FormatException($"Size '{value}' is out of range.");

        return (long)result;
    }
}

/// <summary>One part of a split zip backup set.</summary>
internal sealed record BackupPartInfo(string Name, long SizeBytes, string Sha256, int EntryCount);

/// <summary>
/// Manifest written next to a split zip set (<c>&lt;base&gt;.backupset.json</c>). Restore uses it to
/// open the parts in order and verify them.
/// </summary>
internal sealed record BackupSetManifest(
    int FormatVersion,
    string Mode,
    string CreatedUtc,
    string BaseName,
    long SplitSizeBytes,
    IReadOnlyList<BackupPartInfo> Parts,
    int TotalEntries,
    BackupInfo? Info)
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    /// <summary>Current set-manifest version.</summary>
    public const int CurrentVersion = 3;

    internal static string Serialize(BackupSetManifest manifest)
        => JsonSerializer.Serialize(manifest, JsonOptions);

    internal static BackupSetManifest? Deserialize(string json)
    {
        try
        { return JsonSerializer.Deserialize<BackupSetManifest>(json, JsonOptions); }
        catch (JsonException)
        { return null; }
    }
}

/// <summary>
/// Writes one or more zip parts. A part is closed before a file that would push it past the split
/// size; entries are never split across parts. With a split size of 0 this behaves like a normal
/// single-file backup (and writes the usual <c>.sha256</c> sidecar).
/// </summary>
internal sealed class SplitZipWriter : IDisposable
{
    private readonly string _basePath;
    private readonly long _splitSize;
    private readonly CompressionLevel _compression;
    private readonly List<PartBuilder> _parts = [];
    private FileStream? _stream;
    private ZipArchive? _archive;
    private PartBuilder? _current;
    private bool _completed;

    /// <summary>Creates a writer.</summary>
    /// <param name="outputPath">Single-archive path, or the base name for a split set.</param>
    /// <param name="splitSizeBytes">Maximum uncompressed bytes per part (0 = no split).</param>
    /// <param name="compression">Deflate level.</param>
    internal SplitZipWriter(string outputPath, long splitSizeBytes, CompressionLevel compression)
    {
        _splitSize = splitSizeBytes;
        _compression = compression;

        if (splitSizeBytes > 0)
        {
            var directory = Path.GetDirectoryName(Path.GetFullPath(outputPath)) ?? ".";
            _basePath = Path.Combine(directory, Path.GetFileNameWithoutExtension(outputPath));
            ManifestPath = _basePath + ".backupset.json";
        }
        else
        {
            _basePath = outputPath;
        }
    }

    /// <summary>Total entries written so far (excluding nothing).</summary>
    internal int TotalEntries { get; private set; }

    /// <summary>Manifest path for a split set; <see langword="null"/> for a single archive.</summary>
    internal string? ManifestPath { get; }

    /// <summary>Part paths written so far.</summary>
    internal IReadOnlyList<string> PartPaths => _parts.Select(p => p.Path).ToArray();

    /// <summary>Adds a file to the current part, opening a new part first when required.</summary>
    internal void AddFile(string sourcePath, string entryName, long length)
    {
        EnsureCapacity(length);
        _archive!.CreateEntryFromFile(sourcePath, entryName, _compression);
        _current!.EntryCount++;
        _current.UncompressedBytes += Math.Max(0, length);
        TotalEntries++;
    }

    /// <summary>Adds a text entry.</summary>
    internal void AddText(string entryName, string content)
    {
        EnsureCapacity(0);
        var entry = _archive!.CreateEntry(entryName);
        using var writer = new StreamWriter(entry.Open());
        writer.Write(content);
        _current!.EntryCount++;
        TotalEntries++;
    }

    /// <summary>
    /// Finishes the archive. Writes <c>backup-info.json</c> as the final entry, then either the
    /// single-archive checksum or the part checksums + set manifest.
    /// </summary>
    /// <param name="info">Backup metadata (counts are completed here).</param>
    /// <returns>Paths of the written parts.</returns>
    internal IReadOnlyList<string> Complete(BackupInfo info)
    {
        ArgumentNullException.ThrowIfNull(info);

        // The info entry itself is part of the archive.
        var completedInfo = info with
        {
            Mode = "zip",
            SplitSizeBytes = _splitSize,
            TotalEntryCount = TotalEntries + 1,
        };
        AddText(BackupArchive.InfoEntryName, BackupArchive.SerializeInfo(completedInfo));

        ClosePart();
        _completed = true;

        if (_splitSize == 0)
        {
            var single = _parts[0];
            File.WriteAllText(
                single.Path + ".sha256",
                $"{single.Sha256}  {Path.GetFileName(single.Path)}{Environment.NewLine}");
            return [single.Path];
        }

        foreach (var part in _parts)
        {
            File.WriteAllText(
                part.Path + ".sha256",
                $"{part.Sha256}  {Path.GetFileName(part.Path)}{Environment.NewLine}");
        }

        var manifest = new BackupSetManifest(
            BackupSetManifest.CurrentVersion,
            "zip-set",
            completedInfo.CreatedUtc,
            Path.GetFileName(_basePath),
            _splitSize,
            _parts.Select(p => new BackupPartInfo(Path.GetFileName(p.Path), p.SizeBytes, p.Sha256, p.EntryCount)).ToArray(),
            TotalEntries,
            completedInfo);

        File.WriteAllText(ManifestPath!, BackupSetManifest.Serialize(manifest));
        return _parts.Select(p => p.Path).ToArray();
    }

    private void EnsureCapacity(long nextLength)
    {
        if (_archive is null)
        {
            OpenPart();
            return;
        }

        if (_splitSize > 0 && _current!.EntryCount > 0 && _current.UncompressedBytes + nextLength > _splitSize)
        {
            ClosePart();
            OpenPart();
        }
    }

    private void OpenPart()
    {
        var index = _parts.Count + 1;
        var path = _splitSize > 0
            ? $"{_basePath}.part{index:000}.zip"
            : _basePath;

        // Starting a new set invalidates any older set written under the same base name: leaving the
        // extra parts behind would make a shorter new set look complete but corrupt.
        if (_splitSize > 0 && index == 1)
            DeleteExistingSet();

        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);

        _stream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.None);
        _archive = new ZipArchive(_stream, ZipArchiveMode.Create, leaveOpen: true);
        _current = new PartBuilder(path);
        _parts.Add(_current);
    }

    private void DeleteExistingSet()
    {
        var directory = Path.GetDirectoryName(_basePath);
        if (string.IsNullOrEmpty(directory) || !Directory.Exists(directory))
            return;

        var name = Path.GetFileName(_basePath);
        foreach (var file in Directory.EnumerateFiles(directory, name + ".part*.zip"))
            TryDelete(file);

        foreach (var file in Directory.EnumerateFiles(directory, name + ".part*.zip.sha256"))
            TryDelete(file);
    }

    private void ClosePart()
    {
        if (_archive is null || _stream is null || _current is null)
            return;

        _archive.Dispose();
        _stream.Flush();
        _stream.Dispose();

        _archive = null;
        _stream = null;
        _current.SizeBytes = new FileInfo(_current.Path).Length;
        _current.Sha256 = ComputeSha256(_current.Path);
    }

    private static string ComputeSha256(string path)
    {
        using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
        return Convert.ToHexStringLower(SHA256.HashData(stream));
    }

    private void DeleteParts()
    {
        foreach (var part in _parts)
        {
            TryDelete(part.Path);
            TryDelete(part.Path + ".sha256");
        }

        if (ManifestPath is not null)
            TryDelete(ManifestPath);
    }

    private static void TryDelete(string path)
    {
        try
        { if (File.Exists(path)) File.Delete(path); }
        catch { /* best effort */ }
    }

    /// <inheritdoc />
    public void Dispose()
    {
        if (_completed)
            return;

        try
        {
            _archive?.Dispose();
            _stream?.Dispose();
        }
        catch
        {
            // ignore disposal failures while cleaning up a failed backup
        }

        DeleteParts();
    }

    private sealed class PartBuilder(string path)
    {
        public string Path { get; } = path;
        public int EntryCount { get; set; }
        public long UncompressedBytes { get; set; }
        public long SizeBytes { get; set; }
        public string Sha256 { get; set; } = string.Empty;
    }
}

/// <summary>Creates hard links (used by incremental snapshots).</summary>
internal interface IHardLinker
{
    /// <summary>Creates <paramref name="newPath"/> as a hard link to <paramref name="existingPath"/>.</summary>
    bool TryCreate(string existingPath, string newPath);
}

/// <summary>Platform hard-link implementation (Unix <c>link(2)</c>, Windows <c>CreateHardLinkW</c>).</summary>
internal sealed class PlatformHardLinker : IHardLinker
{
    /// <inheritdoc />
    public bool TryCreate(string existingPath, string newPath)
    {
        try
        {
            if (OperatingSystem.IsWindows())
                return CreateHardLinkW(newPath, existingPath, IntPtr.Zero);

            return link(existingPath, newPath) == 0;
        }
        catch
        {
            return false;
        }
    }

    [DllImport("libc", SetLastError = true)]
    private static extern int link(string oldpath, string newpath);

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern bool CreateHardLinkW(string lpFileName, string lpExistingFileName, IntPtr lpSecurityAttributes);
}

/// <summary>Outcome of an incremental mirror snapshot.</summary>
internal sealed record MirrorResult(
    string SnapshotPath,
    int Files,
    int EmptyDirectories,
    int HardLinkedFiles,
    int CopiedFiles,
    string? PreviousSnapshot);

/// <summary>
/// Writes an incremental snapshot: the data directory is mirrored into
/// <c>&lt;target&gt;/&lt;timestamp&gt;/data</c>, with content-addressed blobs hardlinked from the
/// previous snapshot when unchanged. The layout matches a zip archive (<c>data/</c>, <c>config/</c>,
/// <c>system/</c>, <c>opt/</c>, <c>database.sql</c>, <c>backup-info.json</c>), so the same restore
/// logic works for both.
/// </summary>
internal sealed class MirrorSnapshotWriter
{
    private static readonly Regex ContentHashName = new("^[0-9a-fA-F]{64}$", RegexOptions.CultureInvariant);

    private readonly string _targetRoot;
    private readonly string _dataDirectory;
    private readonly string _configDirectory;
    private readonly string _installDirectory;
    private readonly string _serviceUser;
    private readonly IReadOnlyList<string> _excludePaths;
    private readonly IHardLinker _hardLinker;
    private readonly Action<BackupProgress>? _progress;
    private readonly bool _includeSystem;

    internal MirrorSnapshotWriter(
        string targetRoot,
        string dataDirectory,
        string configDirectory,
        string installDirectory,
        string serviceUser,
        IReadOnlyList<string> excludePaths,
        bool includeSystem = true,
        IHardLinker? hardLinker = null,
        Action<BackupProgress>? progress = null)
    {
        _targetRoot = targetRoot;
        _dataDirectory = dataDirectory;
        _configDirectory = configDirectory;
        _installDirectory = installDirectory;
        _serviceUser = serviceUser;
        _excludePaths = excludePaths;
        _includeSystem = includeSystem;
        _hardLinker = hardLinker ?? new PlatformHardLinker();
        _progress = progress;
    }

    /// <summary>
    /// Runs the snapshot. <paramref name="databaseDumper"/> receives the target path for
    /// <c>database.sql</c> and must return whether the dump succeeded.
    /// </summary>
    internal async Task<MirrorResult> RunAsync(
        Func<string, Task<bool>> databaseDumper,
        CancellationToken cancellationToken)
    {
        Directory.CreateDirectory(_targetRoot);

        var partial = FindNewestPartial() ?? Path.Combine(
            _targetRoot, DateTime.UtcNow.ToString("yyyyMMdd-HHmmss") + ".partial");
        Directory.CreateDirectory(partial);

        var previous = FindLatestCompleted(partial);
        if (previous is not null)
            ConsoleOutput.WriteInfo($"Incremental snapshot: hardlinking unchanged blobs from {Path.GetFileName(previous)}.");

        // System configuration (small; always refreshed).
        var systemEntries = new List<string>();
        var warnings = new List<string>();
        if (_includeSystem)
        {
            foreach (var entry in BackupArchive.ResolveExistingEntries(
                         BackupArchive.GetSystemPayloadEntries(_configDirectory, _installDirectory), warnings))
            {
                cancellationToken.ThrowIfCancellationRequested();

                if (!BackupArchive.CanRead(entry.SourcePath))
                {
                    warnings.Add($"System file not readable: {entry.SourcePath}");
                    continue;
                }

                var target = Path.Combine(partial, entry.EntryName.Replace('/', Path.DirectorySeparatorChar));
                Directory.CreateDirectory(Path.GetDirectoryName(target)!);
                File.Copy(entry.SourcePath, target, overwrite: true);
                systemEntries.Add(entry.EntryName);
            }
        }

        // Database dump.
        _progress?.Invoke(new BackupProgress("database", 0, 0, 0, 0));
        var dumpPath = Path.Combine(partial, BackupArchive.DatabaseEntryName);
        var dumpOk = await databaseDumper(dumpPath);

        // Data directory: hardlink unchanged content-addressed blobs, copy everything else.
        _progress?.Invoke(new BackupProgress("mirroring", 0, 0, 0, 0));

        var files = 0;
        var emptyDirectories = 0;
        var hardLinked = 0;
        var copied = 0;
        var dataRoot = Path.Combine(partial, "data");
        Directory.CreateDirectory(dataRoot);

        var directories = Directory.GetDirectories(_dataDirectory, "*", SearchOption.AllDirectories)
            .Where(d => !IsExcluded(d))
            .OrderBy(d => d, StringComparer.Ordinal)
            .ToArray();

        foreach (var directory in directories)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var relative = Path.GetRelativePath(_dataDirectory, directory);
            Directory.CreateDirectory(Path.Combine(dataRoot, relative));
        }

        var allFiles = Directory.GetFiles(_dataDirectory, "*", SearchOption.AllDirectories)
            .Where(f => !IsExcluded(f))
            .OrderBy(f => f, StringComparer.Ordinal)
            .ToArray();

        long totalBytes = 0;
        foreach (var file in allFiles)
        {
            try { totalBytes += new FileInfo(file).Length; }
            catch { /* unreadable */ }
        }

        _progress?.Invoke(new BackupProgress("mirroring", 0, allFiles.Length, 0, totalBytes));

        long processedBytes = 0;
        foreach (var file in allFiles)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var relative = Path.GetRelativePath(_dataDirectory, file);
            var target = Path.Combine(dataRoot, relative);
            Directory.CreateDirectory(Path.GetDirectoryName(target)!);

            if (!BackupArchive.CanRead(file))
            {
                warnings.Add($"Data file not readable: {file}");
                continue;
            }

            long length = 0;
            try { length = new FileInfo(file).Length; }
            catch { /* unreadable */ }

            if (IsContentAddressed(relative))
            {
                // Already present in this (resumed) snapshot?
                if (File.Exists(target) && new FileInfo(target).Length == length)
                {
                    files++;
                    processedBytes += length;
                    continue;
                }

                // Hardlink from the previous snapshot when the blob is unchanged.
                if (previous is not null)
                {
                    var previousFile = Path.Combine(previous, "data", relative);
                    if (File.Exists(previousFile) && new FileInfo(previousFile).Length == length
                        && _hardLinker.TryCreate(previousFile, target))
                    {
                        hardLinked++;
                        files++;
                        processedBytes += length;
                    }
                    else
                    {
                        File.Copy(file, target, overwrite: true);
                        copied++;
                        files++;
                        processedBytes += length;
                    }
                }
                else
                {
                    File.Copy(file, target, overwrite: true);
                    copied++;
                    files++;
                    processedBytes += length;
                }
            }
            else
            {
                // Non-content-addressed files (keys, caches, chat uploads) are always copied.
                File.Copy(file, target, overwrite: true);
                copied++;
                files++;
                processedBytes += length;
            }

            if (files % 25 == 0)
                _progress?.Invoke(new BackupProgress("mirroring", files, allFiles.Length, processedBytes, totalBytes));
        }

        // Count/create empty directories so a restore keeps template folders.
        foreach (var directory in directories)
        {
            var relative = Path.GetRelativePath(_dataDirectory, directory);
            var target = Path.Combine(dataRoot, relative);
            if (!Directory.EnumerateFileSystemEntries(directory).Any())
            {
                Directory.CreateDirectory(target);
                emptyDirectories++;
            }
        }

        _progress?.Invoke(new BackupProgress("finalizing", files, allFiles.Length, processedBytes, totalBytes));

        if (!dumpOk)
            warnings.Add("Database dump was not created.");

        var info = new BackupInfo
        {
            FormatVersion = BackupArchive.CurrentFormatVersion,
            Mode = "mirror",
            CreatedUtc = DateTime.UtcNow.ToString("O"),
            CliVersion = BackupArchive.GetCliVersion(),
            DatabaseDumpIncluded = dumpOk,
            StorageManifestIncluded = false,
            DataDirectory = _dataDirectory,
            ConfigDirectory = _configDirectory,
            ServiceUser = _serviceUser,
            SystemEntries = systemEntries,
            DataFileCount = files,
            EmptyDirectoryCount = emptyDirectories,
            TotalEntryCount = files + emptyDirectories + systemEntries.Count + (dumpOk ? 1 : 0) + 1,
            PreviousSnapshot = previous is null ? null : Path.GetFileName(previous),
            HardLinkedFiles = hardLinked,
            CopiedFiles = copied,
            Warnings = warnings,
        };

        // Write metadata last: its presence marks the snapshot as complete.
        var infoPath = Path.Combine(partial, BackupArchive.InfoEntryName);
        var infoTemp = infoPath + ".tmp";
        await File.WriteAllTextAsync(infoTemp, BackupArchive.SerializeInfo(info), cancellationToken);
        File.Move(infoTemp, infoPath, overwrite: true);

        var finalPath = partial[..^".partial".Length];
        if (Directory.Exists(finalPath))
            finalPath += "-2";

        Directory.Move(partial, finalPath);

        return new MirrorResult(finalPath, files, emptyDirectories, hardLinked, copied,
            previous is null ? null : Path.GetFileName(previous));
    }

    private bool IsExcluded(string path)
    {
        var comparison = OperatingSystem.IsWindows()
            ? StringComparison.OrdinalIgnoreCase
            : StringComparison.Ordinal;

        var full = Path.GetFullPath(path);
        foreach (var exclude in _excludePaths)
        {
            var exclusion = Path.GetFullPath(exclude);
            if (full.Equals(exclusion, comparison)
                || full.StartsWith(exclusion + Path.DirectorySeparatorChar, comparison))
                return true;
        }

        return false;
    }

    /// <summary>
    /// True only for <c>storage/files/xx/yy/&lt;sha256&gt;</c> and
    /// <c>storage/chunks/xx/yy/&lt;sha256&gt;</c>: those blobs are immutable and content-addressed,
    /// so a hardlink is always safe.
    /// </summary>
    internal static bool IsContentAddressed(string relativePath)
    {
        var normalized = relativePath.Replace('\\', '/');
        if (!normalized.StartsWith("storage/files/", StringComparison.Ordinal)
            && !normalized.StartsWith("storage/chunks/", StringComparison.Ordinal))
            return false;

        var fileName = Path.GetFileName(normalized);
        return ContentHashName.IsMatch(fileName);
    }

    private string? FindNewestPartial()
    {
        if (!Directory.Exists(_targetRoot))
            return null;

        return Directory.GetDirectories(_targetRoot, "*.partial")
            .OrderByDescending(d => Path.GetFileName(d), StringComparer.Ordinal)
            .FirstOrDefault();
    }

    private string? FindLatestCompleted(string partialPath)
    {
        foreach (var directory in Directory.GetDirectories(_targetRoot)
                     .Where(d => !d.Equals(partialPath, StringComparison.Ordinal)
                                 && !d.EndsWith(".partial", StringComparison.OrdinalIgnoreCase))
                     .OrderByDescending(d => Path.GetFileName(d), StringComparer.Ordinal))
        {
            var infoPath = Path.Combine(directory, BackupArchive.InfoEntryName);
            if (!File.Exists(infoPath))
                continue;

            try
            {
                var info = BackupArchive.DeserializeInfo(File.ReadAllText(infoPath));
                if (info?.Mode == "mirror")
                    return directory;
            }
            catch
            {
                // ignore unreadable snapshots
            }
        }

        return null;
    }
}

/// <summary>One entry exposed by a backup source (zip part or mirror snapshot file).</summary>
internal sealed record BackupEntryRef(string FullName, long Length, Func<Stream> Open);

/// <summary>
/// Read-only view over a backup: a single zip, a split zip set, or an incremental mirror snapshot.
/// </summary>
internal sealed class BackupSource : IDisposable
{
    private readonly List<IDisposable> _disposables;
    private readonly Dictionary<string, BackupEntryRef> _entries;

    private BackupSource(string displayName, IEnumerable<BackupEntryRef> entries, List<IDisposable> disposables)
    {
        DisplayName = displayName;
        _entries = entries.ToDictionary(e => e.FullName, StringComparer.Ordinal);
        _disposables = disposables;
    }

    /// <summary>Human-readable description for the restore plan.</summary>
    internal string DisplayName { get; }

    /// <summary>All entries.</summary>
    internal IReadOnlyCollection<BackupEntryRef> Entries => _entries.Values;

    /// <summary>Finds an entry by its archive name.</summary>
    internal BackupEntryRef? GetEntry(string name)
        => _entries.TryGetValue(name, out var entry) ? entry : null;

    /// <inheritdoc />
    public void Dispose()
    {
        foreach (var disposable in _disposables)
        {
            try { disposable.Dispose(); }
            catch { /* best effort */ }
        }
    }

    /// <summary>Opens the backup at <paramref name="path"/> (file or snapshot directory).</summary>
    internal static bool TryOpen(
        string path, string? snapshotName, bool verifyParts,
        out BackupSource? source, out string? error)
    {
        source = null;
        error = null;

        if (Directory.Exists(path))
            return TryOpenDirectory(path, snapshotName, out source, out error);

        if (File.Exists(path))
            return TryOpenFile(path, verifyParts, out source, out error);

        error = $"Backup not found: {path}";
        return false;
    }

    private static bool TryOpenDirectory(
        string path, string? snapshotName, out BackupSource? source, out string? error)
    {
        source = null;
        error = null;

        string snapshotDirectory;
        if (File.Exists(Path.Combine(path, BackupArchive.InfoEntryName)))
        {
            snapshotDirectory = path;
        }
        else
        {
            var candidates = Directory.GetDirectories(path)
                .Where(d => !d.EndsWith(".partial", StringComparison.OrdinalIgnoreCase)
                            && File.Exists(Path.Combine(d, BackupArchive.InfoEntryName)))
                .OrderByDescending(d => Path.GetFileName(d), StringComparer.Ordinal)
                .ToArray();

            if (snapshotName is not null)
            {
                snapshotDirectory = Path.Combine(path, snapshotName);
                if (!Directory.Exists(snapshotDirectory))
                {
                    error = $"Snapshot '{snapshotName}' not found under {path}.";
                    return false;
                }
            }
            else if (candidates.Length > 0)
            {
                snapshotDirectory = candidates[0];
            }
            else
            {
                error = $"No completed snapshots found under {path}.";
                return false;
            }
        }

        var entries = new List<BackupEntryRef>();
        foreach (var file in Directory.EnumerateFiles(snapshotDirectory, "*", SearchOption.AllDirectories))
        {
            var relative = Path.GetRelativePath(snapshotDirectory, file).Replace('\\', '/');
            var filePath = file;
            var info = new FileInfo(file);
            entries.Add(new BackupEntryRef(relative, info.Length, () => File.OpenRead(filePath)));
        }

        source = new BackupSource(snapshotDirectory, entries, []);
        return true;
    }

    private static bool TryOpenFile(string path, bool verifyParts, out BackupSource? source, out string? error)
    {
        source = null;
        error = null;

        var directory = Path.GetDirectoryName(Path.GetFullPath(path)) ?? ".";
        var fileName = Path.GetFileName(path);

        // A part file implies its set manifest.
        var partMatch = Regex.Match(fileName, @"^(?<base>.+)\.part\d{3}\.zip$", RegexOptions.IgnoreCase);
        if (partMatch.Success)
        {
            var manifestCandidate = Path.Combine(directory, partMatch.Groups["base"].Value + ".backupset.json");
            if (File.Exists(manifestCandidate))
                return TryOpenSet(manifestCandidate, verifyParts, out source, out error);
        }

        if (fileName.EndsWith(".backupset.json", StringComparison.OrdinalIgnoreCase))
            return TryOpenSet(path, verifyParts, out source, out error);

        // Single archive.
        try
        {
            var archive = ZipFile.OpenRead(path);
            var entries = archive.Entries
                .Select(e => new BackupEntryRef(
                    e.FullName,
                    e.Length,
                    () => e.Open()))
                .ToArray();

            source = new BackupSource(path, entries, [archive]);
            return true;
        }
        catch (Exception ex)
        {
            error = $"Could not open '{path}': {ex.Message}";
            return false;
        }
    }

    private static bool TryOpenSet(
        string manifestPath, bool verifyParts, out BackupSource? source, out string? error)
    {
        source = null;
        error = null;

        BackupSetManifest? manifest;
        try
        { manifest = BackupSetManifest.Deserialize(File.ReadAllText(manifestPath)); }
        catch (Exception ex)
        {
            error = $"Could not read backup set manifest '{manifestPath}': {ex.Message}";
            return false;
        }

        if (manifest is null)
        {
            error = $"Backup set manifest '{manifestPath}' is invalid.";
            return false;
        }

        var directory = Path.GetDirectoryName(Path.GetFullPath(manifestPath)) ?? ".";
        var archives = new List<IDisposable>();
        var entries = new List<BackupEntryRef>();

        try
        {
            foreach (var part in manifest.Parts)
            {
                var partPath = Path.Combine(directory, part.Name);
                if (!File.Exists(partPath))
                {
                    error = $"Backup part '{part.Name}' is missing.";
                    return false;
                }

                var size = new FileInfo(partPath).Length;
                if (part.SizeBytes > 0 && size != part.SizeBytes)
                {
                    error = $"Backup part '{part.Name}' has size {size}, expected {part.SizeBytes}.";
                    return false;
                }

                if (verifyParts && !string.IsNullOrWhiteSpace(part.Sha256))
                {
                    using var stream = File.OpenRead(partPath);
                    var hash = Convert.ToHexStringLower(SHA256.HashData(stream));
                    if (!hash.Equals(part.Sha256, StringComparison.OrdinalIgnoreCase))
                    {
                        error = $"Backup part '{part.Name}' failed its SHA-256 check.";
                        return false;
                    }
                }

                var archive = ZipFile.OpenRead(partPath);
                archives.Add(archive);
                foreach (var entry in archive.Entries)
                {
                    entries.Add(new BackupEntryRef(entry.FullName, entry.Length, () => entry.Open()));
                }
            }

            source = new BackupSource(manifestPath, entries, archives);
            return true;
        }
        catch (Exception ex)
        {
            foreach (var archive in archives)
            {
                try { archive.Dispose(); }
                catch { /* best effort */ }
            }

            error = $"Could not open backup set '{manifestPath}': {ex.Message}";
            return false;
        }
    }
}
