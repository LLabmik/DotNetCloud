namespace DotNetCloud.Modules.Contacts.Models;

/// <summary>
/// A custom key-value field on a contact.
/// </summary>
public sealed class ContactCustomField
{
    /// <summary>Unique identifier.</summary>
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>Parent contact ID.</summary>
    public Guid ContactId { get; set; }

    /// <summary>Navigation property to the parent contact.</summary>
    public Contact? Contact { get; set; }

    /// <summary>Field key/name.</summary>
    public required string Key { get; set; }

    /// <summary>Field value.</summary>
    public required string Value { get; set; }
}
