using DotNetCloud.Core.Authorization;
using DotNetCloud.Core.DTOs;
using DotNetCloud.Core.Errors;
using DotNetCloud.Modules.Tracks.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace DotNetCloud.Modules.Tracks.Data.Services;

/// <summary>
/// Service for managing card file attachments and URL links.
/// </summary>
public sealed class AttachmentService
{
    private readonly TracksDbContext _db;
    private readonly BoardService _boardService;
    private readonly ActivityService _activityService;
    private readonly ILogger<AttachmentService> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="AttachmentService"/> class.
    /// </summary>
    public AttachmentService(TracksDbContext db, BoardService boardService, ActivityService activityService, ILogger<AttachmentService> logger)
    {
        _db = db;
        _boardService = boardService;
        _activityService = activityService;
        _logger = logger;
    }

    /// <summary>
    /// Adds an attachment to a card. Requires Member role or higher.
    /// </summary>
    public async Task<CardAttachmentDto> AddAttachmentAsync(Guid cardId, string fileName, Guid? fileNodeId, string? url, CallerContext caller, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(fileName);

        var card = await _db.Cards
            .Include(c => c.Swimlane)
            .FirstOrDefaultAsync(c => c.Id == cardId && !c.IsDeleted, cancellationToken)
            ?? throw new ValidationException(ErrorCodes.CardNotFound, "Card not found.");

        await _boardService.EnsureBoardRoleAsync(card.Swimlane!.BoardId, caller.UserId, BoardMemberRole.Member, cancellationToken);

        var attachment = new CardAttachment
        {
            CardId = cardId,
            FileName = fileName,
            FileNodeId = fileNodeId,
            Url = url,
            UploadedByUserId = caller.UserId
        };

        _db.CardAttachments.Add(attachment);
        card.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Attachment {AttachmentId} '{FileName}' added to card {CardId} by user {UserId}",
            attachment.Id, fileName, cardId, caller.UserId);

        await _activityService.LogAsync(card.Swimlane.BoardId, caller.UserId, "attachment.added", "CardAttachment", attachment.Id,
            $"{{\"fileName\":\"{fileName}\",\"cardId\":\"{cardId}\"}}", cancellationToken);

        return MapToDto(attachment);
    }

    /// <summary>
    /// Gets all attachments for a card.
    /// </summary>
    public async Task<IReadOnlyList<CardAttachmentDto>> GetAttachmentsAsync(Guid cardId, CallerContext caller, CancellationToken cancellationToken = default)
    {
        var card = await _db.Cards
            .AsNoTracking()
            .Include(c => c.Swimlane)
            .FirstOrDefaultAsync(c => c.Id == cardId && !c.IsDeleted, cancellationToken)
            ?? throw new ValidationException(ErrorCodes.CardNotFound, "Card not found.");

        await _boardService.EnsureBoardMemberAsync(card.Swimlane!.BoardId, caller.UserId, cancellationToken);

        var attachments = await _db.CardAttachments
            .AsNoTracking()
            .Where(a => a.CardId == cardId)
            .OrderByDescending(a => a.CreatedAt)
            .ToListAsync(cancellationToken);

        return attachments.Select(MapToDto).ToList();
    }

    /// <summary>
    /// Removes an attachment from a card. Requires Member role or higher.
    /// </summary>
    public async Task RemoveAttachmentAsync(Guid attachmentId, CallerContext caller, CancellationToken cancellationToken = default)
    {
        var attachment = await _db.CardAttachments
            .Include(a => a.Card).ThenInclude(c => c!.Swimlane)
            .FirstOrDefaultAsync(a => a.Id == attachmentId, cancellationToken)
            ?? throw new ValidationException(ErrorCodes.CardNotFound, "Attachment not found.");

        await _boardService.EnsureBoardRoleAsync(attachment.Card!.Swimlane!.BoardId, caller.UserId, BoardMemberRole.Member, cancellationToken);

        _db.CardAttachments.Remove(attachment);
        attachment.Card.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Attachment {AttachmentId} removed from card {CardId} by user {UserId}",
            attachmentId, attachment.CardId, caller.UserId);

        await _activityService.LogAsync(attachment.Card.Swimlane.BoardId, caller.UserId, "attachment.removed", "CardAttachment", attachmentId,
            $"{{\"fileName\":\"{attachment.FileName}\"}}", cancellationToken);
    }

    private static CardAttachmentDto MapToDto(CardAttachment a) => new()
    {
        Id = a.Id,
        CardId = a.CardId,
        FileNodeId = a.FileNodeId,
        FileName = a.FileName,
        Url = a.Url,
        AddedByUserId = a.UploadedByUserId,
        CreatedAt = a.CreatedAt
    };
}
