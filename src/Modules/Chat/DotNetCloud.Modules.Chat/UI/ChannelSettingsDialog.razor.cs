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
    private string _newMemberIdInput = string.Empty;

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

    /// <summary>Channel members shown in settings management.</summary>
    [Parameter]
    public List<MemberViewModel> Members { get; set; } = [];

    /// <summary>Whether the current user can manage channel settings (Admin or Owner).</summary>
    [Parameter]
    public bool CanManageChannel { get; set; }

    /// <summary>Callback to add a member by user id.</summary>
    [Parameter]
    public EventCallback<Guid> OnAddMember { get; set; }

    /// <summary>Callback to remove a member by user id.</summary>
    [Parameter]
    public EventCallback<Guid> OnRemoveMember { get; set; }

    /// <summary>Callback to change a member role.</summary>
    [Parameter]
    public EventCallback<(Guid UserId, string Role)> OnChangeMemberRole { get; set; }

    /// <summary>Channel creation timestamp.</summary>
    [Parameter]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>Display name of channel creator.</summary>
    [Parameter]
    public string CreatedByDisplayName { get; set; } = "Unknown";

    /// <summary>Editable channel name.</summary>
    protected string EditName { get => _editName; set => _editName = value; }

    /// <summary>Editable channel topic.</summary>
    protected string? EditTopic { get => _editTopic; set => _editTopic = value; }

    /// <summary>Editable channel description.</summary>
    protected string? EditDescription { get => _editDescription; set => _editDescription = value; }

    /// <summary>Notification preference selection.</summary>
    protected string NotificationPref { get => _notificationPref; set => _notificationPref = value; }

    /// <summary>Input value for adding a new member by ID.</summary>
    protected string NewMemberIdInput { get => _newMemberIdInput; set => _newMemberIdInput = value; }

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

    /// <summary>Adds a member from typed user id.</summary>
    protected async Task AddMember()
    {
        if (!Guid.TryParse(_newMemberIdInput, out var userId))
        {
            return;
        }

        await OnAddMember.InvokeAsync(userId);
        _newMemberIdInput = string.Empty;
    }

    /// <summary>Removes member from channel.</summary>
    protected async Task RemoveMember(Guid userId)
    {
        await OnRemoveMember.InvokeAsync(userId);
    }

    /// <summary>Changes member role in channel.</summary>
    protected async Task ChangeMemberRole(Guid userId, string? role)
    {
        if (string.IsNullOrWhiteSpace(role))
        {
            return;
        }

        await OnChangeMemberRole.InvokeAsync((userId, role));
    }
}
