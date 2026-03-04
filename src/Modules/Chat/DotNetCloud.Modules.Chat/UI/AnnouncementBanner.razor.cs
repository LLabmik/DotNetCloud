using Microsoft.AspNetCore.Components;

namespace DotNetCloud.Modules.Chat.UI;

/// <summary>
/// Code-behind for the announcement banner.
/// Displays urgent/important announcements with acknowledgement support.
/// </summary>
public partial class AnnouncementBanner : ComponentBase
{
    /// <summary>The announcement to display.</summary>
    [Parameter]
    public AnnouncementViewModel? Announcement { get; set; }

    /// <summary>Whether the current user has acknowledged this announcement.</summary>
    [Parameter]
    public bool IsAcknowledged { get; set; }

    /// <summary>Callback when the user acknowledges the announcement.</summary>
    [Parameter]
    public EventCallback<Guid> OnAcknowledge { get; set; }

    /// <summary>Callback when the user dismisses the banner.</summary>
    [Parameter]
    public EventCallback OnDismiss { get; set; }

    /// <summary>Acknowledges the announcement.</summary>
    protected async Task Acknowledge()
    {
        if (Announcement is not null)
        {
            await OnAcknowledge.InvokeAsync(Announcement.Id);
        }
    }

    /// <summary>Dismisses the banner.</summary>
    protected async Task Dismiss()
    {
        await OnDismiss.InvokeAsync();
    }

    /// <summary>Truncates content for inline display.</summary>
    protected static string TruncateContent(string content)
    {
        const int maxLength = 120;
        return content.Length <= maxLength ? content : string.Concat(content.AsSpan(0, maxLength), "…");
    }
}
