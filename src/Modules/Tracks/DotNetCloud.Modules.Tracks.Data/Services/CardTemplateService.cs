using System.Text.Json;
using DotNetCloud.Core.Authorization;
using DotNetCloud.Core.DTOs;
using DotNetCloud.Core.Errors;
using DotNetCloud.Modules.Tracks.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace DotNetCloud.Modules.Tracks.Data.Services;

/// <summary>
/// Service for managing card templates on a board.
/// </summary>
public sealed class CardTemplateService
{
    private readonly TracksDbContext _db;
    private readonly BoardService _boardService;
    private readonly CardService _cardService;
    private readonly ILogger<CardTemplateService> _logger;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false
    };

    /// <summary>
    /// Initializes a new instance of the <see cref="CardTemplateService"/> class.
    /// </summary>
    public CardTemplateService(TracksDbContext db, BoardService boardService, CardService cardService, ILogger<CardTemplateService> logger)
    {
        _db = db;
        _boardService = boardService;
        _cardService = cardService;
        _logger = logger;
    }

    /// <summary>
    /// Lists all card templates for a board.
    /// </summary>
    public async Task<IReadOnlyList<CardTemplateDto>> ListTemplatesAsync(Guid boardId, CallerContext caller, CancellationToken cancellationToken = default)
    {
        await _boardService.EnsureBoardMemberAsync(boardId, caller.UserId, cancellationToken);

        var templates = await _db.CardTemplates
            .AsNoTracking()
            .Where(t => t.BoardId == boardId)
            .OrderBy(t => t.Name)
            .ToListAsync(cancellationToken);

        return templates.Select(MapToDto).ToList();
    }

    /// <summary>
    /// Gets a card template by ID.
    /// </summary>
    public async Task<CardTemplateDto?> GetTemplateAsync(Guid templateId, CallerContext caller, CancellationToken cancellationToken = default)
    {
        var template = await _db.CardTemplates
            .AsNoTracking()
            .FirstOrDefaultAsync(t => t.Id == templateId, cancellationToken);

        if (template is null)
            return null;

        await _boardService.EnsureBoardMemberAsync(template.BoardId, caller.UserId, cancellationToken);

        return MapToDto(template);
    }

    /// <summary>
    /// Saves an existing card as a template on its board.
    /// </summary>
    public async Task<CardTemplateDto> SaveCardAsTemplateAsync(Guid cardId, SaveCardAsTemplateDto dto, CallerContext caller, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(dto);

        var card = await _db.Cards
            .AsNoTracking()
            .Include(c => c.List)
            .Include(c => c.CardLabels)
            .Include(c => c.Checklists).ThenInclude(cl => cl.Items)
            .FirstOrDefaultAsync(c => c.Id == cardId && !c.IsDeleted, cancellationToken)
            ?? throw new ValidationException(ErrorCodes.CardNotFound, "Card not found.");

        await _boardService.EnsureBoardRoleAsync(card.List!.BoardId, caller.UserId, BoardMemberRole.Member, cancellationToken);

        var labelIds = dto.IncludeLabels
            ? card.CardLabels.Select(cl => cl.LabelId).ToList()
            : new List<Guid>();

        string? checklistsJson = null;
        if (dto.IncludeChecklists && card.Checklists.Count > 0)
        {
            var checklistDefs = card.Checklists.OrderBy(c => c.Position).Select(cl => new ChecklistDefinition
            {
                Title = cl.Title,
                Items = cl.Items.OrderBy(i => i.Position).Select(i => i.Title).ToList()
            }).ToList();
            checklistsJson = JsonSerializer.Serialize(checklistDefs, JsonOptions);
        }

        var template = new CardTemplate
        {
            BoardId = card.List.BoardId,
            Name = dto.Name,
            TitlePattern = card.Title,
            Description = card.Description,
            Priority = card.Priority,
            LabelIdsJson = labelIds.Count > 0 ? JsonSerializer.Serialize(labelIds, JsonOptions) : null,
            ChecklistsJson = checklistsJson,
            CreatedByUserId = caller.UserId
        };

        _db.CardTemplates.Add(template);
        await _db.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Card {CardId} saved as template {TemplateId} '{Name}' by user {UserId}",
            cardId, template.Id, dto.Name, caller.UserId);

        return MapToDto(template);
    }

    /// <summary>
    /// Creates a new card in a list from a card template.
    /// </summary>
    public async Task<CardDto> CreateCardFromTemplateAsync(Guid templateId, Guid listId, CreateCardFromTemplateDto dto, CallerContext caller, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(dto);

        var template = await _db.CardTemplates
            .AsNoTracking()
            .FirstOrDefaultAsync(t => t.Id == templateId, cancellationToken)
            ?? throw new ValidationException(ErrorCodes.CardTemplateNotFound, "Card template not found.");

        // Resolve label IDs that still exist on the board
        var validLabelIds = new List<Guid>();
        if (!string.IsNullOrEmpty(template.LabelIdsJson))
        {
            var storedIds = JsonSerializer.Deserialize<List<Guid>>(template.LabelIdsJson, JsonOptions) ?? [];
            var existingIds = await _db.Labels
                .Where(l => storedIds.Contains(l.Id))
                .Select(l => l.Id)
                .ToListAsync(cancellationToken);
            validLabelIds.AddRange(existingIds);
        }

        var title = dto.Title ?? template.TitlePattern ?? template.Name;

        var createCardDto = new CreateCardDto
        {
            Title = title,
            Description = template.Description,
            Priority = template.Priority,
            LabelIds = validLabelIds,
            AssigneeIds = []
        };

        var card = await _cardService.CreateCardAsync(listId, createCardDto, caller, cancellationToken);

        // Apply checklist definitions
        if (!string.IsNullOrEmpty(template.ChecklistsJson))
        {
            var checklistDefs = JsonSerializer.Deserialize<List<ChecklistDefinition>>(template.ChecklistsJson, JsonOptions);
            if (checklistDefs is not null)
            {
                foreach (var clDef in checklistDefs)
                {
                    var checklist = new CardChecklist
                    {
                        CardId = card.Id,
                        Title = clDef.Title,
                        Position = 1000.0
                    };
                    _db.CardChecklists.Add(checklist);
                    await _db.SaveChangesAsync(cancellationToken);

                    var pos = 1000.0;
                    foreach (var itemTitle in clDef.Items)
                    {
                        _db.ChecklistItems.Add(new ChecklistItem
                        {
                            ChecklistId = checklist.Id,
                            Title = itemTitle,
                            Position = pos
                        });
                        pos += 1000.0;
                    }
                    await _db.SaveChangesAsync(cancellationToken);
                }
            }
        }

        _logger.LogInformation("Card {CardId} created from template {TemplateId} in list {ListId} by user {UserId}",
            card.Id, templateId, listId, caller.UserId);

        // Return refreshed card with checklists
        return await _cardService.GetCardAsync(card.Id, caller, cancellationToken) ?? card;
    }

    /// <summary>
    /// Deletes a card template. Only the creator can delete.
    /// </summary>
    public async Task DeleteTemplateAsync(Guid templateId, CallerContext caller, CancellationToken cancellationToken = default)
    {
        var template = await _db.CardTemplates
            .FirstOrDefaultAsync(t => t.Id == templateId, cancellationToken)
            ?? throw new ValidationException(ErrorCodes.CardTemplateNotFound, "Card template not found.");

        if (template.CreatedByUserId != caller.UserId)
        {
            // Board admin/owner can also delete
            await _boardService.EnsureBoardRoleAsync(template.BoardId, caller.UserId, BoardMemberRole.Admin, cancellationToken);
        }

        _db.CardTemplates.Remove(template);
        await _db.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Card template {TemplateId} '{Name}' deleted by user {UserId}", templateId, template.Name, caller.UserId);
    }

    private static CardTemplateDto MapToDto(CardTemplate t)
    {
        var labelIds = !string.IsNullOrEmpty(t.LabelIdsJson)
            ? JsonSerializer.Deserialize<List<Guid>>(t.LabelIdsJson, JsonOptions) ?? []
            : (IReadOnlyList<Guid>)[];

        return new CardTemplateDto
        {
            Id = t.Id,
            BoardId = t.BoardId,
            Name = t.Name,
            TitlePattern = t.TitlePattern,
            Description = t.Description,
            Priority = t.Priority,
            LabelIds = labelIds,
            ChecklistsJson = t.ChecklistsJson,
            CreatedByUserId = t.CreatedByUserId,
            CreatedAt = t.CreatedAt
        };
    }
}

/// <summary>
/// JSON-serialized checklist definition for card templates.
/// </summary>
internal sealed class ChecklistDefinition
{
    /// <summary>Checklist title.</summary>
    public required string Title { get; set; }

    /// <summary>Item titles within the checklist.</summary>
    public List<string> Items { get; set; } = [];
}
