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

        // Parse raw timestamp value and validate request age.
        // Different WOPI implementations may emit FILETIME, DateTime ticks, or Unix time.
        if (!long.TryParse(timestamp, out var timestampValue))
        {
            _logger.LogWarning("WOPI proof validation failed: invalid X-WOPI-TimeStamp value '{Timestamp}'.", timestamp);
            return false;
        }

        if (!TryResolveTimestampUtc(timestampValue, out var requestTime))
        {
            _logger.LogWarning("WOPI proof validation failed: unrecognized X-WOPI-TimeStamp value '{Timestamp}'.", timestamp);
            return false;
        }

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
        byte[] proofBytes = BuildProofBytes(accessToken, requestUrl.ToUpperInvariant(), timestampValue);

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
        if (!string.IsNullOrEmpty(discovery.ProofKeyValue) ||
            (!string.IsNullOrEmpty(discovery.ProofKey) && !string.IsNullOrEmpty(discovery.ProofKeyExponent)))
        {
            if (TryVerify(proofBytes, currentSig, discovery.ProofKeyValue, discovery.ProofKey, discovery.ProofKeyExponent))
                return true;

            if (oldSig is not null && TryVerify(proofBytes, oldSig, discovery.ProofKeyValue, discovery.ProofKey, discovery.ProofKeyExponent))
                return true;
        }

        if ((!string.IsNullOrEmpty(discovery.OldProofKeyValue) ||
            (!string.IsNullOrEmpty(discovery.OldProofKey) && !string.IsNullOrEmpty(discovery.OldProofKeyExponent))) &&
            currentSig is not null)
        {
            if (TryVerify(proofBytes, currentSig, discovery.OldProofKeyValue, discovery.OldProofKey, discovery.OldProofKeyExponent))
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

    private bool TryVerify(
        byte[] data,
        byte[] signature,
        string? base64SubjectPublicKeyInfo,
        string? base64Modulus,
        string? base64Exponent)
    {
        if (!string.IsNullOrWhiteSpace(base64SubjectPublicKeyInfo))
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
            }
        }

        if (!string.IsNullOrWhiteSpace(base64Modulus) && !string.IsNullOrWhiteSpace(base64Exponent))
        {
            try
            {
                var modulus = Convert.FromBase64String(base64Modulus);
                var exponent = Convert.FromBase64String(base64Exponent);

                using var rsa = RSA.Create();
                rsa.ImportParameters(new RSAParameters
                {
                    Modulus = modulus,
                    Exponent = exponent
                });

                return rsa.VerifyData(data, signature, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
            }
            catch (Exception ex) when (ex is CryptographicException or FormatException)
            {
                _logger.LogWarning(ex, "WOPI proof modulus/exponent verification threw an exception.");
            }
        }

        return false;
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

    private static bool TryResolveTimestampUtc(long timestampValue, out DateTime utcTime)
    {
        utcTime = default;

        // 1) Windows FILETIME (100ns since 1601-01-01 UTC)
        if (timestampValue > 0)
        {
            try
            {
                var fileTimeUtc = DateTime.FromFileTimeUtc(timestampValue);
                if (fileTimeUtc.Year >= 2000 && fileTimeUtc.Year <= 3000)
                {
                    utcTime = fileTimeUtc;
                    return true;
                }
            }
            catch (ArgumentOutOfRangeException)
            {
                // Try next format.
            }
        }

        // 2) DateTime ticks (100ns since 0001-01-01 UTC)
        if (timestampValue >= DateTime.UnixEpoch.Ticks && timestampValue <= DateTime.MaxValue.Ticks)
        {
            try
            {
                var dateTimeTicksUtc = new DateTime(timestampValue, DateTimeKind.Utc);
                if (dateTimeTicksUtc.Year >= 2000 && dateTimeTicksUtc.Year <= 3000)
                {
                    utcTime = dateTimeTicksUtc;
                    return true;
                }
            }
            catch (ArgumentOutOfRangeException)
            {
                // Try next format.
            }
        }

        // 3) Unix milliseconds
        if (timestampValue >= 946684800000 && timestampValue <= 32503680000000)
        {
            utcTime = DateTimeOffset.FromUnixTimeMilliseconds(timestampValue).UtcDateTime;
            return true;
        }

        // 4) Unix seconds
        if (timestampValue >= 946684800 && timestampValue <= 32503680000)
        {
            utcTime = DateTimeOffset.FromUnixTimeSeconds(timestampValue).UtcDateTime;
            return true;
        }

        return false;
    }
}
