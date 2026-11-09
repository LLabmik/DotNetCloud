namespace DotNetCloud.Core.Server.Tests.Modules;

/// <summary>
/// Guards the shell layout rule that lets a framed module app use the whole content area.
/// </summary>
/// <remarks>
/// The shell caps page content at a reading width (<c>.content-area { max-width: 1400px }</c>) and
/// pads it. Full-bleed in-core pages opt out through <c>:has(&lt;layout class&gt;)</c>, but a module's
/// own layout lives inside an iframe, so those selectors can never match — without a dedicated rule
/// the frame is narrower and inset, and a module app uses only part of the available room.
/// </remarks>
[TestClass]
public class ModuleAppPageLayoutTests
{
    [TestMethod]
    public void ShellCss_MakesTheFramedModuleFullBleed()
    {
        var css = ReadShellCss();

        StringAssert.Contains(
            css,
            ".page-content:has(.module-app-host)",
            "the module page must opt out of the reading-width container");
        StringAssert.Contains(
            css,
            ".page-content:has(.module-app-host) .content-area",
            "the content area must lose its max-width and padding for module pages");
        StringAssert.Contains(css, ".content-area {", "the shared container rule must still exist");
    }

    [TestMethod]
    public void ShellCss_VersionIsBumpedWhenTheStylesheetChanges()
    {
        var app = ReadShellMarkup();

        Assert.IsTrue(
            System.Text.RegularExpressions.Regex.IsMatch(app, @"css/app\.css\?v=\d{8}-\d{2}"),
            "app.css is served with a long cache; keep a ?v=yyyyMMdd-nn query so edits reach browsers");
    }

    private static string ReadShellCss() => Read("wwwroot", "css", "app.css");

    private static string ReadShellMarkup() => Read("Components", "App.razor");

    private static string Read(params string[] relativePath)
    {
        var root = FindRepositoryRoot();
        if (root is null)
        {
            Assert.Inconclusive("Repository root (DotNetCloud.sln) not found; run the tests from the repository.");
        }

        var path = Path.Combine([root, "src", "UI", "DotNetCloud.UI.Web", .. relativePath]);
        Assert.IsTrue(File.Exists(path), $"Expected file was not found at {path}");
        return File.ReadAllText(path);
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
