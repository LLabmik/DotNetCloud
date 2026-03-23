using DotNetCloud.Core.Authorization;
using DotNetCloud.Core.DTOs;

namespace DotNetCloud.Modules.Contacts.Services;

/// <summary>
/// Manages contact avatar images and general file attachments.
/// </summary>
public interface IContactAvatarService
{
    /// <summary>Uploads or replaces the avatar for a contact.</summary>
    Task<ContactAttachmentDto> UploadAvatarAsync(Guid contactId, Stream fileStream, string fileName, string contentType, CallerContext caller, CancellationToken cancellationToken = default);

    /// <summary>Gets the avatar file stream and content type for a contact. Returns null if no avatar exists.</summary>
    Task<(Stream Stream, string ContentType, string FileName)?> GetAvatarAsync(Guid contactId, CallerContext caller, CancellationToken cancellationToken = default);

    /// <summary>Gets the raw avatar bytes for a contact. Returns null if no avatar exists.</summary>
    Task<(byte[] Data, string ContentType)?> GetAvatarBytesAsync(Guid contactId, CallerContext caller, CancellationToken cancellationToken = default);

    /// <summary>Deletes the avatar for a contact.</summary>
    Task DeleteAvatarAsync(Guid contactId, CallerContext caller, CancellationToken cancellationToken = default);

    /// <summary>Saves an avatar from raw bytes (used during vCard import).</summary>
    Task SaveAvatarFromBytesAsync(Guid contactId, byte[] data, string contentType, CallerContext caller, CancellationToken cancellationToken = default);

    /// <summary>Adds a general file attachment to a contact.</summary>
    Task<ContactAttachmentDto> AddAttachmentAsync(Guid contactId, Stream fileStream, string fileName, string contentType, string? description, CallerContext caller, CancellationToken cancellationToken = default);

    /// <summary>Downloads an attachment by ID. Returns null if not found or not authorized.</summary>
    Task<(Stream Stream, string ContentType, string FileName)?> GetAttachmentAsync(Guid attachmentId, CallerContext caller, CancellationToken cancellationToken = default);

    /// <summary>Deletes an attachment by ID.</summary>
    Task DeleteAttachmentAsync(Guid attachmentId, CallerContext caller, CancellationToken cancellationToken = default);

    /// <summary>Lists all attachments for a contact.</summary>
    Task<IReadOnlyList<ContactAttachmentDto>> ListAttachmentsAsync(Guid contactId, CallerContext caller, CancellationToken cancellationToken = default);
}
