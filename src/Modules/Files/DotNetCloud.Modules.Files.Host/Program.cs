using DotNetCloud.Core.Events;
using DotNetCloud.Modules.Files;
using DotNetCloud.Modules.Files.Data;
using DotNetCloud.Modules.Files.Host.Services;
using DotNetCloud.Modules.Files.Services;
using DotNetCloud.Modules.Search.Client;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Microsoft.EntityFrameworkCore;
using Scalar.AspNetCore;

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

// --- Services ---

// Register the Files module as a singleton for lifecycle management
builder.Services.AddSingleton<FilesModule>();

// Register EF Core with config-driven database, falling back to in-memory
var connectionString = builder.Configuration["connectionString"]
    ?? builder.Configuration.GetConnectionString("DefaultConnection");
var dbProvider = builder.Configuration["databaseProvider"]
    ?? builder.Configuration["database:provider"];

if (!string.IsNullOrEmpty(connectionString) && !string.IsNullOrEmpty(dbProvider))
{
    builder.Services.AddDbContext<FilesDbContext>(options =>
    {
        if (string.Equals(dbProvider, "PostgreSql", StringComparison.OrdinalIgnoreCase))
            options.UseNpgsql(connectionString);
        else
            options.UseSqlServer(connectionString);
    });
}
else
{
    builder.Services.AddDbContext<FilesDbContext>(options =>
        options.UseInMemoryDatabase("FilesModule"));
}

// Files module business logic services
builder.Services.AddFilesServices(builder.Configuration);

// File storage engine (local filesystem, configurable base path)
var storagePath = builder.Configuration.GetValue<string>("Files:StoragePath");
if (string.IsNullOrWhiteSpace(storagePath))
{
    var dataDir = Environment.GetEnvironmentVariable("DOTNETCLOUD_DATA_DIR");
    storagePath = !string.IsNullOrWhiteSpace(dataDir)
        ? Path.Combine(dataDir, "storage")
        : Path.Combine(builder.Environment.ContentRootPath, "storage");
}
builder.Services.AddSingleton<IFileStorageEngine>(sp =>
    new LocalFileStorageEngine(storagePath, sp.GetRequiredService<ILogger<LocalFileStorageEngine>>()));

// In-process event bus for standalone operation
builder.Services.AddSingleton<IEventBus, InProcessEventBus>();

// Search FTS client for full-text search via Search module gRPC
builder.Services.AddSearchFtsClient(builder.Configuration);

// gRPC
builder.Services.AddGrpc();

// REST API controllers
builder.Services.AddControllers(options =>
{
    options.Filters.Add<DotNetCloud.Modules.Files.Filters.DeviceIdentityFilter>();
});

// Health checks
builder.Services.AddHealthChecks()
    .AddCheck<FilesHealthCheck>("files_module");

// OpenAPI document generation
builder.Services.AddOpenApi("v1", options =>
{
    options.AddDocumentTransformer((document, context, ct) =>
    {
        document.Info.Title = "DotNetCloud Files API";
        document.Info.Version = "1.0.0";
        document.Info.Description = "Files module REST API — upload, download, share, version, and manage files and folders.";
        return Task.CompletedTask;
    });
});

var app = builder.Build();

// --- Middleware ---

// Map gRPC services
app.MapGrpcService<FilesGrpcService>();
app.MapGrpcService<FilesLifecycleService>();

// Map REST API controllers
app.MapControllers();

// Health check endpoint
app.MapHealthChecks("/health");

// OpenAPI + Scalar interactive docs (development only)
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.MapScalarApiReference(options =>
    {
        options
            .WithTitle("DotNetCloud Files API Documentation")
            .WithDefaultHttpClient(ScalarTarget.CSharp, ScalarClient.HttpClient);
    });
}

// Minimal info endpoint
app.MapGet("/", () => Results.Ok(new
{
    module = "dotnetcloud.files",
    version = "1.0.0",
    status = "running"
}));

app.Run();

/// <summary>
/// Public Program type for integration testing via <c>WebApplicationFactory&lt;Program&gt;</c>.
/// </summary>
public partial class Program;
