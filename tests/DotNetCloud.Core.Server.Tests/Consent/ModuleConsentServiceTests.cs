using DotNetCloud.Core.Data.Context;
using DotNetCloud.Core.Data.Entities.Modules;
using DotNetCloud.Core.Data.Naming;
using DotNetCloud.Core.Server.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;

namespace DotNetCloud.Core.Server.Tests.Consent;

/// <summary>
/// Tests for <see cref="ModuleConsentService"/>: the consent plane must grant, withhold, expire,
/// and revoke accurately, and a revocation must be visible on the very next read.
/// </summary>
[TestClass]
public class ModuleConsentServiceTests
{
    private const string ModuleId = "vendor.receipts";

    private sealed class ManualTimeProvider(DateTimeOffset start) : TimeProvider
    {
        public DateTimeOffset Now { get; set; } = start;

        public override DateTimeOffset GetUtcNow() => Now;
    }

    private string _dbName = null!;
    private IMemoryCache _cache = null!;
    private ManualTimeProvider _time = null!;

    [TestInitialize]
    public void Setup()
    {
        _dbName = $"ModuleConsentServiceTests_{Guid.NewGuid():N}";
        _cache = new MemoryCache(new MemoryCacheOptions());
        _time = new ManualTimeProvider(DateTimeOffset.UtcNow);
    }

    [TestCleanup]
    public void Cleanup() => _cache.Dispose();

    private (ModuleConsentService Service, CoreDbContext Db) CreateService()
    {
        var options = new DbContextOptionsBuilder<CoreDbContext>()
            .UseInMemoryDatabase(_dbName)
            .Options;

        var db = new CoreDbContext(options, new PostgreSqlNamingStrategy());
        var service = new ModuleConsentService(db, _cache, _time, NullLogger<ModuleConsentService>.Instance);
        return (service, db);
    }

    private static Guid SeedModule(CoreDbContext db, string moduleId = ModuleId, string status = "Enabled")
    {
        var userId = Guid.CreateVersion7();

        if (!db.InstalledModules.Any(module => module.ModuleId == moduleId))
        {
            db.InstalledModules.Add(new InstalledModule
            {
                ModuleId = moduleId,
                Version = "1.0.0",
                Status = status,
                InstalledAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
            });
            db.SaveChanges();
        }

        return userId;
    }

    [TestMethod]
    public async Task GetGrantedScopesAsync_NoRows_IsEmpty()
    {
        var (service, db) = CreateService();
        var userId = SeedModule(db);

        var granted = await service.GetGrantedScopesAsync(ModuleId, userId);

        Assert.AreEqual(0, granted.Count);
    }

    [TestMethod]
    public async Task GrantAsync_ThenRead_ReturnsTheScope()
    {
        var (service, db) = CreateService();
        var userId = SeedModule(db);

        var written = await service.GrantAsync(ModuleId, userId, ["files:read", "profile"]);

        Assert.AreEqual(2, written);
        var granted = await service.GetGrantedScopesAsync(ModuleId, userId);
        CollectionAssert.AreEquivalent(new[] { "files:read", "profile" }, granted.ToArray());
    }

    [TestMethod]
    public async Task GrantAsync_RepeatedForSameScope_DoesNotDuplicate()
    {
        var (service, db) = CreateService();
        var userId = SeedModule(db);

        await service.GrantAsync(ModuleId, userId, ["files:read"]);
        await service.GrantAsync(ModuleId, userId, ["files:read"]);

        var rows = db.ModuleUserConsents.Count(consent => consent.ModuleId == ModuleId && consent.UserId == userId);
        Assert.AreEqual(1, rows);
    }

    [TestMethod]
    public async Task GrantAsync_UnknownScope_IsIgnored()
    {
        var (service, db) = CreateService();
        var userId = SeedModule(db);

        var written = await service.GrantAsync(ModuleId, userId, ["made-up:read"]);

        Assert.AreEqual(0, written);
        Assert.AreEqual(0, (await service.GetGrantedScopesAsync(ModuleId, userId)).Count);
    }

    [TestMethod]
    public async Task GetMissingScopesAsync_ReportsOnlyTheUngrantedOnes()
    {
        var (service, db) = CreateService();
        var userId = SeedModule(db);
        await service.GrantAsync(ModuleId, userId, ["files:read"]);

        var missing = await service.GetMissingScopesAsync(ModuleId, userId, ["files:read", "profile"]);

        CollectionAssert.AreEqual(new[] { "profile" }, missing.ToArray());
    }

    [TestMethod]
    public async Task ExpiredConsent_IsNotReturned()
    {
        var (service, db) = CreateService();
        var userId = SeedModule(db);

        await service.GrantAsync(
            ModuleId,
            userId,
            ["files:read"],
            expiresAtUtc: _time.Now.UtcDateTime.AddMinutes(1));

        Assert.IsTrue(await service.HasConsentAsync(ModuleId, userId, "files:read"));

        _time.Now = _time.Now.AddMinutes(2);

        // A fresh cache: the service's cache is real-time based, so only a new instance can tell
        // whether the *stored* row is considered expired.
        _cache.Dispose();
        _cache = new MemoryCache(new MemoryCacheOptions());
        var (fresh, _) = CreateService();

        Assert.IsFalse(await fresh.HasConsentAsync(ModuleId, userId, "files:read"));
    }

    [TestMethod]
    public async Task RevokeAsync_StopsTheNextReadDespiteTheCache()
    {
        var (service, db) = CreateService();
        var userId = SeedModule(db);
        await service.GrantAsync(ModuleId, userId, ["files:read"]);

        // Prime the cache.
        Assert.IsTrue(await service.HasConsentAsync(ModuleId, userId, "files:read"));

        var removed = await service.RevokeAsync(ModuleId, userId, "files:read");

        Assert.AreEqual(1, removed);
        Assert.IsFalse(
            await service.HasConsentAsync(ModuleId, userId, "files:read"),
            "Revocation must invalidate the cached grant set, not wait for it to expire.");
    }

    [TestMethod]
    public async Task RevokeAllAsync_RemovesEveryScope()
    {
        var (service, db) = CreateService();
        var userId = SeedModule(db);
        await service.GrantAsync(ModuleId, userId, ["files:read", "profile"]);

        var removed = await service.RevokeAllAsync(ModuleId, userId);

        Assert.AreEqual(2, removed);
        Assert.AreEqual(0, (await service.GetGrantedScopesAsync(ModuleId, userId)).Count);
    }

    [TestMethod]
    public async Task RevokeAllForModuleAsync_DoesNotAffectOtherModules()
    {
        var (service, db) = CreateService();
        var userId = SeedModule(db);
        SeedModule(db, "vendor.other");
        await service.GrantAsync(ModuleId, userId, ["files:read"]);
        await service.GrantAsync("vendor.other", userId, ["files:read"]);

        var removed = await service.RevokeAllForModuleAsync(ModuleId);

        Assert.AreEqual(1, removed);
        Assert.AreEqual(0, (await service.GetGrantedScopesAsync(ModuleId, userId)).Count);
        Assert.AreEqual(1, (await service.GetGrantedScopesAsync("vendor.other", userId)).Count);
    }

    [TestMethod]
    public async Task DeleteAllForUserAsync_IsScopedToThatUser()
    {
        var (service, db) = CreateService();
        var userId = SeedModule(db);
        var otherUser = Guid.CreateVersion7();
        await service.GrantAsync(ModuleId, userId, ["files:read"]);
        await service.GrantAsync(ModuleId, otherUser, ["files:read"]);

        var removed = await service.DeleteAllForUserAsync(userId);

        Assert.AreEqual(1, removed);
        Assert.AreEqual(0, (await service.GetGrantedScopesAsync(ModuleId, userId)).Count);
        Assert.AreEqual(1, (await service.GetGrantedScopesAsync(ModuleId, otherUser)).Count);
    }

    [TestMethod]
    public async Task ListForUserAsync_GroupsScopesPerModule()
    {
        var (service, db) = CreateService();
        var userId = SeedModule(db);
        await service.GrantAsync(ModuleId, userId, ["files:read", "profile"]);

        var apps = await service.ListForUserAsync(userId);

        Assert.AreEqual(1, apps.Count);
        Assert.AreEqual(ModuleId, apps[0].ModuleId);
        Assert.AreEqual(2, apps[0].Scopes.Count);
        Assert.IsTrue(apps[0].IsModuleInstalled);
    }

    [TestMethod]
    public async Task GetOverviewAsync_CountsDistinctUsersPerScope()
    {
        var (service, db) = CreateService();
        var first = SeedModule(db);
        var second = Guid.CreateVersion7();
        await service.GrantAsync(ModuleId, first, ["files:read"]);
        await service.GrantAsync(ModuleId, second, ["files:read"]);
        await service.GrantAsync(ModuleId, second, ["profile"]);

        var overview = await service.GetOverviewAsync(ModuleId);

        Assert.AreEqual(2, overview.ConsentingUsers);
        Assert.AreEqual(2, overview.ScopeCounts.Single(count => count.Scope == "files:read").UserCount);
        Assert.AreEqual(1, overview.ScopeCounts.Single(count => count.Scope == "profile").UserCount);
    }

    /// <summary>
    /// Consent is per user, not per module: one user approving a scope must never let the core serve
    /// another user's data under that scope. The cache is keyed by (module, user) too, so a warm
    /// entry for one user must not answer for another.
    /// </summary>
    [TestMethod]
    public async Task GrantAsync_ForOneUser_DoesNotConsentAnotherUser()
    {
        var (service, db) = CreateService();
        var alice = SeedModule(db);
        var bob = Guid.CreateVersion7();

        await service.GrantAsync(ModuleId, alice, ["files:read"]);

        // Warm the cache for Alice first, so a leaked/shared cache entry would show up as a false
        // positive for Bob rather than being masked by a cold read.
        Assert.IsTrue(await service.HasConsentAsync(ModuleId, alice, "files:read"));
        Assert.AreEqual(1, (await service.GetGrantedScopesAsync(ModuleId, alice)).Count);

        Assert.AreEqual(0, (await service.GetGrantedScopesAsync(ModuleId, bob)).Count,
            "Bob must not inherit Alice's consent.");
        Assert.IsFalse(await service.HasConsentAsync(ModuleId, bob, "files:read"),
            "Bob's calls must be refused with MODULE_CONSENT_REQUIRED until Bob consents himself.");

        var missing = await service.GetMissingScopesAsync(ModuleId, bob, ["files:read"]);
        CollectionAssert.AreEqual(new[] { "files:read" }, missing.ToArray());
    }

    /// <summary>
    /// The unique index is (module, user, scope), so the same scope granted by two users is two
    /// independent rows — never one row that covers everybody.
    /// </summary>
    [TestMethod]
    public async Task GrantAsync_SameScopeForTwoUsers_StoresOneRowPerUser()
    {
        var (service, db) = CreateService();
        var alice = SeedModule(db);
        var bob = Guid.CreateVersion7();

        await service.GrantAsync(ModuleId, alice, ["files:read"]);
        await service.GrantAsync(ModuleId, bob, ["files:read"]);

        var rows = db.ModuleUserConsents.Where(row => row.Scope == "files:read").ToList();

        Assert.AreEqual(2, rows.Count);
        CollectionAssert.AreEquivalent(new[] { alice, bob }, rows.Select(row => row.UserId).ToArray());

        // Revoking one user leaves the other untouched.
        await service.RevokeAsync(ModuleId, alice, "files:read");

        Assert.IsFalse(await service.HasConsentAsync(ModuleId, alice, "files:read"));
        Assert.IsTrue(await service.HasConsentAsync(ModuleId, bob, "files:read"),
            "Revoking Alice's consent must not touch Bob's.");
    }
}
