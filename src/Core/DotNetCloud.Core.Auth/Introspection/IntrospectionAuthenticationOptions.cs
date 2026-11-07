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
    /// The audience a token must carry for this module, or <see langword="null"/> for no audience
    /// requirement (the default).
    /// </summary>
    /// <remarks>
    /// When set, a token whose <c>aud</c> claim does not include this value is treated as inactive.
    /// It is deliberately not defaulted to <see cref="ModuleId"/>: OpenIddict issues client-scoped
    /// audiences today, so an implicit module audience would reject every valid token. Module-scoped
    /// audiences arrive with the per-module OIDC clients (phase A3).
    /// </remarks>
    public string? RequiredAudience { get; set; }

    /// <summary>
    /// How long to cache successful introspection results.
    /// Default: 1 minute. Shorter = more introspection calls but faster revocation.
    /// </summary>
    public TimeSpan CacheDuration { get; set; } = TimeSpan.FromMinutes(1);
}
