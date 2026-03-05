using DotNetCloud.Client.SyncService;
using Microsoft.Extensions.Hosting;

var builder = Host.CreateApplicationBuilder(args);

// Register as a Windows Service (no-op on Linux)
builder.Services.AddWindowsService(options =>
{
    options.ServiceName = "DotNetCloud Sync Service";
});

// Register systemd integration (no-op on Windows)
builder.Services.AddSystemd();

builder.Services.AddDotNetCloudSyncService();

var app = builder.Build();
await app.RunAsync();
