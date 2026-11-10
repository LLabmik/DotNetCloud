using DotNetCloud.Core.Data.Context;
using DotNetCloud.Core.Data.Entities.Modules;
using DotNetCloud.Core.Server.ModuleLoading;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace DotNetCloud.Core.Server.Sandbox;

/// <summary>
/// Writes the default values a module declares in its manifest into the module settings plane.
/// </summary>
/// <remarks>
/// <para>
/// A sandboxed module cannot read the core's <c>SystemSettings</c>; it reads its own settings plane and
/// falls back to built-in defaults when a key is absent. Seeding the manifest defaults at install time
/// means an operator can see — and change — the configuration that is actually in effect, instead of
/// guessing which values a module invented for itself.
/// </para>
/// <para>
/// <strong>Seeding is additive only.</strong> A key that already has a value is never touched, so:
/// an administrator's edit always wins, an upgrade never resets a configured module, and a re-seed
/// after a restart is a no-op.
/// </para>
/// </remarks>
internal static class ModuleSettingDefaultsSeeder
{
    /// <summary>
    /// Seeds the defaults declared by the manifest that sits in <paramref name="moduleDirectory"/>.
    /// </summary>
    /// <param name="db">The core database context.</param>
    /// <param name="moduleId">The module id (must already be registered).</param>
    /// <param name="moduleDirectory">The module's root directory (which holds <c>manifest.json</c>).</param>
    /// <param name="logger">Optional logger.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The number of rows written.</returns>
    public static async Task<int> SeedFromModuleDirectoryAsync(
        CoreDbContext db,
        string moduleId,
        string moduleDirectory,
        ILogger? logger = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(db);
        ArgumentException.ThrowIfNullOrWhiteSpace(moduleId);

        if (string.IsNullOrWhiteSpace(moduleDirectory))
        {
            return 0;
        }

        var manifestPath = Path.Combine(moduleDirectory, "manifest.json");
        if (!File.Exists(manifestPath))
        {
            return 0;
        }

        var loader = new ModuleManifestLoader(NullLogger<ModuleManifestLoader>.Instance);
        var result = loader.LoadAndValidate(manifestPath, moduleId);
        if (!result.IsValid || result.Manifest is null || result.Manifest.Settings.Count == 0)
        {
            return 0;
        }

        return await SeedAsync(db, moduleId, result.Manifest.Settings, logger, cancellationToken);
    }

    /// <summary>
    /// Seeds the given defaults, skipping any key that already has a stored value.
    /// </summary>
    /// <param name="db">The core database context.</param>
    /// <param name="moduleId">The module id.</param>
    /// <param name="defaults">The manifest's setting defaults.</param>
    /// <param name="logger">Optional logger.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The number of rows written.</returns>
    public static async Task<int> SeedAsync(
        CoreDbContext db,
        string moduleId,
        IReadOnlyList<ManifestSettingDefault> defaults,
        ILogger? logger = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(db);
        ArgumentException.ThrowIfNullOrWhiteSpace(moduleId);
        ArgumentNullException.ThrowIfNull(defaults);

        if (defaults.Count == 0)
        {
            return 0;
        }

        // CoreDbContext is NoTracking by default; the existence check only needs the key set, so a
        // projection is cheaper than materialising rows.
        var existingKeys = await db.ModuleSettings
            .AsNoTracking()
            .Where(setting => setting.ModuleId == moduleId)
            .Select(setting => setting.Key)
            .ToListAsync(cancellationToken);

        var known = new HashSet<string>(existingKeys, StringComparer.OrdinalIgnoreCase);
        var written = 0;

        foreach (var setting in defaults)
        {
            if (!known.Add(setting.Key))
            {
                // Either the administrator set it or an earlier seed did. Never overwrite.
                continue;
            }

            db.ModuleSettings.Add(new ModuleSetting
            {
                ModuleId = moduleId,
                Key = setting.Key,
                Value = setting.Value,
                // Manifest defaults are public by definition; validation rejects a secret default.
                IsSecret = false,
                UpdatedAtUtc = DateTime.UtcNow,
                UpdatedByUserId = null,
            });

            written++;
        }

        if (written > 0)
        {
            await db.SaveChangesAsync(cancellationToken);

            logger?.LogInformation(
                "Seeded {Count} manifest default setting(s) for module {ModuleId}",
                written,
                moduleId);
        }

        return written;
    }
}
