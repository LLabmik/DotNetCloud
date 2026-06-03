using DotNetCloud.Core.Events;
using DotNetCloud.Modules.Calendar;
using DotNetCloud.Modules.Calendar.Data;
using DotNetCloud.Modules.Calendar.Host.Services;
using Microsoft.EntityFrameworkCore;

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

// --- Services ---

// Register the Calendar module as a singleton for lifecycle management
builder.Services.AddSingleton<CalendarModule>();

// Use shared database from core server config, fall back to in-memory
var connStr = builder.Configuration["connectionString"]
    ?? builder.Configuration.GetConnectionString("DefaultConnection");
var dbProvider = builder.Configuration["databaseProvider"]
    ?? builder.Configuration["database:provider"];

if (!string.IsNullOrEmpty(connStr) && !string.IsNullOrEmpty(dbProvider))
{
    builder.Services.AddDbContext<CalendarDbContext>(o =>
    {
        if (string.Equals(dbProvider, "PostgreSql", StringComparison.OrdinalIgnoreCase))
            o.UseNpgsql(connStr);
        else
            o.UseSqlServer(connStr);
    });
}
else
{
    builder.Services.AddDbContext<CalendarDbContext>(o => o.UseInMemoryDatabase("CalendarModule"));
}

// In-process event bus for standalone operation
builder.Services.AddSingleton<IEventBus, InProcessEventBus>();

// Stub IOrganizationDirectory (module doesn't have full auth — Core.Server handles that)
builder.Services.AddSingleton<DotNetCloud.Core.Capabilities.IOrganizationDirectory>(
    new StubOrganizationDirectory());

// Register all calendar business-logic services (Calendar, Event, Share, ICal)
builder.Services.AddCalendarServices(builder.Configuration);

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

// --- Middleware ---

// Map gRPC services
app.MapGrpcService<CalendarGrpcService>();
app.MapGrpcService<CalendarLifecycleService>();

// Map REST API controllers
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

/// <summary>No-op stub for IOrganizationDirectory when auth isn't available.</summary>
internal sealed class StubOrganizationDirectory : DotNetCloud.Core.Capabilities.IOrganizationDirectory
{
    public Task<bool> IsOrganizationMemberAsync(Guid orgId, Guid userId, CancellationToken ct = default) => Task.FromResult(false);
    public Task<DotNetCloud.Core.DTOs.OrganizationMemberInfo?> GetMemberAsync(Guid orgId, Guid userId, CancellationToken ct = default) => Task.FromResult<DotNetCloud.Core.DTOs.OrganizationMemberInfo?>(null);
    public Task<IReadOnlyList<DotNetCloud.Core.DTOs.OrganizationDto>> GetUserOrganizationsAsync(Guid userId, CancellationToken ct = default) => Task.FromResult<IReadOnlyList<DotNetCloud.Core.DTOs.OrganizationDto>>(Array.Empty<DotNetCloud.Core.DTOs.OrganizationDto>());
    public Task<bool> HasOrgRoleAsync(Guid orgId, Guid userId, Guid roleId, CancellationToken ct = default) => Task.FromResult(false);
    public Task<bool> HasManagerOrAboveRoleAsync(Guid orgId, Guid userId, CancellationToken ct = default) => Task.FromResult(false);
    public Task<IReadOnlyList<Guid>> GetUserRoleIdsAsync(Guid orgId, Guid userId, CancellationToken ct = default) => Task.FromResult<IReadOnlyList<Guid>>(Array.Empty<Guid>());
}
