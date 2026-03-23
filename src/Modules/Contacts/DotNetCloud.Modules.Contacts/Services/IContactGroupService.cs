using DotNetCloud.Core.Authorization;
using DotNetCloud.Core.DTOs;

namespace DotNetCloud.Modules.Contacts.Services;

/// <summary>
/// Contact group management operations.
/// </summary>
public interface IContactGroupService
{
    /// <summary>Creates a new contact group.</summary>
    Task<ContactGroupDto> CreateGroupAsync(string name, CallerContext caller, CancellationToken cancellationToken = default);

    /// <summary>Gets a group by ID.</summary>
    Task<ContactGroupDto?> GetGroupAsync(Guid groupId, CallerContext caller, CancellationToken cancellationToken = default);

    /// <summary>Lists groups for the calling user.</summary>
    Task<IReadOnlyList<ContactGroupDto>> ListGroupsAsync(CallerContext caller, CancellationToken cancellationToken = default);

    /// <summary>Renames a group.</summary>
    Task<ContactGroupDto> RenameGroupAsync(Guid groupId, string newName, CallerContext caller, CancellationToken cancellationToken = default);

    /// <summary>Deletes a group.</summary>
    Task DeleteGroupAsync(Guid groupId, CallerContext caller, CancellationToken cancellationToken = default);

    /// <summary>Adds a contact to a group.</summary>
    Task AddContactToGroupAsync(Guid groupId, Guid contactId, CallerContext caller, CancellationToken cancellationToken = default);

    /// <summary>Removes a contact from a group.</summary>
    Task RemoveContactFromGroupAsync(Guid groupId, Guid contactId, CallerContext caller, CancellationToken cancellationToken = default);

    /// <summary>Lists contacts in a group.</summary>
    Task<IReadOnlyList<ContactDto>> ListGroupMembersAsync(Guid groupId, CallerContext caller, CancellationToken cancellationToken = default);
}
