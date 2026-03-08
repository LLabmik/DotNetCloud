using DotNetCloud.Modules.Files.Options;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace DotNetCloud.Modules.Files.Data.Services.Background;

/// <summary>
/// Hosted service that prepares the server temp directory on startup and removes stale temp files.
/// Runs once at startup; the directory is created with 700 permissions (Linux/macOS).
/// </summary>
internal sealed class TempFileCleanupService : IHostedService
{
    private readonly string _tmpPath;
    private readonly ILogger<TempFileCleanupService> _logger;

    /// <summary>
    /// Initializes a new instance of <see cref="TempFileCleanupService"/>.
    /// </summary>
    public TempFileCleanupService(IOptions<FileUploadOptions> options, ILogger<TempFileCleanupService> logger)
    {
        _tmpPath = options.Value.TmpPath ?? Path.GetTempPath();
        _logger = logger;
    }

    /// <inheritdoc />
    public Task StartAsync(CancellationToken cancellationToken)
    {
        try
        {
            Directory.CreateDirectory(_tmpPath);

            if (OperatingSystem.IsLinux() || OperatingSystem.IsMacOS())
            {
                File.SetUnixFileMode(
                    _tmpPath,
                    UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
            }

            var cutoff = DateTime.UtcNow.AddHours(-1);
            var deleted = 0;

            foreach (var file in Directory.GetFiles(_tmpPath))
            {
                if (File.GetLastWriteTimeUtc(file) < cutoff)
                {
                    try
                    {
                        File.Delete(file);
                        deleted++;
                    }
                    catch (IOException ex)
                    {
                        _logger.LogWarning(ex, "Could not delete stale temp file {File}.", file);
                    }
                }
            }

            if (deleted > 0)
            {
                _logger.LogInformation("Cleaned up {Count} stale temp file(s) from {TmpPath}.", deleted, _tmpPath);
            }
            else
            {
                _logger.LogDebug("Temp directory ready: {TmpPath}.", _tmpPath);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to initialize temp directory {TmpPath}.", _tmpPath);
        }

        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
