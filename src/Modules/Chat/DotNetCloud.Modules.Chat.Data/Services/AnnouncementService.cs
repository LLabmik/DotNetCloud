using DotNetCloud.Core.Authorization;
using DotNetCloud.Modules.Chat.DTOs;
using DotNetCloud.Modules.Chat.Models;
using DotNetCloud.Modules.Chat.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace DotNetCloud.Modules.Chat.Data.Services;

/// <summary>
/// Manages organization-wide announcements with CRUD and acknowledgement tracking.
/// </summary>
internal sealed class AnnouncementService : IAnnouncementService
{
    private readonly ChatDbContext _db;
    private readonly ILogger<AnnouncementService> _logger;

    public AnnouncementService(ChatDbContext db, ILogger<AnnouncementService> logger)
    {
        _db = db;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<AnnouncementDto> CreateAsync(CreateAnnouncementDto dto, CallerContext caller, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(dto);

        if (string.IsNullOrWhiteSpace(dto.Title))
            throw new ArgumentException("Title is required.", nameof(dto));

        if (string.IsNullOrWhiteSpace(dto.Content))
            throw new ArgumentException("Content is required.", nameof(dto));

        Enum.TryParse<AnnouncementPriority>(dto.Priority, ignoreCase: true, out var priority);

        var announcement = new Announcement
        {
            OrganizationId = dto.OrganizationId,
            AuthorUserId = caller.UserId,
            Title = dto.Title,
            Content = dto.Content,
            Priority = priority,
            ExpiresAt = dto.ExpiresAt,
            RequiresAcknowledgement = dto.RequiresAcknowledgement
        };

        _db.Announcements.Add(announcement);
        await _db.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Announcement {Id} '{Title}' created by {UserId}", announcement.Id, announcement.Title, caller.UserId);

        return ToDto(announcement, 0);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<AnnouncementDto>> ListAsync(CallerContext caller, CancellationToken cancellationToken = default)
    {
        var announcements = await _db.Announcements
            .AsNoTracking()
            .OrderByDescending(a => a.IsPinned)
            .ThenByDescending(a => a.PublishedAt)
            .ToListAsync(cancellationToken);

        var result = new List<AnnouncementDto>(announcements.Count);
        foreach (var a in announcements)
        {
            var ackCount = await _db.AnnouncementAcknowledgements
                .CountAsync(ack => ack.AnnouncementId == a.Id, cancellationToken);
            result.Add(ToDto(a, ackCount));
        }

        return result;
    }

    /// <inheritdoc />
    public async Task<AnnouncementDto?> GetAsync(Guid id, CallerContext caller, CancellationToken cancellationToken = default)
    {
        var announcement = await _db.Announcements
            .AsNoTracking()
            .FirstOrDefaultAsync(a => a.Id == id, cancellationToken);

        if (announcement is null)
            return null;

        var ackCount = await _db.AnnouncementAcknowledgements
            .CountAsync(ack => ack.AnnouncementId == id, cancellationToken);

        return ToDto(announcement, ackCount);
    }

    /// <inheritdoc />
    public async Task UpdateAsync(Guid id, UpdateAnnouncementDto dto, CallerContext caller, CancellationToken cancellationToken = default)
    {
        var announcement = await _db.Announcements.FindAsync([id], cancellationToken)
            ?? throw new InvalidOperationException($"Announcement {id} not found.");

        if (caller.Type != CallerType.System && announcement.AuthorUserId != caller.UserId)
            throw new UnauthorizedAccessException($"Only the author can update announcement {id}.");

        if (dto.Title is not null) announcement.Title = dto.Title;
        if (dto.Content is not null) announcement.Content = dto.Content;
        if (dto.Priority is not null && Enum.TryParse<AnnouncementPriority>(dto.Priority, ignoreCase: true, out var p))
            announcement.Priority = p;
        if (dto.ExpiresAt.HasValue) announcement.ExpiresAt = dto.ExpiresAt;
        if (dto.IsPinned.HasValue) announcement.IsPinned = dto.IsPinned.Value;

        await _db.SaveChangesAsync(cancellationToken);
    }

    /// <inheritdoc />
    public async Task DeleteAsync(Guid id, CallerContext caller, CancellationToken cancellationToken = default)
    {
        var announcement = await _db.Announcements.FindAsync([id], cancellationToken)
            ?? throw new InvalidOperationException($"Announcement {id} not found.");

        if (caller.Type != CallerType.System && announcement.AuthorUserId != caller.UserId)
            throw new UnauthorizedAccessException($"Only the author can delete announcement {id}.");

        announcement.IsDeleted = true;
        announcement.DeletedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Announcement {Id} deleted by {UserId}", id, caller.UserId);
    }

    /// <inheritdoc />
    public async Task AcknowledgeAsync(Guid id, CallerContext caller, CancellationToken cancellationToken = default)
    {
        var exists = await _db.AnnouncementAcknowledgements
            .AnyAsync(a => a.AnnouncementId == id && a.UserId == caller.UserId, cancellationToken);

        if (exists)
            return;

        _db.AnnouncementAcknowledgements.Add(new AnnouncementAcknowledgement
        {
            AnnouncementId = id,
            UserId = caller.UserId
        });

        await _db.SaveChangesAsync(cancellationToken);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<AnnouncementAcknowledgementDto>> GetAcknowledgementsAsync(Guid id, CallerContext caller, CancellationToken cancellationToken = default)
    {
        return await _db.AnnouncementAcknowledgements
            .AsNoTracking()
            .Where(a => a.AnnouncementId == id)
            .OrderBy(a => a.AcknowledgedAt)
            .Select(a => new AnnouncementAcknowledgementDto
            {
                UserId = a.UserId,
                AcknowledgedAt = a.AcknowledgedAt
            })
            .ToListAsync(cancellationToken);
    }

    private static AnnouncementDto ToDto(Announcement a, int ackCount) => new()
    {
        Id = a.Id,
        OrganizationId = a.OrganizationId,
        AuthorUserId = a.AuthorUserId,
        Title = a.Title,
        Content = a.Content,
        Priority = a.Priority.ToString(),
        PublishedAt = a.PublishedAt,
        ExpiresAt = a.ExpiresAt,
        IsPinned = a.IsPinned,
        RequiresAcknowledgement = a.RequiresAcknowledgement,
        AcknowledgementCount = ackCount
    };
}
