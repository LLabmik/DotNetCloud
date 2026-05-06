using System.Security.Cryptography;
using DotNetCloud.Modules.Email.Services;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace DotNetCloud.Modules.Email.Data.Services;

/// <summary>
/// Configuration for <see cref="FileSystemAttachmentStorage"/>.
/// </summary>
public sealed class AttachmentStorageOptions
{
    /// <summary>Base path for storing attachment files. Default: {AppData}/email-attachments.</summary>
    public string BasePath { get; set; } = string.Empty;

    /// <summary>Maximum attachment size in bytes. Default: 25 MB.</summary>
    public long MaxAttachmentSize { get; set; } = 25 * 1024 * 1024;
}

/// <summary>
/// Content-addressable filesystem storage for email attachments.
/// Stores files at {basePath}/attachments/{hash[0..2]}/{hash[2..4]}/{hash}
/// using SHA-256 hash prefix directories (matching the Files module pattern).
/// </summary>
public sealed class FileSystemAttachmentStorage : IAttachmentStorage
{
    private const int BufferSize = 80 * 1024; // 80 KB buffer

    private readonly string _basePath;
    private readonly long _maxAttachmentSize;
    private readonly ILogger<FileSystemAttachmentStorage> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="FileSystemAttachmentStorage"/> class.
    /// </summary>
    public FileSystemAttachmentStorage(IOptions<AttachmentStorageOptions> options, ILogger<FileSystemAttachmentStorage> logger)
    {
        var opts = options.Value;
        _basePath = string.IsNullOrWhiteSpace(opts.BasePath)
            ? Path.Combine(AppContext.BaseDirectory, "email-attachments")
            : opts.BasePath;
        _maxAttachmentSize = opts.MaxAttachmentSize;
        _logger = logger;

        Directory.CreateDirectory(_basePath);
    }

    /// <inheritdoc />
    public async Task<AttachmentStorageResult> StoreAsync(Stream content, string fileName, string contentType, CancellationToken ct = default)
    {
        // Validate size (only if stream supports Length)
        if (content.CanSeek && content.Length > _maxAttachmentSize)
        {
            throw new InvalidOperationException(
                $"Attachment exceeds maximum size of {_maxAttachmentSize / (1024 * 1024)} MB. " +
                "Files over 25 MB can be shared via the Files module.");
        }

        // Compute hash from stream
        var hash = await SHA256.HashDataAsync(content, ct);
        var hexHash = Convert.ToHexString(hash).ToLowerInvariant();
        content.Position = 0;

        // Ensure directory exists for content-addressable path
        var storagePath = GetStoragePath(hexHash);
        var dir = Path.GetDirectoryName(storagePath)!;
        Directory.CreateDirectory(dir);

        // Check if already exists (dedup by hash)
        if (File.Exists(storagePath))
        {
            var fi = new FileInfo(storagePath);
            return new AttachmentStorageResult
            {
                StorageKey = hexHash,
                ContentHash = hexHash,
                Size = fi.Length,
                StoredAt = fi.CreationTimeUtc
            };
        }

        // Write to final location
        content.Position = 0;
        await using (var fs = new FileStream(storagePath, FileMode.CreateNew, FileAccess.Write, FileShare.None, BufferSize, FileOptions.Asynchronous))
        {
            await content.CopyToAsync(fs, BufferSize, ct);
        }

        var fileInfo = new FileInfo(storagePath);
        return new AttachmentStorageResult
        {
            StorageKey = hexHash,
            ContentHash = hexHash,
            Size = fileInfo.Length,
            StoredAt = fileInfo.CreationTimeUtc
        };
    }

    /// <inheritdoc />
    public Task<Stream?> OpenReadAsync(string storageKey, CancellationToken ct = default)
    {
        var path = GetStoragePath(storageKey);
        if (!File.Exists(path))
            return Task.FromResult<Stream?>(null);

        return Task.FromResult<Stream?>(new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, BufferSize, FileOptions.Asynchronous));
    }

    /// <inheritdoc />
    public Task<bool> DeleteAsync(string storageKey, CancellationToken ct = default)
    {
        var path = GetStoragePath(storageKey);
        if (!File.Exists(path))
            return Task.FromResult(false);

        File.Delete(path);
        _logger.LogDebug("Deleted attachment storage file: {StorageKey}", storageKey);
        return Task.FromResult(true);
    }

    /// <inheritdoc />
    public Task<long> GetSizeAsync(string storageKey, CancellationToken ct = default)
    {
        var path = GetStoragePath(storageKey);
        if (!File.Exists(path))
            return Task.FromResult(0L);

        var fi = new FileInfo(path);
        return Task.FromResult(fi.Length);
    }

    /// <summary>
    /// Gets all storage keys for files that have been stored (for cleanup iteration).
    /// </summary>
    public IEnumerable<string> GetAllStorageKeys()
    {
        if (!Directory.Exists(_basePath))
            yield break;

        // Walk hash prefix directories: {basePath}/{prefix2}/{prefix4}/{hash}
        foreach (var dir2 in Directory.EnumerateDirectories(_basePath))
        {
            foreach (var dir4 in Directory.EnumerateDirectories(dir2))
            {
                foreach (var file in Directory.EnumerateFiles(dir4))
                {
                    yield return Path.GetFileName(file);
                }
            }
        }
    }

    /// <summary>
    /// Gets the creation time of a stored file (for TTL cleanup).
    /// </summary>
    public DateTime? GetCreationTime(string storageKey)
    {
        var path = GetStoragePath(storageKey);
        if (!File.Exists(path))
            return null;

        return new FileInfo(path).CreationTimeUtc;
    }

    /// <summary>
    /// Resolves a storage key to its filesystem path.
    /// Pattern: {basePath}/{hash[0..2]}/{hash[2..4]}/{hash}
    /// </summary>
    private string GetStoragePath(string storageKey)
    {
        var prefix2 = storageKey[..2];
        var prefix4 = storageKey[2..4];
        return Path.Combine(_basePath, prefix2, prefix4, storageKey);
    }
}
