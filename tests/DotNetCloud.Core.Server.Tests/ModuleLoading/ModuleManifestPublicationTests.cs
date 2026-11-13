namespace DotNetCloud.Core.Server.Tests.ModuleLoading;

/// <summary>
/// Guards that every module ships its <c>manifest.json</c>, because the supervisor learns a module's
/// capabilities from that file alone.
/// </summary>
/// <remarks>
/// <para>
/// <c>dotnet publish</c> only copies the manifest when the Host project declares it as a
/// <c>Content</c> item. Until 2026-11-13 only the Track C modules did, so the v1 modules deployed
/// without one, the supervisor fell back to <c>ModuleManifestLoader.CreateDefaultManifest</c> — which
/// declares no capabilities — and <c>EnsureManifestGrantsAsync</c> therefore created no grants.
/// </para>
/// <para>
/// That is harmless only while capability enforcement is off. Once
/// <c>ModuleAccessEvaluator</c> enforces grants, every capability call from those modules is denied:
/// on the first production install it broke music (notifications/search) and video, whose mounted
/// media lives in admin shared folders and needs <c>ShareAccessMembershipResolver</c> →
/// <c>GetGroupsForUser</c> (<c>ITeamDirectory</c>) before the file is served.
/// </para>
/// </remarks>
[TestClass]
public class ModuleManifestPublicationTests
{
    [TestMethod]
    public void EveryModuleWithAManifest_PublishesItFromItsHostProject()
    {
        var root = FindRepositoryRoot();
        Assert.IsNotNull(root, "Could not locate the repository root (DotNetCloud.sln).");

        var modulesRoot = Path.Combine(root, "src", "Modules");
        var manifestPaths = Directory
            .GetDirectories(modulesRoot)
            .Select(moduleDirectory => Path.Combine(moduleDirectory, "manifest.json"))
            .Where(File.Exists)
            .ToList();

        Assert.IsTrue(manifestPaths.Count >= 9, $"Expected at least 9 module manifests, found {manifestPaths.Count}.");

        List<string> unpublished = [];

        foreach (var manifestPath in manifestPaths)
        {
            var moduleDirectory = Path.GetDirectoryName(manifestPath)!;
            var moduleName = Path.GetFileName(moduleDirectory);
            var hostProject = Path.Combine(
                moduleDirectory,
                $"DotNetCloud.Modules.{moduleName}.Host",
                $"DotNetCloud.Modules.{moduleName}.Host.csproj");

            if (!File.Exists(hostProject))
            {
                continue;
            }

            var project = File.ReadAllText(hostProject);
            if (!project.Contains("manifest.json", StringComparison.Ordinal)
                || !project.Contains("<Content", StringComparison.Ordinal))
            {
                unpublished.Add(moduleName);
            }
        }

        Assert.AreEqual(
            0,
            unpublished.Count,
            $"These modules do not publish their manifest.json, so their capabilities are never granted: " +
            $"{string.Join(", ", unpublished)}. Add a Content item for ..\\manifest.json to the Host .csproj.");
    }

    /// <summary>
    /// Music and Video stream files out of mounted admin shared folders, and the mounted-file path in
    /// <c>DownloadService</c> resolves the caller's group memberships before it serves the file.
    /// Without <c>ITeamDirectory</c> the folder lookup returns null and playback fails with
    /// "FileNode … was not found".
    /// </summary>
    [TestMethod]
    public void MediaModules_DeclareTeamDirectory()
    {
        var root = FindRepositoryRoot();
        Assert.IsNotNull(root, "Could not locate the repository root (DotNetCloud.sln).");

        foreach (var module in new[] { "Music", "Video" })
        {
            var path = Path.Combine(root, "src", "Modules", module, "manifest.json");
            var manifest = File.ReadAllText(path);

            StringAssert.Contains(
                manifest,
                "ITeamDirectory",
                $"{module} serves files from mounted admin shared folders; " +
                "ShareAccessMembershipResolver needs ITeamDirectory or playback 404s.");
        }
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
