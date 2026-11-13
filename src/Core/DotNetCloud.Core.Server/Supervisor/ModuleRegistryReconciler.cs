using DotNetCloud.Core.Data.Context;
using DotNetCloud.Core.Data.Entities.Modules;
using DotNetCloud.Core.Modules;
using DotNetCloud.Core.Server.ModuleLoading;
using Microsoft.EntityFrameworkCore;

namespace DotNetCloud.Core.Server.Supervisor;

/// <summary>
/// A discovered module plus the manifest the supervisor loaded for it.
/// </summary>
/// <param name="ModuleId">The module identifier (directory name).</param>
/// <param name="Manifest">The module's manifest.</param>
/// <param name="IsRequired">Whether the module is a required product module.</param>
internal sealed record ModuleRegistryEntry(string ModuleId, ModuleManifestData Manifest, bool IsRequired);

/// <summary>
/// Reconciles the <c>InstalledModules</c> registry with what is actually on disk.
/// </summary>
/// <remarks>
/// <para>
/// This used to be insert-only, so an upgraded module kept its original version, trust tier and
/// data-store mode forever: the admin UI and <c>/health/ready</c> reported <c>"0.0.0"</c> and
/// <c>dataStoreMode: None</c> for a module that was running sandboxed with an isolated store.
/// </para>
/// <para>
/// <strong>The rows must be loaded with tracking.</strong> <see cref="CoreDbContext"/> sets
/// <c>QueryTrackingBehavior.NoTracking</c> globally, so a plain <c>ToDictionaryAsync</c> hands back
/// detached instances and every mutation below is silently dropped on <c>SaveChanges</c> — the same
/// trap that made a failed store install unrecoverable.
/// </para>
/// <para>
/// Trust tier comes from <em>provenance</em>: required wins, an existing row keeps its tier (a
/// package that claims a bundled module id is never promoted), and a newly discovered directory is
/// a bundled module.
/// </para>
/// </remarks>
internal static class ModuleRegistryReconciler
{
    /// <summary>
    /// Applies the discovered modules to the registry and saves the changes.
    /// </summary>
    /// <param name="dbContext">The core database context.</param>
    /// <param name="entries">The discovered modules with their manifests.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The number of rows added and reconciled.</returns>
    public static async Task<(int Added, int Updated)> ReconcileAsync(
        CoreDbContext dbContext,
        IReadOnlyList<ModuleRegistryEntry> entries,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(dbContext);
        ArgumentNullException.ThrowIfNull(entries);

        var existingModules = await dbContext.InstalledModules
            .AsTracking()
            .ToDictionaryAsync(m => m.ModuleId, StringComparer.OrdinalIgnoreCase, cancellationToken);

        var added = 0;
        var updated = 0;

        foreach (var entry in entries)
        {
            var isNew = !existingModules.TryGetValue(entry.ModuleId, out var existingRecord);
            InstalledModule record;
            if (isNew)
            {
                record = new InstalledModule
                {
                    ModuleId = entry.ModuleId,
                    Status = "Enabled",
                    InstalledAt = DateTime.UtcNow,
                };

                dbContext.InstalledModules.Add(record);
                existingModules[entry.ModuleId] = record;
                added++;
            }
            else
            {
                record = existingRecord!;
                updated++;
            }

            if (entry.IsRequired)
            {
                record.TrustTier = ModuleTrustTier.Required;
            }
            else if (isNew)
            {
                record.TrustTier = ModuleTrustTier.Bundled;
            }

            record.Version = entry.Manifest.Version;
            record.IsRequired = entry.IsRequired;
            record.UpdatedAt = DateTime.UtcNow;

            // v2 manifests are authoritative for the data-store mode. v1 (legacy in-repo) modules
            // keep the core-managed schema model until Track C migrates them.
            record.DataStoreMode = entry.Manifest.ManifestVersion >= 2
                ? entry.Manifest.EffectiveDataStoreMode
                : ModuleDataStoreMode.None;

            if (entry.IsRequired)
            {
                record.SandboxStatus = ModuleSandboxStatus.Active;
            }
            else if (isNew)
            {
                record.SandboxStatus = record.DataStoreMode == ModuleDataStoreMode.Isolated
                    ? ModuleSandboxStatus.PendingProvisioning
                    : ModuleSandboxStatus.Active;
            }
        }

        if (added > 0 || updated > 0)
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }

        return (added, updated);
    }
}
