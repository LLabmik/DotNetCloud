using DotNetCloud.Core.Data.Entities.Modules;
using DotNetCloud.Core.Data.Modules;
using DotNetCloud.Core.Modules;
using DotNetCloud.Core.Server.ModuleLoading;
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
/// applies EF migrations for modules the core still owns the schema of: the required modules
/// (which share the <c>core</c> schema) and bundled modules whose manifest predates v2.
/// </summary>
/// <remarks>
/// <para>
/// <see cref="ModuleDbContextTypes"/> is now only a <em>registration</em> of “this process owns a
/// DbContext for this module” — it no longer decides the schema <em>mode</em>. Whether a module is
/// core-migrated or self-migrated comes from its manifest (v2 <c>dataStore</c>) via
/// <see cref="IModuleDataStoreModeSource"/>: <c>isolated</c> means the module process provisions
/// and migrates its own schema, so the core must not race it.
/// </para>
/// <para>
/// A converted module therefore drops out of this table when its <c>.Data</c> project stops being
/// referenced by the host, and a v1 bundled module keeps the legacy behaviour until it is
/// converted — the same ordering the upgrade path (§9 adoption) relies on.
/// </para>
/// </remarks>
public class DbContextSchemaProvider : IModuleSchemaProvider
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<DbContextSchemaProvider> _logger;
    private readonly IModuleDataStoreModeSource? _dataStoreModes;

    /// <summary>
    /// Modules whose DbContext this process owns and can migrate.
    /// </summary>
    /// <remarks>
    /// Required modules are core-migrated unconditionally (they share the <c>core</c> schema and
    /// their manifests must not declare a data store). The remaining entries are bundled modules
    /// that have not been converted to the sandbox yet and therefore still declare a v1 manifest.
    /// </remarks>
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

    /// <summary>
    /// Initializes a new instance of the <see cref="DbContextSchemaProvider"/> class.
    /// </summary>
    /// <param name="scopeFactory">Scope factory used to resolve a module DbContext.</param>
    /// <param name="logger">Logger.</param>
    /// <param name="dataStoreModes">
    /// Manifest-declared data-store modes. Optional so the CLI (which has no supervised modules)
    /// and tests can construct the provider without a manifest registry.
    /// </param>
    public DbContextSchemaProvider(
        IServiceScopeFactory scopeFactory,
        ILogger<DbContextSchemaProvider> logger,
        IModuleDataStoreModeSource? dataStoreModes = null)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
        _dataStoreModes = dataStoreModes;
    }

    /// <inheritdoc/>
    /// <remarks>
    /// A module is core-managed when this process owns its DbContext <em>and</em> its manifest has
    /// not declared a data store. A declared <c>dataStore</c> takes the module off this path either
    /// way — <c>isolated</c> because the module process migrates its own schema, <c>none</c> because
    /// there is no schema to migrate. An unknown manifest is treated as v1 (legacy core migration)
    /// so nothing silently stops being migrated during startup ordering.
    /// </remarks>
    public bool IsCoreManaged(string moduleId)
    {
        if (!ModuleDbContextTypes.ContainsKey(moduleId))
        {
            return false;
        }

        // Required modules share the core schema; their manifests cannot declare a data store.
        if (RequiredModules.IsRequired(moduleId))
        {
            return true;
        }

        // The manifest is authoritative for everything else.
        return _dataStoreModes?.GetDataStoreMode(moduleId) is null;
    }

    /// <inheritdoc/>
    public async Task EnsureSchemaAsync(string moduleId, CancellationToken cancellationToken = default)
    {
        if (!IsCoreManaged(moduleId) || !ModuleDbContextTypes.TryGetValue(moduleId, out var contextType))
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
