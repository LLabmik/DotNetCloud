using System.Text.RegularExpressions;

namespace DotNetCloud.Modules.Tracks.Tests;

/// <summary>
/// Guards the Tracks module's own web assets (Track C UI plane).
/// </summary>
/// <remarks>
/// <para>
/// The core frames <c>/apps/dotnetcloud.tracks/</c> and strips that prefix before forwarding, so every
/// asset and API reference in the framed documents must be <b>relative</b>. An absolute
/// <c>/api/v1/...</c> would leave the module and hit a core route.
/// </para>
/// <para>
/// The app itself is a Blazor WebAssembly bundle exercised in a browser; what is asserted here is the
/// manifest-declared widget document and script, which are plain files and therefore cheap to guard.
/// </para>
/// </remarks>
[TestClass]
public class TracksUiAssetsTests
{
    private const string UiDirectory = "ui";

    [TestMethod]
    public void WidgetPage_ExistsAtThePathTheManifestDeclares()
    {
        var manifest = ReadUiFile("manifest.json");

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
    public void WidgetHtml_ReferencesEveryAssetRelatively()
    {
        var html = ReadUiFile("widget/index.html");

        var references = Regex.Matches(html, "(?:src|href)=\"([^\"]+)\"")
            .Select(match => match.Groups[1].Value)
            .ToList();

        Assert.IsTrue(references.Count >= 2, "the widget loads the bridge and its own script");

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
    public void WidgetHtml_LoadsTheSdkBridgeTheAppAlsoUses()
    {
        var html = ReadUiFile("widget/index.html");
        var bridge = ReadUiFile("dncModuleBridge.js");

        // The bridge authenticates the frame's fetch calls with the frame ticket; without it every
        // widget request is 401.
        StringAssert.Contains(html, "../js/dncModuleBridge.js");
        StringAssert.Contains(bridge, "window.dncModuleBridge = bridge");
        StringAssert.Contains(bridge, "dnc:ready");
    }

    [TestMethod]
    public void WidgetScript_BuildsDomWithoutInjectingHtml()
    {
        // Work-item titles are user data; innerHTML would make them a stored-XSS sink in the core origin.
        string[] sinks = [".innerHTML", ".outerHTML", "insertAdjacentHTML", "document.write"];

        var script = ReadUiFile("widget/widget.js");

        foreach (var sink in sinks)
        {
            Assert.IsFalse(
                script.Contains(sink, StringComparison.Ordinal),
                $"widget.js must build DOM with textContent/createElement, never {sink}");
        }

        StringAssert.Contains(script, "textContent");
    }

    [TestMethod]
    public void WidgetScript_CallsTheApiRelativelyAndRegistersTheWidgetRole()
    {
        var script = ReadUiFile("widget/widget.js");

        Assert.IsFalse(
            script.Contains("'/api/", StringComparison.Ordinal),
            "API calls must be relative so they stay inside /apps/<moduleId>/");
        Assert.IsFalse(
            script.Contains("\"/api/", StringComparison.Ordinal),
            "API calls must be relative so they stay inside /apps/<moduleId>/");

        StringAssert.Contains(script, "\"../api/v1/widget/upcoming");
        StringAssert.Contains(script, "role: \"widget\"");
    }

    [TestMethod]
    public void WidgetScript_OpensWorkItemsThroughTheShellDeepLink()
    {
        // The frame route contract is query parameters only (?card=<id>), because the module proxy only
        // serves static files under /apps/{moduleId}/**.
        var script = ReadUiFile("widget/widget.js");

        StringAssert.Contains(script, "\"?card=\"");
        StringAssert.Contains(script, "openExternal");
    }

    /// <summary>
    /// The module stylesheet must define the shared component classes the ported markup uses.
    /// </summary>
    /// <remarks>
    /// The pages were written against the core shell's global stylesheet, which a framed module does
    /// not inherit. Found live: every button fell back to the user agent default and `.btn-ghost`
    /// rendered white-on-white in dark mode. Porting a class here means adding it to
    /// <c>wwwroot/css/app.css</c> too.
    /// </remarks>
    [TestMethod]
    public void AppStylesheet_DefinesTheSharedComponentClassesTheMarkupUses()
    {
        var css = ReadUiFile("css/app.css");

        string[] required =
        [
            "btn", "btn-primary", "btn-secondary", "btn-outline", "btn-ghost", "btn-accent", "btn-sm",
            "alert", "alert-danger", "badge",
            "form-group", "form-row", "form-actions", "form-control-sm",
            "help-content", "help-section", "help-section-title", "help-tips",
            "markdown-body",
        ];

        foreach (var className in required)
        {
            Assert.IsTrue(
                Regex.IsMatch(css, "\\." + Regex.Escape(className) + "(?![A-Za-z0-9_-])[^{]*\\{"),
                $"wwwroot/css/app.css must define .{className}: the framed module cannot inherit the core shell's stylesheet");
        }
    }

    /// <summary>
    /// The module app must not let the Blazor loader resolve its own bootstrap relatively.
    /// </summary>
    /// <remarks>
    /// Found live: the frame rendered "Loading the module UI…" forever with
    /// <c>Failed to resolve module specifier './dotnet.js'. The base URL is about:blank…</c> — an
    /// opaque-origin document cannot resolve a relative dynamic import, and <c>blazor.webassembly.js</c>
    /// does exactly that while starting. The app therefore disables autostart and starts Blazor with
    /// absolute boot URLs derived from the frame's own document URL.
    /// </remarks>
    [TestMethod]
    public void AppEntry_StartsBlazorWithAbsoluteBootUrls()
    {
        var html = ReadUiFile("index.html");

        StringAssert.Contains(
            html,
            "autostart=\"false\"",
            "the loader must not import ./dotnet.js itself: that relative import cannot resolve in an opaque-origin frame");
        StringAssert.Contains(html, "js/tracks-boot.js");

        var boot = ReadUiFile("js/tracks-boot.js");

        StringAssert.Contains(boot, "loadBootResource");
        StringAssert.Contains(
            boot,
            "new URL(\"./\", window.location.href)",
            "absolute boot URLs must come from the frame's own document URL, never a hard-coded /apps/ prefix");
        Assert.IsFalse(
            boot.Contains("\"/apps/", StringComparison.OrdinalIgnoreCase)
            || boot.Contains("'/apps/", StringComparison.OrdinalIgnoreCase),
            "the module must not know the proxy prefix it is served under");
    }

    private static string ReadUiFile(string relativePath)
    {
        var path = Path.Combine(AppContext.BaseDirectory, UiDirectory, relativePath.Replace('/', Path.DirectorySeparatorChar));
        Assert.IsTrue(File.Exists(path), $"expected the shipped asset at {path}");

        return File.ReadAllText(path);
    }
}
