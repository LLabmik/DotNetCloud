using System.Net.Http;
using System.Security.Claims;
using DotNetCloud.Core.Constants;
using DotNetCloud.Core.Data.Entities.Identity;
using DotNetCloud.Core.DTOs;
using DotNetCloud.Core.Localization;
using DotNetCloud.Core.Services;
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
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Localization;
using Microsoft.JSInterop;
using static Microsoft.AspNetCore.Components.Web.RenderMode;

namespace DotNetCloud.UI.Web.Components.Pages.Auth;

/// <summary>Code-behind for the <see cref="Register"/> component.</summary>
public partial class Register : ComponentBase
{
    [Inject] private UserManager<ApplicationUser> UserManager { get; set; } = default!;
    [Inject] private IAdminSettingsService AdminSettingsService { get; set; } = default!;
    [Inject] private IJSRuntime Js { get; set; } = default!;

    [SupplyParameterFromForm]
    private RegisterFormModel? _model { get; set; }

    private bool _submitting;
    private bool _success;
    private string? _errorMessage;
    private bool _closedSystemEnabled;
    private bool _demoModeEnabled;

    protected override async Task OnInitializedAsync()
    {
        _model ??= new();

        try
        {
            var closedSetting = await AdminSettingsService.GetSettingAsync(
                SystemSettingKeys.CoreModule, SystemSettingKeys.ClosedSystemEnabled);
            _closedSystemEnabled = closedSetting?.Value == "true";

            var demoSetting = await AdminSettingsService.GetSettingAsync(
                SystemSettingKeys.CoreModule, SystemSettingKeys.DemoModeEnabled);
            _demoModeEnabled = demoSetting?.Value == "true";
        }
        catch
        {
            // If we can't reach the settings service, assume open registration
            _closedSystemEnabled = false;
            _demoModeEnabled = false;
        }
    }

    private async Task RegisterAsync()
    {
        _model ??= new();
        _errorMessage = null;
        _success = false;

        if (_model.Password != _model.ConfirmPassword)
        {
            _errorMessage = "Passwords do not match.";
            return;
        }

        if (string.IsNullOrWhiteSpace(_model.Username))
        {
            _errorMessage = "Username is required.";
            return;
        }

        _submitting = true;
        try
        {
            var isDemoUser = _demoModeEnabled;
            var displayName = string.IsNullOrWhiteSpace(_model.DisplayName)
                ? _model.Username
                : _model.DisplayName.Trim();

            // Display names are what other users see — they must be unique (case-insensitive).
            var displayNameTaken = await UserManager.Users.AnyAsync(u =>
                u.DisplayName.ToLower() == displayName.ToLower());
            if (displayNameTaken)
            {
                _errorMessage = $"Display name '{displayName}' is already in use. Please choose a different display name.";
                return;
            }

            var user = new ApplicationUser
            {
                UserName = _model.Username,
                Email = string.IsNullOrWhiteSpace(_model.Email) ? null : _model.Email,
                DisplayName = displayName,
                Locale = !string.IsNullOrWhiteSpace(_model.Locale) ? _model.Locale : "en-US",
                Timezone = !string.IsNullOrWhiteSpace(_model.Timezone) ? _model.Timezone : "UTC",
                CreatedAt = DateTime.UtcNow,
                IsActive = true,
                IsDemoUser = isDemoUser
            };

            var result = await UserManager.CreateAsync(user, _model.Password);
            if (result.Succeeded)
            {
                _success = true;
                _model = new RegisterFormModel();
            }
            else
            {
                _errorMessage = string.Join(" ", result.Errors.Select(e => e.Description));
            }
        }
        catch
        {
            _errorMessage = "An error occurred. Please try again.";
        }
        finally
        {
            _submitting = false;
        }
    }

    private async Task DetectTimezoneAsync()
    {
        try
        {
            await Js.InvokeVoidAsync("dotnetcloud.detectAndFillTimezone", "register-timezone");
        }
        catch
        {
            // timezone.js may not have loaded yet; the timezone input already defaults to UTC.
        }
    }

    private sealed class RegisterFormModel
    {
        public string Username { get; set; } = string.Empty;
        public string? Email { get; set; }
        public string DisplayName { get; set; } = string.Empty;
        public string Password { get; set; } = string.Empty;
        public string ConfirmPassword { get; set; } = string.Empty;
        public string Locale { get; set; } = "en-US";
        public string Timezone { get; set; } = "UTC";
    }
}
