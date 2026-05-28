using System.Security.Cryptography;

namespace DotNetCloud.Core.Storage;

/// <summary>
/// Content-addressed storage for binary assets (album art, video posters, thumbnails, etc.).
/// Files are stored by SHA-256 hash with a 2-level directory prefix for filesystem scalability.
/// </summary>
public sealed class ContentAddressedStorage
{
    private readonly string _basePath;

    /// <summary>
    /// Initializes a new instance of the <see cref="ContentAddressedStorage"/> class.
    /// </summary>
    /// <param name="basePath">Root directory for cached content (e.g., <c>{StorageRootPath}/.media-cache</c>).</param>
    public ContentAddressedStorage(string basePath)
    {
        _basePath = basePath ?? throw new ArgumentNullException(nameof(basePath));
        Directory.CreateDirectory(_basePath);
    }

    /// <summary>
    /// Stores data in the content-addressed cache and returns the relative content hash path.
    /// </summary>
    /// <param name="data">Binary data to store.</param>
    /// <param name="extension">File extension including dot (e.g., ".jpg", ".png").</param>
    /// <returns>The content hash string (without extension), used to retrieve the file later.</returns>
    public string Store(byte[] data, string extension)
    {
        ArgumentNullException.ThrowIfNull(data);
        if (data.Length == 0)
            throw new ArgumentException("Data cannot be empty.", nameof(data));

        var hash = ComputeHash(data);
        var path = GetFullPath(hash, extension);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);

        if (!File.Exists(path))
        {
            File.WriteAllBytes(path, data);
        }

        return hash;
    }

    /// <summary>
    /// Stores data from a stream in the content-addressed cache and returns the content hash.
    /// </summary>
    /// <param name="stream">Stream containing binary data to store.</param>
    /// <param name="extension">File extension including dot (e.g., ".jpg", ".png").</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The content hash string.</returns>
    public async Task<string> StoreAsync(Stream stream, string extension, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(stream);

        using var ms = new MemoryStream();
        await stream.CopyToAsync(ms, cancellationToken);
        var data = ms.ToArray();

        return Store(data, extension);
    }

    /// <summary>
    /// Gets the full filesystem path for a cached item.
    /// </summary>
    /// <param name="contentHash">SHA-256 content hash.</param>
    /// <param name="extension">File extension including dot (e.g., ".jpg", ".png").</param>
    /// <returns>Full filesystem path to the cached file.</returns>
    public string GetPath(string contentHash, string extension)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(contentHash);
        ArgumentException.ThrowIfNullOrWhiteSpace(extension);

        return GetFullPath(contentHash, extension);
    }

    /// <summary>
    /// Checks whether content with the given hash exists in the cache.
    /// </summary>
    public bool Exists(string contentHash)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(contentHash);

        var dir = GetDirectoryPrefix(contentHash);
        var prefixDir = Path.Combine(_basePath, "images", dir);

        if (!Directory.Exists(prefixDir))
            return false;

        return Directory.GetFiles(prefixDir, $"{contentHash}.*").Length > 0;
    }

    /// <summary>
    /// Deletes a cached item by content hash.
    /// </summary>
    public bool Delete(string contentHash)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(contentHash);

        var dir = GetDirectoryPrefix(contentHash);
        var prefixDir = Path.Combine(_basePath, "images", dir);

        if (!Directory.Exists(prefixDir))
            return false;

        var files = Directory.GetFiles(prefixDir, $"{contentHash}.*");
        foreach (var file in files)
        {
            try
            {
                File.Delete(file);
            }
            catch
            {
                // Best-effort cleanup
            }
        }

        return true;
    }

    /// <summary>
    /// Computes SHA-256 hash of the given data and returns it as a hex string.
    /// </summary>
    public static string ComputeHash(byte[] data)
    {
        var hashBytes = SHA256.HashData(data);
        return Convert.ToHexStringLower(hashBytes);
    }

    private string GetFullPath(string contentHash, string extension)
    {
        var prefix = GetDirectoryPrefix(contentHash);
        return Path.Combine(_basePath, "images", prefix, $"{contentHash}{extension}");
    }

    private static string GetDirectoryPrefix(string contentHash)
    {
        return contentHash.Length >= 2 ? contentHash[..2] : contentHash;
    }
}
