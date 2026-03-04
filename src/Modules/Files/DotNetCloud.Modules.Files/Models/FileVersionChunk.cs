namespace DotNetCloud.Modules.Files.Models;

/// <summary>
/// Maps which chunks compose a specific file version, and in what order.
/// A single file version is reconstructed by concatenating its chunks in sequence order.
/// </summary>
public sealed class FileVersionChunk
{
    /// <summary>The file version this mapping belongs to.</summary>
    public Guid FileVersionId { get; set; }

    /// <summary>Navigation property to the file version.</summary>
    public FileVersion? FileVersion { get; set; }

    /// <summary>The chunk referenced by this mapping.</summary>
    public Guid FileChunkId { get; set; }

    /// <summary>Navigation property to the chunk.</summary>
    public FileChunk? FileChunk { get; set; }

    /// <summary>
    /// Zero-based sequence index indicating this chunk's position in the file.
    /// Chunks are concatenated in ascending sequence order to reconstruct the file.
    /// </summary>
    public int SequenceIndex { get; set; }
}
