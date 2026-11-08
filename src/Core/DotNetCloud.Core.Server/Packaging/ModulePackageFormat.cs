namespace DotNetCloud.ModulePackaging;

/// <summary>
/// The `.dncpkg` layout and the resource limits applied to it.
/// </summary>
/// <remarks>
/// A package is a deterministic ZIP: entries sorted ordinal, every timestamp fixed, no directory
/// entries, no extra fields. Two packs of the same inputs must be byte-identical so a signature can
/// be reproduced. The signature is detached (`&lt;package&gt;.sig`) because a file cannot cover its
/// own bytes.
/// </remarks>
public static class ModulePackageFormat
{
    /// <summary>Package file extension.</summary>
    public const string Extension = ".dncpkg";

    /// <summary>Detached signature file extension.</summary>
    public const string SignatureExtension = ".sig";

    /// <summary>The manifest entry name (v2 manifest).</summary>
    public const string ManifestEntryName = "manifest.json";

    /// <summary>Required license entry name.</summary>
    public const string LicenseEntryName = "LICENSE";

    /// <summary>Required readme entry name.</summary>
    public const string ReadmeEntryName = "README.md";

    /// <summary>Required changelog entry name.</summary>
    public const string ChangelogEntryName = "CHANGELOG.md";

    /// <summary>Optional icon entry name.</summary>
    public const string IconEntryName = "icon.png";

    /// <summary>Directory that holds the per-RID publish output.</summary>
    public const string FilesDirectoryName = "files";

    /// <summary>Entries that every package must contain.</summary>
    public static readonly string[] RequiredEntryNames =
    [
        ManifestEntryName,
        LicenseEntryName,
        ReadmeEntryName,
        ChangelogEntryName,
    ];

    /// <summary>
    /// The timestamp written to every entry. Zip timestamps have a two-second resolution and no
    /// time zone; a fixed value is what makes the archive deterministic.
    /// </summary>
    public static readonly DateTimeOffset EntryTimestamp = new(1980, 1, 1, 0, 0, 0, TimeSpan.Zero);

    /// <summary>Builds the canonical package file name: <c>&lt;id&gt;-&lt;version&gt;.dncpkg</c>.</summary>
    /// <param name="moduleId">The module id.</param>
    /// <param name="version">The module version.</param>
    /// <returns>The file name (no directory).</returns>
    public static string BuildFileName(string moduleId, string version)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(moduleId);
        ArgumentException.ThrowIfNullOrWhiteSpace(version);

        return $"{moduleId}-{version}{Extension}";
    }

    /// <summary>
    /// Parses <c>&lt;id&gt;-&lt;version&gt;.dncpkg</c>.
    /// </summary>
    /// <param name="fileName">The file name (with or without a directory).</param>
    /// <param name="moduleId">The parsed module id.</param>
    /// <param name="version">The parsed version.</param>
    /// <returns><see langword="true"/> when the name follows the convention.</returns>
    /// <remarks>
    /// Module ids may contain hyphens, so the split is the last hyphen whose suffix parses as a
    /// version rather than simply the first one.
    /// </remarks>
    public static bool TryParseFileName(string fileName, out string moduleId, out string version)
    {
        moduleId = string.Empty;
        version = string.Empty;

        if (string.IsNullOrWhiteSpace(fileName))
        {
            return false;
        }

        var name = Path.GetFileName(fileName);
        if (!name.EndsWith(Extension, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        name = name[..^Extension.Length];

        for (var index = name.LastIndexOf('-'); index > 0; index = name.LastIndexOf('-', index - 1))
        {
            var candidateId = name[..index];
            var candidateVersion = name[(index + 1)..];

            if (candidateVersion.Length == 0 || !char.IsAsciiDigit(candidateVersion[0]))
            {
                continue;
            }

            if (DotNetCloud.Core.Server.ModuleLoading.ModuleCompatibility.TryParseVersion(candidateVersion, out _))
            {
                moduleId = candidateId;
                version = candidateVersion;
                return true;
            }
        }

        return false;
    }

    /// <summary>Returns the detached signature path for a package: <c>&lt;package&gt;.sig</c>.</summary>
    /// <param name="packagePath">The package path.</param>
    /// <returns>The signature file path.</returns>
    public static string GetSignaturePath(string packagePath) => packagePath + SignatureExtension;
}

/// <summary>
/// Hard resource limits applied to a package archive (finding F13).
/// </summary>
/// <remarks>
/// Signature verification proves *who* built an archive, not that the archive is safe to expand.
/// These limits are checked before any entry is extracted.
/// </remarks>
public static class ModulePackageLimits
{
    /// <summary>Maximum number of archive entries.</summary>
    public const int MaxEntries = 20_000;

    /// <summary>Maximum size of the package file itself.</summary>
    public const long MaxPackageBytes = 512L * 1024 * 1024;

    /// <summary>Maximum uncompressed size of the whole archive.</summary>
    public const long MaxUncompressedBytes = 2L * 1024 * 1024 * 1024;

    /// <summary>Maximum uncompressed size of a single entry.</summary>
    public const long MaxEntryBytes = 512L * 1024 * 1024;

    /// <summary>Maximum uncompressed:compressed ratio for entries above <see cref="RatioFloorBytes"/>.</summary>
    public const int MaxCompressionRatio = 200;

    /// <summary>Entries smaller than this are exempt from the ratio check (they cannot be bombs).</summary>
    public const long RatioFloorBytes = 1024 * 1024;

    /// <summary>Maximum entry path length in characters.</summary>
    public const int MaxPathLength = 200;

    /// <summary>Maximum number of '/' separated segments in an entry path.</summary>
    public const int MaxPathSegments = 16;

    /// <summary>Maximum size of the embedded manifest.</summary>
    public const long MaxManifestBytes = 256 * 1024;
}

/// <summary>
/// A validation issue found in a package.
/// </summary>
/// <param name="Code">Stable machine-readable code (see <see cref="PackageIssueCodes"/>).</param>
/// <param name="Message">Human-readable explanation.</param>
/// <param name="EntryName">The offending entry, when the issue is entry-specific.</param>
public sealed record PackageIssue(string Code, string Message, string? EntryName = null)
{
    /// <inheritdoc/>
    public override string ToString() =>
        EntryName is null ? $"[{Code}] {Message}" : $"[{Code}] {EntryName}: {Message}";
}

/// <summary>Stable issue codes returned by the packer and verifier.</summary>
public static class PackageIssueCodes
{
    /// <summary>The package file does not exist.</summary>
    public const string PackageNotFound = "PACKAGE_NOT_FOUND";

    /// <summary>The package file exceeds <see cref="ModulePackageLimits.MaxPackageBytes"/>.</summary>
    public const string PackageTooLarge = "PACKAGE_TOO_LARGE";

    /// <summary>The file is not a readable ZIP archive.</summary>
    public const string InvalidZip = "PACKAGE_INVALID_ZIP";

    /// <summary>The archive has too many entries.</summary>
    public const string EntryCountExceeded = "ARCHIVE_ENTRY_COUNT";

    /// <summary>A single entry is too large.</summary>
    public const string EntryTooLarge = "ARCHIVE_ENTRY_TOO_LARGE";

    /// <summary>The archive's uncompressed size is too large.</summary>
    public const string UncompressedTooLarge = "ARCHIVE_UNCOMPRESSED_TOO_LARGE";

    /// <summary>An entry's compression ratio suggests a zip bomb.</summary>
    public const string CompressionRatioExceeded = "ARCHIVE_COMPRESSION_RATIO";

    /// <summary>An entry name is malformed (control characters, backslashes, empty segments).</summary>
    public const string InvalidEntryName = "ARCHIVE_INVALID_NAME";

    /// <summary>An entry name is absolute.</summary>
    public const string AbsolutePath = "ARCHIVE_ABSOLUTE_PATH";

    /// <summary>An entry name escapes the extraction root.</summary>
    public const string PathTraversal = "ARCHIVE_PATH_TRAVERSAL";

    /// <summary>An entry path is too long.</summary>
    public const string PathTooLong = "ARCHIVE_PATH_TOO_LONG";

    /// <summary>An entry is a symbolic link.</summary>
    public const string SymlinkEntry = "ARCHIVE_SYMLINK";

    /// <summary>The same entry name appears twice.</summary>
    public const string DuplicateEntry = "ARCHIVE_DUPLICATE_ENTRY";

    /// <summary>Two entries collide case-insensitively.</summary>
    public const string CaseCollision = "ARCHIVE_CASE_COLLISION";

    /// <summary>An entry uses a Windows reserved device name.</summary>
    public const string ReservedName = "ARCHIVE_RESERVED_NAME";

    /// <summary>An entry name contains an alternate-data-stream separator or a trailing dot/space.</summary>
    public const string AlternateDataStream = "ARCHIVE_ALTERNATE_DATA_STREAM";

    /// <summary>A required entry is missing.</summary>
    public const string MissingEntry = "PACKAGE_ENTRY_MISSING";

    /// <summary>The embedded manifest failed validation.</summary>
    public const string ManifestInvalid = "MANIFEST_INVALID";

    /// <summary>The manifest's id or version does not match the package.</summary>
    public const string ManifestMismatch = "MANIFEST_MISMATCH";

    /// <summary>The package file name does not follow <c>&lt;id&gt;-&lt;version&gt;.dncpkg</c>.</summary>
    public const string FileNameInvalid = "PACKAGE_FILENAME_INVALID";

    /// <summary>The package is unsigned and unsigned packages are not allowed.</summary>
    public const string SignatureMissing = "SIGNATURE_MISSING";

    /// <summary>The signature file is malformed or does not verify.</summary>
    public const string SignatureInvalid = "SIGNATURE_INVALID";

    /// <summary>The signature was made by a key id that is not trusted.</summary>
    public const string SignatureUntrustedKey = "SIGNATURE_UNTRUSTED_KEY";

    /// <summary>The package hash does not match the hash covered by the signature.</summary>
    public const string HashMismatch = "SIGNATURE_HASH_MISMATCH";

    /// <summary>The manifest requires a newer core, or declares an unparsable version.</summary>
    public const string IncompatibleCore = "MANIFEST_INCOMPATIBLE_CORE";

    /// <summary>The package could not be written.</summary>
    public const string PackageWriteFailed = "PACKAGE_WRITE_FAILED";

    /// <summary>A file to be packaged does not exist.</summary>
    public const string SourceFileMissing = "PACKAGE_SOURCE_MISSING";

    /// <summary>The package contains no binaries for the host runtime.</summary>
    public const string NoMatchingRuntime = "PACKAGE_NO_MATCHING_RUNTIME";
}

/// <summary>Thrown when a package cannot be produced or is rejected.</summary>
public sealed class ModulePackageException : Exception
{
    /// <summary>Initializes a new instance of the <see cref="ModulePackageException"/> class.</summary>
    /// <param name="message">The failure description.</param>
    /// <param name="issues">Structured issues, when available.</param>
    public ModulePackageException(string message, IReadOnlyList<PackageIssue>? issues = null)
        : base(message)
    {
        Issues = issues ?? [];
    }

    /// <summary>Gets the structured issues behind the failure.</summary>
    public IReadOnlyList<PackageIssue> Issues { get; }
}
