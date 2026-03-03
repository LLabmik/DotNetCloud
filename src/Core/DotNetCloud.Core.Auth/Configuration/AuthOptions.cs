namespace DotNetCloud.Core.Auth.Configuration;

/// <summary>
/// Strongly-typed configuration options for DotNetCloud authentication.
/// </summary>
/// <remarks>
/// Bind from the <c>"Auth"</c> configuration section via
/// <c>services.Configure&lt;AuthOptions&gt;(config.GetSection(AuthOptions.SectionName))</c>.
/// </remarks>
public sealed class AuthOptions
{
    /// <summary>
    /// The configuration section name.
    /// </summary>
    public const string SectionName = "Auth";

    /// <summary>
    /// Gets or sets the access token lifetime in minutes. Defaults to 60.
    /// </summary>
    public int AccessTokenLifetimeMinutes { get; set; } = 60;

    /// <summary>
    /// Gets or sets the refresh token lifetime in days. Defaults to 7.
    /// </summary>
    public int RefreshTokenLifetimeDays { get; set; } = 7;

    /// <summary>
    /// Gets or sets the authorization server issuer URI used in JWT tokens.
    /// </summary>
    public string Issuer { get; set; } = "https://localhost:5001";

    /// <summary>
    /// Gets or sets external OAuth2 provider configuration.
    /// </summary>
    public ExternalAuthOptions ExternalAuth { get; set; } = new();
}

/// <summary>
/// Configuration options for external OAuth2/OIDC authentication providers.
/// </summary>
public sealed class ExternalAuthOptions
{
    /// <summary>
    /// Gets or sets Google OAuth2 configuration. <see langword="null"/> if not enabled.
    /// </summary>
    public GoogleAuthOptions? Google { get; set; }

    /// <summary>
    /// Gets or sets Microsoft OAuth2 configuration. <see langword="null"/> if not enabled.
    /// </summary>
    public MicrosoftAuthOptions? Microsoft { get; set; }
}

/// <summary>
/// OAuth2 client credentials for Google sign-in.
/// </summary>
public sealed class GoogleAuthOptions
{
    /// <summary>
    /// Gets or sets the Google OAuth2 client ID.
    /// </summary>
    public string ClientId { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the Google OAuth2 client secret.
    /// </summary>
    public string ClientSecret { get; set; } = string.Empty;
}

/// <summary>
/// OAuth2 client credentials for Microsoft sign-in.
/// </summary>
public sealed class MicrosoftAuthOptions
{
    /// <summary>
    /// Gets or sets the Microsoft OAuth2 application (client) ID.
    /// </summary>
    public string ClientId { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the Microsoft OAuth2 client secret.
    /// </summary>
    public string ClientSecret { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the tenant ID. Use <c>"common"</c> for multi-tenant apps.
    /// </summary>
    public string TenantId { get; set; } = "common";
}
