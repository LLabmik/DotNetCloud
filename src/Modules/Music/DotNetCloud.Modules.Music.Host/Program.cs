using DotNetCloud.Core.Events;
using DotNetCloud.Core.Data.Naming;
using DotNetCloud.Modules.Music;
using DotNetCloud.Modules.Music.Data;
using DotNetCloud.Modules.Music.Data.Services;
using DotNetCloud.Modules.Music.Host.Services;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Microsoft.EntityFrameworkCore;

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

// Register the module as singleton
builder.Services.AddSingleton<MusicModule>();

// Register EF Core with config-driven database, falling back to in-memory
var connectionString = builder.Configuration["connectionString"]
    ?? builder.Configuration.GetConnectionString("DefaultConnection");
var dbProvider = builder.Configuration["databaseProvider"]
    ?? builder.Configuration["database:provider"];

if (!string.IsNullOrEmpty(connectionString) && !string.IsNullOrEmpty(dbProvider))
{
    builder.Services.AddSingleton<ITableNamingStrategy>(
        string.Equals(dbProvider, "PostgreSql", StringComparison.OrdinalIgnoreCase)
            ? new PostgreSqlNamingStrategy()
            : new SqlServerNamingStrategy());

    void ConfigureDb(DbContextOptionsBuilder o)
    {
        if (string.Equals(dbProvider, "PostgreSql", StringComparison.OrdinalIgnoreCase))
            o.UseNpgsql(connectionString);
        else
            o.UseSqlServer(connectionString);
    }

    builder.Services.AddDbContextFactory<MusicDbContext>(ConfigureDb);
    builder.Services.AddDbContext<MusicDbContext>(ConfigureDb);
}
else
{
    builder.Services.AddSingleton<ITableNamingStrategy>(new PostgreSqlNamingStrategy());
    builder.Services.AddDbContextFactory<MusicDbContext>(options =>
        options.UseInMemoryDatabase("MusicModule"));
    builder.Services.AddDbContext<MusicDbContext>(options =>
        options.UseInMemoryDatabase("MusicModule"));
}

// In-process event bus for standalone operation
builder.Services.AddSingleton<IEventBus, InProcessEventBus>();

// Register all business-logic services
builder.Services.AddMusicServices(builder.Configuration);

// gRPC
builder.Services.AddGrpc();
builder.Services.AddControllers();
builder.Services.AddHealthChecks()
    .AddCheck<MusicHealthCheck>("music_module");

var app = builder.Build();

// Map gRPC services
app.MapGrpcService<MusicGrpcServiceImpl>();
app.MapGrpcService<MusicLifecycleService>();

// Map REST API controllers
app.MapControllers();

// Health check endpoint
app.MapHealthChecks("/health");

// Minimal info endpoint
app.MapGet("/", () => Results.Ok(new
{
    module = "dotnetcloud.music",
    version = "1.0.0",
    status = "running"
}));

app.Run();

/// <summary>Marker class for integration test host reference.</summary>
public partial class Program;
