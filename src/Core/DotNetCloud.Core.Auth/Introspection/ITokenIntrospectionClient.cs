namespace DotNetCloud.Core.Auth.Introspection;

/// <summary>
/// Client for the TokenIntrospection gRPC service hosted by Core.Server.
/// Module hosts call this to validate bearer tokens received from clients.
/// </summary>
public interface ITokenIntrospectionClient
{
    /// <summary>
    /// Validates a bearer token by calling Core.Server's TokenIntrospection gRPC service.
    /// </summary>
    /// <param name="token">The raw JWT to validate.</param>
    /// <param name="moduleId">The calling module's ID.</param>
    /// <param name="requiredAudience">
    /// The required audience the token must contain. Usually the module's ID
    /// (e.g., "dotnetcloud.files"). Pass null to skip audience check.
    /// </param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The introspection result.</returns>
    Task<IntrospectionResult> IntrospectAsync(
        string token,
        string moduleId,
        string? requiredAudience,
        CancellationToken cancellationToken = default);
}
