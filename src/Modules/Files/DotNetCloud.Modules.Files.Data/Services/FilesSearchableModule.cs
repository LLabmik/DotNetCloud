using DotNetCloud.Core.Capabilities;
using DotNetCloud.Core.DTOs.Search;
using DotNetCloud.Modules.Files.Models;
using DotNetCloud.Modules.Files.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System.Text;

namespace DotNetCloud.Modules.Files.Data.Services;

/// <summary>
/// Exposes Files module data for full-text search indexing.
/// Provides file and folder metadata as <see cref="SearchDocument"/> instances.
/// </summary>
public sealed class FilesSearchableModule : ISearchableModule
{
    private readonly FilesDbContext _db;
    private readonly IFileStorageEngine _storageEngine;
    private readonly ILogger<FilesSearchableModule> _logger;
    private const int MaxExtractedCharacters = 100_000;
    private const int MaxExtractedBytes = 200_000;

    /// <summary>Initializes a new instance of the <see cref="FilesSearchableModule"/> class.</summary>
    public FilesSearchableModule(
        FilesDbContext db,
        IFileStorageEngine storageEngine,
        ILogger<FilesSearchableModule> logger)
    {
        _db = db;
        _storageEngine = storageEngine;
        _logger = logger;
    }

    /// <inheritdoc />
    public string ModuleId => "files";

    /// <inheritdoc />
    public IReadOnlyCollection<string> SupportedEntityTypes { get; } = ["FileNode"];

    /// <inheritdoc />
    public async Task<IReadOnlyList<SearchDocument>> GetAllSearchableDocumentsAsync(CancellationToken cancellationToken = default)
    {
        var nodes = await _db.FileNodes
            .Where(n => !n.IsDeleted)
            .ToListAsync(cancellationToken);

        _logger.LogDebug("Retrieved {Count} file nodes for search indexing", nodes.Count);

        var tasks = nodes.Select(node => ToSearchDocumentAsync(node, cancellationToken));
        return await Task.WhenAll(tasks);
    }

    /// <inheritdoc />
    public async Task<SearchDocument?> GetSearchableDocumentAsync(string entityId, CancellationToken cancellationToken = default)
    {
        if (!Guid.TryParse(entityId, out var id))
            return null;

        var node = await _db.FileNodes
            .FirstOrDefaultAsync(n => n.Id == id && !n.IsDeleted, cancellationToken);

        return node is null ? null : await ToSearchDocumentAsync(node, cancellationToken);
    }

    private async Task<SearchDocument> ToSearchDocumentAsync(FileNode node, CancellationToken cancellationToken)
    {
        var content = node.Name;
        var extractedText = await TryExtractTextContentAsync(node, cancellationToken);
        if (!string.IsNullOrWhiteSpace(extractedText))
        {
            content = $"{node.Name}\n{extractedText}";
        }

        var metadata = new Dictionary<string, string>();
        if (!string.IsNullOrEmpty(node.MimeType))
            metadata["MimeType"] = node.MimeType;
        if (node.NodeType == FileNodeType.File)
            metadata["Size"] = node.Size.ToString();
        if (!string.IsNullOrWhiteSpace(node.StoragePath))
            metadata["StoragePath"] = node.StoragePath;
        metadata["NodeType"] = node.NodeType.ToString();
        if (!string.IsNullOrEmpty(node.MaterializedPath))
            metadata["Path"] = node.MaterializedPath;

        return new SearchDocument
        {
            ModuleId = "files",
            EntityId = node.Id.ToString(),
            EntityType = "FileNode",
            Title = node.Name,
            Content = content,
            Summary = node.NodeType == FileNodeType.Folder
                ? $"Folder: {node.Name}"
                : $"File: {node.Name} ({node.MimeType ?? "unknown"})",
            OwnerId = node.OwnerId,
            CreatedAt = new DateTimeOffset(node.CreatedAt, TimeSpan.Zero),
            UpdatedAt = new DateTimeOffset(node.UpdatedAt, TimeSpan.Zero),
            Metadata = metadata
        };
    }

    private async Task<string?> TryExtractTextContentAsync(FileNode node, CancellationToken cancellationToken)
    {
        if (node.NodeType != FileNodeType.File || !IsTextLike(node))
        {
            return null;
        }

        var latestVersion = await _db.FileVersions
            .AsNoTracking()
            .Where(v => v.FileNodeId == node.Id)
            .OrderByDescending(v => v.VersionNumber)
            .Select(v => new { v.Id })
            .FirstOrDefaultAsync(cancellationToken);

        if (latestVersion is null)
        {
            return null;
        }

        var chunks = await _db.FileVersionChunks
            .AsNoTracking()
            .Include(vc => vc.FileChunk)
            .Where(vc => vc.FileVersionId == latestVersion.Id)
            .OrderBy(vc => vc.SequenceIndex)
            .ToListAsync(cancellationToken);

        if (chunks.Count == 0)
        {
            return null;
        }

        using var aggregate = new MemoryStream(capacity: Math.Min(MaxExtractedBytes, 16 * 1024));
        foreach (var vc in chunks)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var chunkPath = vc.FileChunk?.StoragePath;
            if (string.IsNullOrWhiteSpace(chunkPath))
            {
                continue;
            }

            await using var chunkStream = await _storageEngine.OpenReadStreamAsync(chunkPath, cancellationToken);
            if (chunkStream is null)
            {
                continue;
            }

            var remainingBytes = MaxExtractedBytes - (int)aggregate.Length;
            if (remainingBytes <= 0)
            {
                break;
            }

            var byteBuffer = new byte[Math.Min(8192, remainingBytes)];
            while (remainingBytes > 0)
            {
                var read = await chunkStream.ReadAsync(byteBuffer.AsMemory(0, Math.Min(byteBuffer.Length, remainingBytes)), cancellationToken);
                if (read == 0)
                {
                    break;
                }

                await aggregate.WriteAsync(byteBuffer.AsMemory(0, read), cancellationToken);
                remainingBytes -= read;
            }

            if (aggregate.Length >= MaxExtractedBytes)
            {
                break;
            }
        }

        if (aggregate.Length == 0)
        {
            _logger.LogDebug("Search content extraction skipped: no readable chunks found for file {FileId}", node.Id);
            return null;
        }

        aggregate.Position = 0;

        var builder = new StringBuilder(capacity: 4096);
        var charBuffer = new char[4096];
        using var reader = new StreamReader(aggregate, Encoding.UTF8, detectEncodingFromByteOrderMarks: true, leaveOpen: false);

        while (builder.Length < MaxExtractedCharacters)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var remaining = MaxExtractedCharacters - builder.Length;
            var readCount = await reader.ReadAsync(charBuffer.AsMemory(0, Math.Min(charBuffer.Length, remaining)));
            if (readCount == 0)
            {
                break;
            }

            builder.Append(charBuffer, 0, readCount);
        }

        return builder.Length == 0 ? null : builder.ToString();
    }

    private static bool IsTextLike(FileNode node)
    {
        if (!string.IsNullOrWhiteSpace(node.MimeType))
        {
            var mimeType = node.MimeType;
            if (mimeType.StartsWith("text/", StringComparison.OrdinalIgnoreCase))
                return true;

            if (mimeType.Equals("application/json", StringComparison.OrdinalIgnoreCase) ||
                mimeType.Equals("application/xml", StringComparison.OrdinalIgnoreCase) ||
                mimeType.Equals("application/javascript", StringComparison.OrdinalIgnoreCase) ||
                mimeType.Equals("application/x-yaml", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        var extension = Path.GetExtension(node.Name);
        return extension.Equals(".txt", StringComparison.OrdinalIgnoreCase) ||
               extension.Equals(".md", StringComparison.OrdinalIgnoreCase) ||
               extension.Equals(".csv", StringComparison.OrdinalIgnoreCase) ||
               extension.Equals(".log", StringComparison.OrdinalIgnoreCase) ||
               extension.Equals(".json", StringComparison.OrdinalIgnoreCase) ||
               extension.Equals(".xml", StringComparison.OrdinalIgnoreCase) ||
               extension.Equals(".yml", StringComparison.OrdinalIgnoreCase) ||
               extension.Equals(".yaml", StringComparison.OrdinalIgnoreCase);
    }
}
