using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;

namespace DotNetCloud.Modules.Files.UI;

/// <summary>
/// Code-behind for the file browser sidebar navigation component.
/// </summary>
public partial class FileSidebar : ComponentBase
{
    [Inject] private IJSRuntime Js { get; set; } = default!;

    /// <summary>Currently active navigation section.</summary>
    [Parameter] public FileSidebarSection ActiveSection { get; set; } = FileSidebarSection.AllFiles;

    /// <summary>Raised when the user selects a navigation section.</summary>
    [Parameter] public EventCallback<FileSidebarSection> OnSectionChanged { get; set; }

    /// <summary>Raised when the user selects a tag in the expandable tag list.</summary>
    [Parameter] public EventCallback<FileTagViewModel> OnTagSelected { get; set; }

    /// <summary>Current user quota to display at the bottom of the sidebar.</summary>
    [Parameter] public QuotaViewModel? Quota { get; set; }

    /// <summary>Number of items currently in the trash (shown as a badge).</summary>
    [Parameter] public int TrashItemCount { get; set; }

    /// <summary>Total size of items in the trash, in bytes.</summary>
    [Parameter] public long TrashBytes { get; set; }

    /// <summary>Available file tags to display in the expandable tag section.</summary>
    [Parameter] public IReadOnlyList<FileTagViewModel> Tags { get; set; } = [];

    private bool _tagsExpanded;
    private bool _sidebarCollapsed;

    /// <summary>Whether the Tags sub-list is expanded.</summary>
    protected bool IsTagsExpanded => _tagsExpanded;

    /// <summary>Whether the sidebar is collapsed (icons only).</summary>
    protected bool SidebarCollapsed => _sidebarCollapsed;

    /// <summary>Toggles the Tags sub-list open or closed.</summary>
    protected void ToggleTagsExpanded() => _tagsExpanded = !_tagsExpanded;

    /// <summary>Toggles the sidebar collapsed state.</summary>
    protected void ToggleCollapsed() => _sidebarCollapsed = !_sidebarCollapsed;

    /// <summary>Permanently collapse the sidebar (preserves state in localStorage).</summary>
    protected void CollapseSidebar() => _sidebarCollapsed = true;

    /// <summary>Permanently expand the sidebar (preserves state in localStorage).</summary>
    protected void ExpandSidebar() => _sidebarCollapsed = false;

    /// <summary>Initialize sidebar collapse state from localStorage.</summary>
    protected override async Task OnInitializedAsync()
    {
        await base.OnInitializedAsync();
        await LoadSidebarCollapsedStateAsync();
    }

    /// <summary>Save collapse state to localStorage when state changes.</summary>
    protected override void OnParametersSet()
    {
        base.OnParametersSet();
        SaveSidebarCollapsedState();
    }

    /// <summary>Save collapse state to localStorage when user toggles the sidebar.</summary>
    protected override async Task OnAfterRenderAsync(bool render)
    {
        await base.OnAfterRenderAsync(render);
        if (render)
        {
            SaveSidebarCollapsedState();
        }
    }

    /// <summary>Load the sidebar collapsed state from localStorage.</summary>
    private async Task LoadSidebarCollapsedStateAsync()
    {
        try
        {
            var collapsed = await Js.InvokeAsync<string>("localStorage.getItem", "dotnetcloud.sidebar:files");
            if (bool.TryParse(collapsed ?? "false", out var parsed))
            {
                _sidebarCollapsed = parsed;
            }
        }
        catch
        {
            // localStorage unavailable - use default (expanded)
        }
    }

    /// <summary>Save the sidebar collapsed state to localStorage.</summary>
    private void SaveSidebarCollapsedState()
    {
        try
        {
            Js.InvokeVoidAsync("localStorage.setItem", "dotnetcloud.sidebar:files", _sidebarCollapsed.ToString().ToLower());
        }
        catch
        {
            // localStorage unavailable - state is still tracked in memory
        }
    }

    /// <summary>Navigates to the specified sidebar section.</summary>
    protected async Task Navigate(FileSidebarSection section)
    {
        if (section == FileSidebarSection.Tags)
        {
            ToggleTagsExpanded();
            return;
        }

        await OnSectionChanged.InvokeAsync(section);
    }

    /// <summary>Navigates to a specific tag filter.</summary>
    protected async Task NavigateToTag(FileTagViewModel tag)
    {
        await OnSectionChanged.InvokeAsync(FileSidebarSection.Tags);
        await OnTagSelected.InvokeAsync(tag);
    }

    /// <summary>Returns the CSS active class when the given section matches <see cref="ActiveSection"/>.</summary>
    protected string IsActive(FileSidebarSection section) =>
        ActiveSection == section ? "active" : string.Empty;

    /// <summary>Formats a byte count as a human-readable string.</summary>
    protected static string FormatBytes(long bytes)
    {
        if (bytes >= 1024L * 1024 * 1024) return $"{bytes / (1024.0 * 1024.0 * 1024.0):F2} GB";
        if (bytes >= 1024L * 1024) return $"{bytes / (1024.0 * 1024.0):F1} MB";
        if (bytes >= 1024L) return $"{bytes / 1024.0:F1} KB";
        return $"{bytes} B";
    }

    /// <summary>CSS class for the quota bar fill based on usage percentage.</summary>
    protected static string QuotaBarClass(QuotaViewModel q)
    {
        if (q.UsagePercent >= 95) return "quota-critical";
        if (q.UsagePercent >= 80) return "quota-warning";
        return "";
    }
}
