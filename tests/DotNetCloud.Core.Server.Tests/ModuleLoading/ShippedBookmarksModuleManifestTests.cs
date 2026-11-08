using DotNetCloud.Core.Data.Entities.Modules;
using DotNetCloud.Core.Modules;
using DotNetCloud.Core.Server.ModuleLoading;
using Microsoft.Extensions.Logging.Abstractions;

namespace DotNetCloud.Core.Server.Tests.ModuleLoading;

/// <summary>
/// Loads the Bookmarks module's <b>shipped</b> <c>manifest.json</c> through the real v2 loader.
/// </summary>
/// <remarks>
/// Bookmarks is the first bundled module converted to the sandboxed model (Track C): it declares an
/// isolated data store, owns its migrations and must implement user erasure. Asserting the shipped
/// file here means a manifest edit fails the build instead of the install.
/// </remarks>
[TestClass]
public class ShippedBookmarksModuleManifestTests
{
    private const string ModuleId = "dotnetcloud.bookmarks";

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

        CollectionAssert.IsSubsetOf(required.ToList(), manifest.UserConsentScopes.ToList());
        CollectionAssert.Contains(manifest.UserConsentScopes.ToList(), "profile");
        CollectionAssert.Contains(manifest.UserConsentScopes.ToList(), "notifications:send");
        CollectionAssert.Contains(manifest.UserConsentScopes.ToList(), "bookmarks:read");
        CollectionAssert.Contains(manifest.UserConsentScopes.ToList(), "bookmarks:write");
    }

    [TestMethod]
    public void ShippedManifest_DeclaresTheCapabilitiesTheHostUses()
    {
        var manifest = LoadManifest().Manifest;

        Assert.IsNotNull(manifest);
        CollectionAssert.Contains(manifest.EffectiveCapabilities.ToList(), "INotificationService");
        CollectionAssert.Contains(manifest.EffectiveCapabilities.ToList(), "IAuditLogger");
        CollectionAssert.Contains(manifest.EffectiveCapabilities.ToList(), "ISearchProvider");
    }

    private static ManifestValidationResult LoadManifest()
    {
        var root = FindRepositoryRoot();
        if (root is null)
        {
            Assert.Inconclusive("Repository root (DotNetCloud.sln) not found; run the tests from the repository.");
        }

        var path = Path.Combine(root, "src", "Modules", "Bookmarks", "manifest.json");
        Assert.IsTrue(File.Exists(path), $"the Bookmarks module manifest was not found at {path}");

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
