using Microsoft.AspNetCore.Components;

namespace DotNetCloud.UI.Web.Components.Shared;

/// <summary>Code-behind for the <see cref="RedirectToDashboard"/> component.</summary>
public partial class RedirectToDashboard : ComponentBase
{
    protected override void OnInitialized()
    {
        Navigation.NavigateTo("/", forceLoad: true);
    }
}
