using Microsoft.AspNetCore.Components;

namespace DotNetCloud.Modules.Chat.UI;

/// <summary>
/// Code-behind for the announcement editor dialog.
/// Supports creating and editing announcements.
/// </summary>
public partial class AnnouncementEditor : ComponentBase
{

    private string _title = string.Empty;
    private string _content = string.Empty;
    private string _priority = "Normal";

    private DateTime? _expiresAt;
    private bool _requiresAcknowledgement;

    /// <summary>Whether the dialog is visible.</summary>
    [Parameter]
    public bool IsVisible { get; set; }

    /// <summary>Whether we are editing an existing announcement.</summary>
    [Parameter]
    public bool IsEditing { get; set; }

    /// <summary>The announcement being edited (null for new).</summary>
    [Parameter]
    public AnnouncementViewModel? EditingAnnouncement { get; set; }

    /// <summary>Callback when the announcement is saved.</summary>
    [Parameter]
    public EventCallback<AnnouncementEditorResult> OnSave { get; set; }

    /// <summary>Callback to close the editor.</summary>
    [Parameter]
    public EventCallback OnCancel { get; set; }

    /// <summary>Editable title.</summary>
    protected string Title { get => _title; set => _title = value; }

    /// <summary>Editable content.</summary>
    protected string Content { get => _content; set => _content = value; }

    /// <summary>Selected priority.</summary>
    protected string Priority { get => _priority; set => _priority = value; }

    /// <summary>Optional expiry date.</summary>
    protected DateTime? ExpiresAt { get => _expiresAt; set => _expiresAt = value; }

    /// <summary>Whether acknowledgement is required.</summary>
    protected bool RequiresAcknowledgement { get => _requiresAcknowledgement; set => _requiresAcknowledgement = value; }

    /// <summary>Whether the save button should be disabled.</summary>
    protected bool IsSaveDisabled => string.IsNullOrWhiteSpace(_title) || string.IsNullOrWhiteSpace(_content);

    /// <inheritdoc />
    protected override void OnParametersSet()
    {
        if (EditingAnnouncement is not null && IsEditing)
        {
            _title = EditingAnnouncement.Title;
            _content = EditingAnnouncement.Content;
            _priority = EditingAnnouncement.Priority;
            _expiresAt = EditingAnnouncement.ExpiresAt;
            _requiresAcknowledgement = EditingAnnouncement.RequiresAcknowledgement;
        }
        else if (!IsEditing)
        {
            _title = string.Empty;
            _content = string.Empty;
            _priority = "Normal";
            _expiresAt = null;
            _requiresAcknowledgement = false;
        }
    }

    /// <summary>Saves the announcement.</summary>
    protected async Task Save()
    {
        if (IsSaveDisabled) return;

        await OnSave.InvokeAsync(new AnnouncementEditorResult
        {
            Title = _title,
            Content = _content,
            Priority = _priority,
            ExpiresAt = _expiresAt,
            RequiresAcknowledgement = _requiresAcknowledgement
        });
    }

    /// <summary>Cancels and closes the editor.</summary>
    protected async Task Cancel()
    {
        await OnCancel.InvokeAsync();
    }
}

/// <summary>
/// Result from the announcement editor dialog.
/// </summary>
public sealed class AnnouncementEditorResult
{
    /// <summary>Title.</summary>
    public string Title { get; init; } = string.Empty;

    /// <summary>Content.</summary>
    public string Content { get; init; } = string.Empty;

    /// <summary>Priority.</summary>
    public string Priority { get; init; } = "Normal";

    /// <summary>Optional expiry date.</summary>
    public DateTime? ExpiresAt { get; init; }

    /// <summary>Whether acknowledgement is required.</summary>
    public bool RequiresAcknowledgement { get; init; }
}
