using DotNetCloud.Core.Auth.Extensions;
using DotNetCloud.Core.Data.Extensions;
using DotNetCloud.Core.Data.Naming;
using DotNetCloud.Core.Data.Initialization;
using DotNetCloud.Core.Localization;
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
using DotNetCloud.Modules.Chat.Data;
using DotNetCloud.Modules.Files.Data;
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
using System.Net.Http;
using System.Net.Security;
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

                // Ensure module data stores are initialized.
                // In Development/Testing hosts (for example WebApplicationFactory integration tests),
                // module DbContexts may intentionally use alternate providers and should not block startup.
                if (app.Environment.IsDevelopment() || app.Environment.IsEnvironment("Testing"))
                {
                    try
                    {
                        var filesDbContext = scope.ServiceProvider.GetRequiredService<FilesDbContext>();
                        await EnsureModuleTablesCreatedAsync(filesDbContext, "FileNodes", logger);

                        var chatDbContext = scope.ServiceProvider.GetRequiredService<ChatDbContext>();
                        await EnsureModuleTablesCreatedAsync(chatDbContext, "Channels", logger);
                    }
                    catch (InvalidOperationException ex)
                    {
                        logger.LogWarning(ex,
                            "Skipping module table bootstrap in {Environment} environment due to provider configuration.",
                            app.Environment.EnvironmentName);
                    }
                }
                else
                {
                    var filesDbContext = scope.ServiceProvider.GetRequiredService<FilesDbContext>();
                    await EnsureModuleTablesCreatedAsync(filesDbContext, "FileNodes", logger);

                    var chatDbContext = scope.ServiceProvider.GetRequiredService<ChatDbContext>();
                    await EnsureModuleTablesCreatedAsync(chatDbContext, "Channels", logger);
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
        builder.Services.AddChatServices(builder.Configuration);
        builder.Services.AddSingleton<IEventBus, InProcessEventBus>();

        var filesStoragePath = builder.Configuration.GetValue<string>("Files:StoragePath");
        var dataDirForStorage = Environment.GetEnvironmentVariable("DOTNETCLOUD_DATA_DIR");
        if (string.IsNullOrWhiteSpace(filesStoragePath))
        {
            filesStoragePath = !string.IsNullOrWhiteSpace(dataDirForStorage)
                ? Path.Combine(dataDirForStorage, "storage")
                : Path.Combine(builder.Environment.ContentRootPath, "storage");
        }

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
            var configuration = sp.GetRequiredService<IConfiguration>();
            var baseUri = new Uri(nav.BaseUri);
            var allowInsecureUiHttps = configuration.GetValue<bool>("Files:Collabora:AllowInsecureTls");

            // In local bare-metal installs with self-signed HTTPS certs, server-side
            // Blazor API calls to the same origin would otherwise fail TLS validation.
            // When AllowInsecureTls is enabled, accept the cert for non-loopback local hostnames
            // (for example https://mint22:15443) used in LAN testing.
            if (baseUri.Scheme == Uri.UriSchemeHttps && (baseUri.IsLoopback || allowInsecureUiHttps))
            {
                var handler = new HttpClientHandler
                {
                    ServerCertificateCustomValidationCallback = HttpClientHandler.DangerousAcceptAnyServerCertificateValidator
                };

                return new HttpClient(handler) { BaseAddress = baseUri };
            }

            return new HttpClient { BaseAddress = baseUri };
        });
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
                $"default-src 'self'; script-src 'self' 'unsafe-inline' 'wasm-unsafe-eval'; style-src 'self' 'unsafe-inline'; img-src 'self' data: https:; font-src 'self' data:; connect-src 'self' ws: wss:; frame-src 'self' {collaboraOrigin}; child-src 'self' {collaboraOrigin}; frame-ancestors 'none';";
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
        app.UseResponseEnvelope(options =>
        {
            options.ExcludePaths =
            [
                .. options.ExcludePaths,
                "/api/v1/wopi/files/",
            ];
        });

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
            .AddAdditionalAssemblies(typeof(DotNetCloud.UI.Web.Client._Imports).Assembly);
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
}
