using System.Text.Json;
using DotNetCloud.Core.DTOs;
using DotNetCloud.Modules.Tracks.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace DotNetCloud.Modules.Tracks.Data.Services;

/// <summary>
/// Manages automation rules: CRUD and rule evaluation against work item events.
/// </summary>
public sealed class AutomationRuleService
{
    private readonly TracksDbContext _db;
    private readonly ILogger<AutomationRuleService> _logger;

    public AutomationRuleService(TracksDbContext db, ILogger<AutomationRuleService> logger)
    {
        _db = db;
        _logger = logger;
    }

    /// <summary>Lists all automation rules for a product.</summary>
    public async Task<List<AutomationRuleDto>> ListAsync(Guid productId, CancellationToken ct)
    {
        return await _db.AutomationRules
            .Where(ar => ar.ProductId == productId)
            .OrderBy(ar => ar.CreatedAt)
            .Select(ar => ToDto(ar))
            .ToListAsync(ct);
    }

    /// <summary>Gets a single automation rule.</summary>
    public async Task<AutomationRuleDto?> GetAsync(Guid ruleId, CancellationToken ct)
    {
        var rule = await _db.AutomationRules.FindAsync([ruleId], ct);
        return rule is null ? null : ToDto(rule);
    }

    /// <summary>Creates a new automation rule.</summary>
    public async Task<AutomationRuleDto> CreateAsync(Guid productId, CreateAutomationRuleDto dto, Guid userId, CancellationToken ct)
    {
        var rule = new AutomationRule
        {
            ProductId = productId,
            Name = dto.Name,
            Trigger = dto.Trigger,
            ConditionsJson = dto.ConditionsJson,
            ActionsJson = dto.ActionsJson,
            IsActive = dto.IsActive,
            CreatedByUserId = userId
        };

        _db.AutomationRules.Add(rule);
        await _db.SaveChangesAsync(ct);
        _logger.LogInformation("Created automation rule {RuleId}: {RuleName}", rule.Id, rule.Name);
        return ToDto(rule);
    }

    /// <summary>Updates an automation rule.</summary>
    public async Task<AutomationRuleDto?> UpdateAsync(Guid ruleId, UpdateAutomationRuleDto dto, CancellationToken ct)
    {
        var rule = await _db.AutomationRules.FindAsync([ruleId], ct);
        if (rule is null) return null;

        if (dto.Name is not null) rule.Name = dto.Name;
        if (dto.Trigger is not null) rule.Trigger = dto.Trigger;
        if (dto.ConditionsJson is not null) rule.ConditionsJson = dto.ConditionsJson;
        if (dto.ActionsJson is not null) rule.ActionsJson = dto.ActionsJson;
        if (dto.IsActive.HasValue) rule.IsActive = dto.IsActive.Value;
        rule.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync(ct);
        return ToDto(rule);
    }

    /// <summary>Deletes an automation rule.</summary>
    public async Task<bool> DeleteAsync(Guid ruleId, CancellationToken ct)
    {
        var rule = await _db.AutomationRules.FindAsync([ruleId], ct);
        if (rule is null) return false;

        _db.AutomationRules.Remove(rule);
        await _db.SaveChangesAsync(ct);
        return true;
    }

    /// <summary>
    /// Evaluates all active rules matching the given trigger against a work item.
    /// Returns a list of action objects to execute.
    /// </summary>
    public async Task<List<AutomationAction>> EvaluateRulesAsync(
        string triggerType, WorkItem workItem, AutomationContext context, CancellationToken ct)
    {
        var rules = await _db.AutomationRules
            .Where(ar => ar.ProductId == workItem.ProductId && ar.Trigger == triggerType && ar.IsActive)
            .ToListAsync(ct);

        var actions = new List<AutomationAction>();

        foreach (var rule in rules)
        {
            try
            {
                if (EvaluateConditions(rule.ConditionsJson, workItem, context))
                {
                    var ruleActions = ParseActions(rule.ActionsJson);
                    actions.AddRange(ruleActions);

                    rule.LastTriggeredAt = DateTime.UtcNow;
                    _logger.LogDebug("Rule {RuleId} '{RuleName}' triggered for work item {WorkItemId}",
                        rule.Id, rule.Name, workItem.Id);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error evaluating rule {RuleId} '{RuleName}'", rule.Id, rule.Name);
            }
        }

        if (actions.Count > 0)
            await _db.SaveChangesAsync(ct);

        return actions;
    }

    private static bool EvaluateConditions(string conditionsJson, WorkItem workItem, AutomationContext context)
    {
        if (string.IsNullOrWhiteSpace(conditionsJson) || conditionsJson == "[]")
            return true;

        var conditions = JsonSerializer.Deserialize<List<AutomationCondition>>(conditionsJson);
        if (conditions is null || conditions.Count == 0) return true;

        foreach (var cond in conditions)
        {
            var fieldValue = GetFieldValue(cond.Field, workItem, context);
            if (!EvaluateCondition(fieldValue, cond.Operator, cond.Value))
                return false;
        }

        return true;
    }

    private static string? GetFieldValue(string field, WorkItem workItem, AutomationContext context)
    {
        return field switch
        {
            "priority" => ((int)workItem.Priority).ToString(),
            "type" => workItem.Type.ToString(),
            "swimlane_id" => workItem.SwimlaneId?.ToString(),
            "swimlane_title" => context.SwimlaneTitle,
            "assignee_id" => workItem.Assignments?.FirstOrDefault()?.UserId.ToString(),
            "story_points" => workItem.StoryPoints?.ToString(),
            "due_date" => workItem.DueDate?.ToString("O"),
            _ => null
        };
    }

    private static bool EvaluateCondition(string? fieldValue, string op, string expectedValue)
    {
        return op switch
        {
            "equals" => string.Equals(fieldValue, expectedValue, StringComparison.OrdinalIgnoreCase),
            "not_equals" => !string.Equals(fieldValue, expectedValue, StringComparison.OrdinalIgnoreCase),
            "contains" => fieldValue?.Contains(expectedValue, StringComparison.OrdinalIgnoreCase) ?? false,
            "greater_than" => double.TryParse(fieldValue, out var fv) && double.TryParse(expectedValue, out var ev) && fv > ev,
            "less_than" => double.TryParse(fieldValue, out var fvl) && double.TryParse(expectedValue, out var evl) && fvl < evl,
            _ => true
        };
    }

    private static List<AutomationAction> ParseActions(string actionsJson)
    {
        if (string.IsNullOrWhiteSpace(actionsJson) || actionsJson == "[]")
            return [];

        return JsonSerializer.Deserialize<List<AutomationAction>>(actionsJson) ?? [];
    }

    private static AutomationRuleDto ToDto(AutomationRule rule) => new()
    {
        Id = rule.Id,
        ProductId = rule.ProductId,
        Name = rule.Name,
        Trigger = rule.Trigger,
        ConditionsJson = rule.ConditionsJson,
        ActionsJson = rule.ActionsJson,
        IsActive = rule.IsActive,
        CreatedByUserId = rule.CreatedByUserId,
        LastTriggeredAt = rule.LastTriggeredAt,
        CreatedAt = rule.CreatedAt,
        UpdatedAt = rule.UpdatedAt
    };
}

/// <summary>Context passed to rule evaluation containing additional data about the triggering event.</summary>
public sealed record AutomationContext
{
    public string? SwimlaneTitle { get; init; }
    public Guid? PreviousSwimlaneId { get; init; }
    public Guid? NewSwimlaneId { get; init; }
}

/// <summary>A condition to evaluate as part of a rule.</summary>
public sealed class AutomationCondition
{
    public string Field { get; set; } = "";
    public string Operator { get; set; } = "equals";
    public string Value { get; set; } = "";
}

/// <summary>An action to execute when a rule triggers.</summary>
public sealed class AutomationAction
{
    public string Type { get; set; } = "";
    public Dictionary<string, string> Parameters { get; set; } = new();
}
