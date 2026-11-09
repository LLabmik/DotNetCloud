using DotNetCloud.Core.Data.Entities.Modules;
using DotNetCloud.Core.Modules;
using DotNetCloud.Core.Server.ModuleLoading;
using Microsoft.Extensions.Logging.Abstractions;

namespace DotNetCloud.Core.Server.Tests.ModuleLoading;

/// <summary>
/// Loads the AI module's <b>shipped</b> <c>manifest.json</c> through the real v2 loader.
/// </summary>
/// <remarks>
/// AI is the third bundled module converted to the sandboxed model (Track C): it declares an isolated
/// data store, owns its migrations, implements user erasure and serves its own UI + widget. Asserting
/// the shipped file here means a manifest edit fails the build instead of the install.
/// </remarks>
[TestClass]
public class ShippedAiModuleManifestTests
{
    private const string ModuleId = "dotnetcloud.ai";

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
    public void ShippedManifest_DeclaresOnlyTheCapabilitiesTheHostUses()
    {
        var manifest = LoadManifest().Manifest;

        Assert.IsNotNull(manifest);
        CollectionAssert.AreEquivalent(
            new[] { "IAuditLogger", "IModuleSettings" },
            manifest.EffectiveCapabilities.ToArray(),
            "conversations live in the module's own schema; settings are read through the settings plane, which GetSetting gates on IModuleSettings");

        // Audit exposes no personal data, so the module asks the user for nothing.
        Assert.AreEqual(0, manifest.UserConsentScopes.Count, "the module must not over-request consent scopes");
    }

    [TestMethod]
    public void ShippedManifest_DeclaresNoCrossModuleEvents()
    {
        var manifest = LoadManifest().Manifest;

        Assert.IsNotNull(manifest);
        Assert.AreEqual(0, manifest.PublishedEvents.Count, "the module publishes nothing cross-module");
        Assert.AreEqual(0, manifest.SubscribedEvents.Count);
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
        Assert.AreEqual("assistant", navItem.Id);
        Assert.AreEqual("/", navItem.Path);
        Assert.AreEqual("smart_toy", navItem.Icon, "icons are Material names, never emoji");

        Assert.IsNotNull(manifest.Ui.Widget, "the home page keeps an AI Assistant card, now served by the module");
        Assert.AreEqual("/widget/index.html", manifest.Ui.Widget.Path);
    }

    private static ManifestValidationResult LoadManifest()
    {
        var root = FindRepositoryRoot();
        if (root is null)
        {
            Assert.Inconclusive("Repository root (DotNetCloud.sln) not found; run the tests from the repository.");
        }

        var path = Path.Combine(root, "src", "Modules", "AI", "manifest.json");
        Assert.IsTrue(File.Exists(path), $"the AI module manifest was not found at {path}");

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
