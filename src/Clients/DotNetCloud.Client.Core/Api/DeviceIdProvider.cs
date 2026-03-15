using Microsoft.Extensions.Logging;

namespace DotNetCloud.Client.Core.Api;

/// <summary>
/// Provides a stable, per-installation device identifier. The ID is generated on first run
/// and persisted to a file in the data directory. Subsequent runs reuse the same ID,
/// ensuring the server can track this device across sessions.
/// </summary>
public sealed class DeviceIdProvider
{
    private const string DeviceIdFileName = "device-id";
    private readonly ILogger<DeviceIdProvider> _logger;
    private Guid? _cachedDeviceId;

    /// <summary>Initializes a new <see cref="DeviceIdProvider"/>.</summary>
    public DeviceIdProvider(ILogger<DeviceIdProvider> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Gets the stable device ID for this installation, creating one if it doesn't exist.
    /// </summary>
    /// <param name="dataDirectory">The sync service data directory (e.g., <c>~/.local/share/dotnetcloud/sync</c>).</param>
    /// <returns>The device GUID.</returns>
    public Guid GetOrCreateDeviceId(string dataDirectory)
    {
        if (_cachedDeviceId.HasValue)
            return _cachedDeviceId.Value;

        var filePath = Path.Combine(dataDirectory, DeviceIdFileName);

        if (File.Exists(filePath))
        {
            var content = File.ReadAllText(filePath).Trim();
            if (Guid.TryParse(content, out var existingId) && existingId != Guid.Empty)
            {
                _cachedDeviceId = existingId;
                _logger.LogDebug("Loaded existing device ID {DeviceId} from {Path}.", existingId, filePath);
                return existingId;
            }

            _logger.LogWarning("Invalid device ID file at {Path} (content: '{Content}'). Regenerating.", filePath, content);
        }

        var newId = Guid.NewGuid();
        Directory.CreateDirectory(dataDirectory);
        File.WriteAllText(filePath, newId.ToString("D"));

        _cachedDeviceId = newId;
        _logger.LogInformation("Generated new device ID {DeviceId} at {Path}.", newId, filePath);
        return newId;
    }
}
