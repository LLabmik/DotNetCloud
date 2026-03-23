namespace DotNetCloud.Modules.Contacts.Models;

/// <summary>
/// Join table linking a contact to a group.
/// </summary>
public sealed class ContactGroupMember
{
    /// <summary>Group ID.</summary>
    public Guid GroupId { get; set; }

    /// <summary>Navigation property to the group.</summary>
    public ContactGroup? Group { get; set; }

    /// <summary>Contact ID.</summary>
    public Guid ContactId { get; set; }

    /// <summary>Navigation property to the contact.</summary>
    public Contact? Contact { get; set; }

    /// <summary>When the contact was added to the group (UTC).</summary>
    public DateTime AddedAt { get; set; } = DateTime.UtcNow;
}
