using DotNetCloud.Core.DTOs.Search;
using DotNetCloud.Modules.Search.Data;
using DotNetCloud.Modules.Search.Data.Models;
using DotNetCloud.Modules.Search.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace DotNetCloud.Modules.Search.Tests.Phase5;

/// <summary>
/// Integration tests for the Phase 5 Search Query Engine.
/// Tests the full query pipeline: query parsing → provider-specific FTS → result aggregation.
/// Uses SQL Server and MariaDB providers (which fall back to <c>Contains</c> on InMemory).
/// PostgreSQL provider uses <c>EF.Functions.ILike</c> which is not supported by InMemory,
/// so it is not tested here — it requires a live PostgreSQL database.
/// </summary>
[TestClass]
public class SearchQueryEngineIntegrationTests
{
    private SearchDbContext _db = null!;
    private Guid _userId;
    private Guid _otherUserId;

    [TestInitialize]
    public void Setup()
    {
        var options = new DbContextOptionsBuilder<SearchDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        _db = new SearchDbContext(options);
        _userId = Guid.NewGuid();
        _otherUserId = Guid.NewGuid();

        SeedTestData();
    }

    [TestCleanup]
    public void Cleanup()
    {
        _db.Dispose();
    }

    private void SeedTestData()
    {
        var now = DateTimeOffset.UtcNow;

        // NOTE: InMemory uses case-sensitive string.Contains(), so seed data uses
        // consistent lowercase for terms we want to match in tests.
        _db.SearchIndexEntries.AddRange(
            // Files module entries
            new SearchIndexEntry
            {
                ModuleId = "files", EntityId = "f1", EntityType = "FileNode",
                Title = "quarterly report Q1 2026", Content = "Revenue grew by 15% compared to the previous quarter. The sales team exceeded quarterly targets.",
                OwnerId = _userId, CreatedAt = now.AddDays(-30), UpdatedAt = now.AddDays(-1), IndexedAt = now
            },
            new SearchIndexEntry
            {
                ModuleId = "files", EntityId = "f2", EntityType = "FileNode",
                Title = "annual budget 2026", Content = "The annual budget for 2026 includes allocation for new hires and infrastructure upgrades.",
                OwnerId = _userId, CreatedAt = now.AddDays(-60), UpdatedAt = now.AddDays(-5), IndexedAt = now,
                MetadataJson = "{\"MimeType\":\"application/pdf\"}"
            },
            new SearchIndexEntry
            {
                ModuleId = "files", EntityId = "f3", EntityType = "FileNode",
                Title = "draft meeting notes", Content = "draft notes from the weekly standup meeting. Action items discussed.",
                OwnerId = _userId, CreatedAt = now.AddDays(-2), UpdatedAt = now.AddHours(-12), IndexedAt = now
            },

            // Notes module entries
            new SearchIndexEntry
            {
                ModuleId = "notes", EntityId = "n1", EntityType = "Note",
                Title = "project budget planning", Content = "budget allocation for Q1 2026 project milestones. Priority items and resource planning.",
                OwnerId = _userId, CreatedAt = now.AddDays(-15), UpdatedAt = now.AddDays(-3), IndexedAt = now
            },
            new SearchIndexEntry
            {
                ModuleId = "notes", EntityId = "n2", EntityType = "Note",
                Title = "team meeting agenda", Content = "Agenda for the quarterly team review. budget review is item 3.",
                OwnerId = _userId, CreatedAt = now.AddDays(-7), UpdatedAt = now.AddDays(-2), IndexedAt = now
            },

            // Chat module entries
            new SearchIndexEntry
            {
                ModuleId = "chat", EntityId = "c1", EntityType = "Message",
                Title = "general channel", Content = "Has anyone reviewed the quarterly report? The budget numbers look good.",
                OwnerId = _userId, CreatedAt = now.AddDays(-1), UpdatedAt = now.AddHours(-6), IndexedAt = now
            },
            new SearchIndexEntry
            {
                ModuleId = "chat", EntityId = "c2", EntityType = "Message",
                Title = "dev channel", Content = "The new search feature is nearly complete. Testing begins tomorrow.",
                OwnerId = _userId, CreatedAt = now.AddHours(-3), UpdatedAt = now.AddHours(-3), IndexedAt = now
            },

            // Other user's entries (should NOT appear in results)
            new SearchIndexEntry
            {
                ModuleId = "files", EntityId = "f99", EntityType = "FileNode",
                Title = "confidential report", Content = "quarterly confidential budget information for executives only.",
                OwnerId = _otherUserId, CreatedAt = now.AddDays(-10), UpdatedAt = now.AddDays(-1), IndexedAt = now
            }
        );

        _db.SaveChanges();
    }

    #region SQL Server Provider Tests

    [TestMethod]
    public async Task SqlServer_SimpleKeywordSearch_ReturnsMatchingResults()
    {
        var provider = new SqlServerSearchProvider(_db, NullLogger<SqlServerSearchProvider>.Instance);
        var query = new SearchQuery { QueryText = "budget", UserId = _userId };

        var result = await provider.SearchAsync(query);

        // "budget" appears in f2, n1, n2, c1 (4 entries for this user)
        Assert.IsTrue(result.TotalCount >= 3, $"Expected at least 3 results, got {result.TotalCount}");
        Assert.IsTrue(result.Items.Count >= 3, "Should return matching items");
    }

    [TestMethod]
    public async Task SqlServer_PhraseSearch_ReturnsExactPhraseMatches()
    {
        var provider = new SqlServerSearchProvider(_db, NullLogger<SqlServerSearchProvider>.Instance);
        var query = new SearchQuery { QueryText = "\"quarterly report\"", UserId = _userId };

        var result = await provider.SearchAsync(query);

        Assert.IsTrue(result.TotalCount >= 1);
        Assert.IsTrue(result.Items.Any(i =>
            i.Title.Contains("quarterly", StringComparison.OrdinalIgnoreCase) ||
            i.Snippet.Contains("quarterly", StringComparison.OrdinalIgnoreCase)));
    }

    [TestMethod]
    public async Task SqlServer_ExclusionSearch_ExcludesMatchingTerms()
    {
        var provider = new SqlServerSearchProvider(_db, NullLogger<SqlServerSearchProvider>.Instance);
        var query = new SearchQuery { QueryText = "meeting -draft", UserId = _userId };

        var result = await provider.SearchAsync(query);

        // "draft meeting notes" should be excluded
        Assert.IsTrue(result.Items.All(i => i.EntityId != "f3"),
            "Entry with 'draft' should be excluded");
    }

    [TestMethod]
    public async Task SqlServer_ModuleFilter_RestrictsToModule()
    {
        var provider = new SqlServerSearchProvider(_db, NullLogger<SqlServerSearchProvider>.Instance);
        var parsed = SearchQueryParser.Parse("in:notes budget");
        var query = new SearchQuery
        {
            QueryText = "budget",
            UserId = _userId,
            ModuleFilter = parsed.ModuleFilter
        };

        var result = await provider.SearchAsync(query);

        Assert.IsTrue(result.Items.All(i => i.ModuleId == "notes"));
        Assert.IsTrue(result.TotalCount >= 1);
    }

    [TestMethod]
    public async Task SqlServer_RelevanceScoring_TitleMatchScoredHigher()
    {
        var provider = new SqlServerSearchProvider(_db, NullLogger<SqlServerSearchProvider>.Instance);
        var query = new SearchQuery { QueryText = "budget", UserId = _userId, SortOrder = SearchSortOrder.Relevance };

        var result = await provider.SearchAsync(query);

        var titleMatch = result.Items.FirstOrDefault(i =>
            i.Title.Contains("budget", StringComparison.OrdinalIgnoreCase));
        var contentOnlyMatch = result.Items.FirstOrDefault(i =>
            !i.Title.Contains("budget", StringComparison.OrdinalIgnoreCase));

        if (titleMatch is not null && contentOnlyMatch is not null)
        {
            Assert.IsTrue(titleMatch.RelevanceScore > contentOnlyMatch.RelevanceScore,
                $"Title match ({titleMatch.RelevanceScore}) should score higher than content-only ({contentOnlyMatch.RelevanceScore})");
        }
    }

    [TestMethod]
    public async Task SqlServer_SnippetGeneration_ContainsMarkTags()
    {
        var provider = new SqlServerSearchProvider(_db, NullLogger<SqlServerSearchProvider>.Instance);
        var query = new SearchQuery { QueryText = "budget", UserId = _userId };

        var result = await provider.SearchAsync(query);

        var itemWithSnippet = result.Items.FirstOrDefault(i => i.Snippet.Contains("<mark>"));
        Assert.IsNotNull(itemWithSnippet, "At least one result should have highlighted snippet");
        Assert.IsTrue(itemWithSnippet.Snippet.Contains("<mark>budget</mark>", StringComparison.OrdinalIgnoreCase));
    }

    [TestMethod]
    public async Task SqlServer_TitleHighlighting_ContainsMarkTags()
    {
        var provider = new SqlServerSearchProvider(_db, NullLogger<SqlServerSearchProvider>.Instance);
        var query = new SearchQuery { QueryText = "budget", UserId = _userId };

        var result = await provider.SearchAsync(query);

        var highlighted = result.Items.FirstOrDefault(i =>
            i.Title.Contains("<mark>", StringComparison.OrdinalIgnoreCase));
        Assert.IsNotNull(highlighted, "Items with match in title should have highlighted title");
    }

    [TestMethod]
    public async Task SqlServer_FacetCounts_GroupedByModule()
    {
        var provider = new SqlServerSearchProvider(_db, NullLogger<SqlServerSearchProvider>.Instance);
        var query = new SearchQuery { QueryText = "budget", UserId = _userId };

        var result = await provider.SearchAsync(query);

        Assert.IsTrue(result.FacetCounts.Count > 0, "Should have facet counts");
        Assert.IsTrue(result.FacetCounts.ContainsKey("files") || result.FacetCounts.ContainsKey("notes"),
            "Facet counts should contain at least files or notes module");
    }

    [TestMethod]
    public async Task SqlServer_PermissionScoping_OnlyReturnsOwnResults()
    {
        var provider = new SqlServerSearchProvider(_db, NullLogger<SqlServerSearchProvider>.Instance);
        var query = new SearchQuery { QueryText = "quarterly", UserId = _userId };

        var result = await provider.SearchAsync(query);

        Assert.IsTrue(result.Items.All(i => i.EntityId != "f99"),
            "Should not return other user's documents");
    }

    #endregion

    #region Additional SQL Server Tests

    [TestMethod]
    public async Task SqlServer_Pagination_ReturnsCorrectPage()
    {
        var provider = new SqlServerSearchProvider(_db, NullLogger<SqlServerSearchProvider>.Instance);
        var query = new SearchQuery { QueryText = "budget", UserId = _userId, Page = 1, PageSize = 2 };

        var result = await provider.SearchAsync(query);

        Assert.AreEqual(1, result.Page);
        Assert.AreEqual(2, result.PageSize);
        Assert.IsTrue(result.Items.Count <= 2);
        Assert.IsTrue(result.TotalCount >= result.Items.Count);
    }

    [TestMethod]
    public async Task SqlServer_DateDescSort_ReturnsMostRecentFirst()
    {
        var provider = new SqlServerSearchProvider(_db, NullLogger<SqlServerSearchProvider>.Instance);
        var query = new SearchQuery { QueryText = "budget", UserId = _userId, SortOrder = SearchSortOrder.DateDesc };

        var result = await provider.SearchAsync(query);

        if (result.Items.Count >= 2)
        {
            for (int i = 0; i < result.Items.Count - 1; i++)
            {
                Assert.IsTrue(result.Items[i].UpdatedAt >= result.Items[i + 1].UpdatedAt,
                    "Results should be sorted by date descending");
            }
        }
    }

    [TestMethod]
    public async Task SqlServer_DateAscSort_ReturnsOldestFirst()
    {
        var provider = new SqlServerSearchProvider(_db, NullLogger<SqlServerSearchProvider>.Instance);
        var query = new SearchQuery { QueryText = "budget", UserId = _userId, SortOrder = SearchSortOrder.DateAsc };

        var result = await provider.SearchAsync(query);

        if (result.Items.Count >= 2)
        {
            for (int i = 0; i < result.Items.Count - 1; i++)
            {
                Assert.IsTrue(result.Items[i].UpdatedAt <= result.Items[i + 1].UpdatedAt,
                    "Results should be sorted by date ascending");
            }
        }
    }

    [TestMethod]
    public async Task SqlServer_SnippetHighlighting_CorrectMarkTags()
    {
        var provider = new SqlServerSearchProvider(_db, NullLogger<SqlServerSearchProvider>.Instance);
        var query = new SearchQuery { QueryText = "search", UserId = _userId };

        var result = await provider.SearchAsync(query);

        if (result.Items.Count > 0)
        {
            var item = result.Items[0];
            Assert.IsTrue(item.Snippet.Contains("<mark>search</mark>", StringComparison.OrdinalIgnoreCase),
                $"Snippet should contain highlighted term. Got: {item.Snippet}");
        }
    }

    #endregion

    #region MariaDB Provider Tests

    [TestMethod]
    public async Task MariaDb_SimpleKeywordSearch_ReturnsMatchingResults()
    {
        var provider = new MariaDbSearchProvider(_db, NullLogger<MariaDbSearchProvider>.Instance);
        var query = new SearchQuery { QueryText = "budget", UserId = _userId };

        var result = await provider.SearchAsync(query);

        Assert.IsTrue(result.TotalCount >= 3);
    }

    [TestMethod]
    public async Task MariaDb_ExclusionSearch_ExcludesResults()
    {
        var provider = new MariaDbSearchProvider(_db, NullLogger<MariaDbSearchProvider>.Instance);
        var query = new SearchQuery { QueryText = "meeting -draft", UserId = _userId };

        var result = await provider.SearchAsync(query);

        Assert.IsTrue(result.Items.All(i => i.EntityId != "f3"),
            "Entry with 'draft' should be excluded");
    }

    [TestMethod]
    public async Task MariaDb_EntityTypeFilter_RestrictsResults()
    {
        var provider = new MariaDbSearchProvider(_db, NullLogger<MariaDbSearchProvider>.Instance);
        var query = new SearchQuery
        {
            QueryText = "budget",
            UserId = _userId,
            EntityTypeFilter = "FileNode"
        };

        var result = await provider.SearchAsync(query);

        Assert.IsTrue(result.Items.All(i => i.EntityType == "FileNode"));
    }

    [TestMethod]
    public async Task MariaDb_RelevanceScoring_ProducesNonZeroScores()
    {
        var provider = new MariaDbSearchProvider(_db, NullLogger<MariaDbSearchProvider>.Instance);
        var query = new SearchQuery { QueryText = "budget", UserId = _userId };

        var result = await provider.SearchAsync(query);

        Assert.IsTrue(result.Items.All(i => i.RelevanceScore > 0),
            "All matching results should have positive relevance scores");
    }

    #endregion

    #region Cross-Provider Consistency Tests (SQL Server & MariaDB)

    [TestMethod]
    public async Task BothProviders_SameQuery_ReturnSameResultCount()
    {
        var sqlProvider = new SqlServerSearchProvider(_db, NullLogger<SqlServerSearchProvider>.Instance);
        var mariaProvider = new MariaDbSearchProvider(_db, NullLogger<MariaDbSearchProvider>.Instance);

        var query = new SearchQuery { QueryText = "budget", UserId = _userId };

        var sqlResult = await sqlProvider.SearchAsync(query);
        var mariaResult = await mariaProvider.SearchAsync(query);

        Assert.AreEqual(sqlResult.TotalCount, mariaResult.TotalCount,
            $"SQL Server ({sqlResult.TotalCount}) and MariaDB ({mariaResult.TotalCount}) should return same count");
    }

    [TestMethod]
    public async Task BothProviders_SameQuery_ReturnSameEntityIds()
    {
        var sqlProvider = new SqlServerSearchProvider(_db, NullLogger<SqlServerSearchProvider>.Instance);
        var mariaProvider = new MariaDbSearchProvider(_db, NullLogger<MariaDbSearchProvider>.Instance);

        var query = new SearchQuery { QueryText = "search", UserId = _userId, SortOrder = SearchSortOrder.DateDesc };

        var sqlResult = await sqlProvider.SearchAsync(query);
        var mariaResult = await mariaProvider.SearchAsync(query);

        var sqlIds = sqlResult.Items.Select(i => i.EntityId).OrderBy(x => x).ToList();
        var mariaIds = mariaResult.Items.Select(i => i.EntityId).OrderBy(x => x).ToList();

        CollectionAssert.AreEqual(sqlIds, mariaIds, "SQL Server and MariaDB should return same entity IDs");
    }

    [TestMethod]
    public async Task BothProviders_ExclusionQuery_SameExclusions()
    {
        var sqlProvider = new SqlServerSearchProvider(_db, NullLogger<SqlServerSearchProvider>.Instance);
        var mariaProvider = new MariaDbSearchProvider(_db, NullLogger<MariaDbSearchProvider>.Instance);

        var query = new SearchQuery { QueryText = "meeting -draft", UserId = _userId, SortOrder = SearchSortOrder.DateDesc };

        var sqlResult = await sqlProvider.SearchAsync(query);
        var mariaResult = await mariaProvider.SearchAsync(query);

        Assert.AreEqual(sqlResult.TotalCount, mariaResult.TotalCount);
    }

    #endregion

    #region Empty/Edge Case Tests

    [TestMethod]
    public async Task BothProviders_EmptyQueryText_HandlesGracefully()
    {
        var providers = GetTestableProviders();

        foreach (var provider in providers)
        {
            var query = new SearchQuery { QueryText = "", UserId = _userId };

            // Provider receives the raw query and parses internally.
            // With empty text, parser produces no terms, so all user's docs may be returned.
            var result = await provider.SearchAsync(query);

            Assert.IsTrue(result.TotalCount >= 0);
        }
    }

    [TestMethod]
    public async Task BothProviders_NoMatchingResults_ReturnsEmptySet()
    {
        var providers = GetTestableProviders();

        foreach (var provider in providers)
        {
            var query = new SearchQuery { QueryText = "zzzznonexistenttermzzzz", UserId = _userId };
            var result = await provider.SearchAsync(query);

            Assert.AreEqual(0, result.TotalCount);
            Assert.AreEqual(0, result.Items.Count);
        }
    }

    [TestMethod]
    public async Task BothProviders_UnknownUser_ReturnsNoResults()
    {
        var providers = GetTestableProviders();
        var unknownUser = Guid.NewGuid();

        foreach (var provider in providers)
        {
            var query = new SearchQuery { QueryText = "budget", UserId = unknownUser };
            var result = await provider.SearchAsync(query);

            Assert.AreEqual(0, result.TotalCount);
        }
    }

    [TestMethod]
    public async Task BothProviders_PaginationBeyondResults_ReturnsEmpty()
    {
        var providers = GetTestableProviders();

        foreach (var provider in providers)
        {
            var query = new SearchQuery { QueryText = "budget", UserId = _userId, Page = 100, PageSize = 20 };
            var result = await provider.SearchAsync(query);

            Assert.AreEqual(0, result.Items.Count);
            Assert.IsTrue(result.TotalCount > 0, "Total count still reflects matching docs");
        }
    }

    #endregion

    /// <summary>
    /// Returns providers that can run with InMemory database.
    /// PostgreSQL provider uses <c>EF.Functions.ILike</c> which is not supported by InMemory.
    /// </summary>
    private List<Core.Capabilities.ISearchProvider> GetTestableProviders()
    {
        return
        [
            new SqlServerSearchProvider(_db, NullLogger<SqlServerSearchProvider>.Instance),
            new MariaDbSearchProvider(_db, NullLogger<MariaDbSearchProvider>.Instance)
        ];
    }
}
