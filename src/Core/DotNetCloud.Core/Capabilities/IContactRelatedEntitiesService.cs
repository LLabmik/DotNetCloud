using DotNetCloud.Core.DTOs;

namespace DotNetCloud.Core.Capabilities;

/// <summary>
/// Provides reverse cross-module lookups for a contact.
/// </summary>
public interface IContactRelatedEntitiesService : ICapabilityInterface
{
    /// <summary>
    /// Gets related calendar events and notes for a contact.
    /// </summary>
    Task<ContactRelatedEntitiesDto> GetRelatedAsync(Guid contactId, Guid ownerId, CancellationToken cancellationToken = default);
}
