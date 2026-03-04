using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;

namespace DotNetCloud.Client.Core.Auth;

/// <summary>
/// Cross-platform token store that encrypts tokens to disk using AES-GCM
/// with a machine-derived key. On Windows, callers may layer DPAPI on top
/// for stronger per-user isolation.
/// </summary>
public sealed class EncryptedFileTokenStore : ITokenStore
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly string _storeDirectory;
    private readonly ILogger<EncryptedFileTokenStore> _logger;

    /// <summary>Initializes a new <see cref="EncryptedFileTokenStore"/>.</summary>
    public EncryptedFileTokenStore(string storeDirectory, ILogger<EncryptedFileTokenStore> logger)
    {
        _storeDirectory = storeDirectory;
        _logger = logger;
        Directory.CreateDirectory(storeDirectory);
    }

    /// <inheritdoc/>
    public async Task SaveAsync(string accountKey, TokenInfo tokens, CancellationToken cancellationToken = default)
    {
        var json = JsonSerializer.SerializeToUtf8Bytes(tokens, JsonOptions);
        var encrypted = EncryptAesGcm(json);
        var path = GetPath(accountKey);
        await File.WriteAllBytesAsync(path, encrypted, cancellationToken);
        _logger.LogDebug("Saved tokens for account {AccountKey}.", accountKey);
    }

    /// <inheritdoc/>
    public async Task<TokenInfo?> LoadAsync(string accountKey, CancellationToken cancellationToken = default)
    {
        var path = GetPath(accountKey);
        if (!File.Exists(path))
            return null;

        try
        {
            var encrypted = await File.ReadAllBytesAsync(path, cancellationToken);
            var json = DecryptAesGcm(encrypted);
            return JsonSerializer.Deserialize<TokenInfo>(json, JsonOptions);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to load tokens for account {AccountKey}.", accountKey);
            return null;
        }
    }

    /// <inheritdoc/>
    public Task DeleteAsync(string accountKey, CancellationToken cancellationToken = default)
    {
        var path = GetPath(accountKey);
        if (File.Exists(path))
            File.Delete(path);
        return Task.CompletedTask;
    }

    private string GetPath(string accountKey)
    {
        var safe = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(accountKey)));
        return Path.Combine(_storeDirectory, $"{safe}.tok");
    }

    private static byte[] EncryptAesGcm(byte[] plaintext)
    {
        var key = DeriveKey();
        var nonce = new byte[AesGcm.NonceByteSizes.MaxSize];
        RandomNumberGenerator.Fill(nonce);
        var tag = new byte[AesGcm.TagByteSizes.MaxSize];
        var ciphertext = new byte[plaintext.Length];

        using var aes = new AesGcm(key, AesGcm.TagByteSizes.MaxSize);
        aes.Encrypt(nonce, plaintext, ciphertext, tag);

        // Format: [nonce (12)] [tag (16)] [ciphertext]
        var result = new byte[nonce.Length + tag.Length + ciphertext.Length];
        Buffer.BlockCopy(nonce, 0, result, 0, nonce.Length);
        Buffer.BlockCopy(tag, 0, result, nonce.Length, tag.Length);
        Buffer.BlockCopy(ciphertext, 0, result, nonce.Length + tag.Length, ciphertext.Length);
        return result;
    }

    private static byte[] DecryptAesGcm(byte[] data)
    {
        var key = DeriveKey();
        var nonceLen = AesGcm.NonceByteSizes.MaxSize;
        var tagLen = AesGcm.TagByteSizes.MaxSize;

        var nonce = data[..nonceLen];
        var tag = data[nonceLen..(nonceLen + tagLen)];
        var ciphertext = data[(nonceLen + tagLen)..];
        var plaintext = new byte[ciphertext.Length];

        using var aes = new AesGcm(key, tagLen);
        aes.Decrypt(nonce, ciphertext, tag, plaintext);
        return plaintext;
    }

    /// <summary>
    /// Derives a machine-unique 256-bit key from the machine name and user name.
    /// </summary>
    private static byte[] DeriveKey()
    {
        var material = $"dotnetcloud:{Environment.MachineName}:{Environment.UserName}";
        return SHA256.HashData(Encoding.UTF8.GetBytes(material));
    }
}
