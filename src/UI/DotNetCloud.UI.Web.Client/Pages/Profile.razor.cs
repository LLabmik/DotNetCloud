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

namespace DotNetCloud.UI.Web.Client.Pages;

/// <summary>Code-behind for the <see cref="Profile"/> component.</summary>
public partial class Profile : ComponentBase
{
    [Inject] private DotNetCloudApiClient Api { get; set; } = default!;
    [Inject] private ToastService Toast { get; set; } = default!;
    [Inject] private IJSRuntime JS { get; set; } = default!;

    private bool _loading = true;
    private UserProfileResponse? _profile;
    private long _avatarCacheBuster = DateTimeOffset.UtcNow.ToUnixTimeSeconds();

    // Profile editing
    private ProfileEditModel? _profileModel;
    private bool _profileSaving;
    private string? _profileError;
    private string? _profileSuccess;

    // Password change
    private PasswordChangeModel? _passwordModel;
    private string? _confirmPassword;
    private bool _passwordSaving;
    private string? _passwordError;
    private string? _passwordSuccess;

    // Timezone detection
    private bool _detectingTimezone;

    // Avatar
    private bool _avatarUploading;
    private string? _avatarError;

    protected override async Task OnInitializedAsync()
    {
        try
        {
            _profile = await Api.GetCurrentUserProfileAsync();
            if (_profile is not null)
            {
                _profileModel = new ProfileEditModel
                {
                    DisplayName = _profile.DisplayName,
                    Locale = _profile.Locale,
                    Timezone = _profile.Timezone,
                };
            }
        }
        catch
        {
            Toast.ShowError("Failed to load profile.");
        }
        finally
        {
            _passwordModel = new PasswordChangeModel();
            _loading = false;
        }
    }

    private string GetInitials()
    {
        if (_profile is null) return "?";

        var name = _profile.DisplayName;
        if (string.IsNullOrWhiteSpace(name))
        {
            name = _profile.Username;
        }

        if (string.IsNullOrWhiteSpace(name)) return "?";

        // Try splitting on space for "First Last"
        var parts = name.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length >= 2)
        {
            return $"{char.ToUpperInvariant(parts[0][0])}{char.ToUpperInvariant(parts[^1][0])}";
        }

        // Single name or email — just use first character
        return char.ToUpperInvariant(name[0]).ToString();
    }

    // ── Timezone Detection ──

    private async Task DetectTimezoneAsync()
    {
        if (_profileModel is null) return;
        _detectingTimezone = true;
        try
        {
            var tz = await JS.InvokeAsync<string>("dotnetcloud.getTimezone");
            if (!string.IsNullOrWhiteSpace(tz))
            {
                _profileModel.Timezone = tz;
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

    // ── Profile Save ──

    private async Task SaveProfileAsync()
    {
        if (_profile is null || _profileModel is null) return;
        _profileError = null;
        _profileSuccess = null;
        _profileSaving = true;

        try
        {
            var dto = new UpdateUserDto
            {
                DisplayName = _profileModel.DisplayName,
                Locale = _profileModel.Locale,
                Timezone = _profileModel.Timezone,
            };

            var result = await Api.UpdateUserAsync(_profile.UserId, dto);
            if (result is null)
            {
                _profileError = "Failed to update profile.";
                return;
            }

            _profile.DisplayName = result.DisplayName;
            _profile.Locale = result.Locale;
            _profile.Timezone = result.Timezone;
            _profileModel.DisplayName = result.DisplayName;
            _profileModel.Locale = result.Locale;
            _profileModel.Timezone = result.Timezone;
            _profileSuccess = "Profile updated successfully.";
            Toast.ShowSuccess("Profile updated.");
        }
        catch (ApiException ex)
        {
            _profileError = ex.Message;
        }
        catch (HttpRequestException)
        {
            _profileError = "Failed to communicate with the server.";
        }
        finally
        {
            _profileSaving = false;
        }
    }

    // ── Password Change ──

    private async Task ChangePasswordAsync()
    {
        if (_passwordModel is null) return;
        _passwordError = null;
        _passwordSuccess = null;

        if (string.IsNullOrWhiteSpace(_passwordModel.CurrentPassword) ||
            string.IsNullOrWhiteSpace(_passwordModel.NewPassword))
        {
            _passwordError = "Both current and new passwords are required.";
            return;
        }

        if (_passwordModel.NewPassword != _confirmPassword)
        {
            _passwordError = "New password and confirmation do not match.";
            return;
        }

        _passwordSaving = true;

        try
        {
            var request = new ChangePasswordRequest
            {
                CurrentPassword = _passwordModel.CurrentPassword,
                NewPassword = _passwordModel.NewPassword,
            };

            var (success, errorMsg) = await Api.ChangePasswordAsync(request);
            if (!success)
            {
                _passwordError = "Current password is incorrect or new password does not meet requirements.";
                return;
            }

            _passwordSuccess = "Password changed successfully.";
            _passwordModel = new PasswordChangeModel();
            _confirmPassword = null;
            Toast.ShowSuccess("Password changed.");
        }
        catch (HttpRequestException)
        {
            _passwordError = "Failed to communicate with the server.";
        }
        finally
        {
            _passwordSaving = false;
        }
    }

    // ── Avatar Upload ──

    private async Task HandleAvatarUploadAsync(InputFileChangeEventArgs e)
    {
        if (_profile is null) return;
        _avatarError = null;
        _avatarUploading = true;

        try
        {
            var file = e.File;
            if (file.Size > 5 * 1024 * 1024)
            {
                _avatarError = "Image must be smaller than 5 MB.";
                return;
            }

            var allowedTypes = new[] { "image/jpeg", "image/png", "image/gif", "image/webp" };
            if (!allowedTypes.Contains(file.ContentType, StringComparer.OrdinalIgnoreCase))
            {
                _avatarError = "Only JPEG, PNG, GIF, and WebP images are allowed.";
                return;
            }

            using var stream = file.OpenReadStream(maxAllowedSize: 5 * 1024 * 1024);
            using var ms = new MemoryStream();
            await stream.CopyToAsync(ms);
            ms.Position = 0;

            var content = new StreamContent(ms);
            content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue(file.ContentType);

            var avatarUrl = await Api.UploadAvatarAsync(_profile.UserId, content, file.Name);
            if (avatarUrl is not null)
            {
                _profile.AvatarUrl = avatarUrl;
                _avatarCacheBuster = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
                Toast.ShowSuccess("Avatar updated.");
            }
            else
            {
                _avatarError = "Failed to upload avatar.";
            }
        }
        catch (Exception)
        {
            _avatarError = "Failed to upload avatar.";
        }
        finally
        {
            _avatarUploading = false;
        }
    }

    private async Task RemoveAvatarAsync()
    {
        if (_profile is null) return;
        _avatarError = null;
        _avatarUploading = true;

        try
        {
            var deleted = await Api.DeleteAvatarAsync(_profile.UserId);
            if (deleted)
            {
                _profile.AvatarUrl = null;
                Toast.ShowSuccess("Avatar removed.");
            }
            else
            {
                _avatarError = "Failed to remove avatar.";
            }
        }
        catch
        {
            _avatarError = "Failed to remove avatar.";
        }
        finally
        {
            _avatarUploading = false;
        }
    }

    // ── Locale Options ──

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

    // ── Models ──

    private sealed class ProfileEditModel
    {
        public string DisplayName { get; set; } = string.Empty;
        public string Locale { get; set; } = "en-US";
        public string Timezone { get; set; } = "UTC";
    }

    private sealed class PasswordChangeModel
    {
        public string CurrentPassword { get; set; } = string.Empty;
        public string NewPassword { get; set; } = string.Empty;
    }
}
