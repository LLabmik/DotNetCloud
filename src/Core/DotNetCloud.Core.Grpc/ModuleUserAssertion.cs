using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace DotNetCloud.Core.Grpc;

/// <summary>
/// A short-lived, core-signed statement that a module is acting for a specific end user.
/// </summary>
/// <remarks>
/// The user identity is the one thing a module must never be able to invent. A module identity
/// proves <i>which process</i> is calling; this assertion proves <i>who it is calling for</i>,
/// and only the core can produce one because only the core holds the signing key.
/// </remarks>
public sealed record ModuleUserAssertion
{
    /// <summary>Gets the end-user identifier the module is acting for.</summary>
    [JsonPropertyName("sub")]
    public required Guid UserId { get; init; }

    /// <summary>Gets the module the assertion was minted for.</summary>
    [JsonPropertyName("aud")]
    public required string Audience { get; init; }

    /// <summary>Gets the operation the user authorised, for coarse scoping.</summary>
    [JsonPropertyName("op")]
    public required string Operation { get; init; }

    /// <summary>Gets the expiry, as a Unix timestamp in seconds.</summary>
    [JsonPropertyName("exp")]
    public required long ExpiresAtUnixSeconds { get; init; }

    /// <summary>Gets the unique assertion id, for audit correlation.</summary>
    [JsonPropertyName("jti")]
    public required Guid Id { get; init; }
}

/// <summary>
/// Encodes, signs and verifies <see cref="ModuleUserAssertion"/> tokens.
/// </summary>
/// <remarks>
/// The token is <c>base64url(payload-json).base64url(ecdsa-sha256-signature)</c>. Signing uses
/// ECDSA P-256 so a module can verify an assertion with the core's public key without ever being
/// able to mint one — a symmetric secret would let any module holding it forge a user identity,
/// which is exactly the problem this replaces.
/// </remarks>
public static class ModuleUserAssertions
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);

    /// <summary>
    /// Creates and signs an assertion token.
    /// </summary>
    /// <param name="assertion">The assertion payload.</param>
    /// <param name="signingKey">The core's private signing key.</param>
    /// <returns>The compact token to send in the <see cref="ModuleIdentityMetadata.UserAssertionHeader"/>.</returns>
    public static string Sign(ModuleUserAssertion assertion, ECDsa signingKey)
    {
        ArgumentNullException.ThrowIfNull(assertion);
        ArgumentNullException.ThrowIfNull(signingKey);

        var payload = JsonSerializer.SerializeToUtf8Bytes(assertion, SerializerOptions);
        var signature = signingKey.SignData(payload, HashAlgorithmName.SHA256);

        return $"{Base64UrlEncode(payload)}.{Base64UrlEncode(signature)}";
    }

    /// <summary>
    /// Imports a base64 SubjectPublicKeyInfo key for verification.
    /// </summary>
    /// <param name="publicKeyBase64">The base64 key material.</param>
    /// <returns>
    /// The key to dispose when done, or <see langword="null"/> when the material is missing or
    /// unusable. Callers should hold the result rather than re-importing it per call — key import
    /// is far more expensive than the verification itself.
    /// </returns>
    public static ECDsa? TryImportPublicKey(string? publicKeyBase64)
    {
        if (string.IsNullOrEmpty(publicKeyBase64))
        {
            return null;
        }

        ECDsa? key = null;
        try
        {
            key = ECDsa.Create();
            key.ImportSubjectPublicKeyInfo(Convert.FromBase64String(publicKeyBase64), out _);
            return key;
        }
        catch (Exception ex) when (ex is FormatException or CryptographicException)
        {
            key?.Dispose();
            return null;
        }
    }

    /// <summary>
    /// Verifies an assertion token.
    /// </summary>
    /// <param name="token">The compact token.</param>
    /// <param name="verificationKey">
    /// The key to verify with — the core's public key, or the core's own key pair on the core side.
    /// </param>
    /// <param name="expectedAudience">The module id the assertion must be scoped to.</param>
    /// <param name="now">The current time, for the expiry check.</param>
    /// <param name="assertion">The verified payload when verification succeeds.</param>
    /// <returns><see langword="true"/> only when the signature, audience and expiry all check out.</returns>
    public static bool TryVerify(
        string? token,
        ECDsa verificationKey,
        string expectedAudience,
        DateTimeOffset now,
        out ModuleUserAssertion? assertion)
    {
        ArgumentNullException.ThrowIfNull(verificationKey);

        assertion = null;

        if (string.IsNullOrEmpty(token) || string.IsNullOrEmpty(expectedAudience))
        {
            return false;
        }

        var separator = token.IndexOf('.', StringComparison.Ordinal);
        if (separator <= 0 || separator == token.Length - 1)
        {
            return false;
        }

        byte[] payload;
        byte[] signature;

        try
        {
            payload = Base64UrlDecode(token[..separator]);
            signature = Base64UrlDecode(token[(separator + 1)..]);
        }
        catch (FormatException)
        {
            return false;
        }

        if (!verificationKey.VerifyData(payload, signature, HashAlgorithmName.SHA256))
        {
            return false;
        }

        ModuleUserAssertion? decoded;
        try
        {
            decoded = JsonSerializer.Deserialize<ModuleUserAssertion>(payload, SerializerOptions);
        }
        catch (JsonException)
        {
            return false;
        }

        if (decoded is null
            || decoded.UserId == Guid.Empty
            || !string.Equals(decoded.Audience, expectedAudience, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (DateTimeOffset.FromUnixTimeSeconds(decoded.ExpiresAtUnixSeconds) <= now)
        {
            return false;
        }

        assertion = decoded;
        return true;
    }

    private static string Base64UrlEncode(byte[] value) =>
        Convert.ToBase64String(value)
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');

    private static byte[] Base64UrlDecode(string value)
    {
        var padded = value.Replace('-', '+').Replace('_', '/');
        padded += (padded.Length % 4) switch
        {
            2 => "==",
            3 => "=",
            0 => string.Empty,
            _ => throw new FormatException("Invalid base64url length."),
        };

        return Convert.FromBase64String(padded);
    }
}
