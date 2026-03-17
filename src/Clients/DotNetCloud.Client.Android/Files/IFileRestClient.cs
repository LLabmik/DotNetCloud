namespace DotNetCloud.Client.Android.Files;

/// <summary>
/// REST API client for file and folder operations against a DotNetCloud server.
/// Follows the same per-call credentials pattern as <see cref="Chat.IChatRestClient"/>.
/// </summary>
public interface IFileRestClient
{
    // ── Browsing ─────────────────────────────────────────────────────

    /// <summary>Lists children of a folder (null = root).</summary>
    Task<IReadOnlyList<FileItem>> ListChildrenAsync(
        string serverBaseUrl, string accessToken,
        Guid? folderId, CancellationToken ct = default);

    /// <summary>Gets metadata for a specific node.</summary>
    Task<FileItem> GetNodeAsync(
        string serverBaseUrl, string accessToken,
        Guid nodeId, CancellationToken ct = default);

    // ── Folder Operations ────────────────────────────────────────────

    /// <summary>Creates a new folder.</summary>
    Task<FileItem> CreateFolderAsync(
        string serverBaseUrl, string accessToken,
        string name, Guid? parentId, CancellationToken ct = default);

    /// <summary>Renames a file or folder.</summary>
    Task<FileItem> RenameAsync(
        string serverBaseUrl, string accessToken,
        Guid nodeId, string newName, CancellationToken ct = default);

    /// <summary>Soft-deletes (trashes) a file or folder.</summary>
    Task DeleteAsync(
        string serverBaseUrl, string accessToken,
        Guid nodeId, CancellationToken ct = default);

    // ── Upload ───────────────────────────────────────────────────────

    /// <summary>
    /// Uploads a file using the chunked upload protocol. Returns the resulting file node.
    /// </summary>
    Task<FileItem> UploadFileAsync(
        string serverBaseUrl, string accessToken,
        string fileName, Guid? parentId,
        Stream fileData, long fileSize, string? mimeType,
        IProgress<FileTransferProgress>? progress = null,
        CancellationToken ct = default);

    // ── Download ─────────────────────────────────────────────────────

    /// <summary>Downloads the current version of a file as a stream.</summary>
    Task<Stream> DownloadAsync(
        string serverBaseUrl, string accessToken,
        Guid nodeId, CancellationToken ct = default);

    // ── Quota ────────────────────────────────────────────────────────

    /// <summary>Gets quota information for the authenticated user.</summary>
    Task<QuotaInfo> GetQuotaAsync(
        string serverBaseUrl, string accessToken,
        CancellationToken ct = default);
}

/// <summary>Represents a file or folder node for display in the file browser.</summary>
/// <param name="Id">Unique node identifier.</param>
/// <param name="Name">Display name.</param>
/// <param name="NodeType"><c>"File"</c> or <c>"Folder"</c>.</param>
/// <param name="Size">Size in bytes.</param>
/// <param name="MimeType">MIME type (null for folders).</param>
/// <param name="ParentId">Parent folder ID (null for root items).</param>
/// <param name="UpdatedAt">Last modified timestamp (UTC).</param>
/// <param name="ChildCount">Number of children (for folders).</param>
public sealed record FileItem(
    Guid Id,
    string Name,
    string NodeType,
    long Size,
    string? MimeType,
    Guid? ParentId,
    DateTime UpdatedAt,
    int ChildCount);

/// <summary>Transfer progress for an upload or download operation.</summary>
/// <param name="BytesTransferred">Bytes transferred so far.</param>
/// <param name="TotalBytes">Total bytes expected.</param>
public sealed record FileTransferProgress(long BytesTransferred, long TotalBytes);

/// <summary>Storage quota information.</summary>
/// <param name="UsedBytes">Bytes currently used.</param>
/// <param name="TotalBytes">Total allocated quota in bytes.</param>
public sealed record QuotaInfo(long UsedBytes, long TotalBytes);
