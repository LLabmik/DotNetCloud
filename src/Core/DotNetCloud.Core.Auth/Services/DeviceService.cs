using DotNetCloud.Core.Data.Context;
using DotNetCloud.Core.DTOs;
using DotNetCloud.Core.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace DotNetCloud.Core.Auth.Services;

/// <summary>
/// Implements <see cref="IDeviceService"/> using EF Core and <see cref="CoreDbContext"/>.
/// </summary>
public sealed class DeviceService : IDeviceService
{
    private readonly CoreDbContext _dbContext;
    private readonly ILogger<DeviceService> _logger;

    /// <summary>
    /// Initializes a new instance of <see cref="DeviceService"/>.
    /// </summary>
    public DeviceService(CoreDbContext dbContext, ILogger<DeviceService> logger)
    {
        _dbContext = dbContext;
        _logger = logger;
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<UserDeviceDto>> GetDevicesAsync(Guid userId)
    {
        var devices = await _dbContext.UserDevices
            .Where(d => d.UserId == userId)
            .OrderByDescending(d => d.LastSeenAt)
            .Select(d => new UserDeviceDto
            {
                Id = d.Id,
                UserId = d.UserId,
                Name = d.Name,
                DeviceType = d.DeviceType,
                LastSeenAt = d.LastSeenAt,
                RegisteredAt = d.CreatedAt,
                IsActive = d.LastSeenAt > DateTime.UtcNow.AddMinutes(-10),
            })
            .ToListAsync();

        return devices.AsReadOnly();
    }

    /// <inheritdoc/>
    public async Task<bool> RemoveDeviceAsync(Guid userId, Guid deviceId)
    {
        var device = await _dbContext.UserDevices
            .FirstOrDefaultAsync(d => d.Id == deviceId && d.UserId == userId);

        if (device is null)
        {
            _logger.LogWarning("Device {DeviceId} not found for user {UserId}", deviceId, userId);
            return false;
        }

        _dbContext.UserDevices.Remove(device);
        await _dbContext.SaveChangesAsync();

        _logger.LogInformation("Device {DeviceId} removed for user {UserId}", deviceId, userId);
        return true;
    }
}
