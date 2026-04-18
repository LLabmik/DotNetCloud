using Microsoft.AspNetCore.Components;

namespace DotNetCloud.Modules.AI.UI;

/// <summary>
/// Code-behind for the AI module admin settings page.
/// Covers LLM provider selection, API endpoint, authentication,
/// default model, and request limits.
/// </summary>
public partial class AiAdminSettings : ComponentBase
{
    /// <summary>Raised when the user saves settings, passing the updated view model.</summary>
    [Parameter] public EventCallback<AiAdminSettingsViewModel> OnSettingsSaved { get; set; }

    /// <summary>
    /// Initializes the settings form with values supplied by the host page,
    /// or defaults if no initial values are provided.
    /// </summary>
    [Parameter] public AiAdminSettingsViewModel? InitialSettings { get; set; }

    private AiAdminSettingsViewModel _settings = new();
    private bool _isSaving;
    private bool _isSaved;
    private string _errorMessage = string.Empty;

    /// <summary>The settings currently being edited.</summary>
    protected AiAdminSettingsViewModel Settings => _settings;

    /// <summary>Whether a save operation is in progress.</summary>
    protected bool IsSaving => _isSaving;

    /// <summary>Whether the last save operation succeeded (shows the success banner).</summary>
    protected bool IsSaved => _isSaved;

    /// <summary>Error message to display, or empty if there is no error.</summary>
    protected string ErrorMessage => _errorMessage;

    /// <inheritdoc />
    protected override void OnParametersSet()
    {
        if (InitialSettings is not null)
        {
            _settings = new AiAdminSettingsViewModel
            {
                Provider = InitialSettings.Provider,
                ApiBaseUrl = InitialSettings.ApiBaseUrl,
                ApiKey = InitialSettings.ApiKey,
                OrganizationId = InitialSettings.OrganizationId,
                DefaultModel = InitialSettings.DefaultModel,
                MaxTokens = InitialSettings.MaxTokens,
                RequestTimeoutSeconds = InitialSettings.RequestTimeoutSeconds
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
        _settings = new AiAdminSettingsViewModel();
        _isSaved = false;
        _errorMessage = string.Empty;
    }

    /// <summary>Returns an appropriate URL placeholder for the selected provider.</summary>
    protected string GetUrlPlaceholder() => _settings.Provider switch
    {
        "openai" => "https://api.openai.com/",
        "anthropic" => "https://api.anthropic.com/",
        _ => "http://localhost:11434/"
    };

    /// <summary>Returns an appropriate model placeholder for the selected provider.</summary>
    protected string GetModelPlaceholder() => _settings.Provider switch
    {
        "openai" => "gpt-4o",
        "anthropic" => "claude-sonnet-4-20250514",
        _ => "gpt-oss:20b"
    };

    private bool ValidateSettings(out string error)
    {
        if (string.IsNullOrWhiteSpace(_settings.ApiBaseUrl))
        {
            error = "API base URL is required.";
            return false;
        }

        if (!Uri.TryCreate(_settings.ApiBaseUrl, UriKind.Absolute, out var uri)
            || (uri.Scheme != "http" && uri.Scheme != "https"))
        {
            error = "API base URL must be a valid HTTP or HTTPS URL.";
            return false;
        }

        if (_settings.Provider is "openai" or "anthropic"
            && string.IsNullOrWhiteSpace(_settings.ApiKey))
        {
            error = $"An API key is required for the {_settings.Provider} provider.";
            return false;
        }

        if (string.IsNullOrWhiteSpace(_settings.DefaultModel))
        {
            error = "A default model name is required.";
            return false;
        }

        if (_settings.MaxTokens < 0)
        {
            error = "Max tokens cannot be negative.";
            return false;
        }

        if (_settings.RequestTimeoutSeconds < 10)
        {
            error = "Request timeout must be at least 10 seconds.";
            return false;
        }

        error = string.Empty;
        return true;
    }
}
