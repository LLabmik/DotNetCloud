using DotNetCloud.Core.Authorization;
using DotNetCloud.Core.DTOs;
using DotNetCloud.Modules.Contacts.Models;
using DotNetCloud.Modules.Contacts.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace DotNetCloud.Modules.Contacts.Data.Services;

/// <summary>
/// Database-backed implementation of <see cref="IContactGroupService"/>.
/// </summary>
public sealed class ContactGroupService : IContactGroupService
{
    private readonly ContactsDbContext _db;
    private readonly ILogger<ContactGroupService> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="ContactGroupService"/> class.
    /// </summary>
    public ContactGroupService(ContactsDbContext db, ILogger<ContactGroupService> logger)
    {
        _db = db;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<ContactGroupDto> CreateGroupAsync(string name, CallerContext caller, CancellationToken cancellationToken = default)
    {
        var exists = await _db.ContactGroups
            .AnyAsync(g => g.OwnerId == caller.UserId && g.Name == name, cancellationToken);

        if (exists)
        {
            throw new Core.Errors.ValidationException(
                Core.Errors.ErrorCodes.ContactGroupAlreadyExists, $"Group '{name}' already exists.");
        }

        var group = new ContactGroup
        {
            OwnerId = caller.UserId,
            Name = name
        };

        _db.ContactGroups.Add(group);
        await _db.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Contact group {GroupId} '{Name}' created by user {UserId}",
            group.Id, name, caller.UserId);

        return MapToDto(group, 0);
    }

    /// <inheritdoc />
    public async Task<ContactGroupDto?> GetGroupAsync(Guid groupId, CallerContext caller, CancellationToken cancellationToken = default)
    {
        var group = await _db.ContactGroups
            .AsNoTracking()
            .FirstOrDefaultAsync(g => g.Id == groupId && g.OwnerId == caller.UserId, cancellationToken);

        if (group is null) return null;

        var memberCount = await _db.ContactGroupMembers
            .CountAsync(m => m.GroupId == groupId, cancellationToken);

        return MapToDto(group, memberCount);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<ContactGroupDto>> ListGroupsAsync(CallerContext caller, CancellationToken cancellationToken = default)
    {
        var groups = await _db.ContactGroups
            .AsNoTracking()
            .Where(g => g.OwnerId == caller.UserId)
            .OrderBy(g => g.Name)
            .ToListAsync(cancellationToken);

        var groupIds = groups.Select(g => g.Id).ToList();
        var memberCounts = await _db.ContactGroupMembers
            .Where(m => groupIds.Contains(m.GroupId))
            .GroupBy(m => m.GroupId)
            .Select(g => new { GroupId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.GroupId, x => x.Count, cancellationToken);

        return groups.Select(g => MapToDto(g, memberCounts.GetValueOrDefault(g.Id, 0))).ToList();
    }

    /// <inheritdoc />
    public async Task<ContactGroupDto> RenameGroupAsync(Guid groupId, string newName, CallerContext caller, CancellationToken cancellationToken = default)
    {
        var group = await _db.ContactGroups
            .FirstOrDefaultAsync(g => g.Id == groupId && g.OwnerId == caller.UserId, cancellationToken)
            ?? throw new Core.Errors.ValidationException(Core.Errors.ErrorCodes.ContactGroupNotFound, "Group not found.");

        group.Name = newName;
        group.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(cancellationToken);

        var memberCount = await _db.ContactGroupMembers
            .CountAsync(m => m.GroupId == groupId, cancellationToken);

        return MapToDto(group, memberCount);
    }

    /// <inheritdoc />
    public async Task DeleteGroupAsync(Guid groupId, CallerContext caller, CancellationToken cancellationToken = default)
    {
        var group = await _db.ContactGroups
            .FirstOrDefaultAsync(g => g.Id == groupId && g.OwnerId == caller.UserId, cancellationToken)
            ?? throw new Core.Errors.ValidationException(Core.Errors.ErrorCodes.ContactGroupNotFound, "Group not found.");

        group.IsDeleted = true;
        group.DeletedAt = DateTime.UtcNow;
        group.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Contact group {GroupId} deleted by user {UserId}", groupId, caller.UserId);
    }

    /// <inheritdoc />
    public async Task AddContactToGroupAsync(Guid groupId, Guid contactId, CallerContext caller, CancellationToken cancellationToken = default)
    {
        var groupExists = await _db.ContactGroups
            .AnyAsync(g => g.Id == groupId && g.OwnerId == caller.UserId, cancellationToken);
        if (!groupExists)
        {
            throw new Core.Errors.ValidationException(Core.Errors.ErrorCodes.ContactGroupNotFound, "Group not found.");
        }

        var contactExists = await _db.Contacts
            .AnyAsync(c => c.Id == contactId && c.OwnerId == caller.UserId, cancellationToken);
        if (!contactExists)
        {
            throw new Core.Errors.ValidationException(Core.Errors.ErrorCodes.ContactNotFound, "Contact not found.");
        }

        var already = await _db.ContactGroupMembers
            .AnyAsync(m => m.GroupId == groupId && m.ContactId == contactId, cancellationToken);
        if (already) return;

        _db.ContactGroupMembers.Add(new ContactGroupMember
        {
            GroupId = groupId,
            ContactId = contactId
        });

        await _db.SaveChangesAsync(cancellationToken);
    }

    /// <inheritdoc />
    public async Task RemoveContactFromGroupAsync(Guid groupId, Guid contactId, CallerContext caller, CancellationToken cancellationToken = default)
    {
        var member = await _db.ContactGroupMembers
            .FirstOrDefaultAsync(m => m.GroupId == groupId && m.ContactId == contactId, cancellationToken);

        if (member is not null)
        {
            _db.ContactGroupMembers.Remove(member);
            await _db.SaveChangesAsync(cancellationToken);
        }
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<ContactDto>> ListGroupMembersAsync(Guid groupId, CallerContext caller, CancellationToken cancellationToken = default)
    {
        var groupExists = await _db.ContactGroups
            .AnyAsync(g => g.Id == groupId && g.OwnerId == caller.UserId, cancellationToken);
        if (!groupExists)
        {
            throw new Core.Errors.ValidationException(Core.Errors.ErrorCodes.ContactGroupNotFound, "Group not found.");
        }

        var contacts = await _db.ContactGroupMembers
            .Where(m => m.GroupId == groupId)
            .Include(m => m.Contact!)
                .ThenInclude(c => c.Emails)
            .Include(m => m.Contact!)
                .ThenInclude(c => c.PhoneNumbers)
            .Include(m => m.Contact!)
                .ThenInclude(c => c.Addresses)
            .Include(m => m.Contact!)
                .ThenInclude(c => c.CustomFields)
            .Include(m => m.Contact!)
                .ThenInclude(c => c.GroupMemberships)
            .Select(m => m.Contact!)
            .AsNoTracking()
            .OrderBy(c => c.DisplayName)
            .ToListAsync(cancellationToken);

        return contacts.Select(c => new ContactDto
        {
            Id = c.Id,
            OwnerId = c.OwnerId,
            ContactType = c.ContactType,
            DisplayName = c.DisplayName,
            FirstName = c.FirstName,
            LastName = c.LastName,
            Organization = c.Organization,
            CreatedAt = c.CreatedAt,
            UpdatedAt = c.UpdatedAt,
            Emails = c.Emails.Select(e => new ContactEmailDto
            {
                Address = e.Address,
                Label = e.Label,
                IsPrimary = e.IsPrimary
            }).ToList(),
            PhoneNumbers = c.PhoneNumbers.Select(p => new ContactPhoneDto
            {
                Number = p.Number,
                Label = p.Label,
                IsPrimary = p.IsPrimary
            }).ToList(),
            GroupIds = c.GroupMemberships.Select(m => m.GroupId).ToList(),
            ETag = c.ETag
        }).ToList();
    }

    private static ContactGroupDto MapToDto(ContactGroup g, int memberCount) => new()
    {
        Id = g.Id,
        OwnerId = g.OwnerId,
        Name = g.Name,
        MemberCount = memberCount,
        CreatedAt = g.CreatedAt,
        UpdatedAt = g.UpdatedAt
    };
}
