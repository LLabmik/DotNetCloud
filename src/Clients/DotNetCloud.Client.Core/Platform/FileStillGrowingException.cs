namespace DotNetCloud.Client.Core.Platform;

/// <summary>
/// Thrown when a file's size changes during a stability check, indicating that
/// another process (e.g. <c>dd</c>, a download manager) is still writing to it.
/// Caught by the sync engine to defer the upload until the file is stable.
/// </summary>
public sealed class FileStillGrowingException : IOException
{
    /// <summary>Full path of the file that is still being written.</summary>
    public string FilePath { get; }

    /// <summary>File size at the start of the stability check.</summary>
    public long InitialSize { get; }

    /// <summary>File size at the end of the stability check.</summary>
    public long FinalSize { get; }

    /// <summary>Initializes a new <see cref="FileStillGrowingException"/>.</summary>
    public FileStillGrowingException(string filePath, long initialSize, long finalSize)
        : base($"File '{filePath}' is still being written (size changed from {initialSize} to {finalSize} during stability check).")
    {
        FilePath = filePath;
        InitialSize = initialSize;
        FinalSize = finalSize;
    }
}
