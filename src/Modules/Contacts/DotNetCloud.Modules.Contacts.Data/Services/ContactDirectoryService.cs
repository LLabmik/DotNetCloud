using DotNetCloud.Core.Capabilities;
using DotNetCloud.Modules.Contacts.Models;
using Microsoft.EntityFrameworkCore;

namespace DotNetCloud.Modules.Contacts.Data.Services;

/// <summary>
/// Implements <see cref="IContactDirectory"/> providing read-only contact directory access
/// backed by the Contacts module database.
/// </summary>
public sealed class ContactDirectoryService : IContactDirectory
{
    private readonly ContactsDbContext _db;

    /// <summary>
    /// Initializes a new instance of <see cref="ContactDirectoryService"/>.
    /// </summary>
    public ContactDirectoryService(ContactsDbContext db)
    {
        _db = db;
    }

    /// <inheritdoc />
    public async Task<string?> GetContactDisplayNameAsync(Guid contactId, CancellationToken cancellationToken = default)
    {
        return await _db.Contacts
            .AsNoTracking()
            .Where(c => c.Id == contactId && !c.IsDeleted)
            .Select(c => c.DisplayName)
            .FirstOrDefaultAsync(cancellationToken);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyDictionary<Guid, string>> GetContactDisplayNamesAsync(
        IEnumerable<Guid> contactIds,
        CancellationToken cancellationToken = default)
    {
        var ids = contactIds.Distinct().ToList();
        if (ids.Count == 0)
            return new Dictionary<Guid, string>();

        return await _db.Contacts
            .AsNoTracking()
            .Where(c => ids.Contains(c.Id) && !c.IsDeleted)
            .ToDictionaryAsync(c => c.Id, c => c.DisplayName, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<(Guid ContactId, string DisplayName)>> SearchContactsAsync(
        Guid userId,
        string query,
        int maxResults = 20,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(query))
            return [];

        var term = query.Trim();

        var lowerTerm = term.ToLowerInvariant();

        var results = await _db.Contacts
            .AsNoTracking()
            .Where(c => c.OwnerId == userId && !c.IsDeleted &&
                (c.DisplayName.ToLower().Contains(lowerTerm) ||
                 (c.FirstName != null && c.FirstName.ToLower().Contains(lowerTerm)) ||
                 (c.LastName != null && c.LastName.ToLower().Contains(lowerTerm)) ||
                 (c.Organization != null && c.Organization.ToLower().Contains(lowerTerm))))
            .OrderBy(c => c.DisplayName)
            .Take(maxResults)
            .Select(c => new { c.Id, c.DisplayName })
            .ToListAsync(cancellationToken);

        return results.Select(r => (r.Id, r.DisplayName)).ToList();
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<ContactSearchResult>> SearchContactsWithEmailsAsync(
        Guid userId,
        string query,
        int maxResults = 10,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(query))
            return [];

        var lowerTerm = query.Trim().ToLowerInvariant();

        var contacts = await _db.Contacts
            .AsNoTracking()
            .Include(c => c.Emails)
            .Where(c => c.OwnerId == userId && !c.IsDeleted &&
                (c.DisplayName.ToLower().Contains(lowerTerm) ||
                 (c.FirstName != null && c.FirstName.ToLower().Contains(lowerTerm)) ||
                 (c.LastName != null && c.LastName.ToLower().Contains(lowerTerm)) ||
                 (c.Organization != null && c.Organization.ToLower().Contains(lowerTerm)) ||
                 c.Emails.Any(e => e.Address.ToLower().Contains(lowerTerm))))
            .OrderBy(c => c.DisplayName)
            .Take(maxResults)
            .ToListAsync(cancellationToken);

        return contacts.Select(c => new ContactSearchResult
        {
            ContactId = c.Id,
            DisplayName = c.DisplayName,
            Emails = c.Emails
                .OrderBy(e => e.SortOrder)
                .Select(e => (e.Address, e.Label))
                .ToList()
        }).ToList();
    }
}
