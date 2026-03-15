using DotNetCloud.Modules.Files.Models;

namespace DotNetCloud.Modules.Files.Services;

/// <summary>
/// Resolves and auto-registers sync devices from incoming request headers.
/// </summary>
public interface ISyncDeviceResolver
{
    /// <summary>
    /// Resolves a device by ID, creating or updating the registration as needed.
    /// Returns null if <paramref name="deviceId"/> is null (client didn't send the header).
    /// </summary>
    Task<SyncDevice?> ResolveAsync(
        Guid? deviceId,
        Guid userId,
        string? deviceName,
        string? platform,
        string? clientVersion,
        CancellationToken cancellationToken = default);
}
