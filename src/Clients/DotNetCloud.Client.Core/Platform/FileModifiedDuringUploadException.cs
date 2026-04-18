namespace DotNetCloud.Client.Core.Platform;

/// <summary>
/// Thrown when a file's last-write time or size changes between Pass 1 (metadata computation)
/// and Pass 2 (data upload), indicating the file was modified mid-upload.
/// Caught by the sync engine to defer the upload until the file is stable.
/// </summary>
public sealed class FileModifiedDuringUploadException : IOException
{
    /// <summary>Full path of the file that was modified during upload.</summary>
    public string FilePath { get; }

    /// <summary>File size captured before Pass 1.</summary>
    public long OriginalSize { get; }

    /// <summary>File size observed before Pass 2.</summary>
    public long CurrentSize { get; }

    /// <summary>Initializes a new <see cref="FileModifiedDuringUploadException"/>.</summary>
    public FileModifiedDuringUploadException(string filePath, long originalSize, long currentSize)
        : base($"File '{filePath}' was modified between upload passes (size: {originalSize} → {currentSize}).")
    {
        FilePath = filePath;
        OriginalSize = originalSize;
        CurrentSize = currentSize;
    }
}
