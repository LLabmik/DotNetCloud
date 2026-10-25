using System.Net.Http;
using System.Security.Claims;
using DotNetCloud.Core.Data.Entities.Identity;
using DotNetCloud.Core.DTOs;
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
using DotNetCloud.UI.Web.Client.Services;
using DotNetCloud.UI.Web.Client.Shared;
using DotNetCloud.UI.Web.Components.Layout;
using DotNetCloud.UI.Web.Components.Pages;
using DotNetCloud.UI.Web.Components.Pages.Modules.Help;
using DotNetCloud.UI.Web.Components.Shared;
using DotNetCloud.UI.Web.Components.Shared.Search;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.AspNetCore.Components.Routing;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Localization;
using Microsoft.JSInterop;
using static Microsoft.AspNetCore.Components.Web.RenderMode;

namespace DotNetCloud.UI.Web.Components.Pages.Auth;

/// <summary>Code-behind for the <see cref="ResetPassword"/> component.</summary>
public partial class ResetPassword : ComponentBase
{
    [Inject] private UserManager<ApplicationUser> UserManager { get; set; } = default!;

    [SupplyParameterFromQuery]
    private string? Username { get; set; }

    [SupplyParameterFromQuery]
    private string? Token { get; set; }

    [SupplyParameterFromForm]
    private ResetPasswordModel? _model { get; set; }

    private bool _submitting;
    private bool _success;
    private string? _errorMessage;

    protected override void OnInitialized()
    {
        _model ??= new();
        if (!string.IsNullOrWhiteSpace(Username)) _model.Username = Username;
        if (!string.IsNullOrWhiteSpace(Token)) _model.Token = Token;
    }

    private async Task SubmitAsync()
    {
        _model ??= new();
        _errorMessage = null;
        _success = false;

        if (_model.NewPassword != _model.ConfirmPassword)
        {
            _errorMessage = "Passwords do not match.";
            return;
        }

        _submitting = true;
        try
        {
            var user = await UserManager.FindByNameAsync(_model.Username);
            if (user is null)
            {
                _errorMessage = "Invalid reset request.";
                return;
            }

            var result = await UserManager.ResetPasswordAsync(user, _model.Token, _model.NewPassword);
            if (result.Succeeded)
            {
                _success = true;
            }
            else
            {
                _errorMessage = string.Join(" ", result.Errors.Select(e => e.Description));
            }
        }
        finally
        {
            _submitting = false;
        }
    }

    private sealed class ResetPasswordModel
    {
        public string Username { get; set; } = string.Empty;
        public string Token { get; set; } = string.Empty;
        public string NewPassword { get; set; } = string.Empty;
        public string ConfirmPassword { get; set; } = string.Empty;
    }
}
