using DotNetCloud.Core.Authorization;
using DotNetCloud.Modules.Contacts.Models;
using DotNetCloud.Modules.Contacts.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace DotNetCloud.Modules.Contacts.Data.Services;

/// <summary>
/// Database-backed implementation of <see cref="IContactShareService"/>.
/// </summary>
public sealed class ContactShareService : IContactShareService
{
    private readonly ContactsDbContext _db;
    private readonly ILogger<ContactShareService> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="ContactShareService"/> class.
    /// </summary>
    public ContactShareService(ContactsDbContext db, ILogger<ContactShareService> logger)
    {
        _db = db;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<ContactShare> ShareContactAsync(Guid contactId, Guid? userId, Guid? teamId, ContactSharePermission permission, CallerContext caller, CancellationToken cancellationToken = default)
    {
        var contactExists = await _db.Contacts
            .AnyAsync(c => c.Id == contactId && c.OwnerId == caller.UserId, cancellationToken);

        if (!contactExists)
        {
            throw new Core.Errors.ValidationException(Core.Errors.ErrorCodes.ContactNotFound, "Contact not found.");
        }

        if (userId is null && teamId is null)
        {
            throw new ArgumentException("Either userId or teamId must be specified.");
        }

        var share = new ContactShare
        {
            ContactId = contactId,
            SharedByUserId = caller.UserId,
            SharedWithUserId = userId,
            SharedWithTeamId = teamId,
            Permission = permission
        };

        _db.ContactShares.Add(share);
        await _db.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Contact {ContactId} shared by user {UserId}", contactId, caller.UserId);

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
}
