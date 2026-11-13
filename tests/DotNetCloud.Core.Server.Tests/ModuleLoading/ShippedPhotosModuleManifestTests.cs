using DotNetCloud.Core.Data.Entities.Modules;
using DotNetCloud.Core.Modules;
using DotNetCloud.Core.Server.ModuleLoading;
using Microsoft.Extensions.Logging.Abstractions;

namespace DotNetCloud.Core.Server.Tests.ModuleLoading;

/// <summary>
/// Loads the Photos module's <b>shipped</b> <c>manifest.json</c> through the real v2 loader.
/// </summary>
/// <remarks>
/// Photos is a bundled module converted to the sandboxed model (Track C): it declares an isolated data
/// store, owns its migrations, must implement user erasure, frames its own UI and reaches the user's
/// image files only through the core's <c>IFileDirectory</c> capability. Asserting the shipped file
/// here means a manifest edit fails the build instead of the install.
/// </remarks>
[TestClass]
public class ShippedPhotosModuleManifestTests
{
    private const string ModuleId = "dotnetcloud.photos";

    private static readonly ModuleManifestLoader Loader = new(NullLogger<ModuleManifestLoader>.Instance);

    [TestMethod]
    public void ShippedManifest_IsAValidIsolatedSandboxManifest()
    {
        var result = LoadManifest();

        Assert.IsTrue(result.IsValid, string.Join("; ", result.Errors));
        Assert.IsNotNull(result.Manifest);
        Assert.AreEqual(2, result.Manifest.ManifestVersion);
        Assert.AreEqual(ModuleDataStoreMode.Isolated, result.Manifest.EffectiveDataStoreMode);
        Assert.IsTrue(result.Manifest.HandlesUserErasure == true, "the module owns user data, so it must erase it");
    }

    [TestMethod]
    public void ShippedManifest_ConsentScopesCoverEveryPersonalDataCapability()
    {
        var manifest = LoadManifest().Manifest;

        Assert.IsNotNull(manifest);
        var required = ModuleCapabilityScopes.RequiredFor(manifest.EffectiveCapabilities);
        var declared = manifest.UserConsentScopes.ToHashSet(StringComparer.Ordinal);

        foreach (var scope in required)
        {
            Assert.IsTrue(
                declared.Contains(scope),
                $"capability declares personal-data access, so the manifest must request the '{scope}' scope");
        }
    }

    [TestMethod]
    public void ShippedManifest_ReadsFilesButNeverWritesThem()
    {
        var manifest = LoadManifest().Manifest;

        Assert.IsNotNull(manifest);
        CollectionAssert.Contains(manifest.EffectiveCapabilities.ToList(), "IFileDirectory");

        // Photos never writes into the user's files: edits are non-destructive, so files:write is not
        // requested and the capability that would expose it is absent.
        CollectionAssert.DoesNotContain(manifest.EffectiveCapabilities.ToList(), "IFileWriter");
        CollectionAssert.DoesNotContain(manifest.UserConsentScopes.ToList(), ModuleScopes.FilesWrite);
    }

    [TestMethod]
    public void ShippedManifest_DoesNotSubscribeToAnotherModulesEvents()
    {
        var manifest = LoadManifest().Manifest;

        Assert.IsNotNull(manifest);
        Assert.IsNotNull(manifest.Events, "the v2 manifest must declare its event block");
        Assert.AreEqual(0, manifest.Events.Subscribes.Count, "a sandboxed module never sees the Files module's events");
    }

    [TestMethod]
    public void ShippedManifest_DeclaresItsIframeUiAndWidget()
    {
        var manifest = LoadManifest().Manifest;

        Assert.IsNotNull(manifest);
        Assert.IsNotNull(manifest.Ui, "the module serves its own UI from its host process (Track C)");
        Assert.AreEqual("iframe", manifest.Ui.Type);
        Assert.AreEqual("/", manifest.Ui.EntryPath);

        var navItem = manifest.Ui.NavItems.Single();
        Assert.AreEqual("photos", navItem.Id);
        Assert.AreEqual("/", navItem.Path);
        Assert.AreEqual("photo_library", navItem.Icon, "icons are Material names, never emoji");

        Assert.IsNotNull(manifest.Ui.Widget, "the home page keeps a Photos card, now served by the module");
        Assert.AreEqual("/widget/index.html", manifest.Ui.Widget.Path);
    }

    private static ManifestValidationResult LoadManifest()
    {
        var root = FindRepositoryRoot();
        if (root is null)
        {
            Assert.Inconclusive("Repository root (DotNetCloud.sln) not found; run the tests from the repository.");
        }

        var path = Path.Combine(root, "src", "Modules", "Photos", "manifest.json");
        Assert.IsTrue(File.Exists(path), $"the Photos module manifest was not found at {path}");

        return Loader.LoadAndValidate(path, ModuleId);
    }

    private static string? FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "DotNetCloud.sln")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        return null;
    }
}
