using DotNetCloud.Core.Authorization;
using DotNetCloud.Core.Capabilities;
using DotNetCloud.Core.Data.Context;
using DotNetCloud.Core.Data.Entities.Modules;
using DotNetCloud.Core.Data.Naming;
using DotNetCloud.Core.Modules;
using DotNetCloud.Core.Server.ModuleLoading;
using DotNetCloud.Core.Server.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace DotNetCloud.Core.Server.Tests.Consent;

/// <summary>
/// Tests for <see cref="UserErasureService"/>: account deletion must reach every erasure-capable
/// module, retry when one is unreachable, and never report success while a module is outstanding.
/// </summary>
[TestClass]
public class UserErasureServiceTests
{
    private const string ErasureModule = "vendor.receipts";
    private const string NoDataModule = "dotnetcloud.about";

    private sealed class ManualTimeProvider(DateTimeOffset start) : TimeProvider
    {
        public DateTimeOffset Now { get; set; } = start;

        public override DateTimeOffset GetUtcNow() => Now;
    }

    private sealed class FakeDispatcher : IModuleErasureDispatcher
    {
        public Dictionary<string, ModuleUserErasureResult> Results { get; } =
            new(StringComparer.OrdinalIgnoreCase);

        public List<(string ModuleId, Guid RequestId, Guid UserId)> Calls { get; } = [];

        public Task<ModuleUserErasureResult> EraseAsync(
            string moduleId,
            Guid requestId,
            Guid userId,
            IReadOnlyCollection<string> scopes,
            CancellationToken cancellationToken = default)
        {
            Calls.Add((moduleId, requestId, userId));

            return Task.FromResult(Results.TryGetValue(moduleId, out var result)
                ? result
                : ModuleUserErasureResult.Success());
        }
    }

    private sealed class RecordingAuditLogger : IAuditLogger
    {
        public List<AuditEntry> Entries { get; } = [];

        public Task LogAsync(AuditEntry entry, CancellationToken cancellationToken = default)
        {
            Entries.Add(entry);
            return Task.CompletedTask;
        }
    }

    private string _dbName = null!;
    private ManualTimeProvider _time = null!;
    private FakeDispatcher _dispatcher = null!;
    private RecordingAuditLogger _audit = null!;
    private ModuleManifestRegistry _manifests = null!;
    private ConsentTestDoubles.FakeConsentService _consent = null!;

    [TestInitialize]
    public void Setup()
    {
        _dbName = $"UserErasureServiceTests_{Guid.NewGuid():N}";
        _time = new ManualTimeProvider(DateTimeOffset.UtcNow);
        _dispatcher = new FakeDispatcher();
        _audit = new RecordingAuditLogger();
        _manifests = new ModuleManifestRegistry();
        _consent = new ConsentTestDoubles.FakeConsentService([]);
    }

    private (UserErasureService Service, CoreDbContext Db) CreateService()
    {
        var options = new DbContextOptionsBuilder<CoreDbContext>()
            .UseInMemoryDatabase(_dbName)
            .Options;

        var db = new CoreDbContext(options, new PostgreSqlNamingStrategy());

        if (!db.InstalledModules.Any())
        {
            db.InstalledModules.AddRange(
                new InstalledModule { ModuleId = ErasureModule, Version = "1.0.0", Status = "Enabled" },
                new InstalledModule { ModuleId = NoDataModule, Version = "1.0.0", Status = "Enabled" });
            db.SaveChanges();
        }

        var service = new UserErasureService(
            db,
            _manifests,
            _dispatcher,
            _consent,
            _audit,
            _time,
            NullLogger<UserErasureService>.Instance);

        return (service, db);
    }

    private void DeclareErasureCapable(string moduleId = ErasureModule, bool handlesErasure = true) =>
        _manifests.Record(moduleId, new ModuleManifestData
        {
            Id = moduleId,
            Name = "Receipts",
            Version = "1.0.0",
            ManifestVersion = 2,
            HandlesUserErasure = handlesErasure,
            UserConsentScopes = [ModuleScopes.FilesRead],
        });

    [TestMethod]
    public async Task RequestErasureAsync_CreatesOneRowPerErasureCapableModule()
    {
        DeclareErasureCapable();
        _manifests.Record(NoDataModule, new ModuleManifestData
        {
            Id = NoDataModule,
            Name = "About",
            Version = "1.0.0",
            ManifestVersion = 2,
            HandlesUserErasure = false,
        });

        var (service, _) = CreateService();
        var userId = Guid.CreateVersion7();

        var request = await service.RequestErasureAsync(userId, Guid.CreateVersion7());

        Assert.AreEqual(1, request.Modules.Count);
        Assert.AreEqual(ErasureModule, request.Modules[0].ModuleId);
        Assert.AreEqual("Pending", request.Status);
    }

    [TestMethod]
    public async Task RequestErasureAsync_IsIdempotentForTheSameUser()
    {
        DeclareErasureCapable();
        var (service, _) = CreateService();
        var userId = Guid.CreateVersion7();

        var first = await service.RequestErasureAsync(userId, null);
        var second = await service.RequestErasureAsync(userId, null);

        Assert.AreEqual(first.RequestId, second.RequestId);
    }

    [TestMethod]
    public async Task RequestErasureAsync_DeletesConsentRowsImmediately()
    {
        DeclareErasureCapable();
        var (service, _) = CreateService();
        var userId = Guid.CreateVersion7();

        await service.RequestErasureAsync(userId, null);

        Assert.AreEqual(1, _consent.DeleteAllForUserCalls, "Consent rows are personal data and must not wait on a module.");
    }

    [TestMethod]
    public async Task ProcessPendingAsync_AcknowledgedModule_CompletesTheRequest()
    {
        DeclareErasureCapable();
        var (service, _) = CreateService();
        var userId = Guid.CreateVersion7();
        var request = await service.RequestErasureAsync(userId, null);

        await service.ProcessPendingAsync();

        var reloaded = await service.GetAsync(request.RequestId);
        Assert.IsNotNull(reloaded);
        Assert.AreEqual("Completed", reloaded.Status);
        Assert.AreEqual("Acknowledged", reloaded.Modules[0].Status);
    }

    [TestMethod]
    public async Task ProcessPendingAsync_FailedModule_StaysPendingAndRetries()
    {
        DeclareErasureCapable();
        _dispatcher.Results[ErasureModule] = ModuleUserErasureResult.Failed("database offline");

        var (service, _) = CreateService();
        var userId = Guid.CreateVersion7();
        var request = await service.RequestErasureAsync(userId, null);

        await service.ProcessPendingAsync();

        var reloaded = await service.GetAsync(request.RequestId);
        Assert.IsNotNull(reloaded);
        Assert.AreEqual("Pending", reloaded.Status);
        Assert.AreEqual("Failed", reloaded.Modules[0].Status);
        Assert.AreEqual(1, reloaded.Modules[0].Attempts);
        StringAssert.Contains(reloaded.Modules[0].LastError, "database offline");

        // The module recovers; the next pass completes the request.
        _dispatcher.Results[ErasureModule] = ModuleUserErasureResult.Success();
        await service.ProcessPendingAsync();

        var completed = await service.GetAsync(request.RequestId);
        Assert.IsNotNull(completed);
        Assert.AreEqual("Completed", completed.Status);
        Assert.AreEqual(2, completed.Modules[0].Attempts);
    }

    [TestMethod]
    public async Task ProcessPendingAsync_AfterTheRetryWindow_MarksTheRequestFailed()
    {
        DeclareErasureCapable();
        _dispatcher.Results[ErasureModule] = ModuleUserErasureResult.Failed("module refused");

        var (service, _) = CreateService();
        var userId = Guid.CreateVersion7();
        var request = await service.RequestErasureAsync(userId, null);

        _time.Now = _time.Now.Add(UserErasureService.RetryWindow).Add(TimeSpan.FromMinutes(1));

        await service.ProcessPendingAsync();

        var reloaded = await service.GetAsync(request.RequestId);
        Assert.IsNotNull(reloaded);
        Assert.AreEqual("Failed", reloaded.Status);
    }

    [TestMethod]
    public async Task OverrideAsync_ForceCompletesAndAudits()
    {
        DeclareErasureCapable();
        _dispatcher.Results[ErasureModule] = ModuleUserErasureResult.Failed("module is gone");

        var (service, _) = CreateService();
        var userId = Guid.CreateVersion7();
        var adminId = Guid.CreateVersion7();
        var request = await service.RequestErasureAsync(userId, adminId);

        await service.ProcessPendingAsync();

        Assert.IsTrue(await service.OverrideAsync(request.RequestId, adminId));

        var reloaded = await service.GetAsync(request.RequestId);
        Assert.IsNotNull(reloaded);
        Assert.AreEqual("Overridden", reloaded.Status);
        Assert.AreEqual(AuditAction.ModuleUserErasureOverridden, _audit.Entries.Single().Action);
    }

    [TestMethod]
    public async Task OverrideAsync_UnknownRequest_ReturnsFalse()
    {
        var (service, _) = CreateService();

        Assert.IsFalse(await service.OverrideAsync(Guid.CreateVersion7(), Guid.CreateVersion7()));
        Assert.AreEqual(0, _audit.Entries.Count);
    }

    [TestMethod]
    public async Task ListAsync_DefaultExcludesTerminalRequests()
    {
        DeclareErasureCapable();
        var (service, _) = CreateService();

        var first = await service.RequestErasureAsync(Guid.CreateVersion7(), null);
        await service.ProcessPendingAsync();
        await service.RequestErasureAsync(Guid.CreateVersion7(), null);

        var outstanding = await service.ListAsync();
        var all = await service.ListAsync(includeCompleted: true);

        Assert.AreEqual(1, outstanding.Count);
        Assert.AreEqual(2, all.Count);
        Assert.AreEqual("Completed", all.Single(request => request.RequestId == first.RequestId).Status);
    }

    [TestMethod]
    public async Task ProcessPendingAsync_ModuleDoesNotGetCalledWithAModuleScopedRequestId()
    {
        DeclareErasureCapable();
        var (service, _) = CreateService();
        var userId = Guid.CreateVersion7();

        var request = await service.RequestErasureAsync(userId, null);
        await service.ProcessPendingAsync();

        var call = _dispatcher.Calls.Single();
        Assert.AreEqual(ErasureModule, call.ModuleId);
        Assert.AreEqual(request.RequestId, call.RequestId);
        Assert.AreEqual(userId, call.UserId);
    }
}
