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

/// <summary>Code-behind for the <see cref="FilesSettings"/> component.</summary>
public partial class FilesSettings : ComponentBase
{
    [Inject] private DotNetCloudApiClient Api { get; set; } = default!;
    [Inject] private ToastService Toast { get; set; } = default!;

    private const string Module = "dotnetcloud.files";
    private const string EnabledKey = "VersionRetention:Enabled";
    private const string MaxNumberKey = "VersionRetention:MaxNumber";
    private const string MaxDaysKey = "VersionRetention:MaxDays";
    private const string DefaultContentEnabledKey = "DefaultUserContent:Enabled";

    private bool _loading = true;
    private bool _saving;
    private bool _pruning;
    private string _errorMessage = string.Empty;
    private string _successMessage = string.Empty;
    private string _pruneMessage = string.Empty;

    private bool _enabled = true;
    private int _maxNumber = 50;
    private int _maxDays;
    private bool _defaultContentEnabled = true;

    private FileVersioningEffectiveSettingsDto? _effective;

    private string CleanupCadence => _effective is null
        ? "daily"
        : _effective.CleanupIntervalHours >= 24
            ? $"{Math.Round(_effective.CleanupIntervalHours / 24, 1)} day(s)"
            : $"{Math.Round(_effective.CleanupIntervalHours, 1)} hour(s)";

    protected override async Task OnInitializedAsync() => await LoadAsync();

    private async Task LoadAsync()
    {
        _loading = true;
        try
        {
            // Show what is actually in force: the configured Files:VersionRetention values with any
            // administrator edits already layered on top.
            _effective = await Api.GetFileVersioningEffectiveSettingsAsync();

            if (_effective is null)
            {
                _errorMessage = "The Files module is not reachable, so the effective versioning policy " +
                    "could not be read. Values below are defaults and may not match the server.";
            }
            else
            {
                PopulateFields(_effective);
            }

            // The default-content toggle is authoritative in the Files module, so read the
            // effective value instead of guessing from configuration. A failure here must not
            // hide the versioning settings that loaded successfully.
            try
            {
                var defaultContent = await Api.GetDefaultContentStatusAsync();
                if (defaultContent is not null)
                    _defaultContentEnabled = defaultContent.Enabled;
            }
            catch
            {
                // Keep the default (enabled) and let the save path surface any real error.
            }
        }
        catch
        {
            _errorMessage = "Failed to load the file versioning settings.";
        }
        finally
        {
            _loading = false;
        }
    }

    private void PopulateFields(FileVersioningEffectiveSettingsDto effective)
    {
        _enabled = effective.Enabled;
        _maxNumber = effective.MaxNumber;
        _maxDays = effective.MaxDays;
    }

    private async Task SaveAsync()
    {
        _errorMessage = string.Empty;
        _successMessage = string.Empty;

        if (_maxNumber < 0 || _maxNumber > 1000)
        {
            _errorMessage = "Maximum versions per file must be between 0 and 1000.";
            return;
        }

        if (_maxDays < 0 || _maxDays > 3650)
        {
            _errorMessage = "Version retention must be between 0 and 3650 days.";
            return;
        }

        _saving = true;
        try
        {
            var results = await Task.WhenAll(
                Api.UpsertSettingAsync(Module, EnabledKey, Str(_enabled.ToString())),
                Api.UpsertSettingAsync(Module, MaxNumberKey, Str(_maxNumber.ToString())),
                Api.UpsertSettingAsync(Module, MaxDaysKey, Str(_maxDays.ToString())),
                Api.UpsertSettingAsync(Module, DefaultContentEnabledKey, Str(_defaultContentEnabled.ToString())));

            if (results.All(r => r))
            {
                _successMessage = "File versioning settings saved and applied.";
                Toast.ShowSuccess("File versioning settings saved and applied.");
                await ReloadEffectiveAsync();
            }
            else
            {
                _errorMessage = "Some settings could not be saved.";
            }
        }
        catch
        {
            _errorMessage = "Failed to save the file versioning settings.";
        }
        finally
        {
            _saving = false;
        }
    }

    private async Task RunPruneAsync()
    {
        _pruning = true;
        _pruneMessage = string.Empty;
        try
        {
            var result = await Api.RunFileVersionPruneAsync();
            if (result is null)
            {
                _pruneMessage = "The retention pass could not be started — the Files module is not reachable.";
                return;
            }

            _pruneMessage = result.VersionsDeleted > 0
                ? $"Retention pass complete: removed {result.VersionsDeleted} version(s) across {result.FilesScanned} file(s)."
                : $"Retention pass complete: nothing to remove across {result.FilesScanned} file(s).";

            await ReloadEffectiveAsync();
        }
        catch
        {
            _pruneMessage = "Failed to run the retention pass.";
        }
        finally
        {
            _pruning = false;
        }
    }

    private async Task ReloadEffectiveAsync()
    {
        _effective = await Api.GetFileVersioningEffectiveSettingsAsync();
        if (_effective is not null)
        {
            PopulateFields(_effective);
        }
    }

    private static UpsertSystemSettingDto Str(string value) => new() { Value = value };
}
