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

namespace DotNetCloud.UI.Shared.Components.Navigation;

/// <summary>Code-behind for the <see cref="DncButton"/> component.</summary>
public partial class DncButton : ComponentBase
{
    /// <summary>Button variant controlling color scheme.</summary>
    [Parameter]
    public ButtonVariant Variant { get; set; } = ButtonVariant.Primary;

    /// <summary>Button size.</summary>
    [Parameter]
    public ButtonSize Size { get; set; } = ButtonSize.Default;

    /// <summary>HTML button type (button, submit, reset).</summary>
    [Parameter]
    public string Type { get; set; } = "button";

    /// <summary>Whether the button is disabled.</summary>
    [Parameter]
    public bool Disabled { get; set; }

    /// <summary>Whether the button stretches to full width.</summary>
    [Parameter]
    public bool FullWidth { get; set; }

    /// <summary>Whether to show a loading spinner.</summary>
    [Parameter]
    public bool Loading { get; set; }

    /// <summary>Additional CSS class.</summary>
    [Parameter]
    public string? CssClass { get; set; }

    /// <summary>Button content.</summary>
    [Parameter]
    public RenderFragment? ChildContent { get; set; }

    /// <summary>Click handler.</summary>
    [Parameter]
    public EventCallback<MouseEventArgs> OnClick { get; set; }

    /// <summary>Additional HTML attributes.</summary>
    [Parameter(CaptureUnmatchedValues = true)]
    public Dictionary<string, object>? AdditionalAttributes { get; set; }

    private string VariantClass => Variant switch
    {
        ButtonVariant.Primary => "btn-primary",
        ButtonVariant.Danger => "btn-danger",
        ButtonVariant.Warning => "btn-warning",
        ButtonVariant.Success => "btn-success",
        ButtonVariant.Outline => "btn-outline",
        _ => "btn-primary"
    };

    private string SizeClass => Size switch
    {
        ButtonSize.Small => "btn-sm",
        _ => ""
    };
}
