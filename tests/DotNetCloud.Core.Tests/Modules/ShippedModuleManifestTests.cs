using System.Text.Json;
using Microsoft.Extensions.Logging.Abstractions;
using DotNetCloud.Core.Server.ModuleLoading;

namespace DotNetCloud.Core.Tests.Modules;

/// <summary>
/// Guards the <c>manifest.json</c> shipped by every optional first-party module.
/// </summary>
/// <remarks>
/// The manifest is what the supervisor reads to decide capabilities, consent and the UI plane, and
/// what the store packages, so a manifest edit has to fail a build here rather than an install. This
/// guard moved here with the module source (2026-11-26): it used to live in
/// <c>DotNetCloud-ModuleStore</c> while the modules did. Required modules (<c>dotnetcloud.files</c>,
/// <c>.chat</c>, <c>.contacts</c>, <c>.calendar</c>, <c>.notes</c>, <c>.about</c>, <c>.extraction</c>)
/// are not optional and are never store content.
/// </remarks>
[TestClass]
public class ShippedModuleManifestTests
{
    private static readonly JsonDocumentOptions Options = new() { CommentHandling = JsonCommentHandling.Skip };

    [TestMethod]
    [DataRow("AI", "dotnetcloud.ai", "IAuditLogger|IModuleSettings")]
    [DataRow("Bookmarks", "dotnetcloud.bookmarks", "IAuditLogger")]
    [DataRow("Email", "dotnetcloud.email", "IAuditLogger|ISearchProvider|IFileDirectory|IFileWriter")]
    [DataRow("Example", "dotnetcloud.example", "INotificationService|IAuditLogger")]
    [DataRow("Photos", "dotnetcloud.photos", "IAuditLogger|ISearchProvider|ITeamDirectory|IFileDirectory|INotificationService")]
    [DataRow("Tracks", "dotnetcloud.tracks", "IAuditLogger|IUserDirectory|INotificationService|IOrganizationDirectory|IRealtimeBroadcaster")]
    public void ShippedManifest_IsAnIsolatedOptionalModule(string folder, string moduleId, string capabilities)
    {
        var path = ManifestPath(folder);

        // The core's own loader is the authority, so this also proves the manifest the store packs is
        // one the server would accept.
        var loader = new ModuleManifestLoader(NullLogger<ModuleManifestLoader>.Instance);
        var result = loader.LoadAndValidate(path, moduleId);
        Assert.IsTrue(result.IsValid, string.Join("; ", result.Errors));

        using var document = JsonDocument.Parse(File.ReadAllText(path), Options);
        var root = document.RootElement;

        Assert.AreEqual(2, root.GetProperty("manifestVersion").GetInt32(), "store packages must ship a v2 manifest");
        Assert.AreEqual(moduleId, root.GetProperty("id").GetString());
        Assert.AreEqual("dotnetcloud", root.GetProperty("publisher").GetProperty("id").GetString());
        Assert.AreEqual("isolated", root.GetProperty("dataStore").GetString(), "the module owns its schema");
        Assert.IsTrue(root.GetProperty("handlesUserErasure").GetBoolean(), "the module owns user data, so it must erase it");
        Assert.AreEqual("iframe", root.GetProperty("ui").GetProperty("type").GetString());

        var declared = root.GetProperty("capabilities")
            .EnumerateArray()
            .Select(capability => capability.GetString()!)
            .ToArray();
        CollectionAssert.AreEquivalent(
            capabilities.Split('|'),
            declared,
            "every capability is a privilege the user has to approve, so the list must stay minimal");
    }

    private static string ManifestPath(string folder) =>
        Path.Combine(FindRepositoryRoot(), "src", "Modules", folder, "manifest.json");

    private static string FindRepositoryRoot()
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

        throw new InvalidOperationException("Could not locate the repository root (DotNetCloud.sln).");
    }
}
