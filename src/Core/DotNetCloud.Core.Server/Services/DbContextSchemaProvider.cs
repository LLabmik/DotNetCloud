using DotNetCloud.Core.Modules;
using DotNetCloud.Modules.AI.Data;
using DotNetCloud.Modules.Calendar.Data;
using DotNetCloud.Modules.Chat.Data;
using DotNetCloud.Modules.Contacts.Data;
using DotNetCloud.Modules.Files.Data;
using DotNetCloud.Modules.Music.Data;
using DotNetCloud.Modules.Notes.Data;
using DotNetCloud.Modules.Photos.Data;
using DotNetCloud.Modules.Search.Data;
using DotNetCloud.Modules.Tracks.Data;
using DotNetCloud.Modules.Video.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace DotNetCloud.Core.Server.Services;

/// <summary>
/// Core-managed schema provider. Resolves a module's DbContext from DI and
/// applies EF migrations. Used by first-party modules whose DbContext types
/// are known at compile time.
/// </summary>
public class DbContextSchemaProvider : IModuleSchemaProvider
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<DbContextSchemaProvider> _logger;

    private static readonly Dictionary<string, Type> ModuleDbContextTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        ["dotnetcloud.files"]     = typeof(FilesDbContext),
        ["dotnetcloud.chat"]      = typeof(ChatDbContext),
        ["dotnetcloud.search"]    = typeof(SearchDbContext),
        ["dotnetcloud.contacts"]  = typeof(ContactsDbContext),
        ["dotnetcloud.calendar"]  = typeof(CalendarDbContext),
        ["dotnetcloud.notes"]     = typeof(NotesDbContext),
        ["dotnetcloud.tracks"]    = typeof(TracksDbContext),
        ["dotnetcloud.photos"]    = typeof(PhotosDbContext),
        ["dotnetcloud.music"]     = typeof(MusicDbContext),
        ["dotnetcloud.video"]     = typeof(VideoDbContext),
        ["dotnetcloud.ai"]        = typeof(AiDbContext),
    };

    public DbContextSchemaProvider(IServiceScopeFactory scopeFactory, ILogger<DbContextSchemaProvider> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    /// <inheritdoc/>
    public bool IsCoreManaged(string moduleId) => ModuleDbContextTypes.ContainsKey(moduleId);

    /// <inheritdoc/>
    public async Task EnsureSchemaAsync(string moduleId, CancellationToken cancellationToken = default)
    {
        if (!ModuleDbContextTypes.TryGetValue(moduleId, out var contextType))
            return;

        using var scope = _scopeFactory.CreateScope();
        var context = (DbContext)scope.ServiceProvider.GetRequiredService(contextType);
        var creator = context.GetService<IRelationalDatabaseCreator>();

        if (await creator.ExistsAsync(cancellationToken))
        {
            var pending = await context.Database.GetPendingMigrationsAsync(cancellationToken);
            if (pending.Any())
            {
                _logger.LogInformation("Applying {Count} pending migrations for {ModuleId}",
                    pending.Count(), moduleId);
                await context.Database.MigrateAsync(cancellationToken);
            }
            return;
        }

        _logger.LogInformation("Creating schema for module {ModuleId}", moduleId);
        await context.Database.MigrateAsync(cancellationToken);
        _logger.LogInformation("Created schema for module {ModuleId}", moduleId);
    }

    /// <inheritdoc/>
    public async Task DropSchemaAsync(string moduleId, CancellationToken cancellationToken = default)
    {
        if (RequiredModules.IsRequired(moduleId))
            throw new InvalidOperationException($"Cannot drop schema for required module '{moduleId}'.");

        if (!ModuleDbContextTypes.TryGetValue(moduleId, out var contextType))
            return;

        using var scope = _scopeFactory.CreateScope();
        var context = (DbContext)scope.ServiceProvider.GetRequiredService(contextType);
        await context.Database.EnsureDeletedAsync(cancellationToken);
    }
}
