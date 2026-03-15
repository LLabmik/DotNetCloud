using Microsoft.Extensions.Logging;
using System.Runtime.InteropServices;

namespace DotNetCloud.Client.Core.Api;

/// <summary>
/// Delegating handler that attaches device identity headers (<c>X-Device-Id</c>,
/// <c>X-Device-Name</c>, <c>X-Device-Platform</c>, <c>X-Client-Version</c>)
/// to every outgoing HTTP request.
/// </summary>
public sealed class DeviceIdentityHandler : DelegatingHandler
{
    private readonly Guid _deviceId;
    private readonly string _deviceName;
    private readonly string _platform;
    private readonly string _clientVersion;
    private readonly ILogger<DeviceIdentityHandler> _logger;

    /// <summary>Initializes a new <see cref="DeviceIdentityHandler"/>.</summary>
    public DeviceIdentityHandler(
        Guid deviceId,
        ILogger<DeviceIdentityHandler> logger)
    {
        _deviceId = deviceId;
        _logger = logger;
        _deviceName = GetDeviceName();
        _platform = GetPlatformName();
        _clientVersion = GetClientVersion();
    }

    /// <inheritdoc/>
    protected override Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request, CancellationToken cancellationToken)
    {
        request.Headers.TryAddWithoutValidation("X-Device-Id", _deviceId.ToString("D"));
        request.Headers.TryAddWithoutValidation("X-Device-Name", _deviceName);
        request.Headers.TryAddWithoutValidation("X-Device-Platform", _platform);
        request.Headers.TryAddWithoutValidation("X-Client-Version", _clientVersion);

        return base.SendAsync(request, cancellationToken);
    }

    private static string GetDeviceName()
    {
        try
        {
            return Environment.MachineName;
        }
        catch
        {
            return "unknown";
        }
    }

    private static string GetPlatformName()
    {
        if (OperatingSystem.IsWindows()) return "Windows";
        if (OperatingSystem.IsLinux()) return "Linux";
        if (OperatingSystem.IsMacOS()) return "macOS";
        if (OperatingSystem.IsAndroid()) return "Android";
        if (OperatingSystem.IsIOS()) return "iOS";
        return RuntimeInformation.OSDescription;
    }

    private static string GetClientVersion()
    {
        var assembly = typeof(DeviceIdentityHandler).Assembly;
        var version = assembly.GetName().Version;
        return version is not null ? version.ToString(3) : "0.0.0";
    }
}
