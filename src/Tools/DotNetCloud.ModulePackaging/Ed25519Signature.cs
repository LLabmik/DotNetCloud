using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;
using Org.BouncyCastle.Crypto.Parameters;
using Org.BouncyCastle.Crypto.Signers;

namespace DotNetCloud.ModulePackaging;

/// <summary>
/// An Ed25519 signing key pair.
/// </summary>
/// <param name="KeyId">Stable key identifier pinned by verifiers (for example <c>store-2026</c>).</param>
/// <param name="PublicKey">Base64 of the 32-byte raw public key.</param>
/// <param name="PrivateKey">Base64 of the 32-byte raw private seed. Never distribute.</param>
public sealed record ModuleSigningKeyPair(string KeyId, string PublicKey, string PrivateKey);

/// <summary>
/// The detached signature file that accompanies a package (`&lt;package&gt;.sig`).
/// </summary>
/// <remarks>
/// The signature covers the package's SHA-256 digest, so signing and verification are streaming and
/// the archive never has to be loaded into memory. The <see cref="Algorithm"/> field is
/// informational: the <see cref="KeyId"/> selects the algorithm, and anything other than
/// <c>ed25519</c> is rejected rather than trusted.
/// </remarks>
public sealed class ModulePackageSignatureFile
{
    /// <summary>Signature file schema version.</summary>
    public int SchemaVersion { get; init; } = 1;

    /// <summary>The key id that produced the signature.</summary>
    public required string KeyId { get; init; }

    /// <summary>The signature algorithm. Only <c>ed25519</c> is accepted.</summary>
    public string Algorithm { get; init; } = Ed25519Signature.AlgorithmName;

    /// <summary>The package file name the signature was produced for.</summary>
    public required string File { get; init; }

    /// <summary>Lower-case hex SHA-256 of the package bytes.</summary>
    public required string Sha256 { get; init; }

    /// <summary>Base64 of the 64-byte Ed25519 signature over the 32 digest bytes.</summary>
    public required string Signature { get; init; }
}

/// <summary>Ed25519 primitives used by the packer and the verifier.</summary>
/// <remarks>
/// .NET 10's <c>System.Security.Cryptography</c> has no Ed25519 surface, so BouncyCastle is used
/// (pinned in <c>Directory.Packages.props</c>). It is pure managed, so the packer runs on every
/// platform the SDK does without a native libsodium asset.
/// </remarks>
public static class Ed25519Signature
{
    /// <summary>The only accepted algorithm name.</summary>
    public const string AlgorithmName = "ed25519";

    private const int KeySizeBytes = 32;
    private const int SignatureSizeBytes = 64;

    /// <summary>Generates a new key pair.</summary>
    /// <param name="keyId">The stable key identifier.</param>
    /// <returns>The key pair, with base64-encoded raw keys.</returns>
    public static ModuleSigningKeyPair GenerateKeyPair(string keyId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(keyId);

        var seed = RandomNumberGenerator.GetBytes(KeySizeBytes);
        var privateKey = new Ed25519PrivateKeyParameters(seed, 0);
        var publicKey = privateKey.GeneratePublicKey().GetEncoded();

        return new ModuleSigningKeyPair(
            keyId,
            Convert.ToBase64String(publicKey),
            Convert.ToBase64String(seed));
    }

    /// <summary>Saves a key pair as JSON, restricting the file to the current user on Unix.</summary>
    /// <param name="keyPair">The key pair.</param>
    /// <param name="outputPath">The destination path.</param>
    public static void WriteKeyPair(ModuleSigningKeyPair keyPair, string outputPath)
    {
        ArgumentNullException.ThrowIfNull(keyPair);
        ArgumentException.ThrowIfNullOrWhiteSpace(outputPath);

        var fullPath = Path.GetFullPath(outputPath);
        Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);

        File.WriteAllText(fullPath, JsonSerializer.Serialize(keyPair, SerializerOptions));

        if (!OperatingSystem.IsWindows())
        {
            File.SetUnixFileMode(fullPath, UnixFileMode.UserRead | UnixFileMode.UserWrite);
        }
    }

    /// <summary>Reads a key pair written by <see cref="WriteKeyPair"/>.</summary>
    /// <param name="path">The key file path.</param>
    /// <returns>The key pair.</returns>
    /// <exception cref="ModulePackageException">The file is missing or malformed.</exception>
    public static ModuleSigningKeyPair ReadKeyPair(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        if (!File.Exists(path))
        {
            throw new ModulePackageException($"Signing key file not found: {path}");
        }

        try
        {
            var keyPair = JsonSerializer.Deserialize<ModuleSigningKeyPair>(
                File.ReadAllText(path), SerializerOptions);

            if (keyPair is null || string.IsNullOrWhiteSpace(keyPair.KeyId) || string.IsNullOrWhiteSpace(keyPair.PrivateKey))
            {
                throw new ModulePackageException($"Signing key file '{path}' is incomplete.");
            }

            return keyPair;
        }
        catch (JsonException ex)
        {
            throw new ModulePackageException($"Signing key file '{path}' is not valid JSON: {ex.Message}");
        }
    }

    /// <summary>Signs a 32-byte digest.</summary>
    /// <param name="digest">The SHA-256 digest to sign.</param>
    /// <param name="privateKeyBase64">Base64 of the 32-byte private seed.</param>
    /// <returns>Base64 of the 64-byte signature.</returns>
    public static string Sign(ReadOnlySpan<byte> digest, string privateKeyBase64)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(privateKeyBase64);

        var seed = Convert.FromBase64String(privateKeyBase64);
        if (seed.Length != KeySizeBytes)
        {
            throw new ModulePackageException(
                $"An Ed25519 private key must be {KeySizeBytes} bytes; got {seed.Length}.");
        }

        var signer = new Ed25519Signer();
        signer.Init(forSigning: true, new Ed25519PrivateKeyParameters(seed, 0));
        signer.BlockUpdate(digest);

        return Convert.ToBase64String(signer.GenerateSignature());
    }

    /// <summary>Verifies a signature over a 32-byte digest.</summary>
    /// <param name="digest">The SHA-256 digest that was signed.</param>
    /// <param name="publicKeyBase64">Base64 of the 32-byte public key.</param>
    /// <param name="signatureBase64">Base64 of the 64-byte signature.</param>
    /// <returns><see langword="true"/> when the signature is valid.</returns>
    public static bool Verify(ReadOnlySpan<byte> digest, string publicKeyBase64, string signatureBase64)
    {
        if (string.IsNullOrWhiteSpace(publicKeyBase64) || string.IsNullOrWhiteSpace(signatureBase64))
        {
            return false;
        }

        byte[] publicKey;
        byte[] signature;

        try
        {
            publicKey = Convert.FromBase64String(publicKeyBase64);
            signature = Convert.FromBase64String(signatureBase64);
        }
        catch (FormatException)
        {
            return false;
        }

        if (publicKey.Length != KeySizeBytes || signature.Length != SignatureSizeBytes)
        {
            return false;
        }

        var verifier = new Ed25519Signer();
        verifier.Init(forSigning: false, new Ed25519PublicKeyParameters(publicKey, 0));
        verifier.BlockUpdate(digest);

        return verifier.VerifySignature(signature);
    }

    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.Never,
    };
}

/// <summary>Produces detached signatures for packages.</summary>
public static class ModulePackageSigner
{
    /// <summary>
    /// Signs a package and writes the detached signature file.
    /// </summary>
    /// <param name="packagePath">The package to sign.</param>
    /// <param name="keyPair">The signing key pair.</param>
    /// <param name="signaturePath">
    /// Where to write the signature; defaults to <c>&lt;package&gt;.sig</c>.
    /// </param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The signature that was written.</returns>
    public static async Task<ModulePackageSignatureFile> SignAsync(
        string packagePath,
        ModuleSigningKeyPair keyPair,
        string? signaturePath = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(packagePath);
        ArgumentNullException.ThrowIfNull(keyPair);

        if (!File.Exists(packagePath))
        {
            throw new ModulePackageException($"Package not found: {packagePath}");
        }

        var digestHex = await DeterministicPackageWriter.ComputeSha256Async(packagePath, cancellationToken);
        var digest = Convert.FromHexString(digestHex);

        var signature = new ModulePackageSignatureFile
        {
            KeyId = keyPair.KeyId,
            File = Path.GetFileName(packagePath),
            Sha256 = digestHex,
            Signature = Ed25519Signature.Sign(digest, keyPair.PrivateKey),
        };

        var target = signaturePath ?? ModulePackageFormat.GetSignaturePath(packagePath);
        await File.WriteAllTextAsync(
            target,
            JsonSerializer.Serialize(signature, SignatureSerializerOptions) + Environment.NewLine,
            cancellationToken);

        return signature;
    }

    /// <summary>Reads a detached signature file.</summary>
    /// <param name="signaturePath">The signature path.</param>
    /// <returns>The parsed signature.</returns>
    /// <exception cref="ModulePackageException">The file is missing or malformed.</exception>
    public static ModulePackageSignatureFile Read(string signaturePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(signaturePath);

        if (!File.Exists(signaturePath))
        {
            throw new ModulePackageException($"Signature file not found: {signaturePath}");
        }

        try
        {
            var signature = JsonSerializer.Deserialize<ModulePackageSignatureFile>(
                File.ReadAllText(signaturePath), SignatureSerializerOptions);

            return signature ?? throw new ModulePackageException(
                $"Signature file '{signaturePath}' is empty.");
        }
        catch (JsonException ex)
        {
            throw new ModulePackageException(
                $"Signature file '{signaturePath}' is not valid JSON: {ex.Message}");
        }
    }

    /// <summary>Serializes a signature deterministically (used by the packer and by tests).</summary>
    /// <param name="signature">The signature.</param>
    /// <returns>Indented camelCase JSON.</returns>
    public static string Serialize(ModulePackageSignatureFile signature) =>
        JsonSerializer.Serialize(signature, SignatureSerializerOptions);

    internal static readonly JsonSerializerOptions SignatureSerializerOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true,
    };
}
