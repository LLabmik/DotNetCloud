using DotNetCloud.Core.Data.Entities.Modules;
using DotNetCloud.Core.Server.ModuleLoading;
using Microsoft.Extensions.Logging.Abstractions;

namespace DotNetCloud.Core.Server.Tests.ModuleLoading;

/// <summary>
/// Loads the Example module's <b>shipped</b> <c>manifest.json</c> through the real v2 loader.
/// </summary>
/// <remarks>
/// <para>
/// The Example module is the reference third-party shape: it is packaged, signed and installed from
/// the store, so its manifest is not documentation — the store validator and the core's installer
/// both reject it if it drifts. Asserting it here (with the production loader, not a reimplementation
/// of the rules) makes a manifest change fail the build instead of the install.
/// </para>
/// <para>
/// What this catches that the module's own tests cannot: the strict v2 JSON rules, the F18 rule
/// (capabilities → declared consent scopes), the Material-icon rule for <c>ui.navItems[].icon</c>,
/// and the sandbox requirements (<c>dataStore</c>, <c>handlesUserErasure</c>).
/// </para>
/// </remarks>
[TestClass]
public class ShippedExampleModuleManifestTests
{
    private const string ModuleId = "dotnetcloud.example";

    private static readonly ModuleManifestLoader Loader = new(NullLogger<ModuleManifestLoader>.Instance);

    [TestMethod]
    public void ShippedManifest_IsValidVersion2SandboxManifest()
    {
        var result = LoadExampleManifest();

        Assert.IsTrue(result.IsValid, string.Join("; ", result.Errors));
        Assert.IsNotNull(result.Manifest);
        Assert.AreEqual(2, result.Manifest.ManifestVersion);
        Assert.AreEqual(ModuleDataStoreMode.Isolated, result.Manifest.EffectiveDataStoreMode);
        Assert.IsTrue(result.Manifest.HandlesUserErasure == true, "an isolated module must implement user erasure");
    }

    [TestMethod]
    public void ShippedManifest_DeclaresTheIframeUiPlane()
    {
        var ui = LoadExampleManifest().Manifest?.Ui;

        Assert.IsNotNull(ui, "the Example module serves its own UI (phase A5)");
        Assert.AreEqual("iframe", ui.Type);
        Assert.IsTrue(ui.NavItems.Count > 0);
        Assert.AreEqual("note", ui.NavItems[0].Icon, "icons are Material names, never emoji");
    }

    [TestMethod]
    public void ShippedManifest_DeclaresConsentScopesForItsCapabilities()
    {
        var manifest = LoadExampleManifest().Manifest;

        Assert.IsNotNull(manifest);
        CollectionAssert.Contains(manifest.UserConsentScopes.ToList(), "notifications:send");
        CollectionAssert.DoesNotContain(manifest.UserConsentScopes.ToList(), "files:read");
    }

    [TestMethod]
    public void ShippedManifest_DeclaresTheAuditCapabilityItUses()
    {
        var manifest = LoadExampleManifest().Manifest;

        Assert.IsNotNull(manifest);
        CollectionAssert.Contains(manifest.EffectiveCapabilities.ToList(), "IAuditLogger");
    }

    private static ManifestValidationResult LoadExampleManifest()
    {
        var root = FindRepositoryRoot();
        if (root is null)
        {
            Assert.Inconclusive("Repository root (DotNetCloud.sln) not found; run the tests from the repository.");
        }

        var path = Path.Combine(root, "src", "Modules", "Example", "manifest.json");
        Assert.IsTrue(File.Exists(path), $"the Example module manifest was not found at {path}");

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
