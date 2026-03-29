using System.Text.Json;
using DotNetCloud.Core.Authorization;
using DotNetCloud.Core.DTOs;
using DotNetCloud.Core.Errors;
using DotNetCloud.Modules.Tracks.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace DotNetCloud.Modules.Tracks.Data.Services;

/// <summary>
/// Service for managing board templates — built-in and user-created.
/// </summary>
public sealed class BoardTemplateService
{
    private readonly TracksDbContext _db;
    private readonly BoardService _boardService;
    private readonly ListService _listService;
    private readonly LabelService _labelService;
    private readonly ILogger<BoardTemplateService> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="BoardTemplateService"/> class.
    /// </summary>
    public BoardTemplateService(TracksDbContext db, BoardService boardService, ListService listService, LabelService labelService, ILogger<BoardTemplateService> logger)
    {
        _db = db;
        _boardService = boardService;
        _listService = listService;
        _labelService = labelService;
        _logger = logger;
    }

    /// <summary>
    /// Lists all available board templates (built-in + user-created).
    /// </summary>
    public async Task<IReadOnlyList<BoardTemplateDto>> ListTemplatesAsync(CallerContext caller, CancellationToken cancellationToken = default)
    {
        var templates = await _db.BoardTemplates
            .AsNoTracking()
            .Where(t => t.IsBuiltIn || t.CreatedByUserId == caller.UserId)
            .OrderBy(t => t.IsBuiltIn ? 0 : 1)
            .ThenBy(t => t.Name)
            .ToListAsync(cancellationToken);

        return templates.Select(MapToDto).ToList();
    }

    /// <summary>
    /// Gets a template by ID.
    /// </summary>
    public async Task<BoardTemplateDto?> GetTemplateAsync(Guid templateId, CallerContext caller, CancellationToken cancellationToken = default)
    {
        var template = await _db.BoardTemplates
            .AsNoTracking()
            .FirstOrDefaultAsync(t => t.Id == templateId && (t.IsBuiltIn || t.CreatedByUserId == caller.UserId), cancellationToken);

        return template is null ? null : MapToDto(template);
    }

    /// <summary>
    /// Creates a board from a template. Creates the board, then applies the template's lists and labels.
    /// </summary>
    public async Task<BoardDto> CreateBoardFromTemplateAsync(Guid templateId, CreateBoardFromTemplateDto dto, CallerContext caller, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(dto);

        var template = await _db.BoardTemplates
            .FirstOrDefaultAsync(t => t.Id == templateId && (t.IsBuiltIn || t.CreatedByUserId == caller.UserId), cancellationToken)
            ?? throw new ValidationException(ErrorCodes.BoardTemplateNotFound, "Board template not found.");

        // Create the base board
        var createDto = new CreateBoardDto
        {
            Title = dto.Title,
            Description = dto.Description,
            TeamId = dto.TeamId,
            Color = dto.Color
        };
        var board = await _boardService.CreateBoardAsync(createDto, caller, cancellationToken);

        // Parse template definition
        var definition = JsonSerializer.Deserialize<TemplateDefinition>(template.DefinitionJson, _jsonOptions);
        if (definition is null)
        {
            _logger.LogWarning("Template {TemplateId} has invalid definition JSON", templateId);
            return board;
        }

        // Create labels from template
        if (definition.Labels is not null)
        {
            foreach (var labelDef in definition.Labels)
            {
                await _labelService.CreateLabelAsync(board.Id, new CreateLabelDto
                {
                    Title = labelDef.Title,
                    Color = labelDef.Color ?? "#6B7280"
                }, caller, cancellationToken);
            }
        }

        // Create lists from template
        if (definition.Lists is not null)
        {
            foreach (var listDef in definition.Lists)
            {
                await _listService.CreateListAsync(board.Id, new CreateBoardListDto
                {
                    Title = listDef.Title,
                    Color = listDef.Color,
                    CardLimit = listDef.CardLimit
                }, caller, cancellationToken);
            }
        }

        _logger.LogInformation("Board {BoardId} created from template {TemplateId} '{TemplateName}' by user {UserId}",
            board.Id, templateId, template.Name, caller.UserId);

        // Return fresh board with all the new lists and labels
        return await _boardService.GetBoardAsync(board.Id, caller, cancellationToken)
            ?? board;
    }

    /// <summary>
    /// Saves an existing board as a template.
    /// </summary>
    public async Task<BoardTemplateDto> SaveBoardAsTemplateAsync(Guid boardId, SaveBoardAsTemplateDto dto, CallerContext caller, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(dto);

        await _boardService.EnsureBoardMemberAsync(boardId, caller.UserId, cancellationToken);

        var board = await _db.Boards
            .AsNoTracking()
            .Include(b => b.Lists.OrderBy(l => l.Position))
            .Include(b => b.Labels)
            .FirstOrDefaultAsync(b => b.Id == boardId && !b.IsDeleted, cancellationToken)
            ?? throw new ValidationException(ErrorCodes.BoardNotFound, "Board not found.");

        var definition = new TemplateDefinition
        {
            Lists = board.Lists.Select(l => new TemplateListDefinition
            {
                Title = l.Title,
                Color = l.Color,
                CardLimit = l.CardLimit
            }).ToList(),
            Labels = board.Labels.Select(l => new TemplateLabelDefinition
            {
                Title = l.Title,
                Color = l.Color
            }).ToList()
        };

        var template = new BoardTemplate
        {
            Name = dto.Name,
            Description = dto.Description,
            Category = dto.Category,
            IsBuiltIn = false,
            CreatedByUserId = caller.UserId,
            DefinitionJson = JsonSerializer.Serialize(definition, _jsonOptions)
        };

        _db.BoardTemplates.Add(template);
        await _db.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Board {BoardId} saved as template {TemplateId} '{Name}' by user {UserId}",
            boardId, template.Id, dto.Name, caller.UserId);

        return MapToDto(template);
    }

    /// <summary>
    /// Deletes a user-created template. Built-in templates cannot be deleted.
    /// </summary>
    public async Task DeleteTemplateAsync(Guid templateId, CallerContext caller, CancellationToken cancellationToken = default)
    {
        var template = await _db.BoardTemplates
            .FirstOrDefaultAsync(t => t.Id == templateId, cancellationToken)
            ?? throw new ValidationException(ErrorCodes.BoardTemplateNotFound, "Board template not found.");

        if (template.IsBuiltIn)
            throw new ValidationException(ErrorCodes.InvalidTemplateDefinition, "Built-in templates cannot be deleted.");

        if (template.CreatedByUserId != caller.UserId)
            throw new ValidationException(ErrorCodes.BoardTemplateNotFound, "Template not found.");

        _db.BoardTemplates.Remove(template);
        await _db.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Template {TemplateId} '{Name}' deleted by user {UserId}", templateId, template.Name, caller.UserId);
    }

    /// <summary>
    /// Seeds built-in templates if they don't already exist.
    /// </summary>
    public async Task SeedBuiltInTemplatesAsync(CancellationToken cancellationToken = default)
    {
        var hasBuiltIn = await _db.BoardTemplates.AnyAsync(t => t.IsBuiltIn, cancellationToken);
        if (hasBuiltIn)
            return;

        var templates = GetBuiltInTemplates();
        _db.BoardTemplates.AddRange(templates);
        await _db.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Seeded {Count} built-in board templates", templates.Count);
    }

    internal static IReadOnlyList<BoardTemplate> GetBuiltInTemplates()
    {
        return
        [
            new BoardTemplate
            {
                Name = "Kanban",
                Description = "Simple kanban board with To Do, In Progress, and Done columns.",
                Category = "General",
                IsBuiltIn = true,
                DefinitionJson = JsonSerializer.Serialize(new TemplateDefinition
                {
                    Lists =
                    [
                        new() { Title = "Backlog" },
                        new() { Title = "To Do" },
                        new() { Title = "In Progress", CardLimit = 5 },
                        new() { Title = "Review" },
                        new() { Title = "Done" }
                    ],
                    Labels =
                    [
                        new() { Title = "Bug", Color = "#EF4444" },
                        new() { Title = "Feature", Color = "#3B82F6" },
                        new() { Title = "Improvement", Color = "#10B981" },
                        new() { Title = "Documentation", Color = "#8B5CF6" }
                    ]
                }, _jsonOptions)
            },
            new BoardTemplate
            {
                Name = "Scrum",
                Description = "Sprint-based board with backlog, sprint columns, and review stages.",
                Category = "Development",
                IsBuiltIn = true,
                DefinitionJson = JsonSerializer.Serialize(new TemplateDefinition
                {
                    Lists =
                    [
                        new() { Title = "Product Backlog" },
                        new() { Title = "Sprint Backlog" },
                        new() { Title = "In Development", CardLimit = 3 },
                        new() { Title = "Code Review" },
                        new() { Title = "QA Testing" },
                        new() { Title = "Done" }
                    ],
                    Labels =
                    [
                        new() { Title = "Story", Color = "#3B82F6" },
                        new() { Title = "Bug", Color = "#EF4444" },
                        new() { Title = "Task", Color = "#F59E0B" },
                        new() { Title = "Spike", Color = "#8B5CF6" },
                        new() { Title = "Epic", Color = "#EC4899" }
                    ]
                }, _jsonOptions)
            },
            new BoardTemplate
            {
                Name = "Bug Tracking",
                Description = "Issue tracking board with triage, severity labels, and resolution stages.",
                Category = "Development",
                IsBuiltIn = true,
                DefinitionJson = JsonSerializer.Serialize(new TemplateDefinition
                {
                    Lists =
                    [
                        new() { Title = "New / Triage" },
                        new() { Title = "Accepted" },
                        new() { Title = "In Progress", CardLimit = 4 },
                        new() { Title = "Fixed" },
                        new() { Title = "Verified" },
                        new() { Title = "Closed" }
                    ],
                    Labels =
                    [
                        new() { Title = "Critical", Color = "#DC2626" },
                        new() { Title = "High", Color = "#EA580C" },
                        new() { Title = "Medium", Color = "#F59E0B" },
                        new() { Title = "Low", Color = "#22C55E" },
                        new() { Title = "Regression", Color = "#7C3AED" },
                        new() { Title = "UI", Color = "#06B6D4" }
                    ]
                }, _jsonOptions)
            },
            new BoardTemplate
            {
                Name = "Personal TODO",
                Description = "Simple personal task board for everyday use.",
                Category = "Personal",
                IsBuiltIn = true,
                DefinitionJson = JsonSerializer.Serialize(new TemplateDefinition
                {
                    Lists =
                    [
                        new() { Title = "Ideas" },
                        new() { Title = "To Do" },
                        new() { Title = "Doing", CardLimit = 3 },
                        new() { Title = "Done" }
                    ],
                    Labels =
                    [
                        new() { Title = "Urgent", Color = "#EF4444" },
                        new() { Title = "Important", Color = "#F59E0B" },
                        new() { Title = "Quick Win", Color = "#10B981" }
                    ]
                }, _jsonOptions)
            }
        ];
    }

    private static BoardTemplateDto MapToDto(BoardTemplate t) => new()
    {
        Id = t.Id,
        Name = t.Name,
        Description = t.Description,
        Category = t.Category,
        IsBuiltIn = t.IsBuiltIn,
        CreatedByUserId = t.CreatedByUserId,
        DefinitionJson = t.DefinitionJson,
        CreatedAt = t.CreatedAt
    };

    private static readonly JsonSerializerOptions _jsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false
    };
}

// ─── Template Definition Models (JSON-serialized) ─────────

/// <summary>
/// JSON-serialized board template definition.
/// </summary>
internal sealed class TemplateDefinition
{
    /// <summary>Lists to create on the board.</summary>
    public List<TemplateListDefinition>? Lists { get; set; }

    /// <summary>Labels to create on the board.</summary>
    public List<TemplateLabelDefinition>? Labels { get; set; }
}

/// <summary>
/// A list definition within a board template.
/// </summary>
internal sealed class TemplateListDefinition
{
    /// <summary>List title.</summary>
    public required string Title { get; set; }

    /// <summary>Optional list color.</summary>
    public string? Color { get; set; }

    /// <summary>Optional WIP limit.</summary>
    public int? CardLimit { get; set; }
}

/// <summary>
/// A label definition within a board template.
/// </summary>
internal sealed class TemplateLabelDefinition
{
    /// <summary>Label title.</summary>
    public required string Title { get; set; }

    /// <summary>Label color (hex).</summary>
    public string? Color { get; set; }
}
