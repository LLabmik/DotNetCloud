using DotNetCloud.Core.Data.Naming;
using DotNetCloud.Modules.AI.Data;
using DotNetCloud.Modules.Bookmarks.Data;
using DotNetCloud.Modules.Calendar.Data;
using DotNetCloud.Modules.Chat.Data;
using DotNetCloud.Modules.Contacts.Data;
using DotNetCloud.Modules.Email.Data;
// Files.Data reference removed — managed by the Files module host process
using DotNetCloud.Modules.Music.Data;
using DotNetCloud.Modules.Notes.Data;
using DotNetCloud.Modules.Photos.Data;
using DotNetCloud.Modules.Tracks.Data;
using DotNetCloud.Modules.Video.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace DotNetCloud.Core.Server.Extensions;

/// <summary>
/// Extension methods for registering module DbContexts and services.
/// </summary>
internal static class ModuleServiceRegistrationExtensions
{
    /// <summary>
    /// Registers all module-specific DbContexts with the same database provider and connection string.
    /// </summary>
    /// <summary>
    /// Registers all module-specific DbContexts with the same database provider and connection string.
    /// For SQL Server, modules with dedicated migration assemblies are configured accordingly.
    /// </summary>
    public static IServiceCollection AddModuleDbContexts(
        this IServiceCollection services,
        DatabaseProvider provider,
        string connectionString)
    {
        ArgumentNullException.ThrowIfNull(services);

        // Modules that have dedicated SQL Server migration assemblies
        const string AiMigrationsAssembly = "DotNetCloud.Modules.AI.Data.SqlServer";
        const string BookmarksMigrationsAssembly = "DotNetCloud.Modules.Bookmarks.Data.SqlServer";
        const string CalendarMigrationsAssembly = "DotNetCloud.Modules.Calendar.Data.SqlServer";
        const string ChatMigrationsAssembly = "DotNetCloud.Modules.Chat.Data.SqlServer";
        const string ContactsMigrationsAssembly = "DotNetCloud.Modules.Contacts.Data.SqlServer";
        const string EmailMigrationsAssembly = "DotNetCloud.Modules.Email.Data.SqlServer";
        // Files.Data reference removed — FilesDbContext is now managed by the Files module host process
        const string MusicMigrationsAssembly = "DotNetCloud.Modules.Music.Data.SqlServer";
        const string NotesMigrationsAssembly = "DotNetCloud.Modules.Notes.Data.SqlServer";
        const string PhotosMigrationsAssembly = "DotNetCloud.Modules.Photos.Data.SqlServer";
        const string TracksMigrationsAssembly = "DotNetCloud.Modules.Tracks.Data.SqlServer";
        const string VideoMigrationsAssembly = "DotNetCloud.Modules.Video.Data.SqlServer";

        // Blazor Server uses Transient lifetime for DbContexts to prevent
        // "second operation started" errors from concurrent component renders
        // sharing the same scoped context (scope = circuit in Blazor Server).
        services.AddDbContext<AiDbContext>(options =>
            ConfigureModuleDbContext(options, provider, connectionString, AiMigrationsAssembly), ServiceLifetime.Transient);
        services.AddDbContext<BookmarksDbContext>(options =>
            ConfigureModuleDbContext(options, provider, connectionString, BookmarksMigrationsAssembly), ServiceLifetime.Transient);
        services.AddDbContext<CalendarDbContext>(options =>
            ConfigureModuleDbContext(options, provider, connectionString, CalendarMigrationsAssembly), ServiceLifetime.Transient);
        services.AddDbContext<ChatDbContext>(options =>
            ConfigureModuleDbContext(options, provider, connectionString, ChatMigrationsAssembly), ServiceLifetime.Transient);
        services.AddDbContext<ContactsDbContext>(options =>
            ConfigureModuleDbContext(options, provider, connectionString, ContactsMigrationsAssembly), ServiceLifetime.Transient);
        services.AddDbContext<EmailDbContext>(options =>
            ConfigureModuleDbContext(options, provider, connectionString, EmailMigrationsAssembly), ServiceLifetime.Transient);
        // FilesDbContext removed — managed by the Files module host process
        services.AddDbContextFactory<MusicDbContext>(options =>
            ConfigureModuleDbContext(options, provider, connectionString, MusicMigrationsAssembly));
        services.AddDbContext<MusicDbContext>(options =>
            ConfigureModuleDbContext(options, provider, connectionString, MusicMigrationsAssembly), ServiceLifetime.Transient);
        services.AddDbContext<NotesDbContext>(options =>
            ConfigureModuleDbContext(options, provider, connectionString, NotesMigrationsAssembly), ServiceLifetime.Transient);
        services.AddDbContext<PhotosDbContext>(options =>
            ConfigureModuleDbContext(options, provider, connectionString, PhotosMigrationsAssembly), ServiceLifetime.Transient);
        services.AddDbContext<TracksDbContext>(options =>
            ConfigureModuleDbContext(options, provider, connectionString, TracksMigrationsAssembly), ServiceLifetime.Transient);
        services.AddDbContext<VideoDbContext>(options =>
            ConfigureModuleDbContext(options, provider, connectionString, VideoMigrationsAssembly), ServiceLifetime.Transient);

        return services;
    }

    /// <summary>
    /// Configures a module DbContext with the appropriate database provider settings.
    /// </summary>
    private static void ConfigureModuleDbContext(
        DbContextOptionsBuilder options,
        DatabaseProvider provider,
        string connectionString,
        string? migrationsAssembly = null)
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

                    if (!string.IsNullOrEmpty(migrationsAssembly))
                    {
                        sqlServerOptions.MigrationsAssembly(migrationsAssembly);
                    }
                });
                break;

            default:
                throw new ArgumentException($"Unsupported database provider: {provider}", nameof(provider));
        }

        // Suppress pending model changes warning for modules that don't have
        // a dedicated SQL Server migrations assembly. Their migrations were
        // generated for the PostgreSQL provider.
        options.ConfigureWarnings(warnings =>
        {
            warnings.Ignore(RelationalEventId.PendingModelChangesWarning);
        });
    }
}
