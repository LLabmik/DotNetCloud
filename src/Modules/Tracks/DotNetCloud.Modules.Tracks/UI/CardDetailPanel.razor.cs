using DotNetCloud.Core.DTOs;
using DotNetCloud.Modules.Tracks.Services;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;

namespace DotNetCloud.Modules.Tracks.UI;

/// <summary>
/// Slide-out panel showing full card details with editing capabilities.
/// </summary>
public partial class CardDetailPanel : ComponentBase
{
    [Inject] private ITracksApiClient ApiClient { get; set; } = default!;

    [Parameter, EditorRequired] public CardDto Card { get; set; } = default!;
    [Parameter, EditorRequired] public BoardDto Board { get; set; } = default!;
    [Parameter] public EventCallback OnClose { get; set; }
    [Parameter] public EventCallback<CardDto> OnCardUpdated { get; set; }
    [Parameter] public EventCallback<Guid> OnCardDeleted { get; set; }

    // Sub-resource state
    private readonly List<CardCommentDto> _comments = [];
    private readonly List<CardChecklistDto> _checklists = [];
    private readonly List<CardAttachmentDto> _attachments = [];
    private readonly List<CardDependencyDto> _dependencies = [];
    private readonly List<TimeEntryDto> _timeEntries = [];
    private readonly List<BoardActivityDto> _activities = [];

    // Edit state
    private bool _isEditingTitle;
    private string _editTitle = "";
    private bool _isEditingDescription;
    private string _editDescription = "";
    private string _selectedPriority = "None";
    private string _dueDate = "";
    private string _storyPoints = "";
    private string _newCommentContent = "";
    private string _listName = "";

    // Pickers
    private bool _showAssignInput;
    private string _assignUserId = "";
    private bool _showLabelPicker;
    private bool _showAttachmentInput;
    private string _attachFileName = "";
    private string _attachUrl = "";
    private Guid? _addingItemToChecklist;
    private string _newChecklistItemTitle = "";

    protected override async Task OnParametersSetAsync()
    {
        _selectedPriority = Card.Priority.ToString();
        _dueDate = Card.DueDate?.ToString("yyyy-MM-dd") ?? "";
        _storyPoints = Card.StoryPoints?.ToString() ?? "";

        // Resolve list name
        var list = Board.Lists.FirstOrDefault(l => l.Id == Card.ListId);
        _listName = list?.Title ?? "Unknown";

        // Load sub-resources in parallel
        var commentsTask = ApiClient.ListCommentsAsync(Card.Id);
        var checklistsTask = ApiClient.ListChecklistsAsync(Card.Id);
        var attachmentsTask = ApiClient.ListAttachmentsAsync(Card.Id);
        var depsTask = ApiClient.ListDependenciesAsync(Card.Id);
        var timeTask = ApiClient.ListTimeEntriesAsync(Card.Id);
        var activityTask = ApiClient.GetCardActivityAsync(Card.Id);

        await Task.WhenAll(commentsTask, checklistsTask, attachmentsTask, depsTask, timeTask, activityTask);

        _comments.Clear(); _comments.AddRange(await commentsTask);
        _checklists.Clear(); _checklists.AddRange(await checklistsTask);
        _attachments.Clear(); _attachments.AddRange(await attachmentsTask);
        _dependencies.Clear(); _dependencies.AddRange(await depsTask);
        _timeEntries.Clear(); _timeEntries.AddRange(await timeTask);
        _activities.Clear(); _activities.AddRange(await activityTask);
    }

    // ── Title ───────────────────────────────────────────────

    private void BeginEditTitle()
    {
        _editTitle = Card.Title;
        _isEditingTitle = true;
    }

    private async Task HandleTitleKeyDown(KeyboardEventArgs e)
    {
        if (e.Key == "Enter") await SaveTitleAsync();
        else if (e.Key == "Escape") _isEditingTitle = false;
    }

    private async Task SaveTitleAsync()
    {
        _isEditingTitle = false;
        if (string.IsNullOrWhiteSpace(_editTitle) || _editTitle.Trim() == Card.Title) return;

        var updated = await ApiClient.UpdateCardAsync(Card.Id, new UpdateCardDto { Title = _editTitle.Trim() });
        if (updated is not null) await OnCardUpdated.InvokeAsync(updated);
    }

    // ── Description ─────────────────────────────────────────

    private void BeginEditDescription()
    {
        _editDescription = Card.Description ?? "";
        _isEditingDescription = true;
    }

    private void CancelEditDescription() => _isEditingDescription = false;

    private async Task SaveDescriptionAsync()
    {
        _isEditingDescription = false;
        var desc = string.IsNullOrWhiteSpace(_editDescription) ? null : _editDescription.Trim();
        var updated = await ApiClient.UpdateCardAsync(Card.Id, new UpdateCardDto { Description = desc });
        if (updated is not null) await OnCardUpdated.InvokeAsync(updated);
    }

    // ── Priority / Due Date / Story Points ──────────────────

    private async Task SavePriorityAsync()
    {
        if (Enum.TryParse<CardPriority>(_selectedPriority, out var p))
        {
            var updated = await ApiClient.UpdateCardAsync(Card.Id, new UpdateCardDto { Priority = p });
            if (updated is not null) await OnCardUpdated.InvokeAsync(updated);
        }
    }

    private async Task SaveDueDateAsync(ChangeEventArgs e)
    {
        var val = e.Value?.ToString();
        DateTime? dueDate = DateTime.TryParse(val, out var d) ? d : null;
        var updated = await ApiClient.UpdateCardAsync(Card.Id, new UpdateCardDto { DueDate = dueDate });
        if (updated is not null) await OnCardUpdated.InvokeAsync(updated);
    }

    private async Task SaveStoryPointsAsync(ChangeEventArgs e)
    {
        var val = e.Value?.ToString();
        int? sp = int.TryParse(val, out var n) ? n : null;
        var updated = await ApiClient.UpdateCardAsync(Card.Id, new UpdateCardDto { StoryPoints = sp });
        if (updated is not null) await OnCardUpdated.InvokeAsync(updated);
    }

    // ── Assignments ─────────────────────────────────────────

    private async Task AssignUserAsync()
    {
        if (!Guid.TryParse(_assignUserId, out var userId)) return;
        await ApiClient.AssignUserAsync(Card.Id, userId);
        _showAssignInput = false;
        _assignUserId = "";
        await RefreshCardAsync();
    }

    private async Task UnassignUserAsync(Guid userId)
    {
        await ApiClient.UnassignUserAsync(Card.Id, userId);
        await RefreshCardAsync();
    }

    // ── Labels ──────────────────────────────────────────────

    private async Task AddLabelAsync(Guid labelId)
    {
        await ApiClient.AddLabelToCardAsync(Card.Id, labelId);
        _showLabelPicker = false;
        await RefreshCardAsync();
    }

    private async Task RemoveLabelAsync(Guid labelId)
    {
        await ApiClient.RemoveLabelFromCardAsync(Card.Id, labelId);
        await RefreshCardAsync();
    }

    // ── Comments ────────────────────────────────────────────

    private async Task AddCommentAsync()
    {
        if (string.IsNullOrWhiteSpace(_newCommentContent)) return;
        var comment = await ApiClient.CreateCommentAsync(Card.Id, _newCommentContent.Trim());
        if (comment is not null) _comments.Insert(0, comment);
        _newCommentContent = "";
    }

    private async Task DeleteCommentAsync(Guid commentId)
    {
        await ApiClient.DeleteCommentAsync(Card.Id, commentId);
        _comments.RemoveAll(c => c.Id == commentId);
    }

    // ── Checklists ──────────────────────────────────────────

    private async Task AddChecklistAsync()
    {
        var checklist = await ApiClient.CreateChecklistAsync(Card.Id, "Checklist");
        if (checklist is not null) _checklists.Add(checklist);
    }

    private async Task DeleteChecklistAsync(Guid checklistId)
    {
        await ApiClient.DeleteChecklistAsync(Card.Id, checklistId);
        _checklists.RemoveAll(c => c.Id == checklistId);
    }

    private async Task AddChecklistItemAsync(Guid checklistId)
    {
        if (string.IsNullOrWhiteSpace(_newChecklistItemTitle)) return;
        await ApiClient.AddChecklistItemAsync(Card.Id, checklistId, _newChecklistItemTitle.Trim());
        _addingItemToChecklist = null;
        _newChecklistItemTitle = "";
        // Refresh checklists
        _checklists.Clear();
        _checklists.AddRange(await ApiClient.ListChecklistsAsync(Card.Id));
    }

    private async Task ToggleChecklistItemAsync(Guid checklistId, Guid itemId)
    {
        await ApiClient.ToggleChecklistItemAsync(Card.Id, checklistId, itemId);
        _checklists.Clear();
        _checklists.AddRange(await ApiClient.ListChecklistsAsync(Card.Id));
    }

    private async Task HandleChecklistItemKeyDown(KeyboardEventArgs e)
    {
        if (e.Key == "Enter" && _addingItemToChecklist.HasValue)
            await AddChecklistItemAsync(_addingItemToChecklist.Value);
        else if (e.Key == "Escape")
            _addingItemToChecklist = null;
    }

    // ── Attachments ─────────────────────────────────────────

    private async Task AddAttachmentAsync()
    {
        if (string.IsNullOrWhiteSpace(_attachFileName)) return;
        var url = string.IsNullOrWhiteSpace(_attachUrl) ? null : _attachUrl.Trim();
        var att = await ApiClient.AddAttachmentAsync(Card.Id, _attachFileName.Trim(), url, null);
        if (att is not null) _attachments.Add(att);
        _showAttachmentInput = false;
        _attachFileName = "";
        _attachUrl = "";
    }

    private async Task RemoveAttachmentAsync(Guid attachmentId)
    {
        await ApiClient.RemoveAttachmentAsync(Card.Id, attachmentId);
        _attachments.RemoveAll(a => a.Id == attachmentId);
    }

    // ── Dependencies ────────────────────────────────────────

    private async Task RemoveDependencyAsync(Guid dependsOnCardId)
    {
        await ApiClient.RemoveDependencyAsync(Card.Id, dependsOnCardId);
        _dependencies.RemoveAll(d => d.DependsOnCardId == dependsOnCardId);
    }

    // ── Time Tracking ───────────────────────────────────────

    private async Task StartTimerAsync()
    {
        var entry = await ApiClient.StartTimerAsync(Card.Id);
        if (entry is not null) _timeEntries.Add(entry);
    }

    private async Task StopTimerAsync()
    {
        var entry = await ApiClient.StopTimerAsync(Card.Id);
        if (entry is not null)
        {
            _timeEntries.Clear();
            _timeEntries.AddRange(await ApiClient.ListTimeEntriesAsync(Card.Id));
        }
        await RefreshCardAsync();
    }

    // ── Card Actions ────────────────────────────────────────

    private async Task ArchiveCardAsync()
    {
        var updated = await ApiClient.UpdateCardAsync(Card.Id, new UpdateCardDto { IsArchived = !Card.IsArchived });
        if (updated is not null) await OnCardUpdated.InvokeAsync(updated);
    }

    private async Task DeleteCardAsync()
    {
        await ApiClient.DeleteCardAsync(Card.Id);
        await OnCardDeleted.InvokeAsync(Card.Id);
    }

    // ── Helpers ─────────────────────────────────────────────

    private async Task RefreshCardAsync()
    {
        var refreshed = await ApiClient.GetCardAsync(Card.Id);
        if (refreshed is not null) await OnCardUpdated.InvokeAsync(refreshed);
    }

    private static string FormatMinutes(int totalMinutes)
    {
        if (totalMinutes == 0) return "0m";
        var hours = totalMinutes / 60;
        var minutes = totalMinutes % 60;
        return hours > 0 ? $"{hours}h {minutes}m" : $"{minutes}m";
    }

    private static string GetInitials(string? name)
    {
        if (string.IsNullOrWhiteSpace(name)) return "?";
        var parts = name.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        return parts.Length >= 2
            ? $"{parts[0][0]}{parts[1][0]}".ToUpperInvariant()
            : name[..1].ToUpperInvariant();
    }
}
