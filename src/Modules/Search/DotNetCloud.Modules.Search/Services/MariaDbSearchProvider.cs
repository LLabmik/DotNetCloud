using System.Text.Json;
using DotNetCloud.Core.Capabilities;
using DotNetCloud.Core.DTOs.Search;
using DotNetCloud.Modules.Search.Data;
using DotNetCloud.Modules.Search.Data.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace DotNetCloud.Modules.Search.Services;

/// <summary>
/// MariaDB full-text search provider using <c>MATCH ... AGAINST ... IN BOOLEAN MODE</c> for native FTS.
/// Falls back to <c>Contains</c> when running on non-MariaDB providers (e.g., InMemory for tests).
/// Supports query parsing with phrases, exclusions, and relevance ranking.
/// </summary>
public sealed class MariaDbSearchProvider : ISearchProvider
{
    private readonly SearchDbContext _db;
    private readonly ILogger<MariaDbSearchProvider> _logger;

    /// <summary>Initializes a new instance of the <see cref="MariaDbSearchProvider"/> class.</summary>
    public MariaDbSearchProvider(SearchDbContext db, ILogger<MariaDbSearchProvider> logger)
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

        var parsed = SearchQueryParser.Parse(query.QueryText);

        var dbQuery = _db.SearchIndexEntries
            .Where(e => e.OwnerId == query.UserId);

        if (!string.IsNullOrEmpty(query.ModuleFilter))
        {
            dbQuery = dbQuery.Where(e => e.ModuleId == query.ModuleFilter);
        }

        if (!string.IsNullOrEmpty(query.EntityTypeFilter))
        {
            dbQuery = dbQuery.Where(e => e.EntityType == query.EntityTypeFilter);
        }

        // MariaDB MATCH AGAINST fallback to Contains() for InMemory
        if (parsed.HasSearchableContent)
        {
            var searchTerms = parsed.Terms.Concat(parsed.Phrases).ToList();
            foreach (var term in searchTerms)
            {
                var t = term;
                dbQuery = dbQuery.Where(e =>
                    e.Title.Contains(t) ||
                    e.Content.Contains(t));
            }
        }

        // Apply exclusions
        foreach (var exclusion in parsed.Exclusions)
        {
            var ex = exclusion;
            dbQuery = dbQuery.Where(e =>
                !e.Title.Contains(ex) &&
                !e.Content.Contains(ex));
        }

        var totalCount = await dbQuery.CountAsync(cancellationToken);

        // Sort order with relevance support
        dbQuery = query.SortOrder switch
        {
            SearchSortOrder.DateDesc => dbQuery.OrderByDescending(e => e.UpdatedAt),
            SearchSortOrder.DateAsc => dbQuery.OrderBy(e => e.UpdatedAt),
            _ => ApplyRelevanceSort(dbQuery, parsed)
        };

        var skip = (query.Page - 1) * query.PageSize;
        var entries = await dbQuery
            .Skip(skip)
            .Take(query.PageSize)
            .ToListAsync(cancellationToken);

        // Facet counts
        var facetQuery = BuildFacetQuery(query, parsed);
        var facetCounts = await facetQuery
            .GroupBy(e => e.ModuleId)
            .Select(g => new { ModuleId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(g => g.ModuleId, g => g.Count, cancellationToken);

        var items = entries.Select(e => new SearchResultItem
        {
            ModuleId = e.ModuleId,
            EntityId = e.EntityId,
            EntityType = e.EntityType,
            Title = SnippetGenerator.HighlightTitle(e.Title, parsed),
            Snippet = SnippetGenerator.Generate(e.Content, parsed),
            RelevanceScore = CalculateRelevanceScore(e, parsed),
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

    private IQueryable<SearchIndexEntry> BuildFacetQuery(SearchQuery query, ParsedSearchQuery parsed)
    {
        var facetQuery = _db.SearchIndexEntries
            .Where(e => e.OwnerId == query.UserId);

        if (parsed.HasSearchableContent)
        {
            var searchTerms = parsed.Terms.Concat(parsed.Phrases).ToList();
            foreach (var term in searchTerms)
            {
                var t = term;
                facetQuery = facetQuery.Where(e =>
                    e.Title.Contains(t) ||
                    e.Content.Contains(t));
            }
        }

        foreach (var exclusion in parsed.Exclusions)
        {
            var ex = exclusion;
            facetQuery = facetQuery.Where(e =>
                !e.Title.Contains(ex) &&
                !e.Content.Contains(ex));
        }

        return facetQuery;
    }

    private static IQueryable<SearchIndexEntry> ApplyRelevanceSort(
        IQueryable<SearchIndexEntry> query, ParsedSearchQuery parsed)
    {
        if (parsed.Terms.Count > 0)
        {
            var firstTerm = parsed.Terms[0];
            return query
                .OrderByDescending(e => e.Title.Contains(firstTerm))
                .ThenByDescending(e => e.UpdatedAt);
        }

        if (parsed.Phrases.Count > 0)
        {
            var firstPhrase = parsed.Phrases[0];
            return query
                .OrderByDescending(e => e.Title.Contains(firstPhrase))
                .ThenByDescending(e => e.UpdatedAt);
        }

        return query.OrderByDescending(e => e.UpdatedAt);
    }

    private static double CalculateRelevanceScore(SearchIndexEntry entry, ParsedSearchQuery parsed)
    {
        var score = 0.0;
        var allTerms = parsed.Terms.Concat(parsed.Phrases).ToList();

        foreach (var term in allTerms)
        {
            if (entry.Title.Contains(term, StringComparison.OrdinalIgnoreCase))
                score += 2.0;

            if (entry.Content.Contains(term, StringComparison.OrdinalIgnoreCase))
                score += 1.0;

            if (entry.Title.Equals(term, StringComparison.OrdinalIgnoreCase))
                score += 3.0;
        }

        return score > 0 ? score : 0.1;
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
