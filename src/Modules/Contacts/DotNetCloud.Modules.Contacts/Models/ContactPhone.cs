namespace DotNetCloud.Modules.Contacts.Models;

/// <summary>
/// A phone number associated with a contact.
/// </summary>
public sealed class ContactPhone
{
    /// <summary>Unique identifier.</summary>
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>Parent contact ID.</summary>
    public Guid ContactId { get; set; }

    /// <summary>Navigation property to the parent contact.</summary>
    public Contact? Contact { get; set; }

    /// <summary>The phone number (E.164 recommended).</summary>
    public required string Number { get; set; }

    /// <summary>Label (e.g., "mobile", "work", "home", "fax").</summary>
    public string Label { get; set; } = "other";

    /// <summary>Whether this is the preferred phone number.</summary>
    public bool IsPrimary { get; set; }

    /// <summary>Sort order for display.</summary>
    public int SortOrder { get; set; }
}
