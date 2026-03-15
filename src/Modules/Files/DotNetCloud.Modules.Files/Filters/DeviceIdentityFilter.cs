using DotNetCloud.Modules.Files.Services;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System.Security.Claims;

namespace DotNetCloud.Modules.Files.Filters;

/// <summary>
/// Action filter that extracts <c>X-Device-Id</c> (and optional <c>X-Device-Name</c>,
/// <c>X-Device-Platform</c>, <c>X-Client-Version</c>) headers from the request,
/// auto-registers the device via <see cref="ISyncDeviceResolver"/>, and populates
/// the scoped <see cref="IDeviceContext"/> for downstream use.
/// </summary>
public sealed class DeviceIdentityFilter : IAsyncActionFilter
{
    /// <summary>Header name for the client-generated device GUID.</summary>
    public const string DeviceIdHeader = "X-Device-Id";

    /// <summary>Header name for the device display name (typically hostname).</summary>
    public const string DeviceNameHeader = "X-Device-Name";

    /// <summary>Header name for the OS platform.</summary>
    public const string DevicePlatformHeader = "X-Device-Platform";

    /// <summary>Header name for the client version string.</summary>
    public const string ClientVersionHeader = "X-Client-Version";

    /// <inheritdoc />
    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        var httpContext = context.HttpContext;
        var logger = httpContext.RequestServices.GetService<ILogger<DeviceIdentityFilter>>();

        // Only process for authenticated requests
        if (httpContext.User?.Identity?.IsAuthenticated == true)
        {
            var rawDeviceId = httpContext.Request.Headers[DeviceIdHeader].FirstOrDefault();
            if (Guid.TryParse(rawDeviceId, out var deviceId) && deviceId != Guid.Empty)
            {
                var claimValue = httpContext.User.FindFirstValue(ClaimTypes.NameIdentifier)
                                ?? httpContext.User.FindFirstValue("sub");

                if (Guid.TryParse(claimValue, out var userId))
                {
                    var deviceName = httpContext.Request.Headers[DeviceNameHeader].FirstOrDefault();
                    var platform = httpContext.Request.Headers[DevicePlatformHeader].FirstOrDefault();
                    var clientVersion = httpContext.Request.Headers[ClientVersionHeader].FirstOrDefault();

                    var resolver = httpContext.RequestServices.GetService<ISyncDeviceResolver>();
                    var deviceContext = httpContext.RequestServices.GetService<IDeviceContext>();

                    if (resolver is not null && deviceContext is not null)
                    {
                        try
                        {
                            var device = await resolver.ResolveAsync(
                                deviceId, userId, deviceName, platform, clientVersion,
                                httpContext.RequestAborted);

                            if (device is not null)
                            {
                                deviceContext.DeviceId = device.Id;
                                logger?.LogWarning(
                                    "DeviceIdentityFilter: resolved device {DeviceId} for user {UserId} on {RequestPath}.",
                                    device.Id, userId, httpContext.Request.Path);
                            }
                            else
                            {
                                logger?.LogWarning(
                                    "DeviceIdentityFilter: resolver returned null for device {DeviceId}, user {UserId} on {RequestPath}.",
                                    deviceId, userId, httpContext.Request.Path);
                            }
                        }
                        catch (Exception ex)
                        {
                            // Device resolution is best-effort — never block the request
                            logger?.LogWarning(ex,
                                "Failed to resolve device {DeviceId} for user {UserId}. Proceeding without device context.",
                                deviceId, userId);
                        }
                    }
                    else
                    {
                        logger?.LogWarning(
                            "DeviceIdentityFilter: missing DI services — resolver={ResolverAvailable}, deviceContext={ContextAvailable} for device {DeviceId} on {RequestPath}.",
                            resolver is not null, deviceContext is not null, deviceId, httpContext.Request.Path);
                    }
                }
                else
                {
                    logger?.LogWarning(
                        "DeviceIdentityFilter: failed to parse userId from claims (NameIdentifier={NameId}, sub={Sub}) for device header {RawDeviceId} on {RequestPath}.",
                        httpContext.User.FindFirstValue(ClaimTypes.NameIdentifier),
                        httpContext.User.FindFirstValue("sub"),
                        rawDeviceId, httpContext.Request.Path);
                }
            }
            else if (!string.IsNullOrEmpty(rawDeviceId))
            {
                logger?.LogWarning(
                    "DeviceIdentityFilter: X-Device-Id header present but not a valid GUID: '{RawDeviceId}' on {RequestPath}.",
                    rawDeviceId, httpContext.Request.Path);
            }
        }

        await next();
    }
}
