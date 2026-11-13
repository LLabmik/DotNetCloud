using System.Text.RegularExpressions;

namespace DotNetCloud.Modules.Bookmarks.Tests;

/// <summary>
/// Guards the module's own web UI assets (phase A5 UI plane, Blazor WebAssembly shape).
/// </summary>
/// <remarks>
/// <para>
/// The core frames <c>/apps/dotnetcloud.bookmarks/</c> and strips that prefix before forwarding, so
/// every asset reference in the framed document must be <b>relative</b>. An absolute
/// <c>/css/app.css</c> would leave the module and hit a core route — a 404 at best, a core asset at
/// worst.
/// </para>
/// <para>
/// The app is a Blazor WebAssembly bundle exercised in a browser; the widget stays a plain static
/// document whose script guards are unchanged.
/// </para>
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

        Assert.IsTrue(references.Count >= 3, "index.html must load the bridge, its scripts and its stylesheet");

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
    public void IndexHtml_LoadsTheBridgeBeforeTheBlazorBootstrap()
    {
        var html = ReadUiFile("index.html");

        StringAssert.Contains(html, "js/dncModuleBridge.js");
        StringAssert.Contains(html, "js/dncModuleFrame.js");
        StringAssert.Contains(html, "js/dncModuleBoot.js");

        var bridgeIndex = html.IndexOf("js/dncModuleBridge.js", StringComparison.Ordinal);
        var bootIndex = html.IndexOf("js/dncModuleBoot.js", StringComparison.Ordinal);

        // The bridge patches window.fetch when it starts; loading it after the bootstrap would let the
        // first API call escape without a frame ticket and answer 401.
        Assert.IsTrue(bridgeIndex >= 0 && bridgeIndex < bootIndex, "the SDK bridge must load before Blazor starts");
    }

    [TestMethod]
    public void AppEntry_StartsBlazorWithAbsoluteBootUrls()
    {
        var html = ReadUiFile("index.html");

        StringAssert.Contains(
            html,
            "autostart=\"false\"",
            "the loader must not import ./dotnet.js itself: that relative import cannot resolve in an opaque-origin frame");

        var boot = ReadUiFile("js/dncModuleBoot.js");

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

    [TestMethod]
    public void FrameScript_StartsTheBridgeAndForwardsConsentAsAnArray()
    {
        var frame = ReadUiFile("js/dncModuleFrame.js");

        StringAssert.Contains(
            frame,
            "api.start({ title: document.title, role: \"app\" });",
            "the bridge must be started at script load, before Blazor can issue a request");
        StringAssert.Contains(frame, "bridge().requestConsent(scopes || []);");
    }

    [TestMethod]
    public void AppStylesheet_DefinesTheSharedComponentClassesTheMarkupUses()
    {
        var css = ReadUiFile("css/app.css");

        string[] required =
        [
            "btn", "btn-primary", "btn-secondary", "btn-danger",
            "alert", "alert-danger",
            "form-group", "form-actions",
            "card", "card-title", "card-url", "card-description", "card-meta", "card-actions",
            "grid", "table", "table-row", "table-url", "table-actions",
            "empty-state", "status", "icon-btn",
            "nav-item", "is-active", "nav-text", "folder-dot", "sidebar-empty",
            "preview-card", "preview-body", "preview-image", "preview-favicon", "preview-site",
            "help-section", "help-steps", "help-tips",
            "material-icon", "material-icon--sm",
        ];

        foreach (var className in required)
        {
            Assert.IsTrue(
                Regex.IsMatch(css, "\\." + Regex.Escape(className) + "(?![A-Za-z0-9_-])[^{]*\\{"),
                $"wwwroot/css/app.css must define .{className}: the framed module cannot inherit the core shell's stylesheet");
        }
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
    public void WidgetScript_DoesNotInjectHtmlAndCallsTheApiRelatively()
    {
        // Bookmark titles and URLs are user data. Building DOM with innerHTML would make them a
        // stored-XSS sink inside the core's origin.
        string[] sinks = [".innerHTML", ".outerHTML", "insertAdjacentHTML", "document.write"];

        var script = ReadUiFile("widget/widget.js");

        foreach (var sink in sinks)
        {
            Assert.IsFalse(
                script.Contains(sink, StringComparison.Ordinal),
                $"widget.js must build DOM with textContent/createElement, never {sink}");
        }

        Assert.IsFalse(
            script.Contains("'/api/", StringComparison.Ordinal),
            "API calls must be relative so they stay inside /apps/<moduleId>/");
        StringAssert.Contains(NormaliseQuotes(script), "'../api/v1/bookmarks");
    }

    [TestMethod]
    public void Bridge_IsShippedAsAStaticAsset()
    {
        var bridge = ReadUiFile("js/dncModuleBridge.js");

        // The bridge is the SDK's single copy; this proves the module links the real thing, not a stub.
        StringAssert.Contains(bridge, "window.dncModuleBridge = bridge");
        StringAssert.Contains(bridge, "dnc:ready");
    }

    private static string ReadUiFile(string relativePath)
    {
        var path = Path.Combine(
            AppContext.BaseDirectory,
            UiDirectory,
            relativePath.Replace('/', Path.DirectorySeparatorChar));

        Assert.IsTrue(File.Exists(path), $"Expected UI asset was not copied to the test output: {path}");
        return File.ReadAllText(path);
    }

    /// <summary>
    /// Maps double-quoted JavaScript string literals onto single quotes.
    /// </summary>
    /// <remarks>
    /// The guards assert what the module UI does — calls stay relative — not which quote character
    /// the script uses.
    /// </remarks>
    private static string NormaliseQuotes(string script) => script.Replace("\"", "'", StringComparison.Ordinal);
}
