using DotNetCloud.Client.Core.Api;
using DotNetCloud.Client.Core.Sync;
using Microsoft.Extensions.Logging;

namespace DotNetCloud.Client.Core.VirtualFiles;

/// <summary>
/// A no-op implementation of <see cref="IVirtualFileProvider"/> for unsupported platforms (macOS).
/// All operations log a warning and complete successfully without side effects.
/// </summary>
public sealed class NoOpVirtualFileProvider : IVirtualFileProvider
{
    private readonly ILogger<NoOpVirtualFileProvider> _logger;

    /// <summary>
    /// Initializes a new instance of <see cref="NoOpVirtualFileProvider"/>.
    /// </summary>
    public NoOpVirtualFileProvider(ILogger<NoOpVirtualFileProvider> logger)
    {
        _logger = logger;
    }

    /// <inheritdoc/>
    public Task InitializeAsync(SyncContext context, CancellationToken ct = default)
    {
        _logger.LogWarning(
            "NoOpVirtualFileProvider: InitializeAsync called for {DisplayName}. " +
            "Virtual file syncing is not supported on this platform.",
            context.DisplayName);
        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    public Task CreatePlaceholdersAsync(SyncTreeNodeResponse tree, CancellationToken ct = default)
    {
        _logger.LogWarning("NoOpVirtualFileProvider: CreatePlaceholdersAsync called — no-op.");
        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    public Task HydrateFileAsync(string localPath, Guid nodeId, CancellationToken ct = default)
    {
        _logger.LogWarning("NoOpVirtualFileProvider: HydrateFileAsync called for {LocalPath} — no-op.", localPath);
        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    public Task DehydrateFileAsync(string localPath, CancellationToken ct = default)
    {
        _logger.LogWarning("NoOpVirtualFileProvider: DehydrateFileAsync called for {LocalPath} — no-op.", localPath);
        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    public Task PinFileAsync(string localPath, CancellationToken ct = default)
    {
        _logger.LogWarning("NoOpVirtualFileProvider: PinFileAsync called for {LocalPath} — no-op.", localPath);
        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    public Task UnpinFileAsync(string localPath, CancellationToken ct = default)
    {
        _logger.LogWarning("NoOpVirtualFileProvider: UnpinFileAsync called for {LocalPath} — no-op.", localPath);
        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    public Task<bool> IsHydratedAsync(string localPath, CancellationToken ct = default)
    {
        _logger.LogWarning("NoOpVirtualFileProvider: IsHydratedAsync called for {LocalPath} — returning true.", localPath);
        return Task.FromResult(true);
    }

    /// <inheritdoc/>
    public Task ShutdownAsync(CancellationToken ct = default)
    {
        _logger.LogWarning("NoOpVirtualFileProvider: ShutdownAsync called — no-op.");
        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    public ValueTask DisposeAsync()
    {
        _logger.LogWarning("NoOpVirtualFileProvider: DisposeAsync called — no-op.");
        return ValueTask.CompletedTask;
    }
}
