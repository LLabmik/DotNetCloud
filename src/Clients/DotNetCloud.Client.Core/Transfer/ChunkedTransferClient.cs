using System.Numerics;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Threading.Channels;
using DotNetCloud.Client.Core.Api;
using Microsoft.Extensions.Logging;
using System.Diagnostics;

namespace DotNetCloud.Client.Core.Transfer;

/// <summary>
/// Implements chunked upload and download with SHA-256 deduplication, delta sync, and resumption.
/// </summary>
public sealed class ChunkedTransferClient : IChunkedTransferClient
{
    /// <summary>Default chunk size: 4 MB.</summary>
    public const int DefaultChunkSize = 4 * 1024 * 1024;

    /// <summary>Maximum concurrent chunk transfers.</summary>
    public int MaxConcurrency { get; set; } = 4;

    private readonly IDotNetCloudApiClient _api;
    private readonly ILogger<ChunkedTransferClient> _logger;

    /// <summary>Initializes a new <see cref="ChunkedTransferClient"/>.</summary>
    public ChunkedTransferClient(IDotNetCloudApiClient api, ILogger<ChunkedTransferClient> logger)
    {
        _api = api;
        _logger = logger;
    }

    /// <inheritdoc/>
    public async Task<Guid> UploadAsync(
        Guid? existingNodeId,
        string localPath,
        Stream fileStream,
        IProgress<TransferProgress>? progress,
        CancellationToken cancellationToken = default)
    {
        var fileName = Path.GetFileName(localPath);
        var fileSize = fileStream.Length;
        var mimeType = MimeTypeFromExtension(Path.GetExtension(localPath));
        var uploadTimer = Stopwatch.StartNew();

        _logger.LogInformation(
            "File upload starting: FileName={FileName}, FileSize={FileSize}.",
            fileName,
            fileSize);

        try
        {
            // Pass 1: compute chunk metadata (hashes + sizes) without retaining data.
            // Memory: only the 64 KB read buffer + incremental hash state.
            var metadata = await ComputeChunkMetadataAsync(fileStream, cancellationToken);
            _logger.LogDebug("Uploading {File}: {ChunkCount} CDC chunks, {Size} bytes.", fileName, metadata.Count, fileSize);

            // Initiate session — server returns which chunks it already has
            var session = await _api.InitiateUploadAsync(
                fileName, null, fileSize, mimeType,
                metadata.Select(m => m.Hash).ToList(),
                metadata.Select(m => m.Size).ToList(),
                cancellationToken);

            var presentChunks = new HashSet<string>(session.PresentChunks, StringComparer.OrdinalIgnoreCase);
            var uploaded = 0;
            var skipped = 0;
            var totalChunks = metadata.Count;

            // Pass 2: re-read file via CDC, feed chunks into bounded channel.
            // Peak memory: ChannelCapacity × avg chunk size ≈ 32 MB.
            fileStream.Seek(0, SeekOrigin.Begin);
            var channel = Channel.CreateBounded<(ChunkData Chunk, int Index)>(
                new BoundedChannelOptions(ChannelCapacity) { FullMode = BoundedChannelFullMode.Wait });

            var producer = Task.Run(async () =>
            {
                var index = 0;
                await foreach (var chunk in ChunkFileAsync(fileStream, cancellationToken))
                {
                    await channel.Writer.WriteAsync((chunk, index), cancellationToken);
                    index++;
                }
                channel.Writer.Complete();
            }, cancellationToken);

            var consumers = Enumerable.Range(0, MaxConcurrency).Select(_ => Task.Run(async () =>
            {
                await foreach (var (chunk, index) in channel.Reader.ReadAllAsync(cancellationToken))
                {
                    if (presentChunks.Contains(chunk.Hash))
                    {
                        Interlocked.Increment(ref skipped);
                        _logger.LogDebug("Chunk {Index} hash {Hash} already on server — skipping.", index, chunk.Hash);
                        continue;
                    }

                    var uploadAttempts = 0;
                    for (var attempt = 1; attempt <= ChunkUploadMaxRetries; attempt++)
                    {
                        uploadAttempts = attempt;
                        try
                        {
                            using var chunkStream = new MemoryStream(chunk.Data);
                            await _api.UploadChunkAsync(session.SessionId, index, chunk.Hash, chunkStream, cancellationToken);
                            break;
                        }
                        catch (HttpRequestException ex) when (
                            attempt < ChunkUploadMaxRetries &&
                            (ex.StatusCode is null || (int)ex.StatusCode >= 500))
                        {
                            var delay = TimeSpan.FromSeconds(Math.Pow(2, attempt - 1))
                                        + TimeSpan.FromMilliseconds(Random.Shared.Next(0, 500));
                            _logger.LogWarning(ex,
                                "Chunk {Hash} upload attempt {Attempt}/{MaxAttempts} failed. Retrying in {DelayMs}ms.",
                                chunk.Hash, attempt, ChunkUploadMaxRetries, (int)delay.TotalMilliseconds);
                            await Task.Delay(delay, cancellationToken);
                        }
                    }

                    _logger.LogDebug("Chunk {Hash} upload complete: Attempts={Attempts}.", chunk.Hash, uploadAttempts);

                    var count = Interlocked.Increment(ref uploaded);
                    progress?.Report(new TransferProgress
                    {
                        BytesTransferred = (long)count * DefaultChunkSize,
                        TotalBytes = fileSize,
                        ChunksTransferred = count,
                        TotalChunks = totalChunks,
                        ChunksSkipped = skipped,
                    });
                }
            }, cancellationToken)).ToList();

            await Task.WhenAll(new[] { producer }.Concat(consumers));

            var result = await _api.CompleteUploadAsync(session.SessionId, cancellationToken);
            uploadTimer.Stop();

            _logger.LogInformation(
                "File upload complete: FileName={FileName}, NodeId={NodeId}, FileSize={FileSize}, DurationMs={DurationMs}.",
                fileName,
                result.Node.Id,
                fileSize,
                uploadTimer.ElapsedMilliseconds);

            return result.Node.Id;
        }
        catch (Exception ex)
        {
            uploadTimer.Stop();
            _logger.LogError(
                ex,
                "File upload failed: FileName={FileName}, FileSize={FileSize}, DurationMs={DurationMs}.",
                fileName,
                fileSize,
                uploadTimer.ElapsedMilliseconds);
            throw;
        }
    }

    /// <inheritdoc/>
    public async Task<Stream> DownloadAsync(
        Guid nodeId,
        IProgress<TransferProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        var downloadTimer = Stopwatch.StartNew();

        _logger.LogInformation("File download starting: NodeId={NodeId}.", nodeId);

        try
        {
            var manifest = await _api.GetChunkManifestAsync(nodeId, cancellationToken);

            if (!manifest.Chunks.Any())
            {
                // Small file or no manifest — fall back to direct download
                var direct = await _api.DownloadAsync(nodeId, cancellationToken);
                downloadTimer.Stop();

                _logger.LogInformation(
                    "File download complete: NodeId={NodeId}, FileSize={FileSize}, DurationMs={DurationMs}.",
                    nodeId,
                    manifest.TotalSize,
                    downloadTimer.ElapsedMilliseconds);

                return direct;
            }

            var stream = await DownloadChunksAsync(manifest, progress, cancellationToken);
            downloadTimer.Stop();

            _logger.LogInformation(
                "File download complete: NodeId={NodeId}, FileSize={FileSize}, DurationMs={DurationMs}.",
                nodeId,
                manifest.TotalSize,
                downloadTimer.ElapsedMilliseconds);

            return stream;
        }
        catch (Exception ex)
        {
            downloadTimer.Stop();
            _logger.LogError(
                ex,
                "File download failed: NodeId={NodeId}, DurationMs={DurationMs}.",
                nodeId,
                downloadTimer.ElapsedMilliseconds);
            throw;
        }
    }

    /// <inheritdoc/>
    public async Task<Stream> DownloadVersionAsync(
        Guid nodeId,
        int versionNumber,
        IProgress<TransferProgress>? progress = null,
        CancellationToken cancellationToken = default) =>
        await _api.DownloadVersionAsync(nodeId, versionNumber, cancellationToken);

    // ── Private helpers ─────────────────────────────────────────────────────

    private const int ChunkDownloadMaxAttempts = 3;
    private const int ChunkUploadMaxRetries = 3;

    /// <summary>Bounded channel capacity: limits peak memory to ~32 MB (8 × 4 MB avg).</summary>
    private const int ChannelCapacity = 8;

    private async Task<Stream> DownloadChunksAsync(
        ChunkManifestResponse manifest,
        IProgress<TransferProgress>? progress,
        CancellationToken cancellationToken)
    {
        var chunkCount = manifest.Chunks.Count;
        var tempDir = Path.Combine(Path.GetTempPath(), "dnc-chunks", Guid.NewGuid().ToString("N"));
        var assembledPath = Path.Combine(Path.GetTempPath(), $"dnc-{Guid.NewGuid():N}.tmp");
        Directory.CreateDirectory(tempDir);

        try
        {
            // Download all chunks in parallel (bounded by MaxConcurrency) to individual temp files.
            // Peak memory: one chunk buffer per concurrent download (MaxConcurrency × chunk size).
            var sem = new SemaphoreSlim(MaxConcurrency, MaxConcurrency);
            var downloaded = 0;

            var downloadTasks = manifest.Chunks.Select(async (chunk, index) =>
            {
                await sem.WaitAsync(cancellationToken);
                try
                {
                    byte[]? chunkBytes = null;

                    for (var attempt = 1; attempt <= ChunkDownloadMaxAttempts; attempt++)
                    {
                        try
                        {
                            using var chunkStream = await _api.DownloadChunkByHashAsync(chunk.Hash, cancellationToken);
                            using var ms = new MemoryStream();
                            await chunkStream.CopyToAsync(ms, cancellationToken);
                            var bytes = ms.ToArray();

                            var actualHash = Convert.ToHexStringLower(SHA256.HashData(bytes));
                            if (string.Equals(actualHash, chunk.Hash, StringComparison.OrdinalIgnoreCase))
                            {
                                chunkBytes = bytes;
                                break;
                            }

                            _logger.LogWarning(
                                "Chunk hash mismatch: ExpectedHash={ExpectedHash}, ActualHash={ActualHash}, Attempt={Attempt}/{MaxAttempts}.",
                                chunk.Hash, actualHash, attempt, ChunkDownloadMaxAttempts);

                            if (attempt < ChunkDownloadMaxAttempts)
                            {
                                var delay = TimeSpan.FromSeconds(Math.Pow(2, attempt - 1))
                                            + TimeSpan.FromMilliseconds(Random.Shared.Next(0, 500));
                                await Task.Delay(delay, cancellationToken);
                            }
                        }
                        catch (HttpRequestException ex) when (
                            attempt < ChunkDownloadMaxAttempts &&
                            (ex.StatusCode is null || (int)ex.StatusCode >= 500))
                        {
                            var delay = TimeSpan.FromSeconds(Math.Pow(2, attempt - 1))
                                        + TimeSpan.FromMilliseconds(Random.Shared.Next(0, 500));
                            _logger.LogWarning(ex,
                                "Chunk {Hash} download attempt {Attempt}/{MaxAttempts} failed with network error. Retrying in {DelayMs}ms.",
                                chunk.Hash, attempt, ChunkDownloadMaxAttempts, (int)delay.TotalMilliseconds);
                            await Task.Delay(delay, cancellationToken);
                        }
                    }

                    if (chunkBytes is null)
                    {
                        _logger.LogError(
                            "Chunk integrity verification failed after {MaxAttempts} attempts: ExpectedHash={ExpectedHash}.",
                            ChunkDownloadMaxAttempts, chunk.Hash);
                        throw new ChunkIntegrityException(
                            $"Chunk {chunk.Hash} failed integrity verification after {ChunkDownloadMaxAttempts} download attempts.");
                    }

                    // Write verified chunk to temp file — keeps in-memory usage bounded.
                    var tempPath = Path.Combine(tempDir, index.ToString());
                    await File.WriteAllBytesAsync(tempPath, chunkBytes, cancellationToken);

                    var count = Interlocked.Increment(ref downloaded);
                    progress?.Report(new TransferProgress
                    {
                        BytesTransferred = count * DefaultChunkSize,
                        TotalBytes = manifest.TotalSize,
                        ChunksTransferred = count,
                        TotalChunks = chunkCount,
                    });
                }
                finally
                {
                    sem.Release();
                }
            });

            await Task.WhenAll(downloadTasks);

            // Concatenate temp chunk files into a single assembled file (one chunk in memory at a time).
            using (var output = File.Create(assembledPath))
            {
                for (var i = 0; i < chunkCount; i++)
                {
                    var chunkPath = Path.Combine(tempDir, i.ToString());
                    using var f = File.OpenRead(chunkPath);
                    await f.CopyToAsync(output, cancellationToken);
                }
            }

            // Return file-backed stream — DeleteOnClose removes the file when the caller disposes.
            return new FileStream(assembledPath, FileMode.Open, FileAccess.Read,
                FileShare.None, 4096, FileOptions.DeleteOnClose | FileOptions.Asynchronous);
        }
        catch
        {
            // On error, clean up the assembled file if it was created.
            try { if (File.Exists(assembledPath)) File.Delete(assembledPath); } catch { }
            throw;
        }
        finally
        {
            // Always clean up the per-chunk temp directory.
            try { Directory.Delete(tempDir, recursive: true); }
            catch (Exception ex) { _logger.LogWarning(ex, "Failed to clean up chunk temp dir: {TempDir}.", tempDir); }
        }
    }

    private static string? MimeTypeFromExtension(string ext) => ext.ToLowerInvariant() switch
    {
        ".pdf" => "application/pdf",
        ".jpg" or ".jpeg" => "image/jpeg",
        ".png" => "image/png",
        ".txt" => "text/plain",
        ".docx" => "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
        ".xlsx" => "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
        _ => null,
    };

    // ── CDC chunking (Gear hash / FastCDC) ─────────────────────────────────
    // Constants match server ContentHasher exactly — same seed ⇒ same chunk boundaries.

    private const int CdcMinSize = 512 * 1024;     // 512 KB
    private const int CdcMaxSize = 16 * 1024 * 1024; // 16 MB

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

    private static ulong ComputeGearMask(int avgSize)
    {
        var bits = BitOperations.Log2((uint)avgSize);
        return (1UL << (int)bits) - 1;
    }

    /// <summary>
    /// Computes chunk metadata (hashes and sizes) using CDC without retaining chunk data.
    /// Memory-efficient: only a 64 KB read buffer and incremental hash state are held.
    /// </summary>
    private static async Task<List<ChunkMetadata>> ComputeChunkMetadataAsync(
        Stream stream, CancellationToken cancellationToken)
    {
        var result = new List<ChunkMetadata>();
        var mask = ComputeGearMask(DefaultChunkSize);
        ulong gearHash = 0;
        var chunkLen = 0;

        using var hasher = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        var buf = new byte[65536];
        var hashBuf = new byte[32];

        while (true)
        {
            var n = await ReadFullBufferAsync(stream, buf, cancellationToken);
            if (n == 0) break;

            var processed = 0;
            while (processed < n)
            {
                if (chunkLen < CdcMinSize)
                {
                    var skip = Math.Min(CdcMinSize - chunkLen, n - processed);
                    hasher.AppendData(buf, processed, skip);
                    for (var i = 0; i < skip; i++)
                        gearHash = (gearHash << 1) ^ GearTable[buf[processed + i]];
                    chunkLen += skip;
                    processed += skip;
                    continue;
                }

                gearHash = (gearHash << 1) ^ GearTable[buf[processed]];
                hasher.AppendData(buf, processed, 1);
                chunkLen++;
                processed++;

                if ((gearHash & mask) == 0 || chunkLen >= CdcMaxSize)
                {
                    hasher.GetHashAndReset(hashBuf);
                    result.Add(new ChunkMetadata(Convert.ToHexStringLower(hashBuf), chunkLen));
                    chunkLen = 0;
                    gearHash = 0;
                }
            }
        }

        if (chunkLen > 0)
        {
            hasher.GetHashAndReset(hashBuf);
            result.Add(new ChunkMetadata(Convert.ToHexStringLower(hashBuf), chunkLen));
        }

        return result;
    }

    /// <summary>
    /// Streams file content as CDC chunks via <see cref="IAsyncEnumerable{T}"/>, yielding one chunk at a time.
    /// Only one chunk's data is live at any moment — suitable for feeding a bounded channel.
    /// </summary>
    private static async IAsyncEnumerable<ChunkData> ChunkFileAsync(
        Stream stream, [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        var mask = ComputeGearMask(DefaultChunkSize);
        ulong gearHash = 0;
        var chunkLen = 0;

        using var hasher = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        var chunkAccum = new List<byte[]>();
        var buf = new byte[65536];
        var hashBuf = new byte[32];

        while (true)
        {
            var n = await ReadFullBufferAsync(stream, buf, cancellationToken);
            if (n == 0) break;

            var segStart = 0;
            var processed = 0;

            while (processed < n)
            {
                if (chunkLen < CdcMinSize)
                {
                    var skip = Math.Min(CdcMinSize - chunkLen, n - processed);
                    hasher.AppendData(buf, processed, skip);
                    for (var i = 0; i < skip; i++)
                        gearHash = (gearHash << 1) ^ GearTable[buf[processed + i]];
                    chunkLen += skip;
                    processed += skip;
                    continue;
                }

                gearHash = (gearHash << 1) ^ GearTable[buf[processed]];
                hasher.AppendData(buf, processed, 1);
                chunkLen++;
                processed++;

                if ((gearHash & mask) == 0 || chunkLen >= CdcMaxSize)
                {
                    chunkAccum.Add(buf[segStart..processed].ToArray());
                    segStart = processed;

                    hasher.GetHashAndReset(hashBuf);
                    var hash = Convert.ToHexStringLower(hashBuf);
                    var totalLen = chunkAccum.Sum(s => s.Length);
                    var data = new byte[totalLen];
                    var pos = 0;
                    foreach (var seg in chunkAccum) { seg.CopyTo(data, pos); pos += seg.Length; }

                    yield return new ChunkData { Data = data, Hash = hash };

                    chunkAccum.Clear();
                    chunkLen = 0;
                    gearHash = 0;
                }
            }

            if (n > segStart)
                chunkAccum.Add(buf[segStart..n].ToArray());
        }

        if (chunkLen > 0)
        {
            hasher.GetHashAndReset(hashBuf);
            var hash = Convert.ToHexStringLower(hashBuf);
            var totalLen = chunkAccum.Sum(s => s.Length);
            var data = new byte[totalLen];
            var pos = 0;
            foreach (var seg in chunkAccum) { seg.CopyTo(data, pos); pos += seg.Length; }

            yield return new ChunkData { Data = data, Hash = hash };
        }
    }

    private static async Task<int> ReadFullBufferAsync(Stream stream, byte[] buffer, CancellationToken cancellationToken)
    {
        var totalRead = 0;
        while (totalRead < buffer.Length)
        {
            var n = await stream.ReadAsync(buffer.AsMemory(totalRead), cancellationToken);
            if (n == 0) break;
            totalRead += n;
        }
        return totalRead;
    }

    private sealed class ChunkData
    {
        public required byte[] Data { get; init; }
        public required string Hash { get; init; }
    }

    private record ChunkMetadata(string Hash, int Size);
}
