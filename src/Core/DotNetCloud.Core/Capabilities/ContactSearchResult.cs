namespace DotNetCloud.Core.Capabilities;

/// <summary>
/// Represents a contact search result with email addresses, for use in autocomplete and lookups.
/// </summary>
public sealed record ContactSearchResult
{
    /// <summary>
    /// The unique identifier of the contact.
    /// </summary>
    public required Guid ContactId { get; init; }

    /// <summary>
    /// The display name of the contact.
    /// </summary>
    public required string DisplayName { get; init; }

    /// <summary>
    /// Email addresses associated with this contact.
    /// Each entry contains the address and its label (e.g., "work", "home", "other").
    /// </summary>
    public required IReadOnlyList<(string Address, string Label)> Emails { get; init; }
}
