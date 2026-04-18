using DotNetCloud.Core.Capabilities;
using DotNetCloud.Core.DTOs.Search;
using DotNetCloud.Modules.Search.Data;
using DotNetCloud.Modules.Search.Data.Models;
using DotNetCloud.Modules.Search.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace DotNetCloud.Modules.Search.Tests.Phase8;

/// <summary>
/// Critical security tests: guarantees that one user's data NEVER appears in another user's search results.
/// Every test in this class validates the user data isolation boundary across all search providers.
/// These tests MUST pass — failures indicate a data-leak vulnerability.
/// </summary>
[TestClass]
public class UserDataIsolationTests
{
    private static readonly Guid UserAlice = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid UserBob = Guid.Parse("22222222-2222-2222-2222-222222222222");
    private static readonly Guid UserCharlie = Guid.Parse("33333333-3333-3333-3333-333333333333");
    private static readonly Guid NonExistentUser = Guid.Parse("99999999-9999-9999-9999-999999999999");

    private SearchDbContext CreateDbContext(string name)
    {
        var options = new DbContextOptionsBuilder<SearchDbContext>()
            .UseInMemoryDatabase(name)
            .Options;
        return new SearchDbContext(options);
    }

    private static SearchDocument Doc(string moduleId, string entityId, string title, string content, Guid ownerId)
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

    private static SearchQuery Query(string text, Guid userId, int page = 1, int pageSize = 50)
    {
        return new SearchQuery
        {
            QueryText = text,
            UserId = userId,
            Page = page,
            PageSize = pageSize,
            SortOrder = SearchSortOrder.Relevance
        };
    }

    // ───────────────────────────────────────────────
    //  SqlServer Provider — Cross-User Isolation
    // ───────────────────────────────────────────────

    [TestMethod]
    public async Task SqlServer_UserSearchNeverReturnsOtherUsersDocuments()
    {
        using var db = CreateDbContext(nameof(SqlServer_UserSearchNeverReturnsOtherUsersDocuments));
        var provider = new SqlServerSearchProvider(db, NullLogger<SqlServerSearchProvider>.Instance);

        // Index documents for three different users with the SAME content
        await provider.IndexDocumentAsync(Doc("notes", "alice-1", "Budget Report", "quarterly budget analysis", UserAlice));
        await provider.IndexDocumentAsync(Doc("notes", "bob-1", "Budget Report", "quarterly budget analysis", UserBob));
        await provider.IndexDocumentAsync(Doc("notes", "charlie-1", "Budget Report", "quarterly budget analysis", UserCharlie));

        // Each user searches for "budget" — must only see their own document
        var aliceResult = await provider.SearchAsync(Query("budget", UserAlice));
        var bobResult = await provider.SearchAsync(Query("budget", UserBob));
        var charlieResult = await provider.SearchAsync(Query("budget", UserCharlie));

        Assert.AreEqual(1, aliceResult.TotalCount, "Alice should see exactly 1 result");
        Assert.AreEqual(1, bobResult.TotalCount, "Bob should see exactly 1 result");
        Assert.AreEqual(1, charlieResult.TotalCount, "Charlie should see exactly 1 result");

        Assert.IsTrue(aliceResult.Items.All(i => i.EntityId.StartsWith("alice-")), "Alice must only see her own docs");
        Assert.IsTrue(bobResult.Items.All(i => i.EntityId.StartsWith("bob-")), "Bob must only see his own docs");
        Assert.IsTrue(charlieResult.Items.All(i => i.EntityId.StartsWith("charlie-")), "Charlie must only see his own docs");
    }

    [TestMethod]
    public async Task SqlServer_NonExistentUserGetsZeroResults()
    {
        using var db = CreateDbContext(nameof(SqlServer_NonExistentUserGetsZeroResults));
        var provider = new SqlServerSearchProvider(db, NullLogger<SqlServerSearchProvider>.Instance);

        await provider.IndexDocumentAsync(Doc("notes", "1", "Important Note", "very important content", UserAlice));
        await provider.IndexDocumentAsync(Doc("files", "2", "Secret File", "confidential data", UserBob));

        var result = await provider.SearchAsync(Query("important", NonExistentUser));

        Assert.AreEqual(0, result.TotalCount, "Non-existent user must see zero results");
        Assert.AreEqual(0, result.Items.Count);
    }

    [TestMethod]
    public async Task SqlServer_EmptyGuidUserGetsZeroResults()
    {
        using var db = CreateDbContext(nameof(SqlServer_EmptyGuidUserGetsZeroResults));
        var provider = new SqlServerSearchProvider(db, NullLogger<SqlServerSearchProvider>.Instance);

        await provider.IndexDocumentAsync(Doc("notes", "1", "Test Note", "test content", UserAlice));

        var result = await provider.SearchAsync(Query("test", Guid.Empty));

        Assert.AreEqual(0, result.TotalCount, "Empty GUID user must see zero results");
    }

    [TestMethod]
    public async Task SqlServer_CrossModule_UserIsolation()
    {
        using var db = CreateDbContext(nameof(SqlServer_CrossModule_UserIsolation));
        var provider = new SqlServerSearchProvider(db, NullLogger<SqlServerSearchProvider>.Instance);

        // Alice has docs across multiple modules
        await provider.IndexDocumentAsync(Doc("notes", "a1", "Project Plan", "project details", UserAlice));
        await provider.IndexDocumentAsync(Doc("files", "a2", "Project File", "project archive", UserAlice));
        await provider.IndexDocumentAsync(Doc("chat", "a3", "Project Discussion", "project chat", UserAlice));

        // Bob also has docs across multiple modules with identical content
        await provider.IndexDocumentAsync(Doc("notes", "b1", "Project Plan", "project details", UserBob));
        await provider.IndexDocumentAsync(Doc("files", "b2", "Project File", "project archive", UserBob));
        await provider.IndexDocumentAsync(Doc("chat", "b3", "Project Discussion", "project chat", UserBob));

        var aliceResult = await provider.SearchAsync(Query("project", UserAlice));
        var bobResult = await provider.SearchAsync(Query("project", UserBob));

        Assert.AreEqual(3, aliceResult.TotalCount, "Alice should see her 3 docs");
        Assert.AreEqual(3, bobResult.TotalCount, "Bob should see his 3 docs");

        // Verify NO cross-contamination — Alice's results must not contain Bob's IDs
        var aliceEntityIds = aliceResult.Items.Select(i => i.EntityId).ToHashSet();
        var bobEntityIds = bobResult.Items.Select(i => i.EntityId).ToHashSet();

        Assert.IsFalse(aliceEntityIds.Overlaps(bobEntityIds), "Alice and Bob's results must be completely disjoint");
    }

    [TestMethod]
    public async Task SqlServer_FacetCounts_NeverLeakOtherUserDocuments()
    {
        using var db = CreateDbContext(nameof(SqlServer_FacetCounts_NeverLeakOtherUserDocuments));
        var provider = new SqlServerSearchProvider(db, NullLogger<SqlServerSearchProvider>.Instance);

        // Alice: 2 notes, 1 file
        await provider.IndexDocumentAsync(Doc("notes", "a1", "Meeting Notes", "team meeting", UserAlice));
        await provider.IndexDocumentAsync(Doc("notes", "a2", "Standup Notes", "daily meeting", UserAlice));
        await provider.IndexDocumentAsync(Doc("files", "a3", "Meeting Agenda", "team meeting", UserAlice));

        // Bob: 5 notes, 3 files, 2 chat messages — Alice must never see these counts
        for (var i = 1; i <= 5; i++)
            await provider.IndexDocumentAsync(Doc("notes", $"b-note-{i}", "Meeting", "team meeting", UserBob));
        for (var i = 1; i <= 3; i++)
            await provider.IndexDocumentAsync(Doc("files", $"b-file-{i}", "Meeting", "team meeting", UserBob));
        for (var i = 1; i <= 2; i++)
            await provider.IndexDocumentAsync(Doc("chat", $"b-chat-{i}", "Meeting", "team meeting", UserBob));

        var aliceResult = await provider.SearchAsync(Query("meeting", UserAlice));

        Assert.AreEqual(3, aliceResult.TotalCount, "Alice should see exactly 3 results");
        Assert.AreEqual(2, aliceResult.FacetCounts["notes"], "Alice notes facet should be 2");
        Assert.AreEqual(1, aliceResult.FacetCounts["files"], "Alice files facet should be 1");
        Assert.IsFalse(aliceResult.FacetCounts.ContainsKey("chat"), "Alice should not see chat facet (she has no chat docs)");

        // Verify Bob's facets are correct and separate
        var bobResult = await provider.SearchAsync(Query("meeting", UserBob));
        Assert.AreEqual(10, bobResult.TotalCount, "Bob should see 10 results");
        Assert.AreEqual(5, bobResult.FacetCounts["notes"]);
        Assert.AreEqual(3, bobResult.FacetCounts["files"]);
        Assert.AreEqual(2, bobResult.FacetCounts["chat"]);
    }

    [TestMethod]
    public async Task SqlServer_TotalCount_NeverIncludesOtherUserDocuments()
    {
        using var db = CreateDbContext(nameof(SqlServer_TotalCount_NeverIncludesOtherUserDocuments));
        var provider = new SqlServerSearchProvider(db, NullLogger<SqlServerSearchProvider>.Instance);

        // Alice has 2 matching docs
        await provider.IndexDocumentAsync(Doc("notes", "a1", "Report Alpha", "annual report data", UserAlice));
        await provider.IndexDocumentAsync(Doc("notes", "a2", "Report Beta", "annual report data", UserAlice));

        // Bob has 100 matching docs — the totalCount for Alice must NEVER reflect Bob's data
        for (var i = 1; i <= 100; i++)
            await provider.IndexDocumentAsync(Doc("notes", $"b{i}", "Report", "annual report data", UserBob));

        var aliceResult = await provider.SearchAsync(Query("report", UserAlice));

        Assert.AreEqual(2, aliceResult.TotalCount, "TotalCount must reflect only Alice's documents, not Bob's 100");
    }

    [TestMethod]
    public async Task SqlServer_Pagination_NeverLeaksDocumentsAcrossPages()
    {
        using var db = CreateDbContext(nameof(SqlServer_Pagination_NeverLeaksDocumentsAcrossPages));
        var provider = new SqlServerSearchProvider(db, NullLogger<SqlServerSearchProvider>.Instance);

        // Alice has 5 docs, Bob has 20 docs with same content
        for (var i = 1; i <= 5; i++)
            await provider.IndexDocumentAsync(Doc("notes", $"a{i}", $"Task {i}", "sprint planning", UserAlice));
        for (var i = 1; i <= 20; i++)
            await provider.IndexDocumentAsync(Doc("notes", $"b{i}", $"Task {i}", "sprint planning", UserBob));

        // Page through Alice's results (page size 2) — every page must be Alice-only
        var allAliceEntityIds = new HashSet<string>();
        for (var page = 1; page <= 3; page++)
        {
            var result = await provider.SearchAsync(new SearchQuery
            {
                QueryText = "sprint",
                UserId = UserAlice,
                Page = page,
                PageSize = 2
            });

            Assert.AreEqual(5, result.TotalCount, $"Total on page {page} must still be 5");
            foreach (var item in result.Items)
            {
                Assert.IsTrue(item.EntityId.StartsWith("a"), $"Page {page}: found non-Alice entity {item.EntityId}");
                allAliceEntityIds.Add(item.EntityId);
            }
        }

        Assert.AreEqual(5, allAliceEntityIds.Count, "All 5 of Alice's docs must be reachable via pagination");
    }

    [TestMethod]
    public async Task SqlServer_ModuleFilter_NeverShowsOtherUserDocsInSameModule()
    {
        using var db = CreateDbContext(nameof(SqlServer_ModuleFilter_NeverShowsOtherUserDocsInSameModule));
        var provider = new SqlServerSearchProvider(db, NullLogger<SqlServerSearchProvider>.Instance);

        await provider.IndexDocumentAsync(Doc("notes", "a1", "Recipe", "chocolate cake recipe", UserAlice));
        await provider.IndexDocumentAsync(Doc("notes", "b1", "Recipe", "chocolate cake recipe", UserBob));
        await provider.IndexDocumentAsync(Doc("files", "a2", "Recipe Photo", "chocolate cake", UserAlice));

        var result = await provider.SearchAsync(new SearchQuery
        {
            QueryText = "recipe",
            UserId = UserAlice,
            ModuleFilter = "notes",
            Page = 1,
            PageSize = 50
        });

        Assert.AreEqual(1, result.TotalCount);
        Assert.AreEqual("a1", result.Items[0].EntityId, "Only Alice's note should appear");
    }

    [TestMethod]
    public async Task SqlServer_EntityTypeFilter_NeverShowsOtherUserDocsOfSameType()
    {
        using var db = CreateDbContext(nameof(SqlServer_EntityTypeFilter_NeverShowsOtherUserDocsOfSameType));
        var provider = new SqlServerSearchProvider(db, NullLogger<SqlServerSearchProvider>.Instance);

        await provider.IndexDocumentAsync(new SearchDocument
        {
            ModuleId = "files", EntityId = "a1", EntityType = "Document",
            Title = "Annual Revenue", Content = "revenue data",
            OwnerId = UserAlice, CreatedAt = DateTimeOffset.UtcNow, UpdatedAt = DateTimeOffset.UtcNow,
            Metadata = new Dictionary<string, string>()
        });
        await provider.IndexDocumentAsync(new SearchDocument
        {
            ModuleId = "files", EntityId = "b1", EntityType = "Document",
            Title = "Annual Revenue", Content = "revenue data",
            OwnerId = UserBob, CreatedAt = DateTimeOffset.UtcNow, UpdatedAt = DateTimeOffset.UtcNow,
            Metadata = new Dictionary<string, string>()
        });

        var result = await provider.SearchAsync(new SearchQuery
        {
            QueryText = "revenue",
            UserId = UserAlice,
            EntityTypeFilter = "Document",
            Page = 1,
            PageSize = 50
        });

        Assert.AreEqual(1, result.TotalCount);
        Assert.AreEqual("a1", result.Items[0].EntityId);
    }

    [TestMethod]
    public async Task SqlServer_Exclusions_NeverExposeOtherUserDocs()
    {
        using var db = CreateDbContext(nameof(SqlServer_Exclusions_NeverExposeOtherUserDocs));
        var provider = new SqlServerSearchProvider(db, NullLogger<SqlServerSearchProvider>.Instance);

        await provider.IndexDocumentAsync(Doc("notes", "a1", "Final Draft", "budget final version", UserAlice));
        await provider.IndexDocumentAsync(Doc("notes", "a2", "Working Draft", "budget working draft", UserAlice));
        await provider.IndexDocumentAsync(Doc("notes", "b1", "Final Budget", "budget final version", UserBob));

        // Alice searches "budget -working" — should see only her final draft, never Bob's
        var result = await provider.SearchAsync(Query("budget -working", UserAlice));

        Assert.AreEqual(1, result.TotalCount);
        Assert.AreEqual("a1", result.Items[0].EntityId);
    }

    // ───────────────────────────────────────────────
    //  MariaDB Provider — Cross-User Isolation
    // ───────────────────────────────────────────────

    [TestMethod]
    public async Task MariaDb_UserSearchNeverReturnsOtherUsersDocuments()
    {
        using var db = CreateDbContext(nameof(MariaDb_UserSearchNeverReturnsOtherUsersDocuments));
        var provider = new MariaDbSearchProvider(db, NullLogger<MariaDbSearchProvider>.Instance);

        await provider.IndexDocumentAsync(Doc("notes", "alice-1", "Budget Report", "quarterly budget analysis", UserAlice));
        await provider.IndexDocumentAsync(Doc("notes", "bob-1", "Budget Report", "quarterly budget analysis", UserBob));
        await provider.IndexDocumentAsync(Doc("notes", "charlie-1", "Budget Report", "quarterly budget analysis", UserCharlie));

        var aliceResult = await provider.SearchAsync(Query("budget", UserAlice));
        var bobResult = await provider.SearchAsync(Query("budget", UserBob));
        var charlieResult = await provider.SearchAsync(Query("budget", UserCharlie));

        Assert.AreEqual(1, aliceResult.TotalCount);
        Assert.AreEqual(1, bobResult.TotalCount);
        Assert.AreEqual(1, charlieResult.TotalCount);

        Assert.IsTrue(aliceResult.Items.All(i => i.EntityId.StartsWith("alice-")));
        Assert.IsTrue(bobResult.Items.All(i => i.EntityId.StartsWith("bob-")));
        Assert.IsTrue(charlieResult.Items.All(i => i.EntityId.StartsWith("charlie-")));
    }

    [TestMethod]
    public async Task MariaDb_NonExistentUserGetsZeroResults()
    {
        using var db = CreateDbContext(nameof(MariaDb_NonExistentUserGetsZeroResults));
        var provider = new MariaDbSearchProvider(db, NullLogger<MariaDbSearchProvider>.Instance);

        await provider.IndexDocumentAsync(Doc("files", "1", "Secret File", "top secret data", UserAlice));

        var result = await provider.SearchAsync(Query("secret", NonExistentUser));
        Assert.AreEqual(0, result.TotalCount);
    }

    [TestMethod]
    public async Task MariaDb_EmptyGuidUserGetsZeroResults()
    {
        using var db = CreateDbContext(nameof(MariaDb_EmptyGuidUserGetsZeroResults));
        var provider = new MariaDbSearchProvider(db, NullLogger<MariaDbSearchProvider>.Instance);

        await provider.IndexDocumentAsync(Doc("notes", "1", "Test Note", "test content", UserAlice));

        var result = await provider.SearchAsync(Query("test", Guid.Empty));
        Assert.AreEqual(0, result.TotalCount);
    }

    [TestMethod]
    public async Task MariaDb_CrossModule_UserIsolation()
    {
        using var db = CreateDbContext(nameof(MariaDb_CrossModule_UserIsolation));
        var provider = new MariaDbSearchProvider(db, NullLogger<MariaDbSearchProvider>.Instance);

        await provider.IndexDocumentAsync(Doc("notes", "a1", "Shared Term", "common keyword", UserAlice));
        await provider.IndexDocumentAsync(Doc("files", "a2", "Shared Term", "common keyword", UserAlice));
        await provider.IndexDocumentAsync(Doc("notes", "b1", "Shared Term", "common keyword", UserBob));
        await provider.IndexDocumentAsync(Doc("files", "b2", "Shared Term", "common keyword", UserBob));
        await provider.IndexDocumentAsync(Doc("chat", "b3", "Shared Term", "common keyword", UserBob));

        var aliceResult = await provider.SearchAsync(Query("common", UserAlice));
        var bobResult = await provider.SearchAsync(Query("common", UserBob));

        Assert.AreEqual(2, aliceResult.TotalCount);
        Assert.AreEqual(3, bobResult.TotalCount);

        var aliceIds = aliceResult.Items.Select(i => i.EntityId).ToHashSet();
        var bobIds = bobResult.Items.Select(i => i.EntityId).ToHashSet();
        Assert.IsFalse(aliceIds.Overlaps(bobIds));
    }

    [TestMethod]
    public async Task MariaDb_FacetCounts_NeverLeakOtherUserDocuments()
    {
        using var db = CreateDbContext(nameof(MariaDb_FacetCounts_NeverLeakOtherUserDocuments));
        var provider = new MariaDbSearchProvider(db, NullLogger<MariaDbSearchProvider>.Instance);

        await provider.IndexDocumentAsync(Doc("notes", "a1", "Status Update", "project status", UserAlice));

        // Bob has several modules
        await provider.IndexDocumentAsync(Doc("notes", "b1", "Status Update", "project status", UserBob));
        await provider.IndexDocumentAsync(Doc("files", "b2", "Status Update", "project status", UserBob));
        await provider.IndexDocumentAsync(Doc("chat", "b3", "Status Update", "project status", UserBob));

        var aliceResult = await provider.SearchAsync(Query("status", UserAlice));

        Assert.AreEqual(1, aliceResult.TotalCount);
        Assert.AreEqual(1, aliceResult.FacetCounts.Count, "Alice should see only 1 module facet");
        Assert.AreEqual(1, aliceResult.FacetCounts["notes"]);
        Assert.IsFalse(aliceResult.FacetCounts.ContainsKey("files"));
        Assert.IsFalse(aliceResult.FacetCounts.ContainsKey("chat"));
    }

    [TestMethod]
    public async Task MariaDb_Pagination_NeverLeaksDocumentsAcrossPages()
    {
        using var db = CreateDbContext(nameof(MariaDb_Pagination_NeverLeaksDocumentsAcrossPages));
        var provider = new MariaDbSearchProvider(db, NullLogger<MariaDbSearchProvider>.Instance);

        for (var i = 1; i <= 7; i++)
            await provider.IndexDocumentAsync(Doc("notes", $"a{i}", $"Item {i}", "search target", UserAlice));
        for (var i = 1; i <= 50; i++)
            await provider.IndexDocumentAsync(Doc("notes", $"b{i}", $"Item {i}", "search target", UserBob));

        var allAliceIds = new HashSet<string>();
        for (var page = 1; page <= 4; page++)
        {
            var result = await provider.SearchAsync(new SearchQuery
            {
                QueryText = "search",
                UserId = UserAlice,
                Page = page,
                PageSize = 2
            });

            Assert.AreEqual(7, result.TotalCount, $"Total on page {page} must be 7");
            foreach (var item in result.Items)
            {
                Assert.IsTrue(item.EntityId.StartsWith("a"), $"Page {page}: non-Alice entity {item.EntityId}");
                allAliceIds.Add(item.EntityId);
            }
        }

        Assert.AreEqual(7, allAliceIds.Count);
    }

    // ───────────────────────────────────────────────
    //  Edge Cases — Both Providers
    // ───────────────────────────────────────────────

    [TestMethod]
    public async Task AllProviders_IdenticalDocumentsDifferentOwners_StrictIsolation()
    {
        // Same ModuleId, same content, same title — different EntityId (required by unique index) and different OwnerId
        using var db1 = CreateDbContext(nameof(AllProviders_IdenticalDocumentsDifferentOwners_StrictIsolation) + "_sql");
        using var db2 = CreateDbContext(nameof(AllProviders_IdenticalDocumentsDifferentOwners_StrictIsolation) + "_maria");

        var sqlProvider = new SqlServerSearchProvider(db1, NullLogger<SqlServerSearchProvider>.Instance);
        var mariaProvider = new MariaDbSearchProvider(db2, NullLogger<MariaDbSearchProvider>.Instance);

        var providers = new (ISearchProvider Provider, string Name)[]
        {
            (sqlProvider, "SqlServer"),
            (mariaProvider, "MariaDb")
        };

        foreach (var (provider, name) in providers)
        {
            await provider.IndexDocumentAsync(Doc("notes", "entity-alice", "Identical Title", "identical content word", UserAlice));
            await provider.IndexDocumentAsync(Doc("notes", "entity-bob", "Identical Title", "identical content word", UserBob));

            var aliceResult = await provider.SearchAsync(Query("identical", UserAlice));
            var bobResult = await provider.SearchAsync(Query("identical", UserBob));

            Assert.AreEqual(1, aliceResult.TotalCount, $"{name}: Alice should see exactly 1");
            Assert.AreEqual(1, bobResult.TotalCount, $"{name}: Bob should see exactly 1");
            Assert.AreEqual("entity-alice", aliceResult.Items[0].EntityId, $"{name}: Alice sees wrong entity");
            Assert.AreEqual("entity-bob", bobResult.Items[0].EntityId, $"{name}: Bob sees wrong entity");
        }
    }

    [TestMethod]
    public async Task AllProviders_LargeDataset_NoLeakageUnderLoad()
    {
        using var db1 = CreateDbContext(nameof(AllProviders_LargeDataset_NoLeakageUnderLoad) + "_sql");
        using var db2 = CreateDbContext(nameof(AllProviders_LargeDataset_NoLeakageUnderLoad) + "_maria");

        var sqlProvider = new SqlServerSearchProvider(db1, NullLogger<SqlServerSearchProvider>.Instance);
        var mariaProvider = new MariaDbSearchProvider(db2, NullLogger<MariaDbSearchProvider>.Instance);

        var providers = new (ISearchProvider Provider, string Name)[]
        {
            (sqlProvider, "SqlServer"),
            (mariaProvider, "MariaDb")
        };

        foreach (var (provider, name) in providers)
        {
            // Index 50 docs for Alice, 200 for Bob, 100 for Charlie — all with overlapping content
            for (var i = 0; i < 50; i++)
                await provider.IndexDocumentAsync(Doc("notes", $"a-{i}", $"Doc {i}", "common search term", UserAlice));
            for (var i = 0; i < 200; i++)
                await provider.IndexDocumentAsync(Doc("notes", $"b-{i}", $"Doc {i}", "common search term", UserBob));
            for (var i = 0; i < 100; i++)
                await provider.IndexDocumentAsync(Doc("notes", $"c-{i}", $"Doc {i}", "common search term", UserCharlie));

            var aliceResult = await provider.SearchAsync(Query("common", UserAlice));
            var bobResult = await provider.SearchAsync(Query("common", UserBob));
            var charlieResult = await provider.SearchAsync(Query("common", UserCharlie));

            Assert.AreEqual(50, aliceResult.TotalCount, $"{name}: Alice should see exactly 50");
            Assert.AreEqual(200, bobResult.TotalCount, $"{name}: Bob should see exactly 200");
            Assert.AreEqual(100, charlieResult.TotalCount, $"{name}: Charlie should see exactly 100");

            // Verify no ID overlap
            Assert.IsTrue(aliceResult.Items.All(i => i.EntityId.StartsWith("a-")), $"{name}: Alice result contamination");
            Assert.IsTrue(bobResult.Items.All(i => i.EntityId.StartsWith("b-")), $"{name}: Bob result contamination");
            Assert.IsTrue(charlieResult.Items.All(i => i.EntityId.StartsWith("c-")), $"{name}: Charlie result contamination");
        }
    }

    [TestMethod]
    public async Task AllProviders_DeletedDocumentNeverAppearsForAnyUser()
    {
        using var db1 = CreateDbContext(nameof(AllProviders_DeletedDocumentNeverAppearsForAnyUser) + "_sql");
        using var db2 = CreateDbContext(nameof(AllProviders_DeletedDocumentNeverAppearsForAnyUser) + "_maria");

        var sqlProvider = new SqlServerSearchProvider(db1, NullLogger<SqlServerSearchProvider>.Instance);
        var mariaProvider = new MariaDbSearchProvider(db2, NullLogger<MariaDbSearchProvider>.Instance);

        var providers = new (ISearchProvider Provider, string Name)[]
        {
            (sqlProvider, "SqlServer"),
            (mariaProvider, "MariaDb")
        };

        foreach (var (provider, name) in providers)
        {
            await provider.IndexDocumentAsync(Doc("notes", "shared-topic-a", "Sensitive Report", "sensitive data", UserAlice));
            await provider.IndexDocumentAsync(Doc("notes", "shared-topic-b", "Sensitive Report", "sensitive data", UserBob));

            // Delete Alice's document
            await provider.RemoveDocumentAsync("notes", "shared-topic-a");

            // Neither Alice nor Bob should see Alice's deleted doc
            var aliceResult = await provider.SearchAsync(Query("sensitive", UserAlice));
            var bobResult = await provider.SearchAsync(Query("sensitive", UserBob));

            Assert.AreEqual(0, aliceResult.TotalCount, $"{name}: Alice should see 0 after deletion");
            Assert.AreEqual(1, bobResult.TotalCount, $"{name}: Bob should still see his own doc");
            Assert.AreEqual("shared-topic-b", bobResult.Items[0].EntityId, $"{name}: Bob should see only his doc");
        }
    }

    [TestMethod]
    public async Task AllProviders_SortOrder_NeverLeaksAcrossUsers()
    {
        using var db1 = CreateDbContext(nameof(AllProviders_SortOrder_NeverLeaksAcrossUsers) + "_sql");
        using var db2 = CreateDbContext(nameof(AllProviders_SortOrder_NeverLeaksAcrossUsers) + "_maria");

        var sqlProvider = new SqlServerSearchProvider(db1, NullLogger<SqlServerSearchProvider>.Instance);
        var mariaProvider = new MariaDbSearchProvider(db2, NullLogger<MariaDbSearchProvider>.Instance);

        var providers = new (ISearchProvider Provider, string Name)[]
        {
            (sqlProvider, "SqlServer"),
            (mariaProvider, "MariaDb")
        };

        var baseTime = DateTimeOffset.UtcNow;

        foreach (var (provider, name) in providers)
        {
            // Alice's old doc, Bob's very recent doc — date sort must not mix them
            await provider.IndexDocumentAsync(new SearchDocument
            {
                ModuleId = "notes", EntityId = "a1", EntityType = "TestEntity",
                Title = "Old Report", Content = "quarterly review",
                OwnerId = UserAlice,
                CreatedAt = baseTime.AddDays(-30), UpdatedAt = baseTime.AddDays(-30),
                Metadata = new Dictionary<string, string>()
            });
            await provider.IndexDocumentAsync(new SearchDocument
            {
                ModuleId = "notes", EntityId = "b1", EntityType = "TestEntity",
                Title = "Fresh Report", Content = "quarterly review",
                OwnerId = UserBob,
                CreatedAt = baseTime, UpdatedAt = baseTime,
                Metadata = new Dictionary<string, string>()
            });

            // Alice searches with date sort — must see only her old doc
            var aliceDesc = await provider.SearchAsync(new SearchQuery
            {
                QueryText = "quarterly",
                UserId = UserAlice,
                Page = 1,
                PageSize = 50,
                SortOrder = SearchSortOrder.DateDesc
            });

            var aliceAsc = await provider.SearchAsync(new SearchQuery
            {
                QueryText = "quarterly",
                UserId = UserAlice,
                Page = 1,
                PageSize = 50,
                SortOrder = SearchSortOrder.DateAsc
            });

            Assert.AreEqual(1, aliceDesc.TotalCount, $"{name}: DateDesc Alice should see 1");
            Assert.AreEqual(1, aliceAsc.TotalCount, $"{name}: DateAsc Alice should see 1");
            Assert.AreEqual("a1", aliceDesc.Items[0].EntityId, $"{name}: DateDesc wrong entity");
            Assert.AreEqual("a1", aliceAsc.Items[0].EntityId, $"{name}: DateAsc wrong entity");
        }
    }

    [TestMethod]
    public async Task AllProviders_MetadataInResults_NeverLeaksOtherUserMetadata()
    {
        using var db1 = CreateDbContext(nameof(AllProviders_MetadataInResults_NeverLeaksOtherUserMetadata) + "_sql");
        using var db2 = CreateDbContext(nameof(AllProviders_MetadataInResults_NeverLeaksOtherUserMetadata) + "_maria");

        var sqlProvider = new SqlServerSearchProvider(db1, NullLogger<SqlServerSearchProvider>.Instance);
        var mariaProvider = new MariaDbSearchProvider(db2, NullLogger<MariaDbSearchProvider>.Instance);

        var providers = new (ISearchProvider Provider, string Name)[]
        {
            (sqlProvider, "SqlServer"),
            (mariaProvider, "MariaDb")
        };

        foreach (var (provider, name) in providers)
        {
            await provider.IndexDocumentAsync(new SearchDocument
            {
                ModuleId = "files", EntityId = "a1", EntityType = "FileNode",
                Title = "Contract", Content = "important contract document",
                OwnerId = UserAlice,
                CreatedAt = DateTimeOffset.UtcNow, UpdatedAt = DateTimeOffset.UtcNow,
                Metadata = new Dictionary<string, string>
                {
                    ["Path"] = "/alice/private/contracts/",
                    ["MimeType"] = "application/pdf",
                    ["SecretTag"] = "alice-only-tag"
                }
            });
            await provider.IndexDocumentAsync(new SearchDocument
            {
                ModuleId = "files", EntityId = "b1", EntityType = "FileNode",
                Title = "Contract", Content = "important contract document",
                OwnerId = UserBob,
                CreatedAt = DateTimeOffset.UtcNow, UpdatedAt = DateTimeOffset.UtcNow,
                Metadata = new Dictionary<string, string>
                {
                    ["Path"] = "/bob/secret/contracts/",
                    ["MimeType"] = "application/pdf",
                    ["SecretTag"] = "bob-only-tag"
                }
            });

            var aliceResult = await provider.SearchAsync(Query("contract", UserAlice));
            var bobResult = await provider.SearchAsync(Query("contract", UserBob));

            Assert.AreEqual(1, aliceResult.TotalCount, $"{name}: Alice sees wrong count");
            Assert.AreEqual(1, bobResult.TotalCount, $"{name}: Bob sees wrong count");

            // Verify metadata isolation
            var aliceMeta = aliceResult.Items[0].Metadata;
            var bobMeta = bobResult.Items[0].Metadata;

            Assert.AreEqual("alice-only-tag", aliceMeta["SecretTag"], $"{name}: Alice should see her tag");
            Assert.AreEqual("bob-only-tag", bobMeta["SecretTag"], $"{name}: Bob should see his tag");
            Assert.IsTrue(aliceMeta["Path"].Contains("alice"), $"{name}: Alice's path must be her own");
            Assert.IsTrue(bobMeta["Path"].Contains("bob"), $"{name}: Bob's path must be his own");
        }
    }

    // ───────────────────────────────────────────────
    //  SearchQueryService Layer — Isolation Passthrough
    // ───────────────────────────────────────────────

    [TestMethod]
    public async Task SearchQueryService_PassesUserIdToProvider_NeverSubstitutes()
    {
        using var db = CreateDbContext(nameof(SearchQueryService_PassesUserIdToProvider_NeverSubstitutes));
        var provider = new SqlServerSearchProvider(db, NullLogger<SqlServerSearchProvider>.Instance);
        var service = new SearchQueryService(provider, NullLogger<SearchQueryService>.Instance);

        await provider.IndexDocumentAsync(Doc("notes", "a1", "Confidential", "private data alice", UserAlice));
        await provider.IndexDocumentAsync(Doc("notes", "b1", "Confidential", "private data bob", UserBob));

        var aliceResult = await service.SearchAsync(Query("private", UserAlice));
        var bobResult = await service.SearchAsync(Query("private", UserBob));

        Assert.AreEqual(1, aliceResult.TotalCount);
        Assert.AreEqual(1, bobResult.TotalCount);
        Assert.AreEqual("a1", aliceResult.Items[0].EntityId);
        Assert.AreEqual("b1", bobResult.Items[0].EntityId);
    }

    [TestMethod]
    public async Task SearchQueryService_AdvancedSyntax_NeverBypassesUserIsolation()
    {
        // Verify that in:module, type:X, -exclusions, and "phrases" all respect user scoping
        using var db = CreateDbContext(nameof(SearchQueryService_AdvancedSyntax_NeverBypassesUserIsolation));
        var provider = new SqlServerSearchProvider(db, NullLogger<SqlServerSearchProvider>.Instance);
        var service = new SearchQueryService(provider, NullLogger<SearchQueryService>.Instance);

        await provider.IndexDocumentAsync(Doc("notes", "a1", "Design Review", "architecture design patterns", UserAlice));
        await provider.IndexDocumentAsync(Doc("files", "a2", "Design Spec", "architecture design document", UserAlice));
        await provider.IndexDocumentAsync(Doc("notes", "b1", "Design Review", "architecture design patterns", UserBob));
        await provider.IndexDocumentAsync(Doc("files", "b2", "Design Spec", "architecture design document", UserBob));

        // Advanced syntax: "in:notes design" — must still be user-scoped
        var aliceInNotes = await service.SearchAsync(new SearchQuery
        {
            QueryText = "in:notes design",
            UserId = UserAlice,
            Page = 1,
            PageSize = 50
        });

        Assert.AreEqual(1, aliceInNotes.TotalCount, "in:module must be user-scoped");
        Assert.AreEqual("a1", aliceInNotes.Items[0].EntityId);

        // Phrase search
        var alicePhrase = await service.SearchAsync(new SearchQuery
        {
            QueryText = "\"design patterns\"",
            UserId = UserAlice,
            Page = 1,
            PageSize = 50
        });

        Assert.AreEqual(1, alicePhrase.TotalCount, "Phrase search must be user-scoped");
        Assert.AreEqual("a1", alicePhrase.Items[0].EntityId);
    }

    // ───────────────────────────────────────────────
    //  Index Operations — OwnerId Integrity
    // ───────────────────────────────────────────────

    [TestMethod]
    public async Task IndexUpdate_NeverChangesOwnerIdToAnotherUser()
    {
        using var db = CreateDbContext(nameof(IndexUpdate_NeverChangesOwnerIdToAnotherUser));
        var provider = new SqlServerSearchProvider(db, NullLogger<SqlServerSearchProvider>.Instance);

        // Index Alice's document
        await provider.IndexDocumentAsync(Doc("notes", "doc1", "Original", "original content", UserAlice));

        // Update the same document (same ModuleId+EntityId) but with Alice's ownership preserved
        await provider.IndexDocumentAsync(Doc("notes", "doc1", "Updated Title", "updated content", UserAlice));

        // Alice should see the updated version
        var aliceResult = await provider.SearchAsync(Query("updated", UserAlice));
        Assert.AreEqual(1, aliceResult.TotalCount);

        // Bob should not see it
        var bobResult = await provider.SearchAsync(Query("updated", UserBob));
        Assert.AreEqual(0, bobResult.TotalCount);
    }

    [TestMethod]
    public async Task ReindexModule_DoesNotCorruptOwnership()
    {
        using var db = CreateDbContext(nameof(ReindexModule_DoesNotCorruptOwnership));
        var provider = new SqlServerSearchProvider(db, NullLogger<SqlServerSearchProvider>.Instance);

        await provider.IndexDocumentAsync(Doc("notes", "a1", "Note A", "alpha content", UserAlice));
        await provider.IndexDocumentAsync(Doc("notes", "b1", "Note B", "beta content", UserBob));
        await provider.IndexDocumentAsync(Doc("files", "a2", "File A", "alpha file", UserAlice));

        // Reindex the notes module — clears all notes entries
        await provider.ReindexModuleAsync("notes");

        // Re-index only Alice's note
        await provider.IndexDocumentAsync(Doc("notes", "a1", "Note A Reindexed", "alpha content", UserAlice));

        // Alice should see her reindexed note + her file
        var aliceNotes = await provider.SearchAsync(new SearchQuery
        {
            QueryText = "alpha",
            UserId = UserAlice,
            Page = 1,
            PageSize = 50
        });
        Assert.AreEqual(2, aliceNotes.TotalCount, "Alice should see reindexed note and file");

        // Bob should see nothing (his note was cleared and not re-added)
        var bobNotes = await provider.SearchAsync(Query("beta", UserBob));
        Assert.AreEqual(0, bobNotes.TotalCount, "Bob's notes were cleared by reindex");
    }
}
