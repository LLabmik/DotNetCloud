using System.Text.Json;
using System.Text.RegularExpressions;
using DotNetCloud.Core.Capabilities;
using DotNetCloud.Core.DTOs;
using DotNetCloud.Modules.Tracks.Services;
using DotNetCloud.UI.Shared.Services;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.AspNetCore.Components.Web;

namespace DotNetCloud.Modules.Tracks.UI;

/// <summary>
/// Slide-out detail panel for a work item showing type-specific sections:
/// Epics get sprints/poker/review; Features get child items;
/// Items get checklists/time tracking/sprint assignment.
/// </summary>
public partial class WorkItemDetailPanel : ComponentBase, IDisposable
{
    [Inject] private ITracksApiClient ApiClient { get; set; } = default!;
    [Inject] private BrowserTimeProvider TimeProvider { get; set; } = default!;

    [Parameter, EditorRequired] public WorkItemDto WorkItem { get; set; } = default!;
    [Parameter, EditorRequired] public ProductDto Product { get; set; } = default!;
    [Parameter] public List<ProductMemberDto>? Members { get; set; }
    [Parameter] public List<LabelDto>? ProductLabels { get; set; }
    [Parameter] public string? SwimlaneTitle { get; set; }
    [Parameter] public List<SprintDto>? Sprints { get; set; }
    [Parameter] public EventCallback OnClose { get; set; }
    [Parameter] public EventCallback<WorkItemDto> OnWorkItemUpdated { get; set; }
    [Parameter] public EventCallback<Guid> OnWorkItemDeleted { get; set; }
    [Parameter] public EventCallback<Guid> OnOpenKanban { get; set; }

    // Sub-resource state
    private readonly List<WorkItemCommentDto> _comments = [];
    private readonly List<ChecklistDto> _checklists = [];
    private readonly List<WorkItemAttachmentDto> _attachments = [];
    private readonly List<WorkItemDependencyDto> _dependencies = [];
    private readonly List<TimeEntryDto> _timeEntries = [];
    private readonly List<ActivityDto> _activities = [];
    private readonly List<WorkItemDto> _childWorkItems = [];
    private readonly List<SprintDto> _epicSprints = [];

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

    // Mention typeahead
    private MentionTypeahead? _mentionTypeahead;
    private ElementReference _commentTextarea;
    private int _mentionStartIndex = -1;
    private string _mentionSearchTerm = "";
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
    private string _depTargetItemId = "";
    private readonly List<WorkItemDto> _swimlaneItems = [];

    // Confirmation modal
    private string? _confirmAction;
    private bool _showConfirmModal;

    // Sprint state
    private string? _selectedSprintId;

    // Label color presets
    private static readonly string[] _labelColors =
    [
        "#3b82f6", "#22c55e", "#eab308", "#f97316", "#ef4444",
        "#a855f7", "#ec4899", "#06b6d4", "#64748b", "#84cc16"
    ];

    private static readonly int[] _fibonacciValues = [0, 1, 2, 3, 5, 8, 13, 21, 34, 55, 89];

    // Live time tracking
    private int _liveTrackedMinutes;
    private Timer? _liveTimer;

    // Watcher state
    private bool _isWatching;
    private bool _isTogglingWatch;
    private int _watcherCount;

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
        _selectedPriority = WorkItem.Priority.ToString();
        _dueDate = WorkItem.DueDate?.ToString("yyyy-MM-dd") ?? "";
        _storyPoints = WorkItem.StoryPoints?.ToString() ?? "";
        _selectedSprintId = WorkItem.SprintId?.ToString() ?? "";
        _swimlaneName = SwimlaneTitle ?? "Unknown";

        var tasks = new List<Task>
        {
            ApiClient.ListCommentsAsync(WorkItem.Id),
            ApiClient.ListAttachmentsAsync(WorkItem.Id),
            ApiClient.ListDependenciesAsync(WorkItem.Id),
            ApiClient.ListTimeEntriesAsync(WorkItem.Id),
            ApiClient.GetWorkItemActivityAsync(WorkItem.Id),
            LoadWatcherStateAsync() // Check if current user is watching
        };

        // Checklists only for Items without SubItems
        if (WorkItem.Type == WorkItemType.Item && !Product.SubItemsEnabled)
            tasks.Add(LoadChecklistsAsync());

        // Child work items
        if (WorkItem.Type is WorkItemType.Epic or WorkItemType.Feature or WorkItemType.Item && Product.SubItemsEnabled)
            tasks.Add(LoadChildWorkItemsAsync());

        // Sprints for Epic-level
        if (WorkItem.Type == WorkItemType.Epic)
            tasks.Add(LoadEpicSprintsAsync());

        // Sprints for Item-level assignment
        if (WorkItem.Type == WorkItemType.Item && Sprints is null && WorkItem.ParentWorkItemId is not null)
            tasks.Add(LoadItemSprintsAsync());

        await Task.WhenAll(tasks);

        // Extract results (they're awaited below for ordering but actual loading happened in parallel)
        RecalculateLiveMinutes();
        StartLiveTimerIfNeeded();
    }

    private async Task LoadChecklistsAsync()
    {
        _checklists.Clear();
        _checklists.AddRange(await ApiClient.ListChecklistsAsync(WorkItem.Id));
    }

    /// <summary>
    /// Checks if the current user is watching this work item and loads the watcher count.
    /// Called when the detail panel opens.
    /// </summary>
    private async Task LoadWatcherStateAsync()
    {
        try
        {
            var watchers = await ApiClient.GetWatchersAsync(WorkItem.Id);
            _watcherCount = watchers.Count;
            // We don't have the current user ID here, so we rely on the watch/unwatch
            // endpoint to determine state. We'll assume not watching until toggled.
            _isWatching = false;
        }
        catch
        {
            // Watcher check failed — user can still manually toggle
        }
    }

    /// <summary>
    /// Toggles watching on/off for this work item.
    /// When watching, you get notified about changes even if you're not assigned.
    /// </summary>
    private async Task ToggleWatchAsync()
    {
        if (_isTogglingWatch) return;
        _isTogglingWatch = true;

        try
        {
            if (_isWatching)
            {
                _watcherCount = await ApiClient.UnwatchWorkItemAsync(WorkItem.Id);
                _isWatching = false;
            }
            else
            {
                _watcherCount = await ApiClient.WatchWorkItemAsync(WorkItem.Id);
                _isWatching = true;
            }
        }
        catch
        {
            // Toggle failed — revert visual state
        }
        finally
        {
            _isTogglingWatch = false;
        }
    }

    private async Task LoadChildWorkItemsAsync()
    {
        _childWorkItems.Clear();
        _childWorkItems.AddRange(await ApiClient.GetChildWorkItemsAsync(WorkItem.Id));
    }

    private async Task LoadEpicSprintsAsync()
    {
        _epicSprints.Clear();
        _epicSprints.AddRange(await ApiClient.ListSprintsAsync(WorkItem.Id));
    }

    private async Task LoadItemSprintsAsync()
    {
        // Load sprints via GetChildWorkItems -> parent feature -> parent epic
        try
        {
            // Sprint API is scoped to epics; for an Item, we need to find its containing epic
            // For now, sprints are passed in from the parent via Sprints parameter
        }
        finally
        {
            // Use passed Sprints or empty
        }
    }

    // ── Title ───────────────────────────────────────────────

    private void BeginEditTitle()
    {
        _editTitle = WorkItem.Title;
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
        if (string.IsNullOrWhiteSpace(_editTitle) || _editTitle.Trim() == WorkItem.Title) return;

        var updated = await ApiClient.UpdateWorkItemAsync(WorkItem.Id, new UpdateWorkItemDto { Title = _editTitle.Trim() });
        if (updated is not null) await OnWorkItemUpdated.InvokeAsync(updated);
    }

    // ── Description ─────────────────────────────────────────

    private void BeginEditDescription()
    {
        _editDescription = WorkItem.Description ?? "";
        _isEditingDescription = true;
    }

    private void CancelEditDescription() => _isEditingDescription = false;

    private async Task SaveDescriptionAsync()
    {
        _isEditingDescription = false;
        var desc = string.IsNullOrWhiteSpace(_editDescription) ? null : _editDescription.Trim();
        var updated = await ApiClient.UpdateWorkItemAsync(WorkItem.Id, new UpdateWorkItemDto { Description = desc });
        if (updated is not null) await OnWorkItemUpdated.InvokeAsync(updated);
    }

    // ── Priority / Due Date / Story Points ──────────────────

    private async Task SavePriorityAsync()
    {
        if (Enum.TryParse<Priority>(_selectedPriority, out var p))
        {
            var updated = await ApiClient.UpdateWorkItemAsync(WorkItem.Id, new UpdateWorkItemDto { Priority = p });
            if (updated is not null) await OnWorkItemUpdated.InvokeAsync(updated);
        }
    }

    private async Task SaveDueDateAsync(ChangeEventArgs e)
    {
        var val = e.Value?.ToString();
        DateTime? dueDate = DateTime.TryParse(val, out var d)
            ? DateTime.SpecifyKind(d, DateTimeKind.Utc)
            : null;
        var updated = await ApiClient.UpdateWorkItemAsync(WorkItem.Id, new UpdateWorkItemDto { DueDate = dueDate });
        if (updated is not null) await OnWorkItemUpdated.InvokeAsync(updated);
    }

    private async Task SaveStoryPointsAsync(ChangeEventArgs e)
    {
        var val = e.Value?.ToString();
        int? sp = int.TryParse(val, out var n) ? n : null;
        var updated = await ApiClient.UpdateWorkItemAsync(WorkItem.Id, new UpdateWorkItemDto { StoryPoints = sp });
        if (updated is not null) await OnWorkItemUpdated.InvokeAsync(updated);
    }

    // ── Sprint Assignment (Items only) ─────────────────────

    private async Task SaveSprintAsync(ChangeEventArgs e)
    {
        var val = e.Value?.ToString();

        if (WorkItem.SprintId.HasValue)
            await ApiClient.RemoveItemFromSprintAsync(WorkItem.SprintId.Value, WorkItem.Id);

        if (!string.IsNullOrEmpty(val) && Guid.TryParse(val, out var newSprintId))
        {
            await ApiClient.AddItemToSprintAsync(newSprintId, WorkItem.Id);
            _selectedSprintId = val;
        }
        else
        {
            _selectedSprintId = "";
        }

        await RefreshWorkItemAsync();
    }

    private async Task RemoveFromSprintAsync()
    {
        if (WorkItem.SprintId.HasValue)
        {
            await ApiClient.RemoveItemFromSprintAsync(WorkItem.SprintId.Value, WorkItem.Id);
            _selectedSprintId = "";
            await RefreshWorkItemAsync();
        }
    }

    // ── Assignments ─────────────────────────────────────────

    private async Task AssignUserAsync()
    {
        if (!Guid.TryParse(_assignUserId, out var userId)) return;
        await ApiClient.AssignUserAsync(WorkItem.Id, userId);
        _showAssignInput = false;
        _assignUserId = "";
        await RefreshWorkItemAsync();
    }

    private async Task UnassignUserAsync(Guid userId)
    {
        await ApiClient.UnassignUserAsync(WorkItem.Id, userId);
        await RefreshWorkItemAsync();
    }

    // ── Labels ──────────────────────────────────────────────

    private async Task AddLabelAsync(Guid labelId)
    {
        await ApiClient.AddLabelToWorkItemAsync(WorkItem.Id, labelId);
        _showLabelPicker = false;
        _showCreateLabel = false;
        await RefreshWorkItemAsync();
    }

    private async Task CreateAndApplyLabelAsync()
    {
        if (string.IsNullOrWhiteSpace(_newLabelTitle)) return;
        var label = await ApiClient.CreateLabelAsync(Product.Id, new CreateLabelDto { Title = _newLabelTitle.Trim(), Color = _newLabelColor });
        if (label is not null)
        {
            await ApiClient.AddLabelToWorkItemAsync(WorkItem.Id, label.Id);
        }
        _newLabelTitle = "";
        _newLabelColor = "#3b82f6";
        _showCreateLabel = false;
        _showLabelPicker = false;
        await RefreshWorkItemAsync();
    }

    private async Task RemoveLabelAsync(Guid labelId)
    {
        await ApiClient.RemoveLabelFromWorkItemAsync(WorkItem.Id, labelId);
        await RefreshWorkItemAsync();
    }

    // ── Comments ────────────────────────────────────────────

    /// <summary>Handles keydown in the comment textarea. Detects @ for mention typeahead.</summary>
    private async Task HandleCommentKeyDown(KeyboardEventArgs e)
    {
        // If typeahead is visible, let it handle navigation keys
        if (_mentionTypeahead is not null && _mentionStartIndex >= 0)
        {
            if (e.Key is "ArrowDown" or "ArrowUp" or "Enter" or "Escape")
            {
                await _mentionTypeahead.HandleKeyDownAsync(e.Key);
                return;
            }
        }
    }

    /// <summary>Handles keyup in the comment textarea. Tracks @ mention state.</summary>
    private async Task HandleCommentKeyUp(KeyboardEventArgs e)
    {
        // Detect @ character typed — find the most recent @ before cursor
        var content = _newCommentContent ?? "";
        var atIndex = FindMentionAtSymbol(content);

        if (atIndex >= 0 && (atIndex == 0 || content[atIndex - 1] == ' ' || content[atIndex - 1] == '\n'))
        {
            _mentionStartIndex = atIndex;
            _mentionSearchTerm = content[(atIndex + 1)..];
            
            // Only show typeahead if search term doesn't contain spaces
            if (!_mentionSearchTerm.Contains(' ') && _mentionTypeahead is not null)
            {
                await _mentionTypeahead.ShowAsync(_mentionSearchTerm, 0, 0);
            }
        }
        else if (_mentionStartIndex >= 0)
        {
            // User is still typing the mention — update search
            var searchTerm = content[(_mentionStartIndex + 1)..];
            if (searchTerm.Contains(' ') || searchTerm.Contains('\n') || searchTerm.Length == 0)
            {
                // Mention ended
                _mentionStartIndex = -1;
                _mentionSearchTerm = "";
                _mentionTypeahead?.Hide();
            }
        }
        else
        {
            _mentionTypeahead?.Hide();
        }
    }

    /// <summary>Finds the most recent @ symbol in the content that could be a mention start.</summary>
    private static int FindMentionAtSymbol(string content)
    {
        // Find the last @ that isn't part of an email or preceded by a word character
        for (int i = content.Length - 1; i >= 0; i--)
        {
            if (content[i] == '@')
            {
                // Check if it's preceded by whitespace, start of string, or newline
                if (i == 0 || char.IsWhiteSpace(content[i - 1]))
                    return i;
                // If it's preceded by another @, it could be @@ escaping
            }
        }
        return -1;
    }

    /// <summary>Called when a user is selected from the mention typeahead.</summary>
    private async Task HandleMentionSelected(UserSearchResult user)
    {
        if (_mentionStartIndex < 0) return;

        var content = _newCommentContent ?? "";
        var before = content[.._mentionStartIndex];
        var after = content[(_mentionStartIndex + 1 + _mentionSearchTerm.Length)..];
        
        // Insert @username followed by a space
        _newCommentContent = $"{before}@{user.DisplayName.Replace(" ", "")} {after}";
        _mentionStartIndex = -1;
        _mentionSearchTerm = "";

        // Focus back on textarea
        StateHasChanged();
    }

    /// <summary>Called when the mention typeahead is dismissed.</summary>
    private void HandleMentionDismissed()
    {
        _mentionStartIndex = -1;
        _mentionSearchTerm = "";
    }

    private async Task AddCommentAsync()
    {
        if (string.IsNullOrWhiteSpace(_newCommentContent)) return;
        var comment = await ApiClient.CreateCommentAsync(WorkItem.Id, _newCommentContent.Trim());
        if (comment is not null) _comments.Insert(0, comment);
        _newCommentContent = "";
        _showCommentComposer = false;
        _mentionStartIndex = -1;
        _mentionSearchTerm = "";
    }

    private void CancelComment()
    {
        _newCommentContent = "";
        _showCommentComposer = false;
        _mentionStartIndex = -1;
        _mentionSearchTerm = "";
        _mentionTypeahead?.Hide();
    }

    private async Task DeleteCommentAsync(Guid commentId)
    {
        await ApiClient.DeleteCommentAsync(WorkItem.Id, commentId);
        _comments.RemoveAll(c => c.Id == commentId);
    }

    /// <summary>
    /// Renders comment content with @mentions highlighted as clickable links.
    /// Uses regex to find @username patterns and wraps them in styled spans.
    /// </summary>
    private string RenderCommentContent(string? content)
    {
        if (string.IsNullOrWhiteSpace(content))
            return "";

        // First render markdown to HTML
        var html = MdRenderer.RenderToHtml(content);

        // Then highlight @mentions in the rendered HTML
        // Match @username patterns (alphanumeric, hyphen, underscore, dot)
        var mentionRegex = MentionHighlightRegex();
        return mentionRegex.Replace(html, match =>
        {
            var username = match.Groups[1].Value;
            return $"<span class=\"mention-highlight\" title=\"@{username}\">@{username}</span>";
        });
    }

    [GeneratedRegex(@"(?<=^|\s)@([A-Za-z0-9](?:[A-Za-z0-9._-]*[A-Za-z0-9])?)", RegexOptions.Compiled)]
    private static partial Regex MentionHighlightRegex();

    // ── Checklists ──────────────────────────────────────────

    private async Task AddChecklistAsync()
    {
        var checklist = await ApiClient.CreateChecklistAsync(WorkItem.Id, "Checklist");
        if (checklist is not null) _checklists.Add(checklist);
    }

    private async Task DeleteChecklistAsync(Guid checklistId)
    {
        await ApiClient.DeleteChecklistAsync(WorkItem.Id, checklistId);
        _checklists.RemoveAll(c => c.Id == checklistId);
    }

    private async Task AddChecklistItemAsync(Guid checklistId)
    {
        if (string.IsNullOrWhiteSpace(_newChecklistItemTitle)) return;
        await ApiClient.AddChecklistItemAsync(WorkItem.Id, checklistId, _newChecklistItemTitle.Trim());
        _addingItemToChecklist = null;
        _newChecklistItemTitle = "";
        _checklists.Clear();
        _checklists.AddRange(await ApiClient.ListChecklistsAsync(WorkItem.Id));
    }

    private async Task ToggleChecklistItemAsync(Guid checklistId, Guid itemId)
    {
        await ApiClient.ToggleChecklistItemAsync(WorkItem.Id, checklistId, itemId);
        _checklists.Clear();
        _checklists.AddRange(await ApiClient.ListChecklistsAsync(WorkItem.Id));
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
        var att = await ApiClient.AddAttachmentAsync(WorkItem.Id, _attachFileName.Trim(), url, null);
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
                var att = await ApiClient.AddAttachmentAsync(WorkItem.Id, file.Name, null, null);
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
        await ApiClient.RemoveAttachmentAsync(WorkItem.Id, attachmentId);
        _attachments.RemoveAll(a => a.Id == attachmentId);
    }

    // ── Dependencies ────────────────────────────────────────

    private async Task OpenDependencyPickerAsync()
    {
        _swimlaneItems.Clear();
        if (WorkItem.SwimlaneId.HasValue)
        {
            var items = await ApiClient.ListWorkItemsAsync(WorkItem.SwimlaneId.Value);
            _swimlaneItems.AddRange(items);
        }
        _depTargetItemId = "";
        _depType = "BlockedBy";
        _showDependencyPicker = true;
    }

    private async Task AddDependencyAsync()
    {
        if (!Guid.TryParse(_depTargetItemId, out var targetId)) return;
        if (!Enum.TryParse<DependencyType>(_depType, out var depType)) return;

        var dep = await ApiClient.AddDependencyAsync(WorkItem.Id, new AddWorkItemDependencyDto
        {
            DependsOnWorkItemId = targetId,
            Type = depType
        });
        if (dep is not null) _dependencies.Add(dep);
        _showDependencyPicker = false;
    }

    private async Task RemoveDependencyAsync(Guid dependencyId)
    {
        await ApiClient.RemoveDependencyAsync(WorkItem.Id, dependencyId);
        _dependencies.RemoveAll(d => d.Id == dependencyId);
    }

    // ── Time Tracking ───────────────────────────────────────

    private async Task StartTimerAsync()
    {
        var entry = await ApiClient.StartTimerAsync(WorkItem.Id);
        if (entry is not null) _timeEntries.Add(entry);
        RecalculateLiveMinutes();
        StartLiveTimerIfNeeded();
    }

    private async Task StopTimerAsync()
    {
        var entry = await ApiClient.StopTimerAsync(WorkItem.Id);
        if (entry is not null)
        {
            _timeEntries.Clear();
            _timeEntries.AddRange(await ApiClient.ListTimeEntriesAsync(WorkItem.Id));
        }
        StopLiveTimer();
        RecalculateLiveMinutes();
        await RefreshWorkItemAsync();
    }

    // ── Actions ─────────────────────────────────────────────

    private async Task ConfirmActionAsync()
    {
        switch (_confirmAction)
        {
            case "archive":
            case "unarchive":
                await ArchiveWorkItemAsync();
                break;
            case "delete":
                await DeleteWorkItemAsync();
                break;
        }
        _confirmAction = null;
        _showConfirmModal = false;
    }

    private void ShowArchiveConfirm()
    {
        _confirmAction = WorkItem.IsArchived ? "unarchive" : "archive";
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

    private async Task ArchiveWorkItemAsync()
    {
        var updated = await ApiClient.UpdateWorkItemAsync(WorkItem.Id, new UpdateWorkItemDto { IsArchived = !WorkItem.IsArchived });
        if (updated is not null) await OnWorkItemUpdated.InvokeAsync(updated);
    }

    private async Task DeleteWorkItemAsync()
    {
        await ApiClient.DeleteWorkItemAsync(WorkItem.Id);
        await OnWorkItemDeleted.InvokeAsync(WorkItem.Id);
    }

    // ── Helpers ─────────────────────────────────────────────

    private async Task RefreshWorkItemAsync()
    {
        var refreshed = await ApiClient.GetWorkItemAsync(WorkItem.Id);
        if (refreshed is not null) await OnWorkItemUpdated.InvokeAsync(refreshed);
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

    private static string GetPriorityClass(Priority priority) => priority switch
    {
        Priority.Urgent => "priority-urgent",
        Priority.High => "priority-high",
        Priority.Medium => "priority-medium",
        Priority.Low => "priority-low",
        _ => ""
    };

    private static string FormatActivityAction(ActivityDto activity)
    {
        var details = !string.IsNullOrEmpty(activity.Details)
            ? TryParseJson(activity.Details)
            : null;

        return activity.Action switch
        {
            "workitem.created" => $"created item{GetDetail(details, "title", " \"{0}\"")}",
            "workitem.updated" => FormatWorkItemUpdated(details),
            "workitem.moved" => FormatWorkItemMoved(details),
            "workitem.deleted" => $"deleted item{GetDetail(details, "title", " \"{0}\"")}",
            "workitem.assigned" => $"assigned {GetDetail(details, "displayName", "{0}")} to item",
            "workitem.unassigned" => $"unassigned {GetDetail(details, "displayName", "{0}")} from item",
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

    private static string FormatWorkItemUpdated(Dictionary<string, string>? details)
    {
        var fields = GetDetail(details, "fields", "{0}");
        return string.IsNullOrEmpty(fields) ? "updated item" : $"updated {fields}";
    }

    private static string FormatWorkItemMoved(Dictionary<string, string>? details)
    {
        var toTitle = GetDetail(details, "toTitle", "{0}");
        return string.IsNullOrEmpty(toTitle) ? "moved item to another column" : $"moved item to {toTitle}";
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
        var activeMins = activeEntry?.StartTime is not null
            ? (int)(DateTime.UtcNow - activeEntry.StartTime.Value).TotalMinutes
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
