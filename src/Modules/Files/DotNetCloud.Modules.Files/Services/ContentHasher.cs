using System.Numerics;
using System.Security.Cryptography;

namespace DotNetCloud.Modules.Files.Services;

/// <summary>
/// Metadata for a single content-defined chunk produced by
/// <see cref="ContentHasher.ChunkAndHashCdcAsync"/>.
/// </summary>
/// <param name="Hash">Lowercase hex-encoded SHA-256 hash of the chunk content.</param>
/// <param name="Offset">Byte offset of this chunk from the start of the source stream.</param>
/// <param name="Size">Size of this chunk in bytes.</param>
public record CdcChunkInfo(string Hash, long Offset, int Size);

/// <summary>
/// Utility for computing SHA-256 content hashes for file chunks and deduplication.
/// </summary>
public static class ContentHasher
{
    /// <summary>Default chunk size: 4MB.</summary>
    public const int DefaultChunkSize = 4 * 1024 * 1024;

    /// <summary>Default CDC minimum chunk size: 512 KB.</summary>
    public const int DefaultCdcMinSize = 512 * 1024;

    /// <summary>Default CDC maximum chunk size: 16 MB.</summary>
    public const int DefaultCdcMaxSize = 16 * 1024 * 1024;

    // Gear hash lookup table — 256 deterministic 64-bit values (Knuth LCG, seed "DNetCDC").
    // Stability across runtime versions is required; values are fully determined here.
    private static readonly ulong[] GearTable = CreateGearTable();

    private static ulong[] CreateGearTable()
    {
        const ulong multiplier = 6364136223846793005UL;
        const ulong increment = 1442695040888963407UL;
        var seed = 0xDC44636E65744E44UL; // "DNetCDC\0" LE bytes
        var table = new ulong[256];
        for (var i = 0; i < 256; i++)
        {
            seed = (seed * multiplier) + increment;
            table[i] = seed;
        }
        return table;
    }

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
    /// Splits a stream into content-defined chunks using Gear hashing (FastCDC) and computes the
    /// SHA-256 hash of each chunk.  Chunk boundaries are content-dependent, so a 1-byte edit near
    /// the start of a file only invalidates the affected chunk rather than all subsequent chunks.
    /// </summary>
    /// <param name="stream">Source stream to chunk (must support sequential reads).</param>
    /// <param name="avgSize">
    /// Target average chunk size in bytes.  Must be a power of two; defaults to 4 MB.
    /// </param>
    /// <param name="minSize">Minimum chunk size in bytes; defaults to 512 KB.</param>
    /// <param name="maxSize">Maximum chunk size in bytes; defaults to 16 MB.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>
    /// List of <see cref="CdcChunkInfo"/> records in sequence order, each containing the
    /// chunk hash, its byte offset within the stream, and its size.
    /// </returns>
    public static async Task<List<CdcChunkInfo>> ChunkAndHashCdcAsync(
        Stream stream,
        int avgSize = DefaultChunkSize,
        int minSize = DefaultCdcMinSize,
        int maxSize = DefaultCdcMaxSize,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(stream);

        var chunks = new List<CdcChunkInfo>();
        var mask = ComputeGearMask(avgSize);
        ulong gearHash = 0;
        long fileOffset = 0;
        var chunkLen = 0;

        using var hasher = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        var buf = new byte[65536];

        while (true)
        {
            var n = await ReadFullChunkAsync(stream, buf, cancellationToken);
            if (n == 0) break;

            var processed = 0;

            while (processed < n)
            {
                // Phase 1: accumulate up to minSize without checking boundaries.
                if (chunkLen < minSize)
                {
                    var skip = Math.Min(minSize - chunkLen, n - processed);
                    hasher.AppendData(buf, processed, skip);
                    for (var i = 0; i < skip; i++)
                        gearHash = (gearHash << 1) ^ GearTable[buf[processed + i]];
                    chunkLen += skip;
                    processed += skip;
                    continue;
                }

                // Phase 2: roll hash byte-by-byte, look for boundary or hard cutoff.
                var b = buf[processed];
                gearHash = (gearHash << 1) ^ GearTable[b];
                hasher.AppendData(buf, processed, 1);
                chunkLen++;
                processed++;

                if ((gearHash & mask) == 0 || chunkLen >= maxSize)
                    EmitChunk();
            }
        }

        if (chunkLen > 0)
            EmitChunk();

        return chunks;

        void EmitChunk()
        {
            Span<byte> hashBytes = stackalloc byte[32];
            hasher.GetHashAndReset(hashBytes);
            chunks.Add(new CdcChunkInfo(Convert.ToHexStringLower(hashBytes), fileOffset, chunkLen));
            fileOffset += chunkLen;
            chunkLen = 0;
            gearHash = 0;
        }
    }

    /// <summary>
    /// Computes the Gear hash mask for a given average chunk size.
    /// The mask determines boundary probability: P(boundary) ≈ 1 / avgSize.
    /// </summary>
    private static ulong ComputeGearMask(int avgSize)
    {
        var bits = BitOperations.Log2((uint)avgSize);
        return (1UL << bits) - 1;
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
