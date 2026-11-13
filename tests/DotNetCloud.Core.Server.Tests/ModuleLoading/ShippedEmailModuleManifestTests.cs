using DotNetCloud.Core.Data.Entities.Modules;
using DotNetCloud.Core.Modules;
using DotNetCloud.Core.Server.ModuleLoading;
using Microsoft.Extensions.Logging.Abstractions;

namespace DotNetCloud.Core.Server.Tests.ModuleLoading;

/// <summary>
/// Loads the Email module's <b>shipped</b> <c>manifest.json</c> through the real v2 loader.
/// </summary>
/// <remarks>
/// Email is the third bundled module converted to the sandboxed model (Track C): it declares an
/// isolated data store, owns its migrations and implements user erasure. Asserting the shipped file
/// here means a manifest edit fails the build instead of the install.
/// </remarks>
[TestClass]
public class ShippedEmailModuleManifestTests
{
    private const string ModuleId = "dotnetcloud.email";

    private static readonly ModuleManifestLoader Loader = new(NullLogger<ModuleManifestLoader>.Instance);

    [TestMethod]
    public void ShippedManifest_IsAValidIsolatedSandboxManifest()
    {
        var result = LoadManifest();

        Assert.IsTrue(result.IsValid, string.Join("; ", result.Errors));
        Assert.IsNotNull(result.Manifest);
        Assert.AreEqual(2, result.Manifest.ManifestVersion);
        Assert.AreEqual("dotnetcloud", result.Manifest.Publisher?.Id, "a store package must name its publisher");
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
        CollectionAssert.Contains(manifest.UserConsentScopes.ToList(), "mail:read");
        CollectionAssert.Contains(manifest.UserConsentScopes.ToList(), "mail:write");
        CollectionAssert.Contains(manifest.UserConsentScopes.ToList(), "files:read");
        CollectionAssert.Contains(manifest.UserConsentScopes.ToList(), "files:write");
    }

    [TestMethod]
    public void ShippedManifest_DeclaresOnlyTheCapabilitiesTheHostUses()
    {
        var manifest = LoadManifest().Manifest;

        Assert.IsNotNull(manifest);
        CollectionAssert.Contains(manifest.EffectiveCapabilities.ToList(), "IAuditLogger");
        CollectionAssert.Contains(manifest.EffectiveCapabilities.ToList(), "ISearchProvider");

        // Browsing and saving the user's files goes through the core (IFileDirectory read,
        // IFileWriter write); the module still has no in-process storage or contact capability.
        CollectionAssert.Contains(manifest.EffectiveCapabilities.ToList(), "IFileDirectory");
        CollectionAssert.Contains(manifest.EffectiveCapabilities.ToList(), "IFileWriter");
        CollectionAssert.DoesNotContain(manifest.EffectiveCapabilities.ToList(), "IStorageProvider");
        CollectionAssert.DoesNotContain(manifest.EffectiveCapabilities.ToList(), "IContactDirectory");
        CollectionAssert.DoesNotContain(manifest.EffectiveCapabilities.ToList(), "INotificationService");
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
        Assert.AreEqual("email", navItem.Id);
        Assert.AreEqual("/", navItem.Path);
        Assert.AreEqual("mail", navItem.Icon, "icons are Material names, never emoji");

        Assert.IsNotNull(manifest.Ui.Widget, "the home page keeps an Email card, now served by the module");
        Assert.AreEqual("/widget/index.html", manifest.Ui.Widget.Path);
    }

    private static ManifestValidationResult LoadManifest()
    {
        var root = FindRepositoryRoot();
        if (root is null)
        {
            Assert.Inconclusive("Repository root (DotNetCloud.sln) not found; run the tests from the repository.");
        }

        var path = Path.Combine(root, "src", "Modules", "Email", "manifest.json");
        Assert.IsTrue(File.Exists(path), $"the Email module manifest was not found at {path}");

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
