using DotNetCloud.Client.Core.SyncIgnore;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace DotNetCloud.Client.Core.Tests.SyncIgnore;

[TestClass]
public class SyncIgnoreParserTests
{
    private static SyncIgnoreParser CreateParser() => new();

    // ── Built-in defaults ───────────────────────────────────────────────────

    [TestMethod]
    public void IsIgnored_BuiltInDefaults_IgnoresOsJunk()
    {
        var parser = CreateParser();

        Assert.IsTrue(parser.IsIgnored(".DS_Store"),              ".DS_Store at root");
        Assert.IsTrue(parser.IsIgnored("subdir/.DS_Store"),       ".DS_Store in subdir");
        Assert.IsTrue(parser.IsIgnored("Thumbs.db"),              "Thumbs.db");
        Assert.IsTrue(parser.IsIgnored("nested/dir/Thumbs.db"),   "Thumbs.db nested");
        Assert.IsTrue(parser.IsIgnored("report.tmp"),             "*.tmp");
        Assert.IsTrue(parser.IsIgnored("data/cache.temp"),        "*.temp");
        Assert.IsTrue(parser.IsIgnored("~$lockfile.docx"),        "~$ prefix");
        Assert.IsTrue(parser.IsIgnored("desktop.ini"),            "desktop.ini");
    }

    [TestMethod]
    public void IsIgnored_BuiltInDefaults_IgnoresVcsDirectories()
    {
        var parser = CreateParser();

        Assert.IsTrue(parser.IsIgnored(".git/config"),          ".git/ contents");
        Assert.IsTrue(parser.IsIgnored(".svn/entries"),         ".svn/ contents");
        Assert.IsTrue(parser.IsIgnored(".hg/manifest"),         ".hg/ contents");
    }

    [TestMethod]
    public void IsIgnored_BuiltInDefaults_IgnoresPackageManagerDirs()
    {
        var parser = CreateParser();

        Assert.IsTrue(parser.IsIgnored("node_modules/express/index.js"), "node_modules/");
        Assert.IsTrue(parser.IsIgnored("frontend/node_modules/vue.js"),  "nested node_modules/");
        Assert.IsTrue(parser.IsIgnored(".nuget/packages/log4net.dll"),   ".nuget/");
    }

    [TestMethod]
    public void IsIgnored_RegularFiles_NotIgnored()
    {
        var parser = CreateParser();

        Assert.IsFalse(parser.IsIgnored("README.md"));
        Assert.IsFalse(parser.IsIgnored("src/Main.cs"));
        Assert.IsFalse(parser.IsIgnored("docs/guide.pdf"));
        Assert.IsFalse(parser.IsIgnored("data.json"));
    }

    // ── User pattern ─────────────────────────────────────────────────────────

    [TestMethod]
    public void IsIgnored_UserPattern_MatchesAddedGlob()
    {
        var parser = CreateParser();
        parser.SetUserPatterns(["*.log", "build/"]);

        Assert.IsTrue(parser.IsIgnored("app.log"),               "*.log at root");
        Assert.IsTrue(parser.IsIgnored("logs/error.log"),        "*.log in subdir");
        Assert.IsTrue(parser.IsIgnored("build/output.exe"),      "build/ directory content");
        Assert.IsTrue(parser.IsIgnored("src/build/output.exe"),  "nested build/");
    }

    [TestMethod]
    public void IsIgnored_UserPattern_NegationOverridesDefault()
    {
        // "!important.tmp" should un-ignore a file that the built-in *.tmp rule would ignore.
        var parser = CreateParser();
        parser.SetUserPatterns(["!important.tmp"]);

        Assert.IsFalse(parser.IsIgnored("important.tmp"), "negation un-ignores file");
        Assert.IsTrue(parser.IsIgnored("other.tmp"),      "non-negated *.tmp still ignored");
    }

    // ── .syncignore file loading ──────────────────────────────────────────────

    [TestMethod]
    public async Task Initialize_LoadsSyncIgnoreFile_AppliesUserRules()
    {
        var root = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            await File.WriteAllLinesAsync(Path.Combine(root, ".syncignore"),
                ["# comment", "", "*.log", "build/"]);

            var parser = CreateParser();
            parser.Initialize(root);

            CollectionAssert.Contains(parser.UserPatterns.ToList(), "*.log");
            CollectionAssert.Contains(parser.UserPatterns.ToList(), "build/");
            Assert.IsTrue(parser.IsIgnored("app.log"));
            Assert.IsTrue(parser.IsIgnored("build/out.exe"));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [TestMethod]
    public async Task SaveAsync_WritesUserPatternsToSyncIgnoreFile()
    {
        var root = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            var parser = CreateParser();
            parser.SetUserPatterns(["*.log", "dist/"]);

            await parser.SaveAsync(root);

            var lines = await File.ReadAllLinesAsync(Path.Combine(root, ".syncignore"));
            CollectionAssert.Contains(lines, "*.log");
            CollectionAssert.Contains(lines, "dist/");
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    // ── Glob pattern edge cases ───────────────────────────────────────────────

    [TestMethod]
    public void IsIgnored_GitignoreGlob_MatchesCorrectly()
    {
        var parser = CreateParser();
        parser.SetUserPatterns(["**/*.log", "build/", "node_modules/"]);

        // **/*.log
        Assert.IsTrue(parser.IsIgnored("error.log"),                  "root log");
        Assert.IsTrue(parser.IsIgnored("logs/error.log"),             "subdir log");
        Assert.IsTrue(parser.IsIgnored("a/b/c/trace.log"),            "deep log");
        Assert.IsFalse(parser.IsIgnored("logfile.txt"),               "no .log extension");

        // build/
        Assert.IsTrue(parser.IsIgnored("build/artifact.dll"),         "build root");
        Assert.IsTrue(parser.IsIgnored("project/build/artifact.dll"), "nested build");

        // node_modules/
        Assert.IsTrue(parser.IsIgnored("node_modules/react/index.js"), "node_modules root");
    }

    [TestMethod]
    public void IsIgnored_BackslashPath_NormalisedCorrectly()
    {
        // Windows paths with backslashes should be treated identically to forward slashes.
        var parser = CreateParser();

        Assert.IsTrue(parser.IsIgnored(@"subdir\.DS_Store"));
        Assert.IsTrue(parser.IsIgnored(@"node_modules\react\index.js"));
        Assert.IsTrue(parser.IsIgnored(@"data\cache.tmp"));
    }
}
