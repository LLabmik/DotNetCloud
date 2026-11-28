using System.Text.RegularExpressions;

namespace DotNetCloud.Modules.AI.Tests;

/// <summary>
/// Guards the AI module's own web UI assets (phase A5 UI plane, Blazor WebAssembly shape).
/// </summary>
/// <remarks>
/// The core frames <c>/apps/dotnetcloud.ai/</c> and strips that prefix before forwarding, so every
/// asset reference in the framed document must be <b>relative</b>. The chat stream is a Server-Sent
/// Events response, pumped by the shared frame script because <c>HttpClient</c> cannot stream a body
/// in WebAssembly.
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
    public void FrameScript_PumpsServerSentEventsAndForwardsEachPayload()
    {
        var frame = ReadUiFile("js/dncModuleFrame.js");

        StringAssert.Contains(frame, "stream: function (url, body, dotNetRef)");
        StringAssert.Contains(frame, "text/event-stream");
        StringAssert.Contains(frame, "OnStreamEvent");
        StringAssert.Contains(frame, "OnStreamCompleted");
        StringAssert.Contains(frame, "OnStreamFailed");
        StringAssert.Contains(frame, "stopStream");
    }

    [TestMethod]
    public void AppStylesheet_DefinesTheSharedComponentClassesTheMarkupUses()
    {
        var css = ReadUiFile("css/app.css");

        string[] required =
        [
            "ai-layout", "ai-sidebar", "ai-main", "ai-conversation", "ai-conversation-btn",
            "ai-conversation-title", "ai-rename-input", "ai-model-label", "ai-empty-hint",
            "ai-welcome", "ai-chat", "ai-help", "ai-messages", "ai-msg", "ai-avatar", "ai-msg-body",
            "ai-msg-role", "ai-msg-text", "ai-markdown", "ai-copy-actions", "ai-copy-btn",
            "ai-stream-actions", "ai-abort-btn", "ai-thinking", "ai-thinking-block", "ai-error",
            "ai-composer", "ai-input", "ai-send", "hidden",
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

        StringAssert.Contains(manifest, "\"widget\"");
        Assert.IsTrue(
            File.Exists(Path.Combine(AppContext.BaseDirectory, UiDirectory, "widget", "index.html")),
            "the widget document declared by ui.widget.path must ship with the module");
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
