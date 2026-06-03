namespace DotNetCloud.Modules.Calendar.Host.Configuration;

/// <summary>
/// Configuration options for the Contacts gRPC client used by the Calendar module.
/// </summary>
public sealed class ContactsGrpcClientOptions
{
    /// <summary>
    /// The section name used in configuration (e.g., appsettings.json).
    /// </summary>
    public const string SectionName = "ContactsGrpc";

    /// <summary>
    /// The gRPC address of the Contacts module (e.g., "http://localhost:5002", "unix:///var/run/dotnetcloud/contacts.sock").
    /// </summary>
    public string ContactsModuleAddress { get; set; } = "http://localhost:5002";

    /// <summary>
    /// Timeout for gRPC calls to the Contacts module. Default: 5 seconds.
    /// </summary>
    public TimeSpan Timeout { get; set; } = TimeSpan.FromSeconds(5);
}
