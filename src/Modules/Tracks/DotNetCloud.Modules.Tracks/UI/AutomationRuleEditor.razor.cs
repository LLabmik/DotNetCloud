using System.Text.Json;
using DotNetCloud.Core.DTOs;
using DotNetCloud.Modules.Tracks.Services;
using Microsoft.AspNetCore.Components;

namespace DotNetCloud.Modules.Tracks.UI;

/// <summary>
/// Editor for automation rules: create, edit, delete, enable/disable rules.
/// </summary>
public partial class AutomationRuleEditor : ComponentBase
{
    [Parameter] public required Guid ProductId { get; set; }
    [Parameter] public List<LabelDto>? ProductLabels { get; set; }
    [Parameter] public List<SwimlaneDto>? ProductSwimlanes { get; set; }

    [Inject] private ITracksApiClient ApiClient { get; set; } = default!;

    private List<AutomationRuleDto>? _rules;
    private bool _isEditing;
    private bool _isSaving;
    private Guid? _editingId;
    private string _editName = "";
    private string _editTrigger = "work_item_created";
    private string _editError = "";
    private List<EditActionState> _editActions = new();

    protected override async Task OnParametersSetAsync()
    {
        await LoadRulesAsync();
    }

    private async Task LoadRulesAsync()
    {
        try
        {
            _rules = await ApiClient.ListAutomationRulesAsync(ProductId);
        }
        catch
        {
            _rules = [];
        }
    }

    private void StartCreating()
    {
        _editingId = null;
        _editName = "";
        _editTrigger = "work_item_created";
        _editActions = new List<EditActionState>();
        _editError = "";
        _isEditing = true;
    }

    private void EditRule(AutomationRuleDto rule)
    {
        _editingId = rule.Id;
        _editName = rule.Name;
        _editTrigger = rule.Trigger;
        _editError = "";
        _isEditing = true;

        try
        {
            _editActions = JsonSerializer.Deserialize<List<EditActionState>>(rule.ActionsJson)
                ?? new List<EditActionState>();
        }
        catch
        {
            _editActions = new List<EditActionState>();
        }
    }

    private async Task SaveRule()
    {
        if (string.IsNullOrWhiteSpace(_editName))
        {
            _editError = "Rule name is required.";
            return;
        }

        _isSaving = true;
        _editError = "";

        try
        {
            // Convert edit actions to JSON actions
            var actions = _editActions.Select(a => new
            {
                type = a.Type,
                parameters = BuildActionParameters(a)
            }).ToList();

            var actionsJson = JsonSerializer.Serialize(actions);

            if (_editingId.HasValue)
            {
                await ApiClient.UpdateAutomationRuleAsync(_editingId.Value, new UpdateAutomationRuleDto
                {
                    Name = _editName,
                    Trigger = _editTrigger,
                    ActionsJson = actionsJson
                });
            }
            else
            {
                await ApiClient.CreateAutomationRuleAsync(ProductId, new CreateAutomationRuleDto
                {
                    Name = _editName,
                    Trigger = _editTrigger,
                    ActionsJson = actionsJson
                });
            }

            _isEditing = false;
            await LoadRulesAsync();
        }
        catch (Exception ex)
        {
            _editError = ex.Message;
        }
        finally
        {
            _isSaving = false;
        }
    }

    private static Dictionary<string, string> BuildActionParameters(EditActionState action)
    {
        var parameters = new Dictionary<string, string>();

        switch (action.Type)
        {
            case "add_label":
                if (!string.IsNullOrEmpty(action.LabelId))
                    parameters["label_id"] = action.LabelId;
                break;
            case "remove_label":
                if (!string.IsNullOrEmpty(action.LabelId))
                    parameters["label_id"] = action.LabelId;
                break;
            case "move_to_swimlane":
                if (!string.IsNullOrEmpty(action.SwimlaneId))
                    parameters["swimlane_id"] = action.SwimlaneId;
                break;
            case "assign":
                if (!string.IsNullOrEmpty(action.UserIdStr))
                    parameters["user_id"] = action.UserIdStr;
                break;
            case "set_priority":
                if (!string.IsNullOrEmpty(action.PriorityValue))
                    parameters["priority"] = action.PriorityValue;
                break;
            case "set_field":
                if (!string.IsNullOrEmpty(action.FieldOrCommentValue))
                    parameters["field_id"] = action.FieldOrCommentValue;
                break;
            case "add_comment":
                if (!string.IsNullOrEmpty(action.FieldOrCommentValue))
                    parameters["content"] = action.FieldOrCommentValue;
                break;
        }

        return parameters;
    }

    private void CancelEdit()
    {
        _isEditing = false;
    }

    private async Task DeleteRule(AutomationRuleDto rule)
    {
        try
        {
            await ApiClient.DeleteAutomationRuleAsync(rule.Id);
            await LoadRulesAsync();
        }
        catch { }
    }

    private async Task ToggleRule(AutomationRuleDto rule, bool isActive)
    {
        try
        {
            await ApiClient.UpdateAutomationRuleAsync(rule.Id, new UpdateAutomationRuleDto { IsActive = isActive });
            // Toggle handled below via reload
            await LoadRulesAsync();
        }
        catch { }
    }

    private void AddAction()
    {
        _editActions.Add(new EditActionState());
    }

    private void RemoveAction(int index)
    {
        if (index >= 0 && index < _editActions.Count)
            _editActions.RemoveAt(index);
    }

    private void OnActionTypeChanged(EditActionState action)
    {
        // Reset parameters when action type changes
        action.LabelId = null;
        action.SwimlaneId = null;
        action.UserIdStr = null;
        action.PriorityValue = null;
        action.FieldOrCommentValue = null;
    }

    private async Task CreatePreset(string preset)
    {
        string name, trigger, actionsJson;

        switch (preset)
        {
            case "done_label":
                name = "Auto-label Done";
                trigger = "work_item_moved";
                actionsJson = "[{\"type\":\"add_label\",\"parameters\":{}}]";
                break;
            case "urgent_notify":
                name = "Urgent Item Alert";
                trigger = "work_item_created";
                actionsJson = "[{\"type\":\"notify\",\"parameters\":{}}]";
                break;
            case "due_reminder":
                name = "Due Date Reminder";
                trigger = "due_date_approaching";
                actionsJson = "[{\"type\":\"notify\",\"parameters\":{}}]";
                break;
            default:
                return;
        }

        try
        {
            await ApiClient.CreateAutomationRuleAsync(ProductId, new CreateAutomationRuleDto
            {
                Name = name,
                Trigger = trigger,
                ActionsJson = actionsJson
            });
            await LoadRulesAsync();
        }
        catch { }
    }

    private static string FormatTrigger(string trigger) => trigger switch
    {
        "work_item_created" => "Item created",
        "work_item_moved" => "Item moved",
        "status_changed" => "Status changed",
        "due_date_approaching" => "Due date approaching",
        "assigned" => "Item assigned",
        _ => trigger
    };

    private static string FormatActions(string actionsJson)
    {
        try
        {
            var actions = JsonSerializer.Deserialize<List<JsonElement>>(actionsJson);
            if (actions is null || actions.Count == 0) return "Nothing";
            var parts = actions.Select(a =>
            {
                var type = a.GetProperty("type").GetString() ?? "";
                return type switch
                {
                    "add_label" => "Add label",
                    "remove_label" => "Remove label",
                    "move_to_swimlane" => "Move",
                    "assign" => "Assign",
                    "set_priority" => "Set priority",
                    "set_field" => "Set field",
                    "add_comment" => "Add comment",
                    _ => type
                };
            });
            return string.Join(", ", parts);
        }
        catch { return "Unknown"; }
    }

    private string FormatActionsPreview()
    {
        if (_editActions.Count == 0) return "do nothing";
        var parts = _editActions.Select(a => a.Type switch
        {
            "add_label" => "add label",
            "remove_label" => "remove label",
            "move_to_swimlane" => "move item",
            "assign" => "assign user",
            "set_priority" => "set priority",
            "set_field" => "set field",
            "add_comment" => "add comment",
            _ => a.Type
        });
        return string.Join(" and ", parts);
    }

    private sealed class EditActionState
    {
        public string Type { get; set; } = "";
        public string? LabelId { get; set; }
        public string? SwimlaneId { get; set; }
        public string? UserIdStr { get; set; }
        public string? PriorityValue { get; set; }
        public string? FieldOrCommentValue { get; set; }
    }
}
