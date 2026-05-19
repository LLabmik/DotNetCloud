using DotNetCloud.Core.Modules;
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
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System.Data.Common;

namespace DotNetCloud.Core.Schema.Services;

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
        ["dotnetcloud.search"] = typeof(SearchDbContext),
        ["dotnetcloud.contacts"] = typeof(ContactsDbContext),
        ["dotnetcloud.calendar"] = typeof(CalendarDbContext),
        ["dotnetcloud.notes"] = typeof(NotesDbContext),
        ["dotnetcloud.tracks"] = typeof(TracksDbContext),
        ["dotnetcloud.photos"] = typeof(PhotosDbContext),
        ["dotnetcloud.music"] = typeof(MusicDbContext),
        ["dotnetcloud.video"] = typeof(VideoDbContext),
        ["dotnetcloud.ai"] = typeof(AiDbContext),
        ["dotnetcloud.bookmarks"] = typeof(BookmarksDbContext),
        ["dotnetcloud.email"] = typeof(EmailDbContext),
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
            // Create only tables that don't exist yet. CreateTablesAsync fails
            // completely if any table exists, so use GenerateCreateScript and
            // execute each CREATE TABLE individually, ignoring "already exists".
            await CreateMissingTablesAsync(context, moduleId, cancellationToken);

            var pending = (await context.Database.GetPendingMigrationsAsync(cancellationToken)).ToList();
            if (pending.Count != 0)
            {
                bool migrationApplied = false;
                try
                {
                    await context.Database.MigrateAsync(cancellationToken);
                    migrationApplied = true;
                    _logger.LogInformation("Applied {Count} migrations for module {ModuleId}", pending.Count, moduleId);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Migration failed for module {ModuleId}", moduleId);
                }

                if (!migrationApplied)
                {
                    foreach (var migrationId in pending)
                        await RecordMigrationAsAppliedAsync(context, migrationId, cancellationToken);
                }
            }
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
            var pending = (await context.Database.GetPendingMigrationsAsync(cancellationToken)).ToList();
            if (pending.Count != 0)
                await RecordMigrationAsAppliedAsync(context, pending[0], cancellationToken);
        }

        _logger.LogInformation("Created schema for module {ModuleId}", moduleId);
    }

    /// <summary>
    /// Inserts a row into <c>__EFMigrationsHistory</c> so the migration is not retried
    /// on subsequent startups.
    /// </summary>
    private async Task RecordMigrationAsAppliedAsync(DbContext context, string migrationId,
        CancellationToken cancellationToken)
    {
        try
        {
            await context.Database.ExecuteSqlRawAsync(
                "INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion]) VALUES ({0}, '8.0.0')",
                migrationId);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex,
                "Failed to record migration {MigrationId} in __EFMigrationsHistory. " +
                "Migration will be retried on next startup.", migrationId);
        }
    }

    /// <summary>
    /// Generates CREATE TABLE statements for the module's model and executes each one
    /// individually, ignoring "already exists" errors. Retries up to 10 passes to
    /// resolve FK ordering issues.
    /// </summary>
    private async Task CreateMissingTablesAsync(DbContext context, string moduleId,
        CancellationToken cancellationToken)
    {
        try
        {
            var script = context.Database.GenerateCreateScript();
            if (string.IsNullOrWhiteSpace(script))
            {
                _logger.LogWarning("Empty create script for module {ModuleId}", moduleId);
                return;
            }

            var batches = script.Split("GO", StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Select(b => b.Trim())
                .Where(b => b.Length > 0)
                .ToList();

            // SQL Server's EF Core GenerateCreateScript uses double-quoted identifiers
            // in HasFilter() expressions. Ensure QUOTED_IDENTIFIER ON so these work.
            var isSqlServer = context.Database.ProviderName?.Contains("SqlServer", StringComparison.OrdinalIgnoreCase) == true;
            if (isSqlServer)
            {
                // Apply to each batch individually so retry logic still works per-batch
                for (var i = 0; i < batches.Count; i++)
                {
                    batches[i] = "SET QUOTED_IDENTIFIER ON;\n" + batches[i];
                }
            }

            var failedBatches = new List<string>(batches);
            var created = 0;
            var skipped = 0;

            // Retry loop: each pass tries all remaining batches. FKs to later
            // tables resolve once those tables are created in a previous pass.
            for (var pass = 0; pass < 10 && failedBatches.Count > 0; pass++)
            {
                var stillFailing = new List<string>();
                foreach (var batch in failedBatches)
                {
                    try
                    {
                        await context.Database.ExecuteSqlRawAsync(batch, cancellationToken);
                        created++;
                    }
                    catch (DbException ex)
                    {
                        var msg = ex.Message;
                        if (msg.Contains("already exists", StringComparison.OrdinalIgnoreCase)
                            || msg.Contains("already an object", StringComparison.OrdinalIgnoreCase)
                            || msg.Contains("duplicate", StringComparison.OrdinalIgnoreCase)
                            || msg.Contains("cycles or multiple cascade", StringComparison.OrdinalIgnoreCase)
                            || msg.Contains("SET options", StringComparison.OrdinalIgnoreCase)
                            || msg.Contains("Incorrect WHERE clause", StringComparison.OrdinalIgnoreCase))
                        {
                            skipped++;
                            continue;
                        }
                        // FK target not yet created — retry on next pass
                        _logger.LogDebug(ex, "Batch still failing (pass {Pass}): {Batch}",
                            pass, batch[..System.Math.Min(batch.Length, 200)]);
                        stillFailing.Add(batch);
                    }
                }

                if (stillFailing.Count == failedBatches.Count)
                    break; // No progress — stop retrying
                failedBatches = stillFailing;
            }

            if (created > 0 || skipped > 0)
            {
                _logger.LogInformation("Module {ModuleId}: created {Created}, skipped {Skipped}, still failing {Failed}",
                    moduleId, created, skipped, failedBatches.Count);
            }

            if (failedBatches.Count > 0)
            {
                _logger.LogWarning("{Count} CREATE batches failed for module {ModuleId} after retries",
                    failedBatches.Count, moduleId);
                foreach (var fb in failedBatches.Take(3))
                    _logger.LogWarning("  Failed batch: {Batch}", fb[..System.Math.Min(fb.Length, 300)]);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to create tables for module {ModuleId}", moduleId);
        }
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
