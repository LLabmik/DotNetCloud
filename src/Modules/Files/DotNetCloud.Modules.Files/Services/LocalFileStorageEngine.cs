using Microsoft.Extensions.Logging;

namespace DotNetCloud.Modules.Files.Services;

/// <summary>
/// Local filesystem implementation of <see cref="IFileStorageEngine"/>.
/// Stores chunks using content-addressable paths under a configurable base directory.
/// </summary>
/// <remarks>
/// Storage layout uses hash prefix directories for balanced distribution:
/// <code>
/// {basePath}/chunks/ab/cd/abcdef1234567890...
/// </code>
/// </remarks>
public sealed class LocalFileStorageEngine : IFileStorageEngine
{
    private readonly string _basePath;
    private readonly ILogger<LocalFileStorageEngine> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="LocalFileStorageEngine"/> class.
    /// </summary>
    /// <param name="basePath">Base directory for file storage.</param>
    /// <param name="logger">Logger instance.</param>
    public LocalFileStorageEngine(string basePath, ILogger<LocalFileStorageEngine> logger)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(basePath);
        _basePath = basePath;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task WriteChunkAsync(string storagePath, ReadOnlyMemory<byte> data, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(storagePath);

        var fullPath = GetFullPath(storagePath);
        var directory = Path.GetDirectoryName(fullPath);

        if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
        {
            Directory.CreateDirectory(directory);
        }

        await File.WriteAllBytesAsync(fullPath, data.ToArray(), cancellationToken);

        // Verify the write completed fully — catches disk-full, truncation, filesystem errors.
        var writtenSize = new FileInfo(fullPath).Length;
        if (writtenSize != data.Length)
        {
            try { File.Delete(fullPath); } catch { /* best-effort cleanup */ }
            throw new IOException(
                $"Chunk write verification failed for '{storagePath}': expected {data.Length} bytes, got {writtenSize}.");
        }

        // Prevent execute bits on stored chunk files — chunks are content-addressable
        // data, never executables. Restrict to user read/write only (600 on Linux/macOS).
        if (OperatingSystem.IsLinux() || OperatingSystem.IsMacOS())
        {
            File.SetUnixFileMode(fullPath, UnixFileMode.UserRead | UnixFileMode.UserWrite);
        }

        _logger.LogDebug("Chunk written: {StoragePath} ({Size} bytes)", storagePath, data.Length);
    }

    /// <inheritdoc />
    public async Task<byte[]?> ReadChunkAsync(string storagePath, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(storagePath);

        var fullPath = GetFullPath(storagePath);

        if (!File.Exists(fullPath))
        {
            _logger.LogWarning("Chunk not found: {StoragePath}", storagePath);
            return null;
        }

        return await File.ReadAllBytesAsync(fullPath, cancellationToken);
    }

    /// <inheritdoc />
    public Task<Stream?> OpenReadStreamAsync(string storagePath, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(storagePath);

        var fullPath = GetFullPath(storagePath);

        if (!File.Exists(fullPath))
        {
            _logger.LogWarning("Chunk not found for streaming: {StoragePath}", storagePath);
            return Task.FromResult<Stream?>(null);
        }

        Stream stream = new FileStream(fullPath, FileMode.Open, FileAccess.Read, FileShare.Read,
            bufferSize: 81920, useAsync: true);

        return Task.FromResult<Stream?>(stream);
    }

    /// <inheritdoc />
    public Task<bool> ExistsAsync(string storagePath, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(storagePath);

        var fullPath = GetFullPath(storagePath);
        return Task.FromResult(File.Exists(fullPath));
    }

    /// <inheritdoc />
    public Task DeleteAsync(string storagePath, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(storagePath);

        var fullPath = GetFullPath(storagePath);

        if (File.Exists(fullPath))
        {
            File.Delete(fullPath);
            _logger.LogDebug("Chunk deleted: {StoragePath}", storagePath);
        }

        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task<long> GetTotalSizeAsync(CancellationToken cancellationToken = default)
    {
        if (!Directory.Exists(_basePath))
        {
            return Task.FromResult(0L);
        }

        var totalSize = new DirectoryInfo(_basePath)
            .EnumerateFiles("*", SearchOption.AllDirectories)
            .Sum(f => f.Length);

        return Task.FromResult(totalSize);
    }

    private string GetFullPath(string storagePath)
    {
        // Prevent directory traversal attacks
        var normalized = storagePath.Replace('\\', '/');
        if (normalized.Contains(".."))
        {
            throw new ArgumentException("Storage path must not contain directory traversal sequences.", nameof(storagePath));
        }

        return Path.Combine(_basePath, normalized);
    }
}
