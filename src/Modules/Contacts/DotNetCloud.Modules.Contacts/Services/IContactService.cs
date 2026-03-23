using DotNetCloud.Core.Authorization;
using DotNetCloud.Core.DTOs;
using DotNetCloud.Modules.Contacts.Models;

namespace DotNetCloud.Modules.Contacts.Services;

/// <summary>
/// Core contact CRUD and search operations.
/// </summary>
public interface IContactService
{
    /// <summary>Creates a new contact.</summary>
    Task<ContactDto> CreateContactAsync(CreateContactDto dto, CallerContext caller, CancellationToken cancellationToken = default);

    /// <summary>Gets a contact by ID.</summary>
    Task<ContactDto?> GetContactAsync(Guid contactId, CallerContext caller, CancellationToken cancellationToken = default);

    /// <summary>Updates an existing contact.</summary>
    Task<ContactDto> UpdateContactAsync(Guid contactId, UpdateContactDto dto, CallerContext caller, CancellationToken cancellationToken = default);

    /// <summary>Soft-deletes a contact.</summary>
    Task DeleteContactAsync(Guid contactId, CallerContext caller, CancellationToken cancellationToken = default);

    /// <summary>Lists contacts for the calling user with optional search.</summary>
    Task<IReadOnlyList<ContactDto>> ListContactsAsync(CallerContext caller, string? search = null, int skip = 0, int take = 50, CancellationToken cancellationToken = default);

    /// <summary>Gets contacts by a list of IDs.</summary>
    Task<IReadOnlyList<ContactDto>> GetContactsByIdsAsync(IEnumerable<Guid> contactIds, CallerContext caller, CancellationToken cancellationToken = default);
}
