namespace DotNetCloud.Modules.Files.Services;

/// <summary>
/// Provides access to the current request's device identity.
/// Populated by the <c>DeviceIdentityFilter</c> action filter from the <c>X-Device-Id</c> header.
/// </summary>
public interface IDeviceContext
{
    /// <summary>
    /// The device ID for the current request, or null if the client did not send <c>X-Device-Id</c>.
    /// </summary>
    Guid? DeviceId { get; set; }
}

/// <summary>
/// Default scoped implementation of <see cref="IDeviceContext"/>.
/// </summary>
public sealed class DeviceContext : IDeviceContext
{
    /// <inheritdoc />
    public Guid? DeviceId { get; set; }
}
