using DotNetCloud.Core.Capabilities;
using DotNetCloud.Core.DTOs.Search;
using DotNetCloud.Modules.Files.Models;
using DotNetCloud.Modules.Files.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace DotNetCloud.Modules.Files.Data.Services;

/// <summary>
/// Exposes Files module data for full-text search indexing.
/// Provides file and folder metadata as <see cref="SearchDocument"/> instances.
/// </summary>
public sealed class FilesSearchableModule : ISearchableModule
{
    private readonly FilesDbContext _db;
    private readonly IFileStorageEngine _storageEngine;
    private readonly IEnumerable<IContentExtractor> _extractors;
    private readonly ILogger<FilesSearchableModule> _logger;
    private const int MaxExtractedCharacters = 100_000;
    private const int MaxExtractedBytes = 200_000;

    /// <summary>Initializes a new instance of the <see cref="FilesSearchableModule"/> class.</summary>
    public FilesSearchableModule(
        FilesDbContext db,
        IFileStorageEngine storageEngine,
        IEnumerable<IContentExtractor> extractors,
        ILogger<FilesSearchableModule> logger)
    {
        _db = db;
        _storageEngine = storageEngine;
        _extractors = extractors;
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

        _logger.LogInformation("FilesSearchableModule: retrieved {Count} file nodes for search indexing", nodes.Count);

        // Process sequentially — DbContext is not thread-safe
        var results = new List<SearchDocument>(nodes.Count);
        foreach (var node in nodes)
        {
            results.Add(await ToSearchDocumentAsync(node, cancellationToken));
        }
        return results;
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
        try
        {
            var extractedText = await TryExtractTextContentAsync(node, cancellationToken);
            if (!string.IsNullOrWhiteSpace(extractedText))
            {
                content = $"{node.Name}\n{extractedText}";
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogError(ex, "Failed to extract text content for file {FileName} (Id={FileId})", node.Name, node.Id);
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
        if (node.NodeType != FileNodeType.File)
        {
            _logger.LogDebug("Skipping content extraction for {FileName}: not a file", node.Name);
            return null;
        }

        // Determine MIME type and find an appropriate extractor
        var mimeType = node.MimeType;
        if (string.IsNullOrWhiteSpace(mimeType))
        {
            _logger.LogDebug("Skipping content extraction for {FileName}: no MIME type", node.Name);
            return null;
        }

        var extractor = _extractors.FirstOrDefault(e => e.CanExtract(mimeType));
        if (extractor is null)
        {
            _logger.LogDebug("No content extractor found for {FileName} (Mime={MimeType})", node.Name, mimeType);
            return null;
        }

        _logger.LogInformation("Extracting text content for file {FileName} (Id={FileId}, Mime={MimeType}) using {Extractor}",
            node.Name, node.Id, mimeType, extractor.GetType().Name);

        // Reassemble file from chunks
        var stream = await ReassembleFileStreamAsync(node, cancellationToken);
        if (stream is null)
            return null;

        try
        {
            var extracted = await extractor.ExtractAsync(stream, mimeType, cancellationToken);
            if (extracted is null || string.IsNullOrWhiteSpace(extracted.Text))
            {
                _logger.LogDebug("Extractor returned no content for {FileName}", node.Name);
                return null;
            }

            var text = extracted.Text.Length > MaxExtractedCharacters
                ? extracted.Text[..MaxExtractedCharacters]
                : extracted.Text;

            _logger.LogInformation("Content extraction for {FileName}: {CharCount} characters extracted",
                node.Name, text.Length);
            return text;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogWarning(ex, "Content extraction failed for {FileName} (Mime={MimeType})", node.Name, mimeType);
            return null;
        }
        finally
        {
            await stream.DisposeAsync();
        }
    }

    private async Task<MemoryStream?> ReassembleFileStreamAsync(FileNode node, CancellationToken cancellationToken)
    {
        var latestVersion = await _db.FileVersions
            .AsNoTracking()
            .Where(v => v.FileNodeId == node.Id)
            .OrderByDescending(v => v.VersionNumber)
            .Select(v => new { v.Id })
            .FirstOrDefaultAsync(cancellationToken);

        if (latestVersion is null)
        {
            _logger.LogWarning("No file version found for {FileName} (Id={FileId})", node.Name, node.Id);
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
            _logger.LogWarning("No chunks found for version {VersionId} of file {FileName}", latestVersion.Id, node.Name);
            return null;
        }

        _logger.LogDebug("Reassembling {ChunkCount} chunks for {FileName}", chunks.Count, node.Name);

        var aggregate = new MemoryStream(capacity: Math.Min(MaxExtractedBytes, 16 * 1024));
        foreach (var vc in chunks)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var chunkPath = vc.FileChunk?.StoragePath;
            if (string.IsNullOrWhiteSpace(chunkPath))
            {
                _logger.LogWarning("Chunk {Index} has null/empty StoragePath for file {FileName}", vc.SequenceIndex, node.Name);
                continue;
            }

            await using var chunkStream = await _storageEngine.OpenReadStreamAsync(chunkPath, cancellationToken);
            if (chunkStream is null)
            {
                _logger.LogWarning("Storage engine returned null stream for chunk path {ChunkPath} of file {FileName}", chunkPath, node.Name);
                continue;
            }

            var remainingBytes = MaxExtractedBytes - (int)aggregate.Length;
            if (remainingBytes <= 0)
                break;

            var byteBuffer = new byte[Math.Min(8192, remainingBytes)];
            while (remainingBytes > 0)
            {
                var read = await chunkStream.ReadAsync(byteBuffer.AsMemory(0, Math.Min(byteBuffer.Length, remainingBytes)), cancellationToken);
                if (read == 0)
                    break;

                await aggregate.WriteAsync(byteBuffer.AsMemory(0, read), cancellationToken);
                remainingBytes -= read;
            }

            if (aggregate.Length >= MaxExtractedBytes)
                break;
        }

        if (aggregate.Length == 0)
        {
            _logger.LogWarning("Content extraction yielded 0 bytes for file {FileName} (Id={FileId})", node.Name, node.Id);
            await aggregate.DisposeAsync();
            return null;
        }

        _logger.LogInformation("Reassembled {ByteCount} bytes from {ChunkCount} chunks for {FileName}",
            aggregate.Length, chunks.Count, node.Name);

        aggregate.Position = 0;
        return aggregate;
    }
}
