using System.Text.Json;
using DotNetCloud.Core.DTOs;
using DotNetCloud.Modules.Tracks.Models;
using Microsoft.EntityFrameworkCore;

namespace DotNetCloud.Modules.Tracks.Data.Services;

public sealed class ProductTemplateService
{
    private readonly TracksDbContext _db;

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public ProductTemplateService(TracksDbContext db)
    {
        _db = db;
    }

    public async Task<List<ProductTemplateDto>> GetTemplatesAsync(string? category, CancellationToken ct)
    {
        var query = _db.ProductTemplates.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(category))
        {
            query = query.Where(t => t.Category == category);
        }

        return await query
            .OrderBy(t => t.Name)
            .Select(t => new ProductTemplateDto
            {
                Id = t.Id,
                Name = t.Name,
                Description = t.Description,
                Category = t.Category,
                IsBuiltIn = t.IsBuiltIn,
                CreatedByUserId = t.CreatedByUserId,
                DefinitionJson = t.DefinitionJson,
                CreatedAt = t.CreatedAt,
                UpdatedAt = t.UpdatedAt
            })
            .ToListAsync(ct);
    }

    public async Task<ProductTemplateDto> GetTemplateAsync(Guid templateId, CancellationToken ct)
    {
        var template = await _db.ProductTemplates
            .AsNoTracking()
            .FirstOrDefaultAsync(t => t.Id == templateId, ct);

        if (template is null)
            throw new InvalidOperationException($"ProductTemplate with ID {templateId} not found.");

        return new ProductTemplateDto
        {
            Id = template.Id,
            Name = template.Name,
            Description = template.Description,
            Category = template.Category,
            IsBuiltIn = template.IsBuiltIn,
            CreatedByUserId = template.CreatedByUserId,
            DefinitionJson = template.DefinitionJson,
            CreatedAt = template.CreatedAt,
            UpdatedAt = template.UpdatedAt
        };
    }

    public async Task<ProductTemplateDto> SaveProductAsTemplateAsync(Guid productId, Guid createdByUserId, SaveProductAsTemplateDto dto, CancellationToken ct)
    {
        var swimlanes = await _db.Swimlanes
            .Where(s => s.ContainerType == SwimlaneContainerType.Product && s.ContainerId == productId && !s.IsArchived)
            .OrderBy(s => s.Position)
            .Select(s => new
            {
                s.Title,
                s.Color,
                s.Position,
                s.IsDone
            })
            .ToListAsync(ct);

        var definition = new
        {
            swimlanes = swimlanes.Select(s => new
            {
                title = s.Title,
                color = s.Color,
                position = s.Position,
                isDone = s.IsDone
            })
        };

        var definitionJson = JsonSerializer.Serialize(definition, JsonOptions);

        var template = new ProductTemplate
        {
            Name = dto.Name,
            Description = dto.Description,
            Category = dto.Category,
            IsBuiltIn = false,
            CreatedByUserId = createdByUserId,
            DefinitionJson = definitionJson,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        _db.ProductTemplates.Add(template);
        await _db.SaveChangesAsync(ct);

        return new ProductTemplateDto
        {
            Id = template.Id,
            Name = template.Name,
            Description = template.Description,
            Category = template.Category,
            IsBuiltIn = template.IsBuiltIn,
            CreatedByUserId = template.CreatedByUserId,
            DefinitionJson = template.DefinitionJson,
            CreatedAt = template.CreatedAt,
            UpdatedAt = template.UpdatedAt
        };
    }

    public async Task<ProductDto> CreateProductFromTemplateAsync(Guid templateId, Guid ownerId, CreateProductFromTemplateDto dto, CancellationToken ct)
    {
        var template = await _db.ProductTemplates
            .AsNoTracking()
            .FirstOrDefaultAsync(t => t.Id == templateId, ct);

        if (template is null)
            throw new InvalidOperationException($"ProductTemplate with ID {templateId} not found.");

        var now = DateTime.UtcNow;
        var etag = Guid.NewGuid().ToString("N");

        var product = new Product
        {
            OrganizationId = dto.OrganizationId,
            Name = dto.Name,
            Description = dto.Description,
            Color = dto.Color,
            OwnerId = ownerId,
            SubItemsEnabled = false,
            IsArchived = false,
            ETag = etag,
            CreatedAt = now,
            UpdatedAt = now
        };

        _db.Products.Add(product);

        // Parse the template definition and create swimlanes
        SwimlaneDefinition? definition = null;
        if (!string.IsNullOrWhiteSpace(template.DefinitionJson))
        {
            try
            {
                definition = JsonSerializer.Deserialize<SwimlaneDefinition>(template.DefinitionJson, JsonOptions);
            }
            catch (JsonException)
            {
                // If parsing fails, create a single default swimlane
            }
        }

        if (definition?.Swimlanes is { Count: > 0 })
        {
            foreach (var sl in definition.Swimlanes)
            {
                var swimlane = new Swimlane
                {
                    ContainerType = SwimlaneContainerType.Product,
                    ContainerId = product.Id,
                    Title = sl.Title,
                    Color = sl.Color,
                    Position = sl.Position,
                    IsDone = sl.IsDone,
                    CreatedAt = now,
                    UpdatedAt = now
                };
                _db.Swimlanes.Add(swimlane);
            }
        }
        else
        {
            // Default swimlane
            var defaultSwimlane = new Swimlane
            {
                ContainerType = SwimlaneContainerType.Product,
                ContainerId = product.Id,
                Title = "To Do",
                Position = 1000,
                IsDone = false,
                CreatedAt = now,
                UpdatedAt = now
            };
            _db.Swimlanes.Add(defaultSwimlane);
        }

        await _db.SaveChangesAsync(ct);

        var swimlaneCount = await _db.Swimlanes
            .CountAsync(s => s.ContainerType == SwimlaneContainerType.Product && s.ContainerId == product.Id, ct);

        return new ProductDto
        {
            Id = product.Id,
            OrganizationId = product.OrganizationId,
            Name = product.Name,
            Description = product.Description,
            Color = product.Color,
            OwnerId = product.OwnerId,
            SubItemsEnabled = product.SubItemsEnabled,
            IsArchived = product.IsArchived,
            SwimlaneCount = swimlaneCount,
            EpicCount = 0,
            MemberCount = 0,
            LabelCount = 0,
            ETag = product.ETag,
            CreatedAt = product.CreatedAt,
            UpdatedAt = product.UpdatedAt
        };
    }

    public async Task DeleteTemplateAsync(Guid templateId, CancellationToken ct)
    {
        var template = await _db.ProductTemplates
            .FirstOrDefaultAsync(t => t.Id == templateId, ct);

        if (template is null)
            return;

        if (template.IsBuiltIn)
            throw new InvalidOperationException("Cannot delete built-in templates.");

        _db.ProductTemplates.Remove(template);
        await _db.SaveChangesAsync(ct);
    }

    private sealed class SwimlaneDefinition
    {
        public List<SwimlaneEntry> Swimlanes { get; set; } = new();
    }

    private sealed class SwimlaneEntry
    {
        public string Title { get; set; } = string.Empty;
        public string? Color { get; set; }
        public double Position { get; set; }
        public bool IsDone { get; set; }
    }
}
