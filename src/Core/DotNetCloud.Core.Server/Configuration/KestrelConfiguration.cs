using System.Net;
using System.Security.Cryptography.X509Certificates;
using Microsoft.AspNetCore.Server.Kestrel.Core;

namespace DotNetCloud.Core.Server.Configuration;

/// <summary>
/// Configuration options for the Kestrel web server.
/// </summary>
public sealed class KestrelOptions
{
    /// <summary>
    /// Configuration section key.
    /// </summary>
    public const string SectionName = "Kestrel";

    /// <summary>
    /// Gets or sets the HTTP port. Defaults to 5080.
    /// </summary>
    public int HttpPort { get; set; } = 5080;

    /// <summary>
    /// Gets or sets the HTTPS port. Defaults to 5443.
    /// </summary>
    public int HttpsPort { get; set; } = 5443;

    /// <summary>
    /// Gets or sets whether HTTPS is enabled. Defaults to true.
    /// </summary>
    public bool EnableHttps { get; set; } = true;

    /// <summary>
    /// Gets or sets whether HTTP/2 is enabled. Defaults to true.
    /// </summary>
    public bool EnableHttp2 { get; set; } = true;

    /// <summary>
    /// Gets or sets the TLS certificate path.
    /// </summary>
    public string? CertificatePath { get; set; }

    /// <summary>
    /// Gets or sets the TLS certificate password.
    /// </summary>
    public string? CertificatePassword { get; set; }

    /// <summary>
    /// Gets or sets the maximum request body size in bytes. Defaults to 50MB.
    /// </summary>
    public long MaxRequestBodySize { get; set; } = 50 * 1024 * 1024;

    /// <summary>
    /// Gets or sets the request header timeout in seconds. Defaults to 30.
    /// </summary>
    public int RequestHeaderTimeoutSeconds { get; set; } = 30;

    /// <summary>
    /// Gets or sets the keep-alive timeout in seconds. Defaults to 120.
    /// </summary>
    public int KeepAliveTimeoutSeconds { get; set; } = 120;

    /// <summary>
    /// Gets or sets the maximum concurrent connections. Null means unlimited.
    /// </summary>
    public long? MaxConcurrentConnections { get; set; }

    /// <summary>
    /// Gets or sets the listen addresses. Empty means listen on all interfaces.
    /// </summary>
    public string[] ListenAddresses { get; set; } = [];
}

/// <summary>
/// Extension methods for configuring Kestrel server options.
/// </summary>
public static class KestrelConfiguration
{
    /// <summary>
    /// Configures Kestrel with DotNetCloud default settings.
    /// </summary>
    /// <param name="builder">The web application builder.</param>
    /// <returns>The web application builder for chaining.</returns>
    public static WebApplicationBuilder ConfigureKestrel(this WebApplicationBuilder builder)
    {
        var kestrelOptions = new KestrelOptions();
        builder.Configuration.GetSection(KestrelOptions.SectionName).Bind(kestrelOptions);

        builder.WebHost.ConfigureKestrel(options =>
        {
            ConfigureEndpoints(options, kestrelOptions, builder.Environment);
            ConfigureLimits(options, kestrelOptions);
        });

        return builder;
    }

    private static void ConfigureEndpoints(
        KestrelServerOptions options,
        KestrelOptions config,
        IWebHostEnvironment environment)
    {
        // If specific listen addresses are provided, use them
        if (config.ListenAddresses.Length > 0)
        {
            foreach (var address in config.ListenAddresses)
            {
                options.Listen(IPAddress.Parse(address), config.HttpPort, listenOptions =>
                {
                    ConfigureHttpProtocols(listenOptions, config);
                });

                if (config.EnableHttps)
                {
                    options.Listen(IPAddress.Parse(address), config.HttpsPort, listenOptions =>
                    {
                        ConfigureHttpProtocols(listenOptions, config);
                        ConfigureTls(listenOptions, config, environment);
                    });
                }
            }
        }
        else
        {
            // Listen on any IP
            options.ListenAnyIP(config.HttpPort, listenOptions =>
            {
                ConfigureHttpProtocols(listenOptions, config);
            });

            if (config.EnableHttps)
            {
                options.ListenAnyIP(config.HttpsPort, listenOptions =>
                {
                    ConfigureHttpProtocols(listenOptions, config);
                    ConfigureTls(listenOptions, config, environment);
                });
            }
        }
    }

    private static void ConfigureHttpProtocols(ListenOptions listenOptions, KestrelOptions config)
    {
        listenOptions.Protocols = config.EnableHttp2
            ? HttpProtocols.Http1AndHttp2
            : HttpProtocols.Http1;
    }

    private static void ConfigureTls(
        ListenOptions listenOptions,
        KestrelOptions config,
        IWebHostEnvironment environment)
    {
        if (!string.IsNullOrEmpty(config.CertificatePath))
        {
            if (!File.Exists(config.CertificatePath))
            {
                throw new FileNotFoundException(
                    $"TLS certificate not found at '{config.CertificatePath}'. " +
                    "Provide a valid certificate path or run 'dotnetcloud setup' to reconfigure HTTPS.");
            }

            listenOptions.UseHttps(config.CertificatePath, config.CertificatePassword ?? string.Empty);
        }
        else if (environment.IsDevelopment())
        {
            // Use the development certificate in development mode
            listenOptions.UseHttps();
        }
        else
        {
            throw new InvalidOperationException(
                "HTTPS is enabled but no TLS certificate path is configured. " +
                "Set Kestrel:CertificatePath in appsettings.json or run 'dotnetcloud setup' to configure HTTPS.");
        }
    }

    private static void ConfigureLimits(KestrelServerOptions options, KestrelOptions config)
    {
        options.Limits.MaxRequestBodySize = config.MaxRequestBodySize;
        options.Limits.RequestHeadersTimeout = TimeSpan.FromSeconds(config.RequestHeaderTimeoutSeconds);
        options.Limits.KeepAliveTimeout = TimeSpan.FromSeconds(config.KeepAliveTimeoutSeconds);

        if (config.MaxConcurrentConnections.HasValue)
        {
            options.Limits.MaxConcurrentConnections = config.MaxConcurrentConnections.Value;
        }
    }
}
