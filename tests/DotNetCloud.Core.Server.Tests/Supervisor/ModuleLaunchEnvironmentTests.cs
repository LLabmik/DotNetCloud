using DotNetCloud.Core.Modules.Supervisor;
using DotNetCloud.Core.Server.ModuleLoading;
using DotNetCloud.Core.Server.Supervisor;

namespace DotNetCloud.Core.Server.Tests.Supervisor;

/// <summary>
/// Tests for the module launch environment split (sandbox directories vs legacy core directories).
/// </summary>
[TestClass]
public class ModuleLaunchEnvironmentTests
{
    private const string SandboxedModuleId = "org.example.customapp";

    private string _dataRoot = null!;

    [TestInitialize]
    public void Initialize()
    {
        _dataRoot = Path.Combine(Path.GetTempPath(), "dnc-launch-env-tests", Guid.CreateVersion7().ToString());
        Directory.CreateDirectory(_dataRoot);
    }

    [TestCleanup]
    public void Cleanup()
    {
        try
        {
            if (Directory.Exists(_dataRoot))
            {
                Directory.Delete(_dataRoot, recursive: true);
            }
        }
        catch (IOException)
        {
            // Best effort cleanup; the temp directory is disposable.
        }
    }

    private static ModuleManifestData Manifest(string id, int manifestVersion, string? dataStore = "isolated") =>
        new()
        {
            Id = id,
            Name = "Test Module",
            Version = "1.0.0",
            ManifestVersion = manifestVersion,
            DataStore = dataStore,
        };

    [TestMethod]
    public void IsSandboxed_V2NonRequiredModule_ReturnsTrue()
    {
        Assert.IsTrue(ModuleLaunchEnvironment.IsSandboxed(Manifest(SandboxedModuleId, 2)));
    }

    [TestMethod]
    public void IsSandboxed_V1NonRequiredModule_ReturnsFalse()
    {
        Assert.IsFalse(ModuleLaunchEnvironment.IsSandboxed(Manifest("dotnetcloud.bookmarks", 1)));
    }

    [TestMethod]
    public void IsSandboxed_V2RequiredModule_ReturnsFalse()
    {
        Assert.IsFalse(ModuleLaunchEnvironment.IsSandboxed(Manifest("dotnetcloud.chat", 2)));
    }

    [TestMethod]
    public void Resolve_SandboxedIsolated_ProvidesModuleDirectoriesAndNoLegacyDirectories()
    {
        var plan = ModuleLaunchEnvironment.Resolve(
            Manifest(SandboxedModuleId, 2),
            new ProcessSupervisorOptions(),
            SandboxedModuleId,
            _dataRoot,
            "Host=localhost;Database=module;Username=dnc_mod",
            useLegacyLayout: false);

        Assert.IsNotNull(plan.Layout);
        Assert.IsFalse(plan.LegacyDirectoriesRefused);
        Assert.AreEqual(plan.Layout.ConfigDirectory,
            plan.Variables[ModuleLaunchEnvironment.ModuleConfigDirectoryVariable]);
        Assert.AreEqual(plan.Layout.DataDirectory,
            plan.Variables[ModuleLaunchEnvironment.ModuleDataDirectoryVariable]);
        Assert.AreEqual("Host=localhost;Database=module;Username=dnc_mod",
            plan.Variables[ModuleLaunchEnvironment.ModuleDbConnectionStringVariable]);

        Assert.IsFalse(plan.Variables.ContainsKey(ModuleLaunchEnvironment.LegacyConfigDirectoryVariable));
        Assert.IsFalse(plan.Variables.ContainsKey(ModuleLaunchEnvironment.LegacyDataDirectoryVariable));
    }

    [TestMethod]
    public void Resolve_SandboxedDataStoreNone_OmitsConnectionString()
    {
        var plan = ModuleLaunchEnvironment.Resolve(
            Manifest(SandboxedModuleId, 2, dataStore: "none"),
            new ProcessSupervisorOptions(),
            SandboxedModuleId,
            _dataRoot,
            moduleConnectionString: null,
            useLegacyLayout: false);

        Assert.IsFalse(plan.Variables.ContainsKey(ModuleLaunchEnvironment.ModuleDbConnectionStringVariable));
        Assert.IsTrue(plan.Variables.ContainsKey(ModuleLaunchEnvironment.ModuleConfigDirectoryVariable));
    }

    [TestMethod]
    public void Resolve_SandboxedConnectionStringEmpty_OmitsConnectionStringVariable()
    {
        var plan = ModuleLaunchEnvironment.Resolve(
            Manifest(SandboxedModuleId, 2),
            new ProcessSupervisorOptions(),
            SandboxedModuleId,
            _dataRoot,
            moduleConnectionString: string.Empty,
            useLegacyLayout: false);

        Assert.IsFalse(plan.Variables.ContainsKey(ModuleLaunchEnvironment.ModuleDbConnectionStringVariable));
    }

    [TestMethod]
    public void Resolve_SandboxedWithLegacyLayoutFallbackAndOverrideDisabled_RefusesLegacyDirectories()
    {
        var plan = ModuleLaunchEnvironment.Resolve(
            Manifest(SandboxedModuleId, 2),
            new ProcessSupervisorOptions { AllowLegacyAccess = false },
            SandboxedModuleId,
            _dataRoot,
            moduleConnectionString: null,
            useLegacyLayout: true);

        Assert.IsNull(plan.Layout);
        Assert.IsTrue(plan.LegacyDirectoriesRefused);
        Assert.AreEqual(0, plan.Variables.Count);
    }

    [TestMethod]
    public void Resolve_SandboxedWithLegacyLayoutFallbackAndOverrideEnabled_ForwardsLegacyDirectories()
    {
        var plan = ModuleLaunchEnvironment.Resolve(
            Manifest(SandboxedModuleId, 2),
            new ProcessSupervisorOptions { AllowLegacyAccess = true },
            SandboxedModuleId,
            _dataRoot,
            moduleConnectionString: null,
            useLegacyLayout: true,
            legacyConfigDirectory: "/etc/dotnetcloud",
            legacyDataDirectory: "/var/lib/dotnetcloud");

        Assert.IsNull(plan.Layout);
        Assert.IsFalse(plan.LegacyDirectoriesRefused);
        Assert.AreEqual("/etc/dotnetcloud",
            plan.Variables[ModuleLaunchEnvironment.LegacyConfigDirectoryVariable]);
        Assert.AreEqual("/var/lib/dotnetcloud",
            plan.Variables[ModuleLaunchEnvironment.LegacyDataDirectoryVariable]);
        Assert.IsFalse(plan.Variables.ContainsKey(ModuleLaunchEnvironment.ModuleConfigDirectoryVariable));
    }

    [TestMethod]
    public void Resolve_V1ModuleWithOverrideEnabled_ForwardsLegacyDirectories()
    {
        var plan = ModuleLaunchEnvironment.Resolve(
            Manifest("dotnetcloud.bookmarks", 1),
            new ProcessSupervisorOptions { AllowLegacyAccess = true },
            "dotnetcloud.bookmarks",
            _dataRoot,
            moduleConnectionString: null,
            useLegacyLayout: true,
            legacyConfigDirectory: "/etc/dotnetcloud",
            legacyDataDirectory: null);

        Assert.IsNull(plan.Layout);
        Assert.AreEqual("/etc/dotnetcloud",
            plan.Variables[ModuleLaunchEnvironment.LegacyConfigDirectoryVariable]);
        Assert.IsFalse(plan.Variables.ContainsKey(ModuleLaunchEnvironment.LegacyDataDirectoryVariable));
    }

    [TestMethod]
    public void Resolve_V1ModuleWithOverrideDisabled_RefusesLegacyDirectories()
    {
        var plan = ModuleLaunchEnvironment.Resolve(
            Manifest("dotnetcloud.bookmarks", 1),
            new ProcessSupervisorOptions { AllowLegacyAccess = false },
            "dotnetcloud.bookmarks",
            _dataRoot,
            moduleConnectionString: null,
            useLegacyLayout: true);

        Assert.IsTrue(plan.LegacyDirectoriesRefused);
        Assert.AreEqual(0, plan.Variables.Count);
    }

    [TestMethod]
    public void Resolve_SandboxedNonRequired_UsesModuleDirectoryUnderDataRoot()
    {
        var plan = ModuleLaunchEnvironment.Resolve(
            Manifest(SandboxedModuleId, 2),
            new ProcessSupervisorOptions(),
            SandboxedModuleId,
            _dataRoot,
            moduleConnectionString: null,
            useLegacyLayout: false);

        Assert.IsNotNull(plan.Layout);
        var expectedRoot = Path.GetFullPath(Path.Combine(_dataRoot, "modules", SandboxedModuleId));
        StringAssert.StartsWith(plan.Layout.ConfigDirectory, expectedRoot);
        StringAssert.StartsWith(plan.Layout.DataDirectory, expectedRoot);
    }
}
