namespace DotNetCloud.Modules.Contacts.Models;

/// <summary>
/// A postal address associated with a contact.
/// </summary>
public sealed class ContactAddress
{
    /// <summary>Unique identifier.</summary>
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>Parent contact ID.</summary>
    public Guid ContactId { get; set; }

    /// <summary>Navigation property to the parent contact.</summary>
    public Contact? Contact { get; set; }

    /// <summary>Label (e.g., "home", "work").</summary>
    public string Label { get; set; } = "other";

    /// <summary>Street address lines.</summary>
    public string? Street { get; set; }

    /// <summary>City or locality.</summary>
    public string? City { get; set; }

    /// <summary>State, province, or region.</summary>
    public string? Region { get; set; }

    /// <summary>Postal or ZIP code.</summary>
    public string? PostalCode { get; set; }

    /// <summary>Country name or ISO 3166 code.</summary>
    public string? Country { get; set; }

    /// <summary>Whether this is the preferred address.</summary>
    public bool IsPrimary { get; set; }

    /// <summary>Sort order for display.</summary>
    public int SortOrder { get; set; }
}
