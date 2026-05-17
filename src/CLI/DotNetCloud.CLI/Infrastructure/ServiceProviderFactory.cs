using DotNetCloud.Core.Data.Context;
using DotNetCloud.Core.Data.Extensions;
using DotNetCloud.Core.Modules;
using DotNetCloud.Core.Schema.Services;
using DotNetCloud.Core.Data.Naming;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace DotNetCloud.CLI.Infrastructure;

/// <summary>
/// Builds a minimal <see cref="IServiceProvider"/> for CLI commands that need
/// database access or core services without starting the full web host.
/// </summary>
internal static class ServiceProviderFactory
{
    /// <summary>
    /// Creates a service provider configured with the database context
    /// loaded from the CLI configuration file.
    /// </summary>
    /// <returns>
    /// A configured <see cref="ServiceProvider"/>, or <see langword="null"/>
    /// if no configuration exists.
    /// </returns>
    public static ServiceProvider? CreateFromConfig()
    {
        if (!CliConfiguration.ConfigExists())
        {
            ConsoleOutput.WriteError("DotNetCloud is not configured. Run 'dotnetcloud setup' first.");
            return null;
        }

        var config = CliConfiguration.Load();
        if (string.IsNullOrWhiteSpace(config.ConnectionString))
        {
            ConsoleOutput.WriteError("No database connection string configured. Run 'dotnetcloud setup' first.");
            return null;
        }

        if (!CliConfiguration.TryResolveDatabaseProvider(config.Database.Provider, out var provider) &&
            !CliConfiguration.TryResolveDatabaseProvider(config.DatabaseProvider, out provider))
        {
            ConsoleOutput.WriteError(
                "No valid database provider configured. Run 'dotnetcloud setup' to set Database:Provider.");
            return null;
        }

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddDotNetCloudDbContext(config.ConnectionString, provider);
        services.AddSingleton<IModuleSchemaProvider, DbContextSchemaProvider>();

        return services.BuildServiceProvider();
    }

    /// <summary>
    /// Creates a service provider from an explicit connection string and provider (used during setup).
    /// </summary>
    public static ServiceProvider? CreateFromConnectionString(string connectionString, DatabaseProvider provider)
    {
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            return null;
        }

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddDotNetCloudDbContext(connectionString, provider);
        services.AddSingleton<IModuleSchemaProvider, DbContextSchemaProvider>();

        return services.BuildServiceProvider();
    }

    /// <summary>
    /// Creates a <see cref="CoreDbContext"/> from the CLI configuration.
    /// </summary>
    public static CoreDbContext? CreateDbContext()
    {
        var provider = CreateFromConfig();
        return provider?.GetService<CoreDbContext>();
    }
}
