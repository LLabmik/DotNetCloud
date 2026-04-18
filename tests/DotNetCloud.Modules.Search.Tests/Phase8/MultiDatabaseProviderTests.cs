using DotNetCloud.Core.Capabilities;
using DotNetCloud.Core.DTOs.Search;
using DotNetCloud.Modules.Search.Data;
using DotNetCloud.Modules.Search.Data.Models;
using DotNetCloud.Modules.Search.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace DotNetCloud.Modules.Search.Tests.Phase8;

/// <summary>
/// Multi-database provider integration tests that verify identical behavior
/// across PostgreSQL (ILIKE fallback), SQL Server (Contains fallback), and MariaDB (Contains fallback)
/// search providers using InMemory databases.
/// </summary>
[TestClass]
public class MultiDatabaseProviderTests
{
    private static readonly Guid TestUserId = Guid.Parse("11111111-1111-1111-1111-111111111111");

    private SearchDbContext CreateDbContext(string name)
    {
        var options = new DbContextOptionsBuilder<SearchDbContext>()
            .UseInMemoryDatabase(name)
            .Options;
        return new SearchDbContext(options);
    }

    private static SearchDocument CreateDoc(string moduleId, string entityId, string title, string content)
    {
        return new SearchDocument
        {
            ModuleId = moduleId,
            EntityId = entityId,
            EntityType = "TestEntity",
            Title = title,
            Content = content,
            OwnerId = TestUserId,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow,
            Metadata = new Dictionary<string, string>()
        };
    }

    private static async Task SeedTestData(ISearchProvider provider)
    {
        await provider.IndexDocumentAsync(CreateDoc("notes", "n1", "quarterly budget", "the quarterly budget review for Q4"));
        await provider.IndexDocumentAsync(CreateDoc("notes", "n2", "team meeting", "meeting notes about project timeline"));
        await provider.IndexDocumentAsync(CreateDoc("files", "f1", "budget spreadsheet", "spreadsheet with quarterly budget figures"));
        await provider.IndexDocumentAsync(CreateDoc("chat", "m1", "project update", "the project is on track for deadline"));
        await provider.IndexDocumentAsync(CreateDoc("calendar", "e1", "budget review", "annual budget review session"));
    }

    [TestMethod]
    public async Task AllProviders_SimpleSearch_ReturnsSameResults()
    {
        var providers = new List<(string Name, ISearchProvider Provider, SearchDbContext Db)>();

        // NOTE: PostgreSQL provider uses EF.Functions.ILike which doesn't work with InMemory.
        // Search tests use SqlServer and MariaDB providers only. PostgreSQL tested separately with live DB.
        var dbSql = CreateDbContext("SQL_SimpleSearch");
        providers.Add(("SqlServer", new SqlServerSearchProvider(dbSql, NullLogger<SqlServerSearchProvider>.Instance), dbSql));

        var dbMaria = CreateDbContext("Maria_SimpleSearch");
        providers.Add(("MariaDB", new MariaDbSearchProvider(dbMaria, NullLogger<MariaDbSearchProvider>.Instance), dbMaria));

        foreach (var (name, provider, _) in providers)
        {
            await SeedTestData(provider);
        }

        var query = new SearchQuery
        {
            QueryText = "budget",
            UserId = TestUserId,
            Page = 1,
            PageSize = 20
        };

        var results = new Dictionary<string, SearchResultDto>();
        foreach (var (name, provider, _) in providers)
        {
            results[name] = await provider.SearchAsync(query);
        }

        // All providers should return the same count
        var expectedCount = results.Values.First().TotalCount;
        foreach (var (name, result) in results)
        {
            Assert.AreEqual(expectedCount, result.TotalCount,
                $"{name} returned {result.TotalCount} instead of {expectedCount}");
        }

        Assert.IsTrue(expectedCount >= 3, "Should find 'budget' in at least 3 documents");

        foreach (var (_, _, db) in providers) db.Dispose();
    }

    [TestMethod]
    public async Task AllProviders_ModuleFilter_ConsistentBehavior()
    {
        var providers = new List<(string Name, ISearchProvider Provider, SearchDbContext Db)>();

        var dbSql = CreateDbContext("SQL_ModuleFilter");
        providers.Add(("SqlServer", new SqlServerSearchProvider(dbSql, NullLogger<SqlServerSearchProvider>.Instance), dbSql));

        var dbMaria = CreateDbContext("Maria_ModuleFilter");
        providers.Add(("MariaDB", new MariaDbSearchProvider(dbMaria, NullLogger<MariaDbSearchProvider>.Instance), dbMaria));

        foreach (var (_, provider, _) in providers)
        {
            await SeedTestData(provider);
        }

        var query = new SearchQuery
        {
            QueryText = "budget",
            UserId = TestUserId,
            ModuleFilter = "notes",
            Page = 1,
            PageSize = 20
        };

        foreach (var (name, provider, _) in providers)
        {
            var result = await provider.SearchAsync(query);
            Assert.IsTrue(result.Items.All(i => i.ModuleId == "notes"),
                $"{name}: All results should be from 'notes' module");
        }

        foreach (var (_, _, db) in providers) db.Dispose();
    }

    [TestMethod]
    public async Task AllProviders_IndexAndSearch_Consistent()
    {
        var providers = new List<(string Name, ISearchProvider Provider, SearchDbContext Db)>();

        var dbSql = CreateDbContext("SQL_IndexAndSearch");
        providers.Add(("SqlServer", new SqlServerSearchProvider(dbSql, NullLogger<SqlServerSearchProvider>.Instance), dbSql));

        var dbMaria = CreateDbContext("Maria_IndexAndSearch");
        providers.Add(("MariaDB", new MariaDbSearchProvider(dbMaria, NullLogger<MariaDbSearchProvider>.Instance), dbMaria));

        foreach (var (name, provider, _) in providers)
        {
            // Index
            await provider.IndexDocumentAsync(CreateDoc("notes", "1", "Test Doc", "unique content xyz123"));

            // Search
            var result = await provider.SearchAsync(new SearchQuery
            {
                QueryText = "xyz123",
                UserId = TestUserId,
                Page = 1,
                PageSize = 20
            });

            Assert.AreEqual(1, result.TotalCount, $"{name}: Should find the indexed document");
            Assert.AreEqual("1", result.Items[0].EntityId, $"{name}: Should return correct entity");
        }

        foreach (var (_, _, db) in providers) db.Dispose();
    }

    [TestMethod]
    public async Task AllProviders_RemoveAndSearch_Consistent()
    {
        var providers = new List<(string Name, ISearchProvider Provider, SearchDbContext Db)>();

        var dbSql = CreateDbContext("SQL_RemoveAndSearch");
        providers.Add(("SqlServer", new SqlServerSearchProvider(dbSql, NullLogger<SqlServerSearchProvider>.Instance), dbSql));

        var dbMaria = CreateDbContext("Maria_RemoveAndSearch");
        providers.Add(("MariaDB", new MariaDbSearchProvider(dbMaria, NullLogger<MariaDbSearchProvider>.Instance), dbMaria));

        foreach (var (name, provider, _) in providers)
        {
            await provider.IndexDocumentAsync(CreateDoc("notes", "1", "Temp", "temporary data"));
            await provider.RemoveDocumentAsync("notes", "1");

            var result = await provider.SearchAsync(new SearchQuery
            {
                QueryText = "temporary",
                UserId = TestUserId,
                Page = 1,
                PageSize = 20
            });

            Assert.AreEqual(0, result.TotalCount, $"{name}: Removed document should not appear");
        }

        foreach (var (_, _, db) in providers) db.Dispose();
    }

    [TestMethod]
    public async Task AllProviders_Upsert_UpdatesExisting()
    {
        var providers = new List<(string Name, ISearchProvider Provider, SearchDbContext Db)>();

        var dbSql = CreateDbContext("SQL_Upsert");
        providers.Add(("SqlServer", new SqlServerSearchProvider(dbSql, NullLogger<SqlServerSearchProvider>.Instance), dbSql));

        var dbMaria = CreateDbContext("Maria_Upsert");
        providers.Add(("MariaDB", new MariaDbSearchProvider(dbMaria, NullLogger<MariaDbSearchProvider>.Instance), dbMaria));

        foreach (var (name, provider, _) in providers)
        {
            await provider.IndexDocumentAsync(CreateDoc("notes", "1", "V1 Title", "version one content"));
            await provider.IndexDocumentAsync(CreateDoc("notes", "1", "V2 Title", "version two content"));

            var stats = await provider.GetIndexStatsAsync();
            Assert.AreEqual(1, stats.TotalDocuments, $"{name}: Should have 1 document (upserted, not duplicated)");

            var result = await provider.SearchAsync(new SearchQuery
            {
                QueryText = "version two",
                UserId = TestUserId,
                Page = 1,
                PageSize = 20
            });

            Assert.AreEqual(1, result.TotalCount, $"{name}: Should find updated content");
        }

        foreach (var (_, _, db) in providers) db.Dispose();
    }

    [TestMethod]
    public async Task AllProviders_Stats_ConsistentFormat()
    {
        var providers = new List<(string Name, ISearchProvider Provider, SearchDbContext Db)>();

        var dbSql = CreateDbContext("SQL_Stats");
        providers.Add(("SqlServer", new SqlServerSearchProvider(dbSql, NullLogger<SqlServerSearchProvider>.Instance), dbSql));

        var dbMaria = CreateDbContext("Maria_Stats");
        providers.Add(("MariaDB", new MariaDbSearchProvider(dbMaria, NullLogger<MariaDbSearchProvider>.Instance), dbMaria));

        foreach (var (name, provider, _) in providers)
        {
            await SeedTestData(provider);

            var stats = await provider.GetIndexStatsAsync();

            Assert.AreEqual(5, stats.TotalDocuments, $"{name}: Total document count");
            Assert.AreEqual(2, stats.DocumentsPerModule["notes"], $"{name}: Notes count");
            Assert.AreEqual(1, stats.DocumentsPerModule["files"], $"{name}: Files count");
            Assert.AreEqual(1, stats.DocumentsPerModule["chat"], $"{name}: Chat count");
            Assert.AreEqual(1, stats.DocumentsPerModule["calendar"], $"{name}: Calendar count");
            Assert.IsNotNull(stats.LastIncrementalIndexAt, $"{name}: Should have indexed at timestamp");
        }

        foreach (var (_, _, db) in providers) db.Dispose();
    }

    [TestMethod]
    public async Task AllProviders_ReindexModule_ClearsAndRebuilds()
    {
        var providers = new List<(string Name, ISearchProvider Provider, SearchDbContext Db)>();

        var dbSql = CreateDbContext("SQL_Reindex");
        providers.Add(("SqlServer", new SqlServerSearchProvider(dbSql, NullLogger<SqlServerSearchProvider>.Instance), dbSql));

        var dbMaria = CreateDbContext("Maria_Reindex");
        providers.Add(("MariaDB", new MariaDbSearchProvider(dbMaria, NullLogger<MariaDbSearchProvider>.Instance), dbMaria));

        foreach (var (name, provider, _) in providers)
        {
            await SeedTestData(provider);

            // Reindex notes
            await provider.ReindexModuleAsync("notes");

            var stats = await provider.GetIndexStatsAsync();
            Assert.AreEqual(3, stats.TotalDocuments, $"{name}: Non-notes documents should remain");
            Assert.IsFalse(stats.DocumentsPerModule.ContainsKey("notes"), $"{name}: Notes should be cleared");
            Assert.AreEqual(1, stats.DocumentsPerModule["files"], $"{name}: Files unaffected");
        }

        foreach (var (_, _, db) in providers) db.Dispose();
    }

    [TestMethod]
    public async Task AllProviders_Exclusions_ConsistentBehavior()
    {
        var providers = new List<(string Name, ISearchProvider Provider, SearchDbContext Db)>();

        var dbSql = CreateDbContext("SQL_Exclusions");
        providers.Add(("SqlServer", new SqlServerSearchProvider(dbSql, NullLogger<SqlServerSearchProvider>.Instance), dbSql));

        var dbMaria = CreateDbContext("Maria_Exclusions");
        providers.Add(("MariaDB", new MariaDbSearchProvider(dbMaria, NullLogger<MariaDbSearchProvider>.Instance), dbMaria));

        foreach (var (name, provider, _) in providers)
        {
            await provider.IndexDocumentAsync(CreateDoc("notes", "1", "Final Report", "annual financial report final"));
            await provider.IndexDocumentAsync(CreateDoc("notes", "2", "Draft Report", "annual financial report draft"));

            var result = await provider.SearchAsync(new SearchQuery
            {
                QueryText = "report -draft",
                UserId = TestUserId,
                Page = 1,
                PageSize = 20
            });

            Assert.AreEqual(1, result.TotalCount, $"{name}: Should exclude draft");
            Assert.AreEqual("1", result.Items[0].EntityId, $"{name}: Should return non-draft");
        }

        foreach (var (_, _, db) in providers) db.Dispose();
    }

    [TestMethod]
    public async Task AllProviders_Pagination_ConsistentBehavior()
    {
        var providers = new List<(string Name, ISearchProvider Provider, SearchDbContext Db)>();

        var dbSql = CreateDbContext("SQL_Pagination");
        providers.Add(("SqlServer", new SqlServerSearchProvider(dbSql, NullLogger<SqlServerSearchProvider>.Instance), dbSql));

        var dbMaria = CreateDbContext("Maria_Pagination");
        providers.Add(("MariaDB", new MariaDbSearchProvider(dbMaria, NullLogger<MariaDbSearchProvider>.Instance), dbMaria));

        foreach (var (name, provider, _) in providers)
        {
            for (var i = 1; i <= 15; i++)
            {
                await provider.IndexDocumentAsync(CreateDoc("notes", $"n{i}", $"Note {i}", "searchable content item"));
            }

            var page1 = await provider.SearchAsync(new SearchQuery
            {
                QueryText = "searchable",
                UserId = TestUserId,
                Page = 1,
                PageSize = 5
            });

            Assert.AreEqual(15, page1.TotalCount, $"{name}: Total count");
            Assert.AreEqual(5, page1.Items.Count, $"{name}: Page 1 items");

            var page3 = await provider.SearchAsync(new SearchQuery
            {
                QueryText = "searchable",
                UserId = TestUserId,
                Page = 3,
                PageSize = 5
            });

            Assert.AreEqual(15, page3.TotalCount, $"{name}: Total count on page 3");
            Assert.AreEqual(5, page3.Items.Count, $"{name}: Page 3 items");

            // No overlap between pages
            var page1Ids = page1.Items.Select(i => i.EntityId).ToHashSet();
            var page3Ids = page3.Items.Select(i => i.EntityId).ToHashSet();
            Assert.IsFalse(page1Ids.Overlaps(page3Ids), $"{name}: Pages should not overlap");
        }

        foreach (var (_, _, db) in providers) db.Dispose();
    }

    [TestMethod]
    public async Task AllProviders_MetadataPreserved_AfterIndexing()
    {
        var providers = new List<(string Name, ISearchProvider Provider, SearchDbContext Db)>();

        var dbSql = CreateDbContext("SQL_Metadata");
        providers.Add(("SqlServer", new SqlServerSearchProvider(dbSql, NullLogger<SqlServerSearchProvider>.Instance), dbSql));

        var dbMaria = CreateDbContext("Maria_Metadata");
        providers.Add(("MariaDB", new MariaDbSearchProvider(dbMaria, NullLogger<MariaDbSearchProvider>.Instance), dbMaria));

        foreach (var (name, provider, _) in providers)
        {
            var doc = new SearchDocument
            {
                ModuleId = "files",
                EntityId = "f1",
                EntityType = "Document",
                Title = "Report.pdf",
                Content = "annual report content",
                OwnerId = TestUserId,
                CreatedAt = DateTimeOffset.UtcNow,
                UpdatedAt = DateTimeOffset.UtcNow,
                Metadata = new Dictionary<string, string>
                {
                    ["MimeType"] = "application/pdf",
                    ["Size"] = "1024",
                    ["Path"] = "/documents/report.pdf"
                }
            };

            await provider.IndexDocumentAsync(doc);

            var result = await provider.SearchAsync(new SearchQuery
            {
                QueryText = "annual report",
                UserId = TestUserId,
                Page = 1,
                PageSize = 20
            });

            Assert.AreEqual(1, result.TotalCount, $"{name}: Should find document");
            var item = result.Items[0];
            Assert.AreEqual("application/pdf", item.Metadata["MimeType"], $"{name}: MimeType preserved");
            Assert.AreEqual("1024", item.Metadata["Size"], $"{name}: Size preserved");
            Assert.AreEqual("/documents/report.pdf", item.Metadata["Path"], $"{name}: Path preserved");
        }

        foreach (var (_, _, db) in providers) db.Dispose();
    }
}
