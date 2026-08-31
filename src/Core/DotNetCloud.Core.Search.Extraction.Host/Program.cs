using DotNetCloud.Core.Capabilities;
using DotNetCloud.Core.Search.Extraction;
using DotNetCloud.Core.Search.Extraction.Extractors;
using DotNetCloud.Core.Search.Extraction.Host.Services;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using IContentExtractor = DotNetCloud.Core.Capabilities.IContentExtractor;

var builder = WebApplication.CreateBuilder(args);

// Bind gRPC endpoint from DOTNETCLOUD_GRPC_ENDPOINT (set by ProcessSupervisor).
// Mirrors the old Search module host: the supervisor passes a TCP loopback endpoint
// (or a unix/named-pipe endpoint translated to HTTP here).
var grpcEndpoint = Environment.GetEnvironmentVariable("DOTNETCLOUD_GRPC_ENDPOINT");
if (!string.IsNullOrEmpty(grpcEndpoint))
{
    var uri = new Uri(grpcEndpoint.Replace("unix://", "http://").Replace("net.pipe://", "http://"));
    builder.WebHost.ConfigureKestrel(o =>
        o.Listen(System.Net.IPAddress.Loopback, uri.Port, l => l.Protocols = HttpProtocols.Http2));
}

// --- Services ---

// Content extraction service + all extractors (parser libraries live only in this process)
builder.Services.AddSingleton<ContentExtractionService>();
builder.Services.AddSingleton<IContentExtractor, PlainTextExtractor>();
builder.Services.AddSingleton<IContentExtractor, MarkdownContentExtractor>();
builder.Services.AddSingleton<IContentExtractor, HtmlContentExtractor>();
builder.Services.AddSingleton<IContentExtractor, RtfContentExtractor>();
builder.Services.AddSingleton<IContentExtractor, PdfContentExtractor>();
builder.Services.AddSingleton<IContentExtractor, DocxContentExtractor>();
builder.Services.AddSingleton<IContentExtractor, XlsxContentExtractor>();
builder.Services.AddSingleton<IContentExtractor, PptxContentExtractor>();
builder.Services.AddSingleton<IContentExtractor, OdfContentExtractor>();
builder.Services.AddSingleton<IContentExtractor, XlsContentExtractor>();

// gRPC
builder.Services.AddGrpc();

// Health checks
builder.Services.AddHealthChecks()
    .AddCheck<ExtractionHealthCheck>("extraction_worker");

var app = builder.Build();

// Map gRPC services
app.MapGrpcService<ExtractionGrpcService>();
app.MapGrpcService<ExtractionLifecycleService>();

// Health check endpoint
app.MapHealthChecks("/health");

// Minimal info endpoint
app.MapGet("/", () => Results.Ok(new
{
    module = "dotnetcloud.extraction",
    version = "1.0.0",
    status = "running"
}));

app.Run();

/// <summary>Entry point marker for WebApplicationFactory in integration tests.</summary>
public partial class Program;
