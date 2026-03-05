using DotNetCloud.Core.Auth.Extensions;
using DotNetCloud.Core.Data.Entities.Identity;
using DotNetCloud.Core.Data.Extensions;
using DotNetCloud.Core.Data.Initialization;
using DotNetCloud.Core.Localization;
using DotNetCloud.Core.Server.Configuration;
using DotNetCloud.Core.Server.Extensions;
using DotNetCloud.Core.Server.Initialization;
using DotNetCloud.Core.Server.Middleware;
using DotNetCloud.Core.ServiceDefaults.Extensions;
using DotNetCloud.Core.ServiceDefaults.Telemetry;
using DotNetCloud.UI.Web.Client.Services;
using DotNetCloud.UI.Web.Services;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Identity;

namespace DotNetCloud.Core.Server;

/// <summary>
/// Entry point for the DotNetCloud Core Server application.
/// </summary>
public class Program
{
    /// <summary>
    /// Application entry point.
    /// </summary>
    public static async Task Main(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);

        ConfigureServices(builder);

        var app = builder.Build();

        ConfigurePipeline(app);

        // Migrate and seed database
        using (var scope = app.Services.CreateScope())
        {
            var dbInitializer = scope.ServiceProvider.GetRequiredService<DbInitializer>();
            await dbInitializer.InitializeAsync();

            // Create the initial admin user (idempotent — only when no users exist)
            var adminSeeder = new AdminSeeder(
                scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>(),
                app.Configuration,
                scope.ServiceProvider.GetRequiredService<ILoggerFactory>().CreateLogger<AdminSeeder>());
            await adminSeeder.SeedAsync();
        }

        app.Run();
    }

    /// <summary>
    /// Registers all services for the DotNetCloud server.
    /// Separated from <see cref="Main"/> so <c>WebApplicationFactory</c> can override services.
    /// </summary>
    public static void ConfigureServices(WebApplicationBuilder builder)
    {
        // Configure Kestrel (HTTPS/TLS, HTTP/2, listener addresses, limits)
        builder.ConfigureKestrel();

        // Add service defaults (logging, telemetry, health checks)
        builder.AddDotNetCloudServiceDefaults();

        // Add authentication and authorization
        builder.Services.AddDotNetCloudAuth(builder.Configuration);

        // Add database
        var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
            ?? throw new InvalidOperationException("Connection string 'DefaultConnection' not found.");
        builder.Services.AddDotNetCloudDbContext(connectionString);

        // Add controllers
        builder.Services.AddControllers();

        // Add localization services for i18n support
        builder.Services.AddLocalization();

        // Add Blazor (InteractiveAuto = Server + WebAssembly)
        builder.Services.AddRazorComponents()
            .AddInteractiveServerComponents()
            .AddInteractiveWebAssemblyComponents();

        // Blazor UI services (server-side prerendering needs these too)
        builder.Services.AddSingleton<ModuleUiRegistry>();
        builder.Services.AddSingleton<ToastService>();
        builder.Services.AddScoped(sp =>
        {
            var nav = sp.GetRequiredService<NavigationManager>();
            return new HttpClient { BaseAddress = new Uri(nav.BaseUri) };
        });
        builder.Services.AddScoped<DotNetCloudApiClient>();

        // Add OpenAPI/Swagger with DotNetCloud configuration
        builder.Services.AddDotNetCloudOpenApi(builder.Configuration);

        // Add API versioning
        builder.Services.AddDotNetCloudApiVersioning(builder.Configuration);

        // Add CORS with enhanced configuration
        builder.Services.AddDotNetCloudCors(builder.Configuration);

        // Add rate limiting
        builder.Services.AddDotNetCloudRateLimiting(builder.Configuration);

        // Add SignalR real-time communication
        builder.Services.AddDotNetCloudSignalR(builder.Configuration);

        // Configure forwarded headers for reverse proxy support
        builder.Services.Configure<Microsoft.AspNetCore.Builder.ForwardedHeadersOptions>(options =>
        {
            options.ForwardedHeaders =
                Microsoft.AspNetCore.HttpOverrides.ForwardedHeaders.XForwardedFor |
                Microsoft.AspNetCore.HttpOverrides.ForwardedHeaders.XForwardedProto |
                Microsoft.AspNetCore.HttpOverrides.ForwardedHeaders.XForwardedHost;
        });
    }

    /// <summary>
    /// Configures the HTTP pipeline. Called after <see cref="ConfigureServices"/>.
    /// Separated so <c>WebApplicationFactory</c> can inspect or modify the pipeline.
    /// </summary>
    public static void ConfigurePipeline(WebApplication app)
    {
        // Forwarded headers (must be first for reverse proxy support)
        app.UseForwardedHeaders();

        // Apply middleware (security headers, exception handler, request logging)
        app.UseDotNetCloudMiddleware();

        // Map health checks
        app.MapDotNetCloudHealthChecks();

        // Map Prometheus metrics scraping endpoint (/metrics) when enabled
        app.MapDotNetCloudPrometheus();

        // OpenAPI/Swagger UI (development only)
        app.UseDotNetCloudOpenApi();

        // API versioning middleware (deprecation warnings, version negotiation)
        app.UseApiVersioning();

        // Response envelope middleware (wraps API responses in standard format)
        app.UseResponseEnvelope();

        // CORS
        app.UseCors(CorsConfiguration.PolicyName);

        app.UseHttpsRedirection();

        // Rate limiting
        app.UseDotNetCloudRateLimiting();

        app.UseAuthentication();
        app.UseAuthorization();

        // Serve static files (Blazor wwwroot, CSS, JS)
        app.UseStaticFiles();
        app.UseAntiforgery();

        // Map OpenIddict endpoints
        app.MapOpenIddictEndpoints();

        // Map API controllers
        app.MapControllers();

        // Map SignalR hub endpoints
        app.MapDotNetCloudHubs();

        // Configure request localization (culture from cookie / Accept-Language header)
        var localizationOptions = new RequestLocalizationOptions()
            .SetDefaultCulture(SupportedCultures.DefaultCulture)
            .AddSupportedCultures(SupportedCultures.All)
            .AddSupportedUICultures(SupportedCultures.All);
        app.UseRequestLocalization(localizationOptions);

        // Map Blazor components (InteractiveAuto = Server + WebAssembly)
        app.MapRazorComponents<DotNetCloud.UI.Web.Components.App>()
            .AddInteractiveServerRenderMode()
            .AddInteractiveWebAssemblyRenderMode()
            .AddAdditionalAssemblies(typeof(DotNetCloud.UI.Web.Client._Imports).Assembly);
    }
}
