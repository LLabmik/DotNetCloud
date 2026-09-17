using System.Reflection;
using DotNetCloud.UI.Shared.Components.Editors;

namespace DotNetCloud.UI.Shared.Tests;

/// <summary>
/// Tests for <see cref="MarkdownEditorInterop"/> and the editor's public surface. The interop names
/// are the only contract between <see cref="MarkdownEditor"/> and
/// <c>wwwroot/js/markdown-editor.js</c>, so a rename on either side would break every formatting
/// button silently; these tests pin the shape of that contract.
/// </summary>
[TestClass]
public sealed class MarkdownEditorInteropTests
{
    [TestMethod]
    public void JsObject_IsAValidJavaScriptIdentifierSegment()
    {
        // The helper is invoked as "<JsObject>.<method>", so the name must be a plain global
        // identifier — no dots, spaces or other path syntax.
        var name = MarkdownEditorInterop.JsObject;

        Assert.IsFalse(string.IsNullOrWhiteSpace(name), "The editor needs a JS helper object name.");
        Assert.AreEqual(name, name.Trim());
        Assert.IsTrue(
            name.All(c => char.IsLetterOrDigit(c) || c == '_' || c == '$'),
            $"JS global '{name}' is not a valid identifier segment.");
    }

    /// <summary>
    /// The component calls "&lt;global&gt;.&lt;method&gt;"; the identifiers are asserted by value because the
    /// same two names are written by hand in <c>wwwroot/js/markdown-editor.js</c>.
    /// </summary>
    [TestMethod]
    public void ReadState_IsTheReadMethodOfTheGlobalHelper()
    {
        Assert.AreEqual("dotnetcloudMarkdownEditor.readState", ReadStateIdentifier());
    }

    [TestMethod]
    public void WriteState_IsTheWriteMethodOfTheGlobalHelper()
    {
        Assert.AreEqual("dotnetcloudMarkdownEditor.writeState", WriteStateIdentifier());
    }

    [TestMethod]
    public void ReadStateAndWriteState_AreDifferentCalls()
    {
        Assert.AreNotEqual(ReadStateIdentifier(), WriteStateIdentifier());
    }

    // Read through a call so the assertions are not compile-time constants the analyzers can fold.
    private static string ReadStateIdentifier() => MarkdownEditorInterop.ReadState;

    private static string WriteStateIdentifier() => MarkdownEditorInterop.WriteState;

    [TestMethod]
    public void MethodNames_AreDistinctAndNonEmpty()
    {
        var read = MarkdownEditorInterop.ReadStateMethod;
        var write = MarkdownEditorInterop.WriteStateMethod;

        Assert.IsFalse(string.IsNullOrWhiteSpace(read));
        Assert.IsFalse(string.IsNullOrWhiteSpace(write));
        Assert.AreNotEqual(read, write, "Reading and writing the editor state are different JS methods.");
    }

    [TestMethod]
    public void MarkdownEditorState_Selection_CombinesStartAndEnd()
    {
        var state = new MarkdownEditorState { Value = "hello", SelectionStart = 1, SelectionEnd = 4 };

        Assert.AreEqual(new MarkdownTextRange(1, 4), state.Selection);
    }

    [TestMethod]
    public void MarkdownEditorState_Default_IsAnEmptyDocument()
    {
        var state = new MarkdownEditorState();

        Assert.AreEqual(string.Empty, state.Value);
        Assert.IsTrue(state.Selection.IsCollapsed);
    }

    [TestMethod]
    public void MarkdownEditor_ExposesFillHeightParameter()
    {
        // The Notes editor passes FillHeight to constrain the editing area to the screen; the
        // parameter must stay a [Parameter] bool on the component.
        var property = typeof(MarkdownEditor).GetProperty("FillHeight", BindingFlags.Public | BindingFlags.Instance);

        Assert.IsNotNull(property, "MarkdownEditor no longer exposes the FillHeight parameter.");
        Assert.AreEqual(typeof(bool), property!.PropertyType);

        var attribute = property.GetCustomAttribute<Microsoft.AspNetCore.Components.ParameterAttribute>();
        Assert.IsNotNull(attribute, "FillHeight must stay a [Parameter] so pages can set it.");
    }
}
