using DotNetCloud.Core.Data.Naming;
using DotNetCloud.Core.Grpc;
using DotNetCloud.Modules.Example;
using DotNetCloud.Modules.Example.Data;
using DotNetCloud.Modules.Example.Host.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.Logging;

public static partial class Program
{
    public static async Task Main(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);

        // Load shared config from DOTNETCLOUD_CONFIG_DIR (set by ProcessSupervisor)
        var configDir = Environment.GetEnvironmentVariable("DOTNETCLOUD_CONFIG_DIR");
        if (!string.IsNullOrEmpty(configDir))
        {
            var configPath = Path.Combine(configDir, "config.json");
            if (File.Exists(configPath))
                builder.Configuration.AddJsonFile(configPath, optional: true, reloadOnChange: false);
        }

        // Register the Example module as a singleton for lifecycle management
        builder.Services.AddSingleton<ExampleModule>();

        // Register EF Core with the configured database provider (no in-memory fallback)
        var connectionString = builder.Configuration["connectionString"]
            ?? builder.Configuration.GetConnectionString("DefaultConnection");
        var dbProvider = builder.Configuration["databaseProvider"]
            ?? builder.Configuration["database:provider"];

        if (string.IsNullOrEmpty(connectionString) || string.IsNullOrEmpty(dbProvider))
        {
            throw new InvalidOperationException(
                "The Example module requires a database connection string and provider. " +
                "These are provided via config.json (DOTNETCLOUD_CONFIG_DIR) when launched by the core server.");
        }

        var isPostgreSql = string.Equals(dbProvider, "PostgreSql", StringComparison.OrdinalIgnoreCase);

        builder.Services.AddSingleton<ITableNamingStrategy>(isPostgreSql
            ? new PostgreSqlNamingStrategy()
            : new SqlServerNamingStrategy());

        builder.Services.AddDbContext<ExampleDbContext>(options =>
        {
            if (isPostgreSql)
            {
                options.UseNpgsql(connectionString, npgsql =>
                    npgsql.MigrationsHistoryTable("__EFMigrationsHistory", "example"));
            }
            else
            {
                options.UseSqlServer(connectionString, sql =>
                {
                    sql.EnableRetryOnFailure(maxRetryCount: 3);
                    sql.CommandTimeout(30);
                    sql.MigrationsAssembly("DotNetCloud.Modules.Example.Data.SqlServer");
                    sql.MigrationsHistoryTable("__EFMigrationsHistory", "example");
                });
            }
        });

        // Register the gRPC-backed audit logger (SOC 2 CC4) — routes to Core.Server.
        builder.Services.AddAuditLogger();

        // gRPC + health checks
        builder.Services.AddGrpc();
        builder.Services.AddHealthChecks().AddCheck<ExampleHealthCheck>("example_module");

        var app = builder.Build();

        // Self-migrate on startup (schemaProvider: "self").
        // A connection string is now mandatory, so this is unconditional.
        // Retries with exponential backoff so a database that is briefly unavailable
        // at startup does not crash the module — it continues running in degraded mode.
        {
            const int maxAttempts = 5;
            var delay = TimeSpan.FromSeconds(2);
            var initLogger = app.Services.GetRequiredService<ILoggerFactory>().CreateLogger("ExampleDbInit");

            for (var attempt = 1; attempt <= maxAttempts; attempt++)
            {
                try
                {
                    using var scope = app.Services.CreateScope();
                    var db = scope.ServiceProvider.GetRequiredService<ExampleDbContext>();
                    await db.Database.MigrateAsync();
                    break;
                }
                catch (Exception ex) when (attempt < maxAttempts)
                {
                    initLogger.LogWarning(ex,
                        "Example database migration attempt {Attempt}/{MaxAttempts} failed. Retrying in {Delay}s...",
                        attempt, maxAttempts, delay.TotalSeconds);
                    await Task.Delay(delay);
                    delay *= 2;
                }
                catch (Exception ex)
                {
                    initLogger.LogError(ex,
                        "Example database migration failed after {MaxAttempts} attempts. " +
                        "Continuing in degraded mode; retries will resume when the database recovers.",
                        maxAttempts);
                }
            }
        }

        app.MapGrpcService<ExampleGrpcService>();
        app.MapGrpcService<ExampleLifecycleService>();
        app.MapHealthChecks("/health");
        app.MapGet("/", () => Results.Ok(new { module = "dotnetcloud.example", version = "1.0.0", status = "running" }));

        await app.RunAsync();
    }
}
