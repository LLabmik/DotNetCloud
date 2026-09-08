using DotNetCloud.Core.Authorization;
using DotNetCloud.Modules.Contacts.Models;

namespace DotNetCloud.Modules.Contacts.Services;

/// <summary>
/// Contact sharing operations.
/// </summary>
public interface IContactShareService
{
    /// <summary>Shares a contact with a user or team.</summary>
    Task<ContactShare> ShareContactAsync(Guid contactId, Guid? userId, Guid? teamId, ContactSharePermission permission, CallerContext caller, CancellationToken cancellationToken = default);

    /// <summary>Removes a share.</summary>
    Task RemoveShareAsync(Guid shareId, CallerContext caller, CancellationToken cancellationToken = default);

    /// <summary>Lists shares for a contact.</summary>
    Task<IReadOnlyList<ContactShare>> ListSharesAsync(Guid contactId, CallerContext caller, CancellationToken cancellationToken = default);

    /// <summary>
    /// Lists the contacts shared with the caller (direct user shares plus shares targeting a
    /// team the caller belongs to), including the contact display name for aggregated surfaces.
    /// </summary>
    Task<IReadOnlyList<ContactSharedItem>> ListSharedWithMeAsync(CallerContext caller, CancellationToken cancellationToken = default);
}
