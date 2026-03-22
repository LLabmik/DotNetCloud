namespace DotNetCloud.Core.Server.Configuration;

/// <summary>
/// Configuration options for CORS policies.
/// </summary>
public sealed class CorsOptions
{
    /// <summary>
    /// Configuration section key.
    /// </summary>
    public const string SectionName = "Cors";

    /// <summary>
    /// Gets or sets the allowed origins.
    /// </summary>
    public string[] AllowedOrigins { get; set; } = [];

    /// <summary>
    /// Gets or sets the allowed HTTP methods. Defaults to all methods.
    /// </summary>
    public string[] AllowedMethods { get; set; } = ["GET", "POST", "PUT", "PATCH", "DELETE", "OPTIONS", "HEAD"];

    /// <summary>
    /// Gets or sets the allowed request headers.
    /// </summary>
    public string[] AllowedHeaders { get; set; } = ["Authorization", "Content-Type", "Accept", "X-Requested-With", "X-Api-Version"];

    /// <summary>
    /// Gets or sets the headers exposed to the client.
    /// </summary>
    public string[] ExposedHeaders { get; set; } =
    [
        "X-Api-Version",
        "X-Api-Deprecated",
        "X-Api-Deprecation-Warning",
        "X-RateLimit-Limit",
        "X-RateLimit-Remaining",
        "X-RateLimit-Reset",
        "Retry-After",
        "Sunset"
    ];

    /// <summary>
    /// Gets or sets whether credentials (cookies, authorization headers) are allowed. Defaults to true.
    /// </summary>
    public bool AllowCredentials { get; set; } = true;

    /// <summary>
    /// Gets or sets the preflight cache duration in seconds. Defaults to 600 (10 minutes).
    /// </summary>
    public int PreflightMaxAgeSeconds { get; set; } = 600;
}

/// <summary>
/// Extension methods for configuring CORS policies.
/// </summary>
public static class CorsConfiguration
{
    /// <summary>
    /// The name of the DotNetCloud CORS policy.
    /// </summary>
    public const string PolicyName = "DotNetCloudCors";

    /// <summary>
    /// Adds DotNetCloud CORS configuration.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configuration">The application configuration.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddDotNetCloudCors(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var corsOptions = new CorsOptions();
        configuration.GetSection(CorsOptions.SectionName).Bind(corsOptions);

        services.AddCors(options =>
        {
            options.AddPolicy(PolicyName, policy =>
            {
                // Allowed origins — MUST be explicitly configured.
                // AllowAnyOrigin is never used because it disables CORS protection entirely.
                if (corsOptions.AllowedOrigins.Length > 0)
                {
                    policy.WithOrigins(corsOptions.AllowedOrigins);
                }
                else
                {
                    // Default to self-origin only when no origins are configured.
                    // In production, AllowedOrigins MUST be explicitly set in configuration.
                    // AllowAnyOrigin() is intentionally NOT used — it would allow any website
                    // to make credentialed cross-origin requests to the API.
                    policy.SetIsOriginAllowed(_ => false);
                }

                // Allowed methods
                if (corsOptions.AllowedMethods.Length > 0)
                {
                    policy.WithMethods(corsOptions.AllowedMethods);
                }
                else
                {
                    policy.AllowAnyMethod();
                }

                // Allowed headers
                if (corsOptions.AllowedHeaders.Length > 0)
                {
                    policy.WithHeaders(corsOptions.AllowedHeaders);
                }
                else
                {
                    policy.AllowAnyHeader();
                }

                // Exposed headers
                if (corsOptions.ExposedHeaders.Length > 0)
                {
                    policy.WithExposedHeaders(corsOptions.ExposedHeaders);
                }

                // Credentials
                if (corsOptions.AllowCredentials && corsOptions.AllowedOrigins.Length > 0)
                {
                    policy.AllowCredentials();
                }

                // Preflight cache
                policy.SetPreflightMaxAge(TimeSpan.FromSeconds(corsOptions.PreflightMaxAgeSeconds));
            });
        });

        return services;
    }
}
