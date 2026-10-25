using System.Net.Http;
using System.Net.Http.Json;
using System.Security.Claims;
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
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.AspNetCore.Components.Routing;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.Web.Virtualization;
using Microsoft.Extensions.Localization;
using Microsoft.JSInterop;
using static Microsoft.AspNetCore.Components.Web.RenderMode;

namespace DotNetCloud.UI.Web.Client.Pages.Admin;

/// <summary>Code-behind for the <see cref="UserCreate"/> component.</summary>
public partial class UserCreate : ComponentBase
{
    [Inject] private DotNetCloudApiClient Api { get; set; } = default!;
    [Inject] private ToastService Toast { get; set; } = default!;
    [Inject] private NavigationManager Nav { get; set; } = default!;
    [Inject] private IJSRuntime JS { get; set; } = default!;

    private RegisterRequest _model = new() { Locale = "en-US", Timezone = "UTC" };
    private bool _submitting;
    private bool _detectingTimezone;
    private string? _errorMessage;

    protected override async Task OnInitializedAsync()
    {
        // Default the "require password change" checkbox based on closed system mode
        try
        {
            var setting = await Api.GetSettingAsync(
                DotNetCloud.Core.Constants.SystemSettingKeys.CoreModule,
                DotNetCloud.Core.Constants.SystemSettingKeys.ClosedSystemEnabled);
            _model.PasswordChangeRequired = setting?.Value == "true";
        }
        catch
        {
            // Default to unchecked if the setting can't be read
        }
    }

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (!firstRender) return;
        try
        {
            var rawLocale = await JS.InvokeAsync<string>("dotnetcloud.getLocale");
            if (!string.IsNullOrWhiteSpace(rawLocale))
            {
                var normalized = NormalizeLocale(rawLocale);
                if (normalized is not null)
                {
                    _model.Locale = normalized;
                    StateHasChanged();
                }
            }
        }
        catch { }
    }

    private string? NormalizeLocale(string raw)
    {
        var exact = LocaleOptions.FirstOrDefault(x => x.Code.Equals(raw, StringComparison.OrdinalIgnoreCase));
        if (exact != default) return exact.Code;
        var prefix = raw.Split('-')[0];
        var prefixMatch = LocaleOptions.FirstOrDefault(x => x.Code.StartsWith(prefix + "-",
StringComparison.OrdinalIgnoreCase));
        return prefixMatch != default ? prefixMatch.Code : null;
    }

    private async Task SubmitAsync()
    {
        _errorMessage = null;
        _submitting = true;
        try
        {
            var result = await Api.CreateUserAsync(_model);
            if (result.Success)
            {
                Toast.ShowSuccess("User created successfully.");
                Nav.NavigateTo("/admin/users");
            }
            else
            {
                _errorMessage = result.ErrorMessage ?? "Failed to create user. Please check the form and try again.";
            }
        }
        catch (HttpRequestException)
        {
            _errorMessage = "Failed to communicate with the server.";
        }
        finally
        {
            _submitting = false;
        }
    }

    private async Task DetectTimezoneAsync()
    {
        _detectingTimezone = true;
        try
        {
            var tz = await JS.InvokeAsync<string>("dotnetcloud.getTimezone");
            if (!string.IsNullOrWhiteSpace(tz))
            {
                _model.Timezone = tz;
                Toast.ShowSuccess($"Timezone detected: {tz}");
            }
        }
        catch
        {
            Toast.ShowError("Could not detect timezone.");
        }
        finally
        {
            _detectingTimezone = false;
        }
    }

    private static readonly (string Code, string Name)[] LocaleOptions =
    [
        ("en-US", "English (United States)"),
        ("en-GB", "English (United Kingdom)"),
        ("en-CA", "English (Canada)"),
        ("en-AU", "English (Australia)"),
        ("en-NZ", "English (New Zealand)"),
        ("de-DE", "German (Germany)"),
        ("de-AT", "German (Austria)"),
        ("de-CH", "German (Switzerland)"),
        ("fr-FR", "French (France)"),
        ("fr-CA", "French (Canada)"),
        ("fr-BE", "French (Belgium)"),
        ("fr-CH", "French (Switzerland)"),
        ("es-ES", "Spanish (Spain)"),
        ("es-MX", "Spanish (Mexico)"),
        ("es-AR", "Spanish (Argentina)"),
        ("it-IT", "Italian (Italy)"),
        ("pt-BR", "Portuguese (Brazil)"),
        ("pt-PT", "Portuguese (Portugal)"),
        ("nl-NL", "Dutch (Netherlands)"),
        ("nl-BE", "Dutch (Belgium)"),
        ("sv-SE", "Swedish (Sweden)"),
        ("nb-NO", "Norwegian (Norway)"),
        ("da-DK", "Danish (Denmark)"),
        ("fi-FI", "Finnish (Finland)"),
        ("pl-PL", "Polish (Poland)"),
        ("cs-CZ", "Czech (Czech Republic)"),
        ("sk-SK", "Slovak (Slovakia)"),
        ("hu-HU", "Hungarian (Hungary)"),
        ("ro-RO", "Romanian (Romania)"),
        ("hr-HR", "Croatian (Croatia)"),
        ("bg-BG", "Bulgarian (Bulgaria)"),
        ("ru-RU", "Russian (Russia)"),
        ("uk-UA", "Ukrainian (Ukraine)"),
        ("tr-TR", "Turkish (Turkey)"),
        ("ar-SA", "Arabic (Saudi Arabia)"),
        ("ar-AE", "Arabic (UAE)"),
        ("he-IL", "Hebrew (Israel)"),
        ("ja-JP", "Japanese (Japan)"),
        ("ko-KR", "Korean (South Korea)"),
        ("zh-CN", "Chinese Simplified (China)"),
        ("zh-TW", "Chinese Traditional (Taiwan)"),
    ];
}
