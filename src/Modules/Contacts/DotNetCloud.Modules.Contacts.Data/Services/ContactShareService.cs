using DotNetCloud.Core.Authorization;
using DotNetCloud.Core.Events;
using DotNetCloud.Modules.Contacts.Models;
using DotNetCloud.Modules.Contacts.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using ITeamDirectory = DotNetCloud.Core.Capabilities.ITeamDirectory;

namespace DotNetCloud.Modules.Contacts.Data.Services;

/// <summary>
/// Database-backed implementation of <see cref="IContactShareService"/>.
/// </summary>
public sealed class ContactShareService : IContactShareService
{
    private readonly ContactsDbContext _db;
    private readonly IEventBus _eventBus;
    private readonly ITeamDirectory? _teamDirectory;
    private readonly ILogger<ContactShareService> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="ContactShareService"/> class.
    /// </summary>
    public ContactShareService(
        ContactsDbContext db,
        IEventBus eventBus,
        ILogger<ContactShareService> logger,
        ITeamDirectory? teamDirectory = null)
    {
        _db = db;
        _eventBus = eventBus;
        _teamDirectory = teamDirectory;
        _logger = logger;
    }

    /// <summary>
    /// Resolves the caller's team IDs for team-share queries (empty when the team directory
    /// capability is unavailable).
    /// </summary>
    private async Task<Guid[]> GetCallerTeamIdsAsync(CallerContext caller, CancellationToken cancellationToken)
    {
        if (_teamDirectory is null)
        {
            return [];
        }

        try
        {
            var teams = await _teamDirectory.GetTeamsForUserAsync(caller.UserId, cancellationToken);
            return teams.Select(t => t.Id).ToArray();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to resolve team memberships for {UserId}", caller.UserId);
            return [];
        }
    }

    /// <inheritdoc />
    public async Task<ContactShare> ShareContactAsync(Guid contactId, Guid? userId, Guid? teamId, ContactSharePermission permission, CallerContext caller, CancellationToken cancellationToken = default)
    {
        // User XOR team target.
        if (userId is null && teamId is null)
        {
            throw new ArgumentException("A contact share requires a user or a team target.", nameof(teamId));
        }

        if (userId is not null && teamId is not null)
        {
            throw new ArgumentException("A contact share cannot target both a user and a team.", nameof(teamId));
        }

        var contactExists = await _db.Contacts
            .AnyAsync(c => c.Id == contactId && c.OwnerId == caller.UserId, cancellationToken);

        if (!contactExists)
        {
            throw new Core.Errors.ValidationException(Core.Errors.ErrorCodes.ContactNotFound, "Contact not found.");
        }

        // Check if already shared with this target (dedupe per target).
        ContactShare? existingShare;
        if (userId is { } targetUserId)
        {
            existingShare = await _db.ContactShares
                .FirstOrDefaultAsync(s => s.ContactId == contactId && s.SharedWithUserId == targetUserId, cancellationToken);
        }
        else
        {
            existingShare = await _db.ContactShares
                .FirstOrDefaultAsync(s => s.ContactId == contactId && s.SharedWithTeamId == teamId, cancellationToken);
        }

        if (existingShare is not null)
        {
            existingShare.Permission = permission;
            existingShare.UpdatedByUserId = caller.UserId;
            await _db.SaveChangesAsync(cancellationToken);
            return existingShare;
        }

        var share = new ContactShare
        {
            ContactId = contactId,
            SharedByUserId = caller.UserId,
            SharedWithUserId = userId,
            SharedWithTeamId = teamId,
            Permission = permission,
            UpdatedByUserId = caller.UserId
        };

        _db.ContactShares.Add(share);
        await _db.SaveChangesAsync(cancellationToken);

        await _eventBus.PublishAsync(new ResourceSharedEvent
        {
            EventId = Guid.CreateVersion7(),
            CreatedAt = DateTime.UtcNow,
            SharedByUserId = caller.UserId,
            SharedWithUserId = userId,
            SharedWithTeamId = teamId,
            SourceModuleId = "dotnetcloud.contacts",
            EntityType = "Contact",
            EntityId = contactId,
            EntityDisplayName = "Contact",
            Permission = permission.ToString()
        }, caller, cancellationToken);

        _logger.LogInformation(
            "Contact {ContactId} shared with {TargetType} {TargetId} ({Permission}) by user {UserId}",
            contactId,
            teamId is not null ? "team" : "user",
            teamId ?? userId,
            permission,
            caller.UserId);

        return share;
    }

    /// <inheritdoc />
    public async Task RemoveShareAsync(Guid shareId, CallerContext caller, CancellationToken cancellationToken = default)
    {
        var share = await _db.ContactShares
            .FirstOrDefaultAsync(s => s.Id == shareId && s.SharedByUserId == caller.UserId, cancellationToken);

        if (share is not null)
        {
            _db.ContactShares.Remove(share);
            await _db.SaveChangesAsync(cancellationToken);

            _logger.LogInformation("Contact share {ShareId} removed by user {UserId}", shareId, caller.UserId);
        }
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<ContactShare>> ListSharesAsync(Guid contactId, CallerContext caller, CancellationToken cancellationToken = default)
    {
        return await _db.ContactShares
            .AsNoTracking()
            .Where(s => s.ContactId == contactId && s.SharedByUserId == caller.UserId)
            .OrderBy(s => s.CreatedAt)
            .ToListAsync(cancellationToken);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<ContactSharedItem>> ListSharedWithMeAsync(CallerContext caller, CancellationToken cancellationToken = default)
    {
        var teamIds = await GetCallerTeamIdsAsync(caller, cancellationToken);
        var now = DateTime.UtcNow;

        var shares = await _db.ContactShares
            .AsNoTracking()
            .Include(s => s.Contact)
            .Where(s =>
                (s.SharedWithUserId == caller.UserId ||
                 (s.SharedWithTeamId != null && teamIds.Contains(s.SharedWithTeamId.Value))) &&
                (s.ExpiresAt == null || s.ExpiresAt > now))
            .OrderByDescending(s => s.CreatedAt)
            .ToListAsync(cancellationToken);

        return shares
            .Where(s => s.Contact is not null && !s.Contact!.IsDeleted)
            .Select(s => new ContactSharedItem
            {
                ContactId = s.ContactId,
                DisplayName = s.Contact!.DisplayName,
                SharedByUserId = s.SharedByUserId,
                SharedWithUserId = s.SharedWithUserId,
                SharedWithTeamId = s.SharedWithTeamId,
                Permission = s.Permission,
                CreatedAt = s.CreatedAt,
                ExpiresAt = s.ExpiresAt,
                UpdatedAt = s.Contact.UpdatedAt
            })
            .ToList();
    }
}
