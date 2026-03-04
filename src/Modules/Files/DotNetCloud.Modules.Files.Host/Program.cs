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
builder.Services.AddFilesServices();

// File storage engine (local filesystem, configurable base path)
var storagePath = builder.Configuration.GetValue<string>("Files:StoragePath")
    ?? Path.Combine(builder.Environment.ContentRootPath, "storage");
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
