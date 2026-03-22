using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using DotNetCloud.Core.Authorization;
using DotNetCloud.Modules.Files.DTOs;
using DotNetCloud.Modules.Files.Models;
using DotNetCloud.Modules.Files.Options;
using DotNetCloud.Modules.Files.Services;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace DotNetCloud.Modules.Files.Data.Services;

/// <summary>
/// Generates and validates WOPI access tokens using HMAC-SHA256 signatures.
/// Tokens encode user ID, file ID, permissions, and expiry, signed with a secret key.
/// </summary>
internal sealed class WopiTokenService : IWopiTokenService
{
    private static readonly byte[] EphemeralProcessSigningKey = RandomNumberGenerator.GetBytes(32);

    private readonly FilesDbContext _db;
    private readonly IPermissionService _permissionService;
    private readonly ICollaboraDiscoveryService _discoveryService;
    private readonly CollaboraOptions _options;
    private readonly ILogger<WopiTokenService> _logger;
    private readonly byte[] _signingKey;

    public WopiTokenService(
        FilesDbContext db,
        IPermissionService permissionService,
        ICollaboraDiscoveryService discoveryService,
        IOptions<CollaboraOptions> options,
        ILogger<WopiTokenService> logger,
        IHostEnvironment? hostEnvironment = null)
    {
        _db = db;
        _permissionService = permissionService;
        _discoveryService = discoveryService;
        _options = options.Value;
        _logger = logger;
        _signingKey = DeriveSigningKey(_options.TokenSigningKey, hostEnvironment);
    }

    /// <inheritdoc />
    public async Task<WopiAccessTokenDto> GenerateTokenAsync(Guid fileId, CallerContext caller, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(caller);

        if (!_options.Enabled)
            throw new Core.Errors.InvalidOperationException("Collabora integration is disabled.");

        if (!await _discoveryService.IsAvailableAsync(cancellationToken))
            throw new Core.Errors.InvalidOperationException("Collabora is not available.");

        var node = await _db.FileNodes
            .AsNoTracking()
            .FirstOrDefaultAsync(n => n.Id == fileId && !n.IsDeleted, cancellationToken);

        if (node is null)
            throw new Core.Errors.NotFoundException($"File {fileId} not found.");

        if (node.NodeType != FileNodeType.File)
            throw new Core.Errors.InvalidOperationException("Cannot open a folder in the document editor.");

        var permission = await _permissionService.GetEffectivePermissionAsync(fileId, caller, cancellationToken);
        if (permission is null)
            throw new Core.Errors.ForbiddenException("You do not have access to this file.");

        bool canWrite = permission >= SharePermission.ReadWrite;

        var expiresAt = DateTime.UtcNow.AddMinutes(_options.TokenLifetimeMinutes);
        var payload = new WopiTokenPayload(caller.UserId, fileId, canWrite, expiresAt);
        var token = CreateSignedToken(payload);

        var extension = Path.GetExtension(node.Name)?.TrimStart('.') ?? string.Empty;

        if (string.IsNullOrWhiteSpace(extension) || !await _discoveryService.IsSupportedExtensionAsync(extension, cancellationToken))
            throw new Core.Errors.InvalidOperationException($"File type '.{extension}' is not supported by Collabora.");

        var editorUrlTemplate = await _discoveryService.GetEditorUrlAsync(extension, canWrite ? "edit" : "view", cancellationToken);

        if (string.IsNullOrWhiteSpace(editorUrlTemplate))
            throw new Core.Errors.InvalidOperationException($"No Collabora editor action is available for '.{extension}'.");

        var wopiSrc = BuildWopiSrc(fileId);
        var editorUrl = BuildEditorUrl(editorUrlTemplate, wopiSrc, token, expiresAt);

        _logger.LogInformation("Generated WOPI token for file {FileId} ({FileName}), user {UserId}, canWrite={CanWrite}, expires={ExpiresAt}",
            fileId, node.Name, caller.UserId, canWrite, expiresAt);

        return new WopiAccessTokenDto
        {
            AccessToken = token,
            AccessTokenTtl = new DateTimeOffset(expiresAt).ToUnixTimeMilliseconds(),
            WopiSrc = wopiSrc,
            EditorUrl = editorUrl
        };
    }

    /// <inheritdoc />
    public WopiTokenContext? ValidateToken(string accessToken, Guid fileId)
    {
        if (string.IsNullOrWhiteSpace(accessToken))
            return null;

        try
        {
            var parts = accessToken.Split('.');
            if (parts.Length != 2)
                return null;

            var payloadBytes = DecodeTokenPart(parts[0]);
            var signatureBytes = DecodeTokenPart(parts[1]);

            // Verify signature
            using var hmac = new HMACSHA256(_signingKey);
            var expectedSignature = hmac.ComputeHash(payloadBytes);
            if (!CryptographicOperations.FixedTimeEquals(signatureBytes, expectedSignature))
            {
                _logger.LogWarning("WOPI token signature validation failed for file {FileId}", fileId);
                return null;
            }

            var payload = JsonSerializer.Deserialize<WopiTokenPayload>(payloadBytes);
            if (payload is null)
                return null;

            // Verify file ID matches
            if (payload.FileId != fileId)
            {
                _logger.LogWarning("WOPI token file ID mismatch: token={TokenFileId}, requested={RequestedFileId}",
                    payload.FileId, fileId);
                return null;
            }

            // Verify not expired
            if (payload.ExpiresAt <= DateTime.UtcNow)
            {
                _logger.LogDebug("WOPI token expired for file {FileId}, user {UserId}", fileId, payload.UserId);
                return null;
            }

            return new WopiTokenContext
            {
                UserId = payload.UserId,
                FileId = payload.FileId,
                CanWrite = payload.CanWrite,
                ExpiresAt = payload.ExpiresAt
            };
        }
        catch (Exception ex) when (ex is FormatException or JsonException)
        {
            _logger.LogWarning(ex, "WOPI token parsing failed for file {FileId}", fileId);
            return null;
        }
    }

    private string CreateSignedToken(WopiTokenPayload payload)
    {
        var payloadBytes = JsonSerializer.SerializeToUtf8Bytes(payload);
        using var hmac = new HMACSHA256(_signingKey);
        var signature = hmac.ComputeHash(payloadBytes);

        return $"{WebEncoders.Base64UrlEncode(payloadBytes)}.{WebEncoders.Base64UrlEncode(signature)}";
    }

    private static byte[] DecodeTokenPart(string value)
    {
        // Accept Base64Url (current) and standard Base64 (legacy tokens) for compatibility.
        try
        {
            return WebEncoders.Base64UrlDecode(value);
        }
        catch (FormatException)
        {
            return Convert.FromBase64String(value);
        }
    }

    private string BuildWopiSrc(Guid fileId)
    {
        var baseUrl = _options.WopiBaseUrl.TrimEnd('/');
        return $"{baseUrl}/api/v1/wopi/files/{fileId}";
    }

    private static string BuildEditorUrl(string? editorUrlTemplate, string wopiSrc, string accessToken, DateTime expiresAt)
    {
        if (string.IsNullOrEmpty(editorUrlTemplate))
            return string.Empty;

        // Collabora editor URL template contains placeholders like <WOPI_SRC>
        // Standard format: {editorUrl}?WOPISrc={wopiSrc}&access_token={token}&access_token_ttl={ttl}
        var url = editorUrlTemplate;

        // Remove template placeholders if present
        var queryStart = url.IndexOf('?');
        if (queryStart >= 0)
        {
            // Remove existing query parameters from template
            var baseEditorUrl = url[..queryStart];
            url = baseEditorUrl;
        }

        var ttl = new DateTimeOffset(expiresAt).ToUnixTimeMilliseconds();
        return $"{url}?WOPISrc={Uri.EscapeDataString(wopiSrc)}&access_token={Uri.EscapeDataString(accessToken)}&access_token_ttl={ttl}";
    }

    private static byte[] DeriveSigningKey(string configuredKey, IHostEnvironment? hostEnvironment)
    {
        if (!string.IsNullOrWhiteSpace(configuredKey) && configuredKey.Length >= 32)
            return SHA256.HashData(Encoding.UTF8.GetBytes(configuredKey));

        // SECURITY WARNING: Using ephemeral signing key — WOPI tokens will be invalidated
        // on every server restart and cannot be verified across multiple server instances.
        // In production, ALWAYS configure Files:Collabora:TokenSigningKey (≥ 32 characters).
        // This fallback exists only for initial development/testing.
        var isProduction = hostEnvironment is not null
            ? hostEnvironment.IsProduction()
            : string.Equals(
                Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? "Production",
                "Production", StringComparison.OrdinalIgnoreCase);

        if (isProduction)
        {
            throw new InvalidOperationException(
                "WOPI TokenSigningKey is not configured. In production, set Files:Collabora:TokenSigningKey " +
                "to a secure random string of at least 32 characters. Ephemeral keys are not allowed in production.");
        }

        return EphemeralProcessSigningKey;
    }

    /// <summary>
    /// Internal token payload serialized to JSON and signed.
    /// </summary>
    private sealed record WopiTokenPayload(Guid UserId, Guid FileId, bool CanWrite, DateTime ExpiresAt);
}
