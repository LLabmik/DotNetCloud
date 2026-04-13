using DotNetCloud.Core.Capabilities;
using DotNetCloud.Core.DTOs.Search;
using DotNetCloud.Modules.Files.Models;
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
    private readonly ILogger<FilesSearchableModule> _logger;

    /// <summary>Initializes a new instance of the <see cref="FilesSearchableModule"/> class.</summary>
    public FilesSearchableModule(FilesDbContext db, ILogger<FilesSearchableModule> logger)
    {
        _db = db;
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

        return nodes.Select(ToSearchDocument).ToList();
    }

    /// <inheritdoc />
    public async Task<SearchDocument?> GetSearchableDocumentAsync(string entityId, CancellationToken cancellationToken = default)
    {
        if (!Guid.TryParse(entityId, out var id))
            return null;

        var node = await _db.FileNodes
            .FirstOrDefaultAsync(n => n.Id == id && !n.IsDeleted, cancellationToken);

        return node is null ? null : ToSearchDocument(node);
    }

    private static SearchDocument ToSearchDocument(FileNode node)
    {
        var metadata = new Dictionary<string, string>();
        if (!string.IsNullOrEmpty(node.MimeType))
            metadata["MimeType"] = node.MimeType;
        if (node.NodeType == FileNodeType.File)
            metadata["Size"] = node.Size.ToString();
        metadata["NodeType"] = node.NodeType.ToString();
        if (!string.IsNullOrEmpty(node.MaterializedPath))
            metadata["Path"] = node.MaterializedPath;

        return new SearchDocument
        {
            ModuleId = "files",
            EntityId = node.Id.ToString(),
            EntityType = "FileNode",
            Title = node.Name,
            Content = node.Name, // File name is primary searchable content; actual file content extraction is handled by ContentExtractionService
            Summary = node.NodeType == FileNodeType.Folder
                ? $"Folder: {node.Name}"
                : $"File: {node.Name} ({node.MimeType ?? "unknown"})",
            OwnerId = node.OwnerId,
            CreatedAt = new DateTimeOffset(node.CreatedAt, TimeSpan.Zero),
            UpdatedAt = new DateTimeOffset(node.UpdatedAt, TimeSpan.Zero),
            Metadata = metadata
        };
    }
}
