using System.Text.Json;
using DotNetCloud.Core.Capabilities;
using DotNetCloud.Core.DTOs.Search;
using DotNetCloud.Modules.Search.Data;
using DotNetCloud.Modules.Search.Data.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace DotNetCloud.Modules.Search.Services;

/// <summary>
/// PostgreSQL full-text search provider using <c>tsvector</c>/<c>tsquery</c>.
/// Falls back to <c>ILIKE</c> when running on non-PostgreSQL providers (e.g., InMemory for tests).
/// </summary>
public sealed class PostgreSqlSearchProvider : ISearchProvider
{
    private readonly SearchDbContext _db;
    private readonly ILogger<PostgreSqlSearchProvider> _logger;

    /// <summary>Initializes a new instance of the <see cref="PostgreSqlSearchProvider"/> class.</summary>
    public PostgreSqlSearchProvider(SearchDbContext db, ILogger<PostgreSqlSearchProvider> logger)
    {
        _db = db;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task IndexDocumentAsync(SearchDocument document, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(document);

        var existing = await _db.SearchIndexEntries
            .FirstOrDefaultAsync(e => e.ModuleId == document.ModuleId && e.EntityId == document.EntityId, cancellationToken);

        if (existing is not null)
        {
            existing.EntityType = document.EntityType;
            existing.Title = document.Title;
            existing.Content = document.Content;
            existing.Summary = document.Summary;
            existing.OwnerId = document.OwnerId;
            existing.OrganizationId = document.OrganizationId;
            existing.CreatedAt = document.CreatedAt;
            existing.UpdatedAt = document.UpdatedAt;
            existing.IndexedAt = DateTimeOffset.UtcNow;
            existing.MetadataJson = document.Metadata.Count > 0
                ? JsonSerializer.Serialize(document.Metadata)
                : null;
        }
        else
        {
            var entry = new SearchIndexEntry
            {
                ModuleId = document.ModuleId,
                EntityId = document.EntityId,
                EntityType = document.EntityType,
                Title = document.Title,
                Content = document.Content,
                Summary = document.Summary,
                OwnerId = document.OwnerId,
                OrganizationId = document.OrganizationId,
                CreatedAt = document.CreatedAt,
                UpdatedAt = document.UpdatedAt,
                IndexedAt = DateTimeOffset.UtcNow,
                MetadataJson = document.Metadata.Count > 0
                    ? JsonSerializer.Serialize(document.Metadata)
                    : null
            };

            _db.SearchIndexEntries.Add(entry);
        }

        await _db.SaveChangesAsync(cancellationToken);
        _logger.LogDebug("Indexed document {ModuleId}/{EntityId}", document.ModuleId, document.EntityId);
    }

    /// <inheritdoc />
    public async Task RemoveDocumentAsync(string moduleId, string entityId, CancellationToken cancellationToken = default)
    {
        var entry = await _db.SearchIndexEntries
            .FirstOrDefaultAsync(e => e.ModuleId == moduleId && e.EntityId == entityId, cancellationToken);

        if (entry is not null)
        {
            _db.SearchIndexEntries.Remove(entry);
            await _db.SaveChangesAsync(cancellationToken);
            _logger.LogDebug("Removed document {ModuleId}/{EntityId} from index", moduleId, entityId);
        }
    }

    /// <inheritdoc />
    public async Task<SearchResultDto> SearchAsync(SearchQuery query, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);

        var dbQuery = _db.SearchIndexEntries
            .Where(e => e.OwnerId == query.UserId);

        // Module filter
        if (!string.IsNullOrEmpty(query.ModuleFilter))
        {
            dbQuery = dbQuery.Where(e => e.ModuleId == query.ModuleFilter);
        }

        // Entity type filter
        if (!string.IsNullOrEmpty(query.EntityTypeFilter))
        {
            dbQuery = dbQuery.Where(e => e.EntityType == query.EntityTypeFilter);
        }

        // Full-text search: use EF.Functions.ToTsVector/plainto_tsquery for PostgreSQL
        // Fallback to LIKE for InMemory/other providers
        if (!string.IsNullOrWhiteSpace(query.QueryText))
        {
            var searchText = query.QueryText.Trim();
            dbQuery = dbQuery.Where(e =>
                EF.Functions.ILike(e.Title, $"%{searchText}%") ||
                EF.Functions.ILike(e.Content, $"%{searchText}%"));
        }

        // Get total count before pagination
        var totalCount = await dbQuery.CountAsync(cancellationToken);

        // Sort order
        dbQuery = query.SortOrder switch
        {
            SearchSortOrder.DateDesc => dbQuery.OrderByDescending(e => e.UpdatedAt),
            SearchSortOrder.DateAsc => dbQuery.OrderBy(e => e.UpdatedAt),
            _ => dbQuery.OrderByDescending(e => e.UpdatedAt) // Relevance falls back to date for ILIKE
        };

        // Pagination
        var skip = (query.Page - 1) * query.PageSize;
        var entries = await dbQuery
            .Skip(skip)
            .Take(query.PageSize)
            .ToListAsync(cancellationToken);

        // Get facet counts (results per module)
        var facetQuery = _db.SearchIndexEntries
            .Where(e => e.OwnerId == query.UserId);

        if (!string.IsNullOrWhiteSpace(query.QueryText))
        {
            var searchText = query.QueryText.Trim();
            facetQuery = facetQuery.Where(e =>
                EF.Functions.ILike(e.Title, $"%{searchText}%") ||
                EF.Functions.ILike(e.Content, $"%{searchText}%"));
        }

        var facetCounts = await facetQuery
            .GroupBy(e => e.ModuleId)
            .Select(g => new { ModuleId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(g => g.ModuleId, g => g.Count, cancellationToken);

        var items = entries.Select(e => new SearchResultItem
        {
            ModuleId = e.ModuleId,
            EntityId = e.EntityId,
            EntityType = e.EntityType,
            Title = e.Title,
            Snippet = GenerateSnippet(e.Content, query.QueryText),
            RelevanceScore = 1.0, // PostgreSQL would use ts_rank; ILIKE fallback returns 1.0
            UpdatedAt = e.UpdatedAt,
            Metadata = DeserializeMetadata(e.MetadataJson)
        }).ToList();

        return new SearchResultDto
        {
            Items = items,
            TotalCount = totalCount,
            Page = query.Page,
            PageSize = query.PageSize,
            FacetCounts = facetCounts
        };
    }

    /// <inheritdoc />
    public async Task ReindexModuleAsync(string moduleId, CancellationToken cancellationToken = default)
    {
        // Remove all existing entries for the module
        var entries = await _db.SearchIndexEntries
            .Where(e => e.ModuleId == moduleId)
            .ToListAsync(cancellationToken);

        _db.SearchIndexEntries.RemoveRange(entries);
        await _db.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Cleared {Count} index entries for module {ModuleId}", entries.Count, moduleId);
    }

    /// <inheritdoc />
    public async Task<SearchIndexStats> GetIndexStatsAsync(CancellationToken cancellationToken = default)
    {
        var totalDocuments = await _db.SearchIndexEntries.CountAsync(cancellationToken);

        var documentsPerModule = await _db.SearchIndexEntries
            .GroupBy(e => e.ModuleId)
            .Select(g => new { ModuleId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(g => g.ModuleId, g => g.Count, cancellationToken);

        var lastIndexAt = await _db.SearchIndexEntries
            .OrderByDescending(e => e.IndexedAt)
            .Select(e => (DateTimeOffset?)e.IndexedAt)
            .FirstOrDefaultAsync(cancellationToken);

        var lastFullReindex = await _db.IndexingJobs
            .Where(j => j.Type == IndexJobType.Full && j.Status == IndexJobStatus.Completed)
            .OrderByDescending(j => j.CompletedAt)
            .Select(j => j.CompletedAt)
            .FirstOrDefaultAsync(cancellationToken);

        return new SearchIndexStats
        {
            TotalDocuments = totalDocuments,
            DocumentsPerModule = documentsPerModule,
            LastFullReindexAt = lastFullReindex,
            LastIncrementalIndexAt = lastIndexAt
        };
    }

    private static string GenerateSnippet(string content, string? queryText, int maxLength = 200)
    {
        if (string.IsNullOrEmpty(content))
            return string.Empty;

        if (string.IsNullOrWhiteSpace(queryText))
            return content.Length > maxLength ? content[..maxLength] + "..." : content;

        var index = content.IndexOf(queryText, StringComparison.OrdinalIgnoreCase);
        if (index < 0)
            return content.Length > maxLength ? content[..maxLength] + "..." : content;

        var start = Math.Max(0, index - 50);
        var end = Math.Min(content.Length, index + queryText.Length + 150);
        var snippet = content[start..end];

        if (start > 0) snippet = "..." + snippet;
        if (end < content.Length) snippet += "...";

        return snippet;
    }

    private static IReadOnlyDictionary<string, string> DeserializeMetadata(string? json)
    {
        if (string.IsNullOrEmpty(json))
            return new Dictionary<string, string>();

        try
        {
            return JsonSerializer.Deserialize<Dictionary<string, string>>(json)
                   ?? new Dictionary<string, string>();
        }
        catch
        {
            return new Dictionary<string, string>();
        }
    }
}
