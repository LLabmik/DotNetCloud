using DotNetCloud.Core.Data.Naming;
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
using DotNetCloud.Modules.Search.Data;
using DotNetCloud.Modules.Tracks.Data;
using DotNetCloud.Modules.Video.Data;
using Microsoft.EntityFrameworkCore;

namespace DotNetCloud.Core.Server.Extensions;

/// <summary>
/// Extension methods for registering module DbContexts and services.
/// </summary>
internal static class ModuleServiceRegistrationExtensions
{
    /// <summary>
    /// Registers all module-specific DbContexts with the same database provider and connection string.
    /// </summary>
    public static IServiceCollection AddModuleDbContexts(
        this IServiceCollection services,
        DatabaseProvider provider,
        string connectionString)
    {
        ArgumentNullException.ThrowIfNull(services);

        void Configure(DbContextOptionsBuilder options) =>
            ConfigureModuleDbContext(options, provider, connectionString);

        services.AddDbContext<FilesDbContext>(Configure);
        services.AddDbContext<ChatDbContext>(Configure);
        services.AddDbContext<ContactsDbContext>(Configure);
        services.AddDbContext<CalendarDbContext>(Configure);
        services.AddDbContext<NotesDbContext>(Configure);
        services.AddDbContext<TracksDbContext>(Configure);
        services.AddDbContext<PhotosDbContext>(Configure);
        services.AddDbContext<MusicDbContext>(Configure);
        services.AddDbContext<VideoDbContext>(Configure);
        services.AddDbContext<AiDbContext>(Configure);
        services.AddDbContext<SearchDbContext>(Configure);
        services.AddDbContext<BookmarksDbContext>(Configure);
        services.AddDbContext<EmailDbContext>(Configure);

        return services;
    }

    /// <summary>
    /// Configures a module DbContext with the appropriate database provider settings.
    /// </summary>
    private static void ConfigureModuleDbContext(
        DbContextOptionsBuilder options,
        DatabaseProvider provider,
        string connectionString)
    {
        switch (provider)
        {
            case DatabaseProvider.PostgreSQL:
                options.UseNpgsql(connectionString, npgsqlOptions =>
                {
                    npgsqlOptions.EnableRetryOnFailure(maxRetryCount: 3);
                    npgsqlOptions.CommandTimeout(30);
                });
                break;

            case DatabaseProvider.SqlServer:
                options.UseSqlServer(connectionString, sqlServerOptions =>
                {
                    sqlServerOptions.EnableRetryOnFailure(maxRetryCount: 3);
                    sqlServerOptions.CommandTimeout(30);
                });
                break;

            case DatabaseProvider.MariaDB:
                throw new NotSupportedException(
                    "MariaDB support is temporarily disabled pending a Pomelo.EntityFrameworkCore.MySql " +
                    ".NET 10 compatible release. Use PostgreSQL or SQL Server instead.");

            default:
                throw new ArgumentException($"Unsupported database provider: {provider}", nameof(provider));
        }
    }
}
