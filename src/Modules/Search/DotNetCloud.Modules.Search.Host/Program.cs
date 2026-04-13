using DotNetCloud.Core.Events;
using DotNetCloud.Modules.Search;
using DotNetCloud.Modules.Search.Data;
using DotNetCloud.Modules.Search.Extractors;
using DotNetCloud.Modules.Search.Host.Services;
using DotNetCloud.Modules.Search.Services;
using Microsoft.EntityFrameworkCore;
using IContentExtractor = DotNetCloud.Core.Capabilities.IContentExtractor;
using ISearchProvider = DotNetCloud.Core.Capabilities.ISearchProvider;

var builder = WebApplication.CreateBuilder(args);

// --- Services ---

// Register the Search module as a singleton for lifecycle management
builder.Services.AddSingleton<SearchModule>();

// Register EF Core with an in-memory database for development
// In production, modules use the database configured by the core server
builder.Services.AddDbContext<SearchDbContext>(options =>
    options.UseInMemoryDatabase("SearchModule"));

// In-process event bus for standalone operation
builder.Services.AddSingleton<IEventBus, InProcessEventBus>();

// Search provider — PostgreSQL as default; auto-selected based on DB config in production
builder.Services.AddScoped<ISearchProvider, PostgreSqlSearchProvider>();

// Content extractors
builder.Services.AddSingleton<IContentExtractor, PlainTextExtractor>();
builder.Services.AddSingleton<IContentExtractor, MarkdownContentExtractor>();
builder.Services.AddSingleton<IContentExtractor, PdfContentExtractor>();
builder.Services.AddSingleton<IContentExtractor, DocxContentExtractor>();
builder.Services.AddSingleton<IContentExtractor, XlsxContentExtractor>();

// Search services
builder.Services.AddScoped<SearchQueryService>();
builder.Services.AddScoped<ContentExtractionService>();
builder.Services.AddSingleton<SearchIndexingService>();

// Background reindex service (registered as singleton to allow controller injection)
builder.Services.AddSingleton<SearchReindexBackgroundService>();
builder.Services.AddHostedService(sp => sp.GetRequiredService<SearchReindexBackgroundService>());

// gRPC
builder.Services.AddGrpc();

// REST API controllers
builder.Services.AddControllers();

// Health checks
builder.Services.AddHealthChecks();

var app = builder.Build();

// --- Middleware ---

// Map gRPC services
app.MapGrpcService<SearchGrpcService>();

// Map REST API controllers
app.MapControllers();

// Health check endpoint
app.MapHealthChecks("/health");

// Minimal info endpoint
app.MapGet("/", () => Results.Ok(new
{
    module = "dotnetcloud.search",
    version = "1.0.0",
    status = "running"
}));

app.Run();

/// <summary>Entry point marker for WebApplicationFactory in integration tests.</summary>
public partial class Program;
