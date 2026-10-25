using Microsoft.AspNetCore.Components;

namespace DotNetCloud.UI.Web.Components.Pages.Auth;

/// <summary>Code-behind for the <see cref="ChangePassword"/> page.</summary>
public partial class ChangePassword : ComponentBase
{
    [SupplyParameterFromQuery]
    private string? ReturnUrl { get; set; }

    [SupplyParameterFromQuery]
    private string? Error { get; set; }

    [SupplyParameterFromQuery(Name = "changed")]
    private bool Changed { get; set; }

    private string SafeReturnUrl => IsSafeLocalReturnUrl(ReturnUrl) ? ReturnUrl! : "/";

    private static bool IsSafeLocalReturnUrl(string? returnUrl)
    {
        return !string.IsNullOrWhiteSpace(returnUrl)
            && returnUrl.StartsWith("/", StringComparison.Ordinal)
            && !returnUrl.StartsWith("//", StringComparison.Ordinal);
    }
}
