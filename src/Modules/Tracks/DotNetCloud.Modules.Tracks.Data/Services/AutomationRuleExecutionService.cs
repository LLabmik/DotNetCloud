using DotNetCloud.Core.DTOs;
using DotNetCloud.Modules.Tracks.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace DotNetCloud.Modules.Tracks.Data.Services;

/// <summary>
/// Implements <see cref="IAutomationRuleExecutionService"/> to evaluate and execute
/// automation rules against work item events.
/// </summary>
public sealed class AutomationRuleExecutionService : IAutomationRuleExecutionService
{
    private readonly TracksDbContext _db;
    private readonly AutomationRuleService _ruleService;
    private readonly ILogger<AutomationRuleExecutionService> _logger;

    public AutomationRuleExecutionService(
        TracksDbContext db,
        AutomationRuleService ruleService,
        ILogger<AutomationRuleExecutionService> logger)
    {
        _db = db;
        _ruleService = ruleService;
        _logger = logger;
    }

    public async Task ExecuteAsync(string triggerType, Guid workItemId,
        string? previousSwimlaneId = null, string? newSwimlaneId = null, CancellationToken ct = default)
    {
        var workItem = await _db.WorkItems
            .Include(wi => wi.Assignments)
            .FirstOrDefaultAsync(wi => wi.Id == workItemId, ct);

        if (workItem is null) return;

        var context = new AutomationContext
        {
            PreviousSwimlaneId = Guid.TryParse(previousSwimlaneId, out var prevId) ? prevId : null,
            NewSwimlaneId = Guid.TryParse(newSwimlaneId, out var newId) ? newId : null
        };

        // Resolve swimlane title for context
        if (context.NewSwimlaneId.HasValue)
        {
            var swimlane = await _db.Swimlanes.FindAsync([context.NewSwimlaneId.Value], ct);
            context = context with { SwimlaneTitle = swimlane?.Title };
        }

        var actions = await _ruleService.EvaluateRulesAsync(triggerType, workItem, context, ct);

        foreach (var action in actions)
        {
            await ExecuteActionAsync(workItem, action, ct);
        }
    }

    private async Task ExecuteActionAsync(Models.WorkItem workItem, AutomationAction action, CancellationToken ct)
    {
        try
        {
            switch (action.Type)
            {
                case "add_label":
                    if (action.Parameters.TryGetValue("label_id", out var labelIdStr)
                        && Guid.TryParse(labelIdStr, out var labelId))
                    {
                        var existing = await _db.WorkItemLabels
                            .FirstOrDefaultAsync(wil => wil.WorkItemId == workItem.Id && wil.LabelId == labelId, ct);
                        if (existing is null)
                        {
                            _db.WorkItemLabels.Add(new Models.WorkItemLabel
                            {
                                WorkItemId = workItem.Id,
                                LabelId = labelId
                            });
                            await _db.SaveChangesAsync(ct);
                            _logger.LogInformation("Automation: Added label {LabelId} to work item {WorkItemId}", labelId, workItem.Id);
                        }
                    }
                    break;

                case "remove_label":
                    if (action.Parameters.TryGetValue("label_id", out var removeLabelIdStr)
                        && Guid.TryParse(removeLabelIdStr, out var removeLabelId))
                    {
                        var toRemove = await _db.WorkItemLabels
                            .FirstOrDefaultAsync(wil => wil.WorkItemId == workItem.Id && wil.LabelId == removeLabelId, ct);
                        if (toRemove is not null)
                        {
                            _db.WorkItemLabels.Remove(toRemove);
                            await _db.SaveChangesAsync(ct);
                        }
                    }
                    break;

                case "move_to_swimlane":
                    if (action.Parameters.TryGetValue("swimlane_id", out var swimlaneIdStr)
                        && Guid.TryParse(swimlaneIdStr, out var targetSwimlaneId))
                    {
                        workItem.SwimlaneId = targetSwimlaneId;
                        workItem.UpdatedAt = DateTime.UtcNow;
                        await _db.SaveChangesAsync(ct);
                        _logger.LogInformation("Automation: Moved work item {WorkItemId} to swimlane {SwimlaneId}", workItem.Id, targetSwimlaneId);
                    }
                    break;

                case "assign":
                    if (action.Parameters.TryGetValue("user_id", out var userIdStr)
                        && Guid.TryParse(userIdStr, out var assigneeId))
                    {
                        var existing = await _db.WorkItemAssignments
                            .FirstOrDefaultAsync(a => a.WorkItemId == workItem.Id && a.UserId == assigneeId, ct);
                        if (existing is null)
                        {
                            _db.WorkItemAssignments.Add(new Models.WorkItemAssignment
                            {
                                WorkItemId = workItem.Id,
                                UserId = assigneeId
                            });
                            workItem.UpdatedAt = DateTime.UtcNow;
                            await _db.SaveChangesAsync(ct);
                        }
                    }
                    break;

                case "set_priority":
                    if (action.Parameters.TryGetValue("priority", out var priorityStr)
                        && Enum.TryParse<Priority>(priorityStr, true, out var priority))
                    {
                        workItem.Priority = priority;
                        workItem.UpdatedAt = DateTime.UtcNow;
                        await _db.SaveChangesAsync(ct);
                    }
                    break;

                case "set_field":
                    if (action.Parameters.TryGetValue("field_id", out var fieldIdStr)
                        && Guid.TryParse(fieldIdStr, out var fieldId)
                        && action.Parameters.TryGetValue("value", out var fieldValue))
                    {
                        var existing = await _db.WorkItemFieldValues
                            .FirstOrDefaultAsync(fv => fv.WorkItemId == workItem.Id && fv.CustomFieldId == fieldId, ct);
                        if (existing is not null)
                        {
                            existing.Value = fieldValue;
                        }
                        else
                        {
                            _db.WorkItemFieldValues.Add(new Models.WorkItemFieldValue
                            {
                                WorkItemId = workItem.Id,
                                CustomFieldId = fieldId,
                                Value = fieldValue
                            });
                        }
                        workItem.UpdatedAt = DateTime.UtcNow;
                        await _db.SaveChangesAsync(ct);
                    }
                    break;

                case "add_comment":
                    if (action.Parameters.TryGetValue("content", out var commentContent))
                    {
                        _db.WorkItemComments.Add(new Models.WorkItemComment
                        {
                            WorkItemId = workItem.Id,
                            UserId = workItem.CreatedByUserId,
                            Content = commentContent
                        });
                        await _db.SaveChangesAsync(ct);
                    }
                    break;

                case "notify":
                    _logger.LogInformation("Automation: Notification triggered for work item {WorkItemId}", workItem.Id);
                    break;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to execute automation action {ActionType} on work item {WorkItemId}",
                action.Type, workItem.Id);
        }
    }
}
