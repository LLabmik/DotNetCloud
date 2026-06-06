using DotNetCloud.Core.Auth.Extensions;
using DotNetCloud.Core.Data.Context;
using DotNetCloud.Core.Data.Services;
using DotNetCloud.Core.Data.Extensions;
using DotNetCloud.Core.Data.Naming;
using DotNetCloud.Core.Data.Initialization;
using DotNetCloud.Core.Localization;
using DotNetCloud.Core.Modules;
using DotNetCloud.Core.Modules.Supervisor;
using DotNetCloud.Core.Security;
using DotNetCloud.Core.Schema.Services;
using DotNetCloud.Core.Server.Configuration;
using DotNetCloud.Core.Server.Extensions;
using DotNetCloud.Core.Server.HealthChecks;
using DotNetCloud.Core.Server.Initialization;
using DotNetCloud.Core.Server.Middleware;
using DotNetCloud.Core.Server.Services;
using DotNetCloud.Core.ServiceDefaults.Extensions;
using DotNetCloud.Core.ServiceDefaults.HealthChecks;
using DotNetCloud.Core.ServiceDefaults.Telemetry;
using DotNetCloud.Core.Events;
using DotNetCloud.Modules.Calendar.Data;
using DotNetCloud.Modules.Chat.Data;
using DotNetCloud.Modules.Contacts.Data;
using DotNetCloud.Modules.Files.Data;
using DotNetCloud.Modules.Files.Data.Services.Background;
using DotNetCloud.Modules.Music.Data;
using DotNetCloud.Modules.Notes.Data;
using DotNetCloud.Modules.Photos.Data;
using DotNetCloud.Modules.Tracks.Data;
using DotNetCloud.Modules.Video.Data;
using DotNetCloud.Modules.Bookmarks.Data;
using DotNetCloud.Modules.Email.Data;
using DotNetCloud.Modules.AI.Data;
using DotNetCloud.Modules.Search;
using DotNetCloud.Modules.Search.Client;
using DotNetCloud.Modules.Files.Services;
using DotNetCloud.UI.Web.Client.Services;
using DotNetCloud.UI.Web.Services;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.ResponseCompression;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting.WindowsServices;
using System.Net.Http;
using System.Net.Security;
using System.Reflection;
using Yarp.ReverseProxy.Forwarder;

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

        // Load CLI config.json from DOTNETCLOUD_CONFIG_DIR as an additional
        // configuration source. This is the single source of truth for the
        // database connection string, shared with the CLI.
        var configDir = Environment.GetEnvironmentVariable("DOTNETCLOUD_CONFIG_DIR");
        if (!string.IsNullOrEmpty(configDir))
        {
            var configJsonPath = Path.Combine(configDir, "config.json");
            if (File.Exists(configJsonPath))
            {
                builder.Configuration.AddJsonFile(configJsonPath, optional: true, reloadOnChange: false);
            }
        }

        ConfigureServices(builder);

        var app = builder.Build();

        ConfigurePipeline(app);

        // Initialize database with retry — waits for PostgreSQL to become available
        await InitializeDatabaseAsync(app);

        // Check if the Let's Encrypt TLS certificate is expiring soon and log a warning.
        // The user should run 'dotnetcloud cert-renew' or set up a systemd timer.
        CheckCertificateExpiry(app);

        app.Run();
    }

    /// <summary>
    /// Checks the configured TLS certificate and logs a warning if it's expiring within 30 days.
    /// Does NOT block startup — the cert was valid enough when the server started.
    /// </summary>
    private static void CheckCertificateExpiry(WebApplication app)
    {
        var logger = app.Services.GetRequiredService<ILogger<Program>>();
        var certPath = app.Configuration["Kestrel:CertificatePath"];

        if (string.IsNullOrEmpty(certPath) || !File.Exists(certPath))
        {
            return;
        }

        try
        {
            using var cert = System.Security.Cryptography.X509Certificates.X509CertificateLoader.LoadCertificate(
                System.IO.File.ReadAllBytes(certPath));
            var daysRemaining = (cert.NotAfter - DateTime.UtcNow).Days;

            if (daysRemaining <= 0)
            {
                logger.LogCritical(
                    "TLS certificate at {CertPath} has expired ({ExpiryDate}). " +
                    "Renew immediately: sudo dotnetcloud cert-renew",
                    certPath, cert.NotAfter.ToString("yyyy-MM-dd"));
            }
            else if (daysRemaining <= 7)
            {
                logger.LogWarning(
                    "TLS certificate at {CertPath} expires in {Days} day(s) ({ExpiryDate}). " +
                    "Renew soon: sudo dotnetcloud cert-renew",
                    certPath, daysRemaining, cert.NotAfter.ToString("yyyy-MM-dd"));
            }
            else if (daysRemaining <= 30)
            {
                logger.LogInformation(
                    "TLS certificate at {CertPath} expires in {Days} day(s) ({ExpiryDate}). " +
                    "Schedule renewal: sudo dotnetcloud cert-renew",
                    certPath, daysRemaining, cert.NotAfter.ToString("yyyy-MM-dd"));
            }
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Could not read TLS certificate at {CertPath}.", certPath);
        }
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

                var oidcClientSeeder = scope.ServiceProvider.GetRequiredService<OidcClientSeeder>();
                await oidcClientSeeder.SeedAsync();

                // Only migrate modules that are installed. Module schemas are created
                // lazily — when a module is first installed, its EF migrations are applied.
                var dbContext = scope.ServiceProvider.GetRequiredService<CoreDbContext>();
                var schemaService = scope.ServiceProvider.GetRequiredService<ModuleSchemaService>();
                var installedModuleIds = await dbContext.InstalledModules
                    .Where(m => m.Status == "Enabled" || m.Status == "Installing")
                    .Select(m => m.ModuleId)
                    .ToListAsync();

                foreach (var moduleId in installedModuleIds)
                {
                    try
                    {
                        await schemaService.EnsureModuleSchemaAsync(moduleId, CancellationToken.None);
                    }
                    catch (Exception ex) when (app.Environment.IsDevelopment() || app.Environment.IsEnvironment("Testing"))
                    {
                        logger.LogWarning(ex,
                            "Skipping schema creation for {ModuleId} in {Environment} environment.",
                            moduleId, app.Environment.EnvironmentName);
                    }
                }

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
        if (OperatingSystem.IsWindows())
        {
            builder.Host.UseWindowsService();
            builder.Services.AddWindowsService(options =>
            {
                options.ServiceName = "DotNetCloud Core Server";
            });
        }

        // Configure Kestrel (HTTPS/TLS, HTTP/2, listener addresses, limits)
        builder.ConfigureKestrel();

        // Configure and register supervisor + module gRPC infrastructure.
        builder.ConfigureGrpcForModules();
        builder.Services.AddProcessSupervisor(options =>
        {
            options.PreferTcpTransport = true;
        });

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

        // Resolve configured database provider (authoritative), then resolve
        // connection string. We support both canonical and legacy provider keys
        // during migration from older config schema.
        var provider = ResolveConfiguredDatabaseProvider(builder.Configuration);

        // The CLI config.json uses the flat key "connectionString"; ASP.NET
        // convention uses "ConnectionStrings:DefaultConnection".
        // Prefer the CLI config so the appsettings.json dev defaults don't
        // override the production config set by dotnetcloud setup.
        var connectionString = builder.Configuration["connectionString"]
            ?? builder.Configuration.GetConnectionString("DefaultConnection");

        if (string.IsNullOrEmpty(connectionString))
        {
            throw new InvalidOperationException(
                "Connection string not found. Set 'ConnectionStrings:DefaultConnection' in appsettings.json " +
                "or 'connectionString' in config.json (DOTNETCLOUD_CONFIG_DIR).");
        }
        builder.Services.AddDotNetCloudDbContext(connectionString, provider);

        // Register naming strategy for all module DbContexts based on configured provider
        builder.Services.AddSingleton<ITableNamingStrategy>(provider == DatabaseProvider.SqlServer
            ? new SqlServerNamingStrategy()
            : new PostgreSqlNamingStrategy());

        // Register in-process module data services for interactive module UI actions,
        // using the same provider as the configured core database.
        builder.Services.AddModuleDbContexts(provider, connectionString);

        // Register schema services for lazy module schema creation.
        // SelfManagedSchemaProvider and ModuleSchemaService are registered by AddDotNetCloudDbContext.
        builder.Services.AddSingleton<IFileValidationService, FileValidationService>();
        builder.Services.AddSingleton<IModuleSchemaProvider, DbContextSchemaProvider>();

        // NOTE: Module business services (AddXxxServices) are NO LONGER registered here.
        // Modules now run as process-isolated gRPC services. The Core.Server communicates
        // with them exclusively via gRPC clients defined in Grpc/Clients/.
        // SearchFtsClient is also handled by the Search module host.
        // builder.Services.AddSearchFtsClient(builder.Configuration); // removed — handled by Search module host
        // ✅ Phase 6: gRPC-based reindex dispatcher — calls Search module's ReindexModule RPC
        builder.Services.AddScoped<IAdminSharedFolderReindexDispatcher, InProcessAdminSharedFolderReindexDispatcher>();
        // Register ISearchableModule implementations for search indexing
        builder.Services.AddScoped<DotNetCloud.Core.Capabilities.ISearchableModule, DotNetCloud.Modules.Files.Data.Services.FilesSearchableModule>();
        builder.Services.AddScoped<DotNetCloud.Core.Capabilities.ISearchableModule, DotNetCloud.Modules.Notes.Data.Services.NotesSearchableModule>();
        builder.Services.AddScoped<DotNetCloud.Core.Capabilities.ISearchableModule, DotNetCloud.Modules.Calendar.Data.Services.CalendarSearchableModule>();
        builder.Services.AddScoped<DotNetCloud.Core.Capabilities.ISearchableModule, DotNetCloud.Modules.Bookmarks.Data.Services.BookmarksSearchableModule>();
        builder.Services.AddScoped<DotNetCloud.Core.Capabilities.ISearchableModule, DotNetCloud.Modules.Email.Data.Services.EmailSearchableModule>();
        builder.Services.AddSingleton<IEventBus, InProcessEventBus>();
        builder.Services.AddSingleton<LegacyFilesMigrationService>();
        builder.Services.AddScoped<DotNetCloud.Core.Capabilities.ICrossModuleLinkResolver, CrossModuleLinkResolver>();
        builder.Services.AddSingleton<DotNetCloud.Core.Services.IBackgroundServiceTracker, DotNetCloud.Core.Services.BackgroundServiceTracker>();

        // Update service — queries GitHub Releases API with caching
        builder.Services.AddMemoryCache();
        builder.Services.AddHttpClient("GitHubReleases", client =>
        {
            client.DefaultRequestHeaders.UserAgent.ParseAdd("DotNetCloud-UpdateChecker/1.0");
        });
        builder.Services.AddSingleton<DotNetCloud.Core.Services.IUpdateService, DotNetCloud.Core.Server.Services.GitHubUpdateService>();
        builder.Services.AddScoped<DotNetCloud.Core.Capabilities.INotificationService, NotificationService>();
        builder.Services.AddScoped<DotNetCloud.Modules.Files.Services.IUserOrganizationResolver, UserOrganizationResolver>();
        builder.Services.AddScoped<DotNetCloud.Core.Import.IImportPipeline, ImportPipelineService>();
        builder.Services.AddScoped<DotNetCloud.Core.Server.Services.MediaFolderImportService>();
        builder.Services.AddScoped<DotNetCloud.Core.Services.IMediaLibraryScanner>(sp =>
            sp.GetRequiredService<DotNetCloud.Core.Server.Services.MediaFolderImportService>());

        var filesStoragePath = builder.Configuration.GetValue<string>("Files:StoragePath");
        var dataDirForStorage = Environment.GetEnvironmentVariable("DOTNETCLOUD_DATA_DIR");
        if (string.IsNullOrWhiteSpace(filesStoragePath))
        {
            filesStoragePath = !string.IsNullOrWhiteSpace(dataDirForStorage)
                ? Path.Combine(dataDirForStorage, "storage")
                : Path.Combine(builder.Environment.ContentRootPath, "storage");
        }

        // Propagate the resolved storage path so all services reading
        // "Files:Storage:RootPath" use the persistent location instead of
        // falling back to Path.GetTempPath() (which is ephemeral under
        // systemd PrivateTmp=true).
        builder.Configuration["Files:Storage:RootPath"] = filesStoragePath;

        // Create the server-owned temp directory with restricted permissions (700).
        var tmpDir = !string.IsNullOrWhiteSpace(dataDirForStorage)
            ? Path.Combine(dataDirForStorage, "tmp")
            : Path.Combine(builder.Environment.ContentRootPath, "tmp");
        Directory.CreateDirectory(tmpDir);
        if (OperatingSystem.IsLinux() || OperatingSystem.IsMacOS())
        {
            File.SetUnixFileMode(tmpDir,
                UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
        }

        builder.Services.PostConfigure<DotNetCloud.Modules.Files.Options.FileUploadOptions>(o => o.TmpPath = tmpDir);

        builder.Services.AddSingleton<IFileStorageEngine>(sp =>
            new LocalFileStorageEngine(filesStoragePath, sp.GetRequiredService<ILogger<LocalFileStorageEngine>>()));

        // Add controllers
        builder.Services.AddControllers(options =>
        {
            options.Filters.Add<DotNetCloud.Modules.Files.Filters.DeviceIdentityFilter>();
        });

        // Add reverse proxy forwarding for Collabora paths (single-origin deployment on core HTTPS port).
        builder.Services.AddHttpForwarder();

        // Add localization services for i18n support
        builder.Services.AddLocalization();

        // Add Blazor (InteractiveAuto = Server + WebAssembly)
        builder.Services.AddRazorComponents()
            .AddInteractiveServerComponents(options =>
            {
                options.DetailedErrors = true;
            })
            .AddInteractiveWebAssemblyComponents()
            .AddAuthenticationStateSerialization();

        builder.Services.AddCascadingAuthenticationState();

        // Blazor UI services (server-side prerendering needs these too)
        builder.Services.AddHttpContextAccessor();
        builder.Services.AddSingleton<ModuleUiRegistry>();
        builder.Services.AddScoped<DotNetCloud.UI.Shared.Services.BrowserTimeProvider>();
        builder.Services.AddScoped<ToastService>();
        builder.Services.AddScoped<DotNetCloud.Core.Server.Middleware.CookieCaptureStore>();
        builder.Services.AddScoped<Microsoft.AspNetCore.Components.Server.Circuits.CircuitHandler, DotNetCloud.Core.Server.Middleware.CookieCaptureCircuitHandler>();
        builder.Services.AddTransient<DotNetCloud.Core.Server.Middleware.CookieForwardingHandler>();

        // Scoped HttpClient with BaseAddress for Blazor components. During SSR,
        // NavigationManager.BaseUri may not be available; fall back to the
        // configured Kestrel HTTP port.
        var httpPort = builder.Configuration.GetValue("Kestrel:HttpPort", 5080);
        builder.Services.AddScoped(sp =>
        {
            Uri baseUri;
            try
            {
                var nav = sp.GetRequiredService<NavigationManager>();
                baseUri = new Uri(nav.BaseUri);
            }
            catch
            {
                baseUri = new Uri($"http://localhost:{httpPort}");
            }

            var cookieHandler = sp.GetRequiredService<DotNetCloud.Core.Server.Middleware.CookieForwardingHandler>();
            var configuration = sp.GetRequiredService<IConfiguration>();
            var allowInsecureTls = configuration.GetValue<bool>("Files:Collabora:AllowInsecureTls");

            if (baseUri.Scheme == Uri.UriSchemeHttps &&
                (baseUri.IsLoopback || allowInsecureTls || IsPrivateOrLocalHost(baseUri.Host)))
            {
                cookieHandler.InnerHandler = new HttpClientHandler
                {
                    ServerCertificateCustomValidationCallback = HttpClientHandler.DangerousAcceptAnyServerCertificateValidator
                };
            }
            else
            {
                cookieHandler.InnerHandler = new HttpClientHandler();
            }

            return new HttpClient(cookieHandler) { BaseAddress = baseUri };
        });
        // gRPC client options for all process-isolated modules
        builder.Services.Configure<DotNetCloud.Core.Server.Grpc.Clients.ContactsGrpcClientOptions>(
            builder.Configuration.GetSection(DotNetCloud.Core.Server.Grpc.Clients.ContactsGrpcClientOptions.SectionName));
        builder.Services.Configure<DotNetCloud.Core.Server.Grpc.Clients.CalendarGrpcClientOptions>(
            builder.Configuration.GetSection(DotNetCloud.Core.Server.Grpc.Clients.CalendarGrpcClientOptions.SectionName));
        builder.Services.Configure<DotNetCloud.Core.Server.Grpc.Clients.ChatGrpcClientOptions>(
            builder.Configuration.GetSection(DotNetCloud.Core.Server.Grpc.Clients.ChatGrpcClientOptions.SectionName));
        builder.Services.Configure<DotNetCloud.Core.Server.Grpc.Clients.FilesGrpcClientOptions>(
            builder.Configuration.GetSection(DotNetCloud.Core.Server.Grpc.Clients.FilesGrpcClientOptions.SectionName));
        builder.Services.Configure<DotNetCloud.Core.Server.Grpc.Clients.NotesGrpcClientOptions>(
            builder.Configuration.GetSection(DotNetCloud.Core.Server.Grpc.Clients.NotesGrpcClientOptions.SectionName));
        builder.Services.Configure<DotNetCloud.Core.Server.Grpc.Clients.TracksGrpcClientOptions>(
            builder.Configuration.GetSection(DotNetCloud.Core.Server.Grpc.Clients.TracksGrpcClientOptions.SectionName));
        builder.Services.Configure<DotNetCloud.Core.Server.Grpc.Clients.MusicGrpcClientOptions>(
            builder.Configuration.GetSection(DotNetCloud.Core.Server.Grpc.Clients.MusicGrpcClientOptions.SectionName));
        builder.Services.Configure<DotNetCloud.Core.Server.Grpc.Clients.PhotosGrpcClientOptions>(
            builder.Configuration.GetSection(DotNetCloud.Core.Server.Grpc.Clients.PhotosGrpcClientOptions.SectionName));
        builder.Services.Configure<DotNetCloud.Core.Server.Grpc.Clients.VideoGrpcClientOptions>(
            builder.Configuration.GetSection(DotNetCloud.Core.Server.Grpc.Clients.VideoGrpcClientOptions.SectionName));
        builder.Services.Configure<DotNetCloud.Core.Server.Grpc.Clients.SearchGrpcClientOptions>(
            builder.Configuration.GetSection(DotNetCloud.Core.Server.Grpc.Clients.SearchGrpcClientOptions.SectionName));
        builder.Services.Configure<DotNetCloud.Core.Server.Grpc.Clients.BookmarksGrpcClientOptions>(
            builder.Configuration.GetSection(DotNetCloud.Core.Server.Grpc.Clients.BookmarksGrpcClientOptions.SectionName));
        builder.Services.Configure<DotNetCloud.Core.Server.Grpc.Clients.EmailGrpcClientOptions>(
            builder.Configuration.GetSection(DotNetCloud.Core.Server.Grpc.Clients.EmailGrpcClientOptions.SectionName));
        builder.Services.Configure<DotNetCloud.Core.Server.Grpc.Clients.AboutGrpcClientOptions>(
            builder.Configuration.GetSection(DotNetCloud.Core.Server.Grpc.Clients.AboutGrpcClientOptions.SectionName));
        builder.Services.Configure<DotNetCloud.Core.Server.Grpc.Clients.AiGrpcClientOptions>(
            builder.Configuration.GetSection(DotNetCloud.Core.Server.Grpc.Clients.AiGrpcClientOptions.SectionName));
        builder.Services.AddSingleton<DotNetCloud.Core.Server.Grpc.Clients.ModuleEndpointProvider>();

        // gRPC API client registrations (process-isolated modules)
        // gRPC client registrations (process-isolated modules)
        // Module UI services (Blazor components render in Core.Server process;
        // these register only the interfaces components inject — no hosted services).
        builder.Services.AddNotesUiServices(builder.Configuration);
        builder.Services.AddTracksUiServices(builder.Configuration);
        builder.Services.AddMusicUiServices(builder.Configuration);
        builder.Services.AddPhotosUiServices(builder.Configuration);
        builder.Services.AddVideoUiServices(builder.Configuration);
        builder.Services.AddFilesUiServices(builder.Configuration);
        builder.Services.AddAiUiServices(builder.Configuration);

        // gRPC API client registrations (process-isolated modules)
        // ✅ Fully implemented gRPC clients
        builder.Services.AddScoped<DotNetCloud.Modules.Contacts.Services.IContactsApiClient, DotNetCloud.Core.Server.Grpc.Clients.ContactsGrpcApiClient>();
        builder.Services.AddScoped<DotNetCloud.Modules.Calendar.Services.ICalendarApiClient, DotNetCloud.Core.Server.Grpc.Clients.CalendarGrpcApiClient>();
        builder.Services.AddScoped<DotNetCloud.Modules.Chat.Services.IChatApiClient, DotNetCloud.Core.Server.Grpc.Clients.ChatGrpcApiClient>();
        builder.Services.AddScoped<DotNetCloud.Modules.Files.Services.IFilesApiClient, DotNetCloud.Core.Server.Grpc.Clients.FilesGrpcApiClient>();
        builder.Services.AddScoped<DotNetCloud.Modules.Music.Services.IMusicApiClient, DotNetCloud.Core.Server.Grpc.Clients.MusicGrpcApiClient>();
        builder.Services.AddScoped<DotNetCloud.Modules.Photos.Services.IPhotosApiClient, DotNetCloud.Core.Server.Grpc.Clients.PhotosGrpcApiClient>();
        builder.Services.AddScoped<DotNetCloud.Modules.Video.Services.IVideoApiClient, DotNetCloud.Core.Server.Grpc.Clients.VideoGrpcApiClient>();
        builder.Services.AddScoped<DotNetCloud.Modules.Search.Services.ISearchApiClient, DotNetCloud.Core.Server.Grpc.Clients.SearchGrpcApiClient>();
        builder.Services.AddScoped<DotNetCloud.Modules.About.Services.IAboutApiClient, DotNetCloud.Core.Server.Grpc.Clients.AboutGrpcApiClient>();
        builder.Services.AddScoped<DotNetCloud.Modules.AI.Services.IAiApiClient, DotNetCloud.Core.Server.Grpc.Clients.AiGrpcApiClient>();
        // ✅ gRPC clients (newly implemented)
        builder.Services.AddScoped<DotNetCloud.Modules.Notes.Services.INotesApiClient, DotNetCloud.Core.Server.Grpc.Clients.NotesGrpcApiClient>();
        builder.Services.AddScoped<DotNetCloud.Modules.Bookmarks.Services.IBookmarksApiClient, DotNetCloud.Core.Server.Grpc.Clients.BookmarksGrpcApiClient>();
        // ⚠️ Legacy in-process HTTP clients (TODO: gRPC proto expansion needed — see GRPC_MODULE_CONVERSION_PLAN.md)
        builder.Services.AddScoped<DotNetCloud.Modules.Tracks.Services.ITracksApiClient, DotNetCloud.Core.Server.Grpc.Clients.TracksGrpcApiClient>();
        builder.Services.AddScoped<DotNetCloud.Modules.Email.Services.IEmailApiClient, DotNetCloud.Core.Server.Grpc.Clients.EmailGrpcApiClient>();
        builder.Services.AddScoped<DotNetCloud.Modules.Tracks.Services.IOnboardingStateService, DotNetCloud.Modules.Tracks.Services.OnboardingStateService>();

        // Typed HttpClient for server prerendering of client components (NotificationBell, etc.).
        // During static SSR, HttpClient from the .Client project has no BaseAddress.
        // Uses HTTPS loopback with cert-validation bypass (cert is for cloud.dotnetcloud.net, not localhost).
        var httpsPort = builder.Configuration.GetValue("httpsPort", 5443);
        builder.Services.AddHttpClient<DotNetCloudApiClient>(client =>
            client.BaseAddress = new Uri($"https://localhost:{httpsPort}"))
            .ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler
            {
                ServerCertificateCustomValidationCallback = HttpClientHandler.DangerousAcceptAnyServerCertificateValidator
            });

        // Add OpenAPI/Swagger with DotNetCloud configuration
        builder.Services.AddDotNetCloudOpenApi(builder.Configuration);

        // Add API versioning
        builder.Services.AddDotNetCloudApiVersioning(builder.Configuration);

        // Add CORS with enhanced configuration
        builder.Services.AddDotNetCloudCors(builder.Configuration);

        // Add response compression (Brotli preferred, Gzip fallback; applies to chunk/file downloads)
        builder.Services.AddResponseCompression(options =>
        {
            options.EnableForHttps = true;
            options.Providers.Add<BrotliCompressionProvider>();
            options.Providers.Add<GzipCompressionProvider>();
            // Include octet-stream so raw chunk downloads are eligible for compression.
            // Already-compressed formats (JPEG, ZIP, etc.) use their own MIME types
            // (image/jpeg, application/zip) which are not in this list, so they are skipped.
            options.MimeTypes = ResponseCompressionDefaults.MimeTypes.Concat(
                ["application/octet-stream"]);
        });
        builder.Services.Configure<BrotliCompressionProviderOptions>(options =>
            options.Level = System.IO.Compression.CompressionLevel.Fastest);
        builder.Services.Configure<GzipCompressionProviderOptions>(options =>
            options.Level = System.IO.Compression.CompressionLevel.Fastest);

        // Add request decompression (handles Content-Encoding: gzip/br/deflate on incoming requests).
        // Required because desktop/mobile clients gzip-compress chunk upload bodies.
        builder.Services.AddRequestDecompression();

        // Add rate limiting
        builder.Services.AddDotNetCloudRateLimiting(builder.Configuration);

        // Linux resource health check (inotify watch limit + inode availability).
        // Runs silently on non-Linux platforms.
        var linuxDataDir = Environment.GetEnvironmentVariable("DOTNETCLOUD_DATA_DIR")
            ?? builder.Environment.ContentRootPath;
        builder.Services.AddHealthChecks()
            .Add(new Microsoft.Extensions.Diagnostics.HealthChecks.HealthCheckRegistration(
                "linux-resources",
                sp => new LinuxResourceHealthCheck(
                    linuxDataDir,
                    sp.GetRequiredService<ILogger<LinuxResourceHealthCheck>>()),
                failureStatus: null,
                tags: ["ready"]))
            .Add(new Microsoft.Extensions.Diagnostics.HealthChecks.HealthCheckRegistration(
                "modules-aggregate",
                sp => new ModulesAggregateHealthCheck(
                    sp.GetRequiredService<IProcessSupervisor>(),
                    sp.GetRequiredService<Supervisor.GrpcChannelManager>(),
                    sp.GetRequiredService<ILogger<ModulesAggregateHealthCheck>>()),
                failureStatus: Microsoft.Extensions.Diagnostics.HealthChecks.HealthStatus.Unhealthy,
                tags: ["module"]));
        builder.Services.AddSingleton(sp =>
            new LinuxResourceMonitorService(
                linuxDataDir,
                sp.GetRequiredService<ILogger<LinuxResourceMonitorService>>()));
        builder.Services.AddHostedService(sp => sp.GetRequiredService<LinuxResourceMonitorService>());

        // Add SignalR real-time communication
        builder.Services.AddDotNetCloudSignalR(builder.Configuration);

        // Register initialization services
        builder.Services.AddScoped<AdminSeeder>();
        builder.Services.AddScoped<OidcClientSeeder>();

        // Push notification service (no-op in Core.Server — handled by Chat module gRPC)
        // NOTE: Registered AFTER AddChatServices below to override its IPushNotificationService.
        // (We don't call AddChatServices due to hosted service conflicts; see ChatHub DI section.)

        // Chat services required by ChatHub (SignalR hub in Core.Server).
        // ChatHub depends on Chat module services that are internal; we register them
        // by resolving from ChatServiceRegistration's internal service collection.
        var chatServices = new ServiceCollection();
        DotNetCloud.Modules.Chat.Data.ChatServiceRegistration.AddChatServices(chatServices, builder.Configuration);
        foreach (var descriptor in chatServices)
        {
            // Skip hosted services and transport-level registrations that conflict with Core.Server
            if (descriptor.ServiceType == typeof(Microsoft.Extensions.Hosting.IHostedService))
                continue;
            if (descriptor.ServiceType.FullName?.Contains("Transport") == true)
                continue;
            if (descriptor.ServiceType.FullName?.Contains("Fcm") == true)
                continue;
            if (descriptor.ServiceType.FullName?.Contains("UnifiedPush") == true)
                continue;
            if (descriptor.ServiceType.FullName?.Contains("StunServer") == true)
                continue;
            if (descriptor.ServiceType == typeof(DotNetCloud.Modules.Chat.Services.IPushNotificationService))
                continue; // our NoOpPushNotificationService takes priority
            builder.Services.Add(descriptor);
        }

        builder.Services.AddSingleton<DotNetCloud.Modules.Chat.Services.IPushNotificationService,
            DotNetCloud.Core.Server.Services.NoOpPushNotificationService>();

        builder.Services.AddHostedService<ModuleUiRegistrationHostedService>();
        builder.Services.AddHostedService<NotificationEventSubscriber>();
        builder.Services.AddHostedService<SearchEventSubscriber>();

        // Register backup services
        builder.Services.AddDotNetCloudBackupServices();

        // Register demo account cleanup service
        builder.Services.AddHostedService<DemoAccountCleanupService>();

        // Configure forwarded headers for reverse proxy support.
        // SECURITY: Only trust forwarded headers from known proxies to prevent IP spoofing.
        // By default, ASP.NET Core only trusts loopback (127.0.0.1, ::1).
        // Add your reverse proxy IPs to KnownProxies in production.
        builder.Services.Configure<Microsoft.AspNetCore.Builder.ForwardedHeadersOptions>(options =>
        {
            options.ForwardedHeaders =
                Microsoft.AspNetCore.HttpOverrides.ForwardedHeaders.XForwardedFor |
                Microsoft.AspNetCore.HttpOverrides.ForwardedHeaders.XForwardedProto;
            // XForwardedHost is excluded — allowing attackers to set the Host header
            // can lead to host header injection attacks (password reset link poisoning, etc.).
            // Only enable it if your reverse proxy explicitly sets X-Forwarded-Host.

            // Limit the number of proxy hops to prevent header injection chains.
            options.ForwardLimit = 2;
        });
    }

    private static DatabaseProvider ResolveConfiguredDatabaseProvider(IConfiguration configuration)
    {
        var configuredProvider = configuration["Database:Provider"] ?? configuration["databaseProvider"];

        if (string.IsNullOrWhiteSpace(configuredProvider))
        {
            throw new InvalidOperationException(
                "Database provider not configured. Set 'Database:Provider' (recommended) or legacy 'databaseProvider' " +
                "in config.json, or set environment variable 'Database__Provider'.");
        }

        if (!DatabaseProviderConfiguration.TryParseConfiguredProvider(configuredProvider, out var provider))
        {
            throw new InvalidOperationException(
                $"Invalid database provider '{configuredProvider}'. Supported values: PostgreSQL, SqlServer.");
        }

        return provider;
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

            default:
                throw new InvalidOperationException($"Unsupported database provider: {provider}");
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

        // Response compression — must be before any middleware that writes response bodies.
        // Client advertises support via Accept-Encoding: br, gzip.
        app.UseResponseCompression();

        // Request decompression — unwraps Content-Encoding (gzip, br, deflate) on incoming
        // request bodies so controllers receive uncompressed data. Must be before any
        // middleware that reads Request.Body (e.g. chunk upload hash validation).
        app.UseRequestDecompression();

        // Apply middleware (security headers, exception handler, request logging)
        app.UseDotNetCloudMiddleware(headers =>
        {
            var collaboraUrl = app.Configuration["Files:Collabora:ServerUrl"];
            if (string.IsNullOrWhiteSpace(collaboraUrl) ||
                !Uri.TryCreate(collaboraUrl, UriKind.Absolute, out var collaboraUri))
            {
                return;
            }

            var collaboraOrigin = collaboraUri.GetLeftPart(UriPartial.Authority);
            headers.ContentSecurityPolicy =
                $"default-src 'self'; script-src 'self' 'unsafe-inline' 'unsafe-eval' 'wasm-unsafe-eval'; style-src 'self' 'unsafe-inline'; img-src 'self' data: https:; font-src 'self' data:; connect-src 'self' ws: wss:; frame-src 'self' {collaboraOrigin}; child-src 'self' {collaboraOrigin}; frame-ancestors 'self';";
        });

        // Map health checks
        app.MapDotNetCloudHealthChecks();

        // Map Prometheus metrics scraping endpoint (/metrics) when enabled
        app.MapDotNetCloudPrometheus();

        // OpenAPI/Swagger UI (development only)
        app.UseDotNetCloudOpenApi();

        // API versioning middleware (deprecation warnings, version negotiation)
        app.UseApiVersioning();

        // Response envelope middleware (wraps API responses in standard format).
        // WOPI file protocol endpoints must remain unwrapped for Collabora compatibility.
        // Video/music stream endpoints return raw binary data that must not be buffered
        // into a MemoryStream (which overflows at 2 GB for large files).
        // /api/v1/files/ serves raw file content (audio, video, images, etc.) and must
        // also be excluded to avoid MemoryStream overflow on large video files.
        app.UseResponseEnvelope(options =>
        {
            options.ExcludePaths =
            [
                .. options.ExcludePaths,
                "/api/v1/wopi/files/",
                "/api/v1/videos/",
                "/api/v1/music/",
                "/api/v1/files/",
            ];
        });

        // CORS
        app.UseCors(CorsConfiguration.PolicyName);

        app.UseHttpsRedirection();

        // Capture the auth cookie from the initial HTTP request into a scoped store
        // so Blazor Server components can forward it to module API calls later.
        app.UseMiddleware<DotNetCloud.Core.Server.Middleware.CookieCaptureMiddleware>();

        app.UseAuthentication();
        app.UseAuthorization();

        // Password change enforcement — redirects authenticated users with PasswordChangeRequired=true
        // to the change-password page. Must run after authentication/authorization so the user
        // identity is available, but before endpoint routing so the redirect happens early.
        app.UseMiddleware<PasswordChangeRequiredMiddleware>();

        // Rate limiting — MUST come after UseAuthentication so the GlobalLimiter
        // can distinguish authenticated (200 req/60s per user) from anonymous
        // (20 req/60s per IP) requests.
        app.UseDotNetCloudRateLimiting();

        // Serve static files (Blazor wwwroot, CSS, JS, _framework/blazor.web.js)
        app.MapStaticAssets();
        app.UseAntiforgery();

        // Map OpenIddict endpoints
        app.MapOpenIddictEndpoints();

        // Proxy Collabora through the main DotNetCloud origin so clients only need one public port.
        MapCollaboraReverseProxy(app);

        // Proxy module REST API calls (music, video, notes, wopi) to running module hosts.
        MapModuleApiProxies(app);

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
            .AddAdditionalAssemblies(
                typeof(DotNetCloud.UI.Web.Client._Imports).Assembly,
                typeof(DotNetCloud.Modules.Video.UI.VideoPage).Assembly,
                typeof(DotNetCloud.Modules.Photos.UI.PhotosPage).Assembly,
                typeof(DotNetCloud.Modules.Music.UI.MusicPage).Assembly,
                typeof(DotNetCloud.Modules.Chat.UI.ChatPageLayout).Assembly,
                typeof(DotNetCloud.Modules.Notes.UI.NotesPage).Assembly,
                typeof(DotNetCloud.Modules.Calendar.UI.CalendarPage).Assembly,
                typeof(DotNetCloud.Modules.Contacts.UI.ContactsPage).Assembly,
                typeof(DotNetCloud.Modules.Tracks.UI.TracksPage).Assembly,
                typeof(DotNetCloud.Modules.Files.UI.FileBrowser).Assembly,
                typeof(DotNetCloud.Modules.Bookmarks.UI.BookmarksPage).Assembly,
                typeof(DotNetCloud.Modules.Email.UI.EmailPage).Assembly,
                typeof(DotNetCloud.Modules.About.UI.AboutPage).Assembly);
    }

    private static void MapCollaboraReverseProxy(WebApplication app)
    {
        var logger = app.Services.GetRequiredService<ILoggerFactory>().CreateLogger("CollaboraProxy");
        var collaboraEnabled = app.Configuration.GetValue<bool>("Files:Collabora:Enabled");
        var collaboraUrl = app.Configuration["Files:Collabora:ServerUrl"];
        if (string.IsNullOrWhiteSpace(collaboraUrl) ||
            !Uri.TryCreate(collaboraUrl, UriKind.Absolute, out var collaboraUri))
        {
            if (collaboraEnabled)
            {
                logger.LogWarning(
                    "Collabora is enabled but Files:Collabora:ServerUrl is missing or invalid. " +
                    "Single-origin proxy routes (/hosting, /browser, /cool, /lool) will not be mapped.");
            }

            return;
        }

        // Optional explicit upstream to avoid self-proxy loops when ServerUrl is the public
        // single-origin endpoint (for example https://mint22:15443).
        var proxyUpstreamUrl = app.Configuration["Files:Collabora:ProxyUpstreamUrl"];
        var wopiBaseUrl = app.Configuration["Files:Collabora:WopiBaseUrl"];
        if (string.IsNullOrWhiteSpace(proxyUpstreamUrl) &&
            Uri.TryCreate(wopiBaseUrl, UriKind.Absolute, out var wopiBaseUri) &&
            string.Equals(collaboraUri.GetLeftPart(UriPartial.Authority), wopiBaseUri.GetLeftPart(UriPartial.Authority), StringComparison.OrdinalIgnoreCase))
        {
            logger.LogWarning(
                "Files:Collabora:ServerUrl and Files:Collabora:WopiBaseUrl share the same origin ({Origin}) " +
                "but Files:Collabora:ProxyUpstreamUrl is not set. This commonly causes self-proxy loops/timeouts. " +
                "Set ProxyUpstreamUrl to the internal Collabora endpoint (for example https://localhost:9980).",
                collaboraUri.GetLeftPart(UriPartial.Authority));
        }

        var destinationUri = collaboraUri;
        if (!string.IsNullOrWhiteSpace(proxyUpstreamUrl) &&
            Uri.TryCreate(proxyUpstreamUrl, UriKind.Absolute, out var parsedUpstreamUri))
        {
            destinationUri = parsedUpstreamUri;
        }

        var forwarder = app.Services.GetRequiredService<IHttpForwarder>();
        var allowInsecureTls = app.Configuration.GetValue<bool>("Files:Collabora:AllowInsecureTls");

        var handler = new SocketsHttpHandler
        {
            UseProxy = false,
            AllowAutoRedirect = false,
            UseCookies = false,
            AutomaticDecompression = System.Net.DecompressionMethods.None,
            EnableMultipleHttp2Connections = true,
            SslOptions = new SslClientAuthenticationOptions
            {
                RemoteCertificateValidationCallback = allowInsecureTls
                    ? static (_, _, _, _) => true
                    : null,
            }
        };

        var httpClient = new HttpMessageInvoker(handler);
        app.Lifetime.ApplicationStopping.Register(httpClient.Dispose);

        var destinationPrefix = destinationUri.GetLeftPart(UriPartial.Authority);
        var requestConfig = new ForwarderRequestConfig
        {
            ActivityTimeout = TimeSpan.FromMinutes(15)
        };
        var transformer = new CollaboraProxyTransformer();

        void MapCollaboraPath(string pattern)
        {
            app.Map(pattern, async httpContext =>
            {
                // Collabora responses are rendered inside an iframe in /apps/files.
                // Normalize frame-related headers just before response starts.
                httpContext.Response.OnStarting(() =>
                {
                    NormalizeCollaboraFrameHeaders(httpContext.Response.Headers);
                    return Task.CompletedTask;
                });

                var error = await forwarder.SendAsync(
                    httpContext,
                    destinationPrefix,
                    httpClient,
                    requestConfig,
                    transformer);

                if (error == ForwarderError.None)
                    return;

                var errorFeature = httpContext.GetForwarderErrorFeature();
                logger.LogWarning(
                    errorFeature?.Exception,
                    "Collabora proxy failure for {Path}: {Error}",
                    httpContext.Request.Path,
                    error);

                if (!httpContext.Response.HasStarted)
                {
                    httpContext.Response.StatusCode = StatusCodes.Status502BadGateway;
                }
            });
        }

        // Collabora URL space required for discovery, static assets, and websocket editing session traffic.
        MapCollaboraPath("/hosting/{**catch-all}");
        MapCollaboraPath("/browser/{**catch-all}");
        MapCollaboraPath("/cool/{**catch-all}");
        MapCollaboraPath("/lool/{**catch-all}");

        logger.LogInformation(
            "Collabora reverse proxy enabled: {Destination} for /hosting, /browser, /cool, /lool",
            destinationPrefix);
    }

    /// <summary>
    /// Proxies module REST API paths (e.g. /api/v1/music/*) to the running module host process
    /// via YARP. Module endpoints are resolved from <see cref="IProcessSupervisor"/>.
    /// </summary>
    private static void MapModuleApiProxies(WebApplication app)
    {
        var supervisor = app.Services.GetRequiredService<IProcessSupervisor>();
        var logger = app.Services.GetRequiredService<ILoggerFactory>().CreateLogger("ModuleApiProxy");
        var forwarder = app.Services.GetRequiredService<IHttpForwarder>();

        // Module API prefix → supervisor module ID
        var moduleMappings = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["api/v1/files"] = "dotnetcloud.files",
            ["api/v1/music"] = "dotnetcloud.music",
            ["api/v1/videos"] = "dotnetcloud.video",
            ["api/v1/series"] = "dotnetcloud.video",
            ["api/v1/notes"] = "dotnetcloud.notes",
            ["api/v1/wopi"] = "dotnetcloud.files",
        };

        var handler = new SocketsHttpHandler
        {
            UseProxy = false,
            AllowAutoRedirect = false,
            UseCookies = false,
            EnableMultipleHttp2Connections = true,
        };
        // Module hosts are configured for HTTP/2 (gRPC). Force HTTP/2 for REST proxy.
        handler.SslOptions.RemoteCertificateValidationCallback = static (_, _, _, _) => true;
        var httpClient = new HttpMessageInvoker(handler);
        app.Lifetime.ApplicationStopping.Register(httpClient.Dispose);

        foreach (var (prefix, moduleId) in moduleMappings)
        {
            var pattern = $"/{prefix}/{{**catch-all}}";
            var capturedPrefix = prefix;

            app.Map(pattern, async httpContext =>
            {
                var moduleInfo = supervisor.GetModuleInfo(moduleId);
                if (moduleInfo?.GrpcEndpoint is null || moduleInfo.Status != ModuleProcessStatus.Running)
                {
                    if (!httpContext.Response.HasStarted)
                        httpContext.Response.StatusCode = StatusCodes.Status503ServiceUnavailable;
                    return;
                }

                // GrpcEndpoint is like "http://localhost:50105" — use it directly as destination.
                var destinationPrefix = moduleInfo.GrpcEndpoint;
                if (!destinationPrefix.EndsWith('/'))
                    destinationPrefix += "/";

                var error = await forwarder.SendAsync(
                    httpContext,
                    destinationPrefix,
                    httpClient,
                    new ForwarderRequestConfig
                    {
                        ActivityTimeout = TimeSpan.FromMinutes(5),
                        Version = System.Net.HttpVersion.Version20,
                        VersionPolicy = System.Net.Http.HttpVersionPolicy.RequestVersionOrHigher,
                    },
                    ModuleApiProxyTransformer.Instance);

                if (error != ForwarderError.None && !httpContext.Response.HasStarted)
                {
                    logger.LogWarning(
                        "Module API proxy failure for {Path} → {Module} ({Destination}): {Error}",
                        httpContext.Request.Path, moduleId, destinationPrefix, error);
                    httpContext.Response.StatusCode = StatusCodes.Status502BadGateway;
                }
            });
        }

        logger.LogInformation("Module API proxies mapped for {Count} prefixes: {Prefixes}",
            moduleMappings.Count, string.Join(", ", moduleMappings.Keys));
    }

    /// <summary>
    /// Copies request headers (including auth cookies) and sets X-Forwarded-Proto: https
    /// so module hosts can authenticate the original user.
    /// </summary>
    private sealed class ModuleApiProxyTransformer : HttpTransformer
    {
        public static readonly ModuleApiProxyTransformer Instance = new();

        public override async ValueTask TransformRequestAsync(
            HttpContext httpContext,
            HttpRequestMessage proxyRequest,
            string destinationPrefix,
            CancellationToken cancellationToken)
        {
            await base.TransformRequestAsync(httpContext, proxyRequest, destinationPrefix, cancellationToken);

            // Copy all request headers so the module host receives auth cookies, etc.
            var hasCookie = false;
            foreach (var header in httpContext.Request.Headers)
            {
                if (string.Equals(header.Key, "Host", StringComparison.OrdinalIgnoreCase))
                    continue;
                if (string.Equals(header.Key, "Cookie", StringComparison.OrdinalIgnoreCase))
                    hasCookie = true;
                proxyRequest.Headers.TryAddWithoutValidation(header.Key, header.Value.ToArray());
            }

            proxyRequest.Headers.TryAddWithoutValidation("X-Forwarded-Proto", "https");
            Console.Error.WriteLine($"[YARP-PROXY] Forwarding {httpContext.Request.Path} → {destinationPrefix}. HasCookie={hasCookie}, HeaderCount={httpContext.Request.Headers.Count}");
            Console.Error.Flush();
        }
    }

    private static void NormalizeCollaboraFrameHeaders(IHeaderDictionary headers)
    {
        headers.Remove("X-Frame-Options");

        if (!headers.TryGetValue("Content-Security-Policy", out var cspValues) || cspValues.Count == 0)
        {
            headers["Content-Security-Policy"] = "frame-ancestors 'self';";
            return;
        }

        // Keep one effective CSP for proxied Collabora responses. Multiple CSP headers are
        // combined by browsers and can over-restrict editor bootstrap resources.
        var selectedPolicy = cspValues
            .Where(static policy => !string.IsNullOrWhiteSpace(policy))
            .OrderByDescending(static policy => policy?.Length ?? 0)
            .FirstOrDefault();

        if (string.IsNullOrWhiteSpace(selectedPolicy))
        {
            headers["Content-Security-Policy"] = "frame-ancestors 'self';";
            return;
        }

        var segments = selectedPolicy
            .Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(segment => !segment.StartsWith("frame-ancestors", StringComparison.OrdinalIgnoreCase))
            .ToList();

        segments.Add("frame-ancestors 'self'");
        headers["Content-Security-Policy"] = string.Join("; ", segments) + ";";
    }

    private sealed class CollaboraProxyTransformer : HttpTransformer
    {
        public override async ValueTask TransformRequestAsync(
            HttpContext httpContext,
            HttpRequestMessage proxyRequest,
            string destinationPrefix,
            CancellationToken cancellationToken)
        {
            await base.TransformRequestAsync(httpContext, proxyRequest, destinationPrefix, cancellationToken);

            // Preserve the public origin so Collabora emits websocket/embed metadata
            // using the DotNetCloud endpoint instead of localhost upstream values.
            proxyRequest.Headers.Host = httpContext.Request.Host.Value;

            proxyRequest.Headers.Remove("X-Forwarded-Host");
            proxyRequest.Headers.TryAddWithoutValidation("X-Forwarded-Host", httpContext.Request.Host.Value);

            proxyRequest.Headers.Remove("X-Forwarded-Proto");
            proxyRequest.Headers.TryAddWithoutValidation("X-Forwarded-Proto", httpContext.Request.Scheme);

            proxyRequest.Headers.Remove("X-Forwarded-Port");
            if (httpContext.Request.Host.Port.HasValue)
            {
                proxyRequest.Headers.TryAddWithoutValidation(
                    "X-Forwarded-Port",
                    httpContext.Request.Host.Port.Value.ToString(System.Globalization.CultureInfo.InvariantCulture));
            }
        }
    }

    /// <summary>
    /// Determines whether a hostname is a private or local network host
    /// (e.g. LAN hostnames, .local domains, or RFC 1918 addresses).
    /// Used to auto-accept self-signed TLS certs for self-hosted installs.
    /// </summary>
    private static bool IsPrivateOrLocalHost(string host)
    {
        if (string.IsNullOrWhiteSpace(host))
        {
            return false;
        }

        // Single-label hostnames (no dots) are always local/LAN (e.g. "mint22")
        if (!host.Contains('.'))
        {
            return true;
        }

        // .local mDNS domains
        if (host.EndsWith(".local", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        // Check for RFC 1918 / link-local IP addresses
        if (System.Net.IPAddress.TryParse(host, out var ip))
        {
            var bytes = ip.GetAddressBytes();
            if (bytes.Length == 4)
            {
                return bytes[0] == 10
                    || (bytes[0] == 172 && bytes[1] >= 16 && bytes[1] <= 31)
                    || (bytes[0] == 192 && bytes[1] == 168)
                    || (bytes[0] == 169 && bytes[1] == 254);
            }
        }

        return false;
    }
}
