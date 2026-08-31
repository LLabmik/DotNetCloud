using DotNetCloud.Core.Data.Context;
using DotNetCloud.Core.Data.Extensions;
using DotNetCloud.Core.Data.Naming;
using DotNetCloud.Core.Modules;
using DotNetCloud.Core.Server.Services;
using DotNetCloud.Modules.AI.Data;
using DotNetCloud.Modules.Bookmarks.Data;
using DotNetCloud.Modules.Calendar.Data;
using DotNetCloud.Modules.Chat.Data;
using DotNetCloud.Modules.Contacts.Data;
using DotNetCloud.Modules.Email.Data;
using DotNetCloud.Modules.Files.Data;
using DotNetCloud.Modules.Music.Data;
using DotNetCloud.Modules.Notes.Data;
using DotNetCloud.Modules.Photos.Data;
using DotNetCloud.Modules.Tracks.Data;
using DotNetCloud.Modules.Video.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Migrations;
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
        services.AddModuleDbContexts(provider, config.ConnectionString);
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
        services.AddModuleDbContexts(provider, connectionString);
        services.AddSingleton<IModuleSchemaProvider, DbContextSchemaProvider>();

        return services.BuildServiceProvider();
    }

    private static void ConfigureModuleDbContext(DbContextOptionsBuilder options, DatabaseProvider provider,
        string connectionString, string? migrationsAssembly = null)
    {
        DbResiliencePolicy.Configure(options, provider, connectionString, migrationsAssembly);
    }

    private static IServiceCollection AddModuleDbContexts(
        this IServiceCollection services, DatabaseProvider provider, string connectionString)
    {
        // Register naming strategy based on provider
        services.AddSingleton<ITableNamingStrategy>(provider == DatabaseProvider.SqlServer
            ? new SqlServerNamingStrategy()
            : new PostgreSqlNamingStrategy());

        const string AiMigrationsAssembly = "DotNetCloud.Modules.AI.Data.SqlServer";
        const string BookmarksMigrationsAssembly = "DotNetCloud.Modules.Bookmarks.Data.SqlServer";
        const string CalendarMigrationsAssembly = "DotNetCloud.Modules.Calendar.Data.SqlServer";
        const string ChatMigrationsAssembly = "DotNetCloud.Modules.Chat.Data.SqlServer";
        const string ContactsMigrationsAssembly = "DotNetCloud.Modules.Contacts.Data.SqlServer";
        const string EmailMigrationsAssembly = "DotNetCloud.Modules.Email.Data.SqlServer";
        const string FilesMigrationsAssembly = "DotNetCloud.Modules.Files.Data.SqlServer";
        const string MusicMigrationsAssembly = "DotNetCloud.Modules.Music.Data.SqlServer";
        const string NotesMigrationsAssembly = "DotNetCloud.Modules.Notes.Data.SqlServer";
        const string PhotosMigrationsAssembly = "DotNetCloud.Modules.Photos.Data.SqlServer";
        const string TracksMigrationsAssembly = "DotNetCloud.Modules.Tracks.Data.SqlServer";
        const string VideoMigrationsAssembly = "DotNetCloud.Modules.Video.Data.SqlServer";

        services.AddDbContext<AiDbContext>(options =>
            ConfigureModuleDbContext(options, provider, connectionString, AiMigrationsAssembly));
        services.AddDbContext<BookmarksDbContext>(options =>
            ConfigureModuleDbContext(options, provider, connectionString, BookmarksMigrationsAssembly));
        services.AddDbContext<CalendarDbContext>(options =>
            ConfigureModuleDbContext(options, provider, connectionString, CalendarMigrationsAssembly));
        services.AddDbContext<ChatDbContext>(options =>
            ConfigureModuleDbContext(options, provider, connectionString, ChatMigrationsAssembly));
        services.AddDbContext<ContactsDbContext>(options =>
            ConfigureModuleDbContext(options, provider, connectionString, ContactsMigrationsAssembly));
        services.AddDbContext<EmailDbContext>(options =>
            ConfigureModuleDbContext(options, provider, connectionString, EmailMigrationsAssembly));
        services.AddDbContext<FilesDbContext>(options =>
            ConfigureModuleDbContext(options, provider, connectionString, FilesMigrationsAssembly));
        services.AddDbContextFactory<MusicDbContext>(options =>
            ConfigureModuleDbContext(options, provider, connectionString, MusicMigrationsAssembly));
        services.AddDbContext<MusicDbContext>(options =>
            ConfigureModuleDbContext(options, provider, connectionString, MusicMigrationsAssembly));
        services.AddDbContext<NotesDbContext>(options =>
            ConfigureModuleDbContext(options, provider, connectionString, NotesMigrationsAssembly));
        services.AddDbContext<PhotosDbContext>(options =>
            ConfigureModuleDbContext(options, provider, connectionString, PhotosMigrationsAssembly));
        services.AddDbContext<TracksDbContext>(options =>
            ConfigureModuleDbContext(options, provider, connectionString, TracksMigrationsAssembly));
        services.AddDbContext<VideoDbContext>(options =>
            ConfigureModuleDbContext(options, provider, connectionString, VideoMigrationsAssembly));

        return services;
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
