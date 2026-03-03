using DotNetCloud.Core.DTOs;

namespace DotNetCloud.Core.Services;

/// <summary>
/// Manages user device registration, listing, and removal.
/// </summary>
public interface IDeviceService
{
    /// <summary>
    /// Gets all devices registered by the specified user.
    /// </summary>
    /// <param name="userId">The ID of the user whose devices to list.</param>
    /// <returns>A read-only list of device DTOs.</returns>
    Task<IReadOnlyList<UserDeviceDto>> GetDevicesAsync(Guid userId);

    /// <summary>
    /// Removes a device registration for the specified user.
    /// </summary>
    /// <param name="userId">The ID of the user who owns the device.</param>
    /// <param name="deviceId">The ID of the device to remove.</param>
    /// <returns><see langword="true"/> if the device was found and removed; otherwise <see langword="false"/>.</returns>
    Task<bool> RemoveDeviceAsync(Guid userId, Guid deviceId);
}
