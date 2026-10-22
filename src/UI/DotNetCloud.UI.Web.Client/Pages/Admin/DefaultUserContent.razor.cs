using DotNetCloud.UI.Web.Client.Services;
using Microsoft.AspNetCore.Components;

namespace DotNetCloud.UI.Web.Client.Pages.Admin;

/// <summary>
/// Read-only inspector for the administrator-managed default user content template,
/// plus convergence and export controls.
/// </summary>
public partial class DefaultUserContent : ComponentBase
{
    private bool _loading = true;
    private bool _working;
    private string? _errorMessage;
    private string? _successMessage;

    private DefaultUserContentStatusDto? _status;
    private DefaultContentQueueStatusDto? _queue;
    private IReadOnlyList<DefaultContentIssueDto> _issues = [];
    private IReadOnlyList<DefaultContentBrowseEntryDto> _browseEntries = [];
    private string _browsePath = string.Empty;

    private DefaultContentReconcileResultDto? _preview;

    private bool _publishUpdates = true;
    private bool _overwriteModified;
    private bool _confirmOverwrites;
    private bool _confirmDeletions;

    [Inject] private DotNetCloudApiClient Api { get; set; } = null!;

    /// <inheritdoc />
    protected override async Task OnInitializedAsync()
    {
        await RefreshAsync();
        _loading = false;
    }

    private async Task RefreshAsync()
    {
        _errorMessage = null;
        _working = true;

        try
        {
            _status = await Api.GetDefaultContentStatusAsync();
            _queue = await Api.GetDefaultContentQueueStatusAsync();
            _issues = _status?.Issues ?? [];

            if (_status is not null && _status.RootExists)
                await LoadBrowseCoreAsync(null);
        }
        catch (Exception ex)
        {
            _errorMessage = ex.Message;
        }
        finally
        {
            _working = false;
        }
    }

    private async Task ValidateAsync()
    {
        _errorMessage = null;
        _successMessage = null;
        _working = true;

        try
        {
            _status = await Api.GetDefaultContentStatusAsync();
            _issues = _status?.Issues ?? [];
            _successMessage = _issues.Count == 0
                ? "Template validated: no issues found."
                : $"Template validated: {_issues.Count} issue(s) found.";
        }
        catch (Exception ex)
        {
            _errorMessage = ex.Message;
        }
        finally
        {
            _working = false;
        }
    }

    private async Task LoadBrowseAsync(string? path)
    {
        _errorMessage = null;
        _working = true;

        try
        {
            await LoadBrowseCoreAsync(path);
        }
        catch (Exception ex)
        {
            _errorMessage = ex.Message;
        }
        finally
        {
            _working = false;
        }
    }

    private async Task LoadBrowseCoreAsync(string? path)
    {
        var result = await Api.BrowseDefaultContentAsync(path);
        _browsePath = result?.Path ?? string.Empty;
        _browseEntries = result?.Entries ?? [];
    }

    private async Task SeedDefaultsAsync()
    {
        _errorMessage = null;
        _successMessage = null;
        _working = true;

        try
        {
            await Api.SeedDefaultContentAsync();
            await RefreshAsync();
            _successMessage = "Default folders restored (additive only).";
        }
        catch (Exception ex)
        {
            _errorMessage = ex.Message;
        }
        finally
        {
            _working = false;
        }
    }

    private async Task PreviewAsync()
    {
        _errorMessage = null;
        _successMessage = null;
        _working = true;
        _preview = null;

        try
        {
            _preview = await Api.ReconcileDefaultContentAsync(
                dryRun: true,
                publishUpdates: false,
                overwriteModified: false,
                confirmDeletions: false,
                confirmOverwrites: false);

            _confirmDeletions = false;
            _confirmOverwrites = false;
            _publishUpdates = true;
            _overwriteModified = false;
        }
        catch (Exception ex)
        {
            _errorMessage = ex.Message;
        }
        finally
        {
            _working = false;
        }
    }

    private async Task RunConvergenceAsync()
    {
        _errorMessage = null;
        _successMessage = null;
        _working = true;

        try
        {
            var result = await Api.ReconcileDefaultContentAsync(
                dryRun: false,
                publishUpdates: _publishUpdates,
                overwriteModified: _overwriteModified,
                confirmDeletions: _confirmDeletions,
                confirmOverwrites: _confirmOverwrites);

            if (result is null)
            {
                _errorMessage = "The convergence run returned no result.";
                return;
            }

            if (result.RequiresConfirmation)
            {
                _errorMessage = result.Reason == "overwrite-modified"
                    ? "Acknowledge that user-modified copies will be replaced before continuing."
                    : "Confirm the pending deletions before continuing.";
                return;
            }

            _preview = null;
            _successMessage =
                $"Convergence completed. {result.UsersQueued} user(s) queued, " +
                $"{result.UsersProcessed} processed, {result.Updates} update(s), {result.Deletes} deletion(s).";

            await RefreshAsync();
        }
        catch (Exception ex)
        {
            _errorMessage = ex.Message;
        }
        finally
        {
            _working = false;
        }
    }

    private static string FormatSize(long bytes)
    {
        if (bytes < 1024)
            return $"{bytes} B";
        if (bytes < 1024L * 1024)
            return $"{bytes / 1024.0:F1} KB";
        if (bytes < 1024L * 1024 * 1024)
            return $"{bytes / (1024.0 * 1024):F1} MB";
        return $"{bytes / (1024.0 * 1024 * 1024):F2} GB";
    }
}
