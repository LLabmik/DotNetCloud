using DotNetCloud.Modules.Example;
using DotNetCloud.Modules.Example.Data;
using DotNetCloud.Modules.Example.Host.Services;
using Microsoft.EntityFrameworkCore;

public static partial class Program
{
    public static void Main(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);

        // --- Services ---

        // Register the Example module as a singleton for lifecycle management
        builder.Services.AddSingleton<ExampleModule>();

        // Register EF Core with an in-memory database for the example
        // In production, modules would use the database configured by the core server
        builder.Services.AddDbContext<ExampleDbContext>(options =>
            options.UseInMemoryDatabase("ExampleModule"));

        // gRPC
        builder.Services.AddGrpc();

        // Health checks
        builder.Services.AddHealthChecks()
            .AddCheck<ExampleHealthCheck>("example_module");

        var app = builder.Build();

        // --- Middleware ---

        // Map gRPC services
        app.MapGrpcService<ExampleGrpcService>();
        app.MapGrpcService<ExampleLifecycleService>();

        // Health check endpoint
        app.MapHealthChecks("/health");

        // Minimal info endpoint
        app.MapGet("/", () => Results.Ok(new
        {
            module = "dotnetcloud.example",
            version = "1.0.0",
            status = "running"
        }));

        app.Run();
    }
}
