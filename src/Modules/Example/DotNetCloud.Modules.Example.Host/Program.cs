using DotNetCloud.Core.Data.Naming;
using DotNetCloud.Modules.Example;
using DotNetCloud.Modules.Example.Data;
using DotNetCloud.Modules.Example.Host.Services;
using Microsoft.EntityFrameworkCore;

public static partial class Program
{
    public static async Task Main(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);

        // Register the Example module as a singleton for lifecycle management
        builder.Services.AddSingleton<ExampleModule>();

        // Register ITableNamingStrategy (required by ExampleDbContext)
        builder.Services.AddSingleton<ITableNamingStrategy, PostgreSqlNamingStrategy>();

        // Get connection string from environment (set by core server) or config
        var connectionString = Environment.GetEnvironmentVariable("DOTNETCLOUD_CONNECTION_STRING")
            ?? builder.Configuration.GetConnectionString("DefaultConnection");

        // Register EF Core — use in-memory for development, real DB when connection string is provided
        if (string.IsNullOrEmpty(connectionString))
        {
            builder.Services.AddDbContext<ExampleDbContext>(options =>
                options.UseInMemoryDatabase("ExampleModule"));
        }
        else
        {
            builder.Services.AddDbContext<ExampleDbContext>(options =>
                options.UseNpgsql(connectionString, npgsql =>
                    npgsql.MigrationsHistoryTable("__EFMigrationsHistory", "example")));
        }

        // gRPC + health checks
        builder.Services.AddGrpc();
        builder.Services.AddHealthChecks().AddCheck<ExampleHealthCheck>("example_module");

        var app = builder.Build();

        // Self-migrate on startup when using a real database
        if (!string.IsNullOrEmpty(connectionString))
        {
            using var scope = app.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<ExampleDbContext>();
            await db.Database.MigrateAsync();
        }

        app.MapGrpcService<ExampleGrpcService>();
        app.MapGrpcService<ExampleLifecycleService>();
        app.MapHealthChecks("/health");
        app.MapGet("/", () => Results.Ok(new { module = "dotnetcloud.example", version = "1.0.0", status = "running" }));

        await app.RunAsync();
    }
}
