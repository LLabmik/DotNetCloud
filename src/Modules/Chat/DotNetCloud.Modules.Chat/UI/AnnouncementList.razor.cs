using Microsoft.AspNetCore.Components;

namespace DotNetCloud.Modules.Chat.UI;

/// <summary>
/// Code-behind for the announcement list component.
/// </summary>
public partial class AnnouncementList : ComponentBase
{
    private string _selectedPriority = "All";
    private DateTime? _fromDate;
    private DateTime? _toDate;

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

    /// <summary>Announcements after applying priority and date filters.</summary>
    protected IEnumerable<AnnouncementViewModel> FilteredAnnouncements =>
        Announcements
            .Where(a => _selectedPriority == "All" || a.Priority == _selectedPriority)
            .Where(a => _fromDate == null || a.PublishedAt.Date >= _fromDate.Value.Date)
            .Where(a => _toDate == null || a.PublishedAt.Date <= _toDate.Value.Date);

    /// <summary>Handles announcement selection.</summary>
    protected async Task SelectAnnouncement(Guid id)
    {
        await OnSelect.InvokeAsync(id);
    }

    /// <summary>Clears all active filters.</summary>
    protected void ClearFilters()
    {
        _selectedPriority = "All";
        _fromDate = null;
        _toDate = null;
    }

    private void SetFromDate(ChangeEventArgs e)
    {
        _fromDate = DateTime.TryParse(e.Value?.ToString(), out var d) ? d : null;
    }

    private void SetToDate(ChangeEventArgs e)
    {
        _toDate = DateTime.TryParse(e.Value?.ToString(), out var d) ? d : null;
    }
}
