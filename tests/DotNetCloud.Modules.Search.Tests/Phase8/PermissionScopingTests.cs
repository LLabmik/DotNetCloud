using DotNetCloud.Core.Capabilities;
using DotNetCloud.Core.DTOs.Search;
using DotNetCloud.Modules.Search.Data;
using DotNetCloud.Modules.Search.Data.Models;
using DotNetCloud.Modules.Search.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace DotNetCloud.Modules.Search.Tests.Phase8;

/// <summary>
/// Tests for permission scoping across all search providers.
/// Verifies that User A cannot see User B's documents in search results.
/// </summary>
[TestClass]
public class PermissionScopingTests
{
    private static readonly Guid UserA = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
    private static readonly Guid UserB = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");
    private static readonly Guid OrgId = Guid.Parse("cccccccc-cccc-cccc-cccc-cccccccccccc");

    private SearchDbContext CreateDbContext(string name)
    {
        var options = new DbContextOptionsBuilder<SearchDbContext>()
            .UseInMemoryDatabase(name)
            .Options;
        return new SearchDbContext(options);
    }

    private static SearchDocument CreateDoc(string moduleId, string entityId, string title, string content, Guid ownerId)
    {
        return new SearchDocument
        {
            ModuleId = moduleId,
            EntityId = entityId,
            EntityType = "TestEntity",
            Title = title,
            Content = content,
            OwnerId = ownerId,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow,
            Metadata = new Dictionary<string, string>()
        };
    }

    [TestMethod]
    public async Task SqlServer_UserA_CannotSee_UserB_Documents()
    {
        using var db = CreateDbContext(nameof(SqlServer_UserA_CannotSee_UserB_Documents));
        var provider = new SqlServerSearchProvider(db, NullLogger<SqlServerSearchProvider>.Instance);

        await provider.IndexDocumentAsync(CreateDoc("notes", "1", "UserA Note", "secret content A", UserA));
        await provider.IndexDocumentAsync(CreateDoc("notes", "2", "UserB Note", "secret content B", UserB));

        var query = new SearchQuery
        {
            QueryText = "secret",
            UserId = UserA,
            Page = 1,
            PageSize = 20
        };

        var result = await provider.SearchAsync(query);

        Assert.AreEqual(1, result.TotalCount);
        Assert.AreEqual("1", result.Items[0].EntityId);
    }

    [TestMethod]
    public async Task MariaDb_UserA_CannotSee_UserB_Documents()
    {
        using var db = CreateDbContext(nameof(MariaDb_UserA_CannotSee_UserB_Documents));
        var provider = new MariaDbSearchProvider(db, NullLogger<MariaDbSearchProvider>.Instance);

        await provider.IndexDocumentAsync(CreateDoc("files", "f1", "UserA File", "confidential A", UserA));
        await provider.IndexDocumentAsync(CreateDoc("files", "f2", "UserB File", "confidential B", UserB));

        var query = new SearchQuery
        {
            QueryText = "confidential",
            UserId = UserA,
            Page = 1,
            PageSize = 20
        };

        var result = await provider.SearchAsync(query);

        Assert.AreEqual(1, result.TotalCount);
        Assert.AreEqual("f1", result.Items[0].EntityId);
    }

    [TestMethod]
    public async Task PostgreSql_IndexAndRemove_Scoped()
    {
        // NOTE: PostgreSQL provider uses EF.Functions.ILike which doesn't work with InMemory.
        // Testing index/remove/stats operations only. Search operations tested via SqlServer/MariaDB providers.
        using var db = CreateDbContext(nameof(PostgreSql_IndexAndRemove_Scoped));
        var provider = new PostgreSqlSearchProvider(db, NullLogger<PostgreSqlSearchProvider>.Instance);

        await provider.IndexDocumentAsync(CreateDoc("chat", "m1", "UserA Msg", "private message A", UserA));
        await provider.IndexDocumentAsync(CreateDoc("chat", "m2", "UserB Msg", "private message B", UserB));

        var stats = await provider.GetIndexStatsAsync();
        Assert.AreEqual(2, stats.TotalDocuments);

        await provider.RemoveDocumentAsync("chat", "m1");
        stats = await provider.GetIndexStatsAsync();
        Assert.AreEqual(1, stats.TotalDocuments);
    }

    [TestMethod]
    public async Task PermissionScoping_EmptyResults_WhenUserHasNoDocuments()
    {
        using var db = CreateDbContext(nameof(PermissionScoping_EmptyResults_WhenUserHasNoDocuments));
        var provider = new SqlServerSearchProvider(db, NullLogger<SqlServerSearchProvider>.Instance);

        await provider.IndexDocumentAsync(CreateDoc("notes", "1", "Note", "test content", UserA));
        await provider.IndexDocumentAsync(CreateDoc("notes", "2", "Note2", "test content", UserA));

        var query = new SearchQuery
        {
            QueryText = "test",
            UserId = UserB,
            Page = 1,
            PageSize = 20
        };

        var result = await provider.SearchAsync(query);

        Assert.AreEqual(0, result.TotalCount);
        Assert.AreEqual(0, result.Items.Count);
    }

    [TestMethod]
    public async Task PermissionScoping_FacetCounts_ScopedToUser()
    {
        using var db = CreateDbContext(nameof(PermissionScoping_FacetCounts_ScopedToUser));
        var provider = new SqlServerSearchProvider(db, NullLogger<SqlServerSearchProvider>.Instance);

        await provider.IndexDocumentAsync(CreateDoc("notes", "1", "Note", "report data", UserA));
        await provider.IndexDocumentAsync(CreateDoc("files", "2", "File", "report data", UserA));
        await provider.IndexDocumentAsync(CreateDoc("notes", "3", "Note", "report data", UserB));
        await provider.IndexDocumentAsync(CreateDoc("chat", "4", "Chat", "report data", UserB));
        await provider.IndexDocumentAsync(CreateDoc("files", "5", "File2", "report data", UserB));

        var queryA = new SearchQuery { QueryText = "report", UserId = UserA, Page = 1, PageSize = 20 };
        var resultA = await provider.SearchAsync(queryA);

        Assert.AreEqual(2, resultA.TotalCount);
        Assert.AreEqual(1, resultA.FacetCounts["notes"]);
        Assert.AreEqual(1, resultA.FacetCounts["files"]);
        Assert.IsFalse(resultA.FacetCounts.ContainsKey("chat"));

        var queryB = new SearchQuery { QueryText = "report", UserId = UserB, Page = 1, PageSize = 20 };
        var resultB = await provider.SearchAsync(queryB);

        Assert.AreEqual(3, resultB.TotalCount);
        Assert.AreEqual(1, resultB.FacetCounts["notes"]);
        Assert.AreEqual(1, resultB.FacetCounts["files"]);
        Assert.AreEqual(1, resultB.FacetCounts["chat"]);
    }

    [TestMethod]
    public async Task PermissionScoping_ModuleFilter_WithUserScope()
    {
        using var db = CreateDbContext(nameof(PermissionScoping_ModuleFilter_WithUserScope));
        var provider = new MariaDbSearchProvider(db, NullLogger<MariaDbSearchProvider>.Instance);

        await provider.IndexDocumentAsync(CreateDoc("notes", "1", "Work Note", "meeting notes", UserA));
        await provider.IndexDocumentAsync(CreateDoc("files", "2", "Work File", "meeting schedule", UserA));
        await provider.IndexDocumentAsync(CreateDoc("notes", "3", "Other Note", "meeting notes", UserB));

        var query = new SearchQuery
        {
            QueryText = "meeting",
            UserId = UserA,
            ModuleFilter = "notes",
            Page = 1,
            PageSize = 20
        };

        var result = await provider.SearchAsync(query);

        Assert.AreEqual(1, result.TotalCount);
        Assert.AreEqual("1", result.Items[0].EntityId);
        Assert.AreEqual("notes", result.Items[0].ModuleId);
    }

    [TestMethod]
    public async Task PermissionScoping_StatsNotScopedToUser()
    {
        using var db = CreateDbContext(nameof(PermissionScoping_StatsNotScopedToUser));
        var provider = new SqlServerSearchProvider(db, NullLogger<SqlServerSearchProvider>.Instance);

        await provider.IndexDocumentAsync(CreateDoc("notes", "1", "Note1", "content", UserA));
        await provider.IndexDocumentAsync(CreateDoc("notes", "2", "Note2", "content", UserB));

        // Stats are global (admin-level), not scoped to any user
        var stats = await provider.GetIndexStatsAsync();

        Assert.AreEqual(2, stats.TotalDocuments);
        Assert.AreEqual(2, stats.DocumentsPerModule["notes"]);
    }

    [TestMethod]
    public async Task PermissionScoping_EntityTypeFilter_WithUserScope()
    {
        using var db = CreateDbContext(nameof(PermissionScoping_EntityTypeFilter_WithUserScope));
        var provider = new SqlServerSearchProvider(db, NullLogger<SqlServerSearchProvider>.Instance);

        await provider.IndexDocumentAsync(new SearchDocument
        {
            ModuleId = "files",
            EntityId = "1",
            EntityType = "Document",
            Title = "Report",
            Content = "quarterly data",
            OwnerId = UserA,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow,
            Metadata = new Dictionary<string, string>()
        });
        await provider.IndexDocumentAsync(new SearchDocument
        {
            ModuleId = "files",
            EntityId = "2",
            EntityType = "Image",
            Title = "Report Photo",
            Content = "quarterly image",
            OwnerId = UserA,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow,
            Metadata = new Dictionary<string, string>()
        });
        await provider.IndexDocumentAsync(new SearchDocument
        {
            ModuleId = "files",
            EntityId = "3",
            EntityType = "Document",
            Title = "Other Report",
            Content = "quarterly data",
            OwnerId = UserB,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow,
            Metadata = new Dictionary<string, string>()
        });

        var query = new SearchQuery
        {
            QueryText = "quarterly",
            UserId = UserA,
            EntityTypeFilter = "Document",
            Page = 1,
            PageSize = 20
        };

        var result = await provider.SearchAsync(query);

        Assert.AreEqual(1, result.TotalCount);
        Assert.AreEqual("1", result.Items[0].EntityId);
    }

    [TestMethod]
    public async Task PermissionScoping_Pagination_ScopedToUser()
    {
        using var db = CreateDbContext(nameof(PermissionScoping_Pagination_ScopedToUser));
        var provider = new SqlServerSearchProvider(db, NullLogger<SqlServerSearchProvider>.Instance);

        // Add 5 docs for UserA, 3 for UserB
        for (var i = 1; i <= 5; i++)
        {
            await provider.IndexDocumentAsync(CreateDoc("notes", $"a{i}", $"Note {i}", "project update", UserA));
        }
        for (var i = 1; i <= 3; i++)
        {
            await provider.IndexDocumentAsync(CreateDoc("notes", $"b{i}", $"Note {i}", "project update", UserB));
        }

        var queryPage1 = new SearchQuery
        {
            QueryText = "project",
            UserId = UserA,
            Page = 1,
            PageSize = 3
        };

        var resultPage1 = await provider.SearchAsync(queryPage1);
        Assert.AreEqual(5, resultPage1.TotalCount);
        Assert.AreEqual(3, resultPage1.Items.Count);

        var queryPage2 = new SearchQuery
        {
            QueryText = "project",
            UserId = UserA,
            Page = 2,
            PageSize = 3
        };

        var resultPage2 = await provider.SearchAsync(queryPage2);
        Assert.AreEqual(5, resultPage2.TotalCount);
        Assert.AreEqual(2, resultPage2.Items.Count);
    }

    [TestMethod]
    public async Task PermissionScoping_Exclusions_ScopedToUser()
    {
        using var db = CreateDbContext(nameof(PermissionScoping_Exclusions_ScopedToUser));
        var provider = new MariaDbSearchProvider(db, NullLogger<MariaDbSearchProvider>.Instance);

        await provider.IndexDocumentAsync(CreateDoc("notes", "1", "Draft Note", "draft budget proposal", UserA));
        await provider.IndexDocumentAsync(CreateDoc("notes", "2", "Final Note", "final budget proposal", UserA));
        await provider.IndexDocumentAsync(CreateDoc("notes", "3", "Draft Note", "draft budget proposal", UserB));

        var query = new SearchQuery
        {
            QueryText = "budget -draft",
            UserId = UserA,
            Page = 1,
            PageSize = 20
        };

        var result = await provider.SearchAsync(query);

        Assert.AreEqual(1, result.TotalCount);
        Assert.AreEqual("2", result.Items[0].EntityId);
    }
}
