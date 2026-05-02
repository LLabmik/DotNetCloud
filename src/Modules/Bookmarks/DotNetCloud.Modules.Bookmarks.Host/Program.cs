using DotNetCloud.Core.Events;
using DotNetCloud.Modules.Bookmarks;
using DotNetCloud.Modules.Bookmarks.Data;
using DotNetCloud.Modules.Bookmarks.Host.Services;
using DotNetCloud.Modules.Search.Client;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// Register the module as singleton
builder.Services.AddSingleton<BookmarksModule>();

// Register EF Core with in-memory database (dev only)
builder.Services.AddDbContext<BookmarksDbContext>(options =>
    options.UseInMemoryDatabase("BookmarksModule"));

// In-process event bus for standalone operation
builder.Services.AddSingleton<IEventBus, InProcessEventBus>();

// Search FTS client for full-text search via Search module gRPC
builder.Services.AddSearchFtsClient(builder.Configuration);

// Register all business-logic services
builder.Services.AddBookmarksServices(builder.Configuration);

// gRPC
builder.Services.AddGrpc();
builder.Services.AddControllers();
builder.Services.AddHealthChecks()
    .AddCheck<BookmarksHealthCheck>("bookmarks_module");

var app = builder.Build();

// Map gRPC services
app.MapGrpcService<BookmarksGrpcService>();
app.MapGrpcService<BookmarksLifecycleService>();

// Map REST API controllers
app.MapControllers();

// Health check endpoint
app.MapHealthChecks("/health");

// Minimal info endpoint
app.MapGet("/", () => Results.Ok(new
{
    module = "dotnetcloud.bookmarks",
    version = "1.0.0",
    status = "running"
}));

app.Run();

/// <summary>Marker class for integration test host reference.</summary>
public partial class Program;
