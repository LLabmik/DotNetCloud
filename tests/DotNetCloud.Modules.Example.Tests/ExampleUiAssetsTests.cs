using System.Text.RegularExpressions;

namespace DotNetCloud.Modules.Example.Tests;

/// <summary>
/// Guards the module's own web UI assets (phase A5 UI plane).
/// </summary>
/// <remarks>
/// The core frames <c>/apps/dotnetcloud.example/</c> and strips that prefix before forwarding, so
/// every asset reference in the framed document must be <b>relative</b>. An absolute
/// <c>/assets/app.js</c> would leave the module and hit a core route — a 404 at best, a core script
/// at worst. These tests fail the build rather than the browser.
/// </remarks>
[TestClass]
public class ExampleUiAssetsTests
{
    private const string UiDirectory = "ui";

    [TestMethod]
    public void IndexHtml_Exists()
    {
        Assert.IsTrue(
            File.Exists(Path.Combine(AppContext.BaseDirectory, UiDirectory, "index.html")),
            "the module UI entry document must ship with the module");
    }

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

        // The SDK bridge only forwards an array; a spread `params` argument arriving as a bare string
        // made the consent prompt vanish silently.
        StringAssert.Contains(frame, "bridge().requestConsent(scopes || []);");
    }

    [TestMethod]
    public void Bridge_IsShippedAsAStaticAsset()
    {
        var bridge = ReadUiFile("js/dncModuleBridge.js");

        // The bridge is the SDK's single copy; this proves the module links the real thing, not a stub.
        StringAssert.Contains(bridge, "window.dncModuleBridge = bridge");
        StringAssert.Contains(bridge, "dnc:ready");
    }

    /// <summary>
    /// The module stylesheet must define the classes the ported markup uses.
    /// </summary>
    /// <remarks>
    /// A framed module does not inherit the core shell's global stylesheet, so a missing rule means an
    /// unstyled control rather than a slightly different one.
    /// </remarks>
    [TestMethod]
    public void AppStylesheet_DefinesTheSharedComponentClassesTheMarkupUses()
    {
        var css = ReadUiFile("css/app.css");

        string[] required =
        [
            "btn", "btn-primary", "btn-secondary", "btn-small", "btn-danger",
            "alert", "alert-danger",
            "form-group", "form-actions",
            "note-card", "note-content", "note-meta", "note-actions",
            "empty-state", "status",
            "help-section", "help-steps", "help-tips", "help-faq",
            "nav-link", "is-active", "sidebar-footnote",
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
    public void Manifest_DeclaresTheIframeUiServedFromTheRootPath()
    {
        var manifestPath = Path.Combine(AppContext.BaseDirectory, "manifest.json");
        Assert.IsTrue(File.Exists(manifestPath), $"Expected the packaged manifest at {manifestPath}");
        var manifest = File.ReadAllText(manifestPath);

        StringAssert.Contains(manifest, "\"type\": \"iframe\"");
        StringAssert.Contains(manifest, "\"entryPath\": \"/\"");
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
