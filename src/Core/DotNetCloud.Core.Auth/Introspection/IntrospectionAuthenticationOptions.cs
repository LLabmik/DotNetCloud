using Microsoft.AspNetCore.Authentication;

namespace DotNetCloud.Core.Auth.Introspection;

/// <summary>
/// Configuration options for <see cref="IntrospectionAuthenticationHandler"/>.
/// </summary>
public sealed class IntrospectionAuthenticationOptions
    : AuthenticationSchemeOptions
{
    /// <summary>
    /// The calling module's ID. Defaults to DOTNETCLOUD_MODULE_ID env var.
    /// </summary>
    public string? ModuleId { get; set; }

    /// <summary>
    /// The required audience for tokens processed by this module.
    /// Defaults to <see cref="ModuleId"/> if not set.
    /// </summary>
    public string? RequiredAudience { get; set; }

    /// <summary>
    /// How long to cache successful introspection results.
    /// Default: 1 minute. Shorter = more introspection calls but faster revocation.
    /// </summary>
    public TimeSpan CacheDuration { get; set; } = TimeSpan.FromMinutes(1);
}
