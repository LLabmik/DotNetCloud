using System.Text.RegularExpressions;

namespace DotNetCloud.Modules.Email.Tests;

/// <summary>
/// Guards the Email module's own web UI assets (phase A5 UI plane, Blazor WebAssembly shape).
/// </summary>
/// <remarks>
/// The core frames <c>/apps/dotnetcloud.email/</c> and strips that prefix before forwarding, so every
/// asset reference in the framed document must be <b>relative</b>. The app is a Blazor WebAssembly
/// bundle exercised in a browser; the widget stays a plain static document.
/// </remarks>
[TestClass]
public class EmailUiAssetsTests
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

        Assert.IsTrue(bridgeIndex >= 0 && bridgeIndex < bootIndex, "the SDK bridge must load before Blazor starts");
    }

    [TestMethod]
    public void AppEntry_StartsBlazorWithAbsoluteBootUrls()
    {
        var html = ReadUiFile("index.html");

        StringAssert.Contains(html, "autostart=\"false\"");

        var boot = ReadUiFile("js/dncModuleBoot.js");
        StringAssert.Contains(boot, "loadBootResource");
        StringAssert.Contains(boot, "new URL(\"./\", window.location.href)");
        Assert.IsFalse(
            boot.Contains("\"/apps/", StringComparison.OrdinalIgnoreCase)
            || boot.Contains("'/apps/", StringComparison.OrdinalIgnoreCase),
            "the module must not know the proxy prefix it is served under");
    }

    [TestMethod]
    public void AppStylesheet_DefinesTheSharedComponentClassesTheMarkupUses()
    {
        var css = ReadUiFile("css/app.css");

        string[] required =
        [
            "app", "sidebar", "main", "panel", "banner", "status", "nav-item", "nav-text",
            "sidebar-section", "sidebar-header", "select", "field", "input", "textarea",
            "btn", "btn-primary", "btn-secondary", "btn-danger", "icon-btn",
            "thread-list", "thread-card", "thread-subject", "thread-meta",
            "thread-header", "message", "message-header", "message-from",
            "message-body-frame", "attachments", "attachment", "attachment-row", "attachment-size",
            "compose-card", "compose-row", "compose-label", "compose-body", "compose-footer",
            "chip-list", "chip", "rule-card", "rule-info", "rule-row", "rule-group",
            "file-row", "file-row-name", "file-browser", "crumb", "modal", "overlay-backdrop",
            "empty", "help-section", "help-tips",
            "material-icon",
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

        StringAssert.Contains(manifest, "\"widget\"");
        Assert.IsTrue(
            File.Exists(Path.Combine(AppContext.BaseDirectory, UiDirectory, "widget", "index.html")),
            "the widget document declared by ui.widget.path must ship with the module");
    }

    [TestMethod]
    public void WidgetScript_StartsInTheWidgetRoleAndAvoidsHtmlInjection()
    {
        var script = ReadUiFile("widget/widget.js");

        StringAssert.Contains(script, "role: \"widget\"");
        StringAssert.Contains(script, "textContent");

        foreach (var sink in new[] { ".innerHTML", ".outerHTML", "insertAdjacentHTML", "document.write" })
        {
            Assert.IsFalse(
                script.Contains(sink, StringComparison.Ordinal),
                $"widget.js must build DOM with textContent/createElement, never {sink}");
        }
    }

    [TestMethod]
    public void Bridge_IsShippedAsAStaticAsset()
    {
        var bridge = ReadUiFile("js/dncModuleBridge.js");

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
}
