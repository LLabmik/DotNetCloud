using DotNetCloud.Core.Data.Naming;
using DotNetCloud.Modules.Example;
using DotNetCloud.Modules.Example.Data;
using DotNetCloud.Modules.Example.Host.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

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
                    sql.MigrationsAssembly(typeof(ExampleDbContext).Assembly.FullName);
                    sql.MigrationsHistoryTable("__EFMigrationsHistory", "example");
                });
            }

            options.ReplaceService<IMigrationsAssembly, DotNetCloud.Core.Data.Infrastructure.ProviderAwareMigrationsAssembly>();
        });

        // gRPC + health checks
        builder.Services.AddGrpc();
        builder.Services.AddHealthChecks().AddCheck<ExampleHealthCheck>("example_module");

        var app = builder.Build();

        // Self-migrate on startup (schemaProvider: "self").
        // A connection string is now mandatory, so this is unconditional.
        using (var scope = app.Services.CreateScope())
        {
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
