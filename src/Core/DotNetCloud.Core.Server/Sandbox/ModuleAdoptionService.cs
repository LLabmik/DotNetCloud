using DotNetCloud.Core;
using DotNetCloud.Core.Authorization;
using DotNetCloud.Core.Capabilities;
using DotNetCloud.Core.Data.Context;
using DotNetCloud.Core.Data.Entities.Modules;
using DotNetCloud.Core.Modules;
using DotNetCloud.Core.Modules.Supervisor;
using DotNetCloud.Core.Server.ModuleLoading;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace DotNetCloud.Core.Server.Sandbox;

/// <summary>
/// Adopts the existing core-managed schemas of installed non-required modules into the sandboxed
/// model on upgrade (§9): provisions the scoped principal and transfers schema/object ownership
/// without moving data.
/// </summary>
/// <remarks>
/// Adoption is idempotent and never touches required modules. When a prerequisite is missing, a
/// bundled module may fall back to the transitional legacy-access override (audited, never for
/// store-tier modules); otherwise the module is held with <see cref="ModuleSandboxStatus.Error"/>
/// and an operator-facing reason.
/// </remarks>
internal sealed class ModuleAdoptionService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IModuleDataStoreProvisioningService _provisioning;
    private readonly ModuleDiscoveryService _discovery;
    private readonly ModuleManifestLoader _manifestLoader;
    private readonly ProcessSupervisorOptions _options;
    private readonly ILogger<ModuleAdoptionService> _logger;

    /// <summary>Initializes a new instance of the <see cref="ModuleAdoptionService"/> class.</summary>
    /// <param name="scopeFactory">Scope factory for the core database context and audit logger.</param>
    /// <param name="provisioning">Data-store provisioning/adoption implementation.</param>
    /// <param name="discovery">Discovers the deployed module directories.</param>
    /// <param name="manifestLoader">Reads the deployed <c>manifest.json</c>.</param>
    /// <param name="options">Supervisor options (legacy-access override).</param>
    /// <param name="logger">Logger.</param>
    public ModuleAdoptionService(
        IServiceScopeFactory scopeFactory,
        IModuleDataStoreProvisioningService provisioning,
        ModuleDiscoveryService discovery,
        ModuleManifestLoader manifestLoader,
        IOptions<ProcessSupervisorOptions> options,
        ILogger<ModuleAdoptionService> logger)
    {
        _scopeFactory = scopeFactory;
        _provisioning = provisioning;
        _discovery = discovery;
        _manifestLoader = manifestLoader;
        _options = options.Value;
        _logger = logger;
    }

    /// <summary>
    /// Reports what adoption would do for every installed module, without changing anything.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The preflight report.</returns>
    public async Task<ModuleAdoptionReport> GetPreflightAsync(CancellationToken cancellationToken = default)
    {
        using var scope = _scopeFactory.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<CoreDbContext>();

        var modules = await dbContext.InstalledModules
            .AsNoTracking()
            .OrderBy(m => m.ModuleId)
            .ToListAsync(cancellationToken);

        var stores = await dbContext.ModuleDataStores
            .AsNoTracking()
            .ToDictionaryAsync(d => d.ModuleId, cancellationToken);

        ModuleDataStorePrerequisiteResult? prerequisites = null;

        List<ModuleAdoptionEntry> entries = [];
        foreach (var module in modules)
        {
            entries.Add(await EvaluateAsync(module, stores, cancellationToken));
        }

        return new ModuleAdoptionReport(entries);

        async Task<ModuleAdoptionEntry> EvaluateAsync(
            InstalledModule module,
            IReadOnlyDictionary<string, ModuleDataStore> storeRows,
            CancellationToken ct)
        {
            var dataStoreMode = ResolveEffectiveDataStoreMode(module.ModuleId, module.DataStoreMode);

            if (module.IsRequired)
            {
                return Entry(module, dataStoreMode, ModuleAdoptionOutcome.Skipped, "Required module — schema stays core-managed.");
            }

            if (dataStoreMode != ModuleDataStoreMode.Isolated)
            {
                return Entry(module, dataStoreMode, ModuleAdoptionOutcome.Skipped, "dataStore: none — no schema to adopt.");
            }

            if (storeRows.TryGetValue(module.ModuleId, out var store)
                && store.Status == ModuleDataStoreStatus.Active
                && !string.IsNullOrEmpty(store.EncryptedConnectionString))
            {
                return Entry(module, dataStoreMode, ModuleAdoptionOutcome.AlreadyProvisioned, null);
            }

            prerequisites ??= await _provisioning.CheckPrerequisitesAsync(ct);
            return prerequisites.IsSatisfied
                ? Entry(module, dataStoreMode, ModuleAdoptionOutcome.Adoptable, null)
                : Entry(module, dataStoreMode, ModuleAdoptionOutcome.Blocked, string.Join(" ", prerequisites.Errors));
        }

        static ModuleAdoptionEntry Entry(
            InstalledModule module, ModuleDataStoreMode dataStoreMode, ModuleAdoptionOutcome outcome, string? reason) =>
            new(module.ModuleId, module.TrustTier.ToString(), dataStoreMode.ToString(), outcome, reason);
    }

    /// <summary>
    /// Resolves the data-store mode the module will actually run with.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The deployed manifest is authoritative for the schema mode (§4.7). The persisted
    /// <see cref="InstalledModule.DataStoreMode"/> column is only refreshed by the supervisor's
    /// discovery upsert, which starts <b>after</b> this service. A module converted to a sandboxed
    /// host in this release therefore still reads as <c>None</c> here, and trusting the column would
    /// skip adoption — the supervisor would then provision a brand-new empty schema while the
    /// module's real data stayed behind in the legacy one.
    /// </para>
    /// <para>
    /// Failures fall back to the persisted value: adoption must never block startup.
    /// </para>
    /// </remarks>
    /// <param name="moduleId">The module identifier.</param>
    /// <param name="persistedMode">The mode recorded in the database.</param>
    /// <returns>The effective data-store mode.</returns>
    private ModuleDataStoreMode ResolveEffectiveDataStoreMode(string moduleId, ModuleDataStoreMode persistedMode)
    {
        try
        {
            var discovered = _discovery.DiscoverModule(moduleId);
            if (discovered?.ManifestPath is null)
            {
                return persistedMode;
            }

            var result = _manifestLoader.LoadAndValidate(discovered.ManifestPath, moduleId);
            return result.IsValid && result.Manifest is not null
                ? result.Manifest.EffectiveDataStoreMode
                : persistedMode;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogWarning(
                ex, "Could not read the deployed manifest for module {ModuleId}; using the recorded data-store mode",
                LogSanitizer.Sanitize(moduleId));
            return persistedMode;
        }
    }

    /// <summary>
    /// Adopts every module the preflight marked adoptable.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The adoption report with per-module results.</returns>
    public async Task<ModuleAdoptionReport> AdoptInstalledModulesAsync(
        CancellationToken cancellationToken = default)
    {
        var preflight = await GetPreflightAsync(cancellationToken);

        List<ModuleAdoptionEntry> results = [];
        foreach (var entry in preflight.Entries)
        {
            if (entry.Outcome != ModuleAdoptionOutcome.Adoptable)
            {
                results.Add(entry);
                continue;
            }

            results.Add(await AdoptAsync(entry.ModuleId, cancellationToken));
        }

        var report = new ModuleAdoptionReport(results);
        foreach (var entry in results)
        {
            switch (entry.Outcome)
            {
                case ModuleAdoptionOutcome.Adopted:
                    _logger.LogInformation("Adopted the existing schema for module {ModuleId}", LogSanitizer.Sanitize(entry.ModuleId));
                    break;
                case ModuleAdoptionOutcome.Blocked:
                case ModuleAdoptionOutcome.Failed:
                case ModuleAdoptionOutcome.LegacyOverride:
                    _logger.LogWarning(
                        "Module {ModuleId} was not adopted ({Outcome}): {Reason}",
                        LogSanitizer.Sanitize(entry.ModuleId), entry.Outcome, entry.Reason);
                    break;
            }
        }

        return report;
    }

    /// <summary>
    /// Adopts one module's existing schema.
    /// </summary>
    /// <param name="moduleId">The module identifier.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The per-module result.</returns>
    public async Task<ModuleAdoptionEntry> AdoptAsync(
        string moduleId, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(moduleId);

        using var scope = _scopeFactory.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<CoreDbContext>();

        var module = await dbContext.InstalledModules
            .AsNoTracking()
            .FirstOrDefaultAsync(m => m.ModuleId == moduleId, cancellationToken)
            ?? throw new InvalidOperationException($"Module '{moduleId}' is not installed.");

        var dataStoreMode = ResolveEffectiveDataStoreMode(module.ModuleId, module.DataStoreMode);

        ModuleAdoptionEntry Entry(ModuleAdoptionOutcome outcome, string? reason) =>
            new(moduleId, module.TrustTier.ToString(), dataStoreMode.ToString(), outcome, reason);

        if (module.IsRequired)
        {
            return Entry(ModuleAdoptionOutcome.Skipped, "Required module — schema stays core-managed.");
        }

        if (dataStoreMode != ModuleDataStoreMode.Isolated)
        {
            return Entry(ModuleAdoptionOutcome.Skipped, "dataStore: none — no schema to adopt.");
        }

        // A bundled module's data lives in the schema the core created for it before the sandbox
        // existed (for example "bookmarks"). The schema name is derived from the module id here, in
        // the core — never read from a manifest — and the provisioning service moves the objects
        // into the generated schema the module now owns before transferring ownership.
        var result = ModuleLegacySchema.TryResolve(moduleId, out var legacySchema, out _)
            ? await _provisioning.AdoptExistingStoreAsync(moduleId, legacySchema, cancellationToken)
            : await _provisioning.AdoptExistingStoreAsync(moduleId, cancellationToken);

        if (result.Success)
        {
            await SetSandboxStatusAsync(moduleId, ModuleSandboxStatus.Active, cancellationToken);
            return Entry(ModuleAdoptionOutcome.Adopted, null);
        }

        // Transitional override (§9.5): bundled modules only, never store-tier or required.
        if (_options.AllowLegacyAccess && module.TrustTier != ModuleTrustTier.Store)
        {
            await SetSandboxStatusAsync(moduleId, ModuleSandboxStatus.LegacyOverride, cancellationToken);
            await AuditLegacyOverrideAsync(scope, moduleId, cancellationToken);
            return Entry(ModuleAdoptionOutcome.LegacyOverride, result.Error);
        }

        await SetSandboxStatusAsync(moduleId, ModuleSandboxStatus.Error, cancellationToken);
        return Entry(ModuleAdoptionOutcome.Failed, result.Error);
    }

    private async Task SetSandboxStatusAsync(
        string moduleId, ModuleSandboxStatus status, CancellationToken cancellationToken)
    {
        using var scope = _scopeFactory.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<CoreDbContext>();

        var row = await dbContext.InstalledModules
            .AsTracking()
            .FirstOrDefaultAsync(m => m.ModuleId == moduleId, cancellationToken);

        if (row is null || row.SandboxStatus == status)
        {
            return;
        }

        row.SandboxStatus = status;
        row.UpdatedAt = DateTime.UtcNow;
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private async Task AuditLegacyOverrideAsync(
        IServiceScope scope, string moduleId, CancellationToken cancellationToken)
    {
        try
        {
            var auditLogger = scope.ServiceProvider.GetRequiredService<IAuditLogger>();
            await auditLogger.LogAsync(
                new AuditEntry
                {
                    Caller = CallerContext.CreateSystemContext(),
                    ModuleId = "dotnetcloud.core",
                    Action = AuditAction.ModuleLegacyAccessUsed,
                    EntityType = "Module",
                    EntityId = Guid.CreateVersion7(),
                    Description = $"module-legacy-access-used:{moduleId}",
                },
                cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogWarning(
                ex, "Could not audit the legacy-access override for module {ModuleId}", LogSanitizer.Sanitize(moduleId));
        }
    }
}
