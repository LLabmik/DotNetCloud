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
    public void IndexHtml_LoadsTheBridgeAndTheModuleScript()
    {
        var html = ReadUiFile("index.html");

        StringAssert.Contains(html, "dncModuleBridge.js");
        StringAssert.Contains(html, "assets/example-ui.js");
        StringAssert.Contains(html, "assets/example-ui.css");
    }

    [TestMethod]
    public void ModuleScript_DoesNotInjectHtml()
    {
        // Note text is user data. Building DOM with innerHTML would make it a stored-XSS sink inside
        // the core's origin (the frame is opaque-origin, but the URL it is served from is not).
        var script = ReadUiFile("assets/example-ui.js");

        string[] sinks = [".innerHTML", ".outerHTML", "insertAdjacentHTML", "document.write"];

        foreach (var sink in sinks)
        {
            Assert.IsFalse(
                script.Contains(sink, StringComparison.Ordinal),
                $"the module UI must build DOM with textContent/createElement, never {sink}");
        }
    }

    [TestMethod]
    public void ModuleScript_UsesOnlyRelativeApiPaths()
    {
        var script = ReadUiFile("assets/example-ui.js");

        Assert.IsFalse(
            script.Contains("'/api/", StringComparison.Ordinal),
            "API calls must be relative so they stay inside /apps/<moduleId>/");
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
