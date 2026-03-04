using Microsoft.AspNetCore.Components;

namespace DotNetCloud.Modules.Files.UI;

/// <summary>
/// Code-behind for the file browser sidebar navigation component.
/// </summary>
public partial class FileSidebar : ComponentBase
{
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

    /// <summary>Available file tags to display in the expandable tag section.</summary>
    [Parameter] public IReadOnlyList<FileTagViewModel> Tags { get; set; } = [];

    private bool _tagsExpanded;

    /// <summary>Whether the Tags sub-list is expanded.</summary>
    protected bool IsTagsExpanded => _tagsExpanded;

    /// <summary>Toggles the Tags sub-list open or closed.</summary>
    protected void ToggleTagsExpanded() => _tagsExpanded = !_tagsExpanded;

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
        ActiveSection == section ? "sidebar-nav-item--active" : string.Empty;
}
