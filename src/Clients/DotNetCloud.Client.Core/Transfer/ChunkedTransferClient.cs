using System.Security.Cryptography;
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
            // Split file into chunks and compute hashes
            var chunks = await SplitIntoChunksAsync(fileStream, cancellationToken);
            _logger.LogDebug("Uploading {File}: {ChunkCount} chunks, {Size} bytes.", fileName, chunks.Count, fileSize);

            // Initiate session — server returns which chunks it already has
            var session = await _api.InitiateUploadAsync(
                fileName, null, fileSize, mimeType,
                chunks.Select(c => c.Hash).ToList(),
                cancellationToken);

            var presentChunks = new HashSet<string>(session.PresentChunks, StringComparer.OrdinalIgnoreCase);
            var sem = new SemaphoreSlim(MaxConcurrency, MaxConcurrency);
            var uploaded = 0;
            var skipped = 0;

            var uploadTasks = chunks.Select(async (chunk, index) =>
            {
                if (presentChunks.Contains(chunk.Hash))
                {
                    Interlocked.Increment(ref skipped);
                    _logger.LogDebug("Chunk {Index} hash {Hash} already on server — skipping.", index, chunk.Hash);
                    return;
                }

                await sem.WaitAsync(cancellationToken);
                try
                {
                    using var chunkStream = new MemoryStream(chunk.Data);
                    await _api.UploadChunkAsync(session.SessionId, index, chunk.Hash, chunkStream, cancellationToken);
                    var count = Interlocked.Increment(ref uploaded);
                    progress?.Report(new TransferProgress
                    {
                        BytesTransferred = (long)count * DefaultChunkSize,
                        TotalBytes = fileSize,
                        ChunksTransferred = count,
                        TotalChunks = chunks.Count,
                        ChunksSkipped = skipped,
                    });
                }
                finally
                {
                    sem.Release();
                }
            });

            await Task.WhenAll(uploadTasks);

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

    private async Task<Stream> DownloadChunksAsync(
        ChunkManifestResponse manifest,
        IProgress<TransferProgress>? progress,
        CancellationToken cancellationToken)
    {
        var chunks = new byte[manifest.Chunks.Count][];

        var sem = new SemaphoreSlim(MaxConcurrency, MaxConcurrency);
        var downloaded = 0;

        var downloadTasks = manifest.Chunks.Select(async (chunk, index) =>
        {
            await sem.WaitAsync(cancellationToken);
            try
            {
                using var chunkStream = await _api.DownloadChunkByHashAsync(chunk.Hash, cancellationToken);
                using var ms = new MemoryStream();
                await chunkStream.CopyToAsync(ms, cancellationToken);
                chunks[index] = ms.ToArray();

                var count = Interlocked.Increment(ref downloaded);
                progress?.Report(new TransferProgress
                {
                    BytesTransferred = count * DefaultChunkSize,
                    TotalBytes = manifest.TotalSize,
                    ChunksTransferred = count,
                    TotalChunks = manifest.Chunks.Count,
                });
            }
            finally
            {
                sem.Release();
            }
        });

        await Task.WhenAll(downloadTasks);

        // Reassemble chunks in order
        var buffer = new MemoryStream();
        foreach (var chunk in chunks)
            await buffer.WriteAsync(chunk, cancellationToken);

        buffer.Seek(0, SeekOrigin.Begin);
        return buffer;
    }

    private static async Task<List<ChunkData>> SplitIntoChunksAsync(Stream stream, CancellationToken cancellationToken)
    {
        var chunks = new List<ChunkData>();
        var buffer = new byte[DefaultChunkSize];

        while (true)
        {
            var bytesRead = 0;
            while (bytesRead < buffer.Length)
            {
                var n = await stream.ReadAsync(buffer.AsMemory(bytesRead), cancellationToken);
                if (n == 0) break;
                bytesRead += n;
            }

            if (bytesRead == 0) break;

            var data = buffer[..bytesRead];
            var hash = Convert.ToHexString(SHA256.HashData(data)).ToLowerInvariant();
            chunks.Add(new ChunkData { Data = data.ToArray(), Hash = hash });
        }

        return chunks;
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

    private sealed class ChunkData
    {
        public required byte[] Data { get; init; }
        public required string Hash { get; init; }
    }
}
