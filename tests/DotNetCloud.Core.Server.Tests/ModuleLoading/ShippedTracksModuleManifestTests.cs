using DotNetCloud.Core.Data.Entities.Modules;
using DotNetCloud.Core.Modules;
using DotNetCloud.Core.Server.ModuleLoading;
using Microsoft.Extensions.Logging.Abstractions;

namespace DotNetCloud.Core.Server.Tests.ModuleLoading;

/// <summary>
/// Loads the Tracks module's <b>shipped</b> <c>manifest.json</c> through the real v2 loader.
/// </summary>
/// <remarks>
/// Tracks is the largest bundled module converted to the sandboxed model (Track C): it declares an
/// isolated data store, owns its migrations, implements user erasure and serves its own framed UI plus
/// a Home-page widget. Asserting the shipped file here means a manifest edit fails the build instead of
/// the install.
/// </remarks>
[TestClass]
public class ShippedTracksModuleManifestTests
{
    private const string ModuleId = "dotnetcloud.tracks";

    private static readonly ModuleManifestLoader Loader = new(NullLogger<ModuleManifestLoader>.Instance);

    [TestMethod]
    public void ShippedManifest_IsAValidIsolatedSandboxManifest()
    {
        var result = LoadManifest();

        Assert.IsTrue(result.IsValid, string.Join("; ", result.Errors));
        Assert.IsNotNull(result.Manifest);
        Assert.AreEqual(2, result.Manifest.ManifestVersion);
        Assert.AreEqual(ModuleDataStoreMode.Isolated, result.Manifest.EffectiveDataStoreMode);
        Assert.IsTrue(result.Manifest.HandlesUserErasure == true, "the module owns products, work items and everything beneath them");
    }

    [TestMethod]
    public void ShippedManifest_DeclaresOnlyTheCapabilitiesTheHostUses()
    {
        var manifest = LoadManifest().Manifest;

        Assert.IsNotNull(manifest);
        CollectionAssert.AreEquivalent(
            new[]
            {
                "IAuditLogger",          // SOC 2 audit trail for work-item changes
                "IUserDirectory",        // display names in activity, CSV import, mentions and /api/v1/me
                "INotificationService",  // assignment and mention notifications
                "IOrganizationDirectory",// organizations are core data; the host cannot read core.organizations
                "IRealtimeBroadcaster",  // board state broadcasts to connected clients
            },
            manifest.EffectiveCapabilities.ToArray(),
            "ICurrentUserContext was declared but never resolved anywhere in the module and was dropped");

        CollectionAssert.AreEquivalent(
            new[] { "profile", "notifications:send" },
            manifest.UserConsentScopes.ToArray(),
            "profile is what IUserDirectory exposes; notifications:send is what INotificationService sends");
    }

    [TestMethod]
    public void ShippedManifest_DeclaresItsOwnEventsAndSubscribesToNone()
    {
        var manifest = LoadManifest().Manifest;

        Assert.IsNotNull(manifest);
        Assert.IsTrue(manifest.EffectivePublishedEvents.Contains("WorkItemCreatedEvent"), "the module raises work-item events");
        Assert.IsTrue(manifest.EffectivePublishedEvents.Contains("SprintCompletedEvent"));
        Assert.AreEqual(
            0,
            manifest.EffectiveSubscribedEvents.Count,
            "a sandboxed module cannot see another module's events (Files/Chat subscriptions were in-process only)");
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
        Assert.AreEqual("tracks", navItem.Id);
        Assert.AreEqual("/", navItem.Path);
        Assert.AreEqual("assignment", navItem.Icon, "icons are Material names, never emoji");

        Assert.IsNotNull(manifest.Ui.Widget, "the home page keeps a Tracks card, now served by the module");
        Assert.AreEqual("/widget/index.html", manifest.Ui.Widget.Path);
    }

    private static ManifestValidationResult LoadManifest()
    {
        var root = FindRepositoryRoot();
        if (root is null)
        {
            Assert.Inconclusive("Repository root (DotNetCloud.sln) not found; run the tests from the repository.");
        }

        var path = Path.Combine(root, "src", "Modules", "Tracks", "manifest.json");
        Assert.IsTrue(File.Exists(path), $"the Tracks module manifest was not found at {path}");

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
