namespace DotNetCloud.Client.Core.Api;

/// <summary>
/// HTTP API client for communicating with a DotNetCloud server.
/// </summary>
public interface IDotNetCloudApiClient
{
    /// <summary>Access token used for all authenticated requests.</summary>
    string? AccessToken { get; set; }

    // ── Authentication ──────────────────────────────────────────────────────

    /// <summary>Exchanges an OAuth2 authorization code for tokens.</summary>
    Task<TokenResponse> ExchangeCodeAsync(string code, string codeVerifier, string redirectUri, CancellationToken cancellationToken = default);

    /// <summary>Refreshes an access token using the given refresh token.</summary>
    Task<TokenResponse> RefreshTokenAsync(string refreshToken, string clientId, CancellationToken cancellationToken = default);

    /// <summary>Revokes the given token (access or refresh).</summary>
    Task RevokeTokenAsync(string token, CancellationToken cancellationToken = default);

    // ── File Operations ─────────────────────────────────────────────────────

    /// <summary>Lists children of a folder (null = root).</summary>
    Task<IReadOnlyList<FileNodeResponse>> ListChildrenAsync(Guid? folderId, CancellationToken cancellationToken = default);

    /// <summary>Gets metadata for a specific node.</summary>
    Task<FileNodeResponse> GetNodeAsync(Guid nodeId, CancellationToken cancellationToken = default);

    /// <summary>Creates a new folder.</summary>
    Task<FileNodeResponse> CreateFolderAsync(string name, Guid? parentId, CancellationToken cancellationToken = default);

    /// <summary>Renames a file or folder.</summary>
    Task<FileNodeResponse> RenameAsync(Guid nodeId, string newName, CancellationToken cancellationToken = default);

    /// <summary>Moves a file or folder to a new parent.</summary>
    Task<FileNodeResponse> MoveAsync(Guid nodeId, Guid targetParentId, CancellationToken cancellationToken = default);

    /// <summary>Copies a file or folder to a new parent.</summary>
    Task<FileNodeResponse> CopyAsync(Guid nodeId, Guid targetParentId, CancellationToken cancellationToken = default);

    /// <summary>Soft-deletes (trashes) a file or folder.</summary>
    Task DeleteAsync(Guid nodeId, CancellationToken cancellationToken = default);

    // ── Upload Operations ───────────────────────────────────────────────────

    /// <summary>Initiates a chunked upload session.</summary>
    Task<UploadSessionResponse> InitiateUploadAsync(string fileName, Guid? parentId, long totalSize, string? mimeType, IReadOnlyList<string> chunkHashes, CancellationToken cancellationToken = default);

    /// <summary>Uploads a single chunk.</summary>
    Task UploadChunkAsync(Guid sessionId, int chunkIndex, string chunkHash, Stream chunkData, CancellationToken cancellationToken = default);

    /// <summary>Completes a chunked upload session.</summary>
    Task<CompleteUploadResponse> CompleteUploadAsync(Guid sessionId, CancellationToken cancellationToken = default);

    // ── Download Operations ─────────────────────────────────────────────────

    /// <summary>Downloads the current version of a file as a stream.</summary>
    Task<Stream> DownloadAsync(Guid nodeId, CancellationToken cancellationToken = default);

    /// <summary>Downloads a specific version of a file as a stream.</summary>
    Task<Stream> DownloadVersionAsync(Guid nodeId, int versionNumber, CancellationToken cancellationToken = default);

    /// <summary>Downloads a single chunk by its SHA-256 hash.</summary>
    Task<Stream> DownloadChunkByHashAsync(string chunkHash, CancellationToken cancellationToken = default);

    /// <summary>Gets the chunk manifest for the current version of a file.</summary>
    Task<ChunkManifestResponse> GetChunkManifestAsync(Guid nodeId, CancellationToken cancellationToken = default);

    // ── Sync Operations ─────────────────────────────────────────────────────

    /// <summary>Gets all changes since a given timestamp.</summary>
    Task<IReadOnlyList<SyncChangeResponse>> GetChangesSinceAsync(DateTime since, Guid? folderId, CancellationToken cancellationToken = default);

    /// <summary>Gets a full folder tree snapshot.</summary>
    Task<SyncTreeNodeResponse> GetFolderTreeAsync(Guid? folderId, CancellationToken cancellationToken = default);

    /// <summary>Reconciles the client's file state against the server.</summary>
    Task<ReconcileResponse> ReconcileAsync(Guid? folderId, IReadOnlyList<ClientNodeEntry> clientNodes, CancellationToken cancellationToken = default);

    // ── Quota Operations ────────────────────────────────────────────────────

    /// <summary>Gets quota information for the authenticated user.</summary>
    Task<QuotaResponse> GetQuotaAsync(CancellationToken cancellationToken = default);
}
