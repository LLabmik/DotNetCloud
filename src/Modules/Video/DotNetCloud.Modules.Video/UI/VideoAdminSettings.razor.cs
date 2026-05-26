using DotNetCloud.Core.Services;
using Microsoft.AspNetCore.Components;

namespace DotNetCloud.Modules.Video.UI;

/// <summary>
/// Blazor component for Video module admin settings.
/// </summary>
public partial class VideoAdminSettings : ComponentBase
{
    private const string Module = "dotnetcloud.video";

    private VideoAdminSettingsViewModel _settings = new();

    /// <summary>Callback invoked when settings are saved.</summary>
    [Parameter] public EventCallback<VideoAdminSettingsViewModel> OnSettingsSaved { get; set; }

    /// <summary>Initial settings to populate the form.</summary>
    [Parameter] public VideoAdminSettingsViewModel? InitialSettings { get; set; }

    /// <summary>Whether the save operation is in progress.</summary>
    protected bool IsSaving { get; set; }

    /// <summary>Whether settings were just saved successfully.</summary>
    protected bool IsSaved { get; set; }

    /// <summary>Current error message, if any.</summary>
    protected string? ErrorMessage { get; set; }

    /// <summary>The current settings model.</summary>
    protected VideoAdminSettingsViewModel Settings
    {
        get => _settings;
        set => _settings = value;
    }

    [Inject] private IAdminSettingsService AdminSettingsService { get; set; } = null!;

    /// <inheritdoc />
    protected override void OnInitialized()
    {
        if (InitialSettings is not null)
        {
            _settings = new VideoAdminSettingsViewModel
            {
                TmdbApiKey = InitialSettings.TmdbApiKey
            };
        }
    }

    /// <summary>
    /// Saves the settings to the system settings database.
    /// </summary>
    protected async Task SaveSettings()
    {
        IsSaving = true;
        IsSaved = false;
        ErrorMessage = null;

        try
        {
            await AdminSettingsService.UpsertSettingAsync(
                Module, "TmdbApiKey",
                new Core.DTOs.UpsertSystemSettingDto
                {
                    Value = _settings.TmdbApiKey?.Trim() ?? string.Empty,
                    Description = "TMDB API key for movie and TV series metadata enrichment"
                });

            IsSaved = true;
            await OnSettingsSaved.InvokeAsync(_settings);
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Failed to save settings: {ex.Message}";
        }
        finally
        {
            IsSaving = false;
        }
    }
}
