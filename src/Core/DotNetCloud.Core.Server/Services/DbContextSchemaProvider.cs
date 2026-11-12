using DotNetCloud.Core.Data.Modules;
using DotNetCloud.Core.Modules;
using DotNetCloud.Modules.Calendar.Data;
using DotNetCloud.Modules.Chat.Data;
using DotNetCloud.Modules.Contacts.Data;
using DotNetCloud.Modules.Files.Data;
using DotNetCloud.Modules.Music.Data;
using DotNetCloud.Modules.Notes.Data;
using DotNetCloud.Modules.Video.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System.Data;
using System.Data.Common;

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
        ["dotnetcloud.files"] = typeof(FilesDbContext),
        ["dotnetcloud.chat"] = typeof(ChatDbContext),
        ["dotnetcloud.contacts"] = typeof(ContactsDbContext),
        ["dotnetcloud.calendar"] = typeof(CalendarDbContext),
        ["dotnetcloud.notes"] = typeof(NotesDbContext),
        ["dotnetcloud.music"] = typeof(MusicDbContext),
        ["dotnetcloud.video"] = typeof(VideoDbContext),
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
        var context = (DbContext?)scope.ServiceProvider.GetService(contextType);
        if (context is null)
        {
            // Module DbContext not registered in this process (e.g., process-isolated
            // module whose schema is handled by its own host). Skip gracefully.
            _logger.LogDebug("Module {ModuleId} DbContext not registered in this process; skipping schema creation", moduleId);
            return;
        }
        var creator = context.GetService<IRelationalDatabaseCreator>();

        if (await creator.ExistsAsync(cancellationToken))
        {
            var pending = (await context.Database.GetPendingMigrationsAsync(cancellationToken)).ToList();
            if (pending.Count != 0)
            {
                // Run EF migrations FIRST. The migration history owns schema evolution, including
                // moves into the core schema for required modules (promote migrations such as
                // ALTER TABLE ... SET SCHEMA core). Creating tables from the model BEFORE
                // replaying migrations made those promote steps collide with the model-created
                // tables (42P07 "relation ... already exists in schema core"), leaving duplicate
                // core.* and legacy module.* tables (e.g. both core.Notes AND notes.Notes) and a
                // migration chain that never completed on fresh installs.
                try
                {
                    await context.Database.MigrateAsync(cancellationToken);
                    _logger.LogInformation("Applied {Count} migrations for module {ModuleId}", pending.Count, moduleId);
                    return;
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex,
                        "Migrations failed for module {ModuleId}; falling back to model-based table creation", moduleId);
                }

                // Migration replay failed — typically an adoption database that already has tables
                // but an empty/mismatched migration history. Create any tables missing from the
                // current model, then mark the pending migrations as applied so they are not
                // retried (and re-failed) on every subsequent startup.
                await ModuleSchemaInitializer.BaselineAsync(
                    context, moduleId, pending, _logger, cancellationToken);
                return;
            }

            // No pending migrations. Ensure the schema still matches the current model (create any
            // tables introduced outside the migration history). CreateTablesAsync fails completely
            // if any table exists, so the shared initializer runs each CREATE TABLE individually,
            // ignoring "already exists".
            await ModuleSchemaInitializer.EnsureMissingTablesAsync(
                context, moduleId, _logger, cancellationToken);
            return;
        }

        // Database doesn't exist yet — create it.
        _logger.LogInformation("Creating schema for module {ModuleId}", moduleId);
        try
        {
            await context.Database.MigrateAsync(cancellationToken);
        }
        catch (Exception ex) when (ex is InvalidOperationException or System.Data.Common.DbException)
        {
            _logger.LogWarning(ex,
                "Migrations failed for module {ModuleId}, creating tables from model", moduleId);
            await creator.CreateTablesAsync(cancellationToken);
            var remaining = (await context.Database.GetPendingMigrationsAsync(cancellationToken)).ToList();
            await ModuleSchemaInitializer.BaselineAsync(
                context, moduleId, remaining, _logger, cancellationToken);
        }

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
