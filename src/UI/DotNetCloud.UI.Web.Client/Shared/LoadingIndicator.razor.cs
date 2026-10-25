using Microsoft.AspNetCore.Components;

namespace DotNetCloud.UI.Web.Client.Shared;

/// <summary>Code-behind for the <see cref="LoadingIndicator"/> component.</summary>
public partial class LoadingIndicator : ComponentBase
{
    [Parameter]
    public bool Visible { get; set; } = true;

    [Parameter]
    public string? Message { get; set; }
}
