using System.IO.Compression;
using System.Security.Cryptography;

namespace DotNetCloud.ModulePackaging;

/// <summary>
/// A file to place in the package, with its in-archive entry name.
/// </summary>
/// <param name="EntryName">Entry name using '/' separators.</param>
/// <param name="SourcePath">Path to the file on disk.</param>
public sealed record PackageEntrySource(string EntryName, string SourcePath);

/// <summary>The result of writing a package.</summary>
/// <param name="PackagePath">The full path of the written package.</param>
/// <param name="Sha256">Lower-case hex SHA-256 of the package bytes.</param>
/// <param name="SizeBytes">The package size in bytes.</param>
/// <param name="EntryCount">The number of file entries written.</param>
public sealed record PackageBuildResult(string PackagePath, string Sha256, long SizeBytes, int EntryCount);

/// <summary>
/// Writes deterministic `.dncpkg` archives.
/// </summary>
/// <remarks>
/// Determinism is a signature requirement: the same inputs must produce the same archive bytes, or
/// a signature cannot be reproduced by an independent builder. Entries are sorted ordinal, every
/// timestamp is fixed, directory entries are not stored, and nothing environment-dependent
/// (owner, permissions, access time) is recorded.
/// </remarks>
public static class DeterministicPackageWriter
{
    /// <summary>
    /// Writes a package and returns its hash.
    /// </summary>
    /// <param name="outputPath">Where to write the package.</param>
    /// <param name="entries">The entries to include.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The written package's path, size and SHA-256.</returns>
    /// <exception cref="ModulePackageException">
    /// The entry set is invalid (unsafe names, missing files, duplicate or empty).
    /// </exception>
    public static async Task<PackageBuildResult> WriteAsync(
        string outputPath,
        IReadOnlyList<PackageEntrySource> entries,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(outputPath);
        ArgumentNullException.ThrowIfNull(entries);

        if (entries.Count == 0)
        {
            throw new ModulePackageException("The package has no entries.");
        }

        var invalid = FindInvalidSources(entries);
        if (invalid.Count > 0)
        {
            throw new ModulePackageException("The entry set is not packageable.", invalid);
        }

        var ordered = entries.OrderBy(entry => entry.EntryName, StringComparer.Ordinal).ToList();

        // Safety first: a package the tool writes must pass the checks the installer applies.
        var issues = ArchiveSafetyInspector.Inspect(
            ordered.Select(entry => new PackageEntryInfo(
                entry.EntryName,
                new FileInfo(entry.SourcePath).Length,
                CompressedLength: 0)).ToList());

        if (issues.Count > 0)
        {
            throw new ModulePackageException("The entry set is not packageable.", issues);
        }

        var fullPath = Path.GetFullPath(outputPath);
        Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);

        try
        {
            using (var stream = new FileStream(fullPath, FileMode.Create, FileAccess.Write, FileShare.None))
            using (var archive = new ZipArchive(stream, ZipArchiveMode.Create))
            {
                foreach (var entry in ordered)
                {
                    var zipEntry = archive.CreateEntry(entry.EntryName, CompressionLevel.Optimal);
                    zipEntry.LastWriteTime = ModulePackageFormat.EntryTimestamp;

                    using var source = new FileStream(
                        entry.SourcePath, FileMode.Open, FileAccess.Read, FileShare.Read);
                    using var destination = zipEntry.Open();
                    source.CopyTo(destination);
                }
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            throw new ModulePackageException($"Could not write '{fullPath}': {ex.Message}");
        }

        var size = new FileInfo(fullPath).Length;
        var sha256 = await ComputeSha256Async(fullPath, cancellationToken);

        return new PackageBuildResult(fullPath, sha256, size, ordered.Count);
    }

    /// <summary>
    /// Enumerates a staged directory into package entries, using the directory's relative paths.
    /// </summary>
    /// <param name="rootDirectory">The staging root.</param>
    /// <returns>The entries, sorted ordinal.</returns>
    public static IReadOnlyList<PackageEntrySource> CollectFromDirectory(string rootDirectory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(rootDirectory);

        var root = Path.GetFullPath(rootDirectory);
        if (!Directory.Exists(root))
        {
            throw new ModulePackageException($"Staging directory not found: {root}");
        }

        return Directory
            .GetFiles(root, "*", SearchOption.AllDirectories)
            .Where(file => !IsIgnored(file))
            .Select(file => new PackageEntrySource(
                Path.GetRelativePath(root, file).Replace('\\', '/'),
                file))
            .OrderBy(entry => entry.EntryName, StringComparer.Ordinal)
            .ToList();
    }

    /// <summary>
    /// Computes the lower-case hex SHA-256 of a file, streaming.
    /// </summary>
    /// <param name="path">The file path.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The hex digest.</returns>
    public static async Task<string> ComputeSha256Async(string path, CancellationToken cancellationToken = default)
    {
        await using var stream = new FileStream(
            path, FileMode.Open, FileAccess.Read, FileShare.Read, bufferSize: 81920, useAsync: true);

        var hash = await SHA256.HashDataAsync(stream, cancellationToken);
        return Convert.ToHexStringLower(hash);
    }

    private static List<PackageIssue> FindInvalidSources(IReadOnlyList<PackageEntrySource> entries)
    {
        var issues = new List<PackageIssue>();

        foreach (var entry in entries)
        {
            if (string.IsNullOrWhiteSpace(entry.EntryName))
            {
                issues.Add(new PackageIssue(PackageIssueCodes.InvalidEntryName, "An entry has no name."));
                continue;
            }

            if (!File.Exists(entry.SourcePath))
            {
                issues.Add(new PackageIssue(
                    PackageIssueCodes.SourceFileMissing,
                    "Source file is missing.",
                    entry.EntryName));
            }
        }

        return issues;
    }

    private static bool IsIgnored(string path)
    {
        var name = Path.GetFileName(path);
        return name is ".DS_Store" or "Thumbs.db" or "desktop.ini";
    }
}
