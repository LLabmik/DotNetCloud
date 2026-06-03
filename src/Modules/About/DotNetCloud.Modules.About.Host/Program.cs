using DotNetCloud.Modules.About;
using DotNetCloud.Modules.About.Host.Services;
using Microsoft.AspNetCore.Server.Kestrel.Core;

public static partial class Program
{
    public static async Task Main(string[] args)
    {
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

        builder.Services.AddSingleton<AboutModule>();

        builder.Services.AddGrpc();
        builder.Services.AddHealthChecks().AddCheck<AboutHealthCheck>("about_module");

        var app = builder.Build();

        app.MapGrpcService<AboutGrpcService>();
        app.MapGrpcService<AboutLifecycleService>();
        app.MapHealthChecks("/health");
        app.MapGet("/", () => Results.Ok(new { module = "dotnetcloud.about", version = "1.0.0", status = "running" }));

        await app.RunAsync();
    }
}
