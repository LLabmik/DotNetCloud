namespace DotNetCloud.Modules.Contacts.Models;

/// <summary>
/// Represents a contact record (person, organization, or group).
/// </summary>
public sealed class Contact
{
    /// <summary>Unique identifier for this contact.</summary>
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>User who owns this contact.</summary>
    public Guid OwnerId { get; set; }

    /// <summary>Contact type (Person, Organization, Group).</summary>
    public Core.DTOs.ContactType ContactType { get; set; } = Core.DTOs.ContactType.Person;

    /// <summary>Formatted display name.</summary>
    public required string DisplayName { get; set; }

    /// <summary>Given (first) name.</summary>
    public string? FirstName { get; set; }

    /// <summary>Family (last) name.</summary>
    public string? LastName { get; set; }

    /// <summary>Middle name or initial.</summary>
    public string? MiddleName { get; set; }

    /// <summary>Name prefix (e.g., "Dr.").</summary>
    public string? Prefix { get; set; }

    /// <summary>Name suffix (e.g., "Jr.").</summary>
    public string? Suffix { get; set; }

    /// <summary>Phonetic display name for pronunciation.</summary>
    public string? PhoneticName { get; set; }

    /// <summary>Nickname or alias.</summary>
    public string? Nickname { get; set; }

    /// <summary>Organization or company name.</summary>
    public string? Organization { get; set; }

    /// <summary>Department within the organization.</summary>
    public string? Department { get; set; }

    /// <summary>Job title or role.</summary>
    public string? JobTitle { get; set; }

    /// <summary>URL to the contact's avatar image.</summary>
    public string? AvatarUrl { get; set; }

    /// <summary>Free-form notes about the contact.</summary>
    public string? Notes { get; set; }

    /// <summary>Date of birth.</summary>
    public DateOnly? Birthday { get; set; }

    /// <summary>Wedding anniversary date.</summary>
    public DateOnly? Anniversary { get; set; }

    /// <summary>Personal website URL.</summary>
    public string? WebsiteUrl { get; set; }

    /// <summary>ETag for CardDAV sync-token / conflict detection.</summary>
    public string ETag { get; set; } = Guid.NewGuid().ToString("N");

    /// <summary>Whether this contact has been soft-deleted.</summary>
    public bool IsDeleted { get; set; }

    /// <summary>When the contact was soft-deleted (UTC).</summary>
    public DateTime? DeletedAt { get; set; }

    /// <summary>When the contact was created (UTC).</summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>When the contact was last modified (UTC).</summary>
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>User ID who created this contact record (for audit).</summary>
    public Guid? CreatedByUserId { get; set; }

    /// <summary>User ID who last updated this contact record (for audit).</summary>
    public Guid? UpdatedByUserId { get; set; }

    /// <summary>Email addresses for this contact.</summary>
    public ICollection<ContactEmail> Emails { get; set; } = [];

    /// <summary>Phone numbers for this contact.</summary>
    public ICollection<ContactPhone> PhoneNumbers { get; set; } = [];

    /// <summary>Postal addresses for this contact.</summary>
    public ICollection<ContactAddress> Addresses { get; set; } = [];

    /// <summary>Custom fields (key-value pairs).</summary>
    public ICollection<ContactCustomField> CustomFields { get; set; } = [];

    /// <summary>Group memberships for this contact.</summary>
    public ICollection<ContactGroupMember> GroupMemberships { get; set; } = [];

    /// <summary>Shares granting access to this contact.</summary>
    public ICollection<ContactShare> Shares { get; set; } = [];

    /// <summary>File attachments (including avatar) for this contact.</summary>
    public ICollection<ContactAttachment> Attachments { get; set; } = [];
}
