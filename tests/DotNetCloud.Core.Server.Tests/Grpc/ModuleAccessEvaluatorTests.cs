using DotNetCloud.Core.Capabilities;
using DotNetCloud.Core.Data.Context;
using DotNetCloud.Core.Data.Entities.Modules;
using DotNetCloud.Core.Data.Naming;
using DotNetCloud.Core.Modules;
using DotNetCloud.Core.Server.Grpc.ModuleAccess;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

namespace DotNetCloud.Core.Server.Tests.Grpc;

/// <summary>
/// Tests for <see cref="ModuleAccessEvaluator"/>: tier handling, fail-closed behaviour, the grant
/// cache and its invalidation hook.
/// </summary>
[TestClass]
public class ModuleAccessEvaluatorTests
{
    private const string ModuleId = "dotnetcloud.notes";

    private string _dbName = null!;

    [TestInitialize]
    public void Setup() => _dbName = $"ModuleAccessEvaluatorTests_{Guid.NewGuid():N}";

    private ServiceProvider CreateProvider(bool registerDbContext = true)
    {
        var services = new ServiceCollection();

        if (registerDbContext)
        {
            services.AddDbContext<CoreDbContext>(options =>
                options.UseInMemoryDatabase(_dbName));
            services.AddSingleton<ITableNamingStrategy>(new PostgreSqlNamingStrategy());
        }

        return services.BuildServiceProvider();
    }

    private static ModuleAccessEvaluator CreateEvaluator(ServiceProvider provider, TimeProvider? clock = null) =>
        new(
            provider.GetRequiredService<IServiceScopeFactory>(),
            clock ?? TimeProvider.System,
            NullLogger<ModuleAccessEvaluator>.Instance);

    private static string Api(string rpc) => CapabilityCatalog.Rpc(CapabilityCatalog.ModuleApiService, rpc);

    private static string Legacy(string rpc) =>
        CapabilityCatalog.Rpc(CapabilityCatalog.CoreCapabilitiesService, rpc);

    private void Grant(params string[] capabilities)
    {
        using var provider = CreateProvider();
        using var scope = provider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<CoreDbContext>();

        foreach (var capability in capabilities)
        {
            db.ModuleCapabilityGrants.Add(new ModuleCapabilityGrant
            {
                ModuleId = ModuleId,
                CapabilityName = capability,
                GrantedAt = DateTime.UtcNow,
                Status = ModuleCapabilityGrantStatus.Granted,
            });
        }

        db.SaveChanges();
    }

    [TestMethod]
    public async Task EvaluateAsync_PublicCapability_IsAllowedWithoutAGrant()
    {
        using var provider = CreateProvider();
        var decision = await CreateEvaluator(provider).EvaluateAsync(ModuleId, Api("GetUser"));

        Assert.IsTrue(decision.IsAllowed);
        Assert.AreEqual(nameof(DotNetCloud.Core.Capabilities.IUserDirectory), decision.Capability);
        Assert.IsNull(decision.DenialReason);
    }

    [TestMethod]
    public async Task EvaluateAsync_SelfServiceRpc_IsAllowedWithoutAGrant()
    {
        using var provider = CreateProvider();
        var decision = await CreateEvaluator(provider).EvaluateAsync(ModuleId, Api("ReportHealth"));

        Assert.IsTrue(decision.IsAllowed);
        Assert.IsNull(decision.Capability);
    }

    [TestMethod]
    public async Task EvaluateAsync_NonModuleService_IsOutOfScopeAndAllowed()
    {
        using var provider = CreateProvider();
        var decision = await CreateEvaluator(provider)
            .EvaluateAsync(ModuleId, "/dotnetcloud.files.v1.FilesUploadStream/Upload");

        Assert.IsTrue(decision.IsAllowed);
    }

    [TestMethod]
    public async Task EvaluateAsync_RestrictedWithoutGrant_IsDenied()
    {
        using var provider = CreateProvider();
        var decision = await CreateEvaluator(provider).EvaluateAsync(ModuleId, Api("LogAudit"));

        Assert.IsFalse(decision.IsAllowed);
        Assert.AreEqual(nameof(DotNetCloud.Core.Capabilities.IAuditLogger), decision.Capability);
        Assert.AreEqual(ModuleAccessDenialReason.CapabilityNotGranted, decision.DenialReason);
    }

    [TestMethod]
    public async Task EvaluateAsync_RestrictedWithGrant_IsAllowed()
    {
        Grant(nameof(DotNetCloud.Core.Capabilities.IAuditLogger));

        using var provider = CreateProvider();
        var decision = await CreateEvaluator(provider).EvaluateAsync(ModuleId, Api("LogAudit"));

        Assert.IsTrue(decision.IsAllowed);
    }

    [TestMethod]
    public async Task EvaluateAsync_GrantForAnotherCapability_IsDenied()
    {
        Grant(nameof(DotNetCloud.Core.Capabilities.ISearchProvider));

        using var provider = CreateProvider();
        var decision = await CreateEvaluator(provider).EvaluateAsync(ModuleId, Api("LogAudit"));

        Assert.IsFalse(decision.IsAllowed);
        Assert.AreEqual(ModuleAccessDenialReason.CapabilityNotGranted, decision.DenialReason);
    }

    [TestMethod]
    public async Task EvaluateAsync_UnmappedModuleRpc_IsDeniedAndFailsClosed()
    {
        using var provider = CreateProvider();
        var decision = await CreateEvaluator(provider).EvaluateAsync(ModuleId, Api("SomethingNew"));

        Assert.IsFalse(decision.IsAllowed);
        Assert.IsNull(decision.Capability);
        Assert.AreEqual(ModuleAccessDenialReason.UnmappedRpc, decision.DenialReason);
    }

    [TestMethod]
    public async Task EvaluateAsync_MissingModuleIdentity_IsDenied()
    {
        using var provider = CreateProvider();
        var decision = await CreateEvaluator(provider).EvaluateAsync(null, Api("LogAudit"));

        Assert.IsFalse(decision.IsAllowed);
        Assert.AreEqual(ModuleAccessDenialReason.UnknownModule, decision.DenialReason);
    }

    [TestMethod]
    public async Task EvaluateAsync_GrantLookupFails_IsDeniedNotAllowed()
    {
        // No CoreDbContext registered: resolving it throws, and the evaluator must fail closed.
        using var provider = CreateProvider(registerDbContext: false);
        var decision = await CreateEvaluator(provider).EvaluateAsync(ModuleId, Api("LogAudit"));

        Assert.IsFalse(decision.IsAllowed);
        Assert.AreEqual(ModuleAccessDenialReason.EvaluationFailed, decision.DenialReason);
    }

    [TestMethod]
    public async Task EvaluateAsync_GrantRemovedAfterCaching_StaysAllowedUntilInvalidated()
    {
        Grant(nameof(DotNetCloud.Core.Capabilities.IAuditLogger));

        using var provider = CreateProvider();
        var clock = new ManualTimeProvider(DateTimeOffset.UtcNow);
        var evaluator = CreateEvaluator(provider, clock);

        Assert.IsTrue((await evaluator.EvaluateAsync(ModuleId, Api("LogAudit"))).IsAllowed);

        // Revoke in the database; the cached snapshot still allows the call.
        using (var scope = provider.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<CoreDbContext>();
            db.ModuleCapabilityGrants.RemoveRange(db.ModuleCapabilityGrants);
            db.SaveChanges();
        }

        Assert.IsTrue((await evaluator.EvaluateAsync(ModuleId, Api("LogAudit"))).IsAllowed);

        // The revoke path invalidates the cache, which must take effect immediately.
        evaluator.Invalidate(ModuleId);
        Assert.IsFalse((await evaluator.EvaluateAsync(ModuleId, Api("LogAudit"))).IsAllowed);
    }

    [TestMethod]
    public async Task EvaluateAsync_CacheExpires_ReloadsGrants()
    {
        Grant(nameof(DotNetCloud.Core.Capabilities.IAuditLogger));

        using var provider = CreateProvider();
        var clock = new ManualTimeProvider(DateTimeOffset.UtcNow);
        var evaluator = CreateEvaluator(provider, clock);

        Assert.IsTrue((await evaluator.EvaluateAsync(ModuleId, Api("LogAudit"))).IsAllowed);

        using (var scope = provider.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<CoreDbContext>();
            db.ModuleCapabilityGrants.RemoveRange(db.ModuleCapabilityGrants);
            db.SaveChanges();
        }

        clock.Advance(ModuleAccessEvaluator.CacheTtl + TimeSpan.FromSeconds(1));

        Assert.IsFalse((await evaluator.EvaluateAsync(ModuleId, Api("LogAudit"))).IsAllowed);
    }

    [TestMethod]
    public async Task EvaluateAsync_LegacyAndCanonicalSurfaces_UseTheSameGrant()
    {
        Grant(nameof(DotNetCloud.Core.Capabilities.ISearchProvider));

        using var provider = CreateProvider();
        var evaluator = CreateEvaluator(provider);

        Assert.IsTrue((await evaluator.EvaluateAsync(ModuleId, Legacy("SubmitSearchIndex"))).IsAllowed);
        Assert.IsTrue((await evaluator.EvaluateAsync(ModuleId, Api("SubmitSearchIndex"))).IsAllowed);
    }

    [TestMethod]
    public async Task EvaluateAsync_TeamDirectoryGrant_CoversBothGroupAndTeamRpcs()
    {
        Grant(nameof(DotNetCloud.Core.Capabilities.ITeamDirectory));

        using var provider = CreateProvider();
        var evaluator = CreateEvaluator(provider);

        Assert.IsTrue((await evaluator.EvaluateAsync(ModuleId, Api("GetGroup"))).IsAllowed);
        Assert.IsTrue((await evaluator.EvaluateAsync(ModuleId, Api("GetTeamsForUser"))).IsAllowed);
        Assert.IsTrue((await evaluator.EvaluateAsync(ModuleId, Legacy("GetGroupsForUser"))).IsAllowed);
    }

    [TestMethod]
    public async Task EvaluateAsync_PendingDecision_IsDenied()
    {
        // A store-tier module's request is recorded as Pending; recording it must not grant it.
        GrantWithStatus(nameof(DotNetCloud.Core.Capabilities.IAuditLogger), ModuleCapabilityGrantStatus.Pending);

        using var provider = CreateProvider();
        var decision = await CreateEvaluator(provider).EvaluateAsync(ModuleId, Api("LogAudit"));

        Assert.IsFalse(decision.IsAllowed);
        Assert.AreEqual(ModuleAccessDenialReason.CapabilityNotGranted, decision.DenialReason);
    }

    [TestMethod]
    public async Task EvaluateAsync_RejectedDecision_IsDenied()
    {
        GrantWithStatus(nameof(DotNetCloud.Core.Capabilities.IAuditLogger), ModuleCapabilityGrantStatus.Rejected);

        using var provider = CreateProvider();
        var decision = await CreateEvaluator(provider).EvaluateAsync(ModuleId, Api("LogAudit"));

        Assert.IsFalse(decision.IsAllowed);
        Assert.AreEqual(ModuleAccessDenialReason.CapabilityNotGranted, decision.DenialReason);
    }

    private void GrantWithStatus(string capability, string status)
    {
        using var provider = CreateProvider();
        using var scope = provider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<CoreDbContext>();

        db.ModuleCapabilityGrants.Add(new ModuleCapabilityGrant
        {
            ModuleId = ModuleId,
            CapabilityName = capability,
            GrantedAt = DateTime.UtcNow,
            Status = status,
        });

        db.SaveChanges();
    }

    /// <summary>Controllable clock for cache-expiry tests.</summary>
    private sealed class ManualTimeProvider(DateTimeOffset start) : TimeProvider
    {
        private DateTimeOffset _now = start;

        public override DateTimeOffset GetUtcNow() => _now;

        public void Advance(TimeSpan delta) => _now += delta;
    }
}
