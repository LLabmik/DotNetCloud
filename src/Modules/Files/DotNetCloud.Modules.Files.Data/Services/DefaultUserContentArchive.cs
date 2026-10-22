using System.IO.Compression;
using DotNetCloud.Modules.Files.Options;
using Microsoft.Extensions.Logging;

namespace DotNetCloud.Modules.Files.Data.Services;

/// <summary>Outcome of a template ZIP import.</summary>
/// <param name="Success">Whether the archive was valid and applied (or would be, for a dry run).</param>
/// <param name="DryRun">Whether nothing was written.</param>
/// <param name="Added">Files added.</param>
/// <param name="Updated">Files replaced (<c>--overwrite</c>).</param>
/// <param name="Skipped">Entries skipped because they already exist.</param>
/// <param name="Removed">Entries removed by a replace import.</param>
/// <param name="Issues">Validation findings.</param>
public sealed record DefaultContentImportResult(
    bool Success,
    bool DryRun,
    int Added,
    int Updated,
    int Skipped,
    int Removed,
    IReadOnlyList<string> Issues);

/// <summary>
/// Validated, staged ZIP import for the default-content template.
/// The archive is untrusted input: entries are checked for traversal, symlinks, bombs,
/// illegal names and caps before anything is written.
/// </summary>
public sealed class DefaultUserContentArchive
{
    private static readonly char[] WindowsIllegalChars = ['\\', '/', ':', '*', '?', '"', '<', '>', '|'];

    private readonly ILogger<DefaultUserContentArchive> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="DefaultUserContentArchive"/> class.
    /// </summary>
    public DefaultUserContentArchive(ILogger<DefaultUserContentArchive> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Imports <paramref name="archivePath"/> into the template root.
    /// </summary>
    /// <param name="archivePath">ZIP file to import.</param>
    /// <param name="options">Effective template options (caps).</param>
    /// <param name="replace">Replace the whole template instead of merging.</param>
    /// <param name="overwrite">With merge, replace files whose path already exists.</param>
    /// <param name="dryRun">Validate and report without writing.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public async Task<DefaultContentImportResult> ImportAsync(
        string archivePath,
        DefaultUserContentOptions options,
        bool replace,
        bool overwrite,
        bool dryRun,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(archivePath);
        ArgumentNullException.ThrowIfNull(options);

        if (!File.Exists(archivePath))
            return new DefaultContentImportResult(false, dryRun, 0, 0, 0, 0, ["Archive not found."]);

        var root = DefaultUserContentStore.ResolveRootPath(options);
        var issues = new List<string>();

        ZipArchive archive;
        try
        {
            archive = ZipFile.OpenRead(archivePath);
        }
        catch (InvalidDataException ex)
        {
            return new DefaultContentImportResult(false, dryRun, 0, 0, 0, 0,
                [$"Archive is not a readable ZIP: {ex.Message}"]);
        }

        using (archive)
        {
            var entries = Validate(archive, options, issues);
            if (issues.Count > 0)
                return new DefaultContentImportResult(false, dryRun, 0, 0, 0, 0, issues);

            if (dryRun)
            {
                _logger.LogInformation(
                    "Dry run: {Files} file(s), {Bytes} byte(s) would be imported from {Archive}.",
                    entries.Count, entries.Sum(e => e.Length), archivePath);

                return new DefaultContentImportResult(true, true, entries.Count, 0, 0, 0, []);
            }

            var staging = Path.Combine(
                Path.GetTempPath(),
                $"default-content-import-{Guid.CreateVersion7():N}");

            try
            {
                Directory.CreateDirectory(staging);

                foreach (var entry in entries)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    await ExtractAsync(entry, staging, cancellationToken).ConfigureAwait(false);
                }

                var (added, updated, skipped, removed) = replace
                    ? ApplyReplace(root, staging)
                    : ApplyMerge(root, staging, overwrite);

                DefaultUserContentStore.EnsureMarker(root);

                _logger.LogInformation(
                    "Imported template from {Archive}: {Added} added, {Updated} updated, {Skipped} skipped, {Removed} removed.",
                    archivePath, added, updated, skipped, removed);

                return new DefaultContentImportResult(true, false, added, updated, skipped, removed, []);
            }
            finally
            {
                TryDeleteDirectory(staging);
            }
        }
    }

    private static List<ZipArchiveEntry> Validate(
        ZipArchive archive,
        DefaultUserContentOptions options,
        List<string> issues)
    {
        var entries = new List<ZipArchiveEntry>();
        var keys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var fileCount = 0;
        long totalBytes = 0;

        foreach (var entry in archive.Entries)
        {
            var name = entry.FullName.Replace('\\', '/');

            if (string.IsNullOrWhiteSpace(name) || name.EndsWith('/'))
                continue; // directory entry

            if (name.StartsWith('/') || name.Contains(':') || entry.FullName.Contains('\\'))
            {
                issues.Add($"Rejected path '{entry.FullName}': absolute paths, drive letters and backslashes are not allowed.");
                continue;
            }

            var segments = name.Split('/', StringSplitOptions.RemoveEmptyEntries);
            if (segments.Length == 0 || segments.Any(s => s == ".." || s == "."))
            {
                issues.Add($"Rejected path '{entry.FullName}': path traversal.");
                continue;
            }

            if (segments.Length > options.MaxDepth)
            {
                issues.Add($"Rejected path '{entry.FullName}': depth exceeds {options.MaxDepth}.");
                continue;
            }

            if (segments.Any(s => s.IndexOfAny(WindowsIllegalChars) >= 0))
            {
                issues.Add($"Rejected path '{entry.FullName}': name is not valid on Windows.");
                continue;
            }

            // Unix symlink: external attributes mode bits 0xA000.
            var unixMode = (entry.ExternalAttributes >> 16) & 0xF000;
            if (unixMode == 0xA000)
            {
                issues.Add($"Rejected path '{entry.FullName}': symbolic links are not allowed.");
                continue;
            }

            if (!keys.Add(name))
            {
                issues.Add($"Rejected duplicate path '{entry.FullName}' (case-insensitive).");
                continue;
            }

            if (entry.Length > options.MaxFileBytes)
            {
                issues.Add($"Rejected '{entry.FullName}': file exceeds the per-file cap of {options.MaxFileBytes} bytes.");
                continue;
            }

            fileCount++;
            totalBytes += entry.Length;

            if (fileCount > options.MaxFiles)
            {
                issues.Add($"Archive exceeds the file cap of {options.MaxFiles}.");
                break;
            }

            if (totalBytes > options.MaxTotalBytes)
            {
                issues.Add($"Archive exceeds the total-size cap of {options.MaxTotalBytes} bytes.");
                break;
            }

            entries.Add(entry);
        }

        return entries;
    }

    private static async Task ExtractAsync(ZipArchiveEntry entry, string stagingRoot, CancellationToken cancellationToken)
    {
        var relative = entry.FullName.Replace('\\', '/').TrimStart('/');
        var destination = Path.GetFullPath(Path.Combine(stagingRoot, relative));
        var stagingFull = Path.GetFullPath(stagingRoot).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;

        if (!destination.StartsWith(stagingFull, StringComparison.Ordinal))
            throw new InvalidDataException($"Entry '{entry.FullName}' escapes the staging directory.");

        Directory.CreateDirectory(Path.GetDirectoryName(destination)!);

        await using var source = entry.Open();
        await using var target = new FileStream(destination, FileMode.CreateNew, FileAccess.Write, FileShare.None,
            bufferSize: 81920, useAsync: true);

        // Size-limited copy: never trust the declared length.
        var buffer = new byte[81920];
        long written = 0;
        int read;
        while ((read = await source.ReadAsync(buffer, cancellationToken).ConfigureAwait(false)) > 0)
        {
            written += read;
            if (written > DefaultUserContentOptionsStatic.MaxAbsoluteFileBytes)
                throw new InvalidDataException($"Entry '{entry.FullName}' is too large.");

            await target.WriteAsync(buffer.AsMemory(0, read), cancellationToken).ConfigureAwait(false);
        }
    }

    private static (int Added, int Updated, int Skipped, int Removed) ApplyMerge(
        string root,
        string staging,
        bool overwrite)
    {
        Directory.CreateDirectory(root);

        int added = 0, updated = 0, skipped = 0;

        foreach (var source in Directory.EnumerateFiles(staging, "*", SearchOption.AllDirectories))
        {
            var relative = Path.GetRelativePath(staging, source);
            var destination = Path.Combine(root, relative);

            Directory.CreateDirectory(Path.GetDirectoryName(destination)!);

            if (File.Exists(destination))
            {
                if (!overwrite)
                {
                    skipped++;
                    continue;
                }

                File.Copy(source, destination, overwrite: true);
                updated++;
            }
            else
            {
                File.Copy(source, destination, overwrite: false);
                added++;
            }
        }

        // Empty directories are part of the template too.
        foreach (var directory in Directory.EnumerateDirectories(staging, "*", SearchOption.AllDirectories))
        {
            var relative = Path.GetRelativePath(staging, directory);
            Directory.CreateDirectory(Path.Combine(root, relative));
        }

        return (added, updated, skipped, 0);
    }

    private static (int Added, int Updated, int Skipped, int Removed) ApplyReplace(string root, string staging)
    {
        var removed = 0;
        if (Directory.Exists(root))
            removed = Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories).Count();

        var backup = $"{root}.bak-{DateTime.UtcNow:yyyyMMddHHmmss}";

        // Never delete: move the current template aside so an operator can recover it.
        if (Directory.Exists(root))
            Directory.Move(root, backup);

        Directory.CreateDirectory(root);

        var added = 0;
        foreach (var source in Directory.EnumerateFiles(staging, "*", SearchOption.AllDirectories))
        {
            var destination = Path.Combine(root, Path.GetRelativePath(staging, source));
            Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
            File.Copy(source, destination, overwrite: false);
            added++;
        }

        foreach (var directory in Directory.EnumerateDirectories(staging, "*", SearchOption.AllDirectories))
        {
            Directory.CreateDirectory(Path.Combine(root, Path.GetRelativePath(staging, directory)));
        }

        return (added, 0, 0, removed);
    }

    private static void TryDeleteDirectory(string path)
    {
        try
        {
            if (Directory.Exists(path))
                Directory.Delete(path, recursive: true);
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }
    }

    private static class DefaultUserContentOptionsStatic
    {
        /// <summary>Absolute upper bound accepted from an archive regardless of configuration.</summary>
        public const long MaxAbsoluteFileBytes = 8L * 1024 * 1024 * 1024;
    }
}
