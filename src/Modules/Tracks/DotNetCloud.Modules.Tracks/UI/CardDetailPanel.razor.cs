using System.Text.Json;
using DotNetCloud.Core.DTOs;
using DotNetCloud.Modules.Tracks.Services;
using DotNetCloud.UI.Shared.Services;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.AspNetCore.Components.Web;

namespace DotNetCloud.Modules.Tracks.UI;

/// <summary>
/// Slide-out panel showing full card details with editing capabilities.
/// </summary>
public partial class CardDetailPanel : ComponentBase, IDisposable
{
    [Inject] private ITracksApiClient ApiClient { get; set; } = default!;
    [Inject] private BrowserTimeProvider TimeProvider { get; set; } = default!;

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
    private bool _showCommentComposer;
    private string _swimlaneName = "";

    // Pickers
    private bool _showAssignInput;
    private string _assignUserId = "";
    private bool _showLabelPicker;
    private bool _showCreateLabel;
    private string _newLabelTitle = "";
    private string _newLabelColor = "#3b82f6";
    private bool _showAttachmentInput;
    private string _attachFileName = "";
    private string _attachUrl = "";
    private bool _isUploadingFile;
    private Guid? _addingItemToChecklist;
    private string _newChecklistItemTitle = "";

    // Dependency picker
    private bool _showDependencyPicker;
    private string _depType = "BlockedBy";
    private string _depTargetCardId = "";
    private readonly List<CardDto> _boardCards = [];

    // Confirmation modal
    private string? _confirmAction;
    private bool _showConfirmModal;

    // Sprint state
    private readonly List<SprintDto> _boardSprints = [];
    private string? _selectedSprintId;

    // Label color presets
    private static readonly string[] _labelColors =
    [
        "#3b82f6", "#22c55e", "#eab308", "#f97316", "#ef4444",
        "#a855f7", "#ec4899", "#06b6d4", "#64748b", "#84cc16"
    ];

    // Fibonacci story points
    private static readonly int[] _fibonacciValues = [0, 1, 2, 3, 5, 8, 13, 21, 34, 55, 89];

    // Live time tracking
    private int _liveTrackedMinutes;
    private Timer? _liveTimer;

    /// <inheritdoc />
    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (firstRender)
        {
            await TimeProvider.EnsureInitializedAsync();
            StateHasChanged();
        }
    }

    protected override async Task OnParametersSetAsync()
    {
        _selectedPriority = Card.Priority.ToString();
        _dueDate = Card.DueDate?.ToString("yyyy-MM-dd") ?? "";
        _storyPoints = Card.StoryPoints?.ToString() ?? "";
        _selectedSprintId = Card.SprintId?.ToString() ?? "";

        // Resolve swimlane name
        var swimlane = Board.Swimlanes.FirstOrDefault(l => l.Id == Card.SwimlaneId);
        _swimlaneName = swimlane?.Title ?? "Unknown";

        // Load sub-resources in parallel
        var commentsTask = ApiClient.ListCommentsAsync(Card.Id);
        var checklistsTask = ApiClient.ListChecklistsAsync(Card.Id);
        var attachmentsTask = ApiClient.ListAttachmentsAsync(Card.Id);
        var depsTask = ApiClient.ListDependenciesAsync(Card.Id);
        var timeTask = ApiClient.ListTimeEntriesAsync(Card.Id);
        var activityTask = ApiClient.GetCardActivityAsync(Card.Id);
        var sprintsTask = ApiClient.ListSprintsAsync(Board.Id);

        await Task.WhenAll(commentsTask, checklistsTask, attachmentsTask, depsTask, timeTask, activityTask, sprintsTask);

        _comments.Clear(); _comments.AddRange(await commentsTask);
        _checklists.Clear(); _checklists.AddRange(await checklistsTask);
        _attachments.Clear(); _attachments.AddRange(await attachmentsTask);
        _dependencies.Clear(); _dependencies.AddRange(await depsTask);
        _timeEntries.Clear(); _timeEntries.AddRange(await timeTask);
        _activities.Clear(); _activities.AddRange(await activityTask);
        _boardSprints.Clear(); _boardSprints.AddRange(await sprintsTask);

        // Initialize live tracked minutes and start timer if active
        RecalculateLiveMinutes();
        StartLiveTimerIfNeeded();
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
        DateTime? dueDate = DateTime.TryParse(val, out var d)
            ? DateTime.SpecifyKind(d, DateTimeKind.Utc)
            : null;
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

    // ── Sprint Assignment ───────────────────────────────────

    private async Task SaveSprintAsync(ChangeEventArgs e)
    {
        var val = e.Value?.ToString();

        // Remove from current sprint if assigned
        if (Card.SprintId.HasValue)
        {
            await ApiClient.RemoveCardFromSprintAsync(Board.Id, Card.SprintId.Value, Card.Id);
        }

        // Add to new sprint if selected
        if (!string.IsNullOrEmpty(val) && Guid.TryParse(val, out var newSprintId))
        {
            await ApiClient.AddCardToSprintAsync(Board.Id, newSprintId, Card.Id);
            _selectedSprintId = val;
        }
        else
        {
            _selectedSprintId = "";
        }

        await RefreshCardAsync();
    }

    private async Task RemoveFromSprintAsync()
    {
        if (Card.SprintId.HasValue)
        {
            await ApiClient.RemoveCardFromSprintAsync(Board.Id, Card.SprintId.Value, Card.Id);
            _selectedSprintId = "";
            await RefreshCardAsync();
        }
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
        _showCreateLabel = false;
        await RefreshCardAsync();
    }

    private async Task CreateAndApplyLabelAsync()
    {
        if (string.IsNullOrWhiteSpace(_newLabelTitle)) return;
        var label = await ApiClient.CreateLabelAsync(Board.Id, new CreateLabelDto { Title = _newLabelTitle.Trim(), Color = _newLabelColor });
        if (label is not null)
        {
            await ApiClient.AddLabelToCardAsync(Card.Id, label.Id);
        }
        _newLabelTitle = "";
        _newLabelColor = "#3b82f6";
        _showCreateLabel = false;
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
        _showCommentComposer = false;
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

    private async Task HandleFileSelectedAsync(InputFileChangeEventArgs e)
    {
        _isUploadingFile = true;
        StateHasChanged();
        try
        {
            foreach (var file in e.GetMultipleFiles(10))
            {
                var att = await ApiClient.AddAttachmentAsync(Card.Id, file.Name, null, null);
                if (att is not null) _attachments.Add(att);
            }
        }
        finally
        {
            _isUploadingFile = false;
            StateHasChanged();
        }
    }

    private async Task RemoveAttachmentAsync(Guid attachmentId)
    {
        await ApiClient.RemoveAttachmentAsync(Card.Id, attachmentId);
        _attachments.RemoveAll(a => a.Id == attachmentId);
    }

    // ── Dependencies ────────────────────────────────────────

    private async Task OpenDependencyPickerAsync()
    {
        // Load all cards from all swimlanes on this board for the picker
        _boardCards.Clear();
        foreach (var swimlane in Board.Swimlanes)
        {
            var cards = await ApiClient.ListCardsAsync(swimlane.Id);
            _boardCards.AddRange(cards);
        }
        _depTargetCardId = "";
        _depType = "BlockedBy";
        _showDependencyPicker = true;
    }

    private async Task AddDependencyAsync()
    {
        if (!Guid.TryParse(_depTargetCardId, out var targetId)) return;
        if (!Enum.TryParse<CardDependencyType>(_depType, out var depType)) return;

        var dep = await ApiClient.AddDependencyAsync(Card.Id, targetId, depType);
        if (dep is not null) _dependencies.Add(dep);
        _showDependencyPicker = false;
    }

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
        RecalculateLiveMinutes();
        StartLiveTimerIfNeeded();
    }

    private async Task StopTimerAsync()
    {
        var entry = await ApiClient.StopTimerAsync(Card.Id);
        if (entry is not null)
        {
            _timeEntries.Clear();
            _timeEntries.AddRange(await ApiClient.ListTimeEntriesAsync(Card.Id));
        }
        StopLiveTimer();
        RecalculateLiveMinutes();
        await RefreshCardAsync();
    }

    // ── Card Actions ────────────────────────────────────────

    private async Task ConfirmActionAsync()
    {
        switch (_confirmAction)
        {
            case "archive":
            case "unarchive":
                await ArchiveCardAsync();
                break;
            case "delete":
                await DeleteCardAsync();
                break;
        }
        _confirmAction = null;
        _showConfirmModal = false;
    }

    private void ShowArchiveConfirm()
    {
        _confirmAction = Card.IsArchived ? "unarchive" : "archive";
        _showConfirmModal = true;
    }

    private void ShowDeleteConfirm()
    {
        _confirmAction = "delete";
        _showConfirmModal = true;
    }

    private void CancelConfirm()
    {
        _confirmAction = null;
        _showConfirmModal = false;
    }

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

    private static string FormatActivityAction(BoardActivityDto activity)
    {
        var details = !string.IsNullOrEmpty(activity.Details)
            ? TryParseJson(activity.Details)
            : null;

        return activity.Action switch
        {
            "card.created" => $"created card{GetDetail(details, "title", " \"{0}\"")}",
            "card.updated" => FormatCardUpdated(details),
            "card.moved" => FormatCardMoved(details),
            "card.deleted" => $"deleted card{GetDetail(details, "title", " \"{0}\"")}",
            "card.assigned" => $"assigned {GetDetail(details, "displayName", "{0}")} to Card",
            "card.unassigned" => $"unassigned {GetDetail(details, "displayName", "{0}")} from Card",
            "comment.added" => "added a comment",
            "checklist.created" => "added a checklist",
            "attachment.added" => $"attached{GetDetail(details, "fileName", " {0}")}",
            "label.created" => $"created label{GetDetail(details, "title", " \"{0}\"")}",
            "label.deleted" => "deleted a label",
            "time.logged" => "logged time",
            "poker.started" => "started planning poker",
            "poker.completed" => "completed planning poker",
            "sprint.created" => "created a sprint",
            "sprint.started" => "started a sprint",
            "sprint.completed" => "completed a sprint",
            _ => $"{activity.Action} {activity.EntityType}"
        };
    }

    private static Dictionary<string, string>? TryParseJson(string json)
    {
        try
        {
            return JsonSerializer.Deserialize<Dictionary<string, string>>(json);
        }
        catch
        {
            return null;
        }
    }

    private static string FormatCardUpdated(Dictionary<string, string>? details)
    {
        var fields = GetDetail(details, "fields", "{0}");
        return string.IsNullOrEmpty(fields) ? "updated Card" : $"updated {fields}";
    }

    private static string FormatCardMoved(Dictionary<string, string>? details)
    {
        var toTitle = GetDetail(details, "toTitle", "{0}");
        return string.IsNullOrEmpty(toTitle) ? "moved Card to another column" : $"moved Card to {toTitle}";
    }

    private static string GetDetail(Dictionary<string, string>? details, string key, string format)
    {
        if (details is not null && details.TryGetValue(key, out var value) && !string.IsNullOrEmpty(value))
            return string.Format(format, value);
        return "";
    }

    // ── Live Timer ───────────────────────────────────────────

    private void RecalculateLiveMinutes()
    {
        var completed = _timeEntries
            .Where(e => e.EndTime is not null)
            .Sum(e => e.DurationMinutes);

        var activeEntry = _timeEntries.FirstOrDefault(e => e.EndTime is null);
        var activeMins = activeEntry is not null
            ? (int)(DateTime.UtcNow - activeEntry.StartTime).TotalMinutes
            : 0;

        _liveTrackedMinutes = completed + activeMins;
    }

    private void StartLiveTimerIfNeeded()
    {
        StopLiveTimer();
        if (_timeEntries.Any(e => e.EndTime is null))
        {
            _liveTimer = new Timer(OnLiveTimerTick, null, TimeSpan.FromSeconds(15), TimeSpan.FromSeconds(15));
        }
    }

    private void OnLiveTimerTick(object? state)
    {
        _ = InvokeAsync(() =>
        {
            RecalculateLiveMinutes();
            StateHasChanged();
        });
    }

    private void StopLiveTimer()
    {
        _liveTimer?.Dispose();
        _liveTimer = null;
    }

    public void Dispose()
    {
        StopLiveTimer();
    }
}
