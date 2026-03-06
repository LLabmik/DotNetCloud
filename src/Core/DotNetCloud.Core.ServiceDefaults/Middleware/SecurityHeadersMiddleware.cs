using Microsoft.AspNetCore.Http;

namespace DotNetCloud.Core.ServiceDefaults.Middleware;

/// <summary>
/// Middleware for adding security headers to HTTP responses.
/// </summary>
public class SecurityHeadersMiddleware
{
    private readonly RequestDelegate _next;
    private readonly SecurityHeadersOptions _options;

    /// <summary>
    /// Initializes a new instance of the <see cref="SecurityHeadersMiddleware"/> class.
    /// </summary>
    /// <param name="next">The next middleware in the pipeline.</param>
    /// <param name="options">Security headers options.</param>
    public SecurityHeadersMiddleware(
        RequestDelegate next,
        SecurityHeadersOptions options)
    {
        _next = next ?? throw new ArgumentNullException(nameof(next));
        _options = options ?? throw new ArgumentNullException(nameof(options));
    }

    /// <summary>
    /// Invokes the middleware.
    /// </summary>
    /// <param name="context">The HTTP context.</param>
    /// <returns>A task representing the middleware execution.</returns>
    public async Task InvokeAsync(HttpContext context)
    {
        // Add security headers
        if (_options.EnableContentSecurityPolicy && !string.IsNullOrEmpty(_options.ContentSecurityPolicy))
        {
            context.Response.Headers["Content-Security-Policy"] = _options.ContentSecurityPolicy;
        }

        if (_options.EnableXFrameOptions)
        {
            context.Response.Headers["X-Frame-Options"] = _options.XFrameOptionsValue;
        }

        if (_options.EnableXContentTypeOptions)
        {
            context.Response.Headers["X-Content-Type-Options"] = "nosniff";
        }

        if (_options.EnableStrictTransportSecurity && context.Request.IsHttps)
        {
            context.Response.Headers["Strict-Transport-Security"] = 
                $"max-age={_options.StrictTransportSecurityMaxAge}; includeSubDomains; preload";
        }

        if (_options.EnableReferrerPolicy)
        {
            context.Response.Headers["Referrer-Policy"] = _options.ReferrerPolicyValue;
        }

        if (_options.EnablePermissionsPolicy && !string.IsNullOrEmpty(_options.PermissionsPolicy))
        {
            context.Response.Headers["Permissions-Policy"] = _options.PermissionsPolicy;
        }

        if (_options.RemoveServerHeader)
        {
            context.Response.Headers.Remove("Server");
        }

        if (_options.RemoveXPoweredBy)
        {
            context.Response.Headers.Remove("X-Powered-By");
        }

        await _next(context);
    }
}

/// <summary>
/// Options for configuring security headers.
/// </summary>
public class SecurityHeadersOptions
{
    /// <summary>
    /// Gets or sets whether to enable Content-Security-Policy header.
    /// </summary>
    public bool EnableContentSecurityPolicy { get; set; } = true;

    /// <summary>
    /// Gets or sets the Content-Security-Policy value.
    /// </summary>
    public string ContentSecurityPolicy { get; set; } = 
        "default-src 'self'; script-src 'self' 'unsafe-inline' 'wasm-unsafe-eval'; style-src 'self' 'unsafe-inline'; img-src 'self' data: https:; font-src 'self' data:; connect-src 'self' ws: wss:; frame-ancestors 'none';";

    /// <summary>
    /// Gets or sets whether to enable X-Frame-Options header.
    /// </summary>
    public bool EnableXFrameOptions { get; set; } = true;

    /// <summary>
    /// Gets or sets the X-Frame-Options value (DENY, SAMEORIGIN, or ALLOW-FROM uri).
    /// </summary>
    public string XFrameOptionsValue { get; set; } = "DENY";

    /// <summary>
    /// Gets or sets whether to enable X-Content-Type-Options header.
    /// </summary>
    public bool EnableXContentTypeOptions { get; set; } = true;

    /// <summary>
    /// Gets or sets whether to enable Strict-Transport-Security header.
    /// </summary>
    public bool EnableStrictTransportSecurity { get; set; } = true;

    /// <summary>
    /// Gets or sets the max-age value for Strict-Transport-Security in seconds.
    /// </summary>
    public int StrictTransportSecurityMaxAge { get; set; } = 31536000; // 1 year

    /// <summary>
    /// Gets or sets whether to enable Referrer-Policy header.
    /// </summary>
    public bool EnableReferrerPolicy { get; set; } = true;

    /// <summary>
    /// Gets or sets the Referrer-Policy value.
    /// </summary>
    public string ReferrerPolicyValue { get; set; } = "strict-origin-when-cross-origin";

    /// <summary>
    /// Gets or sets whether to enable Permissions-Policy header.
    /// </summary>
    public bool EnablePermissionsPolicy { get; set; } = true;

    /// <summary>
    /// Gets or sets the Permissions-Policy value.
    /// </summary>
    public string PermissionsPolicy { get; set; } = 
        "geolocation=(), microphone=(), camera=()";

    /// <summary>
    /// Gets or sets whether to remove the Server header.
    /// </summary>
    public bool RemoveServerHeader { get; set; } = true;

    /// <summary>
    /// Gets or sets whether to remove the X-Powered-By header.
    /// </summary>
    public bool RemoveXPoweredBy { get; set; } = true;
}
