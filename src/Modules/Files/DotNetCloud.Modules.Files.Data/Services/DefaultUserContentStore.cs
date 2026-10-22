using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using DotNetCloud.Modules.Files.Models;
using DotNetCloud.Modules.Files.Options;
using DotNetCloud.Modules.Files.Services;
using Microsoft.Extensions.Logging;

namespace DotNetCloud.Modules.Files.Data.Services;

/// <summary>A single entry in the default-content template manifest.</summary>
/// <param name="RelativePath">Template-relative path in original casing.</param>
/// <param name="RelativePathKey">Normalized (<c>/</c>-separated, lower-cased) identity key.</param>
/// <param name="Kind">Folder or file.</param>
/// <param name="Size">File size in bytes (0 for folders).</param>
/// <param name="ContentHash">SHA-256 of the file content (null for folders).</param>
/// <param name="FullPath">Absolute filesystem path.</param>
public sealed record DefaultContentEntry(
    string RelativePath,
    string RelativePathKey,
    DefaultContentItemKind Kind,
    long Size,
    string? ContentHash,
    string FullPath);

/// <summary>Read-only description of the template tree.</summary>
/// <param name="RootPath">Resolved template root.</param>
/// <param name="Entries">Entries ordered by path.</param>
/// <param name="Fingerprint">SHA-256 over the sorted entry list.</param>
/// <param name="FileCount">Number of files.</param>
/// <param name="FolderCount">Number of folders.</param>
/// <param name="TotalBytes">Total file bytes.</param>
public sealed record DefaultContentManifest(
    string RootPath,
    IReadOnlyList<DefaultContentEntry> Entries,
    string Fingerprint,
    int FileCount,
    int FolderCount,
    long TotalBytes)
{
    /// <summary>An empty manifest (feature disabled or template absent).</summary>
    public static DefaultContentManifest Empty(string rootPath) =>
        new(rootPath, [], string.Empty, 0, 0, 0);

    /// <summary>True when the template contains nothing to provision.</summary>
    public bool IsEmpty => Entries.Count == 0;
}

/// <summary>A validation finding for one template entry.</summary>
/// <param name="RelativePath">Entry path.</param>
/// <param name="Code">Stable issue code.</param>
/// <param name="Message">Human-readable description.</param>
/// <param name="Severity">Issue severity.</param>
public sealed record DefaultContentValidationIssue(
    string RelativePath,
    string Code,
    string Message,
    DefaultContentValidationSeverity Severity);

/// <summary>Severity of a template validation finding.</summary>
public enum DefaultContentValidationSeverity
{
    /// <summary>The entry is skipped but the rest of the pass continues.</summary>
    Warning,

    /// <summary>The entry (or the whole template) cannot be provisioned.</summary>
    Error
}

/// <summary>Result of validating the template.</summary>
/// <param name="RootPath">Resolved template root.</param>
/// <param name="RootExists">Whether the root directory exists.</param>
/// <param name="Readable">Whether the root is readable by the service user.</param>
/// <param name="Issues">Findings.</param>
/// <param name="Manifest">Manifest built from the readable entries.</param>
public sealed record DefaultContentValidationResult(
    string RootPath,
    bool RootExists,
    bool Readable,
    IReadOnlyList<DefaultContentValidationIssue> Issues,
    DefaultContentManifest Manifest)
{
    /// <summary>True when no error-severity issue was found.</summary>
    public bool IsValid => Issues.All(i => i.Severity != DefaultContentValidationSeverity.Error);
}

/// <summary>
/// Read-only access to the administrator-managed template directory:
/// path resolution, manifest building, validation, ZIP export, and additive seeding.
/// This type never deletes or rewrites template content.
/// </summary>
public sealed class DefaultUserContentStore
{
    /// <summary>Marker file written once the template is administrator-owned.</summary>
    public const string MarkerFileName = ".dotnetcloud-template";

    /// <summary>Stock folders created on a fresh install.</summary>
    public static readonly IReadOnlyList<string> StockFolders =
        ["Documents", "Photos", "Music", "Templates", "Videos"];

    private static readonly char[] WindowsIllegalChars = ['\\', '/', ':', '*', '?', '"', '<', '>', '|'];

    private static readonly HashSet<string> WindowsReservedNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "CON", "PRN", "AUX", "NUL",
        "COM1", "COM2", "COM3", "COM4", "COM5", "COM6", "COM7", "COM8", "COM9",
        "LPT1", "LPT2", "LPT3", "LPT4", "LPT5", "LPT6", "LPT7", "LPT8", "LPT9"
    };

    private static readonly HashSet<string> HiddenMetadataNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "Thumbs.db", "desktop.ini", ".DS_Store"
    };

    private readonly IDefaultUserContentSettingsProvider? _settings;
    private readonly ILogger<DefaultUserContentStore> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="DefaultUserContentStore"/> class.
    /// </summary>
    /// <param name="logger">Logger instance.</param>
    /// <param name="settings">
    /// Optional settings provider. When omitted (CLI / tests) the configuration baseline passed to
    /// each method is used.
    /// </param>
    public DefaultUserContentStore(
        ILogger<DefaultUserContentStore> logger,
        IDefaultUserContentSettingsProvider? settings = null)
    {
        _logger = logger;
        _settings = settings;
    }

    /// <summary>Resolves the effective template root for the given options.</summary>
    /// <param name="options">Effective options.</param>
    /// <returns>Absolute template root path.</returns>
    public static string ResolveRootPath(DefaultUserContentOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        if (!string.IsNullOrWhiteSpace(options.RootPath))
            return Path.GetFullPath(options.RootPath);

        var dataDir = Environment.GetEnvironmentVariable("DOTNETCLOUD_DATA_DIR");
        if (!string.IsNullOrWhiteSpace(dataDir))
            return Path.GetFullPath(Path.Combine(dataDir, DefaultUserContentOptions.DirectoryName));

        // Development fallback: the process content root (dotnet run).
        return Path.GetFullPath(Path.Combine(Environment.CurrentDirectory, DefaultUserContentOptions.DirectoryName));
    }

    /// <summary>Resolves the root using the effective (overlaid) options.</summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    public async Task<string> ResolveRootPathAsync(CancellationToken cancellationToken = default)
        => ResolveRootPath(await GetOptionsAsync(cancellationToken).ConfigureAwait(false));

    /// <summary>Normalizes a relative path into the provider-neutral tracking key.</summary>
    /// <param name="relativePath">Path relative to the template root.</param>
    public static string ToKey(string relativePath) =>
        relativePath.Replace('\\', '/').Trim('/').ToLowerInvariant();

    /// <summary>Returns whether a template entry name must be skipped by default.</summary>
    /// <param name="name">File or directory name.</param>
    public static bool IsHiddenOrMetadata(string name) =>
        name.StartsWith('.') || HiddenMetadataNames.Contains(name) || name.EndsWith(".tmp", StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Returns a reason when <paramref name="name"/> cannot be synced to Windows clients, otherwise null.
    /// </summary>
    /// <param name="name">File or directory name.</param>
    public static string? GetIllegalNameReason(string name)
    {
        if (name.Length == 0)
            return "Empty name.";

        if (name.IndexOfAny(WindowsIllegalChars) >= 0)
            return "Name contains a character that is illegal on Windows.";

        if (name.EndsWith('.') || name.EndsWith(' '))
            return "Name ends with a dot or space (not syncable to Windows).";

        var stem = Path.GetFileNameWithoutExtension(name);
        if (WindowsReservedNames.Contains(name) || WindowsReservedNames.Contains(stem))
            return "Name is reserved on Windows.";

        return null;
    }

    /// <summary>
    /// Builds and validates the manifest for the template.
    /// Validation findings are returned alongside the provisionable manifest.
    /// </summary>
    /// <param name="options">Effective options.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public async Task<DefaultContentValidationResult> ValidateAsync(
        DefaultUserContentOptions options,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(options);

        var root = ResolveRootPath(options);
        var issues = new List<DefaultContentValidationIssue>();
        var entries = new List<DefaultContentEntry>();

        if (!Directory.Exists(root))
        {
            issues.Add(new DefaultContentValidationIssue(
                string.Empty, "root-missing", $"Template directory '{root}' does not exist.",
                DefaultContentValidationSeverity.Warning));
            return new DefaultContentValidationResult(root, false, false, issues, DefaultContentManifest.Empty(root));
        }

        var readable = true;
        try
        {
            using var probe = Directory.EnumerateFileSystemEntries(root).GetEnumerator();
            probe.MoveNext();
        }
        catch (Exception ex) when (ex is UnauthorizedAccessException or IOException)
        {
            readable = false;
            issues.Add(new DefaultContentValidationIssue(
                string.Empty, "root-unreadable", $"Template directory '{root}' is not readable: {ex.Message}",
                DefaultContentValidationSeverity.Error));
            return new DefaultContentValidationResult(root, true, false, issues, DefaultContentManifest.Empty(root));
        }

        var fileCount = 0;
        var totalBytes = 0L;
        var visited = new HashSet<string>(StringComparer.Ordinal);
        var state = new WalkState();

        await WalkAsync(root, root, string.Empty, 0, options, issues, entries,
            state, visited, cancellationToken).ConfigureAwait(false);

        fileCount = state.FileCount;
        totalBytes = state.TotalBytes;

        entries.Sort((a, b) => string.CompareOrdinal(a.RelativePathKey, b.RelativePathKey));
        var fingerprint = ComputeFingerprint(entries);

        var manifest = new DefaultContentManifest(
            root,
            entries,
            fingerprint,
            fileCount,
            entries.Count(e => e.Kind == DefaultContentItemKind.Folder),
            totalBytes);

        return new DefaultContentValidationResult(root, true, readable, issues, manifest);
    }

    /// <summary>Builds the manifest without the (potentially expensive) full issue list.</summary>
    /// <param name="options">Effective options.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public async Task<DefaultContentManifest> BuildManifestAsync(
        DefaultUserContentOptions options,
        CancellationToken cancellationToken = default)
        => (await ValidateAsync(options, cancellationToken).ConfigureAwait(false)).Manifest;

    /// <summary>
    /// Writes a ZIP of the template root to <paramref name="destination"/>.
    /// Entries are relative to the root (no wrapper directory) and contain no user copies.
    /// </summary>
    /// <param name="options">Effective options.</param>
    /// <param name="destination">Writable destination stream.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public async Task ExportAsync(
        DefaultUserContentOptions options,
        Stream destination,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(destination);

        var manifest = await BuildManifestAsync(options, cancellationToken).ConfigureAwait(false);

        using var archive = new ZipArchive(destination, ZipArchiveMode.Create, leaveOpen: true);

        foreach (var entry in manifest.Entries)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (entry.Kind == DefaultContentItemKind.Folder)
            {
                archive.CreateEntry(entry.RelativePath.Replace('\\', '/') + "/");
                continue;
            }

            var zipEntry = archive.CreateEntry(entry.RelativePath.Replace('\\', '/'), CompressionLevel.Optimal);
            await using var source = new FileStream(entry.FullPath, FileMode.Open, FileAccess.Read, System.IO.FileShare.Read,
                bufferSize: 81920, useAsync: true);
            await using var target = zipEntry.Open();
            await source.CopyToAsync(target, cancellationToken).ConfigureAwait(false);
        }

        _logger.LogInformation(
            "Exported default-content template from {Root}: {Files} file(s), {Bytes} byte(s).",
            manifest.RootPath, manifest.FileCount, manifest.TotalBytes);
    }

    /// <summary>
    /// Adds the stock folder layout to an empty template and writes the ownership marker.
    /// Additive only — existing entries are never modified.
    /// </summary>
    /// <param name="options">Effective options.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The number of folders created.</returns>
    public Task<int> SeedDefaultFoldersAsync(
        DefaultUserContentOptions options,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(options);
        cancellationToken.ThrowIfCancellationRequested();

        var root = ResolveRootPath(options);
        Directory.CreateDirectory(root);

        var created = 0;
        foreach (var folder in StockFolders)
        {
            var path = Path.Combine(root, folder);
            if (!Directory.Exists(path))
            {
                Directory.CreateDirectory(path);
                created++;
            }
        }

        EnsureMarker(root);
        return Task.FromResult(created);
    }

    /// <summary>Writes the ownership marker if it does not already exist.</summary>
    /// <param name="root">Template root.</param>
    public static void EnsureMarker(string root)
    {
        var marker = Path.Combine(root, MarkerFileName);
        if (File.Exists(marker))
            return;

        Directory.CreateDirectory(root);
        File.WriteAllText(marker, $"schema=1\nseeded={DateTime.UtcNow:O}\n");
    }

    /// <summary>Computes the SHA-256 of a file's content.</summary>
    /// <param name="path">File path.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public static async Task<string> ComputeFileHashAsync(string path, CancellationToken cancellationToken = default)
    {
        await using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, System.IO.FileShare.Read,
            bufferSize: 81920, useAsync: true);
        return await ContentHasher.ComputeHashAsync(stream, cancellationToken).ConfigureAwait(false);
    }

    private async Task<DefaultUserContentOptions> GetOptionsAsync(CancellationToken cancellationToken)
    {
        if (_settings is null)
            return new DefaultUserContentOptions();

        return await _settings.GetAsync(cancellationToken).ConfigureAwait(false);
    }

    private async Task WalkAsync(
        string root,
        string directory,
        string relativeDirectory,
        int depth,
        DefaultUserContentOptions options,
        List<DefaultContentValidationIssue> issues,
        List<DefaultContentEntry> entries,
        WalkState state,
        HashSet<string> visited,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (!visited.Add(directory))
        {
            issues.Add(new DefaultContentValidationIssue(relativeDirectory, "cycle",
                "Directory cycle detected (symlink loop); skipped.", DefaultContentValidationSeverity.Warning));
            return;
        }

        if (depth > options.MaxDepth)
        {
            issues.Add(new DefaultContentValidationIssue(relativeDirectory, "depth",
                $"Directory depth exceeds the configured maximum of {options.MaxDepth}; skipped.",
                DefaultContentValidationSeverity.Warning));
            return;
        }

        IEnumerable<FileSystemInfo> children;
        try
        {
            children = new DirectoryInfo(directory)
                .EnumerateFileSystemInfos("*", new EnumerationOptions
                {
                    IgnoreInaccessible = false,
                    RecurseSubdirectories = false,
                    ReturnSpecialDirectories = false,
                    AttributesToSkip = 0
                })
                .OrderBy(i => i.Name, StringComparer.OrdinalIgnoreCase);
        }
        catch (Exception ex) when (ex is UnauthorizedAccessException or IOException)
        {
            issues.Add(new DefaultContentValidationIssue(relativeDirectory, "unreadable",
                $"Directory is not readable: {ex.Message}", DefaultContentValidationSeverity.Warning));
            return;
        }

        foreach (var child in children)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var name = child.Name;
            if (options.SkipHiddenFiles && IsHiddenOrMetadata(name))
                continue;

            var illegal = GetIllegalNameReason(name);
            if (illegal is not null)
            {
                issues.Add(new DefaultContentValidationIssue(
                    Combine(relativeDirectory, name), "illegal-name", illegal,
                    DefaultContentValidationSeverity.Warning));
                continue;
            }

            // Never follow symlinks — they could escape the template root.
            if (child.LinkTarget is not null || child.Attributes.HasFlag(FileAttributes.ReparsePoint))
            {
                issues.Add(new DefaultContentValidationIssue(
                    Combine(relativeDirectory, name), "symlink",
                    "Symbolic links are not followed and are skipped.",
                    DefaultContentValidationSeverity.Warning));
                continue;
            }

            var relativePath = Combine(relativeDirectory, name);
            var fullPath = Path.Combine(directory, name);

            // Defence in depth: the resolved path must stay under the template root.
            var normalizedFull = Path.GetFullPath(fullPath);
            if (!IsUnderRoot(root, normalizedFull))
            {
                issues.Add(new DefaultContentValidationIssue(relativePath, "traversal",
                    "Entry resolves outside the template root; skipped.",
                    DefaultContentValidationSeverity.Warning));
                continue;
            }

            if (child is DirectoryInfo)
            {
                entries.Add(new DefaultContentEntry(relativePath, ToKey(relativePath),
                    DefaultContentItemKind.Folder, 0, null, normalizedFull));

                await WalkAsync(root, normalizedFull, relativePath, depth + 1, options, issues, entries,
                    state, visited, cancellationToken).ConfigureAwait(false);
                continue;
            }

            var file = (FileInfo)child;
            if (file.Length > options.MaxFileBytes)
            {
                issues.Add(new DefaultContentValidationIssue(relativePath, "oversize",
                    $"File is {file.Length} bytes, above the per-file cap of {options.MaxFileBytes}.",
                    DefaultContentValidationSeverity.Warning));
                continue;
            }

            if (state.FileCount >= options.MaxFiles)
            {
                issues.Add(new DefaultContentValidationIssue(relativePath, "max-files",
                    $"Template file cap of {options.MaxFiles} reached; remaining files are skipped.",
                    DefaultContentValidationSeverity.Warning));
                continue;
            }

            if (state.TotalBytes + file.Length > options.MaxTotalBytes)
            {
                issues.Add(new DefaultContentValidationIssue(relativePath, "max-total-bytes",
                    $"Template total-size cap of {options.MaxTotalBytes} reached; remaining files are skipped.",
                    DefaultContentValidationSeverity.Warning));
                continue;
            }

            string hash;
            try
            {
                hash = await ComputeFileHashAsync(normalizedFull, cancellationToken).ConfigureAwait(false);
            }
            catch (Exception ex) when (ex is UnauthorizedAccessException or IOException)
            {
                issues.Add(new DefaultContentValidationIssue(relativePath, "unreadable",
                    $"File is not readable: {ex.Message}", DefaultContentValidationSeverity.Warning));
                continue;
            }

            entries.Add(new DefaultContentEntry(relativePath, ToKey(relativePath),
                DefaultContentItemKind.File, file.Length, hash, normalizedFull));
            state.FileCount++;
            state.TotalBytes += file.Length;
        }
    }

    private sealed class WalkState
    {
        public int FileCount { get; set; }

        public long TotalBytes { get; set; }
    }

    private static bool IsUnderRoot(string root, string candidate)
    {
        var normalizedRoot = Path.GetFullPath(root)
            .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
            + Path.DirectorySeparatorChar;

        return candidate.StartsWith(normalizedRoot, StringComparison.Ordinal)
            || string.Equals(
                candidate.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar),
                normalizedRoot.TrimEnd(Path.DirectorySeparatorChar),
                StringComparison.Ordinal);
    }

    private static string Combine(string directory, string name) =>
        string.IsNullOrEmpty(directory) ? name : $"{directory}/{name}";

    private static string ComputeFingerprint(IReadOnlyList<DefaultContentEntry> entries)
    {
        var builder = new StringBuilder();
        foreach (var entry in entries)
        {
            builder.Append(entry.Kind).Append('|')
                .Append(entry.RelativePathKey).Append('|')
                .Append(entry.ContentHash ?? "-").Append('|')
                .Append(entry.Size)
                .Append('\n');
        }

        return Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(builder.ToString())));
    }
}
