using System.Text;
using DotNetCloud.Core.Authorization;
using DotNetCloud.Core.DTOs;
using DotNetCloud.Modules.Contacts.Models;
using DotNetCloud.Modules.Contacts.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace DotNetCloud.Modules.Contacts.Data.Services;

/// <summary>
/// Database-backed implementation of <see cref="IVCardService"/>.
/// Supports vCard 3.0 (RFC 2426) import/export with PHOTO support.
/// </summary>
public sealed class VCardService : IVCardService
{
    private readonly ContactsDbContext _db;
    private readonly IContactService _contactService;
    private readonly IContactAvatarService _avatarService;
    private readonly ILogger<VCardService> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="VCardService"/> class.
    /// </summary>
    public VCardService(ContactsDbContext db, IContactService contactService, IContactAvatarService avatarService, ILogger<VCardService> logger)
    {
        _db = db;
        _contactService = contactService;
        _avatarService = avatarService;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<string> ExportVCardAsync(Guid contactId, CallerContext caller, CancellationToken cancellationToken = default)
    {
        var contact = await _db.Contacts
            .Include(c => c.Emails)
            .Include(c => c.PhoneNumbers)
            .Include(c => c.Addresses)
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.Id == contactId && c.OwnerId == caller.UserId, cancellationToken)
            ?? throw new Core.Errors.ValidationException(Core.Errors.ErrorCodes.ContactNotFound, "Contact not found.");

        var avatarData = await _avatarService.GetAvatarBytesAsync(contactId, caller, cancellationToken);

        return SerializeVCard(contact, avatarData?.Data, avatarData?.ContentType);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<Guid>> ImportVCardsAsync(string vCardText, CallerContext caller, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(vCardText);

        var vcards = ParseVCards(vCardText);
        var createdIds = new List<Guid>();

        foreach (var (dto, photoData, photoContentType) in vcards)
        {
            var created = await _contactService.CreateContactAsync(dto, caller, cancellationToken);
            createdIds.Add(created.Id);

            // Import avatar if PHOTO data was present
            if (photoData is not null && photoContentType is not null)
            {
                try
                {
                    await _avatarService.SaveAvatarFromBytesAsync(created.Id, photoData, photoContentType, caller, cancellationToken);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to import avatar for contact {ContactId}", created.Id);
                }
            }
        }

        _logger.LogInformation("Imported {Count} vCards for user {UserId}", createdIds.Count, caller.UserId);
        return createdIds;
    }

    /// <inheritdoc />
    public async Task<string> ExportAllVCardsAsync(CallerContext caller, CancellationToken cancellationToken = default)
    {
        var contacts = await _db.Contacts
            .Include(c => c.Emails)
            .Include(c => c.PhoneNumbers)
            .Include(c => c.Addresses)
            .AsNoTracking()
            .Where(c => c.OwnerId == caller.UserId)
            .OrderBy(c => c.DisplayName)
            .ToListAsync(cancellationToken);

        var sb = new StringBuilder();
        foreach (var contact in contacts)
        {
            var avatarData = await _avatarService.GetAvatarBytesAsync(contact.Id, caller, cancellationToken);
            sb.Append(SerializeVCard(contact, avatarData?.Data, avatarData?.ContentType));
        }

        return sb.ToString();
    }

    private static string SerializeVCard(Contact contact, byte[]? avatarData = null, string? avatarContentType = null)
    {
        var sb = new StringBuilder();
        sb.AppendLine("BEGIN:VCARD");
        sb.AppendLine("VERSION:3.0");
        sb.Append("FN:").AppendLine(EscapeVCardValue(contact.DisplayName));

        if (contact.LastName is not null || contact.FirstName is not null)
        {
            sb.Append("N:")
              .Append(EscapeVCardValue(contact.LastName ?? ""))
              .Append(';')
              .Append(EscapeVCardValue(contact.FirstName ?? ""))
              .Append(';')
              .Append(EscapeVCardValue(contact.MiddleName ?? ""))
              .Append(';')
              .Append(EscapeVCardValue(contact.Prefix ?? ""))
              .Append(';')
              .AppendLine(EscapeVCardValue(contact.Suffix ?? ""));
        }

        if (contact.Nickname is not null)
            sb.Append("NICKNAME:").AppendLine(EscapeVCardValue(contact.Nickname));

        if (contact.Organization is not null)
        {
            sb.Append("ORG:")
              .Append(EscapeVCardValue(contact.Organization));
            if (contact.Department is not null)
                sb.Append(';').Append(EscapeVCardValue(contact.Department));
            sb.AppendLine();
        }

        if (contact.JobTitle is not null)
            sb.Append("TITLE:").AppendLine(EscapeVCardValue(contact.JobTitle));

        foreach (var email in contact.Emails)
        {
            sb.Append("EMAIL;TYPE=").Append(email.Label.ToUpperInvariant());
            if (email.IsPrimary) sb.Append(";TYPE=PREF");
            sb.Append(':').AppendLine(EscapeVCardValue(email.Address));
        }

        foreach (var phone in contact.PhoneNumbers)
        {
            sb.Append("TEL;TYPE=").Append(phone.Label.ToUpperInvariant());
            if (phone.IsPrimary) sb.Append(";TYPE=PREF");
            sb.Append(':').AppendLine(EscapeVCardValue(phone.Number));
        }

        foreach (var addr in contact.Addresses)
        {
            sb.Append("ADR;TYPE=").Append(addr.Label.ToUpperInvariant());
            if (addr.IsPrimary) sb.Append(";TYPE=PREF");
            sb.Append(":;;")
              .Append(EscapeVCardValue(addr.Street ?? ""))
              .Append(';')
              .Append(EscapeVCardValue(addr.City ?? ""))
              .Append(';')
              .Append(EscapeVCardValue(addr.Region ?? ""))
              .Append(';')
              .Append(EscapeVCardValue(addr.PostalCode ?? ""))
              .Append(';')
              .AppendLine(EscapeVCardValue(addr.Country ?? ""));
        }

        if (contact.Birthday.HasValue)
            sb.Append("BDAY:").AppendLine(contact.Birthday.Value.ToString("yyyy-MM-dd"));

        if (contact.WebsiteUrl is not null)
            sb.Append("URL:").AppendLine(EscapeVCardValue(contact.WebsiteUrl));

        if (contact.Notes is not null)
            sb.Append("NOTE:").AppendLine(EscapeVCardValue(contact.Notes));

        // PHOTO property (base64-encoded inline image)
        if (avatarData is not null && avatarContentType is not null)
        {
            var imageType = avatarContentType.ToUpperInvariant() switch
            {
                "IMAGE/JPEG" => "JPEG",
                "IMAGE/PNG" => "PNG",
                "IMAGE/GIF" => "GIF",
                "IMAGE/WEBP" => "WEBP",
                _ => "JPEG"
            };
            var base64 = Convert.ToBase64String(avatarData);
            sb.Append("PHOTO;ENCODING=b;TYPE=").Append(imageType).Append(':').AppendLine(base64);
        }

        sb.Append("UID:").AppendLine(contact.Id.ToString());
        sb.Append("REV:").AppendLine(contact.UpdatedAt.ToString("yyyyMMddTHHmmssZ"));
        sb.AppendLine("END:VCARD");

        return sb.ToString();
    }

    private static IReadOnlyList<(CreateContactDto Dto, byte[]? PhotoData, string? PhotoContentType)> ParseVCards(string text)
    {
        var results = new List<(CreateContactDto, byte[]?, string?)>();
        var lines = text.Split('\n', StringSplitOptions.TrimEntries);

        string? displayName = null;
        string? firstName = null;
        string? lastName = null;
        string? middleName = null;
        string? prefix = null;
        string? suffix = null;
        string? org = null;
        string? dept = null;
        string? jobTitle = null;
        string? notes = null;
        string? websiteUrl = null;
        DateOnly? birthday = null;
        byte[]? photoData = null;
        string? photoContentType = null;
        var emails = new List<ContactEmailDto>();
        var phones = new List<ContactPhoneDto>();
        var addresses = new List<ContactAddressDto>();
        bool inVCard = false;

        foreach (var line in lines)
        {
            if (line.Equals("BEGIN:VCARD", StringComparison.OrdinalIgnoreCase))
            {
                inVCard = true;
                displayName = null; firstName = null; lastName = null; middleName = null;
                prefix = null; suffix = null; org = null; dept = null; jobTitle = null;
                notes = null; websiteUrl = null; birthday = null;
                photoData = null; photoContentType = null;
                emails = []; phones = []; addresses = [];
                continue;
            }

            if (line.Equals("END:VCARD", StringComparison.OrdinalIgnoreCase))
            {
                if (inVCard && displayName is not null)
                {
                    results.Add((new CreateContactDto
                    {
                        ContactType = ContactType.Person,
                        DisplayName = displayName,
                        FirstName = firstName,
                        LastName = lastName,
                        MiddleName = middleName,
                        Prefix = prefix,
                        Suffix = suffix,
                        Organization = org,
                        Department = dept,
                        JobTitle = jobTitle,
                        Notes = notes,
                        Birthday = birthday,
                        WebsiteUrl = websiteUrl,
                        Emails = emails,
                        PhoneNumbers = phones,
                        Addresses = addresses
                    }, photoData, photoContentType));
                }
                inVCard = false;
                continue;
            }

            if (!inVCard) continue;

            if (line.StartsWith("FN:", StringComparison.OrdinalIgnoreCase))
            {
                displayName = UnescapeVCardValue(line[3..]);
            }
            else if (line.StartsWith("N:", StringComparison.OrdinalIgnoreCase) || line.StartsWith("N;", StringComparison.OrdinalIgnoreCase))
            {
                var colonIdx = line.IndexOf(':');
                if (colonIdx >= 0)
                {
                    var parts = line[(colonIdx + 1)..].Split(';');
                    if (parts.Length > 0) lastName = NullIfEmpty(UnescapeVCardValue(parts[0]));
                    if (parts.Length > 1) firstName = NullIfEmpty(UnescapeVCardValue(parts[1]));
                    if (parts.Length > 2) middleName = NullIfEmpty(UnescapeVCardValue(parts[2]));
                    if (parts.Length > 3) prefix = NullIfEmpty(UnescapeVCardValue(parts[3]));
                    if (parts.Length > 4) suffix = NullIfEmpty(UnescapeVCardValue(parts[4]));
                }
            }
            else if (line.StartsWith("ORG:", StringComparison.OrdinalIgnoreCase) || line.StartsWith("ORG;", StringComparison.OrdinalIgnoreCase))
            {
                var colonIdx = line.IndexOf(':');
                if (colonIdx >= 0)
                {
                    var parts = line[(colonIdx + 1)..].Split(';');
                    org = NullIfEmpty(UnescapeVCardValue(parts[0]));
                    if (parts.Length > 1) dept = NullIfEmpty(UnescapeVCardValue(parts[1]));
                }
            }
            else if (line.StartsWith("TITLE:", StringComparison.OrdinalIgnoreCase))
            {
                jobTitle = UnescapeVCardValue(line[6..]);
            }
            else if (line.StartsWith("EMAIL", StringComparison.OrdinalIgnoreCase))
            {
                var colonIdx = line.IndexOf(':');
                if (colonIdx >= 0)
                {
                    var paramPart = line[..colonIdx].ToUpperInvariant();
                    var address = UnescapeVCardValue(line[(colonIdx + 1)..]);
                    var label = paramPart.Contains("WORK") ? "work" : paramPart.Contains("HOME") ? "home" : "other";
                    var isPrimary = paramPart.Contains("PREF");
                    emails.Add(new ContactEmailDto { Address = address, Label = label, IsPrimary = isPrimary });
                }
            }
            else if (line.StartsWith("TEL", StringComparison.OrdinalIgnoreCase))
            {
                var colonIdx = line.IndexOf(':');
                if (colonIdx >= 0)
                {
                    var paramPart = line[..colonIdx].ToUpperInvariant();
                    var number = UnescapeVCardValue(line[(colonIdx + 1)..]);
                    var label = paramPart.Contains("CELL") || paramPart.Contains("MOBILE") ? "mobile"
                        : paramPart.Contains("WORK") ? "work"
                        : paramPart.Contains("HOME") ? "home"
                        : paramPart.Contains("FAX") ? "fax"
                        : "other";
                    var isPrimary = paramPart.Contains("PREF");
                    phones.Add(new ContactPhoneDto { Number = number, Label = label, IsPrimary = isPrimary });
                }
            }
            else if (line.StartsWith("ADR", StringComparison.OrdinalIgnoreCase))
            {
                var colonIdx = line.IndexOf(':');
                if (colonIdx >= 0)
                {
                    var paramPart = line[..colonIdx].ToUpperInvariant();
                    var parts = line[(colonIdx + 1)..].Split(';');
                    var label = paramPart.Contains("WORK") ? "work" : paramPart.Contains("HOME") ? "home" : "other";
                    var isPrimary = paramPart.Contains("PREF");
                    addresses.Add(new ContactAddressDto
                    {
                        Label = label,
                        Street = parts.Length > 2 ? NullIfEmpty(UnescapeVCardValue(parts[2])) : null,
                        City = parts.Length > 3 ? NullIfEmpty(UnescapeVCardValue(parts[3])) : null,
                        Region = parts.Length > 4 ? NullIfEmpty(UnescapeVCardValue(parts[4])) : null,
                        PostalCode = parts.Length > 5 ? NullIfEmpty(UnescapeVCardValue(parts[5])) : null,
                        Country = parts.Length > 6 ? NullIfEmpty(UnescapeVCardValue(parts[6])) : null,
                        IsPrimary = isPrimary
                    });
                }
            }
            else if (line.StartsWith("BDAY:", StringComparison.OrdinalIgnoreCase))
            {
                if (DateOnly.TryParse(line[5..], out var bday))
                    birthday = bday;
            }
            else if (line.StartsWith("URL:", StringComparison.OrdinalIgnoreCase))
            {
                websiteUrl = UnescapeVCardValue(line[4..]);
            }
            else if (line.StartsWith("NOTE:", StringComparison.OrdinalIgnoreCase))
            {
                notes = UnescapeVCardValue(line[5..]);
            }
            else if (line.StartsWith("PHOTO", StringComparison.OrdinalIgnoreCase))
            {
                // Parse PHOTO;ENCODING=b;TYPE=JPEG:base64data
                var colonIdx = line.IndexOf(':');
                if (colonIdx >= 0)
                {
                    var paramPart = line[..colonIdx].ToUpperInvariant();
                    var data = line[(colonIdx + 1)..];

                    // Only handle base64-encoded photos
                    if (paramPart.Contains("ENCODING=B") || paramPart.Contains("ENCODING=BASE64"))
                    {
                        try
                        {
                            photoData = Convert.FromBase64String(data);
                            photoContentType = paramPart.Contains("TYPE=PNG") ? "image/png"
                                : paramPart.Contains("TYPE=GIF") ? "image/gif"
                                : paramPart.Contains("TYPE=WEBP") ? "image/webp"
                                : "image/jpeg";
                        }
                        catch (FormatException)
                        {
                            // Invalid base64; skip the photo
                        }
                    }
                }
            }
        }

        return results;
    }

    private static string EscapeVCardValue(string value)
    {
        return value
            .Replace("\\", "\\\\")
            .Replace(",", "\\,")
            .Replace(";", "\\;")
            .Replace("\n", "\\n");
    }

    private static string UnescapeVCardValue(string value)
    {
        return value
            .Replace("\\n", "\n")
            .Replace("\\;", ";")
            .Replace("\\,", ",")
            .Replace("\\\\", "\\");
    }

    private static string? NullIfEmpty(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value;
}
