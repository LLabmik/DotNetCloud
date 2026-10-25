using DotNetCloud.Core.Data.Entities.Identity;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Identity;

namespace DotNetCloud.UI.Web.Components.Pages.Auth;

/// <summary>Code-behind for the <see cref="Logout"/> page.</summary>
public partial class Logout : ComponentBase
{
    [Inject] private SignInManager<ApplicationUser> SignInManager { get; set; } = default!;
    [Inject] private NavigationManager Navigation { get; set; } = default!;

    protected override async Task OnInitializedAsync()
    {
        await SignInManager.SignOutAsync();
        Navigation.NavigateTo("/auth/login", forceLoad: true);
    }
}
