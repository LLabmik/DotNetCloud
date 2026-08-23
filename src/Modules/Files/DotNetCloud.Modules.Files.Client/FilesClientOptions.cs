namespace DotNetCloud.Modules.Files.Client;

/// <summary>
/// Configuration options for the Files gRPC client.
/// </summary>
public class FilesClientOptions
{
    /// <summary>
    /// Configuration section name.
    /// </summary>
    public const string SectionName = "FilesModule";

    /// <summary>
    /// The base address of the Files module gRPC host.
    /// </summary>
    public string FilesModuleAddress { get; set; } = "http://localhost:5000";
}
