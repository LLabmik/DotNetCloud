using DotNetCloud.Core.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DotNetCloud.Core.Server.Controllers;

/// <summary>
/// Device management endpoints for listing and removing user devices.
/// </summary>
[ApiController]
[Route("api/v1/core/auth/devices")]
[Authorize]
public class DeviceController : ControllerBase
{
    private readonly IDeviceService _deviceService;
    private readonly ILogger<DeviceController> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="DeviceController"/> class.
    /// </summary>
    public DeviceController(IDeviceService deviceService, ILogger<DeviceController> logger)
    {
        _deviceService = deviceService ?? throw new ArgumentNullException(nameof(deviceService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// List all devices registered by the current user.
    /// </summary>
    /// <returns>A list of the user's registered devices.</returns>
    [HttpGet]
    public async Task<IActionResult> GetDevicesAsync()
    {
        if (!TryGetUserId(out var userId))
        {
            return Unauthorized(new { success = false, error = new { code = "INVALID_TOKEN", message = "Invalid token claims" } });
        }

        var devices = await _deviceService.GetDevicesAsync(userId);
        _logger.LogInformation("Listed {Count} devices for user {UserId}", devices.Count, userId);
        return Ok(new { success = true, data = devices });
    }

    /// <summary>
    /// Remove a device registration for the current user.
    /// </summary>
    /// <param name="deviceId">The ID of the device to remove.</param>
    /// <returns>Confirmation that the device was removed.</returns>
    [HttpDelete("{deviceId:guid}")]
    public async Task<IActionResult> RemoveDeviceAsync(Guid deviceId)
    {
        if (!TryGetUserId(out var userId))
        {
            return Unauthorized(new { success = false, error = new { code = "INVALID_TOKEN", message = "Invalid token claims" } });
        }

        var removed = await _deviceService.RemoveDeviceAsync(userId, deviceId);
        if (!removed)
        {
            return NotFound(new { success = false, error = new { code = "DEVICE_NOT_FOUND", message = "Device not found or does not belong to the current user." } });
        }

        _logger.LogInformation("Device {DeviceId} removed for user {UserId}", deviceId, userId);
        return Ok(new { success = true, message = "Device removed successfully." });
    }

    private bool TryGetUserId(out Guid userId)
    {
        var claim = User.FindFirst("sub")?.Value;
        return Guid.TryParse(claim, out userId);
    }
}
