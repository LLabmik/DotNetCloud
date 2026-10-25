using Microsoft.AspNetCore.Components;

namespace DotNetCloud.UI.Web.Components.Shared;

/// <summary>Code-behind for the <see cref="RedirectToLogin"/> component.</summary>
public partial class RedirectToLogin : ComponentBase
{
    protected override void OnInitialized()
    {
        var returnPath = "/";
        if (Uri.TryCreate(Navigation.Uri, UriKind.Absolute, out var parsed))
        {
            var local = parsed.PathAndQuery;
            if (!string.IsNullOrEmpty(local)
                && local.StartsWith("/", StringComparison.Ordinal)
                && !local.StartsWith("//", StringComparison.Ordinal))
            {
                returnPath = local;
            }
        }

        Navigation.NavigateTo($"auth/login?returnUrl={Uri.EscapeDataString(returnPath)}", forceLoad: true);
    }
}
