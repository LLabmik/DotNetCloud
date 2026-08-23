using DotNetCloud.Core.Grpc;
using DotNetCloud.Modules.About;
using DotNetCloud.Modules.About.Host.Services;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Diagnostics;

public static partial class Program
{
    public static async Task Main(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);

        // Load shared config from DOTNETCLOUD_CONFIG_DIR
        var configDir = Environment.GetEnvironmentVariable("DOTNETCLOUD_CONFIG_DIR");
        if (!string.IsNullOrEmpty(configDir))
        {
            var p = Path.Combine(configDir, "config.json");
            if (File.Exists(p))
                builder.Configuration.AddJsonFile(p, optional: true, reloadOnChange: false);
        }

        // Bind gRPC endpoint from DOTNETCLOUD_GRPC_ENDPOINT (set by ProcessSupervisor)
        var grpcEndpoint = Environment.GetEnvironmentVariable("DOTNETCLOUD_GRPC_ENDPOINT");
        if (!string.IsNullOrEmpty(grpcEndpoint))
        {
            var uri = new Uri(grpcEndpoint.Replace("unix://", "http://").Replace("net.pipe://", "http://"));
            builder.WebHost.ConfigureKestrel(o =>
                o.Listen(System.Net.IPAddress.Loopback, uri.Port, l => l.Protocols = HttpProtocols.Http2));
        }

        // Share DataProtection keys with Core.Server so auth cookies from the proxy are valid.
        var dataProtectionDir = Environment.GetEnvironmentVariable("DOTNETCLOUD_DATA_DIR");
        var dpKeysPath = !string.IsNullOrWhiteSpace(dataProtectionDir)
            ? Path.Combine(dataProtectionDir, "data-protection-keys")
            : Path.Combine(builder.Environment.ContentRootPath, "data-protection-keys");
        Directory.CreateDirectory(dpKeysPath);
        builder.Services.AddDataProtection()
            .SetApplicationName("DotNetCloud")
            .PersistKeysToFileSystem(new DirectoryInfo(dpKeysPath));

        // Cookie auth — same cookie name as Core.Server. SecurePolicy=None because
        // the YARP proxy forwards over HTTP (localhost) with X-Forwarded-Proto set by proxy.
        builder.Services.AddAuthentication("Identity.Application")
            .AddCookie("Identity.Application", options =>
            {
                options.Cookie.Name = ".AspNetCore.Identity.Application";
                options.Cookie.HttpOnly = true;
                options.Cookie.SameSite = SameSiteMode.Lax;
                options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
                options.Cookie.IsEssential = true;
                options.ExpireTimeSpan = TimeSpan.FromHours(24);
                options.SlidingExpiration = true;

                // Skip Identity user-store lookup — the cookie was already validated
                // by Core.Server. Just accept the decrypted principal as-is.
                options.Events.OnValidatePrincipal = static context =>
                {
                    if (context.Principal?.Identity?.IsAuthenticated == true)
                        return Task.CompletedTask;
                    context.RejectPrincipal();
                    return Task.CompletedTask;
                };

                // Return 401 for API requests instead of redirecting to login.
                options.Events.OnRedirectToLogin = context =>
                {
                    context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                    return Task.CompletedTask;
                };
                options.Events.OnRedirectToAccessDenied = context =>
                {
                    context.Response.StatusCode = StatusCodes.Status403Forbidden;
                    return Task.CompletedTask;
                };
            });

        builder.Services.AddAuthorization();

        // Register the gRPC-backed audit logger (SOC 2 CC4) — routes to Core.Server.
        builder.Services.AddAuditLogger();

        builder.Services.AddSingleton<AboutModule>();

        builder.Services.AddGrpc();
        builder.Services.AddHealthChecks().AddCheck<AboutHealthCheck>("about_module");

        var app = builder.Build();

        // Show full exception details for debugging; remove in production.
        app.UseDeveloperExceptionPage();

        // Trust X-Forwarded-Proto from the YARP proxy so __Host- cookies work over HTTP.
        app.UseForwardedHeaders(new ForwardedHeadersOptions
        {
            ForwardedHeaders = ForwardedHeaders.XForwardedProto,
            KnownProxies = { System.Net.IPAddress.Loopback, System.Net.IPAddress.IPv6Loopback },
        });
        app.UseAuthentication();
        app.UseAuthorization();

        app.MapGrpcService<AboutGrpcService>();
        app.MapGrpcService<AboutLifecycleService>();
        app.MapHealthChecks("/health");
        app.MapGet("/", () => Results.Ok(new { module = "dotnetcloud.about", version = "1.0.0", status = "running" }));

        await app.RunAsync();
    }
}
