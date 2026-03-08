namespace DotNetCloud.Modules.Files.Models;

/// <summary>
/// Represents the content-scan status for a file version.
/// </summary>
public enum FileScanStatus
{
    /// <summary>The file has not yet been scanned.</summary>
    NotScanned = 0,

    /// <summary>The scanner found no threats.</summary>
    Clean = 1,

    /// <summary>The scanner detected a threat.</summary>
    Threat = 2,

    /// <summary>Scanning failed with an error.</summary>
    Error = 3,
}
