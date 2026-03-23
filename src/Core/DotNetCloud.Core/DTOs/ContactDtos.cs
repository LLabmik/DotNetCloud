namespace DotNetCloud.Core.DTOs;

/// <summary>
/// Represents a contact record (person, organization, or group).
/// </summary>
public sealed record ContactDto
{
    /// <summary>
    /// Unique identifier for the contact.
    /// </summary>
    public required Guid Id { get; init; }

    /// <summary>
    /// Identifier of the user who owns this contact.
    /// </summary>
    public required Guid OwnerId { get; init; }

    /// <summary>
    /// The type of contact entry.
    /// </summary>
    public required ContactType ContactType { get; init; }

    /// <summary>
    /// Formatted display name (e.g., "Jane Doe" or "Acme Corp").
    /// </summary>
    public required string DisplayName { get; init; }

    /// <summary>
    /// Given (first) name. Null for organizations.
    /// </summary>
    public string? FirstName { get; init; }

    /// <summary>
    /// Family (last) name. Null for organizations.
    /// </summary>
    public string? LastName { get; init; }

    /// <summary>
    /// Optional middle name or initial.
    /// </summary>
    public string? MiddleName { get; init; }

    /// <summary>
    /// Name prefix (e.g., "Dr.", "Mr.").
    /// </summary>
    public string? Prefix { get; init; }

    /// <summary>
    /// Name suffix (e.g., "Jr.", "III").
    /// </summary>
    public string? Suffix { get; init; }

    /// <summary>
    /// Phonetic display name for pronunciation support.
    /// </summary>
    public string? PhoneticName { get; init; }

    /// <summary>
    /// Optional nickname or alias.
    /// </summary>
    public string? Nickname { get; init; }

    /// <summary>
    /// Organization or company name for person contacts.
    /// </summary>
    public string? Organization { get; init; }

    /// <summary>
    /// Department within the organization.
    /// </summary>
    public string? Department { get; init; }

    /// <summary>
    /// Job title or role.
    /// </summary>
    public string? JobTitle { get; init; }

    /// <summary>
    /// URL to the contact's avatar image.
    /// </summary>
    public string? AvatarUrl { get; init; }

    /// <summary>
    /// Free-form notes about the contact.
    /// </summary>
    public string? Notes { get; init; }

    /// <summary>
    /// Date of birth.
    /// </summary>
    public DateOnly? Birthday { get; init; }

    /// <summary>
    /// Wedding anniversary date.
    /// </summary>
    public DateOnly? Anniversary { get; init; }

    /// <summary>
    /// Personal website URL.
    /// </summary>
    public string? WebsiteUrl { get; init; }

    /// <summary>
    /// Whether this contact has been soft-deleted.
    /// </summary>
    public bool IsDeleted { get; init; }

    /// <summary>
    /// Timestamp when the contact was deleted, if applicable.
    /// </summary>
    public DateTime? DeletedAt { get; init; }

    /// <summary>
    /// Timestamp when the contact was created.
    /// </summary>
    public required DateTime CreatedAt { get; init; }

    /// <summary>
    /// Timestamp when the contact was last modified.
    /// </summary>
    public required DateTime UpdatedAt { get; init; }

    /// <summary>
    /// Email addresses associated with this contact.
    /// </summary>
    public IReadOnlyList<ContactEmailDto> Emails { get; init; } = [];

    /// <summary>
    /// Phone numbers associated with this contact.
    /// </summary>
    public IReadOnlyList<ContactPhoneDto> PhoneNumbers { get; init; } = [];

    /// <summary>
    /// Postal addresses associated with this contact.
    /// </summary>
    public IReadOnlyList<ContactAddressDto> Addresses { get; init; } = [];

    /// <summary>
    /// File attachments (including avatar) for this contact.
    /// </summary>
    public IReadOnlyList<ContactAttachmentDto> Attachments { get; init; } = [];

    /// <summary>
    /// Group IDs this contact belongs to.
    /// </summary>
    public IReadOnlyList<Guid> GroupIds { get; init; } = [];

    /// <summary>
    /// Arbitrary custom fields (key-value pairs).
    /// </summary>
    public IReadOnlyDictionary<string, string> CustomFields { get; init; } = new Dictionary<string, string>();

    /// <summary>
    /// ETag for CardDAV sync-token / conflict detection.
    /// </summary>
    public string? ETag { get; init; }
}

/// <summary>
/// Classifies a contact entry.
/// </summary>
public enum ContactType
{
    /// <summary>An individual person.</summary>
    Person,

    /// <summary>An organization or company.</summary>
    Organization,

    /// <summary>A named group of contacts.</summary>
    Group
}

/// <summary>
/// An email address associated with a contact.
/// </summary>
public sealed record ContactEmailDto
{
    /// <summary>
    /// The email address.
    /// </summary>
    public required string Address { get; init; }

    /// <summary>
    /// Label describing the email type (e.g., "work", "home", "other").
    /// </summary>
    public string Label { get; init; } = "other";

    /// <summary>
    /// Whether this is the preferred email address.
    /// </summary>
    public bool IsPrimary { get; init; }
}

/// <summary>
/// A phone number associated with a contact.
/// </summary>
public sealed record ContactPhoneDto
{
    /// <summary>
    /// The phone number string (E.164 recommended).
    /// </summary>
    public required string Number { get; init; }

    /// <summary>
    /// Label describing the phone type (e.g., "mobile", "work", "home", "fax").
    /// </summary>
    public string Label { get; init; } = "other";

    /// <summary>
    /// Whether this is the preferred phone number.
    /// </summary>
    public bool IsPrimary { get; init; }
}

/// <summary>
/// A postal address associated with a contact.
/// </summary>
public sealed record ContactAddressDto
{
    /// <summary>
    /// Label describing the address type (e.g., "home", "work").
    /// </summary>
    public string Label { get; init; } = "other";

    /// <summary>
    /// Street address lines.
    /// </summary>
    public string? Street { get; init; }

    /// <summary>
    /// City or locality.
    /// </summary>
    public string? City { get; init; }

    /// <summary>
    /// State, province, or region.
    /// </summary>
    public string? Region { get; init; }

    /// <summary>
    /// Postal or ZIP code.
    /// </summary>
    public string? PostalCode { get; init; }

    /// <summary>
    /// Country name or ISO 3166 code.
    /// </summary>
    public string? Country { get; init; }

    /// <summary>
    /// Whether this is the preferred address.
    /// </summary>
    public bool IsPrimary { get; init; }
}

/// <summary>
/// A named contact group (e.g., "Family", "Colleagues").
/// </summary>
public sealed record ContactGroupDto
{
    /// <summary>
    /// Unique identifier for the group.
    /// </summary>
    public required Guid Id { get; init; }

    /// <summary>
    /// Identifier of the user who owns this group.
    /// </summary>
    public required Guid OwnerId { get; init; }

    /// <summary>
    /// Display name for the group.
    /// </summary>
    public required string Name { get; init; }

    /// <summary>
    /// Number of contacts in the group.
    /// </summary>
    public int MemberCount { get; init; }

    /// <summary>
    /// Timestamp when the group was created.
    /// </summary>
    public required DateTime CreatedAt { get; init; }

    /// <summary>
    /// Timestamp when the group was last modified.
    /// </summary>
    public required DateTime UpdatedAt { get; init; }
}

/// <summary>
/// Request DTO for creating a new contact.
/// </summary>
public sealed record CreateContactDto
{
    /// <summary>
    /// The type of contact to create.
    /// </summary>
    public required ContactType ContactType { get; init; }

    /// <summary>
    /// Display name for the contact.
    /// </summary>
    public required string DisplayName { get; init; }

    /// <summary>
    /// Given (first) name.
    /// </summary>
    public string? FirstName { get; init; }

    /// <summary>
    /// Family (last) name.
    /// </summary>
    public string? LastName { get; init; }

    /// <summary>
    /// Optional middle name or initial.
    /// </summary>
    public string? MiddleName { get; init; }

    /// <summary>
    /// Name prefix.
    /// </summary>
    public string? Prefix { get; init; }

    /// <summary>
    /// Name suffix.
    /// </summary>
    public string? Suffix { get; init; }

    /// <summary>
    /// Organization or company name.
    /// </summary>
    public string? Organization { get; init; }

    /// <summary>
    /// Department within the organization.
    /// </summary>
    public string? Department { get; init; }

    /// <summary>
    /// Job title or role.
    /// </summary>
    public string? JobTitle { get; init; }

    /// <summary>
    /// Free-form notes.
    /// </summary>
    public string? Notes { get; init; }

    /// <summary>
    /// Date of birth.
    /// </summary>
    public DateOnly? Birthday { get; init; }

    /// <summary>
    /// Personal website URL.
    /// </summary>
    public string? WebsiteUrl { get; init; }

    /// <summary>
    /// Email addresses for the contact.
    /// </summary>
    public IReadOnlyList<ContactEmailDto> Emails { get; init; } = [];

    /// <summary>
    /// Phone numbers for the contact.
    /// </summary>
    public IReadOnlyList<ContactPhoneDto> PhoneNumbers { get; init; } = [];

    /// <summary>
    /// Postal addresses for the contact.
    /// </summary>
    public IReadOnlyList<ContactAddressDto> Addresses { get; init; } = [];

    /// <summary>
    /// Arbitrary custom fields.
    /// </summary>
    public IReadOnlyDictionary<string, string> CustomFields { get; init; } = new Dictionary<string, string>();
}

/// <summary>
/// Request DTO for updating an existing contact.
/// Only non-null fields are applied.
/// </summary>
public sealed record UpdateContactDto
{
    /// <summary>
    /// Updated display name.
    /// </summary>
    public string? DisplayName { get; init; }

    /// <summary>
    /// Updated given name.
    /// </summary>
    public string? FirstName { get; init; }

    /// <summary>
    /// Updated family name.
    /// </summary>
    public string? LastName { get; init; }

    /// <summary>
    /// Updated middle name or initial.
    /// </summary>
    public string? MiddleName { get; init; }

    /// <summary>
    /// Updated name prefix.
    /// </summary>
    public string? Prefix { get; init; }

    /// <summary>
    /// Updated name suffix.
    /// </summary>
    public string? Suffix { get; init; }

    /// <summary>
    /// Updated organization name.
    /// </summary>
    public string? Organization { get; init; }

    /// <summary>
    /// Updated department.
    /// </summary>
    public string? Department { get; init; }

    /// <summary>
    /// Updated job title.
    /// </summary>
    public string? JobTitle { get; init; }

    /// <summary>
    /// Updated notes.
    /// </summary>
    public string? Notes { get; init; }

    /// <summary>
    /// Updated date of birth.
    /// </summary>
    public DateOnly? Birthday { get; init; }

    /// <summary>
    /// Updated website URL.
    /// </summary>
    public string? WebsiteUrl { get; init; }

    /// <summary>
    /// Replacement email list. Null means no change.
    /// </summary>
    public IReadOnlyList<ContactEmailDto>? Emails { get; init; }

    /// <summary>
    /// Replacement phone number list. Null means no change.
    /// </summary>
    public IReadOnlyList<ContactPhoneDto>? PhoneNumbers { get; init; }

    /// <summary>
    /// Replacement address list. Null means no change.
    /// </summary>
    public IReadOnlyList<ContactAddressDto>? Addresses { get; init; }

    /// <summary>
    /// Replacement custom fields. Null means no change.
    /// </summary>
    public IReadOnlyDictionary<string, string>? CustomFields { get; init; }
}

/// <summary>
/// Metadata for a file attachment associated with a contact.
/// </summary>
public sealed record ContactAttachmentDto
{
    /// <summary>
    /// Unique identifier for the attachment.
    /// </summary>
    public required Guid Id { get; init; }

    /// <summary>
    /// The contact this attachment belongs to.
    /// </summary>
    public required Guid ContactId { get; init; }

    /// <summary>
    /// Original file name.
    /// </summary>
    public required string FileName { get; init; }

    /// <summary>
    /// MIME content type (e.g., "image/jpeg").
    /// </summary>
    public required string ContentType { get; init; }

    /// <summary>
    /// File size in bytes.
    /// </summary>
    public long FileSizeBytes { get; init; }

    /// <summary>
    /// Whether this attachment is the contact's avatar image.
    /// </summary>
    public bool IsAvatar { get; init; }

    /// <summary>
    /// Optional description or label for the attachment.
    /// </summary>
    public string? Description { get; init; }

    /// <summary>
    /// When the attachment was created (UTC).
    /// </summary>
    public required DateTime CreatedAt { get; init; }

    /// <summary>
    /// When the attachment was last modified (UTC).
    /// </summary>
    public required DateTime UpdatedAt { get; init; }
}
