namespace DotNetCloud.Modules.Files.DTOs;

/// <summary>
/// Represents a WOPI access token with its metadata.
/// </summary>
public sealed record WopiAccessTokenDto
{
    /// <summary>The opaque access token string.</summary>
    public required string AccessToken { get; init; }

    /// <summary>Token expiration as Unix timestamp in milliseconds (for WOPI protocol).</summary>
    public long AccessTokenTtl { get; init; }

    /// <summary>The WOPI source URL that Collabora should use to access the file.</summary>
    public required string WopiSrc { get; init; }

    /// <summary>The full editor URL including WOPI source and access token parameters.</summary>
    public required string EditorUrl { get; init; }
}

/// <summary>
/// WOPI CheckFileInfo response as defined by the WOPI protocol.
/// See: https://learn.microsoft.com/en-us/microsoft-365/cloud-storage-partner-program/rest/files/checkfileinfo
/// </summary>
public sealed record WopiCheckFileInfoResponse
{
    /// <summary>File name without path.</summary>
    public required string BaseFileName { get; init; }

    /// <summary>Unique identifier of the file owner.</summary>
    public required string OwnerId { get; init; }

    /// <summary>File size in bytes.</summary>
    public long Size { get; init; }

    /// <summary>Version identifier (changes on each save).</summary>
    public required string Version { get; init; }

    /// <summary>Whether the current user has write permission.</summary>
    public bool UserCanWrite { get; init; }

    /// <summary>Whether the host supports creating new files via WOPI.</summary>
    public bool UserCanNotWriteRelative { get; init; } = true;

    /// <summary>Whether the host supports PutFile.</summary>
    public bool SupportsUpdate { get; init; } = true;

    /// <summary>Whether the host supports lock operations.</summary>
    public bool SupportsLocks { get; init; }

    /// <summary>SHA-256 hash of the file content.</summary>
    public string SHA256 { get; init; } = string.Empty;

    /// <summary>Last modified time in ISO 8601 format.</summary>
    public required string LastModifiedTime { get; init; }

    /// <summary>Unique identifier for the current user.</summary>
    public string? UserId { get; init; }

    /// <summary>Display name of the current user.</summary>
    public string? UserFriendlyName { get; init; }

    /// <summary>Whether the current user is an anonymous user.</summary>
    public bool IsAnonymousUser { get; init; }
}

/// <summary>
/// Represents a file format entry from Collabora's WOPI discovery XML.
/// </summary>
public sealed record CollaboraAppAction
{
    /// <summary>The application name (e.g., "writer", "calc", "impress").</summary>
    public required string AppName { get; init; }

    /// <summary>The action (e.g., "edit", "view").</summary>
    public required string Action { get; init; }

    /// <summary>The file extension this action applies to (e.g., "docx", "xlsx").</summary>
    public required string Extension { get; init; }

    /// <summary>The MIME type this action applies to.</summary>
    public string? MimeType { get; init; }

    /// <summary>The URL pattern for the editor iframe.</summary>
    public required string UrlSrc { get; init; }
}

/// <summary>
/// Result of Collabora WOPI discovery.
/// </summary>
public sealed record CollaboraDiscoveryResult
{
    /// <summary>Whether discovery was successful.</summary>
    public bool IsAvailable { get; init; }

    /// <summary>All available actions from Collabora.</summary>
    public IReadOnlyList<CollaboraAppAction> Actions { get; init; } = [];

    /// <summary>The Collabora proof key modulus (legacy; prefer <see cref="ProofKeyValue"/>).</summary>
    public string? ProofKey { get; init; }

    /// <summary>The Collabora proof key exponent paired with <see cref="ProofKey"/>.</summary>
    public string? ProofKeyExponent { get; init; }

    /// <summary>The Collabora old proof key modulus (legacy; prefer <see cref="OldProofKeyValue"/>).</summary>
    public string? OldProofKey { get; init; }

    /// <summary>The Collabora old proof key exponent paired with <see cref="OldProofKey"/>.</summary>
    public string? OldProofKeyExponent { get; init; }

    /// <summary>
    /// The Collabora current proof key as Base64-encoded SubjectPublicKeyInfo DER bytes.
    /// Used for RSA-SHA256 signature verification of X-WOPI-Proof headers.
    /// </summary>
    public string? ProofKeyValue { get; init; }

    /// <summary>
    /// The Collabora previous proof key as Base64-encoded SubjectPublicKeyInfo DER bytes.
    /// Used during key rotation to validate in-flight requests signed with the old key.
    /// </summary>
    public string? OldProofKeyValue { get; init; }

    /// <summary>When this discovery data was fetched.</summary>
    public DateTime FetchedAt { get; init; } = DateTime.UtcNow;
}
