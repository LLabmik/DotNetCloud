using System.Text.Json;
using DotNetCloud.Core.DTOs;
using DotNetCloud.Core.Errors;
using DotNetCloud.Modules.Tracks.Models;
using Microsoft.EntityFrameworkCore;

namespace DotNetCloud.Modules.Tracks.Data.Services;

/// <summary>
/// Manages recurring work item rules — CRUD and processing of due items.
/// </summary>
public sealed class RecurringWorkItemService
{
    private readonly TracksDbContext _db;

    public RecurringWorkItemService(TracksDbContext db) => _db = db;

    public async Task<RecurringRuleDto> CreateRuleAsync(
        Guid productId, Guid createdByUserId, CreateRecurringRuleDto dto, CancellationToken ct)
    {
        var nextRun = Cronos.CronExpression.Parse(dto.CronExpression).GetNextOccurrence(
            DateTime.UtcNow, TimeZoneInfo.Utc, inclusive: false)
            ?? throw new ValidationException("CronExpression", "Cron expression does not produce any future occurrences.");

        var rule = new RecurringRule
        {
            ProductId = productId,
            SwimlaneId = dto.SwimlaneId,
            Type = (WorkItemType)dto.Type,
            TemplateJson = dto.TemplateJson,
            CronExpression = dto.CronExpression,
            NextRunAt = nextRun,
            IsActive = dto.IsActive,
            CreatedByUserId = createdByUserId
        };

        _db.Set<RecurringRule>().Add(rule);
        await _db.SaveChangesAsync(ct);

        return MapToDto(rule);
    }

    public async Task<List<RecurringRuleDto>> GetRulesAsync(Guid productId, CancellationToken ct)
    {
        var rules = await _db.Set<RecurringRule>()
            .Include(rr => rr.Swimlane)
            .Where(rr => rr.ProductId == productId)
            .OrderBy(rr => rr.CreatedAt)
            .ToListAsync(ct);

        return rules.Select(MapToDto).ToList();
    }

    public async Task<RecurringRuleDto?> GetRuleAsync(Guid ruleId, CancellationToken ct)
    {
        var rule = await _db.Set<RecurringRule>()
            .Include(rr => rr.Swimlane)
            .FirstOrDefaultAsync(rr => rr.Id == ruleId, ct);

        return rule is null ? null : MapToDto(rule);
    }

    public async Task<RecurringRuleDto> UpdateRuleAsync(
        Guid ruleId, UpdateRecurringRuleDto dto, Guid updatedByUserId, CancellationToken ct)
    {
        var rule = await _db.Set<RecurringRule>()
            .Include(rr => rr.Swimlane)
            .FirstOrDefaultAsync(rr => rr.Id == ruleId, ct)
            ?? throw new NotFoundException("RecurringRule", ruleId);

        if (dto.SwimlaneId.HasValue) rule.SwimlaneId = dto.SwimlaneId.Value;
        if (dto.Type.HasValue) rule.Type = (WorkItemType)dto.Type.Value;
        if (dto.TemplateJson is not null) rule.TemplateJson = dto.TemplateJson;
        if (dto.IsActive.HasValue) rule.IsActive = dto.IsActive.Value;

        if (dto.CronExpression is not null)
        {
            rule.CronExpression = dto.CronExpression;
            var nextRun = Cronos.CronExpression.Parse(dto.CronExpression).GetNextOccurrence(
                DateTime.UtcNow, TimeZoneInfo.Utc, inclusive: false);
            if (nextRun is null)
                throw new ValidationException("CronExpression", "Cron expression does not produce any future occurrences.");
            rule.NextRunAt = nextRun.Value;
        }

        rule.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);

        return MapToDto(rule);
    }

    public async Task DeleteRuleAsync(Guid ruleId, CancellationToken ct)
    {
        var rule = await _db.Set<RecurringRule>()
            .FirstOrDefaultAsync(rr => rr.Id == ruleId, ct)
            ?? throw new NotFoundException("RecurringRule", ruleId);

        _db.Set<RecurringRule>().Remove(rule);
        await _db.SaveChangesAsync(ct);
    }

    /// <summary>
    /// Finds all active rules whose NextRunAt has passed and creates work items for them.
    /// Returns the list of created work item IDs.
    /// </summary>
    public async Task<List<Guid>> ProcessDueRecurringItemsAsync(CancellationToken ct)
    {
        var now = DateTime.UtcNow;
        var dueRules = await _db.Set<RecurringRule>()
            .Include(rr => rr.Swimlane)
            .Where(rr => rr.IsActive && rr.NextRunAt <= now)
            .ToListAsync(ct);

        var createdIds = new List<Guid>();

        foreach (var rule in dueRules)
        {
            try
            {
                using var doc = JsonDocument.Parse(rule.TemplateJson);
                var root = doc.RootElement;

                var title = root.TryGetProperty("title", out var t) && t.ValueKind == JsonValueKind.String
                    ? t.GetString()! : "Recurring item";

                var description = root.TryGetProperty("description", out var desc) && desc.ValueKind == JsonValueKind.String
                    ? desc.GetString() : null;

                var priority = Priority.None;
                if (root.TryGetProperty("priority", out var p) && p.ValueKind == JsonValueKind.String)
                    Enum.TryParse<Priority>(p.GetString(), ignoreCase: true, out priority);

                var storyPoints = root.TryGetProperty("storyPoints", out var sp) && sp.ValueKind == JsonValueKind.Number
                    ? (int?)sp.GetInt32() : null;

                // Get position: put at top of swimlane
                var maxPosition = await _db.Set<WorkItem>()
                    .Where(wi => wi.SwimlaneId == rule.SwimlaneId && !wi.IsArchived)
                    .MaxAsync(wi => (double?)wi.Position, ct) ?? 0;

                var position = maxPosition > 0 ? maxPosition + 1024 : 1000;

                var itemNumber = await _db.Set<WorkItem>()
                    .Where(wi => wi.ProductId == rule.ProductId)
                    .MaxAsync(wi => (int?)wi.ItemNumber, ct) ?? 0;

                var workItem = new WorkItem
                {
                    ProductId = rule.ProductId,
                    SwimlaneId = rule.SwimlaneId,
                    Type = rule.Type,
                    Title = title,
                    Description = description,
                    Priority = priority,
                    StoryPoints = storyPoints,
                    Position = position,
                    ItemNumber = itemNumber + 1,
                    RecurringRuleId = rule.Id,
                    CreatedByUserId = rule.CreatedByUserId
                };

                _db.Set<WorkItem>().Add(workItem);

                // Handle labels
                if (root.TryGetProperty("labels", out var labels) && labels.ValueKind == JsonValueKind.Array)
                {
                    foreach (var labelStr in labels.EnumerateArray())
                    {
                        var labelTitle = labelStr.GetString();
                        if (string.IsNullOrWhiteSpace(labelTitle)) continue;

                        var label = await _db.Set<Label>()
                            .FirstOrDefaultAsync(l => l.ProductId == rule.ProductId
                                                   && l.Title == labelTitle, ct);

                        if (label is not null)
                        {
                            _db.Set<WorkItemLabel>().Add(new WorkItemLabel
                            {
                                WorkItemId = workItem.Id,
                                LabelId = label.Id
                            });
                        }
                    }
                }

                // Handle assignees
                if (root.TryGetProperty("assigneeIds", out var assigneeIds) && assigneeIds.ValueKind == JsonValueKind.Array)
                {
                    foreach (var userIdStr in assigneeIds.EnumerateArray())
                    {
                        var userIdText = userIdStr.GetString();
                        if (string.IsNullOrWhiteSpace(userIdText) || !Guid.TryParse(userIdText, out var userId))
                            continue;

                        _db.Set<WorkItemAssignment>().Add(new WorkItemAssignment
                        {
                            WorkItemId = workItem.Id,
                            UserId = userId
                        });
                    }
                }

                createdIds.Add(workItem.Id);

                // Update rule timing
                rule.LastRunAt = now;
                var nextRun = Cronos.CronExpression.Parse(rule.CronExpression)
                    .GetNextOccurrence(now, TimeZoneInfo.Utc, inclusive: false);
                rule.NextRunAt = nextRun ?? now.AddMonths(1);
                rule.UpdatedAt = now;
            }
            catch (Exception ex)
            {
                // Log and continue with other rules
                System.Diagnostics.Debug.WriteLine($"RecurringRule {rule.Id} failed: {ex.Message}");
                rule.UpdatedAt = now;
            }
        }

        if (createdIds.Count > 0)
            await _db.SaveChangesAsync(ct);

        return createdIds;
    }

    private static RecurringRuleDto MapToDto(RecurringRule r) => new()
    {
        Id = r.Id,
        ProductId = r.ProductId,
        SwimlaneId = r.SwimlaneId,
        SwimlaneTitle = r.Swimlane?.Title,
        Type = (WorkItemType)r.Type,
        TemplateJson = r.TemplateJson,
        CronExpression = r.CronExpression,
        NextRunAt = r.NextRunAt,
        IsActive = r.IsActive,
        LastRunAt = r.LastRunAt,
        CreatedByUserId = r.CreatedByUserId,
        CreatedAt = r.CreatedAt,
        UpdatedAt = r.UpdatedAt
    };
}
