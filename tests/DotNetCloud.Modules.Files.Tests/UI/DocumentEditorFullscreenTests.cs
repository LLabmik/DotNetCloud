using System.Reflection;
using DotNetCloud.Modules.Files.UI;
using DotNetCloud.UI.Shared.Components.DataDisplay;
using Microsoft.JSInterop;

namespace DotNetCloud.Modules.Files.Tests.UI;

/// <summary>
/// Tests for <see cref="DocumentEditorFullscreen"/> — the icon, tooltip and JS interop names behind
/// the Collabora editor's fullscreen button. The icon names are resolved against
/// <see cref="MaterialSvgIcons"/>, because a name without an SVG path renders as literal text in
/// the button instead of an icon.
/// </summary>
[TestClass]
public sealed class DocumentEditorFullscreenTests
{
    [TestMethod]
    public void GetIcon_NotFullscreen_ReturnsEnterIcon()
    {
        var result = DocumentEditorFullscreen.GetIcon(false);

        Assert.AreEqual("fullscreen", result);
    }

    [TestMethod]
    public void GetIcon_Fullscreen_ReturnsExitIcon()
    {
        var result = DocumentEditorFullscreen.GetIcon(true);

        Assert.AreEqual("fullscreen_exit", result);
    }

    [TestMethod]
    public void GetTooltip_NotFullscreen_ReturnsEnterTooltip()
    {
        var result = DocumentEditorFullscreen.GetTooltip(false);

        Assert.AreEqual("Fullscreen", result);
    }

    [TestMethod]
    public void GetTooltip_Fullscreen_ReturnsExitTooltip()
    {
        var result = DocumentEditorFullscreen.GetTooltip(true);

        Assert.AreEqual("Exit fullscreen", result);
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void GetIcon_BothStates_ResolveToSvgPathData(bool isFullscreen)
    {
        var icon = DocumentEditorFullscreen.GetIcon(isFullscreen);

        Assert.IsTrue(
            MaterialSvgIcons.HasPath(icon),
            $"Fullscreen toggle icon '{icon}' has no SVG path and would render as text.");
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void GetTooltip_BothStates_AreNonEmpty(bool isFullscreen)
    {
        var tooltip = DocumentEditorFullscreen.GetTooltip(isFullscreen);

        Assert.IsFalse(string.IsNullOrWhiteSpace(tooltip), "The fullscreen toggle needs a non-empty tooltip.");
    }

    [TestMethod]
    public void JsObject_IsAValidJavaScriptIdentifierSegment()
    {
        // The helper is invoked as "<JsObject>.<method>", so the name must be a plain global
        // identifier — no dots, spaces or other path syntax.
        var name = DocumentEditorFullscreen.JsObject;

        Assert.IsFalse(string.IsNullOrWhiteSpace(name));
        Assert.AreEqual(name, name.Trim());
        Assert.IsTrue(
            name.All(c => char.IsLetterOrDigit(c) || c == '_' || c == '$'),
            $"JS global '{name}' is not a valid identifier segment.");
    }

    /// <summary>
    /// The browser resolves fullscreen changes by invoking the callback name from JS, so the
    /// code-behind must expose a matching public <c>[JSInvokable]</c> method that accepts a bool
    /// and returns a task. A rename on either side breaks the toggle silently.
    /// </summary>
    [TestMethod]
    public void DocumentEditor_ExposesJsInvokableCallbackForFullscreenChanges()
    {
        var callbackName = DocumentEditorFullscreen.ChangedCallback;
        var method = typeof(DocumentEditor).GetMethod(
            callbackName,
            BindingFlags.Public | BindingFlags.Instance);

        Assert.IsNotNull(method, $"DocumentEditor has no public method '{callbackName}' for the JS bridge to call.");
        Assert.AreEqual(typeof(Task), method!.ReturnType, "The callback must return a Task so the JS bridge can await it.");

        var parameters = method.GetParameters();
        Assert.AreEqual(1, parameters.Length, "The callback takes exactly one argument.");
        Assert.AreEqual(typeof(bool), parameters[0].ParameterType, "The callback receives the fullscreen state as a bool.");

        var attribute = method.GetCustomAttribute<JSInvokableAttribute>();
        Assert.IsNotNull(attribute, $"'{callbackName}' must be marked [JSInvokable] to be reachable from the browser.");
        Assert.AreEqual(callbackName, attribute!.Identifier, "The JSInvokable name must match the name the JS helper invokes.");
    }
}
