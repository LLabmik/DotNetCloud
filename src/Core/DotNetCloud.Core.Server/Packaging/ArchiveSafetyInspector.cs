using System.IO.Compression;

namespace DotNetCloud.ModulePackaging;

/// <summary>
/// One archive entry, reduced to the facts the safety checks need.
/// </summary>
/// <param name="FullName">The entry name exactly as stored in the archive.</param>
/// <param name="Length">The uncompressed size in bytes.</param>
/// <param name="CompressedLength">The compressed size in bytes.</param>
/// <param name="IsDirectory">Whether the entry is a directory marker.</param>
/// <param name="IsSymlink">Whether the entry is a symbolic link (unix mode <c>0xA000</c> or a reparse point).</param>
public sealed record PackageEntryInfo(
    string FullName,
    long Length,
    long CompressedLength,
    bool IsDirectory = false,
    bool IsSymlink = false);

/// <summary>
/// Rejects archive structures that are unsafe to expand, independently of the signature (F13).
/// </summary>
/// <remarks>
/// <para>
/// A signature proves who produced the archive; it does not prove the archive is safe. Every
/// package is inspected before extraction, and extraction re-checks each resolved path against the
/// destination root.
/// </para>
/// <para>
/// Rejected shapes: absolute paths, `..` traversal, backslashes, control characters, empty
/// segments, over-long paths, Windows reserved device names, alternate-data-stream separators,
/// trailing dots/spaces, duplicate entries, case-only collisions, symbolic links, oversized entries,
/// oversized totals, too many entries, and zip-bomb compression ratios.
/// </para>
/// </remarks>
public static class ArchiveSafetyInspector
{
    private static readonly string[] ReservedDeviceNames =
    [
        "CON", "PRN", "AUX", "NUL",
        "COM1", "COM2", "COM3", "COM4", "COM5", "COM6", "COM7", "COM8", "COM9",
        "LPT1", "LPT2", "LPT3", "LPT4", "LPT5", "LPT6", "LPT7", "LPT8", "LPT9",
    ];

    /// <summary>
    /// Builds entry descriptors from an open archive.
    /// </summary>
    /// <param name="archive">The archive to read.</param>
    /// <returns>One descriptor per entry.</returns>
    public static IReadOnlyList<PackageEntryInfo> Describe(ZipArchive archive)
    {
        ArgumentNullException.ThrowIfNull(archive);

        var entries = new List<PackageEntryInfo>(archive.Entries.Count);

        foreach (var entry in archive.Entries)
        {
            entries.Add(new PackageEntryInfo(
                entry.FullName,
                entry.Length,
                entry.CompressedLength,
                IsDirectory: entry.FullName.EndsWith('/'),
                IsSymlink: IsSymlink(entry)));
        }

        return entries;
    }

    /// <summary>
    /// Inspects entry descriptors and returns every issue found.
    /// </summary>
    /// <param name="entries">The entries to inspect.</param>
    /// <returns>An empty list when the archive is safe to expand.</returns>
    public static IReadOnlyList<PackageIssue> Inspect(IReadOnlyList<PackageEntryInfo> entries)
    {
        ArgumentNullException.ThrowIfNull(entries);

        var issues = new List<PackageIssue>();

        if (entries.Count > ModulePackageLimits.MaxEntries)
        {
            issues.Add(new PackageIssue(
                PackageIssueCodes.EntryCountExceeded,
                $"Archive has {entries.Count} entries; the limit is {ModulePackageLimits.MaxEntries}."));
        }

        var exactNames = new HashSet<string>(StringComparer.Ordinal);
        var caseInsensitiveNames = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var uncompressedTotal = 0L;

        foreach (var entry in entries)
        {
            ValidateName(entry, issues);

            if (!entry.IsDirectory)
            {
                uncompressedTotal += entry.Length;

                if (entry.IsSymlink)
                {
                    issues.Add(new PackageIssue(
                        PackageIssueCodes.SymlinkEntry,
                        "Symbolic links are not allowed in a package.",
                        entry.FullName));
                }

                if (entry.Length > ModulePackageLimits.MaxEntryBytes)
                {
                    issues.Add(new PackageIssue(
                        PackageIssueCodes.EntryTooLarge,
                        $"Entry is {entry.Length} bytes; the per-entry limit is {ModulePackageLimits.MaxEntryBytes}.",
                        entry.FullName));
                }

                if (entry.Length > ModulePackageLimits.RatioFloorBytes
                    && entry.CompressedLength > 0
                    && entry.Length / entry.CompressedLength > ModulePackageLimits.MaxCompressionRatio)
                {
                    issues.Add(new PackageIssue(
                        PackageIssueCodes.CompressionRatioExceeded,
                        $"Entry compresses {entry.Length / Math.Max(1, entry.CompressedLength)}:1; " +
                        $"the limit is {ModulePackageLimits.MaxCompressionRatio}:1.",
                        entry.FullName));
                }
            }

            var comparisonName = entry.FullName.TrimEnd('/');

            if (!exactNames.Add(comparisonName))
            {
                issues.Add(new PackageIssue(
                    PackageIssueCodes.DuplicateEntry,
                    "The same entry name appears more than once.",
                    entry.FullName));
            }
            else if (caseInsensitiveNames.TryGetValue(comparisonName, out var firstSpelling))
            {
                issues.Add(new PackageIssue(
                    PackageIssueCodes.CaseCollision,
                    $"Collides case-insensitively with '{firstSpelling}'.",
                    entry.FullName));
            }
            else
            {
                caseInsensitiveNames[comparisonName] = entry.FullName;
            }
        }

        if (uncompressedTotal > ModulePackageLimits.MaxUncompressedBytes)
        {
            issues.Add(new PackageIssue(
                PackageIssueCodes.UncompressedTooLarge,
                $"Archive expands to {uncompressedTotal} bytes; the limit is {ModulePackageLimits.MaxUncompressedBytes}."));
        }

        return issues;
    }

    /// <summary>
    /// Inspects an archive and throws when it is unsafe to expand.
    /// </summary>
    /// <param name="archive">The archive to inspect.</param>
    /// <exception cref="ModulePackageException">The archive contains unsafe entries.</exception>
    public static void EnsureSafe(ZipArchive archive)
    {
        var issues = Inspect(Describe(archive));
        if (issues.Count > 0)
        {
            throw new ModulePackageException(
                $"The package archive contains {issues.Count} unsafe entr{(issues.Count == 1 ? "y" : "ies")}.",
                issues);
        }
    }

    /// <summary>
    /// Detects a symbolic link from the entry's external attributes.
    /// </summary>
    /// <param name="entry">The archive entry.</param>
    /// <returns><see langword="true"/> for unix symlinks and Windows reparse points.</returns>
    public static bool IsSymlink(ZipArchiveEntry entry)
    {
        ArgumentNullException.ThrowIfNull(entry);

        // Windows reparse point (0x400) — used by some archivers to smuggle links on NTFS.
        if ((entry.ExternalAttributes & 0x400) != 0)
        {
            return true;
        }

        // Unix mode lives in the high 16 bits; S_IFLNK is 0xA000.
        var unixMode = (entry.ExternalAttributes >> 16) & 0xF000;
        return unixMode == 0xA000;
    }

    private static void ValidateName(PackageEntryInfo entry, List<PackageIssue> issues)
    {
        var name = entry.FullName;

        if (string.IsNullOrWhiteSpace(name))
        {
            issues.Add(new PackageIssue(PackageIssueCodes.InvalidEntryName, "Entry name is empty."));
            return;
        }

        foreach (var character in name)
        {
            if (char.IsControl(character))
            {
                issues.Add(new PackageIssue(
                    PackageIssueCodes.InvalidEntryName,
                    "Entry name contains a control character.",
                    name));
                return;
            }
        }

        if (name.Contains('\\'))
        {
            issues.Add(new PackageIssue(
                PackageIssueCodes.InvalidEntryName,
                "Entry names must use '/' separators, never '\\'.",
                name));
            return;
        }

        if (name.StartsWith('/') || (name.Length >= 2 && char.IsAsciiLetter(name[0]) && name[1] == ':'))
        {
            issues.Add(new PackageIssue(PackageIssueCodes.AbsolutePath, "Entry name is absolute.", name));
            return;
        }

        if (name.Length > ModulePackageLimits.MaxPathLength)
        {
            issues.Add(new PackageIssue(
                PackageIssueCodes.PathTooLong,
                $"Entry path is {name.Length} characters; the limit is {ModulePackageLimits.MaxPathLength}.",
                name));
            return;
        }

        var trimmed = name.TrimEnd('/');
        var segments = trimmed.Split('/');

        if (segments.Length > ModulePackageLimits.MaxPathSegments)
        {
            issues.Add(new PackageIssue(
                PackageIssueCodes.PathTooLong,
                $"Entry path has {segments.Length} segments; the limit is {ModulePackageLimits.MaxPathSegments}.",
                name));
            return;
        }

        foreach (var segment in segments)
        {
            if (segment.Length == 0 || segment == ".")
            {
                issues.Add(new PackageIssue(
                    PackageIssueCodes.InvalidEntryName,
                    "Entry path contains an empty or '.' segment.",
                    name));
                return;
            }

            if (segment == "..")
            {
                issues.Add(new PackageIssue(
                    PackageIssueCodes.PathTraversal,
                    "Entry path contains a '..' segment.",
                    name));
                return;
            }

            if (segment.Contains(':'))
            {
                issues.Add(new PackageIssue(
                    PackageIssueCodes.AlternateDataStream,
                    "Entry path contains ':' (Windows alternate data stream).",
                    name));
                return;
            }

            if (segment.EndsWith('.') || segment.EndsWith(' '))
            {
                issues.Add(new PackageIssue(
                    PackageIssueCodes.AlternateDataStream,
                    "Entry path segment ends with a dot or a space (Windows strips it, causing collisions).",
                    name));
                return;
            }

            var stem = segment;
            var extensionIndex = stem.IndexOf('.', StringComparison.Ordinal);
            if (extensionIndex > 0)
            {
                stem = stem[..extensionIndex];
            }

            if (ReservedDeviceNames.Contains(stem, StringComparer.OrdinalIgnoreCase))
            {
                issues.Add(new PackageIssue(
                    PackageIssueCodes.ReservedName,
                    $"Entry path uses the reserved Windows device name '{stem}'.",
                    name));
                return;
            }
        }
    }
}
