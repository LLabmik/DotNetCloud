using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;
using DotNetCloud.Modules.Files.Options;
using DotNetCloud.Modules.Files.Services;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace DotNetCloud.Modules.Files.Data.Services;

/// <summary>
/// Validates WOPI proof key signatures using the RSA public key from Collabora's discovery document.
/// </summary>
/// <remarks>
/// Implements the WOPI proof key validation algorithm:
/// 1. Reject requests older than 20 minutes (X-WOPI-TimeStamp).
/// 2. Build the expected proof bytes: [token_len][token][url_len][url][8][timestamp_int64_be].
/// 3. Verify RSA-SHA256 signature using Collabora's current public key.
/// 4. If that fails, verify with old key (for key rotation).
/// 5. If that fails, verify the old proof header against the current key.
/// See: https://docs.microsoft.com/en-us/microsoft-365/cloud-storage-partner-program/rest/files/checkfileinfo/checkfileinfo-other
/// </remarks>
internal sealed class WopiProofKeyValidator : IWopiProofKeyValidator
{
    private static readonly TimeSpan MaxTimestampAge = TimeSpan.FromMinutes(20);

    private readonly ICollaboraDiscoveryService _discoveryService;
    private readonly CollaboraOptions _options;
    private readonly ILogger<WopiProofKeyValidator> _logger;

    /// <summary>
    /// Initializes a new instance of <see cref="WopiProofKeyValidator"/>.
    /// </summary>
    public WopiProofKeyValidator(
        ICollaboraDiscoveryService discoveryService,
        IOptions<CollaboraOptions> options,
        ILogger<WopiProofKeyValidator> logger)
    {
        _discoveryService = discoveryService;
        _options = options.Value;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<bool> ValidateAsync(
        string accessToken,
        string requestUrl,
        string? proof,
        string? proofOld,
        string? timestamp,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(proof) || string.IsNullOrEmpty(timestamp))
        {
            _logger.LogWarning("WOPI proof validation failed: missing X-WOPI-Proof or X-WOPI-TimeStamp header.");
            return false;
        }

        // Parse and validate timestamp (Windows FILETIME = 100-nanosecond intervals since 1601-01-01)
        if (!long.TryParse(timestamp, out var timestampTicks))
        {
            _logger.LogWarning("WOPI proof validation failed: invalid X-WOPI-TimeStamp value '{Timestamp}'.", timestamp);
            return false;
        }

        var requestTime = DateTime.FromFileTimeUtc(timestampTicks);
        var age = DateTime.UtcNow - requestTime;
        if (age > MaxTimestampAge || age < TimeSpan.Zero)
        {
            _logger.LogWarning("WOPI proof validation failed: timestamp is {Age:F0}s old (max {Max}s).",
                age.TotalSeconds, MaxTimestampAge.TotalSeconds);
            return false;
        }

        var discovery = await _discoveryService.DiscoverAsync(cancellationToken);

        if (string.IsNullOrEmpty(discovery.ProofKeyValue) && string.IsNullOrEmpty(discovery.OldProofKeyValue))
        {
            // Collabora not available or no proof keys — cannot validate
            _logger.LogWarning("WOPI proof validation failed: no proof keys available in discovery.");
            return false;
        }

        // Build the signed proof bytes
        byte[] proofBytes = BuildProofBytes(accessToken, requestUrl.ToUpperInvariant(), timestampTicks);

        // Decode the provided signatures
        byte[]? currentSig = TryBase64Decode(proof);
        byte[]? oldSig = string.IsNullOrEmpty(proofOld) ? null : TryBase64Decode(proofOld);

        if (currentSig is null)
        {
            _logger.LogWarning("WOPI proof validation failed: cannot decode X-WOPI-Proof base64.");
            return false;
        }

        // Validation algorithm (any one match is sufficient):
        // 1. Current proof, current key
        // 2. Current proof, old key (old key still valid during rotation)
        // 3. Old proof, current key (request in-flight during key rotation)
        if (!string.IsNullOrEmpty(discovery.ProofKeyValue))
        {
            if (TryVerify(proofBytes, currentSig, discovery.ProofKeyValue))
                return true;

            if (oldSig is not null && TryVerify(proofBytes, oldSig, discovery.ProofKeyValue))
                return true;
        }

        if (!string.IsNullOrEmpty(discovery.OldProofKeyValue) && currentSig is not null)
        {
            if (TryVerify(proofBytes, currentSig, discovery.OldProofKeyValue))
                return true;
        }

        _logger.LogWarning("WOPI proof validation failed: signature did not match current or old key.");
        return false;
    }

    /// <summary>
    /// Constructs the canonical proof byte array as specified by the WOPI protocol.
    /// </summary>
    internal static byte[] BuildProofBytes(string accessToken, string upperCaseUrl, long timestampTicks)
    {
        var tokenBytes = Encoding.UTF8.GetBytes(accessToken);
        var urlBytes = Encoding.UTF8.GetBytes(upperCaseUrl);

        // Total size: 4 + token + 4 + url + 4 + 8
        var result = new byte[4 + tokenBytes.Length + 4 + urlBytes.Length + 4 + 8];
        var span = result.AsSpan();

        BinaryPrimitives.WriteInt32BigEndian(span, tokenBytes.Length);
        span = span[4..];
        tokenBytes.CopyTo(span);
        span = span[tokenBytes.Length..];

        BinaryPrimitives.WriteInt32BigEndian(span, urlBytes.Length);
        span = span[4..];
        urlBytes.CopyTo(span);
        span = span[urlBytes.Length..];

        BinaryPrimitives.WriteInt32BigEndian(span, 8);
        span = span[4..];
        BinaryPrimitives.WriteInt64BigEndian(span, timestampTicks);

        return result;
    }

    private bool TryVerify(byte[] data, byte[] signature, string base64SubjectPublicKeyInfo)
    {
        try
        {
            var keyBytes = Convert.FromBase64String(base64SubjectPublicKeyInfo);
            using var rsa = RSA.Create();
            rsa.ImportSubjectPublicKeyInfo(keyBytes, out _);
            return rsa.VerifyData(data, signature, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        }
        catch (Exception ex) when (ex is CryptographicException or FormatException)
        {
            _logger.LogWarning(ex, "WOPI proof key verification threw an exception.");
            return false;
        }
    }

    private static byte[]? TryBase64Decode(string value)
    {
        try
        {
            return Convert.FromBase64String(value);
        }
        catch (FormatException)
        {
            return null;
        }
    }
}
