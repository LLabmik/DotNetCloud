using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ApplicationModels;
using Microsoft.AspNetCore.Routing;

namespace DotNetCloud.Core.Server.Configuration;

/// <summary>
/// Configuration options for API versioning.
/// </summary>
public sealed class ApiVersioningOptions
{
    /// <summary>
    /// Configuration section key.
    /// </summary>
    public const string SectionName = "ApiVersioning";

    /// <summary>
    /// Gets or sets the current API version. Defaults to "1".
    /// </summary>
    public string CurrentVersion { get; set; } = "1";

    /// <summary>
    /// Gets or sets the minimum supported API version. Defaults to "1".
    /// </summary>
    public string MinimumVersion { get; set; } = "1";

    /// <summary>
    /// Gets or sets the list of deprecated API versions.
    /// </summary>
    public string[] DeprecatedVersions { get; set; } = [];

    /// <summary>
    /// Gets or sets the API route prefix template. Defaults to "api/v{version}".
    /// </summary>
    public string RoutePrefix { get; set; } = "api/v{version}";

    /// <summary>
    /// Gets or sets the deprecation warning message template.
    /// </summary>
    public string DeprecationWarningTemplate { get; set; } =
        "API version {0} is deprecated and will be removed on {1}. Please migrate to version {2}.";

    /// <summary>
    /// Gets or sets the deprecation dates per version (version → date).
    /// </summary>
    public Dictionary<string, DateTime> DeprecationDates { get; set; } = new();
}

/// <summary>
/// Represents an API version with parsing and comparison support.
/// </summary>
public sealed class ApiVersion : IComparable<ApiVersion>, IEquatable<ApiVersion>
{
    /// <summary>
    /// Gets the major version number.
    /// </summary>
    public int Major { get; }

    /// <summary>
    /// Gets the optional minor version number.
    /// </summary>
    public int? Minor { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="ApiVersion"/> class.
    /// </summary>
    /// <param name="major">The major version number.</param>
    /// <param name="minor">The optional minor version number.</param>
    public ApiVersion(int major, int? minor = null)
    {
        Major = major;
        Minor = minor;
    }

    /// <summary>
    /// Parses a version string like "1", "1.0", "2.1" into an <see cref="ApiVersion"/>.
    /// </summary>
    /// <param name="version">The version string.</param>
    /// <returns>The parsed API version.</returns>
    /// <exception cref="FormatException">Thrown when the version string is invalid.</exception>
    public static ApiVersion Parse(string version)
    {
        if (string.IsNullOrWhiteSpace(version))
        {
            throw new FormatException("API version string cannot be null or empty.");
        }

        var parts = version.Split('.');

        if (!int.TryParse(parts[0], out var major) || major < 0)
        {
            throw new FormatException($"Invalid API version: '{version}'. Major version must be a non-negative integer.");
        }

        int? minor = null;
        if (parts.Length > 1)
        {
            if (!int.TryParse(parts[1], out var minorValue) || minorValue < 0)
            {
                throw new FormatException($"Invalid API version: '{version}'. Minor version must be a non-negative integer.");
            }
            minor = minorValue;
        }

        return new ApiVersion(major, minor);
    }

    /// <summary>
    /// Attempts to parse a version string.
    /// </summary>
    /// <param name="version">The version string.</param>
    /// <param name="result">The parsed API version, or null if parsing fails.</param>
    /// <returns>True if parsing succeeded; otherwise false.</returns>
    public static bool TryParse(string? version, out ApiVersion? result)
    {
        result = null;

        if (string.IsNullOrWhiteSpace(version))
        {
            return false;
        }

        try
        {
            result = Parse(version);
            return true;
        }
        catch (FormatException)
        {
            return false;
        }
    }

    /// <inheritdoc/>
    public int CompareTo(ApiVersion? other)
    {
        if (other is null)
        {
            return 1;
        }

        var majorComparison = Major.CompareTo(other.Major);
        if (majorComparison != 0)
        {
            return majorComparison;
        }

        return (Minor ?? 0).CompareTo(other.Minor ?? 0);
    }

    /// <inheritdoc/>
    public bool Equals(ApiVersion? other)
    {
        if (other is null)
        {
            return false;
        }

        return Major == other.Major && (Minor ?? 0) == (other.Minor ?? 0);
    }

    /// <inheritdoc/>
    public override bool Equals(object? obj) => obj is ApiVersion other && Equals(other);

    /// <inheritdoc/>
    public override int GetHashCode() => HashCode.Combine(Major, Minor ?? 0);

    /// <inheritdoc/>
    public override string ToString() => Minor.HasValue ? $"{Major}.{Minor}" : Major.ToString();
}

/// <summary>
/// Middleware that handles API version negotiation and deprecation warnings.
/// </summary>
public sealed class ApiVersionMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ApiVersioningOptions _options;
    private readonly ILogger<ApiVersionMiddleware> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="ApiVersionMiddleware"/> class.
    /// </summary>
    /// <param name="next">The next middleware in the pipeline.</param>
    /// <param name="options">API versioning options.</param>
    /// <param name="logger">The logger.</param>
    public ApiVersionMiddleware(
        RequestDelegate next,
        ApiVersioningOptions options,
        ILogger<ApiVersionMiddleware> logger)
    {
        _next = next ?? throw new ArgumentNullException(nameof(next));
        _options = options ?? throw new ArgumentNullException(nameof(options));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Invokes the middleware.
    /// </summary>
    /// <param name="context">The HTTP context.</param>
    public async Task InvokeAsync(HttpContext context)
    {
        var path = context.Request.Path.Value;

        // Only process API paths
        if (path is not null && path.StartsWith("/api/v", StringComparison.OrdinalIgnoreCase))
        {
            var version = ExtractVersionFromPath(path);

            if (version is not null)
            {
                // Add version to response headers
                context.Response.Headers["X-Api-Version"] = version;

                // Check if the version is deprecated
                if (_options.DeprecatedVersions.Contains(version))
                {
                    var currentVersion = _options.CurrentVersion;
                    var deprecationDate = _options.DeprecationDates.TryGetValue(version, out var date)
                        ? date.ToString("yyyy-MM-dd")
                        : "a future date";

                    var warning = string.Format(_options.DeprecationWarningTemplate, version, deprecationDate, currentVersion);
                    context.Response.Headers["X-Api-Deprecated"] = "true";
                    context.Response.Headers["X-Api-Deprecation-Warning"] = warning;
                    context.Response.Headers["Sunset"] = deprecationDate;

                    _logger.LogWarning(
                        "Deprecated API version {Version} requested: {Path}",
                        version,
                        path);
                }

                // Check if version is below minimum supported
                if (ApiVersion.TryParse(version, out var parsedVersion) &&
                    ApiVersion.TryParse(_options.MinimumVersion, out var minVersion) &&
                    parsedVersion!.CompareTo(minVersion!) < 0)
                {
                    _logger.LogWarning(
                        "Unsupported API version {Version} requested (minimum: {MinVersion}): {Path}",
                        version,
                        _options.MinimumVersion,
                        path);

                    context.Response.StatusCode = StatusCodes.Status400BadRequest;
                    context.Response.ContentType = "application/json";
                    await context.Response.WriteAsJsonAsync(new
                    {
                        success = false,
                        code = "API_VERSION_NOT_SUPPORTED",
                        message = $"API version '{version}' is no longer supported. Minimum supported version is '{_options.MinimumVersion}'."
                    });
                    return;
                }
            }
        }

        await _next(context);
    }

    private static string? ExtractVersionFromPath(string path)
    {
        // Extract version from paths like /api/v1/... or /api/v2.1/...
        const string prefix = "/api/v";
        if (!path.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        var afterPrefix = path.AsSpan(prefix.Length);
        var slashIndex = afterPrefix.IndexOf('/');
        var versionSpan = slashIndex >= 0 ? afterPrefix[..slashIndex] : afterPrefix;

        return versionSpan.Length > 0 ? versionSpan.ToString() : null;
    }
}

/// <summary>
/// Extension methods for configuring API versioning.
/// </summary>
public static class ApiVersioningExtensions
{
    /// <summary>
    /// Adds API versioning services and configuration.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configuration">The application configuration.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddDotNetCloudApiVersioning(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var options = new ApiVersioningOptions();
        configuration.GetSection(ApiVersioningOptions.SectionName)?.Bind(options);
        services.AddSingleton(options);

        return services;
    }

    /// <summary>
    /// Adds the API versioning middleware to the pipeline.
    /// </summary>
    /// <param name="app">The web application.</param>
    /// <returns>The web application for chaining.</returns>
    public static WebApplication UseApiVersioning(this WebApplication app)
    {
        app.UseMiddleware<ApiVersionMiddleware>();
        return app;
    }
}
