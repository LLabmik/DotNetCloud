using DotNetCloud.Core.Authorization;
using DotNetCloud.Core.DTOs;
using DotNetCloud.Core.Events;
using DotNetCloud.Modules.Contacts.Models;
using DotNetCloud.Modules.Contacts.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace DotNetCloud.Modules.Contacts.Data.Services;

/// <summary>
/// Database-backed implementation of <see cref="IContactService"/>.
/// </summary>
public sealed class ContactService : IContactService
{
    private readonly ContactsDbContext _db;
    private readonly IEventBus _eventBus;
    private readonly ILogger<ContactService> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="ContactService"/> class.
    /// </summary>
    public ContactService(ContactsDbContext db, IEventBus eventBus, ILogger<ContactService> logger)
    {
        _db = db;
        _eventBus = eventBus;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<ContactDto> CreateContactAsync(CreateContactDto dto, CallerContext caller, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(dto);

        var contact = new Contact
        {
            OwnerId = caller.UserId,
            ContactType = dto.ContactType,
            DisplayName = dto.DisplayName,
            FirstName = dto.FirstName,
            LastName = dto.LastName,
            MiddleName = dto.MiddleName,
            Prefix = dto.Prefix,
            Suffix = dto.Suffix,
            Organization = dto.Organization,
            Department = dto.Department,
            JobTitle = dto.JobTitle,
            Notes = dto.Notes,
            Birthday = dto.Birthday,
            WebsiteUrl = dto.WebsiteUrl
        };

        foreach (var email in dto.Emails)
        {
            contact.Emails.Add(new ContactEmail
            {
                Address = email.Address,
                Label = email.Label,
                IsPrimary = email.IsPrimary,
                SortOrder = contact.Emails.Count
            });
        }

        foreach (var phone in dto.PhoneNumbers)
        {
            contact.PhoneNumbers.Add(new ContactPhone
            {
                Number = phone.Number,
                Label = phone.Label,
                IsPrimary = phone.IsPrimary,
                SortOrder = contact.PhoneNumbers.Count
            });
        }

        foreach (var addr in dto.Addresses)
        {
            contact.Addresses.Add(new ContactAddress
            {
                Label = addr.Label,
                Street = addr.Street,
                City = addr.City,
                Region = addr.Region,
                PostalCode = addr.PostalCode,
                Country = addr.Country,
                IsPrimary = addr.IsPrimary,
                SortOrder = contact.Addresses.Count
            });
        }

        foreach (var (key, value) in dto.CustomFields)
        {
            contact.CustomFields.Add(new ContactCustomField { Key = key, Value = value });
        }

        _db.Contacts.Add(contact);
        await _db.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Contact {ContactId} '{DisplayName}' created by user {UserId}",
            contact.Id, contact.DisplayName, caller.UserId);

        await _eventBus.PublishAsync(new ContactCreatedEvent
        {
            EventId = Guid.NewGuid(),
            CreatedAt = DateTime.UtcNow,
            ContactId = contact.Id,
            DisplayName = contact.DisplayName,
            OwnerId = caller.UserId
        }, caller, cancellationToken);

        return MapToDto(contact);
    }

    /// <inheritdoc />
    public async Task<ContactDto?> GetContactAsync(Guid contactId, CallerContext caller, CancellationToken cancellationToken = default)
    {
        var contact = await QueryContacts()
            .FirstOrDefaultAsync(c => c.Id == contactId && (c.OwnerId == caller.UserId || c.Shares.Any(s => s.SharedWithUserId == caller.UserId)), cancellationToken);

        return contact is null ? null : MapToDto(contact);
    }

    /// <inheritdoc />
    public async Task<ContactDto> UpdateContactAsync(Guid contactId, UpdateContactDto dto, CallerContext caller, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(dto);

        var contact = await QueryContacts()
            .FirstOrDefaultAsync(c => c.Id == contactId && c.OwnerId == caller.UserId, cancellationToken)
            ?? throw new Core.Errors.ValidationException(Core.Errors.ErrorCodes.ContactNotFound, "Contact not found.");

        if (dto.DisplayName is not null) contact.DisplayName = dto.DisplayName;
        if (dto.FirstName is not null) contact.FirstName = dto.FirstName;
        if (dto.LastName is not null) contact.LastName = dto.LastName;
        if (dto.MiddleName is not null) contact.MiddleName = dto.MiddleName;
        if (dto.Prefix is not null) contact.Prefix = dto.Prefix;
        if (dto.Suffix is not null) contact.Suffix = dto.Suffix;
        if (dto.Organization is not null) contact.Organization = dto.Organization;
        if (dto.Department is not null) contact.Department = dto.Department;
        if (dto.JobTitle is not null) contact.JobTitle = dto.JobTitle;
        if (dto.Notes is not null) contact.Notes = dto.Notes;
        if (dto.Birthday is not null) contact.Birthday = dto.Birthday;
        if (dto.WebsiteUrl is not null) contact.WebsiteUrl = dto.WebsiteUrl;

        if (dto.Emails is not null)
        {
            _db.ContactEmails.RemoveRange(contact.Emails);
            contact.Emails.Clear();
            foreach (var email in dto.Emails)
            {
                contact.Emails.Add(new ContactEmail
                {
                    Address = email.Address,
                    Label = email.Label,
                    IsPrimary = email.IsPrimary,
                    SortOrder = contact.Emails.Count
                });
            }
        }

        if (dto.PhoneNumbers is not null)
        {
            _db.ContactPhones.RemoveRange(contact.PhoneNumbers);
            contact.PhoneNumbers.Clear();
            foreach (var phone in dto.PhoneNumbers)
            {
                contact.PhoneNumbers.Add(new ContactPhone
                {
                    Number = phone.Number,
                    Label = phone.Label,
                    IsPrimary = phone.IsPrimary,
                    SortOrder = contact.PhoneNumbers.Count
                });
            }
        }

        if (dto.Addresses is not null)
        {
            _db.ContactAddresses.RemoveRange(contact.Addresses);
            contact.Addresses.Clear();
            foreach (var addr in dto.Addresses)
            {
                contact.Addresses.Add(new ContactAddress
                {
                    Label = addr.Label,
                    Street = addr.Street,
                    City = addr.City,
                    Region = addr.Region,
                    PostalCode = addr.PostalCode,
                    Country = addr.Country,
                    IsPrimary = addr.IsPrimary,
                    SortOrder = contact.Addresses.Count
                });
            }
        }

        if (dto.CustomFields is not null)
        {
            _db.ContactCustomFields.RemoveRange(contact.CustomFields);
            contact.CustomFields.Clear();
            foreach (var (key, value) in dto.CustomFields)
            {
                contact.CustomFields.Add(new ContactCustomField { Key = key, Value = value });
            }
        }

        contact.ETag = Guid.NewGuid().ToString("N");
        contact.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Contact {ContactId} updated by user {UserId}", contactId, caller.UserId);

        await _eventBus.PublishAsync(new ContactUpdatedEvent
        {
            EventId = Guid.NewGuid(),
            CreatedAt = DateTime.UtcNow,
            ContactId = contactId,
            UpdatedByUserId = caller.UserId
        }, caller, cancellationToken);

        return MapToDto(contact);
    }

    /// <inheritdoc />
    public async Task DeleteContactAsync(Guid contactId, CallerContext caller, CancellationToken cancellationToken = default)
    {
        var contact = await _db.Contacts
            .FirstOrDefaultAsync(c => c.Id == contactId && c.OwnerId == caller.UserId, cancellationToken)
            ?? throw new Core.Errors.ValidationException(Core.Errors.ErrorCodes.ContactNotFound, "Contact not found.");

        contact.IsDeleted = true;
        contact.DeletedAt = DateTime.UtcNow;
        contact.UpdatedAt = DateTime.UtcNow;
        contact.ETag = Guid.NewGuid().ToString("N");

        await _db.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Contact {ContactId} soft-deleted by user {UserId}", contactId, caller.UserId);

        await _eventBus.PublishAsync(new ContactDeletedEvent
        {
            EventId = Guid.NewGuid(),
            CreatedAt = DateTime.UtcNow,
            ContactId = contactId,
            DeletedByUserId = caller.UserId,
            IsPermanent = false
        }, caller, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<ContactDto>> ListContactsAsync(CallerContext caller, string? search = null, int skip = 0, int take = 50, CancellationToken cancellationToken = default)
    {
        var query = QueryContacts()
            .Where(c => c.OwnerId == caller.UserId || c.Shares.Any(s => s.SharedWithUserId == caller.UserId));

        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim();
            query = query.Where(c =>
                c.DisplayName.Contains(term) ||
                (c.FirstName != null && c.FirstName.Contains(term)) ||
                (c.LastName != null && c.LastName.Contains(term)) ||
                (c.Organization != null && c.Organization.Contains(term)) ||
                c.Emails.Any(e => e.Address.Contains(term)));
        }

        var contacts = await query
            .OrderBy(c => c.DisplayName)
            .Skip(skip)
            .Take(take)
            .ToListAsync(cancellationToken);

        return contacts.Select(MapToDto).ToList();
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<ContactDto>> GetContactsByIdsAsync(IEnumerable<Guid> contactIds, CallerContext caller, CancellationToken cancellationToken = default)
    {
        var ids = contactIds.ToList();
        if (ids.Count == 0) return [];

        var contacts = await QueryContacts()
            .Where(c => ids.Contains(c.Id) && (c.OwnerId == caller.UserId || c.Shares.Any(s => s.SharedWithUserId == caller.UserId)))
            .ToListAsync(cancellationToken);

        return contacts.Select(MapToDto).ToList();
    }

    private IQueryable<Contact> QueryContacts()
    {
        return _db.Contacts
            .Include(c => c.Emails)
            .Include(c => c.PhoneNumbers)
            .Include(c => c.Addresses)
            .Include(c => c.CustomFields)
            .Include(c => c.GroupMemberships)
            .Include(c => c.Shares)
            .Include(c => c.Attachments)
            .AsNoTracking();
    }

    private static ContactDto MapToDto(Contact c)
    {
        return new ContactDto
        {
            Id = c.Id,
            OwnerId = c.OwnerId,
            ContactType = c.ContactType,
            DisplayName = c.DisplayName,
            FirstName = c.FirstName,
            LastName = c.LastName,
            MiddleName = c.MiddleName,
            Prefix = c.Prefix,
            Suffix = c.Suffix,
            PhoneticName = c.PhoneticName,
            Nickname = c.Nickname,
            Organization = c.Organization,
            Department = c.Department,
            JobTitle = c.JobTitle,
            AvatarUrl = c.AvatarUrl,
            Notes = c.Notes,
            Birthday = c.Birthday,
            Anniversary = c.Anniversary,
            WebsiteUrl = c.WebsiteUrl,
            IsDeleted = c.IsDeleted,
            DeletedAt = c.DeletedAt,
            CreatedAt = c.CreatedAt,
            UpdatedAt = c.UpdatedAt,
            Emails = c.Emails.OrderBy(e => e.SortOrder).Select(e => new ContactEmailDto
            {
                Address = e.Address,
                Label = e.Label,
                IsPrimary = e.IsPrimary
            }).ToList(),
            PhoneNumbers = c.PhoneNumbers.OrderBy(p => p.SortOrder).Select(p => new ContactPhoneDto
            {
                Number = p.Number,
                Label = p.Label,
                IsPrimary = p.IsPrimary
            }).ToList(),
            Addresses = c.Addresses.OrderBy(a => a.SortOrder).Select(a => new ContactAddressDto
            {
                Label = a.Label,
                Street = a.Street,
                City = a.City,
                Region = a.Region,
                PostalCode = a.PostalCode,
                Country = a.Country,
                IsPrimary = a.IsPrimary
            }).ToList(),
            GroupIds = c.GroupMemberships.Select(m => m.GroupId).ToList(),
            CustomFields = c.CustomFields.ToDictionary(f => f.Key, f => f.Value),
            Attachments = c.Attachments.OrderBy(a => a.CreatedAt).Select(a => new ContactAttachmentDto
            {
                Id = a.Id,
                ContactId = a.ContactId,
                FileName = a.FileName,
                ContentType = a.ContentType,
                FileSizeBytes = a.FileSizeBytes,
                IsAvatar = a.IsAvatar,
                Description = a.Description,
                CreatedAt = a.CreatedAt,
                UpdatedAt = a.UpdatedAt
            }).ToList(),
            ETag = c.ETag
        };
    }
}
