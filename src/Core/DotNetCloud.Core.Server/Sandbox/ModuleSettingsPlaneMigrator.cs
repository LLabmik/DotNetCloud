using DotNetCloud.Core.Data.Context;
using DotNetCloud.Core.Data.Entities.Modules;
using DotNetCloud.Core.Modules;
using Microsoft.EntityFrameworkCore;

namespace DotNetCloud.Core.Server.Sandbox;

/// <summary>
/// Moves a converted module's administrator settings from the core <c>SystemSettings</c> table into the
/// module settings plane.
/// </summary>
/// <remarks>
/// <para>
/// Before a module is sandboxed its settings lived in <c>SystemSettings</c> (module = module id) and
/// were read through <c>IAdminSettingsService</c>. A sandboxed module cannot read that table, so it
/// reads <c>ModuleSettings</c> instead and falls back to its own <c>appsettings.json</c> when a key is
/// absent. Without this migration an upgraded install silently reverts to the built-in defaults — the
/// operator's model or provider endpoint appears to be ignored, and an endpoint that pointed at
/// another host stops being used at all.
/// </para>
/// <para>
/// The migration is one-way and idempotent: a value already present in the module settings plane is
/// never overwritten (the operator may have edited it in the new panel), empty values are not copied,
/// keys known to hold credentials are written as secrets, and the stale <c>SystemSettings</c> rows are
/// removed so exactly one source of truth remains.
/// </para>
/// </remarks>
internal sealed class ModuleSettingsPlaneMigrator
{
    /// <summary>
    /// Modules whose settings provider reads the module settings plane, mapped to the keys that hold
    /// credentials. Add an entry as each module is converted (Track C).
    /// </summary>
    internal static readonly IReadOnlyDictionary<string, IReadOnlySet<string>> ConvertedModules =
        new Dictionary<string, IReadOnlySet<string>>(StringComparer.Ordinal)
        {
            ["dotnetcloud.ai"] = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "ApiKey",
            },
        };

    /// <summary>
    /// Copies the converted modules' system settings into the module settings plane and deletes the
    /// now-unused system rows.
    /// </summary>
    /// <param name="db">The core database context.</param>
    /// <param name="settings">The module settings store.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>How many values were copied and how many stale rows were removed.</returns>
    public static async Task<ModuleSettingsMigrationReport> MigrateAsync(
        CoreDbContext db,
        IModuleSettingsStore settings,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(db);
        ArgumentNullException.ThrowIfNull(settings);

        var moduleIds = ConvertedModules.Keys.ToList();

        var rows = await db.SystemSettings
            .AsTracking()
            .Where(setting => moduleIds.Contains(setting.Module))
            .OrderBy(setting => setting.Module)
            .ThenBy(setting => setting.Key)
            .ToListAsync(cancellationToken);

        if (rows.Count == 0)
        {
            return ModuleSettingsMigrationReport.Empty;
        }

        var copied = 0;
        var skipped = 0;
        var removed = 0;

        foreach (var row in rows)
        {
            var secretKeys = ConvertedModules[row.Module];

            if (string.IsNullOrWhiteSpace(row.Value))
            {
                // "Not configured": copying an empty value would be indistinguishable from absent and
                // would make a later panel edit look like a change that never happened.
                db.SystemSettings.Remove(row);
                removed++;
                skipped++;
                continue;
            }

            if (!ModuleSettingLimits.IsValidKey(row.Key) || row.Value.Length > ModuleSettingLimits.MaxValueLength)
            {
                // Not representable in the settings plane: leave the row in place rather than
                // truncating an operator's value; the module falls back to its own default for it.
                skipped++;
                continue;
            }

            var existing = await settings.GetAsync(row.Module, row.Key, cancellationToken);
            if (existing is null)
            {
                await settings.SetAsync(
                    row.Module,
                    row.Key,
                    row.Value,
                    isSecret: secretKeys.Contains(row.Key),
                    updatedByUserId: null,
                    cancellationToken);
                copied++;
            }
            else
            {
                // Already configured in the new plane: the operator's newer value wins.
                skipped++;
            }

            // The system row is stale in every representable case — the module no longer reads that
            // table.
            db.SystemSettings.Remove(row);
            removed++;
        }

        await db.SaveChangesAsync(cancellationToken);

        return new ModuleSettingsMigrationReport(copied, skipped, removed);
    }
}

/// <summary>Outcome of <see cref="ModuleSettingsPlaneMigrator.MigrateAsync"/>.</summary>
/// <param name="Copied">Values written into the module settings plane.</param>
/// <param name="Skipped">Rows that needed no copy (already present, empty or not representable).</param>
/// <param name="RemovedSystemSettings">Stale <c>SystemSettings</c> rows deleted.</param>
internal sealed record ModuleSettingsMigrationReport(int Copied, int Skipped, int RemovedSystemSettings)
{
    /// <summary>A run that found nothing to migrate.</summary>
    public static ModuleSettingsMigrationReport Empty { get; } = new(0, 0, 0);
}
