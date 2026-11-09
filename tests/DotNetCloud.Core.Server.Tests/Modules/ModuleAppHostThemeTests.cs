namespace DotNetCloud.Core.Server.Tests.Modules;

/// <summary>
/// Drift guard between the shell's theme mechanism and the module-frame host script.
/// </summary>
/// <remarks>
/// The shell applies dark mode with the <c>dark-mode</c> class (<c>theme.js</c>), while the frame host
/// used to read a <c>data-theme</c> attribute that nothing set — so every module iframe rendered in
/// light mode no matter what the user chose. These tests read both scripts so a future change to
/// either side fails the build instead of the browser.
/// </remarks>
[TestClass]
public class ModuleAppHostThemeTests
{
    [TestMethod]
    public void ShellTheme_IsAppliedAsTheDarkModeClass()
    {
        var theme = ReadScript("theme.js");

        StringAssert.Contains(
            theme,
            "classList.toggle(\"dark-mode\"",
            "if the shell ever switches to another mechanism, module-app-host.js must be updated too");
    }

    [TestMethod]
    public void FrameHost_ReadsTheShellThemeMechanism()
    {
        var host = ReadScript("module-app-host.js");

        StringAssert.Contains(
            host,
            "classList.contains('dark-mode')",
            "the frame must derive its theme from the same signal the shell applies");
        StringAssert.Contains(
            host,
            "currentTheme()",
            "the theme read must stay a single helper");
    }

    [TestMethod]
    public void FrameHost_PushesThemeChangesIntoTheFrame()
    {
        var host = ReadScript("module-app-host.js");

        StringAssert.Contains(
            host,
            "new MutationObserver(",
            "a theme toggle must reach the frame without a remount");
        StringAssert.Contains(host, "type: 'dnc:theme'", "the bridge listens for dnc:theme");
    }

    private static string ReadScript(string fileName)
    {
        var root = FindRepositoryRoot();
        if (root is null)
        {
            Assert.Inconclusive("Repository root (DotNetCloud.sln) not found; run the tests from the repository.");
        }

        var path = Path.Combine(root, "src", "UI", "DotNetCloud.UI.Web", "wwwroot", "js", fileName);
        Assert.IsTrue(File.Exists(path), $"Expected script was not found at {path}");
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
