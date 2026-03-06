using DotNetCloud.Core.Events;
using DotNetCloud.Modules.Files;
using DotNetCloud.Modules.Files.Data;
using DotNetCloud.Modules.Files.Host.Services;
using DotNetCloud.Modules.Files.Services;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// --- Services ---

// Register the Files module as a singleton for lifecycle management
builder.Services.AddSingleton<FilesModule>();

// Register EF Core with an in-memory database for development
// In production, modules use the database configured by the core server
builder.Services.AddDbContext<FilesDbContext>(options =>
    options.UseInMemoryDatabase("FilesModule"));

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

// gRPC
builder.Services.AddGrpc();

// REST API controllers
builder.Services.AddControllers();

// Health checks
builder.Services.AddHealthChecks()
    .AddCheck<FilesHealthCheck>("files_module");

var app = builder.Build();

// --- Middleware ---

// Map gRPC services
app.MapGrpcService<FilesGrpcService>();
app.MapGrpcService<FilesLifecycleService>();

// Map REST API controllers
app.MapControllers();

// Health check endpoint
app.MapHealthChecks("/health");

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
