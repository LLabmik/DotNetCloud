namespace DotNetCloud.Client.Core.Transfer;

/// <summary>
/// Result of a chunked file upload, containing both the server node ID and the content hash.
/// </summary>
public sealed record UploadResult(Guid NodeId, string? ContentHash);
