using DotNetCloud.Modules.Files.Data;
using DotNetCloud.Modules.Files.Models;
using DotNetCloud.Modules.Files.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace DotNetCloud.Modules.Files.Data.Services;

/// <summary>
/// Resolves and auto-registers sync devices. On first contact a new <see cref="SyncDevice"/>
/// row is created; subsequent requests update <see cref="SyncDevice.LastSeenAt"/>.
/// </summary>
internal sealed class SyncDeviceResolver : ISyncDeviceResolver
{
    private readonly FilesDbContext _db;
    private readonly ILogger<SyncDeviceResolver> _logger;

    public SyncDeviceResolver(FilesDbContext db, ILogger<SyncDeviceResolver> logger)
    {
        _db = db;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<SyncDevice?> ResolveAsync(
        Guid? deviceId,
        Guid userId,
        string? deviceName,
        string? platform,
        string? clientVersion,
        CancellationToken cancellationToken = default)
    {
        if (deviceId is null || deviceId == Guid.Empty)
            return null;

        var device = await _db.SyncDevices
            .FirstOrDefaultAsync(d => d.Id == deviceId.Value, cancellationToken);

        if (device is not null)
        {
            // Ensure the device belongs to this user (prevent cross-user spoofing)
            if (device.UserId != userId)
            {
                _logger.LogWarning(
                    "Device {DeviceId} is registered to user {RegisteredUserId} but request came from {RequestUserId}. Ignoring device header.",
                    deviceId, device.UserId, userId);
                return null;
            }

            // Update last-seen and metadata
            device.LastSeenAt = DateTime.UtcNow;
            if (!string.IsNullOrEmpty(deviceName))
                device.DeviceName = deviceName;
            if (!string.IsNullOrEmpty(platform))
                device.Platform = platform;
            if (!string.IsNullOrEmpty(clientVersion))
                device.ClientVersion = clientVersion;

            await _db.SaveChangesAsync(cancellationToken);
            return device;
        }

        // Auto-register new device
        device = new SyncDevice
        {
            Id = deviceId.Value,
            UserId = userId,
            DeviceName = deviceName ?? "Unknown",
            Platform = platform,
            ClientVersion = clientVersion
        };

        _db.SyncDevices.Add(device);

        try
        {
            await _db.SaveChangesAsync(cancellationToken);
            _logger.LogInformation(
                "Auto-registered new sync device {DeviceId} '{DeviceName}' ({Platform}) for user {UserId}.",
                device.Id, device.DeviceName, device.Platform, device.UserId);
        }
        catch (DbUpdateException ex) when (DbExceptionClassifier.IsUniqueConstraintViolation(ex))
        {
            // Concurrent registration race — another request won; re-fetch.
            _db.ChangeTracker.Clear();
            device = await _db.SyncDevices.FirstAsync(d => d.Id == deviceId.Value, cancellationToken);
        }

        return device;
    }
}
