using DotNetCloud.Core.Data.Naming;
using DotNetCloud.Core.Grpc;
using DotNetCloud.Modules.Example;
using DotNetCloud.Modules.Example.Data;
using DotNetCloud.Modules.Example.Host.Services;
using DotNetCloud.Modules.Sdk;
using DotNetCloud.Modules.Sdk.Api;
using DotNetCloud.Modules.Sdk.Testing;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.Logging;

public static partial class Program
{
    public static async Task Main(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);

        // Bind the gRPC endpoint the supervisor assigned. Without this the host was not
        // addressable by ProcessSupervisor at all.
        var grpcEndpoint = Environment.GetEnvironmentVariable("DOTNETCLOUD_GRPC_ENDPOINT");
        if (!string.IsNullOrEmpty(grpcEndpoint))
        {
            var uri = new Uri(grpcEndpoint.Replace("unix://", "http://").Replace("net.pipe://", "http://"));
            builder.WebHost.ConfigureKestrel(o =>
                o.Listen(System.Net.IPAddress.Loopback, uri.Port, l => l.Protocols = HttpProtocols.Http2));
        }

        // DataProtection keys are private to the module. Sandboxed hosts persist them under their
        // own data directory; never the core key ring.
        var moduleDataDir = Environment.GetEnvironmentVariable("DOTNETCLOUD_MODULE_DATA_DIR");
        var dataProtectionDir = !string.IsNullOrWhiteSpace(moduleDataDir)
            ? Path.Combine(moduleDataDir, "data-protection-keys")
            : Path.Combine(builder.Environment.ContentRootPath, "data-protection-keys");
        Directory.CreateDirectory(dataProtectionDir);
        builder.Services.AddDataProtection()
            .SetApplicationName("DotNetCloud.Modules.Example")
            .PersistKeysToFileSystem(new DirectoryInfo(dataProtectionDir));

        // Sandboxed modules receive only non-secret topology in module.json. Legacy hosts keep
        // loading the shared config.json until they are migrated to the sandboxed model.
        var moduleConfigDir = Environment.GetEnvironmentVariable("DOTNETCLOUD_MODULE_CONFIG_DIR");
        if (!string.IsNullOrEmpty(moduleConfigDir))
        {
            var moduleJsonPath = Path.Combine(moduleConfigDir, "module.json");
            if (File.Exists(moduleJsonPath))
                builder.Configuration.AddJsonFile(moduleJsonPath, optional: false, reloadOnChange: false);
        }

        // Load shared config from DOTNETCLOUD_CONFIG_DIR (set by ProcessSupervisor for legacy hosts)
        var configDir = Environment.GetEnvironmentVariable("DOTNETCLOUD_CONFIG_DIR");
        if (!string.IsNullOrEmpty(configDir))
        {
            var configPath = Path.Combine(configDir, "config.json");
            if (File.Exists(configPath))
                builder.Configuration.AddJsonFile(configPath, optional: true, reloadOnChange: false);
        }

        // Register the Example module as a singleton for lifecycle management
        builder.Services.AddSingleton<ExampleModule>();

        // Register EF Core with the configured database provider (no in-memory fallback).
        // Sandboxed hosts get a scoped connection string from the supervisor and the engine name
        // from module.json; legacy hosts read both from config.json. ModuleEnvironment is the SDK
        // contract for the supervisor-provided values.
        ModuleEnvironment.TryFromEnvironment(out var moduleEnvironment);

        var sandboxConnectionString = moduleEnvironment?.DatabaseConnectionString
            ?? Environment.GetEnvironmentVariable("DOTNETCLOUD_MODULE_DB_CONNECTION_STRING");
        var connectionString = !string.IsNullOrEmpty(sandboxConnectionString)
            ? sandboxConnectionString
            : builder.Configuration["connectionString"]
                ?? builder.Configuration.GetConnectionString("DefaultConnection");
        var dbProvider = builder.Configuration["databaseProvider"]
            ?? builder.Configuration["database:provider"];

        if (string.IsNullOrEmpty(connectionString) || string.IsNullOrEmpty(dbProvider))
        {
            throw new InvalidOperationException(
                "The Example module requires a database connection string and provider. " +
                "Sandboxed hosts receive them from ProcessSupervisor; legacy hosts read them from " +
                "config.json (DOTNETCLOUD_CONFIG_DIR).");
        }

        var isPostgreSql = string.Equals(dbProvider, "PostgreSql", StringComparison.OrdinalIgnoreCase);

        // Sandboxed modules own a generated schema; the host must use it for both the model's
        // default schema and the migration-history table.
        var sandboxSchema = builder.Configuration["databaseSchema"];
        var migrationsSchema = string.IsNullOrWhiteSpace(sandboxSchema) ? "example" : sandboxSchema;

        ITableNamingStrategy namingStrategy = isPostgreSql
            ? new PostgreSqlNamingStrategy()
            : new SqlServerNamingStrategy();

        if (!string.IsNullOrWhiteSpace(sandboxSchema))
        {
            namingStrategy = new SandboxTableNamingStrategy(namingStrategy, "example", sandboxSchema);
        }

        builder.Services.AddSingleton(namingStrategy);

        builder.Services.AddDbContext<ExampleDbContext>(options =>
        {
            if (isPostgreSql)
            {
                options.UseNpgsql(connectionString, npgsql =>
                    npgsql.MigrationsHistoryTable("__EFMigrationsHistory", migrationsSchema));
            }
            else
            {
                options.UseSqlServer(connectionString, sql =>
                {
                    sql.EnableRetryOnFailure(maxRetryCount: 3);
                    sql.CommandTimeout(30);
                    sql.MigrationsAssembly("DotNetCloud.Modules.Example.Data.SqlServer");
                    sql.MigrationsHistoryTable("__EFMigrationsHistory", migrationsSchema);
                });
            }
        });

        // Core API access goes through the SDK: one client, one credential, the canonical
        // module_api surface. When the host was not launched by the supervisor (local development)
        // the in-memory harness keeps the module runnable without a core process.
        if (moduleEnvironment is not null)
        {
            builder.Services.AddSingleton(moduleEnvironment);
            builder.Services.AddSingleton<IDncModuleApi>(_ => new DncModuleApiClient(moduleEnvironment));
        }
        else
        {
            builder.Services.AddSingleton<IDncModuleApi, InMemoryDncModuleApi>();
        }

        // gRPC + health checks
        builder.Services.AddGrpc(options =>
            options.Interceptors.Add<DotNetCloud.Core.Grpc.ModulePeerAuthenticationInterceptor>());
        builder.Services.AddHealthChecks().AddCheck<ExampleHealthCheck>("example_module");

        var app = builder.Build();

        // Adopt the core-signed end-user assertion before anything can call back into the core.
        app.UseModuleUserAssertion();

        // Self-migrate on startup (dataStore: "isolated" means the module owns its schema).
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
