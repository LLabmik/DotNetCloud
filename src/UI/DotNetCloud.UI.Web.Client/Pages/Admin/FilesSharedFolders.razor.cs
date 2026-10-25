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

/// <summary>Code-behind for the <see cref="FilesSharedFolders"/> component.</summary>
public partial class FilesSharedFolders : ComponentBase, IDisposable
{
    [Inject] private DotNetCloudApiClient Api { get; set; } = default!;
    [Inject] private ToastService Toast { get; set; } = default!;

    private readonly SharedFolderEditorState _editor = new();
    private bool _loading = true;
    private bool _actionInProgress;
    private bool _showEditor;
    private bool _showSchedule;
    private bool _showSourcePicker;
    private bool _sourcePickerLoading;
    private bool _groupsLoading;
    private string _error = string.Empty;
    private string _modalError = string.Empty;
    private string _sourcePickerError = string.Empty;
    private Guid _editorGroupOrganizationId;
    private string _scheduleScanLocal = string.Empty;
    private SharedFolderEditorMode _editorMode;
    private AdminSharedFolderResponse? _scheduleTarget;
    private AdminSharedFolderBrowseResponse? _sourcePicker;
    private IReadOnlyList<OrganizationDto> _organizations = [];
    private IReadOnlyList<GroupDto> _availableGroups = [];
    private IReadOnlyList<AdminSharedFolderResponse> _sharedFolders = [];
    private ConfirmDialog _confirmDialog = default!;

    private AdminSharedFolderCleanupStatusResponse? _cleanupJob;
    private System.Threading.Timer? _pollTimer;

    private bool IsEditorScheduleDisabled => string.Equals(_editor.CrawlMode, "Manual", StringComparison.OrdinalIgnoreCase);
    private bool CanSourcePickerGoUp => _sourcePicker is not null && !string.IsNullOrWhiteSpace(_sourcePicker.RelativePath);

    protected override async Task OnInitializedAsync()
    {
        await RefreshAsync();
    }

    private async Task RefreshAsync()
    {
        _loading = true;
        _error = string.Empty;

        try
        {
            var organizationsTask = Api.ListOrganizationsAsync();
            var sharedFoldersTask = Api.ListAdminSharedFoldersAsync();

            await Task.WhenAll(organizationsTask, sharedFoldersTask);

            _organizations = organizationsTask.Result;
            _sharedFolders = sharedFoldersTask.Result;

            if (_organizations.Count > 0)
            {
                if (_editorGroupOrganizationId == Guid.Empty || _organizations.All(org => org.Id != _editorGroupOrganizationId))
                {
                    _editorGroupOrganizationId = _organizations[0].Id;
                }

                await LoadGroupsAsync(_editorGroupOrganizationId);
            }
            else
            {
                _availableGroups = [];
            }
        }
        catch (Exception ex)
        {
            _error = ex.Message;
        }
        finally
        {
            _loading = false;
        }
    }

    private void ShowCreate()
    {
        _modalError = string.Empty;
        _editorMode = SharedFolderEditorMode.Create;
        _editor.Reset();
        CloseSourcePicker();
        _editorGroupOrganizationId = _organizations.Count > 0 ? _organizations[0].Id : Guid.Empty;
        _showEditor = true;
    }

    private async Task ShowEditAsync(AdminSharedFolderResponse folder)
    {
        _modalError = string.Empty;
        _editorMode = SharedFolderEditorMode.Edit;
        _editor.Load(folder);
        CloseSourcePicker();

        var suggestedOrganizationId = folder.OrganizationId
            ?? folder.GrantedGroups.Select(group => group.OrganizationId).FirstOrDefault(id => id.HasValue)
            ?? (_organizations.Count > 0 ? _organizations[0].Id : Guid.Empty);

        _editorGroupOrganizationId = suggestedOrganizationId;
        if (_editorGroupOrganizationId != Guid.Empty)
        {
            await LoadGroupsAsync(_editorGroupOrganizationId);
        }

        _showEditor = true;
    }

    private void CloseEditor()
    {
        CloseSourcePicker();
        _showEditor = false;
        _modalError = string.Empty;
        _actionInProgress = false;
    }

    private void ShowSchedule(AdminSharedFolderResponse folder)
    {
        _scheduleTarget = folder;
        _scheduleScanLocal = ToDateTimeLocalValue(folder.NextScheduledScanAt);
        _modalError = string.Empty;
        _showSchedule = true;
    }

    private void CloseSchedule()
    {
        _showSchedule = false;
        _scheduleTarget = null;
        _scheduleScanLocal = string.Empty;
        _modalError = string.Empty;
        _actionInProgress = false;
    }

    private async Task OnEditorOrganizationChanged(ChangeEventArgs args)
    {
        if (!Guid.TryParse(args.Value?.ToString(), out var organizationId))
        {
            return;
        }

        _editorGroupOrganizationId = organizationId;
        await LoadGroupsAsync(organizationId);
        _editor.SelectedGroupIds.RemoveWhere(groupId => _availableGroups.All(group => group.Id != groupId));
    }

    private async Task LoadGroupsAsync(Guid organizationId)
    {
        if (organizationId == Guid.Empty)
        {
            _availableGroups = [];
            return;
        }

        _groupsLoading = true;
        StateHasChanged();

        try
        {
            _availableGroups = await Api.ListGroupsAsync(organizationId);
        }
        catch (Exception ex)
        {
            _availableGroups = [];
            _modalError = ex.Message;
        }
        finally
        {
            _groupsLoading = false;
        }
    }

    private void ToggleGroup(Guid groupId, bool isSelected)
    {
        if (isSelected)
        {
            _editor.SelectedGroupIds.Add(groupId);
        }
        else
        {
            _editor.SelectedGroupIds.Remove(groupId);
        }
    }

    private void OnEditorDisplayNameChanged(ChangeEventArgs args)
    {
        _editor.DisplayName = args.Value?.ToString() ?? string.Empty;
    }

    private void OnEditorSourcePathChanged(ChangeEventArgs args)
    {
        _editor.SourcePath = args.Value?.ToString() ?? string.Empty;
    }

    private void OnEditorCrawlModeChanged(ChangeEventArgs args)
    {
        _editor.ApplyCrawlMode(args.Value?.ToString());
    }

    private void OnEditorScheduleChanged(ChangeEventArgs args)
    {
        _editor.NextScheduledScanAtLocal = args.Value?.ToString() ?? string.Empty;
    }

    private void OnScheduleScanChanged(ChangeEventArgs args)
    {
        _scheduleScanLocal = args.Value?.ToString() ?? string.Empty;
    }

    private async Task SaveEditorAsync()
    {
        _modalError = string.Empty;
        _editor.EnsureScheduledDefault();
        if (!ValidateEditor())
        {
            return;
        }

        _actionInProgress = true;
        try
        {
            var request = new SaveAdminSharedFolderRequest
            {
                DisplayName = _editor.DisplayName.Trim(),
                SourcePath = _editor.SourcePath.Trim(),
                IsEnabled = _editor.IsEnabled,
                AccessMode = _editor.AccessMode,
                CrawlMode = _editor.CrawlMode,
                NextScheduledScanAt = string.Equals(_editor.CrawlMode, "Scheduled", StringComparison.OrdinalIgnoreCase)
                    ? ParseDateTimeLocal(_editor.NextScheduledScanAtLocal)
                    : null,
                GroupIds = _editor.SelectedGroupIds.OrderBy(id => id).ToArray(),
            };

            if (_editorMode == SharedFolderEditorMode.Create)
            {
                var created = await Api.CreateAdminSharedFolderAsync(request);
                if (created is not null)
                {
                    Toast.ShowSuccess($"Shared folder '{created.DisplayName}' created.");
                }
            }
            else if (_editor.Id.HasValue)
            {
                var updated = await Api.UpdateAdminSharedFolderAsync(_editor.Id.Value, request);
                if (updated is not null)
                {
                    Toast.ShowSuccess($"Shared folder '{updated.DisplayName}' updated.");
                }
            }

            CloseEditor();
            await RefreshAsync();
        }
        catch (Exception ex)
        {
            _modalError = ex.Message;
        }
        finally
        {
            _actionInProgress = false;
        }
    }

    private async Task OpenSourcePickerAsync()
    {
        _showSourcePicker = true;
        var preferredPath = string.IsNullOrWhiteSpace(_editor.SourcePath)
            ? null
            : _editor.SourcePath.Trim();

        await LoadSourcePickerAsync(preferredPath, fallbackToRoot: !string.IsNullOrWhiteSpace(preferredPath));
    }

    private void CloseSourcePicker()
    {
        _showSourcePicker = false;
        _sourcePickerLoading = false;
        _sourcePickerError = string.Empty;
        _sourcePicker = null;
    }

    private async Task NavigateSourcePickerToRootAsync()
    {
        await LoadSourcePickerAsync(null);
    }

    private async Task NavigateSourcePickerAsync(string sourcePath)
    {
        await LoadSourcePickerAsync(sourcePath);
    }

    private async Task NavigateSourcePickerUpAsync()
    {
        if (_sourcePicker is null)
        {
            return;
        }

        if (!CanSourcePickerGoUp)
        {
            await LoadSourcePickerAsync(null);
            return;
        }

        var parent = Directory.GetParent(_sourcePicker.CurrentPath);
        var parentPath = parent?.FullName;
        await LoadSourcePickerAsync(string.IsNullOrWhiteSpace(parentPath) ? null : parentPath);
    }

    private Task UseSourcePickerCurrentFolderAsync()
    {
        if (_sourcePicker is null)
        {
            return Task.CompletedTask;
        }

        _editor.ApplyPickedSourcePath(_sourcePicker.CurrentPath);
        CloseSourcePicker();
        return Task.CompletedTask;
    }

    private async Task LoadSourcePickerAsync(string? sourcePath, bool fallbackToRoot = false)
    {
        _sourcePickerLoading = true;
        _sourcePickerError = string.Empty;

        try
        {
            _sourcePicker = await Api.BrowseAdminSharedFolderDirectoriesAsync(sourcePath);
        }
        catch (Exception) when (fallbackToRoot && !string.IsNullOrWhiteSpace(sourcePath))
        {
            try
            {
                _sourcePicker = await Api.BrowseAdminSharedFolderDirectoriesAsync();
            }
            catch (Exception fallbackEx)
            {
                _sourcePicker = null;
                _sourcePickerError = fallbackEx.Message;
            }
        }
        catch (Exception ex)
        {
            _sourcePicker = null;
            _sourcePickerError = ex.Message;
        }
        finally
        {
            _sourcePickerLoading = false;
        }
    }

    private async Task ScheduleScanAsync()
    {
        if (_scheduleTarget is null)
        {
            return;
        }

        _actionInProgress = true;
        _modalError = string.Empty;
        try
        {
            var scheduled = await Api.ScheduleAdminSharedFolderRescanAsync(_scheduleTarget.Id, ParseDateTimeLocal(_scheduleScanLocal));
            if (scheduled is not null)
            {
                Toast.ShowSuccess($"Scan scheduled for '{scheduled.DisplayName}'.");
            }

            CloseSchedule();
            await RefreshAsync();
        }
        catch (Exception ex)
        {
            _modalError = ex.Message;
        }
        finally
        {
            _actionInProgress = false;
        }
    }

    private async Task RescanNowAsync(AdminSharedFolderResponse folder)
    {
        _actionInProgress = true;
        try
        {
            await Api.ScheduleAdminSharedFolderRescanAsync(folder.Id, null);
            Toast.ShowSuccess($"Rescan queued for '{folder.DisplayName}'.");
            await RefreshAsync();
        }
        catch (Exception ex)
        {
            _error = ex.Message;
        }
        finally
        {
            _actionInProgress = false;
        }
    }

    private async Task ReindexAsync(AdminSharedFolderResponse folder)
    {
        _actionInProgress = true;
        try
        {
            await Api.RequestAdminSharedFolderReindexAsync(folder.Id);
            Toast.ShowSuccess($"Reindex requested for '{folder.DisplayName}'.");
            await RefreshAsync();
        }
        catch (Exception ex)
        {
            _error = ex.Message;
        }
        finally
        {
            _actionInProgress = false;
        }
    }

    private async Task DeleteAsync(AdminSharedFolderResponse folder)
    {
        if (!await _confirmDialog.ShowAsync($"Delete shared folder '{folder.DisplayName}'?", "Delete Shared Folder"))
        {
            return;
        }

        _actionInProgress = true;
        try
        {
            var result = await Api.DeleteAdminSharedFolderAsync(folder.Id);
            if (result is not null)
            {
                _cleanupJob = new AdminSharedFolderCleanupStatusResponse
                {
                    CleanupJobId = result.CleanupJobId,
                    SharedFolderId = folder.Id,
                    DisplayName = folder.DisplayName,
                    Phase = "DeletingDefinition",
                    SearchDocsTotal = result.PendingSearchRemovals,
                    SearchDocsRemoved = result.SearchDocsRemoved,
                };

                Toast.ShowSuccess($"Shared folder '{folder.DisplayName}' deleted. Cleanup started.");
                await RefreshAsync();

                // Start polling for cleanup progress
                _pollTimer?.Dispose();
                _pollTimer = new System.Threading.Timer(
                    async _ => await PollCleanupStatusAsync(),
                    null,
                    TimeSpan.FromSeconds(1),
                    TimeSpan.FromSeconds(2));
            }
            else
            {
                Toast.ShowSuccess($"Shared folder '{folder.DisplayName}' deleted.");
                await RefreshAsync();
            }
        }
        catch (Exception ex)
        {
            _error = ex.Message;
        }
        finally
        {
            _actionInProgress = false;
        }
    }

    private async Task PollCleanupStatusAsync()
    {
        if (_cleanupJob is null)
        {
            return;
        }

        try
        {
            var status = await Api.GetAdminSharedFolderCleanupStatusAsync(_cleanupJob.CleanupJobId);
            if (status is not null)
            {
                _cleanupJob = status;
                await InvokeAsync(StateHasChanged);

                if (status.IsComplete)
                {
                    _pollTimer?.Dispose();
                    _pollTimer = null;
                    if (status.Phase == "Complete")
                    {
                        await InvokeAsync(RefreshAsync);
                    }
                }
            }
        }
        catch
        {
            // Swallow polling errors — will retry on next interval
        }
    }

    private void DismissCleanup()
    {
        _pollTimer?.Dispose();
        _pollTimer = null;
        _cleanupJob = null;
    }

    private static string GetStepClass(string phase)
    {
        return "cleanup-step";
    }

    private static string GetStepIcon(string phase)
    {
        return "☐";
    }

    private bool HasStepCompleted(string phase)
    {
        if (_cleanupJob is null)
        {
            return false;
        }

        var phases = new[] { "DeletingDefinition", "RemovingSearchDocs", "CleaningMediaSources", "CleaningMediaEntities", "Complete", "Failed" };
        var currentIndex = Array.IndexOf(phases, _cleanupJob.Phase);
        var phaseIndex = Array.IndexOf(phases, phase);
        return phaseIndex >= 0 && currentIndex >= 0 && phaseIndex < currentIndex;
    }

    public void Dispose()
    {
        _pollTimer?.Dispose();
        _pollTimer = null;
    }

    private bool ValidateEditor()
    {
        if (string.IsNullOrWhiteSpace(_editor.DisplayName))
        {
            _modalError = "Display name is required.";
            return false;
        }

        if (string.IsNullOrWhiteSpace(_editor.SourcePath))
        {
            _modalError = "Source path is required.";
            return false;
        }

        if (_editor.SelectedGroupIds.Count == 0)
        {
            _modalError = "Select at least one group to grant access.";
            return false;
        }

        return true;
    }

    private string ResolveOrganizationName(Guid? organizationId)
    {
        if (!organizationId.HasValue)
        {
            return "Global scope";
        }

        return _organizations.FirstOrDefault(organization => organization.Id == organizationId.Value)?.Name
            ?? organizationId.Value.ToString();
    }

    private static string FormatSchedule(DateTime? value)
    {
        return value.HasValue
            ? value.Value.ToLocalTime().ToString("yyyy-MM-dd HH:mm")
            : "Not scheduled";
    }

    private static string FormatRelative(DateTime value)
    {
        var delta = DateTime.UtcNow - value;
        if (delta.TotalMinutes < 1)
        {
            return "just now";
        }

        if (delta.TotalHours < 1)
        {
            return $"{Math.Floor(delta.TotalMinutes)}m ago";
        }

        if (delta.TotalDays < 1)
        {
            return $"{Math.Floor(delta.TotalHours)}h ago";
        }

        return $"{Math.Floor(delta.TotalDays)}d ago";
    }

    private static string GetScanStatusClass(string status)
    {
        return status switch
        {
            "Succeeded" => "status-pill--success",
            "Failed" => "status-pill--danger",
            _ => "status-pill--neutral",
        };
    }

    private static string GetReindexStateClass(string state)
    {
        return state switch
        {
            "Running" => "status-pill--progress",
            "Requested" => "status-pill--warning",
            _ => "status-pill--neutral",
        };
    }

    private static string ToDateTimeLocalValue(DateTime? value)
    {
        return value.HasValue
            ? value.Value.ToLocalTime().ToString("yyyy-MM-ddTHH:mm")
            : string.Empty;
    }

    private string GetSourcePickerRootLabel()
    {
        if (_sourcePicker is null || string.IsNullOrWhiteSpace(_sourcePicker.RootPath))
        {
            return "Root";
        }

        if (Path.GetDirectoryName(_sourcePicker.RootPath) is null)
        {
            return _sourcePicker.RootPath;
        }

        var trimmedRoot = Path.TrimEndingDirectorySeparator(_sourcePicker.RootPath);
        var label = Path.GetFileName(trimmedRoot);
        return string.IsNullOrWhiteSpace(label)
            ? trimmedRoot
            : label;
    }

    private IReadOnlyList<SourcePickerBreadcrumb> GetSourcePickerBreadcrumbs()
    {
        if (_sourcePicker is null || string.IsNullOrWhiteSpace(_sourcePicker.RelativePath))
        {
            return [];
        }

        var breadcrumbs = new List<SourcePickerBreadcrumb>();
        var segments = _sourcePicker.RelativePath
            .Split('/', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        for (var index = 0; index < segments.Length; index++)
        {
            var relativePath = string.Join('/', segments.Take(index + 1));
            var sourcePath = Path.GetFullPath(Path.Combine(
                _sourcePicker.RootPath,
                relativePath.Replace('/', Path.DirectorySeparatorChar)));

            breadcrumbs.Add(new SourcePickerBreadcrumb(segments[index], sourcePath));
        }

        return breadcrumbs;
    }

    private static DateTime? ParseDateTimeLocal(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        return DateTime.TryParse(value, out var parsed)
            ? DateTime.SpecifyKind(parsed, DateTimeKind.Local).ToUniversalTime()
            : null;
    }

    private sealed class SharedFolderEditorState
    {
        private string? _lastSuggestedDisplayName;

        public Guid? Id { get; private set; }

        public string DisplayName { get; set; } = string.Empty;

        public string SourcePath { get; set; } = string.Empty;

        public bool IsEnabled { get; set; } = true;

        public string AccessMode { get; set; } = "ReadOnly";

        public string CrawlMode { get; set; } = "Scheduled";

        public string NextScheduledScanAtLocal { get; set; } = string.Empty;

        public HashSet<Guid> SelectedGroupIds { get; } = [];

        public void Reset()
        {
            Id = null;
            DisplayName = string.Empty;
            SourcePath = string.Empty;
            IsEnabled = true;
            AccessMode = "ReadOnly";
            CrawlMode = "Scheduled";
            NextScheduledScanAtLocal = string.Empty;
            _lastSuggestedDisplayName = null;
            EnsureScheduledDefault();
            SelectedGroupIds.Clear();
        }

        public void Load(AdminSharedFolderResponse folder)
        {
            Id = folder.Id;
            DisplayName = folder.DisplayName;
            SourcePath = folder.SourcePath;
            IsEnabled = folder.IsEnabled;
            AccessMode = folder.AccessMode;
            CrawlMode = folder.CrawlMode;
            NextScheduledScanAtLocal = ToDateTimeLocalValue(folder.NextScheduledScanAt);
            _lastSuggestedDisplayName = MatchesSuggestedDisplayName(folder.DisplayName, folder.SourcePath)
                ? SuggestDisplayNameFromPath(folder.SourcePath)
                : null;
            EnsureScheduledDefault();
            SelectedGroupIds.Clear();

            foreach (var groupId in folder.GrantedGroups.Select(group => group.GroupId))
            {
                SelectedGroupIds.Add(groupId);
            }
        }

        public void ApplyPickedSourcePath(string sourcePath)
        {
            SourcePath = sourcePath;

            var suggestedDisplayName = SuggestDisplayNameFromPath(sourcePath);
            if (!string.IsNullOrWhiteSpace(suggestedDisplayName)
                && (string.IsNullOrWhiteSpace(DisplayName)
                    || string.Equals(DisplayName, _lastSuggestedDisplayName, StringComparison.Ordinal)))
            {
                DisplayName = suggestedDisplayName;
            }

            _lastSuggestedDisplayName = suggestedDisplayName;
        }

        public void ApplyCrawlMode(string? crawlMode)
        {
            CrawlMode = string.IsNullOrWhiteSpace(crawlMode)
                ? "Scheduled"
                : crawlMode;

            if (string.Equals(CrawlMode, "Scheduled", StringComparison.OrdinalIgnoreCase))
            {
                EnsureScheduledDefault();
                return;
            }

            NextScheduledScanAtLocal = string.Empty;
        }

        public void EnsureScheduledDefault()
        {
            if (string.Equals(CrawlMode, "Scheduled", StringComparison.OrdinalIgnoreCase)
                && string.IsNullOrWhiteSpace(NextScheduledScanAtLocal))
            {
                NextScheduledScanAtLocal = CreateDefaultScheduledScanLocalValue();
            }
        }

        private static string CreateDefaultScheduledScanLocalValue()
        {
            return DateTime.Now.AddHours(24).ToString("yyyy-MM-ddTHH:mm");
        }

        private static bool MatchesSuggestedDisplayName(string displayName, string sourcePath)
        {
            var suggestedDisplayName = SuggestDisplayNameFromPath(sourcePath);
            return !string.IsNullOrWhiteSpace(suggestedDisplayName)
                && string.Equals(displayName.Trim(), suggestedDisplayName, StringComparison.Ordinal);
        }

        private static string SuggestDisplayNameFromPath(string sourcePath)
        {
            if (string.IsNullOrWhiteSpace(sourcePath))
            {
                return string.Empty;
            }

            var lastSegment = Path.GetFileName(Path.TrimEndingDirectorySeparator(sourcePath.Trim()));
            if (string.IsNullOrWhiteSpace(lastSegment))
            {
                return string.Empty;
            }

            var normalized = string.Join(' ', lastSegment
                .Replace('-', ' ')
                .Replace('_', ' ')
                .Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));

            if (string.IsNullOrWhiteSpace(normalized))
            {
                return lastSegment;
            }

            return string.Equals(normalized, normalized.ToLowerInvariant(), StringComparison.Ordinal)
                ? System.Globalization.CultureInfo.CurrentCulture.TextInfo.ToTitleCase(normalized)
                : normalized;
        }
    }

    private sealed record SourcePickerBreadcrumb(string Label, string SourcePath);

    private enum SharedFolderEditorMode
    {
        Create,
        Edit,
    }
}
