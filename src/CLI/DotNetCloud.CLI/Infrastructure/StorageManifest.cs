using System.Globalization;
using System.Text;
using System.Text.Json;
using DotNetCloud.Modules.Files.Data;
using Microsoft.EntityFrameworkCore;

namespace DotNetCloud.CLI.Infrastructure;

/// <summary>
/// Builds a tab-separated index of every stored content blob, keyed by the database rows that
/// reference it.
/// </summary>
/// <remarks>
/// Stored content is content-addressed and deliberately carries no file extension, so a copy of the
/// storage tree on its own is not self-describing: nothing on disk records which blob belongs to which
/// file, or in which order a chunked file's chunks concatenate. This manifest is that missing index —
/// keep it alongside any backup of the storage directory so the tree can be interpreted (and rebuilt)
/// without a live database.
/// </remarks>
public static class StorageManifest
{
    /// <summary>Names of the data-row columns, in order, tab-separated.</summary>
    public const string ColumnHeader =
        "record\tid\tnodeId\tstoragePath\tbytes\tmimeType\tfileName\tfilePath\townerId\tisChunked\tisDeleted\tsequenceIndex";

    private const string FileRecord = "file";
    private const string ChunkRecord = "blob";

    /// <summary>Totals produced while writing a manifest.</summary>
    /// <param name="FileVersions">Number of <c>file</c> rows written.</param>
    /// <param name="ChunkRows">Number of <c>blob</c> rows written.</param>
    /// <param name="TotalBytes">Sum of every file version's recorded size.</param>
    /// <param name="MissingBlobs">Whole-file versions whose blob is absent from disk.</param>
    /// <param name="TruncatedBlobs">Whole-file versions whose blob exists with a different size.</param>
    public sealed record Result(int FileVersions, int ChunkRows, long TotalBytes, int MissingBlobs, int TruncatedBlobs);

    /// <summary>
    /// Resolves the storage root the same way the server does: <c>Files:Storage:RootPath</c> from the
    /// server configuration file, otherwise the data directory's <c>storage</c> subdirectory.
    /// </summary>
    public static string ResolveStorageRoot()
    {
        var configured = ReadConfiguredStorageRoot();
        if (!string.IsNullOrWhiteSpace(configured))
            return configured;

        var dataDirectory = CliConfiguration.Load().DataDirectory;
        return !string.IsNullOrWhiteSpace(dataDirectory)
            ? Path.Combine(dataDirectory, "storage")
            : Path.Combine(AppContext.BaseDirectory, "storage");
    }

    /// <summary>
    /// Writes the manifest to <paramref name="writer"/> and returns its totals.
    /// </summary>
    /// <param name="db">Files database context.</param>
    /// <param name="storageRoot">Storage root the <c>storagePath</c> values are relative to.</param>
    /// <param name="includeChunks">When <see langword="true"/>, emits the ordered <c>blob</c> rows that
    /// make a chunked file reconstructible.</param>
    /// <param name="writer">Destination writer.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public static async Task<Result> WriteAsync(
        FilesDbContext db,
        string storageRoot,
        bool includeChunks,
        TextWriter writer,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(db);
        ArgumentNullException.ThrowIfNull(writer);

        await WriteHeaderAsync(writer, storageRoot, includeChunks, cancellationToken).ConfigureAwait(false);

        var versions = await db.FileVersions
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Join(
                db.FileNodes.IgnoreQueryFilters().AsNoTracking(),
                v => v.FileNodeId,
                n => n.Id,
                (v, n) => new
                {
                    v.Id,
                    v.FileNodeId,
                    v.StoragePath,
                    v.Size,
                    v.MimeType,
                    v.IsChunked,
                    v.VersionNumber,
                    NodeName = n.Name,
                    NodePath = n.MaterializedPath,
                    n.OwnerId,
                    n.IsDeleted
                })
            .OrderBy(x => x.NodePath)
            .ThenBy(x => x.NodeName)
            .ThenBy(x => x.VersionNumber)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        Dictionary<Guid, List<(int Sequence, string Path, long Size)>> chunksByVersion = [];
        if (includeChunks)
        {
            var chunkRows = await db.FileVersionChunks
                .AsNoTracking()
                .Include(vc => vc.FileChunk)
                .Where(vc => vc.FileChunk != null)
                .Select(vc => new
                {
                    vc.FileVersionId,
                    vc.SequenceIndex,
                    Path = vc.FileChunk!.StoragePath,
                    Size = (long)vc.FileChunk!.Size
                })
                .ToListAsync(cancellationToken)
                .ConfigureAwait(false);

            chunksByVersion = chunkRows
                .GroupBy(c => c.FileVersionId)
                .ToDictionary(
                    g => g.Key,
                    g => g.OrderBy(c => c.SequenceIndex).Select(c => (c.SequenceIndex, c.Path, c.Size)).ToList());
        }

        var fileCount = 0;
        var chunkCount = 0;
        var totalBytes = 0L;
        var missing = 0;
        var truncated = 0;

        foreach (var version in versions)
        {
            fileCount++;
            totalBytes += version.Size;

            if (!version.IsChunked)
            {
                var onDisk = TryGetBlobLength(storageRoot, version.StoragePath);
                if (onDisk is null)
                {
                    missing++;
                }
                else if (version.Size > 0 && onDisk.Value != version.Size)
                {
                    truncated++;
                }
            }

            await WriteRowAsync(
                writer,
                cancellationToken,
                FileRecord,
                version.Id,
                version.FileNodeId,
                version.StoragePath,
                version.Size,
                version.MimeType,
                version.NodeName,
                version.NodePath,
                version.OwnerId,
                version.IsChunked,
                version.IsDeleted,
                null).ConfigureAwait(false);

            if (includeChunks && version.IsChunked && chunksByVersion.TryGetValue(version.Id, out var chunks))
            {
                foreach (var chunk in chunks)
                {
                    chunkCount++;
                    await WriteRowAsync(
                        writer,
                        cancellationToken,
                        ChunkRecord,
                        version.Id,
                        version.FileNodeId,
                        chunk.Path,
                        chunk.Size,
                        null,
                        null,
                        null,
                        null,
                        null,
                        null,
                        chunk.Sequence).ConfigureAwait(false);
                }
            }
        }

        var totals =
            $"#totals\tfileVersions={fileCount}\tchunkRows={chunkCount}\tbytes={totalBytes}" +
            $"\tmissingWholeFileBlobs={missing}\ttruncatedWholeFileBlobs={truncated}";
        await writer.WriteLineAsync(totals.AsMemory(), cancellationToken).ConfigureAwait(false);
        await writer.FlushAsync(cancellationToken).ConfigureAwait(false);

        return new Result(fileCount, chunkCount, totalBytes, missing, truncated);
    }

    private static async Task WriteHeaderAsync(
        TextWriter writer,
        string storageRoot,
        bool includeChunks,
        CancellationToken cancellationToken)
    {
        string[] lines =
        [
            "# DotNetCloud storage manifest",
            $"# generated: {DateTimeOffset.UtcNow:O}",
            $"# storageRoot: {storageRoot}",
            $"# scope: {(includeChunks ? "file versions and their ordered chunk mappings" : "file versions only (chunk rows omitted)")}",
            "# layout: content is content-addressed and stored WITHOUT a file extension.",
            "#   record=file, isChunked=false -> the file's bytes are the whole-file blob at storagePath.",
            "#   record=file, isChunked=true  -> the bytes are the concatenation of that version's 'blob' rows,",
            "#                                   in ascending sequenceIndex order.",
            "# values are tab-separated; tabs and newlines inside a value are replaced with spaces.",
            "#columns\t" + ColumnHeader
        ];

        foreach (var line in lines)
            await writer.WriteLineAsync(line.AsMemory(), cancellationToken).ConfigureAwait(false);
    }

    private static async Task WriteRowAsync(TextWriter writer, CancellationToken cancellationToken, params object?[] values)
    {
        var line = new StringBuilder();
        for (var i = 0; i < values.Length; i++)
        {
            if (i > 0)
                line.Append('\t');
            line.Append(Format(values[i]));
        }

        await writer.WriteLineAsync(line.ToString().AsMemory(), cancellationToken).ConfigureAwait(false);
    }

    private static string Format(object? value) => value switch
    {
        null => string.Empty,
        string text => Sanitize(text),
        bool flag => flag ? "true" : "false",
        IFormattable formattable => formattable.ToString(null, CultureInfo.InvariantCulture),
        _ => Sanitize(value.ToString() ?? string.Empty)
    };

    private static string Sanitize(string value) =>
        value.Replace('\t', ' ').Replace('\r', ' ').Replace('\n', ' ');

    private static long? TryGetBlobLength(string storageRoot, string? storagePath)
    {
        if (string.IsNullOrWhiteSpace(storagePath))
            return null;

        try
        {
            var fullPath = Path.Combine(
                storageRoot, storagePath.Replace('/', Path.DirectorySeparatorChar));
            var info = new FileInfo(fullPath);
            return info.Exists ? info.Length : null;
        }
        catch (Exception ex) when (ex is ArgumentException or NotSupportedException or PathTooLongException)
        {
            return null;
        }
    }

    private static string? ReadConfiguredStorageRoot()
    {
        try
        {
            var configPath = CliConfiguration.GetConfigFilePath();
            if (!File.Exists(configPath))
                return null;

            using var document = JsonDocument.Parse(File.ReadAllText(configPath));
            if (document.RootElement.TryGetProperty("Files", out var files)
                && files.TryGetProperty("Storage", out var storage)
                && storage.TryGetProperty("RootPath", out var rootPath)
                && rootPath.ValueKind == JsonValueKind.String)
            {
                return rootPath.GetString();
            }
        }
        catch (JsonException)
        {
            // Malformed config — fall back to the data-directory default.
        }
        catch (IOException)
        {
            // Unreadable config — fall back to the data-directory default.
        }

        return null;
    }
}
