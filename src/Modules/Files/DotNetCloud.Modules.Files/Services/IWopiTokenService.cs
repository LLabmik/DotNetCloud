using DotNetCloud.Core.Authorization;
using DotNetCloud.Modules.Files.DTOs;

namespace DotNetCloud.Modules.Files.Services;

/// <summary>
/// Generates and validates WOPI access tokens for Collabora integration.
/// Tokens are per-user, per-file, and time-limited using HMAC-SHA256 signatures.
/// </summary>
public interface IWopiTokenService
{
    /// <summary>
    /// Generates a WOPI access token for a specific file and user.
    /// </summary>
    /// <param name="fileId">The file node ID.</param>
    /// <param name="caller">The user requesting access.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A WOPI access token DTO with the token, TTL, and editor URL.</returns>
    Task<WopiAccessTokenDto> GenerateTokenAsync(Guid fileId, CallerContext caller, CancellationToken cancellationToken = default);

    /// <summary>
    /// Validates a WOPI access token and returns the associated context.
    /// </summary>
    /// <param name="accessToken">The access token to validate.</param>
    /// <param name="fileId">The file ID the token should be valid for.</param>
    /// <returns>The validated token context, or null if the token is invalid/expired.</returns>
    WopiTokenContext? ValidateToken(string accessToken, Guid fileId);
}

/// <summary>
/// Represents the validated context extracted from a WOPI access token.
/// </summary>
public sealed record WopiTokenContext
{
    /// <summary>User ID embedded in the token.</summary>
    public required Guid UserId { get; init; }

    /// <summary>File ID the token is valid for.</summary>
    public required Guid FileId { get; init; }

    /// <summary>Whether the user has write permission.</summary>
    public bool CanWrite { get; init; }

    /// <summary>Token expiration time (UTC).</summary>
    public required DateTime ExpiresAt { get; init; }
}
