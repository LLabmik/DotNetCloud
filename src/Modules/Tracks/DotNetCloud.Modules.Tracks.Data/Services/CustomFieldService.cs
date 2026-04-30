using DotNetCloud.Core.DTOs;
using DotNetCloud.Core.Errors;
using DotNetCloud.Modules.Tracks.Models;
using Microsoft.EntityFrameworkCore;

namespace DotNetCloud.Modules.Tracks.Data.Services;

/// <summary>
/// Manages custom field definitions and work item field values for a product.
/// </summary>
public sealed class CustomFieldService
{
    private readonly TracksDbContext _db;

    public CustomFieldService(TracksDbContext db) => _db = db;

    // ─── Field Definitions ──────────────────────────────────────────────

    public async Task<CustomFieldDto> CreateFieldAsync(Guid productId, CreateCustomFieldDto dto, CancellationToken ct)
    {
        var maxPosition = await _db.Set<CustomField>()
            .Where(cf => cf.ProductId == productId)
            .MaxAsync(cf => (int?)cf.Position, ct) ?? -1;

        var field = new CustomField
        {
            ProductId = productId,
            Name = dto.Name,
            Type = dto.Type,
            OptionsJson = dto.OptionsJson,
            IsRequired = dto.IsRequired,
            Position = maxPosition + 1
        };

        _db.Set<CustomField>().Add(field);
        await _db.SaveChangesAsync(ct);

        return MapFieldToDto(field);
    }

    public async Task<List<CustomFieldDto>> GetFieldsAsync(Guid productId, CancellationToken ct)
    {
        var fields = await _db.Set<CustomField>()
            .Where(cf => cf.ProductId == productId)
            .OrderBy(cf => cf.Position)
            .ToListAsync(ct);

        return fields.Select(MapFieldToDto).ToList();
    }

    public async Task<CustomFieldDto> UpdateFieldAsync(Guid fieldId, UpdateCustomFieldDto dto, CancellationToken ct)
    {
        var field = await _db.Set<CustomField>()
            .FirstOrDefaultAsync(cf => cf.Id == fieldId, ct)
            ?? throw new NotFoundException("CustomField", fieldId);

        if (dto.Name is not null) field.Name = dto.Name;
        if (dto.Type.HasValue) field.Type = dto.Type.Value;
        if (dto.OptionsJson is not null) field.OptionsJson = dto.OptionsJson;
        if (dto.IsRequired.HasValue) field.IsRequired = dto.IsRequired.Value;
        field.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync(ct);
        return MapFieldToDto(field);
    }

    public async Task DeleteFieldAsync(Guid fieldId, CancellationToken ct)
    {
        var field = await _db.Set<CustomField>()
            .FirstOrDefaultAsync(cf => cf.Id == fieldId, ct)
            ?? throw new NotFoundException("CustomField", fieldId);

        _db.Set<CustomField>().Remove(field);
        await _db.SaveChangesAsync(ct);
    }

    public async Task ReorderFieldsAsync(Guid productId, List<Guid> orderedIds, CancellationToken ct)
    {
        var fields = await _db.Set<CustomField>()
            .Where(cf => cf.ProductId == productId && orderedIds.Contains(cf.Id))
            .ToListAsync(ct);

        for (int i = 0; i < orderedIds.Count; i++)
        {
            var field = fields.FirstOrDefault(f => f.Id == orderedIds[i]);
            if (field is not null)
            {
                field.Position = i;
                field.UpdatedAt = DateTime.UtcNow;
            }
        }

        await _db.SaveChangesAsync(ct);
    }

    // ─── Field Values ───────────────────────────────────────────────────

    public async Task<List<WorkItemFieldValueDto>> GetFieldValuesAsync(Guid workItemId, CancellationToken ct)
    {
        var values = await _db.Set<WorkItemFieldValue>()
            .Include(fv => fv.CustomField)
            .Where(fv => fv.WorkItemId == workItemId)
            .ToListAsync(ct);

        return values.Select(MapValueToDto).ToList();
    }

    public async Task<WorkItemFieldValueDto> SetFieldValueAsync(
        Guid workItemId, SetFieldValueDto dto, CancellationToken ct)
    {
        var field = await _db.Set<CustomField>()
            .FirstOrDefaultAsync(cf => cf.Id == dto.CustomFieldId, ct)
            ?? throw new NotFoundException("CustomField", dto.CustomFieldId);

        ValidateFieldValue(field, dto.Value);

        var existing = await _db.Set<WorkItemFieldValue>()
            .Include(fv => fv.CustomField)
            .FirstOrDefaultAsync(fv => fv.WorkItemId == workItemId
                                    && fv.CustomFieldId == dto.CustomFieldId, ct);

        if (existing is not null)
        {
            existing.Value = dto.Value;
            existing.UpdatedAt = DateTime.UtcNow;
        }
        else
        {
            existing = new WorkItemFieldValue
            {
                WorkItemId = workItemId,
                CustomFieldId = dto.CustomFieldId,
                Value = dto.Value
            };
            _db.Set<WorkItemFieldValue>().Add(existing);
        }

        await _db.SaveChangesAsync(ct);
        return MapValueToDto(existing);
    }

    public async Task BatchSetFieldValuesAsync(
        Guid workItemId, BatchSetFieldValuesDto dto, CancellationToken ct)
    {
        // Validate all fields exist and values are valid
        var fieldIds = dto.FieldValues.Select(fv => fv.CustomFieldId).ToList();
        var fields = await _db.Set<CustomField>()
            .Where(cf => fieldIds.Contains(cf.Id))
            .ToDictionaryAsync(cf => cf.Id, ct);

        foreach (var fv in dto.FieldValues)
        {
            if (!fields.TryGetValue(fv.CustomFieldId, out var field))
                throw new NotFoundException("CustomField", fv.CustomFieldId);
            ValidateFieldValue(field, fv.Value);
        }

        var existingValues = await _db.Set<WorkItemFieldValue>()
            .Where(fv => fv.WorkItemId == workItemId && fieldIds.Contains(fv.CustomFieldId))
            .ToDictionaryAsync(fv => fv.CustomFieldId, ct);

        foreach (var fv in dto.FieldValues)
        {
            if (existingValues.TryGetValue(fv.CustomFieldId, out var existing))
            {
                existing.Value = fv.Value;
                existing.UpdatedAt = DateTime.UtcNow;
            }
            else
            {
                _db.Set<WorkItemFieldValue>().Add(new WorkItemFieldValue
                {
                    WorkItemId = workItemId,
                    CustomFieldId = fv.CustomFieldId,
                    Value = fv.Value
                });
            }
        }

        await _db.SaveChangesAsync(ct);
    }

    public async Task ClearFieldValueAsync(Guid workItemId, Guid customFieldId, CancellationToken ct)
    {
        var value = await _db.Set<WorkItemFieldValue>()
            .FirstOrDefaultAsync(fv => fv.WorkItemId == workItemId
                                    && fv.CustomFieldId == customFieldId, ct);

        if (value is not null)
        {
            _db.Set<WorkItemFieldValue>().Remove(value);
            await _db.SaveChangesAsync(ct);
        }
    }

    // ─── Helpers ────────────────────────────────────────────────────────

    private static void ValidateFieldValue(CustomField field, string? value)
    {
        if (field.IsRequired && string.IsNullOrWhiteSpace(value))
            throw new ValidationException("Value", $"Custom field '{field.Name}' is required.");

        if (string.IsNullOrWhiteSpace(value)) return;

        switch (field.Type)
        {
            case CustomFieldType.Number:
                if (!decimal.TryParse(value, out _))
                    throw new ValidationException("Value", $"Custom field '{field.Name}' must be a number.");
                break;
            case CustomFieldType.Date:
                if (!DateTime.TryParse(value, out _))
                    throw new ValidationException("Value", $"Custom field '{field.Name}' must be a valid date.");
                break;
            case CustomFieldType.SingleSelect:
                ValidateSelectValue(field, value, true);
                break;
            case CustomFieldType.MultiSelect:
                ValidateSelectValue(field, value, false);
                break;
        }
    }

    private static void ValidateSelectValue(CustomField field, string value, bool singleSelect)
    {
        if (string.IsNullOrWhiteSpace(field.OptionsJson)) return;
        var options = System.Text.Json.JsonSerializer.Deserialize<string[]>(field.OptionsJson);
        if (options is null || options.Length == 0) return;

        var selectedValues = singleSelect
            ? new[] { value }
            : value.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        foreach (var sv in selectedValues)
        {
            if (!options.Contains(sv, StringComparer.OrdinalIgnoreCase))
                throw new ValidationException("Value",
                    $"'{sv}' is not a valid option for field '{field.Name}'. Valid options: {string.Join(", ", options)}");
        }
    }

    private static CustomFieldDto MapFieldToDto(CustomField f) => new()
    {
        Id = f.Id,
        ProductId = f.ProductId,
        Name = f.Name,
        Type = f.Type,
        OptionsJson = f.OptionsJson,
        IsRequired = f.IsRequired,
        Position = f.Position,
        CreatedAt = f.CreatedAt,
        UpdatedAt = f.UpdatedAt
    };

    private static WorkItemFieldValueDto MapValueToDto(WorkItemFieldValue fv) => new()
    {
        Id = fv.Id,
        WorkItemId = fv.WorkItemId,
        CustomFieldId = fv.CustomFieldId,
        CustomFieldName = fv.CustomField?.Name,
        FieldType = fv.CustomField?.Type ?? CustomFieldType.Text,
        Value = fv.Value,
        UpdatedAt = fv.UpdatedAt
    };
}
