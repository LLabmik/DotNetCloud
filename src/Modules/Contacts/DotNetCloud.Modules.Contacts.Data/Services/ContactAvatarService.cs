using DotNetCloud.Core.Authorization;
using DotNetCloud.Core.DTOs;
using DotNetCloud.Modules.Contacts.Models;
using DotNetCloud.Modules.Contacts.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace DotNetCloud.Modules.Contacts.Data.Services;

/// <summary>
/// Disk-backed implementation of <see cref="IContactAvatarService"/>.
/// Stores avatar and attachment files under a configurable base directory.
/// </summary>
public sealed class ContactAvatarService : IContactAvatarService
{
    private readonly ContactsDbContext _db;
    private readonly ILogger<ContactAvatarService> _logger;
    private readonly string _storageBasePath;

    /// <summary>Maximum allowed avatar file size (5 MB).</summary>
    private const long MaxAvatarSize = 5 * 1024 * 1024;

    /// <summary>Maximum allowed general attachment size (25 MB).</summary>
    private const long MaxAttachmentSize = 25 * 1024 * 1024;

    private static readonly HashSet<string> AllowedAvatarTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "image/jpeg", "image/png", "image/gif", "image/webp", "image/svg+xml"
    };

    /// <summary>
    /// Initializes a new instance of the <see cref="ContactAvatarService"/> class.
    /// </summary>
    public ContactAvatarService(ContactsDbContext db, ILogger<ContactAvatarService> logger, string storageBasePath)
    {
        _db = db;
        _logger = logger;
        _storageBasePath = storageBasePath;
    }

    /// <inheritdoc />
    public async Task<ContactAttachmentDto> UploadAvatarAsync(
        Guid contactId, Stream fileStream, string fileName, string contentType,
        CallerContext caller, CancellationToken cancellationToken = default)
    {
        ValidateAvatarContentType(contentType);
        var contact = await GetOwnedContactAsync(contactId, caller, cancellationToken);

        // Remove any existing avatar
        var existingAvatar = await _db.ContactAttachments
            .FirstOrDefaultAsync(a => a.ContactId == contactId && a.IsAvatar, cancellationToken);
        if (existingAvatar is not null)
        {
            DeleteFileIfExists(existingAvatar.StoragePath);
            _db.ContactAttachments.Remove(existingAvatar);
        }

        // Save new avatar to disk
        var storagePath = GenerateStoragePath(contactId, fileName, isAvatar: true);
        var fullPath = GetFullPath(storagePath);
        var fileSize = await SaveStreamToFileAsync(fileStream, fullPath, MaxAvatarSize, cancellationToken);

        var attachment = new ContactAttachment
        {
            ContactId = contactId,
            FileName = SanitizeFileName(fileName),
            ContentType = contentType,
            FileSizeBytes = fileSize,
            StoragePath = storagePath,
            IsAvatar = true,
            Description = "Contact avatar"
        };

        _db.ContactAttachments.Add(attachment);

        // Update the contact's AvatarUrl to point to the API endpoint
        contact.AvatarUrl = $"/api/v1/contacts/{contactId}/avatar";
        contact.ETag = Guid.NewGuid().ToString("N");
        contact.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Avatar uploaded for contact {ContactId} by user {UserId}", contactId, caller.UserId);

        return MapToDto(attachment);
    }

    /// <inheritdoc />
    public async Task<(Stream Stream, string ContentType, string FileName)?> GetAvatarAsync(
        Guid contactId, CallerContext caller, CancellationToken cancellationToken = default)
    {
        var avatar = await _db.ContactAttachments
            .Include(a => a.Contact)
            .AsNoTracking()
            .FirstOrDefaultAsync(a => a.ContactId == contactId && a.IsAvatar
                && a.Contact != null
                && (a.Contact.OwnerId == caller.UserId || a.Contact.Shares.Any(s => s.SharedWithUserId == caller.UserId)),
                cancellationToken);

        if (avatar is null)
            return null;

        var fullPath = GetFullPath(avatar.StoragePath);
        if (!File.Exists(fullPath))
            return null;

        return (new FileStream(fullPath, FileMode.Open, FileAccess.Read, FileShare.Read), avatar.ContentType, avatar.FileName);
    }

    /// <inheritdoc />
    public async Task<(byte[] Data, string ContentType)?> GetAvatarBytesAsync(
        Guid contactId, CallerContext caller, CancellationToken cancellationToken = default)
    {
        var avatar = await _db.ContactAttachments
            .Include(a => a.Contact)
            .AsNoTracking()
            .FirstOrDefaultAsync(a => a.ContactId == contactId && a.IsAvatar
                && a.Contact != null
                && (a.Contact.OwnerId == caller.UserId || a.Contact.Shares.Any(s => s.SharedWithUserId == caller.UserId)),
                cancellationToken);

        if (avatar is null)
            return null;

        var fullPath = GetFullPath(avatar.StoragePath);
        if (!File.Exists(fullPath))
            return null;

        var data = await File.ReadAllBytesAsync(fullPath, cancellationToken);
        return (data, avatar.ContentType);
    }

    /// <inheritdoc />
    public async Task DeleteAvatarAsync(Guid contactId, CallerContext caller, CancellationToken cancellationToken = default)
    {
        var contact = await GetOwnedContactAsync(contactId, caller, cancellationToken);

        var avatar = await _db.ContactAttachments
            .FirstOrDefaultAsync(a => a.ContactId == contactId && a.IsAvatar, cancellationToken);

        if (avatar is not null)
        {
            DeleteFileIfExists(avatar.StoragePath);
            _db.ContactAttachments.Remove(avatar);
        }

        contact.AvatarUrl = null;
        contact.ETag = Guid.NewGuid().ToString("N");
        contact.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Avatar deleted for contact {ContactId} by user {UserId}", contactId, caller.UserId);
    }

    /// <inheritdoc />
    public async Task SaveAvatarFromBytesAsync(
        Guid contactId, byte[] data, string contentType,
        CallerContext caller, CancellationToken cancellationToken = default)
    {
        var contact = await GetOwnedContactAsync(contactId, caller, cancellationToken);

        // Remove existing avatar
        var existingAvatar = await _db.ContactAttachments
            .FirstOrDefaultAsync(a => a.ContactId == contactId && a.IsAvatar, cancellationToken);
        if (existingAvatar is not null)
        {
            DeleteFileIfExists(existingAvatar.StoragePath);
            _db.ContactAttachments.Remove(existingAvatar);
        }

        var extension = GetExtensionForContentType(contentType);
        var fileName = $"avatar{extension}";
        var storagePath = GenerateStoragePath(contactId, fileName, isAvatar: true);
        var fullPath = GetFullPath(storagePath);

        Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
        await File.WriteAllBytesAsync(fullPath, data, cancellationToken);

        var attachment = new ContactAttachment
        {
            ContactId = contactId,
            FileName = fileName,
            ContentType = contentType,
            FileSizeBytes = data.Length,
            StoragePath = storagePath,
            IsAvatar = true,
            Description = "Contact avatar (imported)"
        };

        _db.ContactAttachments.Add(attachment);

        contact.AvatarUrl = $"/api/v1/contacts/{contactId}/avatar";
        contact.ETag = Guid.NewGuid().ToString("N");
        contact.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync(cancellationToken);
    }

    /// <inheritdoc />
    public async Task<ContactAttachmentDto> AddAttachmentAsync(
        Guid contactId, Stream fileStream, string fileName, string contentType,
        string? description, CallerContext caller, CancellationToken cancellationToken = default)
    {
        await GetOwnedContactAsync(contactId, caller, cancellationToken);

        var storagePath = GenerateStoragePath(contactId, fileName, isAvatar: false);
        var fullPath = GetFullPath(storagePath);
        var fileSize = await SaveStreamToFileAsync(fileStream, fullPath, MaxAttachmentSize, cancellationToken);

        var attachment = new ContactAttachment
        {
            ContactId = contactId,
            FileName = SanitizeFileName(fileName),
            ContentType = contentType,
            FileSizeBytes = fileSize,
            StoragePath = storagePath,
            IsAvatar = false,
            Description = description
        };

        _db.ContactAttachments.Add(attachment);
        await _db.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Attachment '{FileName}' added to contact {ContactId} by user {UserId}",
            attachment.FileName, contactId, caller.UserId);

        return MapToDto(attachment);
    }

    /// <inheritdoc />
    public async Task<(Stream Stream, string ContentType, string FileName)?> GetAttachmentAsync(
        Guid attachmentId, CallerContext caller, CancellationToken cancellationToken = default)
    {
        var attachment = await _db.ContactAttachments
            .Include(a => a.Contact)
            .AsNoTracking()
            .FirstOrDefaultAsync(a => a.Id == attachmentId
                && a.Contact != null
                && (a.Contact.OwnerId == caller.UserId || a.Contact.Shares.Any(s => s.SharedWithUserId == caller.UserId)),
                cancellationToken);

        if (attachment is null)
            return null;

        var fullPath = GetFullPath(attachment.StoragePath);
        if (!File.Exists(fullPath))
            return null;

        return (new FileStream(fullPath, FileMode.Open, FileAccess.Read, FileShare.Read), attachment.ContentType, attachment.FileName);
    }

    /// <inheritdoc />
    public async Task DeleteAttachmentAsync(Guid attachmentId, CallerContext caller, CancellationToken cancellationToken = default)
    {
        var attachment = await _db.ContactAttachments
            .Include(a => a.Contact)
            .FirstOrDefaultAsync(a => a.Id == attachmentId, cancellationToken)
            ?? throw new Core.Errors.ValidationException(Core.Errors.ErrorCodes.ContactNotFound, "Attachment not found.");

        if (attachment.Contact?.OwnerId != caller.UserId)
            throw new Core.Errors.ValidationException(Core.Errors.ErrorCodes.ContactNotFound, "Attachment not found.");

        DeleteFileIfExists(attachment.StoragePath);
        _db.ContactAttachments.Remove(attachment);

        // If it was an avatar, clear the AvatarUrl
        if (attachment.IsAvatar && attachment.Contact is not null)
        {
            attachment.Contact.AvatarUrl = null;
            attachment.Contact.ETag = Guid.NewGuid().ToString("N");
            attachment.Contact.UpdatedAt = DateTime.UtcNow;
        }

        await _db.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Attachment {AttachmentId} deleted from contact {ContactId} by user {UserId}",
            attachmentId, attachment.ContactId, caller.UserId);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<ContactAttachmentDto>> ListAttachmentsAsync(
        Guid contactId, CallerContext caller, CancellationToken cancellationToken = default)
    {
        var attachments = await _db.ContactAttachments
            .Include(a => a.Contact)
            .AsNoTracking()
            .Where(a => a.ContactId == contactId
                && a.Contact != null
                && (a.Contact.OwnerId == caller.UserId || a.Contact.Shares.Any(s => s.SharedWithUserId == caller.UserId)))
            .OrderBy(a => a.CreatedAt)
            .ToListAsync(cancellationToken);

        return attachments.Select(MapToDto).ToList();
    }

    // ─── Private helpers ───────────────────────────────────────────────

    private async Task<Contact> GetOwnedContactAsync(Guid contactId, CallerContext caller, CancellationToken cancellationToken)
    {
        return await _db.Contacts
            .FirstOrDefaultAsync(c => c.Id == contactId && c.OwnerId == caller.UserId, cancellationToken)
            ?? throw new Core.Errors.ValidationException(Core.Errors.ErrorCodes.ContactNotFound, "Contact not found.");
    }

    private static void ValidateAvatarContentType(string contentType)
    {
        if (!AllowedAvatarTypes.Contains(contentType))
            throw new Core.Errors.ValidationException(
                "ContentType",
                $"Avatar must be an image (jpeg, png, gif, webp, svg). Got: {contentType}");
    }

    private string GenerateStoragePath(Guid contactId, string fileName, bool isAvatar)
    {
        var sanitized = SanitizeFileName(fileName);
        var extension = Path.GetExtension(sanitized);
        var uniqueId = Guid.NewGuid().ToString("N")[..8];
        var subFolder = isAvatar ? "avatars" : "attachments";

        // contacts/{contactId}/avatars/{uniqueId}{ext}  or
        // contacts/{contactId}/attachments/{uniqueId}{ext}
        return Path.Combine("contacts", contactId.ToString(), subFolder, $"{uniqueId}{extension}");
    }

    private string GetFullPath(string storagePath)
    {
        return Path.Combine(_storageBasePath, storagePath);
    }

    private static async Task<long> SaveStreamToFileAsync(Stream source, string fullPath, long maxSize, CancellationToken cancellationToken)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);

        long totalRead = 0;
        var buffer = new byte[8192];

        await using var fileStream = new FileStream(fullPath, FileMode.Create, FileAccess.Write, FileShare.None);

        int bytesRead;
        while ((bytesRead = await source.ReadAsync(buffer, cancellationToken)) > 0)
        {
            totalRead += bytesRead;
            if (totalRead > maxSize)
            {
                // Clean up the partially written file
                await fileStream.DisposeAsync();
                File.Delete(fullPath);
                throw new Core.Errors.ValidationException(
                    "FileSize",
                    $"File exceeds maximum allowed size of {maxSize / (1024 * 1024)} MB.");
            }

            await fileStream.WriteAsync(buffer.AsMemory(0, bytesRead), cancellationToken);
        }

        return totalRead;
    }

    private void DeleteFileIfExists(string storagePath)
    {
        var fullPath = GetFullPath(storagePath);
        if (File.Exists(fullPath))
        {
            File.Delete(fullPath);
        }
    }

    private static string SanitizeFileName(string fileName)
    {
        // Strip path components and invalid chars
        var name = Path.GetFileName(fileName);
        foreach (var c in Path.GetInvalidFileNameChars())
        {
            name = name.Replace(c, '_');
        }
        return string.IsNullOrWhiteSpace(name) ? "file" : name;
    }

    private static string GetExtensionForContentType(string contentType)
    {
        return contentType.ToLowerInvariant() switch
        {
            "image/jpeg" => ".jpg",
            "image/png" => ".png",
            "image/gif" => ".gif",
            "image/webp" => ".webp",
            "image/svg+xml" => ".svg",
            _ => ".bin"
        };
    }

    private static ContactAttachmentDto MapToDto(ContactAttachment a)
    {
        return new ContactAttachmentDto
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
        };
    }
}
