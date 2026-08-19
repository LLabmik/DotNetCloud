using DotNetCloud.Core.Auth.Authorization;
using DotNetCloud.Core.Auth.Introspection;
using DotNetCloud.Core.Data.Naming;
using DotNetCloud.Core.Events;
using DotNetCloud.Core.Grpc;
using DotNetCloud.Core.Grpc.Capabilities;
using DotNetCloud.Modules.Calendar;
using DotNetCloud.Modules.Calendar.Data;
using DotNetCloud.Modules.Calendar.Host.Services;
using DotNetCloud.Modules.Contacts.Data;
using DotNetCloud.Modules.Contacts.Data.Services;
using IContactDirectory = DotNetCloud.Core.Capabilities.IContactDirectory;
using IOrganizationDirectory = DotNetCloud.Core.Capabilities.IOrganizationDirectory;
using DotNetCloud.Core.Auth.Capabilities;
using DotNetCloud.Core.Data.Context;
using Grpc.Net.Client;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Diagnostics;

var builder = WebApplication.CreateBuilder(args);

// Load shared config from DOTNETCLOUD_CONFIG_DIR (database connection string from setup)
var configDir = Environment.GetEnvironmentVariable("DOTNETCLOUD_CONFIG_DIR");
if (!string.IsNullOrEmpty(configDir))
{
    var configJsonPath = Path.Combine(configDir, "config.json");
    if (File.Exists(configJsonPath))
    {
        builder.Configuration.AddJsonFile(configJsonPath, optional: true, reloadOnChange: false);
    }
}

// Read gRPC endpoint assigned by the ProcessSupervisor
var grpcEndpoint = Environment.GetEnvironmentVariable("DOTNETCLOUD_GRPC_ENDPOINT");
if (!string.IsNullOrEmpty(grpcEndpoint))
{
    var uri = new Uri(grpcEndpoint.Replace("unix://", "http://").Replace("net.pipe://", "http://"));
    builder.WebHost.ConfigureKestrel(options =>
    {
        options.Listen(System.Net.IPAddress.Loopback, uri.Port, listenOptions =>
        {
            listenOptions.Protocols = Microsoft.AspNetCore.Server.Kestrel.Core.HttpProtocols.Http2;
        });
    });
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

// Register token introspection client (replaces local JWT key validation).
// Bearer tokens are validated by calling Core.Server's TokenIntrospection gRPC service.
builder.Services.AddTokenIntrospection();

// Register the gRPC-backed audit logger (SOC 2 CC4) — routes to Core.Server.
builder.Services.AddAuditLogger();

// Authentication: supports both cookie (browser/Blazor) and introspection (desktop/mobile).
// A policy scheme automatically routes to the correct handler based on the request.
builder.Services.AddAuthentication(options =>
    {
        options.DefaultScheme = "DotNetCloud.Module";
        options.DefaultAuthenticateScheme = "DotNetCloud.Module";
        options.DefaultChallengeScheme = "DotNetCloud.Module";
    })
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
    })
    .AddIntrospection(IntrospectionAuthenticationExtensions.SchemeName)
    .AddPolicyScheme("DotNetCloud.Module", "DotNetCloud.Module", options =>
    {
        // Route to introspection handler for Bearer tokens, Cookie handler for browser requests
        options.ForwardDefaultSelector = context =>
        {
            if (context.Request.Headers.TryGetValue("Authorization", out var auth)
                && auth.Count > 0
                && auth[0]?.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase) == true)
            {
                return IntrospectionAuthenticationExtensions.SchemeName;
            }
            return "Identity.Application";
        };
    });

builder.Services.AddAuthorization(options => AuthorizationPolicies.Configure(options));
builder.Services.AddSingleton<IAuthorizationHandler, PermissionAuthorizationHandler>();

// --- Services ---

// Register the Calendar module as a singleton for lifecycle management
builder.Services.AddSingleton<CalendarModule>();

// Use shared database from core server config (fail fast if missing)
var connStr = builder.Configuration["connectionString"]
    ?? builder.Configuration.GetConnectionString("DefaultConnection");
var dbProvider = builder.Configuration["databaseProvider"]
    ?? builder.Configuration["database:provider"];

if (string.IsNullOrEmpty(connStr) || string.IsNullOrEmpty(dbProvider))
{
    throw new InvalidOperationException(
        "The Calendar module requires a database connection string and provider. " +
        "These are provided via config.json (DOTNETCLOUD_CONFIG_DIR) when launched by the core server.");
}

// Register the naming strategy so CalendarDbContext uses the correct
// table/column naming for the active provider (snake_case for PostgreSQL,
// PascalCase for SQL Server).
if (string.Equals(dbProvider, "PostgreSql", StringComparison.OrdinalIgnoreCase))
    builder.Services.AddSingleton<ITableNamingStrategy, PostgreSqlNamingStrategy>();
else
    builder.Services.AddSingleton<ITableNamingStrategy, SqlServerNamingStrategy>();

builder.Services.AddDbContext<CalendarDbContext>(o =>
{
    if (string.Equals(dbProvider, "PostgreSql", StringComparison.OrdinalIgnoreCase))
        o.UseNpgsql(connStr);
    else
        o.UseSqlServer(connStr, sql => sql.MigrationsAssembly("DotNetCloud.Modules.Calendar.Data.SqlServer"));
});

// In-process event bus for standalone operation
builder.Services.AddSingleton<IEventBus, InProcessEventBus>();

// Contact directory — real implementation from Contacts module for attendee search.
builder.Services.AddDbContext<ContactsDbContext>(o =>
{
    if (string.Equals(dbProvider, "PostgreSql", StringComparison.OrdinalIgnoreCase))
        o.UseNpgsql(connStr);
    else
        o.UseSqlServer(connStr, sql => sql.MigrationsAssembly("DotNetCloud.Modules.Contacts.Data.SqlServer"));
}, ServiceLifetime.Transient);
builder.Services.AddScoped<IContactDirectory, ContactDirectoryService>();

// Organization directory — real implementation from Core.Auth for calendar sharing checks.
// CoreDbContext must be registered for OrganizationDirectoryService.
builder.Services.AddDbContext<CoreDbContext>(o =>
{
    if (string.Equals(dbProvider, "PostgreSql", StringComparison.OrdinalIgnoreCase))
        o.UseNpgsql(connStr);
    else
        o.UseSqlServer(connStr, sql => sql.MigrationsAssembly("DotNetCloud.Core.Data.SqlServer"));
}, ServiceLifetime.Transient);
builder.Services.AddScoped<IOrganizationDirectory, OrganizationDirectoryService>();

// Register all calendar business-logic services (Calendar, Event, Share, ICal)
builder.Services.AddCalendarServices(builder.Configuration);

// CoreCapabilities gRPC client — connects to Core.Server for in-app notifications
// and real-time event broadcasting. The endpoint is provided by the ProcessSupervisor.
var coreEndpoint = Environment.GetEnvironmentVariable("DOTNETCLOUD_CORE_ENDPOINT");
if (!string.IsNullOrEmpty(coreEndpoint))
{
    _ = builder.Services.AddSingleton(_ =>
    {
        var channel = GrpcChannel.ForAddress(coreEndpoint);
        return new CoreCapabilities.CoreCapabilitiesClient(channel);
    });
    builder.Services.AddHostedService<CalendarReminderEventSubscriber>();
    builder.Services.AddHostedService<CalendarEventBroadcastSubscriber>();
}

// Contacts gRPC client for attendee contact search
builder.Services.Configure<DotNetCloud.Modules.Calendar.Host.Configuration.ContactsGrpcClientOptions>(
    builder.Configuration.GetSection(DotNetCloud.Modules.Calendar.Host.Configuration.ContactsGrpcClientOptions.SectionName));
builder.Services.AddSingleton<DotNetCloud.Modules.Calendar.Host.Services.ContactsGrpcClient>();

builder.Services.AddGrpc();

// REST API controllers
builder.Services.AddControllers();

// Health checks
builder.Services.AddHealthChecks()
    .AddCheck<CalendarHealthCheck>("calendar_module");

var app = builder.Build();

// Ensure database schema exists.
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<CalendarDbContext>();
    var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();
    try
    {
        await db.Database.MigrateAsync();
        logger.LogInformation("Calendar database migrated successfully");
    }
    catch (Exception ex)
    {
        logger.LogWarning(ex, "Calendar MigrateAsync failed, falling back to EnsureCreated");
        try
        {
            var created = await db.Database.EnsureCreatedAsync();
            logger.LogInformation("Calendar EnsureCreated result: {Created}", created);
        }
        catch (Exception ex2)
        {
            logger.LogError(ex2, "Calendar EnsureCreated also failed");
        }
    }
}

// Show full exception details for debugging; remove in production.
app.UseDeveloperExceptionPage();

// --- Middleware ---

// Map gRPC services
app.MapGrpcService<CalendarGrpcService>();
app.MapGrpcService<CalendarLifecycleService>();

// Map REST API controllers
// Trust X-Forwarded-Proto from the YARP proxy so __Host- cookies work over HTTP.
app.UseForwardedHeaders(new ForwardedHeadersOptions
{
    ForwardedHeaders = ForwardedHeaders.XForwardedProto,
    KnownProxies = { System.Net.IPAddress.Loopback, System.Net.IPAddress.IPv6Loopback },
});
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

// Health check endpoint
app.MapHealthChecks("/health");

// Minimal info endpoint
app.MapGet("/", () => Results.Ok(new
{
    module = "dotnetcloud.calendar",
    version = "1.0.0",
    status = "running"
}));

app.Run();

/// <summary>Entry point marker for WebApplicationFactory in integration tests.</summary>
public partial class Program;
