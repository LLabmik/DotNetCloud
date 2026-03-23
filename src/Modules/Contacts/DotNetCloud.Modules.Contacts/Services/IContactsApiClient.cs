using DotNetCloud.Core.DTOs;

namespace DotNetCloud.Modules.Contacts.Services;

/// <summary>
/// HTTP API client for the Contacts REST endpoints.
/// </summary>
public interface IContactsApiClient
{
    Task<IReadOnlyList<ContactDto>> ListContactsAsync(string? search, int skip, int take, CancellationToken cancellationToken = default);
    Task<ContactDto?> GetContactAsync(Guid contactId, CancellationToken cancellationToken = default);
    Task<ContactDto?> CreateContactAsync(CreateContactDto dto, CancellationToken cancellationToken = default);
    Task<ContactDto?> UpdateContactAsync(Guid contactId, UpdateContactDto dto, CancellationToken cancellationToken = default);
    Task DeleteContactAsync(Guid contactId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<ContactGroupDto>> ListGroupsAsync(CancellationToken cancellationToken = default);
    Task<ContactRelatedEntitiesDto> GetRelatedAsync(Guid contactId, CancellationToken cancellationToken = default);

    // Sharing
    Task<IReadOnlyList<ContactShareResponse>> ListSharesAsync(Guid contactId, CancellationToken cancellationToken = default);
    Task<ContactShareResponse?> ShareContactAsync(Guid contactId, Guid? userId, Guid? teamId, string permission = "ReadOnly", CancellationToken cancellationToken = default);
    Task RevokeShareAsync(Guid shareId, CancellationToken cancellationToken = default);

    // Avatar
    Task<string?> GetAvatarUrlAsync(Guid contactId);
}

/// <summary>
/// Share response deserialized from the ContactShare entity returned by the server.
/// </summary>
public sealed record ContactShareResponse
{
    /// <summary>Share ID.</summary>
    public Guid Id { get; init; }

    /// <summary>Contact ID.</summary>
    public Guid ContactId { get; init; }

    /// <summary>User who shared the contact.</summary>
    public Guid SharedByUserId { get; init; }

    /// <summary>User the contact is shared with.</summary>
    public Guid? SharedWithUserId { get; init; }

    /// <summary>Team the contact is shared with.</summary>
    public Guid? SharedWithTeamId { get; init; }

    /// <summary>Permission level.</summary>
    public string Permission { get; init; } = "ReadOnly";

    /// <summary>When the share was created.</summary>
    public DateTime CreatedAt { get; init; }
}
