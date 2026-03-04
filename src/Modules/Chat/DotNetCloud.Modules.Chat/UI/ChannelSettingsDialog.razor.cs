using Microsoft.AspNetCore.Components;

namespace DotNetCloud.Modules.Chat.UI;

/// <summary>
/// Code-behind for the channel settings dialog.
/// Allows editing channel metadata, managing notifications, archiving, and deleting.
/// </summary>
public partial class ChannelSettingsDialog : ComponentBase
{
    private string _editName = string.Empty;
    private string? _editTopic;
    private string? _editDescription;
    private string _notificationPref = "All";

    /// <summary>Whether the dialog is visible.</summary>
    [Parameter]
    public bool IsVisible { get; set; }

    /// <summary>The channel being edited.</summary>
    [Parameter]
    public ChannelViewModel? Channel { get; set; }

    /// <summary>Callback when settings are saved.</summary>
    [Parameter]
    public EventCallback<(string Name, string? Topic, string? Description)> OnSave { get; set; }

    /// <summary>Callback when notification preference changes.</summary>
    [Parameter]
    public EventCallback<string> OnNotificationPrefChanged { get; set; }

    /// <summary>Callback when the channel should be archived.</summary>
    [Parameter]
    public EventCallback OnArchive { get; set; }

    /// <summary>Callback when the channel should be deleted.</summary>
    [Parameter]
    public EventCallback OnDelete { get; set; }

    /// <summary>Callback to close the dialog.</summary>
    [Parameter]
    public EventCallback OnClose { get; set; }

    /// <summary>Editable channel name.</summary>
    protected string EditName { get => _editName; set => _editName = value; }

    /// <summary>Editable channel topic.</summary>
    protected string? EditTopic { get => _editTopic; set => _editTopic = value; }

    /// <summary>Editable channel description.</summary>
    protected string? EditDescription { get => _editDescription; set => _editDescription = value; }

    /// <summary>Notification preference selection.</summary>
    protected string NotificationPref { get => _notificationPref; set => _notificationPref = value; }

    /// <inheritdoc />
    protected override void OnParametersSet()
    {
        if (Channel is not null)
        {
            _editName = Channel.Name;
            _editTopic = Channel.Topic;
        }
    }

    /// <summary>Saves the channel settings.</summary>
    protected async Task SaveChanges()
    {
        await OnSave.InvokeAsync((_editName, _editTopic, _editDescription));
        await OnNotificationPrefChanged.InvokeAsync(_notificationPref);
        await Close();
    }

    /// <summary>Archives the channel.</summary>
    protected async Task ArchiveChannel()
    {
        await OnArchive.InvokeAsync();
        await Close();
    }

    /// <summary>Deletes the channel.</summary>
    protected async Task DeleteChannel()
    {
        await OnDelete.InvokeAsync();
        await Close();
    }

    /// <summary>Closes the dialog.</summary>
    protected async Task Close()
    {
        await OnClose.InvokeAsync();
    }
}
