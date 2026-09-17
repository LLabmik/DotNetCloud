namespace DotNetCloud.Modules.Files.UI;

/// <summary>
/// Presents the Collabora document editor's fullscreen toggle: the Material icon names, the
/// tooltip/aria-label text, and the JS interop identifiers used to drive it.
/// Kept out of the component so the icon names are asserted by unit tests — a name with no
/// SVG path in <c>MaterialSvgIcons</c> renders as literal text inside the button.
/// </summary>
internal static class DocumentEditorFullscreen
{
    /// <summary>Material icon shown while the editor is <em>not</em> fullscreen (the "enter" affordance).</summary>
    internal const string EnterIcon = "fullscreen";

    /// <summary>Material icon shown while the editor <em>is</em> fullscreen (the "exit" affordance).</summary>
    internal const string ExitIcon = "fullscreen_exit";

    /// <summary>Tooltip / aria-label while the editor is not fullscreen.</summary>
    internal const string EnterTooltip = "Fullscreen";

    /// <summary>Tooltip / aria-label while the editor is fullscreen.</summary>
    internal const string ExitTooltip = "Exit fullscreen";

    /// <summary>Browser global that implements the fullscreen toggle (see <c>wwwroot/js/document-editor.js</c>).</summary>
    internal const string JsObject = "dotnetcloudDocumentEditor";

    /// <summary>JS interop method the browser calls when the fullscreen state changes.</summary>
    internal const string ChangedCallback = "OnFullscreenChanged";

    /// <summary>Returns the icon name for the current fullscreen state.</summary>
    /// <param name="isFullscreen">Whether the editor container currently holds fullscreen.</param>
    internal static string GetIcon(bool isFullscreen)
        => isFullscreen ? ExitIcon : EnterIcon;

    /// <summary>Returns the tooltip / aria-label for the current fullscreen state.</summary>
    /// <param name="isFullscreen">Whether the editor container currently holds fullscreen.</param>
    internal static string GetTooltip(bool isFullscreen)
        => isFullscreen ? ExitTooltip : EnterTooltip;
}
