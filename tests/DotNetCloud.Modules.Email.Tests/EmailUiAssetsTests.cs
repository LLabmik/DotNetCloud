using System.Text.RegularExpressions;

namespace DotNetCloud.Modules.Email.Tests;

/// <summary>
/// Guards the module's own web UI assets (phase A5 UI plane, Track C).
/// </summary>
/// <remarks>
/// The core frames <c>/apps/dotnetcloud.email/</c> and strips that prefix before forwarding, so every
/// asset and API reference in the framed documents must be <b>relative</b>. An absolute
/// <c>/assets/email-ui.js</c> would leave the module and hit a core route — a 404 at best, a core
/// script at worst. These tests fail the build rather than the browser.
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
        StringAssert.Contains(html, "assets/email-ui.js");
        StringAssert.Contains(html, "assets/email-ui.css");
    }

    [TestMethod]
    public void ModuleScripts_DoNotInjectHtml()
    {
        // Subjects, addresses, folder names, rule names and message ids are user data. Building DOM
        // with innerHTML would make them a stored-XSS sink inside the core's origin.
        string[] sinks = [".innerHTML", ".outerHTML", "insertAdjacentHTML", "document.write"];

        foreach (var file in new[] { "assets/email-ui.js", "widget/widget.js" })
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
    public void ModuleScripts_RenderMessageHtmlInsideASandboxedFrame()
    {
        var script = ReadUiFile("assets/email-ui.js");

        // A message body is HTML produced by a third party. It is rendered in a nested frame with no
        // privileges at all (sandbox=""), which is what keeps injected handlers from running in this
        // document's origin.
        StringAssert.Contains(script, "frame.setAttribute('sandbox', '')");
        StringAssert.Contains(script, "frame.srcdoc");
    }

    [TestMethod]
    public void ModuleScripts_IncludeCredentialsForTheirOwnApi()
    {
        // The frame's origin is opaque, so every fetch is cross-origin and the proxy authenticates it
        // from the session cookie. 'same-origin' would send nothing and the module would answer 401.
        foreach (var file in new[] { "assets/email-ui.js", "widget/widget.js" })
        {
            var script = ReadUiFile(file);

            StringAssert.Contains(script, "credentials: 'include'");
            Assert.IsFalse(
                script.Contains("credentials: 'same-origin'", StringComparison.Ordinal),
                $"{file} would send no credentials from an opaque-origin frame");
        }
    }

    [TestMethod]
    public void ModuleScripts_UseOnlyRelativeApiPaths()
    {
        var script = ReadUiFile("assets/email-ui.js");

        Assert.IsFalse(
            script.Contains("'/api/", StringComparison.Ordinal),
            "API calls must be relative so they stay inside /apps/<moduleId>/");
        StringAssert.Contains(script, "var API = 'api/v1/email'");

        var widgetScript = ReadUiFile("widget/widget.js");
        StringAssert.Contains(widgetScript, "var API = 'api/v1/email'");
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
    public void WidgetScript_StartsInTheWidgetRole()
    {
        var script = ReadUiFile("widget/widget.js");

        StringAssert.Contains(script, "role: 'widget'");
        StringAssert.Contains(script, "reportHeight");
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
    public void ModuleScripts_ReachCloudFilesThroughTheModulesOwnApi()
    {
        var script = ReadUiFile("assets/email-ui.js");

        // Browse and save are core capabilities brokered by the module's backend. The frame must never
        // learn about the Files module (a direct sibling call is forbidden and does not authenticate).
        StringAssert.Contains(script, "API + '/files'");
        StringAssert.Contains(script, "'/attachments/from-file'");
        StringAssert.Contains(script, "'/save-to-files'");

        Assert.IsFalse(
            script.Contains("dotnetcloud.files", StringComparison.Ordinal),
            "the Email UI must not address the Files module directly");
    }

    [TestMethod]
    public void ModuleScripts_AskForConsentWhenFileAccessIsDenied()
    {
        var script = ReadUiFile("assets/email-ui.js");

        // A capability grant is not consent: the first file call of a user who never approved
        // files:read/files:write answers FILES_CONSENT_REQUIRED, and the UI has to start the prompt.
        StringAssert.Contains(script, "FILES_CONSENT_REQUIRED");
        StringAssert.Contains(script, "requestConsent");
        StringAssert.Contains(script, "FILES_UNAVAILABLE");
    }

    [TestMethod]
    public void Stylesheet_ShipsTheFileBrowserRules()
    {
        var css = ReadUiFile("assets/email-ui.css");

        StringAssert.Contains(css, ".file-browser");
        StringAssert.Contains(css, ".file-row");
        StringAssert.Contains(css, ".attachment-row");
    }

    private static string ReadUiFile(string relativePath)
    {
        var path = Path.Combine(AppContext.BaseDirectory, UiDirectory, relativePath);
        Assert.IsTrue(File.Exists(path), $"Expected UI asset was not copied to the test output: {path}");
        return File.ReadAllText(path);
    }
}
