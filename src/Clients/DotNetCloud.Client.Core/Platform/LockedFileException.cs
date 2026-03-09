namespace DotNetCloud.Client.Core.Platform;

/// <summary>
/// Thrown when a file cannot be opened for reading after all tier strategies (shared-read,
/// retry, VSS shadow copy) are exhausted. Caught by the sync engine to defer the
/// sync operation rather than count it as a permanent failure.
/// </summary>
public sealed class LockedFileException : IOException
{
    /// <summary>Full path of the file that could not be read.</summary>
    public string FilePath { get; }

    /// <summary>Initializes a new <see cref="LockedFileException"/>.</summary>
    public LockedFileException(string filePath)
        : base($"File '{filePath}' is locked by another process and could not be read after all retry tiers.")
    {
        FilePath = filePath;
    }
}
