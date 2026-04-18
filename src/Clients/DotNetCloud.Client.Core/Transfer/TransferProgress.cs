namespace DotNetCloud.Client.Core.Transfer;

/// <summary>
/// Indicates the current phase of a chunked transfer operation.
/// </summary>
public enum TransferPhase
{
    /// <summary>Computing chunk hashes (pre-upload scan).</summary>
    Hashing,

    /// <summary>Uploading or downloading chunk data.</summary>
    Transferring,
}

/// <summary>
/// Reports progress for a chunked transfer operation.
/// </summary>
public sealed class TransferProgress
{
    /// <summary>Current phase of the transfer.</summary>
    public TransferPhase Phase { get; init; } = TransferPhase.Transferring;

    /// <summary>Bytes transferred so far.</summary>
    public long BytesTransferred { get; init; }

    /// <summary>Total bytes to transfer.</summary>
    public long TotalBytes { get; init; }

    /// <summary>Number of chunks transferred so far.</summary>
    public int ChunksTransferred { get; init; }

    /// <summary>Total number of chunks.</summary>
    public int TotalChunks { get; init; }

    /// <summary>Number of chunks skipped due to deduplication.</summary>
    public int ChunksSkipped { get; init; }

    /// <summary>Percentage complete (0–100).</summary>
    public double PercentComplete => TotalBytes > 0 ? BytesTransferred * 100.0 / TotalBytes : 0;
}
