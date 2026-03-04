using Microsoft.AspNetCore.Components;

namespace DotNetCloud.Modules.Files.UI;

/// <summary>
/// Code-behind for the Files module admin settings page.
/// </summary>
public partial class FilesAdminSettings : ComponentBase
{
    /// <summary>Raised when the user saves settings, passing the updated view model.</summary>
    [Parameter] public EventCallback<AdminSettingsViewModel> OnSettingsSaved { get; set; }

    private AdminSettingsViewModel _settings = new();
    private bool _isSaving;
    private bool _isSaved;
    private string _errorMessage = string.Empty;

    /// <summary>The settings currently being edited.</summary>
    protected AdminSettingsViewModel Settings => _settings;

    /// <summary>Whether a save operation is in progress.</summary>
    protected bool IsSaving => _isSaving;

    /// <summary>Whether the last save operation succeeded (shows the success banner).</summary>
    protected bool IsSaved => _isSaved;

    /// <summary>Error message to display, or empty if there is no error.</summary>
    protected string ErrorMessage => _errorMessage;

    /// <summary>
    /// Initializes the settings form with values supplied by the host page,
    /// or defaults if no initial values are provided.
    /// </summary>
    [Parameter] public AdminSettingsViewModel? InitialSettings { get; set; }

    /// <inheritdoc />
    protected override void OnParametersSet()
    {
        if (InitialSettings is not null)
        {
            _settings = new AdminSettingsViewModel
            {
                DefaultQuotaGb = InitialSettings.DefaultQuotaGb,
                TrashRetentionDays = InitialSettings.TrashRetentionDays,
                MaxVersionsPerFile = InitialSettings.MaxVersionsPerFile,
                VersionRetentionDays = InitialSettings.VersionRetentionDays,
                MaxUploadMb = InitialSettings.MaxUploadMb,
                AllowedExtensions = InitialSettings.AllowedExtensions,
                BlockedExtensions = InitialSettings.BlockedExtensions,
                StoragePath = InitialSettings.StoragePath
            };
        }
    }

    /// <summary>Validates and saves the current settings.</summary>
    protected async Task SaveSettings()
    {
        _errorMessage = string.Empty;
        _isSaved = false;

        if (!ValidateSettings(out var error))
        {
            _errorMessage = error;
            return;
        }

        _isSaving = true;
        StateHasChanged();

        await OnSettingsSaved.InvokeAsync(_settings);

        _isSaving = false;
        _isSaved = true;
    }

    /// <summary>Resets all fields to their default values.</summary>
    protected void ResetToDefaults()
    {
        _settings = new AdminSettingsViewModel();
        _isSaved = false;
        _errorMessage = string.Empty;
    }

    private bool ValidateSettings(out string error)
    {
        if (_settings.DefaultQuotaGb < 0)
        {
            error = "Default quota cannot be negative.";
            return false;
        }

        if (_settings.MaxUploadMb < 1)
        {
            error = "Maximum upload size must be at least 1 MB.";
            return false;
        }

        if (_settings.TrashRetentionDays < 1)
        {
            error = "Trash retention must be at least 1 day.";
            return false;
        }

        if (_settings.MaxVersionsPerFile < 0)
        {
            error = "Maximum versions cannot be negative.";
            return false;
        }

        if (_settings.VersionRetentionDays < 0)
        {
            error = "Version retention cannot be negative.";
            return false;
        }

        error = string.Empty;
        return true;
    }
}
