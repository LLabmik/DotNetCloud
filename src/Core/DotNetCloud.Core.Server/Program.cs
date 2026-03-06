using DotNetCloud.Core.Auth.Extensions;
using DotNetCloud.Core.Data.Extensions;
using DotNetCloud.Core.Data.Naming;
using DotNetCloud.Core.Data.Initialization;
using DotNetCloud.Core.Localization;
using DotNetCloud.Core.Server.Configuration;
using DotNetCloud.Core.Server.Extensions;
using DotNetCloud.Core.Server.Initialization;
using DotNetCloud.Core.Server.Middleware;
using DotNetCloud.Core.Server.Services;
using DotNetCloud.Core.ServiceDefaults.Extensions;
using DotNetCloud.Core.ServiceDefaults.HealthChecks;
using DotNetCloud.Core.ServiceDefaults.Telemetry;
using DotNetCloud.Core.Events;
using DotNetCloud.Modules.Chat.Data;
using DotNetCloud.Modules.Files.Data;
using DotNetCloud.Modules.Files.Services;
using DotNetCloud.UI.Web.Client.Services;
using DotNetCloud.UI.Web.Services;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.DependencyInjection;

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
        // Resolve static assets from the deployed server directory even when launched
        // by a service manager with a different working directory.
        var appBasePath = AppContext.BaseDirectory;
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions
        {
            Args = args,
            ContentRootPath = appBasePath,
            WebRootPath = Path.Combine(appBasePath, "wwwroot")
        });

        ConfigureServices(builder);

        var app = builder.Build();

        ConfigurePipeline(app);

        // Initialize database with retry — waits for PostgreSQL to become available
        await InitializeDatabaseAsync(app);

        app.Run();
    }

    /// <summary>
    /// Initializes the database with exponential backoff retry.
    /// Waits for the database to become available (e.g. when PostgreSQL starts after the app),
    /// then runs migrations and seeds default data.
    /// On permanent failure after all retries, the application is stopped with a clear error.
    /// </summary>
    private static async Task InitializeDatabaseAsync(WebApplication app)
    {
        const int maxAttempts = 5;
        var delay = TimeSpan.FromSeconds(2);
        var logger = app.Services.GetRequiredService<ILogger<Program>>();

        for (var attempt = 1; attempt <= maxAttempts; attempt++)
        {
            using var scope = app.Services.CreateScope();
            try
            {
                var dbInitializer = scope.ServiceProvider.GetRequiredService<DbInitializer>();
                await dbInitializer.InitializeAsync();

                var adminSeeder = scope.ServiceProvider.GetRequiredService<AdminSeeder>();
                await adminSeeder.SeedAsync();

                // Ensure module data stores are initialized.
                var filesDbContext = scope.ServiceProvider.GetRequiredService<FilesDbContext>();
                await EnsureModuleTablesCreatedAsync(filesDbContext, "FileNodes", logger);

                var chatDbContext = scope.ServiceProvider.GetRequiredService<ChatDbContext>();
                await EnsureModuleTablesCreatedAsync(chatDbContext, "Channels", logger);

                // Mark the application as ready for traffic now that DB is initialized
                var startupCheck = app.Services.GetService<StartupHealthCheck>();
                startupCheck?.MarkReady();

                return; // Success
            }
            catch (Exception ex) when (attempt < maxAttempts)
            {
                logger.LogWarning(
                    ex,
                    "Database initialization attempt {Attempt}/{MaxAttempts} failed. Retrying in {Delay}s...",
                    attempt, maxAttempts, delay.TotalSeconds);

                await Task.Delay(delay);
                delay *= 2; // Exponential backoff: 2s → 4s → 8s → 16s
            }
            catch (Exception ex)
            {
                logger.LogCritical(
                    ex,
                    "Database initialization failed after {MaxAttempts} attempts. " +
                    "Verify the database is running and the connection string is correct. Shutting down.",
                    maxAttempts);

                // Ensure health checks report unhealthy, then stop the application
                await app.StopAsync();
                throw;
            }
        }
    }

    /// <summary>
    /// Registers all services for the DotNetCloud server.
    /// Separated from <see cref="Main"/> so <c>WebApplicationFactory</c> can override services.
    /// </summary>
    public static void ConfigureServices(WebApplicationBuilder builder)
    {
        // Configure Kestrel (HTTPS/TLS, HTTP/2, listener addresses, limits)
        builder.ConfigureKestrel();

        // Configure and register supervisor + module gRPC infrastructure.
        builder.ConfigureGrpcForModules();
        builder.Services.AddProcessSupervisor();

        // Add service defaults (logging, telemetry, health checks)
        builder.AddDotNetCloudServiceDefaults();

        // Add authentication and authorization
        builder.Services.AddDotNetCloudAuth(builder.Configuration);

        // Persist DataProtection keys so auth/antiforgery tokens survive restarts.
        var dataRootDir = Environment.GetEnvironmentVariable("DOTNETCLOUD_DATA_DIR");
        var dataProtectionKeysPath = !string.IsNullOrWhiteSpace(dataRootDir)
            ? Path.Combine(dataRootDir, "data-protection-keys")
            : Path.Combine(builder.Environment.ContentRootPath, "data-protection-keys");
        Directory.CreateDirectory(dataProtectionKeysPath);
        builder.Services.AddDataProtection()
            .SetApplicationName("DotNetCloud")
            .PersistKeysToFileSystem(new DirectoryInfo(dataProtectionKeysPath));

        // Add database
        var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
            ?? throw new InvalidOperationException("Connection string 'DefaultConnection' not found.");
        builder.Services.AddDotNetCloudDbContext(connectionString);

        // Register in-process module data services for interactive module UI actions,
        // using the same provider as the configured core database.
        var provider = DatabaseProviderDetector.DetectProvider(connectionString);
        builder.Services.AddDbContext<FilesDbContext>(options =>
            ConfigureModuleDbContext(options, provider, connectionString));
        builder.Services.AddDbContext<ChatDbContext>(options =>
            ConfigureModuleDbContext(options, provider, connectionString));
        builder.Services.AddFilesServices(builder.Configuration);
        builder.Services.AddChatServices();
        builder.Services.AddSingleton<IEventBus, InProcessEventBus>();

        var filesStoragePath = builder.Configuration.GetValue<string>("Files:StoragePath");
        if (string.IsNullOrWhiteSpace(filesStoragePath))
        {
            var dataDir = Environment.GetEnvironmentVariable("DOTNETCLOUD_DATA_DIR");
            filesStoragePath = !string.IsNullOrWhiteSpace(dataDir)
                ? Path.Combine(dataDir, "storage")
                : Path.Combine(builder.Environment.ContentRootPath, "storage");
        }
        builder.Services.AddSingleton<IFileStorageEngine>(sp =>
            new LocalFileStorageEngine(filesStoragePath, sp.GetRequiredService<ILogger<LocalFileStorageEngine>>()));

        // Add controllers
        builder.Services.AddControllers();

        // Add localization services for i18n support
        builder.Services.AddLocalization();

        // Add Blazor (InteractiveAuto = Server + WebAssembly)
        builder.Services.AddRazorComponents()
            .AddInteractiveServerComponents()
            .AddInteractiveWebAssemblyComponents()
            .AddAuthenticationStateSerialization();

        builder.Services.AddCascadingAuthenticationState();

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

        // Register initialization services
        builder.Services.AddScoped<AdminSeeder>();
        builder.Services.AddHostedService<ModuleUiRegistrationHostedService>();

        // Configure forwarded headers for reverse proxy support
        builder.Services.Configure<Microsoft.AspNetCore.Builder.ForwardedHeadersOptions>(options =>
        {
            options.ForwardedHeaders =
                Microsoft.AspNetCore.HttpOverrides.ForwardedHeaders.XForwardedFor |
                Microsoft.AspNetCore.HttpOverrides.ForwardedHeaders.XForwardedProto |
                Microsoft.AspNetCore.HttpOverrides.ForwardedHeaders.XForwardedHost;
        });
    }

    private static void ConfigureModuleDbContext(
        DbContextOptionsBuilder options,
        DatabaseProvider provider,
        string connectionString)
    {
        switch (provider)
        {
            case DatabaseProvider.PostgreSQL:
                options.UseNpgsql(connectionString, npgsqlOptions =>
                {
                    npgsqlOptions.EnableRetryOnFailure(maxRetryCount: 3);
                    npgsqlOptions.CommandTimeout(30);
                });
                break;

            case DatabaseProvider.SqlServer:
                options.UseSqlServer(connectionString, sqlServerOptions =>
                {
                    sqlServerOptions.EnableRetryOnFailure(maxRetryCount: 3);
                    sqlServerOptions.CommandTimeout(30);
                });
                break;

            case DatabaseProvider.MariaDB:
                throw new NotSupportedException("MariaDB support is temporarily disabled pending Pomelo .NET 10 update");

            default:
                throw new InvalidOperationException($"Unsupported database provider: {provider}");
        }
    }

    private static async Task EnsureModuleTablesCreatedAsync(
        DbContext context,
        string sentinelTable,
        ILogger logger)
    {
        if (await ModuleTableExistsAsync(context, sentinelTable))
        {
            return;
        }

        var creator = context.Database.GetService<IRelationalDatabaseCreator>();
        await creator.CreateTablesAsync();

        logger.LogInformation(
            "Created module tables for context {ContextType} because sentinel table {SentinelTable} was missing.",
            context.GetType().Name,
            sentinelTable);
    }

    private static async Task<bool> ModuleTableExistsAsync(DbContext context, string tableName)
    {
        var connection = context.Database.GetDbConnection();
        var shouldClose = connection.State != System.Data.ConnectionState.Open;

        if (shouldClose)
        {
            await connection.OpenAsync();
        }

        try
        {
            await using var command = connection.CreateCommand();

            var provider = context.Database.ProviderName ?? string.Empty;
            if (provider.Contains("Npgsql", StringComparison.OrdinalIgnoreCase))
            {
                command.CommandText = "SELECT EXISTS (SELECT 1 FROM information_schema.tables WHERE table_schema = 'public' AND table_name = @tableName);";
            }
            else if (provider.Contains("SqlServer", StringComparison.OrdinalIgnoreCase))
            {
                command.CommandText = "SELECT CASE WHEN EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_SCHEMA = 'dbo' AND TABLE_NAME = @tableName) THEN 1 ELSE 0 END;";
            }
            else
            {
                throw new NotSupportedException($"Unsupported relational provider for module table checks: {provider}");
            }

            var parameter = command.CreateParameter();
            parameter.ParameterName = "@tableName";
            parameter.Value = tableName;
            command.Parameters.Add(parameter);

            var result = await command.ExecuteScalarAsync();
            return result switch
            {
                bool boolResult => boolResult,
                byte byteResult => byteResult != 0,
                short shortResult => shortResult != 0,
                int intResult => intResult != 0,
                long longResult => longResult != 0,
                _ => false
            };
        }
        finally
        {
            if (shouldClose)
            {
                await connection.CloseAsync();
            }
        }
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

        // Serve static files (Blazor wwwroot, CSS, JS, _framework/blazor.web.js)
        app.MapStaticAssets();
        app.UseAntiforgery();

        // Map OpenIddict endpoints
        app.MapOpenIddictEndpoints();

        // Map API controllers
        app.MapControllers();

        // Map SignalR hub endpoints
        app.MapDotNetCloudHubs();

        // Map gRPC services used by process-isolated modules.
        app.MapModuleGrpcServices();

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
