using DotNetCloud.Core.Auth.Extensions;
using DotNetCloud.Core.Data.Context;
using DotNetCloud.Core.Data.Services;
using DotNetCloud.Core.Data.Extensions;
using DotNetCloud.Core.Data.Naming;
using DotNetCloud.Core.Data.Initialization;
using DotNetCloud.Core.Localization;
using DotNetCloud.Core.Modules;
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
using DotNetCloud.Modules.Music.Data;
using DotNetCloud.Modules.Notes.Data;
using DotNetCloud.Modules.Photos.Data;
using DotNetCloud.Modules.Tracks.Data;
using DotNetCloud.Modules.Video.Data;
using DotNetCloud.Modules.Bookmarks.Data;
using DotNetCloud.Modules.Email.Data;
using DotNetCloud.Modules.AI.Data;
using DotNetCloud.Modules.Search;
using DotNetCloud.Modules.Search.Data;
using DotNetCloud.Modules.Files.Services;
using DotNetCloud.UI.Web.Client.Services;
using DotNetCloud.UI.Web.Services;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.ResponseCompression;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Storage;
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
    private static readonly string[] FilesMigrationChain =
    [
        "20260304172504_InitialFilesSchema",
        "20260308113429_AddFileVersionScanStatus",
        "20260308164648_AddCdcChunkMetadata",
        "20260309063020_AddSyncCursorSupport",
        "20260309083622_AddPosixPermissions",
        "20260309093919_AddSymlinkSupport",
        "20260314133732_SyncHardeningP0",
        "20260315074239_SyncDeviceIdentity",
        "20260315121601_SyncDeviceCursorTracking",
        "20260321123812_AddShareExpiryNotificationSentAt",
        "20260423104054_AddAdminSharedFolders",
    ];

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

        // Resolve database connection string. The CLI's config.json uses the
        // flat key "connectionString"; ASP.NET convention uses "ConnectionStrings:DefaultConnection".
        // Check both so the single config.json file is the source of truth.
        var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
            ?? builder.Configuration["connectionString"];

        if (string.IsNullOrEmpty(connectionString))
        {
            throw new InvalidOperationException(
                "Connection string not found. Set 'ConnectionStrings:DefaultConnection' in appsettings.json " +
                "or 'connectionString' in config.json (DOTNETCLOUD_CONFIG_DIR).");
        }
        builder.Services.AddDotNetCloudDbContext(connectionString);

        // Register in-process module data services for interactive module UI actions,
        // using the same provider as the configured core database.
        var provider = DatabaseProviderDetector.DetectProvider(connectionString);
        builder.Services.AddDbContext<FilesDbContext>(options =>
            ConfigureModuleDbContext(options, provider, connectionString));
        builder.Services.AddDbContext<ChatDbContext>(options =>
            ConfigureModuleDbContext(options, provider, connectionString));
        builder.Services.AddDbContext<ContactsDbContext>(options =>
            ConfigureModuleDbContext(options, provider, connectionString));
        builder.Services.AddDbContext<CalendarDbContext>(options =>
            ConfigureModuleDbContext(options, provider, connectionString));
        builder.Services.AddDbContext<NotesDbContext>(options =>
            ConfigureModuleDbContext(options, provider, connectionString));
        builder.Services.AddDbContext<TracksDbContext>(options =>
            ConfigureModuleDbContext(options, provider, connectionString));
        builder.Services.AddDbContext<PhotosDbContext>(options =>
            ConfigureModuleDbContext(options, provider, connectionString));
        builder.Services.AddDbContext<MusicDbContext>(options =>
            ConfigureModuleDbContext(options, provider, connectionString));
        builder.Services.AddDbContext<VideoDbContext>(options =>
            ConfigureModuleDbContext(options, provider, connectionString));
        builder.Services.AddDbContext<AiDbContext>(options =>
            ConfigureModuleDbContext(options, provider, connectionString));
        builder.Services.AddDbContext<SearchDbContext>(options =>
            ConfigureModuleDbContext(options, provider, connectionString));
        builder.Services.AddDbContext<BookmarksDbContext>(options =>
            ConfigureModuleDbContext(options, provider, connectionString));
        builder.Services.AddDbContext<EmailDbContext>(options =>
            ConfigureModuleDbContext(options, provider, connectionString));

        // Register schema services for lazy module schema creation.
        // SelfManagedSchemaProvider and ModuleSchemaService are registered by AddDotNetCloudDbContext.
        builder.Services.AddSingleton<IModuleSchemaProvider, DbContextSchemaProvider>();

        builder.Services.AddFilesServices(builder.Configuration);
        builder.Services.AddChatServices(builder.Configuration);
        builder.Services.AddContactsServices(builder.Configuration);
        builder.Services.AddCalendarServices(builder.Configuration);
        builder.Services.AddNotesServices(builder.Configuration);
        builder.Services.AddTracksServices(builder.Configuration);
        builder.Services.AddPhotosServices(builder.Configuration);
        builder.Services.AddMusicServices(builder.Configuration);
        builder.Services.AddVideoServices(builder.Configuration);
        builder.Services.AddAiServices(builder.Configuration);
        builder.Services.AddSearchServices(builder.Configuration);
        builder.Services.AddBookmarksServices(builder.Configuration);
        builder.Services.AddEmailServices(builder.Configuration);
        builder.Services.AddSingleton<DotNetCloud.Modules.Files.Data.Services.Background.IAdminSharedFolderReindexDispatcher>(sp =>
            new InProcessAdminSharedFolderReindexDispatcher(sp.GetService<DotNetCloud.Modules.Search.Services.SearchReindexBackgroundService>()));
        // Register ISearchableModule implementations for search indexing
        builder.Services.AddScoped<DotNetCloud.Core.Capabilities.ISearchableModule, DotNetCloud.Modules.Files.Data.Services.FilesSearchableModule>();
        builder.Services.AddScoped<DotNetCloud.Core.Capabilities.ISearchableModule, DotNetCloud.Modules.Notes.Data.Services.NotesSearchableModule>();
        builder.Services.AddScoped<DotNetCloud.Core.Capabilities.ISearchableModule, DotNetCloud.Modules.Calendar.Data.Services.CalendarSearchableModule>();
        builder.Services.AddScoped<DotNetCloud.Core.Capabilities.ISearchableModule, DotNetCloud.Modules.Bookmarks.Data.Services.BookmarksSearchableModule>();
        builder.Services.AddScoped<DotNetCloud.Core.Capabilities.ISearchableModule, DotNetCloud.Modules.Email.Data.Services.EmailSearchableModule>();
        builder.Services.AddSingleton<IEventBus, InProcessEventBus>();
        builder.Services.AddSingleton<DotNetCloud.Core.Capabilities.ICrossModuleLinkResolver, CrossModuleLinkResolver>();
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
        builder.Services.AddSingleton<ModuleUiRegistry>();
        builder.Services.AddScoped<DotNetCloud.UI.Shared.Services.BrowserTimeProvider>();
        builder.Services.AddScoped<ToastService>();
        builder.Services.AddTransient<DotNetCloud.Core.Server.Middleware.CookieForwardingHandler>();
        builder.Services.AddScoped(sp =>
        {
            var nav = sp.GetRequiredService<NavigationManager>();
            var configuration = sp.GetRequiredService<IConfiguration>();
            var baseUri = new Uri(nav.BaseUri);
            var allowInsecureTls = configuration.GetValue<bool>("Files:Collabora:AllowInsecureTls");

            // Cookie forwarding handler: during SSR, forwards browser auth cookies
            // to the outgoing HttpClient so API calls are authenticated.
            var cookieHandler = sp.GetRequiredService<DotNetCloud.Core.Server.Middleware.CookieForwardingHandler>();

            // In self-hosted installs with self-signed HTTPS certs, server-side
            // Blazor API calls to the same origin would otherwise fail TLS validation.
            // Accept self-signed certs for loopback, private/LAN hostnames, or when
            // AllowInsecureTls is explicitly enabled.
            if (baseUri.Scheme == Uri.UriSchemeHttps &&
                (baseUri.IsLoopback || allowInsecureTls || IsPrivateOrLocalHost(baseUri.Host)))
            {
                var sslHandler = new HttpClientHandler
                {
                    ServerCertificateCustomValidationCallback = HttpClientHandler.DangerousAcceptAnyServerCertificateValidator
                };
                cookieHandler.InnerHandler = sslHandler;

                return new HttpClient(cookieHandler) { BaseAddress = baseUri };
            }

            cookieHandler.InnerHandler = new HttpClientHandler();
            return new HttpClient(cookieHandler) { BaseAddress = baseUri };
        });
        builder.Services.AddScoped<DotNetCloud.Modules.Contacts.Services.IContactsApiClient, DotNetCloud.Modules.Contacts.Services.ContactsApiClient>();
        builder.Services.AddScoped<DotNetCloud.Modules.Calendar.Services.ICalendarApiClient, DotNetCloud.Modules.Calendar.Services.CalendarApiClient>();
        builder.Services.AddScoped<DotNetCloud.Modules.Notes.Services.INotesApiClient, DotNetCloud.Modules.Notes.Services.NotesApiClient>();
        builder.Services.AddScoped<DotNetCloud.Modules.Tracks.Services.ITracksApiClient, DotNetCloud.Modules.Tracks.Services.TracksApiClient>();
        builder.Services.AddScoped<DotNetCloud.Modules.Tracks.Services.IOnboardingStateService, DotNetCloud.Modules.Tracks.Services.OnboardingStateService>();
        builder.Services.AddScoped<DotNetCloud.Modules.Email.Services.IEmailApiClient, DotNetCloud.Modules.Email.Services.EmailApiClient>();
        builder.Services.AddScoped<DotNetCloud.Modules.Bookmarks.Services.IBookmarksApiClient, DotNetCloud.Modules.Bookmarks.Services.BookmarksApiClient>();
        builder.Services.AddScoped<DotNetCloudApiClient>();

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
                tags: ["ready"]));
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
        builder.Services.AddHostedService<ModuleUiRegistrationHostedService>();
        builder.Services.AddHostedService<NotificationEventSubscriber>();
        builder.Services.AddHostedService<SearchEventSubscriber>();

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

    private static async Task MigrateFilesDatabaseAsync(
        FilesDbContext context,
        ILogger logger,
        CancellationToken cancellationToken = default)
    {
        await BaselineLegacyFilesMigrationHistoryAsync(context, logger, cancellationToken);
        await context.Database.MigrateAsync(cancellationToken);
        logger.LogInformation("Files module database migrated");
    }

    private static async Task BaselineLegacyFilesMigrationHistoryAsync(
        FilesDbContext context,
        ILogger logger,
        CancellationToken cancellationToken)
    {
        var pendingMigrations = (await context.Database.GetPendingMigrationsAsync(cancellationToken))
            .ToHashSet(StringComparer.Ordinal);

        if (!pendingMigrations.Contains(FilesMigrationChain[0]))
        {
            return;
        }

        if (!await ModuleTableExistsAsync(context, "FileNodes"))
        {
            return;
        }

        var detectedMigrations = await DetectLegacyFilesMigrationsAsync(context, cancellationToken);
        if (detectedMigrations.Count == 0)
        {
            return;
        }

        var appliedMigrations = (await context.Database.GetAppliedMigrationsAsync(cancellationToken))
            .ToHashSet(StringComparer.Ordinal);

        var migrationsToInsert = detectedMigrations
            .Where(migrationId => !appliedMigrations.Contains(migrationId))
            .ToList();

        if (migrationsToInsert.Count == 0)
        {
            return;
        }

        foreach (var migrationId in migrationsToInsert)
        {
            await InsertMigrationHistoryRowAsync(context, migrationId, cancellationToken);
        }

        logger.LogWarning(
            "Baselined {Count} Files migration history entries for a legacy schema created without EF migration history: {Migrations}",
            migrationsToInsert.Count,
            string.Join(", ", migrationsToInsert));
    }

    private static async Task<List<string>> DetectLegacyFilesMigrationsAsync(
        FilesDbContext context,
        CancellationToken cancellationToken)
    {
        var detected = new List<string>();

        if (!await TablesExistAsync(
                context,
                cancellationToken,
                "FileChunks",
                "FileNodes",
                "FileShares",
                "FileVersionChunks",
                "FileVersions",
                "UploadSessions"))
        {
            return detected;
        }

        detected.Add(FilesMigrationChain[0]);

        if (!await ColumnExistsAsync(context, "FileVersions", "ScanStatus", cancellationToken))
        {
            return detected;
        }

        detected.Add(FilesMigrationChain[1]);

        if (!await ColumnExistsAsync(context, "UploadSessions", "ChunkSizesManifest", cancellationToken)
            || !await ColumnExistsAsync(context, "FileVersionChunks", "ChunkSize", cancellationToken)
            || !await ColumnExistsAsync(context, "FileVersionChunks", "Offset", cancellationToken))
        {
            return detected;
        }

        detected.Add(FilesMigrationChain[2]);

        if (!await TablesExistAsync(context, cancellationToken, "UserSyncCounters")
            || !await ColumnExistsAsync(context, "FileNodes", "SyncSequence", cancellationToken))
        {
            return detected;
        }

        detected.Add(FilesMigrationChain[3]);

        if (!await ColumnExistsAsync(context, "UploadSessions", "PosixMode", cancellationToken)
            || !await ColumnExistsAsync(context, "UploadSessions", "PosixOwnerHint", cancellationToken)
            || !await ColumnExistsAsync(context, "FileVersions", "PosixMode", cancellationToken)
            || !await ColumnExistsAsync(context, "FileNodes", "PosixMode", cancellationToken)
            || !await ColumnExistsAsync(context, "FileNodes", "PosixOwnerHint", cancellationToken))
        {
            return detected;
        }

        detected.Add(FilesMigrationChain[4]);

        if (!await ColumnExistsAsync(context, "FileNodes", "LinkTarget", cancellationToken))
        {
            return detected;
        }

        detected.Add(FilesMigrationChain[5]);

        if (!await IndexExistsAsync(context, "FileNodes", "uq_file_nodes_parent_name_active", cancellationToken)
            || !await IndexExistsAsync(context, "FileNodes", "uq_file_nodes_root_name_active", cancellationToken))
        {
            return detected;
        }

        detected.Add(FilesMigrationChain[6]);

        if (!await TablesExistAsync(context, cancellationToken, "SyncDevices")
            || !await ColumnExistsAsync(context, "UploadSessions", "DeviceId", cancellationToken)
            || !await ColumnExistsAsync(context, "FileNodes", "OriginatingDeviceId", cancellationToken))
        {
            return detected;
        }

        detected.Add(FilesMigrationChain[7]);

        if (!await TablesExistAsync(context, cancellationToken, "SyncDeviceCursors"))
        {
            return detected;
        }

        detected.Add(FilesMigrationChain[8]);

        if (!await ColumnExistsAsync(context, "FileShares", "ExpiryNotificationSentAt", cancellationToken))
        {
            return detected;
        }

        detected.Add(FilesMigrationChain[9]);

        if (!await TablesExistAsync(context, cancellationToken, "AdminSharedFolders", "AdminSharedFolderGrants"))
        {
            return detected;
        }

        detected.Add(FilesMigrationChain[10]);
        return detected;
    }

    private static async Task<bool> TablesExistAsync(
        DbContext context,
        CancellationToken cancellationToken,
        params string[] tableNames)
    {
        foreach (var tableName in tableNames)
        {
            if (!await ModuleTableExistsAsync(context, tableName))
            {
                return false;
            }
        }

        return true;
    }

    private static async Task<bool> ColumnExistsAsync(
        DbContext context,
        string tableName,
        string columnName,
        CancellationToken cancellationToken)
    {
        var provider = context.Database.ProviderName ?? string.Empty;

        if (provider.Contains("Npgsql", StringComparison.OrdinalIgnoreCase))
        {
            return await ExecuteExistsQueryAsync(
                context,
                "SELECT EXISTS (SELECT 1 FROM information_schema.columns WHERE table_schema = 'public' AND table_name = @tableName AND column_name = @columnName);",
                cancellationToken,
                ("@tableName", tableName),
                ("@columnName", columnName));
        }

        if (provider.Contains("SqlServer", StringComparison.OrdinalIgnoreCase))
        {
            return await ExecuteExistsQueryAsync(
                context,
                "SELECT CASE WHEN EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_SCHEMA = 'dbo' AND TABLE_NAME = @tableName AND COLUMN_NAME = @columnName) THEN 1 ELSE 0 END;",
                cancellationToken,
                ("@tableName", tableName),
                ("@columnName", columnName));
        }

        if (provider.Contains("InMemory", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        throw new NotSupportedException($"Unsupported relational provider for column checks: {provider}");
    }

    private static async Task<bool> IndexExistsAsync(
        DbContext context,
        string tableName,
        string indexName,
        CancellationToken cancellationToken)
    {
        var provider = context.Database.ProviderName ?? string.Empty;

        if (provider.Contains("Npgsql", StringComparison.OrdinalIgnoreCase))
        {
            return await ExecuteExistsQueryAsync(
                context,
                "SELECT EXISTS (SELECT 1 FROM pg_indexes WHERE schemaname = 'public' AND tablename = @tableName AND indexname = @indexName);",
                cancellationToken,
                ("@tableName", tableName),
                ("@indexName", indexName));
        }

        if (provider.Contains("SqlServer", StringComparison.OrdinalIgnoreCase))
        {
            return await ExecuteExistsQueryAsync(
                context,
                "SELECT CASE WHEN EXISTS (SELECT 1 FROM sys.indexes i INNER JOIN sys.tables t ON i.object_id = t.object_id INNER JOIN sys.schemas s ON t.schema_id = s.schema_id WHERE s.name = 'dbo' AND t.name = @tableName AND i.name = @indexName) THEN 1 ELSE 0 END;",
                cancellationToken,
                ("@tableName", tableName),
                ("@indexName", indexName));
        }

        if (provider.Contains("InMemory", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        throw new NotSupportedException($"Unsupported relational provider for index checks: {provider}");
    }

    private static async Task<bool> ExecuteExistsQueryAsync(
        DbContext context,
        string commandText,
        CancellationToken cancellationToken,
        params (string Name, object Value)[] parameters)
    {
        var connection = context.Database.GetDbConnection();
        var shouldClose = connection.State != System.Data.ConnectionState.Open;

        if (shouldClose)
        {
            await connection.OpenAsync(cancellationToken);
        }

        try
        {
            await using var command = connection.CreateCommand();
            command.CommandText = commandText;

            foreach (var (name, value) in parameters)
            {
                var parameter = command.CreateParameter();
                parameter.ParameterName = name;
                parameter.Value = value;
                command.Parameters.Add(parameter);
            }

            var result = await command.ExecuteScalarAsync(cancellationToken);
            return result switch
            {
                bool boolResult => boolResult,
                byte byteResult => byteResult != 0,
                short shortResult => shortResult != 0,
                int intResult => intResult != 0,
                long longResult => longResult != 0,
                _ => false,
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

    private static async Task InsertMigrationHistoryRowAsync(
        DbContext context,
        string migrationId,
        CancellationToken cancellationToken)
    {
        var provider = context.Database.ProviderName ?? string.Empty;
        var commandText = provider.Contains("Npgsql", StringComparison.OrdinalIgnoreCase)
            ? "INSERT INTO \"__EFMigrationsHistory\" (\"MigrationId\", \"ProductVersion\") VALUES (@migrationId, @productVersion) ON CONFLICT (\"MigrationId\") DO NOTHING;"
            : provider.Contains("SqlServer", StringComparison.OrdinalIgnoreCase)
                ? "IF NOT EXISTS (SELECT 1 FROM [__EFMigrationsHistory] WHERE [MigrationId] = @migrationId) INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion]) VALUES (@migrationId, @productVersion);"
                : throw new NotSupportedException($"Unsupported relational provider for migration history inserts: {provider}");

        var connection = context.Database.GetDbConnection();
        var shouldClose = connection.State != System.Data.ConnectionState.Open;

        if (shouldClose)
        {
            await connection.OpenAsync(cancellationToken);
        }

        try
        {
            await using var command = connection.CreateCommand();
            command.CommandText = commandText;

            var migrationParameter = command.CreateParameter();
            migrationParameter.ParameterName = "@migrationId";
            migrationParameter.Value = migrationId;
            command.Parameters.Add(migrationParameter);

            var versionParameter = command.CreateParameter();
            versionParameter.ParameterName = "@productVersion";
            versionParameter.Value = GetEntityFrameworkProductVersion();
            command.Parameters.Add(versionParameter);

            await command.ExecuteNonQueryAsync(cancellationToken);
        }
        finally
        {
            if (shouldClose)
            {
                await connection.CloseAsync();
            }
        }
    }

    private static string GetEntityFrameworkProductVersion()
    {
        return typeof(DbContext).Assembly
                   .GetCustomAttribute<AssemblyInformationalVersionAttribute>()
                   ?.InformationalVersion
                   ?.Split('+')[0]
               ?? typeof(DbContext).Assembly.GetName().Version?.ToString()
               ?? "10.0.0";
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
            else if (provider.Contains("InMemory", StringComparison.OrdinalIgnoreCase))
            {
                // Integration tests use EF InMemory; there are no physical tables to probe.
                return true;
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
        app.UseResponseEnvelope(options =>
        {
            options.ExcludePaths =
            [
                .. options.ExcludePaths,
                "/api/v1/wopi/files/",
                "/api/v1/videos/",
                "/api/v1/music/",
            ];
        });

        // CORS
        app.UseCors(CorsConfiguration.PolicyName);

        app.UseHttpsRedirection();

        app.UseAuthentication();
        app.UseAuthorization();

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
                typeof(DotNetCloud.Modules.Email.UI.EmailPage).Assembly);
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
