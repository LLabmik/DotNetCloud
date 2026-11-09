using System.Text.RegularExpressions;

namespace DotNetCloud.Modules.AI.Tests;

/// <summary>
/// Guards the module's own web UI assets (phase A5 UI plane, Track C).
/// </summary>
/// <remarks>
/// The core frames <c>/apps/dotnetcloud.ai/</c> and strips that prefix before forwarding, so every
/// asset and API reference in the framed documents must be <b>relative</b>. An absolute
/// <c>/assets/ai-ui.js</c> would leave the module and hit a core route — a 404 at best, a core script
/// at worst. These tests fail the build rather than the browser.
/// </remarks>
[TestClass]
public class AiUiAssetsTests
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
    public void IndexHtml_LoadsTheBridgeAndTheModuleAssets()
    {
        var html = ReadUiFile("index.html");

        StringAssert.Contains(html, "dncModuleBridge.js");
        StringAssert.Contains(html, "assets/ai-ui.js");
        StringAssert.Contains(html, "assets/ai-ui.css");
    }

    [TestMethod]
    public void ModuleScripts_DoNotInjectHtml()
    {
        // Model output, conversation titles and messages are untrusted. Building DOM with innerHTML
        // would make them a stored-XSS sink inside the core's origin — the markdown renderer builds
        // text nodes and elements instead.
        string[] sinks = [".innerHTML", ".outerHTML", "insertAdjacentHTML", "document.write"];

        foreach (var file in new[] { "assets/ai-ui.js", "widget/widget.js" })
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
    public void MarkdownRenderer_EscapesRawHtmlAndRestrictsLinkSchemes()
    {
        var script = ReadUiFile("assets/ai-ui.js");

        // The renderer only emits text nodes and a fixed element set, and links are limited to http(s).
        StringAssert.Contains(script, "document.createTextNode");
        StringAssert.Contains(script, "\\(https?:\\/\\/", "links must be restricted to http(s) URLs");
        Assert.IsFalse(
            script.Contains("eval(", StringComparison.Ordinal),
            "the UI must never evaluate model output");
    }

    [TestMethod]
    public void ModuleScripts_UseOnlyRelativeApiPaths()
    {
        var script = ReadUiFile("assets/ai-ui.js");

        Assert.IsFalse(
            script.Contains("'/api/", StringComparison.Ordinal),
            "API calls must be relative so they stay inside /apps/<moduleId>/");
        StringAssert.Contains(script, "var API = \"api/v1/ai\"");

        var widgetScript = ReadUiFile("widget/widget.js");
        StringAssert.Contains(widgetScript, "\"../api/v1/ai");
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
