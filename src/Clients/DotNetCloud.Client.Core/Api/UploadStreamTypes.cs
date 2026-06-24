namespace DotNetCloud.Client.Core.Api;

/// <summary>Metadata for a streaming gRPC file upload.</summary>
public sealed class UploadStreamMetadata
{
    /// <summary>File name (e.g. "report.pdf").</summary>
    public string FileName { get; init; } = string.Empty;

    /// <summary>Optional parent folder ID. Null for root.</summary>
    public Guid? ParentId { get; init; }

    /// <summary>Total file size in bytes.</summary>
    public long TotalSize { get; init; }

    /// <summary>Optional MIME type (e.g. "application/pdf").</summary>
    public string? MimeType { get; init; }

    /// <summary>CDC chunk hashes in order.</summary>
    public IReadOnlyList<string> ChunkHashes { get; init; } = [];

    /// <summary>Optional CDC chunk sizes in bytes. Must match ChunkHashes count.</summary>
    public IReadOnlyList<int>? ChunkSizes { get; init; }

    /// <summary>Optional POSIX mode (Linux clients only).</summary>
    public int? PosixMode { get; init; }

    /// <summary>Optional POSIX owner hint (Linux clients only).</summary>
    public string? PosixOwnerHint { get; init; }
}

/// <summary>A single chunk for streaming upload.</summary>
public sealed class UploadStreamChunk
{
    /// <summary>SHA-256 hash of the chunk data.</summary>
    public string Hash { get; init; } = string.Empty;

    /// <summary>Raw chunk bytes.</summary>
    public byte[] Data { get; init; } = [];

    /// <summary>Chunk index in the file (0-based).</summary>
    public int Index { get; init; }
}
