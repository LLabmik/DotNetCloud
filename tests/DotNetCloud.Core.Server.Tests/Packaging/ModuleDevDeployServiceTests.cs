using DotNetCloud.Core.Data.Context;
using DotNetCloud.Core.Data.Entities.Modules;
using DotNetCloud.Core.Data.Naming;
using DotNetCloud.Core.Modules;
using DotNetCloud.Core.Modules.Supervisor;
using DotNetCloud.ModulePackaging;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;

namespace DotNetCloud.Core.Server.Tests.Packaging;

/// <summary>
/// Behaviour of <see cref="ModuleDevDeployService"/>: swap, guard, rollback, refusal.
/// </summary>
/// <remarks>
/// The process control is faked because the real one belongs to a host (the supervisor in the server,
/// the readiness poll in the CLI); what is under test here is the deploy sequence and the guarantees
/// it makes about the live directory and the installation record.
/// </remarks>
[TestClass]
[DoNotParallelize]
public class ModuleDevDeployServiceTests
{
    private const string ModulesDirectoryEnvironmentVariable = "DOTNETCLOUD_MODULES_DIR";
    private const string ModuleId = "test.module";

    private const string Manifest = """
        {
          "manifestVersion": 2,
          "id": "test.module",
          "name": "Test",
          "version": "1.2.3",
          "description": "Fixture module for the dev deploy tests.",
          "publisher": { "id": "dotnetcloud", "name": "DotNetCloud" },
          "moduleApiVersion": 1,
          "minCoreVersion": "0.7.0",
          "dataStore": "isolated",
          "handlesUserErasure": true,
          "capabilities": [],
          "userConsentScopes": [],
          "events": { "publishes": [], "subscribes": [] }
        }
        """;

    private string _root = null!;
    private string? _previousModulesDirectory;
    private CoreDbContext _db = null!;
    private FakeProcessControl _control = null!;
    private Mock<IModuleDataStoreProvisioningService> _dataStores = null!;
    private ModuleDevDeployService _service = null!;

    [TestInitialize]
    public void Initialize()
    {
        _previousModulesDirectory = Environment.GetEnvironmentVariable(ModulesDirectoryEnvironmentVariable);

        _root = Path.Combine(Path.GetTempPath(), $"dnc-dev-{Guid.CreateVersion7():N}");
        Directory.CreateDirectory(_root);
        Environment.SetEnvironmentVariable(ModulesDirectoryEnvironmentVariable, _root);

        var options = new DbContextOptionsBuilder<CoreDbContext>()
            .UseInMemoryDatabase($"ModuleDev_{Guid.CreateVersion7():N}")
            .Options;

        _db = new CoreDbContext(options, new PostgreSqlNamingStrategy());
        _control = new FakeProcessControl();
        _dataStores = new Mock<IModuleDataStoreProvisioningService>();
        _dataStores
            .Setup(service => service.EnsureProvisionedAsync(It.IsAny<string>(), It.IsAny<int?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ModuleDataStoreProvisioningResult(true, "test_module", "test_module_login", null));

        _service = CreateService(_db, _control);
    }

    [TestCleanup]
    public void Cleanup()
    {
        _db.Dispose();
        Environment.SetEnvironmentVariable(ModulesDirectoryEnvironmentVariable, _previousModulesDirectory);

        if (Directory.Exists(_root))
        {
            try
            { Directory.Delete(_root, recursive: true); }
            catch { /* best effort */ }
        }
    }

    [TestMethod]
    public async Task Deploy_ReplacesTheLiveDirectoryAndRecordsTheBuild()
    {
        await InstallLiveAsync("old");
        var published = Publish("new");

        var result = await _service.DeployAsync(Request(published));

        Assert.IsTrue(result.Success, result.Error);
        Assert.IsTrue(result.Deployed);
        Assert.AreEqual("new", ReadLive());
        Assert.AreEqual("old", File.ReadAllText(Path.Combine(
            _root, ".dev", ModuleId, "previous", "test.module.dll")));

        var state = _service.ReadState(ModuleId);
        Assert.IsNotNull(state);
        Assert.AreEqual(ModuleDevDeployService.HashTree(published), state!.ContentHash);
        Assert.AreEqual(ModuleInstallStatus.Enabled, (await ReadRecordAsync()).Status);
    }

    [TestMethod]
    public async Task Deploy_UnchangedBuild_DoesNotTouchTheModuleAgain()
    {
        await InstallLiveAsync("old");
        var published = Publish("new");

        Assert.IsTrue((await _service.DeployAsync(Request(published))).Deployed);

        var second = await _service.DeployAsync(Request(published));

        Assert.IsTrue(second.Success);
        Assert.IsFalse(second.Deployed, "an identical build must not restart the module");
        Assert.AreEqual(1, _control.StopCalls);
    }

    [TestMethod]
    public async Task Deploy_ChangedMigrationAssemblies_AreRefusedWithoutTheFlag()
    {
        await InstallLiveAsync("old");
        var first = Publish("v1", dataAssembly: "migrations-1");
        Assert.IsTrue((await _service.DeployAsync(Request(first))).Success);

        var second = Publish("v2", dataAssembly: "migrations-2");
        var refused = await _service.DeployAsync(Request(second));

        Assert.IsFalse(refused.Success);
        StringAssert.Contains(refused.Error, "--allow-schema-change");
        Assert.AreEqual("v1", ReadLive());
    }

    [TestMethod]
    public async Task Deploy_ChangedMigrationAssemblies_DeployWithTheFlag()
    {
        await InstallLiveAsync("old");
        Assert.IsTrue((await _service.DeployAsync(Request(Publish("v1", dataAssembly: "migrations-1")))).Success);

        var result = await _service.DeployAsync(
            Request(Publish("v2", dataAssembly: "migrations-2")) with { AllowSchemaChange = true });

        Assert.IsTrue(result.Success, result.Error);
        Assert.AreEqual("v2", ReadLive());
    }

    [TestMethod]
    public async Task Deploy_BuildThatNeverBecomesHealthy_RestoresThePreviousBuild()
    {
        await InstallLiveAsync("old");
        _control.UnhealthyReason = "the module exited with code 1";

        var result = await _service.DeployAsync(Request(Publish("new")));

        Assert.IsFalse(result.Success);
        StringAssert.Contains(result.Error, "exited");
        Assert.AreEqual("old", ReadLive(), "the previous build must be back in the live directory");
        Assert.AreEqual(ModuleInstallStatus.Enabled, (await ReadRecordAsync()).Status);
    }

    [TestMethod]
    public async Task Deploy_ModuleThatNeverStops_KeepsTheCurrentBuild()
    {
        await InstallLiveAsync("old");
        _control.StopTimesOut = true;

        var result = await _service.DeployAsync(Request(Publish("new")));

        Assert.IsFalse(result.Success);
        Assert.AreEqual("old", ReadLive());
        Assert.AreEqual(ModuleInstallStatus.Enabled, (await ReadRecordAsync()).Status,
            "a module that is still running must not be left disabled");
    }

    [TestMethod]
    public async Task Deploy_ForAModuleThatIsNotInstalled_InstallsItFirst()
    {
        var published = Publish("new", withManifest: true);

        var result = await _service.DeployAsync(Request(published));

        Assert.IsTrue(result.Success, result.Error);
        Assert.IsTrue(result.Deployed);
        Assert.AreEqual("new", ReadLive());

        var record = await ReadRecordAsync();
        Assert.AreEqual("1.2.3", record.Version, "the version comes from the build's manifest");
        Assert.AreEqual(ModuleDataStoreMode.Isolated, record.DataStoreMode);
        Assert.AreEqual(ModuleInstallStatus.Enabled, record.Status);

        _dataStores.Verify(
            service => service.EnsureProvisionedAsync(ModuleId, It.IsAny<int?>(), It.IsAny<CancellationToken>()),
            Times.Once,
            "an isolated module must get its schema before it runs");
    }

    [TestMethod]
    public async Task Deploy_ForAModuleWithoutAValidManifest_IsRefused()
    {
        var result = await _service.DeployAsync(Request(Publish("new")));

        Assert.IsFalse(result.Success);
        StringAssert.Contains(result.Error, "manifest.json");
        Assert.AreEqual(0, _control.StopCalls);
        Assert.IsFalse(Directory.Exists(Path.Combine(_root, ModuleId)));
    }

    [TestMethod]
    public async Task Deploy_ForARequiredModule_IsRefused()
    {
        var result = await _service.DeployAsync(Request(Publish("new", withManifest: true)) with
        {
            ModuleId = "dotnetcloud.files",
        });

        Assert.IsFalse(result.Success);
        StringAssert.Contains(result.Error, "required platform module");
        Assert.AreEqual(0, _control.StopCalls);
    }

    [TestMethod]
    public async Task Restore_WithoutAPreviousDevBuild_IsRefused()
    {
        await InstallLiveAsync("old");

        var result = await _service.RestoreAsync(ModuleId);

        Assert.IsFalse(result.Success);
        StringAssert.Contains(result.Error, "no previous dev build");
    }

    [TestMethod]
    public async Task Restore_PutsThePreviousDevBuildBack()
    {
        await InstallLiveAsync("old");
        Assert.IsTrue((await _service.DeployAsync(Request(Publish("new")))).Deployed);

        var result = await _service.RestoreAsync(ModuleId);

        Assert.IsTrue(result.Success, result.Error);
        Assert.AreEqual("old", ReadLive());
        Assert.IsNull(_service.ReadState(ModuleId), "the restored tree is unknown, so no state may claim otherwise");
    }

    [TestMethod]
    public void MigrationFingerprint_IgnoresAssembliesOutsideTheDataNaming()
    {
        var published = Publish("v1", dataAssembly: "migrations-1");
        File.WriteAllText(Path.Combine(published, "DotNetCloud.Modules.Test.Web.dll"), "web");

        Assert.AreEqual(
            ModuleDevDeployService.MigrationFingerprint(Publish("v2", dataAssembly: "migrations-1")),
            ModuleDevDeployService.MigrationFingerprint(published));
    }

    private ModuleDevDeployService CreateService(CoreDbContext db, IModuleDevProcessControl control) =>
        new(
            db,
            control,
            _dataStores.Object,
            Options.Create(new ProcessSupervisorOptions { ModulesDirectory = _root }),
            NullLogger<ModuleDevDeployService>.Instance);

    private static ModuleDevDeployRequest Request(string published) => new()
    {
        ModuleId = ModuleId,
        PublishedDirectory = published,
        SourcePath = "src/Modules/Test/DotNetCloud.Modules.Test.Host",
    };

    private async Task InstallLiveAsync(string content)
    {
        _db.InstalledModules.Add(new InstalledModule
        {
            ModuleId = ModuleId,
            Version = "1.0.0",
            InstalledAt = DateTime.UtcNow,
            Status = ModuleInstallStatus.Enabled,
            SandboxStatus = ModuleSandboxStatus.Active,
            DataStoreMode = ModuleDataStoreMode.Isolated,
        });
        await _db.SaveChangesAsync();

        var live = Path.Combine(_root, ModuleId);
        Directory.CreateDirectory(live);
        File.WriteAllText(Path.Combine(live, "test.module.dll"), content);
    }

    private string Publish(string content, string? dataAssembly = null, bool withManifest = false)
    {
        var directory = Path.Combine(_root, "publish", Guid.CreateVersion7().ToString("N"));
        Directory.CreateDirectory(directory);
        File.WriteAllText(Path.Combine(directory, "test.module.dll"), content);

        if (withManifest)
        {
            File.WriteAllText(Path.Combine(directory, "manifest.json"), Manifest);
        }

        if (dataAssembly is not null)
        {
            File.WriteAllText(Path.Combine(directory, "DotNetCloud.Modules.Test.Data.dll"), dataAssembly);
        }

        return directory;
    }

    private string ReadLive() =>
        File.ReadAllText(Path.Combine(_root, ModuleId, "test.module.dll"));

    private async Task<InstalledModule> ReadRecordAsync()
    {
        _db.ChangeTracker.Clear();
        return await _db.InstalledModules.AsNoTracking().SingleAsync(module => module.ModuleId == ModuleId);
    }

    private sealed class FakeProcessControl : IModuleDevProcessControl
    {
        public bool StopTimesOut { get; set; }

        public string? UnhealthyReason { get; set; }

        public int StopCalls { get; private set; }

        public int HealthyCalls { get; private set; }

        public Task WaitForStoppedAsync(string moduleId, CancellationToken cancellationToken = default)
        {
            StopCalls++;
            return StopTimesOut
                ? Task.FromException(new TimeoutException($"Module '{moduleId}' did not stop."))
                : Task.CompletedTask;
        }

        public Task<string?> WaitForHealthyAsync(string moduleId, CancellationToken cancellationToken = default)
        {
            HealthyCalls++;
            return Task.FromResult(UnhealthyReason);
        }
    }
}
