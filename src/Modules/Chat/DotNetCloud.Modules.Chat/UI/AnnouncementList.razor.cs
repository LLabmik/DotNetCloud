using Microsoft.AspNetCore.Components;

namespace DotNetCloud.Modules.Chat.UI;

/// <summary>
/// Code-behind for the announcement list component.
/// </summary>
public partial class AnnouncementList : ComponentBase
{
    /// <summary>The announcements to display.</summary>
    [Parameter]
    public List<AnnouncementViewModel> Announcements { get; set; } = [];

    /// <summary>Whether the user can create new announcements.</summary>
    [Parameter]
    public bool CanCreate { get; set; }

    /// <summary>Callback to create a new announcement.</summary>
    [Parameter]
    public EventCallback OnCreate { get; set; }

    /// <summary>Callback when an announcement is selected.</summary>
    [Parameter]
    public EventCallback<Guid> OnSelect { get; set; }

    /// <summary>Handles announcement selection.</summary>
    protected async Task SelectAnnouncement(Guid id)
    {
        await OnSelect.InvokeAsync(id);
    }
}
