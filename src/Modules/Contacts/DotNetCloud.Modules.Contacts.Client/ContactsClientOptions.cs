namespace DotNetCloud.Modules.Contacts.Client;

/// <summary>
/// Configuration options for the Contacts gRPC client.
/// </summary>
public class ContactsClientOptions
{
    /// <summary>
    /// Configuration section name.
    /// </summary>
    public const string SectionName = "ContactsModule";

    /// <summary>
    /// The base address of the Contacts module gRPC host.
    /// </summary>
    public string ContactsModuleAddress { get; set; } = "http://localhost:5000";
}
