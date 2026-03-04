using System.Security.Cryptography;

namespace DotNetCloud.Modules.Files.Services;

/// <summary>
/// Utility for computing SHA-256 content hashes for file chunks and deduplication.
/// </summary>
public static class ContentHasher
{
    /// <summary>Default chunk size: 4MB.</summary>
    public const int DefaultChunkSize = 4 * 1024 * 1024;

    /// <summary>
    /// Computes a SHA-256 hash of the given data.
    /// </summary>
    /// <param name="data">Data to hash.</param>
    /// <returns>Lowercase hex-encoded SHA-256 hash.</returns>
    public static string ComputeHash(ReadOnlySpan<byte> data)
    {
        Span<byte> hash = stackalloc byte[32];
        SHA256.HashData(data, hash);
        return Convert.ToHexStringLower(hash);
    }

    /// <summary>
    /// Computes a SHA-256 hash of the given stream.
    /// </summary>
    /// <param name="stream">Stream to hash.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Lowercase hex-encoded SHA-256 hash.</returns>
    public static async Task<string> ComputeHashAsync(Stream stream, CancellationToken cancellationToken = default)
    {
        var hash = await SHA256.HashDataAsync(stream, cancellationToken);
        return Convert.ToHexStringLower(hash);
    }

    /// <summary>
    /// Splits a stream into chunks and computes the SHA-256 hash of each chunk.
    /// </summary>
    /// <param name="stream">Source stream to chunk.</param>
    /// <param name="chunkSize">Size of each chunk in bytes.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>List of (hash, chunkData) tuples in sequence order.</returns>
    public static async Task<List<(string Hash, byte[] Data)>> ChunkAndHashAsync(
        Stream stream,
        int chunkSize = DefaultChunkSize,
        CancellationToken cancellationToken = default)
    {
        var chunks = new List<(string Hash, byte[] Data)>();
        var buffer = new byte[chunkSize];

        while (true)
        {
            var bytesRead = await ReadFullChunkAsync(stream, buffer, cancellationToken);
            if (bytesRead == 0) break;

            var chunkData = buffer[..bytesRead].ToArray();
            var hash = ComputeHash(chunkData);
            chunks.Add((hash, chunkData));
        }

        return chunks;
    }

    /// <summary>
    /// Computes a combined hash from an ordered list of chunk hashes.
    /// Used to create a single content hash for an entire file from its chunk manifest.
    /// </summary>
    /// <param name="chunkHashes">Ordered list of chunk SHA-256 hashes.</param>
    /// <returns>Lowercase hex-encoded SHA-256 hash of the manifest.</returns>
    public static string ComputeManifestHash(IEnumerable<string> chunkHashes)
    {
        var combined = string.Join(":", chunkHashes);
        var hash = SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(combined));
        return Convert.ToHexStringLower(hash);
    }

    /// <summary>
    /// Computes the content-addressable storage path for a chunk based on its hash.
    /// Uses hash prefix directories for balanced distribution.
    /// </summary>
    /// <param name="hash">SHA-256 hash of the chunk.</param>
    /// <returns>Relative storage path (e.g., "chunks/ab/cd/abcdef...").</returns>
    public static string GetChunkStoragePath(string hash)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(hash);
        return $"chunks/{hash[..2]}/{hash[2..4]}/{hash}";
    }

    /// <summary>
    /// Computes the content-addressable storage path for a file based on its manifest hash.
    /// </summary>
    /// <param name="manifestHash">SHA-256 hash of the chunk manifest.</param>
    /// <returns>Relative storage path (e.g., "files/ab/cd/abcdef...").</returns>
    public static string GetFileStoragePath(string manifestHash)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(manifestHash);
        return $"files/{manifestHash[..2]}/{manifestHash[2..4]}/{manifestHash}";
    }

    private static async Task<int> ReadFullChunkAsync(
        Stream stream, byte[] buffer, CancellationToken cancellationToken)
    {
        var totalRead = 0;
        while (totalRead < buffer.Length)
        {
            var bytesRead = await stream.ReadAsync(
                buffer.AsMemory(totalRead, buffer.Length - totalRead),
                cancellationToken);

            if (bytesRead == 0) break;
            totalRead += bytesRead;
        }
        return totalRead;
    }
}
