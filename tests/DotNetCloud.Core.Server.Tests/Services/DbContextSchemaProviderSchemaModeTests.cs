using DotNetCloud.Core.Data.Entities.Modules;
using DotNetCloud.Core.Server.ModuleLoading;
using DotNetCloud.Core.Server.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace DotNetCloud.Core.Server.Tests.Services;

/// <summary>
/// Tests for the manifest-driven schema mode of <see cref="DbContextSchemaProvider"/> (A1 tail).
/// </summary>
/// <remarks>
/// The compile-time <c>ModuleDbContextTypes</c> table only registers which DbContexts this process
/// owns. Whether a module is core-migrated or self-migrated is decided by its manifest: a v2
/// <c>dataStore: "isolated"</c> module provisions and migrates its own schema, so the core's pass
/// must skip it or two migrators race over the same schema. Modules without a v2 manifest keep the
/// legacy core-migration behaviour so nothing silently stops being migrated.
/// </remarks>
[TestClass]
public class DbContextSchemaProviderSchemaModeTests
{
    private const string RequiredModuleId = "dotnetcloud.files";
    private const string LegacyBundledModuleId = "dotnetcloud.music";

    private static DbContextSchemaProvider CreateProvider(IModuleDataStoreModeSource? modes) =>
        new(
            Mock.Of<IServiceScopeFactory>(),
            NullLogger<DbContextSchemaProvider>.Instance,
            modes);

    private static IModuleDataStoreModeSource Modes(params (string ModuleId, ModuleDataStoreMode Mode)[] entries)
    {
        var mock = new Mock<IModuleDataStoreModeSource>();
        mock.Setup(m => m.GetDataStoreMode(It.IsAny<string>())).Returns((ModuleDataStoreMode?)null);
        foreach (var (moduleId, mode) in entries)
        {
            mock.Setup(m => m.GetDataStoreMode(moduleId)).Returns(mode);
        }

        return mock.Object;
    }

    [TestMethod]
    public void IsCoreManaged_WhenRequiredModule_ReturnsTrue()
    {
        var provider = CreateProvider(null);

        Assert.IsTrue(provider.IsCoreManaged(RequiredModuleId));
    }

    [TestMethod]
    public void IsCoreManaged_WhenRequiredModuleDeclaresIsolated_StillReturnsTrue()
    {
        // A required module cannot declare a data store (manifest validation rejects it), and if one
        // ever did the core would still own the `core` schema table — never hand it to the module.
        var provider = CreateProvider(Modes((RequiredModuleId, ModuleDataStoreMode.Isolated)));

        Assert.IsTrue(provider.IsCoreManaged(RequiredModuleId));
    }

    [TestMethod]
    public void IsCoreManaged_WhenV1ManifestHasNoDataStore_ReturnsTrue()
    {
        // No entry means "unknown or v1" — the legacy path, so a bundled module keeps migrating.
        var provider = CreateProvider(Modes());

        Assert.IsTrue(provider.IsCoreManaged(LegacyBundledModuleId));
    }

    [TestMethod]
    public void IsCoreManaged_WhenManifestDeclaresIsolated_ReturnsFalse()
    {
        var provider = CreateProvider(Modes((LegacyBundledModuleId, ModuleDataStoreMode.Isolated)));

        Assert.IsFalse(provider.IsCoreManaged(LegacyBundledModuleId));
    }

    [TestMethod]
    public void IsCoreManaged_WhenManifestDeclaresNoDataStore_ReturnsFalse()
    {
        var provider = CreateProvider(Modes((LegacyBundledModuleId, ModuleDataStoreMode.None)));

        Assert.IsFalse(provider.IsCoreManaged(LegacyBundledModuleId));
    }

    [TestMethod]
    public void IsCoreManaged_WhenConvertedModuleHasNoRegisteredDbContext_ReturnsFalse()
    {
        // Converted modules ship their own .Data project and are no longer referenced by the host.
        var provider = CreateProvider(Modes(("dotnetcloud.tracks", ModuleDataStoreMode.Isolated)));

        Assert.IsFalse(provider.IsCoreManaged("dotnetcloud.tracks"));
    }

    [TestMethod]
    public void IsCoreManaged_WhenRegistryIsAbsent_FallsBackToLegacyBehaviour()
    {
        // The CLI constructs the provider without a manifest registry; it must keep migrating the
        // modules it knows about. (Asserted through a module that is *not* required.)
        var provider = CreateProvider(null);

        Assert.IsTrue(provider.IsCoreManaged(LegacyBundledModuleId));
    }
}
