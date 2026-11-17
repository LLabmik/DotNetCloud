using DotNetCloud.Core.Modules.Supervisor;
using DotNetCloud.ModulePackaging;

namespace DotNetCloud.Core.Server.Tests.Packaging;

/// <summary>
/// Tests for <see cref="ModuleInstallLayout"/> directory containment.
/// </summary>
/// <remarks>
/// A module id reaches the layout straight from an admin REST route, so every directory the layout
/// derives from one must stay inside the modules root. The environment variable is set per test
/// because the layout reads it in preference to the options.
/// </remarks>
[TestClass]
[DoNotParallelize]
public class ModuleInstallLayoutTests
{
    private const string ModulesDirectoryEnvironmentVariable = "DOTNETCLOUD_MODULES_DIR";

    private string _root = null!;
    private string? _previousModulesDirectory;
    private ModuleInstallLayout _layout = null!;

    [TestInitialize]
    public void Initialize()
    {
        _previousModulesDirectory = Environment.GetEnvironmentVariable(ModulesDirectoryEnvironmentVariable);

        _root = Path.Combine(Path.GetTempPath(), $"dnc-layout-{Guid.CreateVersion7():N}");
        Directory.CreateDirectory(_root);
        Environment.SetEnvironmentVariable(ModulesDirectoryEnvironmentVariable, _root);

        _layout = new ModuleInstallLayout(new ProcessSupervisorOptions { ModulesDirectory = _root });
    }

    [TestCleanup]
    public void Cleanup()
    {
        Environment.SetEnvironmentVariable(ModulesDirectoryEnvironmentVariable, _previousModulesDirectory);

        if (Directory.Exists(_root))
        {
            try
            { Directory.Delete(_root, recursive: true); }
            catch { /* best effort */ }
        }
    }

    [TestMethod]
    public void FindPrevious_ModuleIdEscapesModulesRoot_ReturnsNull()
    {
        var outside = Path.Combine(_root, "escape");
        Directory.CreateDirectory(outside);

        var result = _layout.FindPrevious($"../{Path.GetFileName(outside)}");

        Assert.IsNull(result);
    }

    [TestMethod]
    public void FindPrevious_RetainedVersion_ReturnsDirectoryAndVersion()
    {
        var retained = Path.Combine(_root, ".previous", "test.module", "1.2.3");
        Directory.CreateDirectory(retained);

        var result = _layout.FindPrevious("test.module");

        Assert.IsNotNull(result);
        Assert.AreEqual("1.2.3", result.Value.Version);
    }

    [TestMethod]
    public void CreateStagingRoot_ModuleIdEscapesModulesRoot_Throws()
    {
        Assert.ThrowsExactly<ModulePackageException>(
            () => _layout.CreateStagingRoot("../escape"));
    }

    [TestMethod]
    public void ArchiveCurrent_ModuleIdEscapesModulesRoot_Throws()
    {
        Assert.ThrowsExactly<ModulePackageException>(
            () => _layout.ArchiveCurrent("../escape", "1.0.0"));
    }

    [TestMethod]
    public void CreateStagingRoot_ValidModuleId_CreatesDirectoryUnderStagingRoot()
    {
        var staging = _layout.CreateStagingRoot("test.module");

        var stagingBase = Path.Combine(_root, ".staging") + Path.DirectorySeparatorChar;
        Assert.IsTrue(Directory.Exists(staging));
        Assert.IsTrue(staging.StartsWith(stagingBase, StringComparison.Ordinal));
    }
}
