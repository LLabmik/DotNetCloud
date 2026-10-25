using System.Net.Http;
using System.Security.Claims;
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
using Microsoft.Extensions.Localization;
using Microsoft.JSInterop;
using static Microsoft.AspNetCore.Components.Web.RenderMode;

namespace DotNetCloud.UI.Web.Components.Pages.Auth;

/// <summary>Code-behind for the <see cref="MfaSetup"/> component.</summary>
public partial class MfaSetup : ComponentBase
{
    private enum SetupState { Loading, Ready, Verified, Error }

    private SetupState _state = SetupState.Loading;
    private TotpSetupResponse? _setup;
    private BackupCodesResponse? _backupCodes;
    private string? _errorMessage;
    private bool _submitting;
    private TotpVerifyModel _verifyModel = new();
    private string _returnUrl = "/";
    private EditForm? _verifyForm;

    [Inject] private IMfaService MfaService { get; set; } = default!;
    [Inject] private SignInManager<ApplicationUser> SignInManager { get; set; } = default!;
    [Inject] private UserManager<ApplicationUser> UserManager { get; set; } = default!;
    [Inject] private NavigationManager Navigation { get; set; } = default!;
    [Inject] private IJSRuntime Js { get; set; } = default!;

    [SupplyParameterFromQuery]
    private string? ReturnUrl { get; set; }

    protected override async Task OnInitializedAsync()
    {
        _returnUrl = IsSafeLocalReturnUrl(ReturnUrl) ? ReturnUrl! : "/";
        await InitSetupAsync();
    }

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (_state == SetupState.Ready && _setup is not null && firstRender is false)
        {
            await Js.InvokeVoidAsync("dotnetcloudMfaQr.render",
                "mfa-qr-container", _setup.QrCodeUri, 220, "#000000", "#ffffff");
        }
    }

    private async Task InitSetupAsync()
    {
        _state = SetupState.Loading;
        _errorMessage = null;

        try
        {
            var user = await UserManager.GetUserAsync(SignInManager.Context.User);
            if (user is null)
            {
                _errorMessage = "Could not identify the current user. Please log in again.";
                _state = SetupState.Error;
                return;
            }

            _setup = await MfaService.GetTotpSetupAsync(user.Id);
            _state = SetupState.Ready;
        }
        catch (Exception ex)
        {
            _errorMessage = $"Failed to initialize MFA setup: {ex.Message}";
            _state = SetupState.Error;
        }
    }

    /// <summary>
    /// Runs after the verify-code input value is bound (oninput). Auto-submits once the
    /// 6th digit is present, reusing the same validation+verify path as the Verify button.
    /// </summary>
    private async Task OnVerifyCodeChanged()
    {
        if (_submitting || _state != SetupState.Ready)
            return;

        var code = _verifyModel.Code?.Trim() ?? string.Empty;
        if (code.Length < 6)
            return;

        // Mirror OnValidSubmit: only proceed when DataAnnotations validation passes.
        if (_verifyForm?.EditContext is { } editContext && !editContext.Validate())
            return;

        await VerifyAsync();
    }

    private async Task VerifyAsync()
    {
        _errorMessage = null;
        _submitting = true;

        try
        {
            var user = await UserManager.GetUserAsync(SignInManager.Context.User);
            if (user is null)
            {
                _errorMessage = "Could not identify the current user. Please log in again.";
                _state = SetupState.Error;
                return;
            }

            var verified = await MfaService.VerifyTotpAsync(user.Id, _verifyModel.Code.Trim());
            if (!verified)
            {
                _errorMessage = "Invalid code. Please check the code in your authenticator app and try again.";
                return;
            }

            // Enable MFA for the user
            var result = await UserManager.SetTwoFactorEnabledAsync(user, true);
            if (!result.Succeeded)
            {
                _errorMessage = $"Failed to enable MFA: {string.Join(", ", result.Errors.Select(e => e.Description))}";
                return;
            }

            // Clear the MFA setup required flag (set by older versions)
            if (user.MfaSetupRequired)
            {
                user.MfaSetupRequired = false;
                await UserManager.UpdateAsync(user);
            }

            // Generate backup codes
            _backupCodes = await MfaService.GenerateBackupCodesAsync(user.Id);

            // Note: no SignOut/SignIn here — in Blazor Server Interactive mode,
            // the HTTP response has already started so cookie operations fail
            // with "Headers are read-only". The MFA state is persisted in the
            // database and will be picked up on the next full page navigation
            // (the Continue button is a plain <a href> link).

            _state = SetupState.Verified;
        }
        catch (Exception ex)
        {
            _errorMessage = $"An error occurred: {ex.Message}";
            _state = SetupState.Error;
        }
        finally
        {
            _submitting = false;
        }
    }

    private static bool IsSafeLocalReturnUrl(string? returnUrl)
    {
        return !string.IsNullOrWhiteSpace(returnUrl)
            && returnUrl.StartsWith("/", StringComparison.Ordinal)
            && !returnUrl.StartsWith("//", StringComparison.Ordinal);
    }

    private async Task CopyBackupCodesAsync()
    {
        if (_backupCodes?.Codes is null || _backupCodes.Codes.Count == 0)
            return;

        var text = string.Join("\n", _backupCodes.Codes);
        try
        {
            await Js.InvokeVoidAsync("navigator.clipboard.writeText", text);
        }
        catch
        {
            // Clipboard API may fail in some browser contexts; silently ignore
        }
    }

    private async Task CopySharedKeyAsync()
    {
        if (string.IsNullOrEmpty(_setup?.SharedKey))
            return;

        try
        {
            await Js.InvokeVoidAsync("navigator.clipboard.writeText", _setup.SharedKey);
        }
        catch
        {
            // Clipboard API may fail in some browser contexts; silently ignore
        }
    }

    private sealed class TotpVerifyModel
    {
        [System.ComponentModel.DataAnnotations.Required(ErrorMessage = "Verification code is required.")]
        [System.ComponentModel.DataAnnotations.StringLength(6, MinimumLength = 6, ErrorMessage = "Code must be 6 digits.")]
        [System.ComponentModel.DataAnnotations.RegularExpression("^[0-9]{6}$", ErrorMessage = "Code must be 6 digits.")]
        public string Code { get; set; } = string.Empty;
    }
}
