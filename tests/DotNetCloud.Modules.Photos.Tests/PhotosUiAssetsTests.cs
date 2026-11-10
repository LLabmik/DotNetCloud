using System.Text.RegularExpressions;

namespace DotNetCloud.Modules.Photos.Tests;

/// <summary>
/// Guards the module's own web UI assets (phase A5 UI plane, Track C).
/// </summary>
/// <remarks>
/// The core frames <c>/apps/dotnetcloud.photos/</c> and strips that prefix before forwarding, so every
/// asset and API reference in the framed documents must be <b>relative</b>. An absolute
/// <c>/assets/photos-ui.js</c> would leave the module and hit a core route — a 404 at best, a core
/// script at worst. These tests fail the build rather than the browser.
/// </remarks>
[TestClass]
public class PhotosUiAssetsTests
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
        StringAssert.Contains(html, "assets/photos-ui.js");
        StringAssert.Contains(html, "assets/photos-ui.css");
    }

    [TestMethod]
    public void ModuleScripts_DoNotInjectHtml()
    {
        // File names, album titles and tags are user data. Building DOM with innerHTML would make them
        // a stored-XSS sink inside the core's origin.
        string[] sinks = [".innerHTML", ".outerHTML", "insertAdjacentHTML", "document.write"];

        foreach (var file in new[] { "assets/photos-ui.js", "widget/widget.js" })
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
        var script = ReadUiFile("assets/photos-ui.js");

        Assert.IsFalse(
            script.Contains("'/api/", StringComparison.Ordinal),
            "API calls must be relative so they stay inside /apps/<moduleId>/");
        Assert.IsFalse(
            script.Contains("\"/api/", StringComparison.Ordinal),
            "API calls must be relative so they stay inside /apps/<moduleId>/");
        StringAssert.Contains(script, "var API_PHOTOS = \"api/v1/photos\"");

        var widgetScript = ReadUiFile("widget/widget.js");
        StringAssert.Contains(widgetScript, "\"../api/v1/photos");
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
        StringAssert.Contains(manifest, "\"/widget/index.html\"");

        Assert.IsTrue(
            File.Exists(Path.Combine(AppContext.BaseDirectory, UiDirectory, "widget", "index.html")),
            "the widget document declared by ui.widget.path must ship with the module");
    }

    [TestMethod]
    public void WidgetScript_RegistersTheWidgetRole()
    {
        var widgetScript = ReadUiFile("widget/widget.js");

        StringAssert.Contains(widgetScript, "role: \"widget\"");
    }

    [TestMethod]
    public void ModuleScript_HonoursShellDeepLinks()
    {
        // The core forwards the shell page's query into the frame, so a search hit or the Home widget
        // must be able to open one photo (or one album) directly.
        var script = ReadUiFile("assets/photos-ui.js");

        StringAssert.Contains(script, "new URLSearchParams(window.location.search");
        StringAssert.Contains(script, "params.get(\"photoId\")");
        StringAssert.Contains(script, "params.get(\"album\")");
    }

    [TestMethod]
    public void Bridge_IsShippedAsAStaticAsset()
    {
        var bridge = ReadUiFile("dncModuleBridge.js");

        // The bridge is the SDK's single copy; this proves the host copies the real thing and not a stub.
        StringAssert.Contains(bridge, "window.dncModuleBridge = bridge");
        StringAssert.Contains(bridge, "dnc:ready");
    }

    [TestMethod]
    public void Bridge_ExposesTheMediaUrlHelper()
    {
        // Browser-initiated subresources (<img src>, <video src>) cannot carry the ticket header and an
        // opaque-origin frame sends no session cookie, so the ticket has to travel in the query string.
        var bridge = ReadUiFile("dncModuleBridge.js");

        StringAssert.Contains(bridge, "mediaUrl: function (url)");
        StringAssert.Contains(bridge, "TICKET_QUERY + '=' + encodeURIComponent(state.ticket)");
        Assert.IsFalse(
            bridge.Contains("mediaUrl: function (url) {\n      return state.ticket", StringComparison.Ordinal),
            "mediaUrl must not append the ticket to a third-party URL.");
    }

    [TestMethod]
    public void ModuleScripts_AuthenticateEveryImageRequest()
    {
        // Regression: the first live browser check rendered every card with a broken image because
        // `img.src` was the bare thumbnail URL — the frame's request carries neither the core session
        // cookie (opaque origin) nor the bridge's ticket header, so the proxy answered 401.
        var script = ReadUiFile("assets/photos-ui.js");
        var widgetScript = ReadUiFile("widget/widget.js");

        Assert.IsFalse(
            Regex.IsMatch(script, @"\.src\s*=\s*thumbnailUrl\("),
            "photos-ui.js assigns a bare thumbnail URL to img.src; wrap it in mediaUrl(...).");
        Assert.IsFalse(
            Regex.IsMatch(widgetScript, @"\.src\s*=\s*""\.\./"),
            "widget.js assigns a bare relative API URL to img.src; wrap it in mediaUrl(...).");

        foreach (var (file, source) in new[] { ("photos-ui.js", script), ("widget.js", widgetScript) })
        {
            foreach (Match match in Regex.Matches(source, @"\.src\s*=\s*([^;]+);"))
            {
                StringAssert.Contains(
                    match.Groups[1].Value,
                    "mediaUrl(",
                    $"{file} has an unauthenticated img.src assignment: {match.Groups[1].Value.Trim()}");
            }
        }
    }

    [TestMethod]
    public void ModuleScript_PreviewsQueuedEditsBeforeSaving()
    {
        // An edit only reaches the pixels on "Save edits", so without a local preview the user rotates a
        // photo and nothing appears to happen.
        var script = ReadUiFile("assets/photos-ui.js");

        StringAssert.Contains(script, "transformOfEditStack");
        StringAssert.Contains(script, "previewTransforms");
        StringAssert.Contains(script, "image.style.transform");
    }

    [TestMethod]
    public void ModuleScript_PreviewSurvivesRapidEdits()
    {
        // Two quick rotations used to leave one turn on screen: the earlier (smaller) stack landed last
        // and overwrote the newer preview.
        var script = ReadUiFile("assets/photos-ui.js");

        StringAssert.Contains(script, "applyPreviewOperation(photo.id, operationType, parameters)");
        StringAssert.Contains(script, "token === previewSequence");
    }

    [TestMethod]
    public void ModuleScript_AsksForFileConsentWhenTheCoreRefuses()
    {
        // Reading an original needs `files:read`; the module must ask the shell for it rather than
        // reporting a bare failure.
        var script = ReadUiFile("assets/photos-ui.js");

        StringAssert.Contains(script, "FILES_CONSENT_REQUIRED");
        StringAssert.Contains(script, "bridge.requestConsent([\"files:read\"])");
    }

    private static string ReadUiFile(string relativePath)
    {
        var path = Path.Combine(AppContext.BaseDirectory, UiDirectory, relativePath);
        Assert.IsTrue(File.Exists(path), $"Expected UI asset was not copied to the test output: {path}");
        return File.ReadAllText(path);
    }
}
