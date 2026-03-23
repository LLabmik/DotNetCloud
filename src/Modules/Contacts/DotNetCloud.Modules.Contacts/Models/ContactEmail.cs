namespace DotNetCloud.Modules.Contacts.Models;

/// <summary>
/// An email address associated with a contact.
/// </summary>
public sealed class ContactEmail
{
    /// <summary>Unique identifier.</summary>
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>Parent contact ID.</summary>
    public Guid ContactId { get; set; }

    /// <summary>Navigation property to the parent contact.</summary>
    public Contact? Contact { get; set; }

    /// <summary>The email address.</summary>
    public required string Address { get; set; }

    /// <summary>Label (e.g., "work", "home", "other").</summary>
    public string Label { get; set; } = "other";

    /// <summary>Whether this is the preferred email address.</summary>
    public bool IsPrimary { get; set; }

    /// <summary>Sort order for display.</summary>
    public int SortOrder { get; set; }
}
