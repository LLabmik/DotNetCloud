using System.Text.RegularExpressions;

namespace DotNetCloud.Modules.Bookmarks.Tests;

/// <summary>
/// Guards the module's own web UI assets (phase A5 UI plane, Track C).
/// </summary>
/// <remarks>
/// The core frames <c>/apps/dotnetcloud.bookmarks/</c> and strips that prefix before forwarding, so
/// every asset and API reference in the framed documents must be <b>relative</b>. An absolute
/// <c>/assets/bookmarks-ui.js</c> would leave the module and hit a core route — a 404 at best, a
/// core script at worst. These tests fail the build rather than the browser.
/// </remarks>
[TestClass]
public class BookmarksUiAssetsTests
{
    private const string UiDirectory = "ui";

    [TestMethod]
    public void IndexHtml_ReferencesEveryAssetRelatively()
    {
        var html = ReadUiFile("index.html");

        var references = Regex.Matches(html, "(?:src|href)=\"([^\"]+)\"")
            .Select(match => match.Groups[1].Value)
            .ToList();

        Assert.IsTrue(references.Count >= 3, "index.html must load the bridge, its script and its stylesheet");

        foreach (var reference in references)
        {
            Assert.IsFalse(
                reference.StartsWith('/'),
                $"'{reference}' is root-relative: the framed document lives under /apps/<moduleId>/ and only relative paths stay inside it.");
            Assert.IsFalse(
                reference.Contains("/apps/", StringComparison.OrdinalIgnoreCase),
                $"'{reference}' hard-codes the proxy prefix, which the module must never know about.");
            Assert.IsFalse(
                reference.Contains("://", StringComparison.Ordinal),
                $"'{reference}' is an absolute URL; module UI assets must be same-origin.");
        }
    }

    [TestMethod]
    public void IndexHtml_LoadsTheBridgeAndTheModuleScript()
    {
        var html = ReadUiFile("index.html");

        StringAssert.Contains(html, "dncModuleBridge.js");
        StringAssert.Contains(html, "assets/bookmarks-ui.js");
        StringAssert.Contains(html, "assets/bookmarks-ui.css");
    }

    [TestMethod]
    public void ModuleScripts_DoNotInjectHtml()
    {
        // Bookmark titles, URLs, notes and preview text are user data. Building DOM with innerHTML
        // would make them a stored-XSS sink inside the core's origin.
        string[] sinks = [".innerHTML", ".outerHTML", "insertAdjacentHTML", "document.write"];

        foreach (var file in new[] { "assets/bookmarks-ui.js", "widget/widget.js" })
        {
            var script = ReadUiFile(file);

            foreach (var sink in sinks)
            {
                Assert.IsFalse(
                    script.Contains(sink, StringComparison.Ordinal),
                    $"{file} must build DOM with textContent/createElement, never {sink}");
            }
        }
    }

    [TestMethod]
    public void ModuleScripts_UseOnlyRelativeApiPaths()
    {
        var script = ReadUiFile("assets/bookmarks-ui.js");

        Assert.IsFalse(
            script.Contains("'/api/", StringComparison.Ordinal),
            "API calls must be relative so they stay inside /apps/<moduleId>/");
        StringAssert.Contains(script, "var API = 'api/v1/bookmarks'");

        var widgetScript = ReadUiFile("widget/widget.js");
        StringAssert.Contains(widgetScript, "'../api/v1/bookmarks");
        Assert.IsFalse(
            widgetScript.Contains("'/api/", StringComparison.Ordinal),
            "the widget frame must call the API relatively as well");
    }

    [TestMethod]
    public void WidgetPage_ExistsAtThePathTheManifestDeclares()
    {
        var manifest = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "manifest.json"));

        StringAssert.Contains(
            manifest,
            "\"widget\"",
            "the module declares a home-page widget, so the shell renders a sandboxed card for it");

        Assert.IsTrue(
            File.Exists(Path.Combine(AppContext.BaseDirectory, UiDirectory, "widget", "index.html")),
            "the widget document declared by ui.widget.path must ship with the module");
    }

    [TestMethod]
    public void Bridge_IsShippedAsAStaticAsset()
    {
        var bridge = ReadUiFile("dncModuleBridge.js");

        // The bridge is the SDK's single copy; this proves the host copies the real thing and not a stub.
        StringAssert.Contains(bridge, "window.dncModuleBridge = bridge");
        StringAssert.Contains(bridge, "dnc:ready");
    }

    private static string ReadUiFile(string relativePath)
    {
        var path = Path.Combine(AppContext.BaseDirectory, UiDirectory, relativePath);
        Assert.IsTrue(File.Exists(path), $"Expected UI asset was not copied to the test output: {path}");
        return File.ReadAllText(path);
    }
}
