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
    public IReadOnlyCollection<string> SupportedEntityTypes { get; } = ["FileNode", "AdminSharedFolderMount"];

    /// <inheritdoc />
    public async Task<IReadOnlyList<SearchDocument>> GetAllSearchableDocumentsAsync(CancellationToken cancellationToken = default)
    {
        var nodes = await _db.FileNodes
            .Where(n => !n.IsDeleted)
            .ToListAsync(cancellationToken);

        var adminSharedFolders = await _db.AdminSharedFolders
            .AsNoTracking()
            .Include(folder => folder.Grants)
            .Where(folder => folder.IsEnabled)
            .OrderBy(folder => folder.DisplayName)
            .ToListAsync(cancellationToken);

        _logger.LogInformation(
            "FilesSearchableModule: retrieved {FileNodeCount} file nodes and {SharedFolderCount} admin shared folders for search indexing",
            nodes.Count,
            adminSharedFolders.Count);

        // Process sequentially — DbContext is not thread-safe
        var results = new List<SearchDocument>(nodes.Count + adminSharedFolders.Count);
        foreach (var node in nodes)
        {
            results.Add(await ToSearchDocumentAsync(node, cancellationToken));
        }

        foreach (var adminSharedFolder in adminSharedFolders)
        {
            results.AddRange(await GetMountedSearchDocumentsAsync(adminSharedFolder, cancellationToken));
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

    private async Task<IReadOnlyList<SearchDocument>> GetMountedSearchDocumentsAsync(AdminSharedFolderDefinition definition, CancellationToken cancellationToken)
    {
        var groupIds = definition.Grants.Select(grant => grant.GroupId).Distinct().ToArray();
        if (groupIds.Length == 0 || !Directory.Exists(definition.SourcePath))
        {
            return [];
        }

        var documents = new List<SearchDocument>
        {
            CreateMountedFolderSearchDocument(definition, string.Empty, definition.DisplayName, definition.SourcePath, groupIds)
        };

        var pendingDirectories = new Stack<(string RelativePath, string PhysicalPath)>();
        pendingDirectories.Push((string.Empty, definition.SourcePath));

        while (pendingDirectories.Count > 0)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var (relativePath, physicalPath) = pendingDirectories.Pop();
            IEnumerable<FileSystemInfo> entries;
            try
            {
                entries = new DirectoryInfo(physicalPath)
                    .EnumerateFileSystemInfos()
                    .OrderBy(info => info is FileInfo)
                    .ThenBy(info => info.Name, StringComparer.OrdinalIgnoreCase)
                    .ToArray();
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                _logger.LogWarning(ex, "Failed to enumerate mounted shared folder {SharedFolderId} at {PhysicalPath}", definition.Id, physicalPath);
                continue;
            }

            foreach (var entry in entries)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var entryRelativePath = string.IsNullOrEmpty(relativePath)
                    ? entry.Name
                    : $"{relativePath}/{entry.Name}";

                if (entry is DirectoryInfo directory)
                {
                    documents.Add(CreateMountedFolderSearchDocument(definition, entryRelativePath, directory.Name, directory.FullName, groupIds));
                    pendingDirectories.Push((entryRelativePath, directory.FullName));
                    continue;
                }

                if (entry is FileInfo file)
                {
                    documents.Add(await CreateMountedFileSearchDocumentAsync(definition, entryRelativePath, file, groupIds, cancellationToken));
                }
            }
        }

        return documents;
    }

    private SearchDocument CreateMountedFolderSearchDocument(
        AdminSharedFolderDefinition definition,
        string relativePath,
        string name,
        string physicalPath,
        IReadOnlyCollection<Guid> groupIds)
    {
        var normalizedRelativePath = VirtualMountedNodeRegistry.NormalizeRelativePath(relativePath);
        var virtualPath = BuildMountedVirtualPath(definition.DisplayName, normalizedRelativePath);
        var directoryInfo = new DirectoryInfo(physicalPath);

        return new SearchDocument
        {
            ModuleId = "files",
            EntityId = string.IsNullOrEmpty(normalizedRelativePath)
                ? VirtualMountedNodeRegistry.GetAdminSharedFolderRootId(definition.Id).ToString()
                : VirtualMountedNodeRegistry.GetMountedNodeId(definition.Id, normalizedRelativePath, isDirectory: true).ToString(),
            EntityType = "AdminSharedFolderMount",
            Title = name,
            Content = $"{name}\n{virtualPath}",
            Summary = $"Mounted folder: {virtualPath}",
            OwnerId = definition.CreatedByUserId,
            OrganizationId = definition.OrganizationId,
            CreatedAt = new DateTimeOffset(definition.CreatedAt, TimeSpan.Zero),
            UpdatedAt = new DateTimeOffset(directoryInfo.Exists ? directoryInfo.LastWriteTimeUtc : definition.UpdatedAt, TimeSpan.Zero),
            Metadata = CreateMountedMetadata(
                definition,
                normalizedRelativePath,
                mimeType: null,
                nodeType: FileNodeType.Folder,
                virtualPath,
                groupIds),
        };
    }

    private async Task<SearchDocument> CreateMountedFileSearchDocumentAsync(
        AdminSharedFolderDefinition definition,
        string relativePath,
        FileInfo file,
        IReadOnlyCollection<Guid> groupIds,
        CancellationToken cancellationToken)
    {
        var normalizedRelativePath = VirtualMountedNodeRegistry.NormalizeRelativePath(relativePath);
        var mimeType = GetMountedMimeType(file.Name);
        var virtualPath = BuildMountedVirtualPath(definition.DisplayName, normalizedRelativePath);
        var content = $"{file.Name}\n{virtualPath}";

        var extractedText = await TryExtractMountedTextContentAsync(file.FullName, mimeType, cancellationToken);
        if (!string.IsNullOrWhiteSpace(extractedText))
        {
            content = $"{content}\n{extractedText}";
        }

        return new SearchDocument
        {
            ModuleId = "files",
            EntityId = VirtualMountedNodeRegistry.GetMountedNodeId(definition.Id, normalizedRelativePath, isDirectory: false).ToString(),
            EntityType = "AdminSharedFolderMount",
            Title = file.Name,
            Content = content,
            Summary = $"Mounted file: {virtualPath}",
            OwnerId = definition.CreatedByUserId,
            OrganizationId = definition.OrganizationId,
            CreatedAt = new DateTimeOffset(definition.CreatedAt, TimeSpan.Zero),
            UpdatedAt = new DateTimeOffset(file.LastWriteTimeUtc, TimeSpan.Zero),
            Metadata = CreateMountedMetadata(
                definition,
                normalizedRelativePath,
                mimeType,
                FileNodeType.File,
                virtualPath,
                groupIds,
                size: file.Length),
        };
    }

    private IReadOnlyDictionary<string, string> CreateMountedMetadata(
        AdminSharedFolderDefinition definition,
        string relativePath,
        string? mimeType,
        FileNodeType nodeType,
        string virtualPath,
        IReadOnlyCollection<Guid> groupIds,
        long? size = null)
    {
        var metadata = new Dictionary<string, string>
        {
            [SearchVisibilityMetadata.VisibilityScopeKey] = SearchVisibilityMetadata.VisibilityScopeGroupMembers,
            [SearchVisibilityMetadata.GroupScopeKey] = SearchVisibilityMetadata.BuildGroupScopeKey(groupIds),
            [SearchVisibilityMetadata.SharedFolderIdKey] = definition.Id.ToString("D"),
            [SearchVisibilityMetadata.RelativePathKey] = relativePath,
            [SearchVisibilityMetadata.VirtualSourceKindKey] = "AdminSharedFolder",
            ["NodeType"] = nodeType.ToString(),
            ["Path"] = virtualPath,
        };

        if (!string.IsNullOrWhiteSpace(mimeType))
        {
            metadata["MimeType"] = mimeType;
        }

        if (size.HasValue)
        {
            metadata["Size"] = size.Value.ToString();
        }

        return metadata;
    }

    private async Task<string?> TryExtractMountedTextContentAsync(string filePath, string? mimeType, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(mimeType))
        {
            return null;
        }

        var extractor = _extractors.FirstOrDefault(candidate => candidate.CanExtract(mimeType));
        if (extractor is null)
        {
            return null;
        }

        try
        {
            await using var stream = new FileStream(filePath, FileMode.Open, FileAccess.Read, System.IO.FileShare.ReadWrite | System.IO.FileShare.Delete);
            var extracted = await extractor.ExtractAsync(stream, mimeType, cancellationToken);
            if (extracted is null || string.IsNullOrWhiteSpace(extracted.Text))
            {
                return null;
            }

            return extracted.Text.Length > MaxExtractedCharacters
                ? extracted.Text[..MaxExtractedCharacters]
                : extracted.Text;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            _logger.LogWarning(ex, "Failed to extract mounted file content from {FilePath}", filePath);
            return null;
        }
    }

    private static string BuildMountedVirtualPath(string displayName, string relativePath)
    {
        return string.IsNullOrEmpty(relativePath)
            ? $"_DotNetCloud/{displayName}"
            : $"_DotNetCloud/{displayName}/{relativePath}";
    }

    private static string? GetMountedMimeType(string fileName)
    {
        return Path.GetExtension(fileName).ToLowerInvariant() switch
        {
            ".txt" or ".log" or ".ini" or ".cfg" or ".conf" or ".env" => "text/plain",
            ".md" or ".markdown" => "text/markdown",
            ".json" => "application/json",
            ".xml" => "application/xml",
            ".html" or ".htm" => "text/html",
            ".css" => "text/css",
            ".js" => "text/javascript",
            ".ts" => "text/typescript",
            ".cs" => "text/x-csharp",
            ".py" => "text/x-python",
            ".sh" or ".bash" => "text/x-shellscript",
            ".sql" => "text/x-sql",
            ".yaml" or ".yml" => "text/yaml",
            ".toml" => "text/toml",
            ".csv" => "text/csv",
            ".png" => "image/png",
            ".jpg" or ".jpeg" => "image/jpeg",
            ".gif" => "image/gif",
            ".webp" => "image/webp",
            ".svg" => "image/svg+xml",
            ".pdf" => "application/pdf",
            ".mp4" => "video/mp4",
            ".webm" => "video/webm",
            ".mp3" => "audio/mpeg",
            ".wav" => "audio/wav",
            ".flac" => "audio/flac",
            ".docx" => "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
            ".xlsx" => "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            ".pptx" => "application/vnd.openxmlformats-officedocument.presentationml.presentation",
            ".odt" => "application/vnd.oasis.opendocument.text",
            ".ods" => "application/vnd.oasis.opendocument.spreadsheet",
            ".odp" => "application/vnd.oasis.opendocument.presentation",
            _ => null,
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
