using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.DependencyInjection;

namespace DotNetCloud.Core.Auth.Introspection;

/// <summary>
/// Extension methods for registering the introspection authentication handler.
/// </summary>
public static class IntrospectionAuthenticationExtensions
{
    /// <summary>
    /// The authentication scheme name for the introspection handler.
    /// </summary>
    public const string SchemeName = "Introspection";

    /// <summary>
    /// Adds the token introspection authentication handler. Module hosts use this
    /// instead of JwtBearer to validate tokens by calling Core.Server's
    /// TokenIntrospection gRPC service.
    /// </summary>
    public static AuthenticationBuilder AddIntrospection(
        this AuthenticationBuilder builder,
        Action<IntrospectionAuthenticationOptions>? configureOptions = null)
    {
        return builder.AddScheme<IntrospectionAuthenticationOptions,
            IntrospectionAuthenticationHandler>(SchemeName, configureOptions);
    }

    /// <summary>
    /// Adds the token introspection authentication handler with a custom scheme name.
    /// </summary>
    public static AuthenticationBuilder AddIntrospection(
        this AuthenticationBuilder builder,
        string authenticationScheme,
        Action<IntrospectionAuthenticationOptions>? configureOptions = null)
    {
        return builder.AddScheme<IntrospectionAuthenticationOptions,
            IntrospectionAuthenticationHandler>(authenticationScheme, configureOptions);
    }
}
