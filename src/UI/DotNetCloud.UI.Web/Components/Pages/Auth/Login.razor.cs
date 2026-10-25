using Microsoft.AspNetCore.Components;

namespace DotNetCloud.UI.Web.Components.Pages.Auth;

/// <summary>Code-behind for the <see cref="Login"/> page.</summary>
public partial class Login : ComponentBase
{
    [SupplyParameterFromQuery]
    private string? ReturnUrl { get; set; }

    [SupplyParameterFromQuery]
    private string? Error { get; set; }

    [SupplyParameterFromQuery]
    private string? Username { get; set; }

    private string SafeReturnUrl => IsSafeLocalReturnUrl(ReturnUrl) ? ReturnUrl! : "/";

    protected override void OnParametersSet()
    {
        Username ??= string.Empty;
    }

    private static bool IsSafeLocalReturnUrl(string? returnUrl)
    {
        return !string.IsNullOrWhiteSpace(returnUrl)
            && returnUrl.StartsWith("/", StringComparison.Ordinal)
            && !returnUrl.StartsWith("//", StringComparison.Ordinal);
    }
}
