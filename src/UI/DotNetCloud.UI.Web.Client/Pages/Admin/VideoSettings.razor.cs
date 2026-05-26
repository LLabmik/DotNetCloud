using DotNetCloud.Core.DTOs;
using DotNetCloud.Core.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Components;

namespace DotNetCloud.UI.Web.Client.Pages.Admin;

/// <summary>
/// Admin settings page for the Video module.
/// </summary>
public partial class VideoSettings : ComponentBase
{
    private const string Module = "dotnetcloud.video";

    private bool _loading = true;
    private bool _isSaving;
    private bool _isSaved;
    private string? _errorMessage;
    private string _tmdbApiKey = string.Empty;

    [Inject] private IAdminSettingsService AdminSettingsService { get; set; } = null!;

    protected override async Task OnInitializedAsync()
    {
        try
        {
            var setting = await AdminSettingsService.GetSettingAsync(Module, "TmdbApiKey");
            _tmdbApiKey = setting?.Value ?? string.Empty;
        }
        catch
        {
            // Show empty form on error
        }
        finally
        {
            _loading = false;
        }
    }

    private async Task SaveSettings()
    {
        _isSaving = true;
        _isSaved = false;
        _errorMessage = null;

        try
        {
            await AdminSettingsService.UpsertSettingAsync(
                Module, "TmdbApiKey",
                new UpsertSystemSettingDto
                {
                    Value = _tmdbApiKey?.Trim() ?? string.Empty,
                    Description = "TMDB API key for movie and TV series metadata enrichment"
                });

            _isSaved = true;
        }
        catch (Exception ex)
        {
            _errorMessage = $"Failed to save settings: {ex.Message}";
        }
        finally
        {
            _isSaving = false;
        }
    }
}
