using DotNetCloud.Core.Authorization;
using DotNetCloud.Modules.Files.DTOs;

namespace DotNetCloud.Modules.Files.Services;

/// <summary>
/// Manages chunked file uploads with deduplication.
/// </summary>
public interface IChunkedUploadService
{
    /// <summary>Initiates a new upload session. Returns which chunks are already stored (dedup).</summary>
    Task<UploadSessionDto> InitiateUploadAsync(InitiateUploadDto dto, CallerContext caller, CancellationToken cancellationToken = default);

    /// <summary>Uploads a single chunk. Verifies hash and writes to storage.</summary>
    Task UploadChunkAsync(Guid sessionId, string chunkHash, ReadOnlyMemory<byte> data, CallerContext caller, CancellationToken cancellationToken = default);

    /// <summary>Completes an upload session: creates file version, updates node, publishes event.</summary>
    Task<FileNodeDto> CompleteUploadAsync(Guid sessionId, CallerContext caller, CancellationToken cancellationToken = default);

    /// <summary>Cancels an upload session.</summary>
    Task CancelUploadAsync(Guid sessionId, CallerContext caller, CancellationToken cancellationToken = default);

    /// <summary>Gets the status of an upload session.</summary>
    Task<UploadSessionDto?> GetSessionAsync(Guid sessionId, CallerContext caller, CancellationToken cancellationToken = default);
}
