using DotNetCloud.Core.Localization;
using DotNetCloud.UI.Shared.Components.DataDisplay;
using DotNetCloud.UI.Shared.Components.Dialogs;
using DotNetCloud.UI.Shared.Components.Editors;
using DotNetCloud.UI.Shared.Components.Forms;
using DotNetCloud.UI.Shared.Components.Layout;
using DotNetCloud.UI.Shared.Components.Navigation;
using DotNetCloud.UI.Shared.Components.Notifications;
using DotNetCloud.UI.Shared.Resources;
using DotNetCloud.UI.Shared.Services;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.AspNetCore.Components.Routing;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.Localization;
using Microsoft.JSInterop;

namespace DotNetCloud.UI.Shared.Components.DataDisplay;

/// <summary>Code-behind for the <see cref="WidgetCard"/> component.</summary>
public partial class WidgetCard : ComponentBase
{
    /// <summary>
    /// The module that owns this widget (e.g. <c>dotnetcloud.files</c>).
    /// Selects the per-module art direction applied to the card.
    /// </summary>
    [Parameter]
    public string ModuleId { get; set; } = string.Empty;

    /// <summary>
    /// The widget card title shown in the header.
    /// </summary>
    [Parameter]
    public string Title { get; set; } = string.Empty;

    /// <summary>
    /// Optional Material icon ligature name. Falls back to <see cref="ModuleIconProvider"/> when
    /// empty or when the supplied value is not a known icon.
    /// </summary>
    [Parameter]
    public string Icon { get; set; } = string.Empty;

    /// <summary>
    /// Optional "Open" link target. Hidden when empty.
    /// </summary>
    [Parameter]
    public string Href { get; set; } = string.Empty;

    /// <summary>
    /// The widget body content rendered inside the card.
    /// </summary>
    [Parameter]
    public RenderFragment? ChildContent { get; set; }

    /// <summary>
    /// CSS-safe slug for the owning module, used to select the per-module art direction.
    /// </summary>
    private string Slug => ToSlug(ModuleId);

    /// <summary>
    /// The Material icon to render — the supplied <see cref="Icon"/> when it maps to a known SVG
    /// path, otherwise the owning module's canonical icon.
    /// </summary>
    private string ResolvedIcon =>
        !string.IsNullOrWhiteSpace(Icon) && MaterialSvgIcons.GetPath(Icon) is not null
            ? Icon
            : ModuleIconProvider.GetIcon(ModuleId, "widgets");

    /// <summary>
    /// Converts a module identifier such as <c>dotnetcloud.files</c> into a CSS class fragment
    /// such as <c>files</c>.
    /// </summary>
    private static string ToSlug(string moduleId)
    {
        var id = moduleId ?? string.Empty;
        const string prefix = "dotnetcloud.";
        if (id.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
        {
            id = id[prefix.Length..];
        }

        var chars = id.Select(c => char.IsLetterOrDigit(c) ? char.ToLowerInvariant(c) : '-').ToArray();
        var slug = new string(chars).Trim('-');
        return string.IsNullOrEmpty(slug) ? "generic" : slug;
    }
}
