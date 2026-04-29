using System.Text.Json;
using DotNetCloud.Core.DTOs;
using DotNetCloud.Modules.Tracks.Models;
using Microsoft.EntityFrameworkCore;

namespace DotNetCloud.Modules.Tracks.Data.Services;

public sealed class ItemTemplateService
{
    private readonly TracksDbContext _db;

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public ItemTemplateService(TracksDbContext db)
    {
        _db = db;
    }

    public async Task<List<ItemTemplateDto>> GetTemplatesByProductAsync(Guid productId, CancellationToken ct)
    {
        return await _db.ItemTemplates
            .AsNoTracking()
            .Where(t => t.ProductId == productId)
            .OrderBy(t => t.Name)
            .Select(t => new ItemTemplateDto
            {
                Id = t.Id,
                ProductId = t.ProductId,
                Name = t.Name,
                TitlePattern = t.TitlePattern,
                Description = t.Description,
                Priority = t.Priority,
                LabelIdsJson = t.LabelIdsJson,
                ChecklistsJson = t.ChecklistsJson,
                CreatedByUserId = t.CreatedByUserId,
                CreatedAt = t.CreatedAt,
                UpdatedAt = t.UpdatedAt
            })
            .ToListAsync(ct);
    }

    public async Task<ItemTemplateDto> SaveItemAsTemplateAsync(Guid workItemId, Guid createdByUserId, SaveItemAsTemplateDto dto, CancellationToken ct)
    {
        var workItem = await _db.WorkItems
            .Include(wi => wi.WorkItemLabels)
            .Include(wi => wi.Checklists)
            .AsNoTracking()
            .FirstOrDefaultAsync(wi => wi.Id == workItemId && !wi.IsDeleted, ct);

        if (workItem is null)
            throw new InvalidOperationException($"WorkItem with ID {workItemId} not found.");

        // Serialize label IDs to JSON array
        var labelIds = workItem.WorkItemLabels
            .Select(wl => wl.LabelId)
            .ToList();

        var labelIdsJson = JsonSerializer.Serialize(labelIds, JsonOptions);

        // Serialize checklists to JSON
        string? checklistsJson = null;
        if (workItem.Checklists.Count > 0)
        {
            var checklistData = workItem.Checklists.Select(c => new
            {
                title = c.Title,
                position = c.Position,
                items = c.Items.Select(i => new
                {
                    title = i.Title,
                    position = i.Position
                })
            });
            checklistsJson = JsonSerializer.Serialize(checklistData, JsonOptions);
        }

        var template = new ItemTemplate
        {
            ProductId = workItem.ProductId,
            Name = dto.Name,
            TitlePattern = workItem.Title,
            Description = workItem.Description,
            Priority = workItem.Priority,
            LabelIdsJson = labelIdsJson,
            ChecklistsJson = checklistsJson,
            CreatedByUserId = createdByUserId,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        _db.ItemTemplates.Add(template);
        await _db.SaveChangesAsync(ct);

        return new ItemTemplateDto
        {
            Id = template.Id,
            ProductId = template.ProductId,
            Name = template.Name,
            TitlePattern = template.TitlePattern,
            Description = template.Description,
            Priority = template.Priority,
            LabelIdsJson = template.LabelIdsJson,
            ChecklistsJson = template.ChecklistsJson,
            CreatedByUserId = template.CreatedByUserId,
            CreatedAt = template.CreatedAt,
            UpdatedAt = template.UpdatedAt
        };
    }

    public async Task<WorkItemDto> CreateItemFromTemplateAsync(Guid templateId, Guid swimlaneId, Guid createdByUserId, CreateItemFromTemplateDto dto, CancellationToken ct)
    {
        var template = await _db.ItemTemplates
            .AsNoTracking()
            .FirstOrDefaultAsync(t => t.Id == templateId, ct);

        if (template is null)
            throw new InvalidOperationException($"ItemTemplate with ID {templateId} not found.");

        var now = DateTime.UtcNow;
        var etag = Guid.NewGuid().ToString("N");

        var workItem = new WorkItem
        {
            ProductId = template.ProductId,
            Type = WorkItemType.Item,
            SwimlaneId = swimlaneId,
            Title = string.IsNullOrWhiteSpace(template.TitlePattern) ? dto.Title : template.TitlePattern,
            Description = template.Description,
            Priority = template.Priority,
            Position = 0,
            CreatedByUserId = createdByUserId,
            ETag = etag,
            CreatedAt = now,
            UpdatedAt = now
        };

        _db.WorkItems.Add(workItem);

        // Apply labels from template
        if (!string.IsNullOrWhiteSpace(template.LabelIdsJson))
        {
            try
            {
                var labelIds = JsonSerializer.Deserialize<List<Guid>>(template.LabelIdsJson, JsonOptions);
                if (labelIds is { Count: > 0 })
                {
                    foreach (var labelId in labelIds)
                    {
                        _db.WorkItemLabels.Add(new WorkItemLabel
                        {
                            WorkItemId = workItem.Id,
                            LabelId = labelId
                        });
                    }
                }
            }
            catch (JsonException)
            {
                // Ignore malformed label JSON
            }
        }

        // Apply checklists from template
        if (!string.IsNullOrWhiteSpace(template.ChecklistsJson))
        {
            try
            {
                var checklistEntries = JsonSerializer.Deserialize<List<ChecklistDefinition>>(template.ChecklistsJson, JsonOptions);
                if (checklistEntries is { Count: > 0 })
                {
                    foreach (var cl in checklistEntries)
                    {
                        var checklist = new Checklist
                        {
                            ItemId = workItem.Id,
                            Title = cl.Title,
                            Position = cl.Position,
                            CreatedAt = now
                        };
                        _db.Checklists.Add(checklist);

                        if (cl.Items is { Count: > 0 })
                        {
                            foreach (var item in cl.Items)
                            {
                                _db.ChecklistItems.Add(new ChecklistItem
                                {
                                    ChecklistId = checklist.Id,
                                    Title = item.Title,
                                    Position = item.Position,
                                    CreatedAt = now,
                                    UpdatedAt = now
                                });
                            }
                        }
                    }
                }
            }
            catch (JsonException)
            {
                // Ignore malformed checklist JSON
            }
        }

        await _db.SaveChangesAsync(ct);

        return new WorkItemDto
        {
            Id = workItem.Id,
            ProductId = workItem.ProductId,
            ParentWorkItemId = workItem.ParentWorkItemId,
            Type = workItem.Type,
            SwimlaneId = workItem.SwimlaneId,
            ItemNumber = workItem.ItemNumber,
            Title = workItem.Title,
            Description = workItem.Description,
            Position = workItem.Position,
            Priority = workItem.Priority,
            DueDate = workItem.DueDate,
            StoryPoints = workItem.StoryPoints,
            IsArchived = workItem.IsArchived,
            CommentCount = 0,
            AttachmentCount = 0,
            ETag = workItem.ETag,
            CreatedAt = workItem.CreatedAt,
            UpdatedAt = workItem.UpdatedAt
        };
    }

    public async Task DeleteTemplateAsync(Guid templateId, CancellationToken ct)
    {
        var template = await _db.ItemTemplates
            .FirstOrDefaultAsync(t => t.Id == templateId, ct);

        if (template is null)
            return;

        _db.ItemTemplates.Remove(template);
        await _db.SaveChangesAsync(ct);
    }

    private sealed class ChecklistDefinition
    {
        public string Title { get; set; } = string.Empty;
        public double Position { get; set; }
        public List<ChecklistItemDefinition> Items { get; set; } = new();
    }

    private sealed class ChecklistItemDefinition
    {
        public string Title { get; set; } = string.Empty;
        public double Position { get; set; }
    }
}
