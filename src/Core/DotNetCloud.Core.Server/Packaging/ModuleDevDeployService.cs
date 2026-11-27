using System.Security.Cryptography;
using System.Text.Json;
using DotNetCloud.Core.Capabilities;
using DotNetCloud.Core.Data.Context;
using DotNetCloud.Core.Data.Entities.Modules;
using DotNetCloud.Core.Modules;
using DotNetCloud.Core.Modules.Supervisor;
using DotNetCloud.Core.Server.ModuleLoading;
using DotNetCloud.Core.Server.Sandbox;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace DotNetCloud.ModulePackaging;

/// <summary>
/// One <c>dotnetcloud module dev</c> iteration: replace an installed module's binaries with a local
/// build, without changing its identity (schema, data directory, Data Protection ring, credentials).
/// </summary>
public sealed record ModuleDevDeployRequest
{
    /// <summary>The module id the build belongs to (from its <c>manifest.json</c>).</summary>
    public required string ModuleId { get; init; }

    /// <summary>The directory <c>dotnet publish</c> wrote the module host into.</summary>
    public required string PublishedDirectory { get; init; }

    /// <summary>Where the build came from; recorded in the state file and the audit entry.</summary>
    public string? SourcePath { get; init; }

    /// <summary>Permit a deploy whose migration assemblies changed since the last one.</summary>
    public bool AllowSchemaChange { get; init; }

    /// <summary>The operator, when the caller knows one; the audit row falls back to a system caller.</summary>
    public Guid? PerformedByUserId { get; init; }
}

/// <summary>The outcome of one dev deploy.</summary>
/// <param name="Success">Whether the new build is deployed and running.</param>
/// <param name="Deployed">Whether anything was deployed; <see langword="false"/> means "already current".</param>
/// <param name="Error">Why the deploy failed, for an operator.</param>
/// <param name="LogTail">Captured module log tail when the new build failed to come up.</param>
public sealed record ModuleDevDeployResult(bool Success, bool Deployed, string? Error = null, string? LogTail = null)
{
    /// <summary>A deploy that changed nothing because the build matched the last one.</summary>
    public static ModuleDevDeployResult Unchanged() => new(true, Deployed: false);
}

/// <summary>
/// Stops a module, and starts it again, through whatever owns the module process.
/// </summary>
/// <remarks>
/// The deploy service never spawns a module: the supervisor owns every module process. This seam is
/// therefore also where "wait until it is really stopped" and "wait until it is healthy again" live,
/// and it differs per host — in the server it is the supervisor itself, in the CLI it is a poll of the
/// readiness report the server publishes.
/// </remarks>
public interface IModuleDevProcessControl
{
    /// <summary>Waits until the module process is no longer running.</summary>
    /// <param name="moduleId">The module id.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <exception cref="TimeoutException">The process did not stop in time.</exception>
    Task WaitForStoppedAsync(string moduleId, CancellationToken cancellationToken = default);

    /// <summary>Waits until the module reports healthy.</summary>
    /// <param name="moduleId">The module id.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns><see langword="null"/> when healthy, otherwise why it is not.</returns>
    Task<string?> WaitForHealthyAsync(string moduleId, CancellationToken cancellationToken = default);
}

/// <summary>The build a module directory is currently running, as recorded under <c>.dev/&lt;id&gt;</c>.</summary>
public sealed record ModuleDevState
{
    /// <summary>The module id.</summary>
    public required string ModuleId { get; init; }

    /// <summary>The source path the build came from.</summary>
    public string? SourcePath { get; init; }

    /// <summary>SHA-256 over the deployed tree (relative path, length and content of every file).</summary>
    public required string ContentHash { get; init; }

    /// <summary>SHA-256 over the migration assemblies of the deployed tree.</summary>
    public required string MigrationFingerprint { get; init; }

    /// <summary>When the deploy happened (UTC).</summary>
    public required DateTime DeployedAtUtc { get; init; }
}

/// <summary>Deploys a locally built module over an installed one, for development.</summary>
public interface IModuleDevDeployService
{
    /// <summary>Deploys a published build over the installed module.</summary>
    /// <param name="request">The build to deploy.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The outcome.</returns>
    Task<ModuleDevDeployResult> DeployAsync(
        ModuleDevDeployRequest request, CancellationToken cancellationToken = default);

    /// <summary>Puts the previous dev build back.</summary>
    /// <param name="moduleId">The module id.</param>
    /// <param name="performedByUserId">The operator, when known.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The outcome.</returns>
    Task<ModuleDevDeployResult> RestoreAsync(
        string moduleId, Guid? performedByUserId = null, CancellationToken cancellationToken = default);

    /// <summary>Reads the recorded dev build for a module, if it is running one.</summary>
    /// <param name="moduleId">The module id.</param>
    /// <returns>The recorded state, or <see langword="null"/> when the module has no dev build.</returns>
    ModuleDevState? ReadState(string moduleId);
}

/// <summary>
/// The dev deploy service: publish output in, running module out.
/// </summary>
/// <remarks>
/// <para>
/// Everything dev owns lives under <c>&lt;modules&gt;/.dev/&lt;id&gt;/</c>: the staged build, the one
/// previous build and <c>state.json</c>. That root starts with a dot, so module discovery never sees a
/// half-swapped tree, and an operator can delete a whole workspace.
/// </para>
/// <para>
/// The swap never touches <c>.previous/</c>, which is what <c>dotnetcloud module rollback</c>
/// restores: a dev build must never become a rollback target.
/// </para>
/// <para>
/// Lifecycle is still database-driven. A deploy writes <see cref="ModuleInstallStatus.Disabled"/>,
/// waits, swaps, writes <see cref="ModuleInstallStatus.Enabled"/> and waits; the supervisor applies
/// both because the CLI never spawns a module.
/// </para>
/// </remarks>
public sealed class ModuleDevDeployService : IModuleDevDeployService
{
    private static readonly JsonSerializerOptions StateJsonOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true,
    };

    private readonly CoreDbContext _db;
    private readonly IModuleDevProcessControl _processControl;
    private readonly IModuleDataStoreProvisioningService _dataStores;
    private readonly IAuditLogger? _auditLogger;
    private readonly IOptions<ProcessSupervisorOptions> _supervisorOptions;
    private readonly ILogger<ModuleDevDeployService> _logger;

    /// <summary>Initializes a new instance of the <see cref="ModuleDevDeployService"/> class.</summary>
    /// <param name="db">The core database context, for the installation record and the audit trail.</param>
    /// <param name="processControl">Stops and starts the module through whoever owns the process.</param>
    /// <param name="dataStores">Provisions the isolated schema when the module is not installed yet.</param>
    /// <param name="supervisorOptions">Supplies the modules directory.</param>
    /// <param name="logger">Logger.</param>
    /// <param name="auditLogger">Audit sink; the deploy is still performed without one.</param>
    public ModuleDevDeployService(
        CoreDbContext db,
        IModuleDevProcessControl processControl,
        IModuleDataStoreProvisioningService dataStores,
        IOptions<ProcessSupervisorOptions> supervisorOptions,
        ILogger<ModuleDevDeployService> logger,
        IAuditLogger? auditLogger = null)
    {
        _db = db ?? throw new ArgumentNullException(nameof(db));
        _processControl = processControl ?? throw new ArgumentNullException(nameof(processControl));
        _dataStores = dataStores ?? throw new ArgumentNullException(nameof(dataStores));
        _supervisorOptions = supervisorOptions ?? throw new ArgumentNullException(nameof(supervisorOptions));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _auditLogger = auditLogger;
    }

    /// <inheritdoc/>
    public async Task<ModuleDevDeployResult> DeployAsync(
        ModuleDevDeployRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var publishedDirectory = Path.GetFullPath(request.PublishedDirectory);
        if (!Directory.Exists(publishedDirectory))
        {
            return new ModuleDevDeployResult(
                false, Deployed: false, Error: $"The build output '{publishedDirectory}' does not exist.");
        }

        var layout = new ModuleInstallLayout(_supervisorOptions.Value);
        var record = await FindInstallationAsync(request.ModuleId, cancellationToken);
        var isFirstInstall = record is null;

        if (record is null)
        {
            var prepared = await PrepareFirstInstallAsync(request, publishedDirectory, cancellationToken);
            record = prepared.Record;

            if (record is null)
            {
                return new ModuleDevDeployResult(false, Deployed: false, Error: prepared.Error);
            }
        }

        var contentHash = HashTree(publishedDirectory);
        var migrationFingerprint = MigrationFingerprint(publishedDirectory);
        var state = ReadState(request.ModuleId);

        if (state is not null && string.Equals(state.ContentHash, contentHash, StringComparison.Ordinal))
        {
            return ModuleDevDeployResult.Unchanged();
        }

        if (state is not null
            && !string.Equals(state.MigrationFingerprint, migrationFingerprint, StringComparison.Ordinal)
            && !request.AllowSchemaChange)
        {
            return new ModuleDevDeployResult(
                false, Deployed: false,
                Error: "This build carries different migration assemblies than the deployed one "
                       + "(the module's schema would change). Take a backup of the module data and "
                       + "re-run with --allow-schema-change.");
        }

        // 1. Stage the build outside the live directory.
        var staged = CopyToStaging(publishedDirectory, layout.CreateDevStagingRoot(request.ModuleId));

        // 2. Stop the module through the database, and wait for the process to release its files.
        string? previousDirectory = null;
        try
        {
            await SetStatusAsync(record, ModuleInstallStatus.Disabled, cancellationToken);

            try
            {
                await _processControl.WaitForStoppedAsync(request.ModuleId, cancellationToken);
            }
            catch (TimeoutException ex)
            {
                layout.DeleteStaging(staged);
                return await FailAsync(
                    record, request, "dev-deploy:stop-timeout", ex.Message, isFirstInstall, cancellationToken);
            }

            // 3. Swap: live -> .dev/<id>/previous, staged -> live.
            try
            {
                previousDirectory = layout.MoveLiveToDevPrevious(request.ModuleId);
                layout.Promote(staged, request.ModuleId);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                layout.Restore(previousDirectory, request.ModuleId);
                layout.DeleteStaging(staged);
                return await FailAsync(
                    record, request, "dev-deploy:swap-failed",
                    "Could not replace the module directory: " + ex.Message, isFirstInstall, cancellationToken);
            }
        }
        catch (TimeoutException)
        {
            throw;
        }

        WriteState(new ModuleDevState
        {
            ModuleId = request.ModuleId,
            SourcePath = request.SourcePath,
            ContentHash = contentHash,
            MigrationFingerprint = migrationFingerprint,
            DeployedAtUtc = DateTime.UtcNow,
        }, layout);

        // A module that is being installed for the first time has no settings yet; seed the manifest
        // defaults the way an install does, so the first request sees them. Additive only.
        try
        {
            await ModuleSettingDefaultsSeeder.SeedFromModuleDirectoryAsync(
                _db, request.ModuleId, layout.GetModuleDirectory(request.ModuleId), _logger, cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogWarning(
                ex, "Failed to seed default settings for module {ModuleId}", request.ModuleId);
        }

        // 4. Start it again and wait for it to report healthy.
        await SetStatusAsync(record, ModuleInstallStatus.Enabled, cancellationToken);
        var unhealthy = await _processControl.WaitForHealthyAsync(request.ModuleId, cancellationToken);

        if (unhealthy is null)
        {
            _logger.LogInformation(
                "Dev build {ContentHash} of module {ModuleId} is running",
                contentHash, request.ModuleId);
            await AuditAsync(
                request.ModuleId,
                request.PerformedByUserId,
                isFirstInstall ? $"dev-install:{contentHash}" : $"dev-deploy:{contentHash}",
                cancellationToken,
                isFirstInstall ? AuditAction.ModuleInstalled : AuditAction.ModuleUpdated);
            return new ModuleDevDeployResult(true, Deployed: true);
        }

        // 5. The new build never came up: put the previous one back and leave it running.
        _logger.LogWarning(
            "Dev build {ContentHash} of module {ModuleId} did not start: {Reason}",
            contentHash, request.ModuleId, unhealthy);

        await SetStatusAsync(record, ModuleInstallStatus.Disabled, cancellationToken);
        await TryWaitForStoppedAsync(request.ModuleId, cancellationToken);

        try
        {
            layout.Restore(previousDirectory, request.ModuleId);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            _logger.LogError(ex, "Could not restore the previous build of module {ModuleId}", request.ModuleId);
        }

        if (state is not null)
        {
            WriteState(state, layout);
        }
        else
        {
            DeleteState(layout, request.ModuleId);
        }

        await SetStatusAsync(record, ModuleInstallStatus.Enabled, cancellationToken);
        await TryWaitForHealthyAsync(request.ModuleId, cancellationToken);

        await AuditAsync(
            request.ModuleId, request.PerformedByUserId,
            $"dev-deploy-failed:{contentHash}", cancellationToken);

        return new ModuleDevDeployResult(
            false, Deployed: false,
            Error: unhealthy + " The previous build was restored.",
            LogTail: null);
    }

    /// <inheritdoc/>
    public async Task<ModuleDevDeployResult> RestoreAsync(
        string moduleId, Guid? performedByUserId = null, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(moduleId);

        var record = await FindInstallationAsync(moduleId, cancellationToken);
        if (record is null)
        {
            return new ModuleDevDeployResult(false, Deployed: false, Error: $"Module '{moduleId}' is not installed.");
        }

        var layout = new ModuleInstallLayout(_supervisorOptions.Value);
        var previous = layout.GetDevPreviousDirectory(moduleId);

        if (!Directory.Exists(previous))
        {
            return new ModuleDevDeployResult(
                false, Deployed: false,
                Error: $"There is no previous dev build of '{moduleId}' to restore. "
                       + "Only a dev deploy creates one; a catalogue install does not.");
        }

        await SetStatusAsync(record, ModuleInstallStatus.Disabled, cancellationToken);
        try
        {
            await _processControl.WaitForStoppedAsync(moduleId, cancellationToken);
        }
        catch (TimeoutException ex)
        {
            await SetStatusAsync(record, ModuleInstallStatus.Enabled, cancellationToken);
            return new ModuleDevDeployResult(false, Deployed: false, Error: ex.Message);
        }

        string? displaced = null;
        try
        {
            // The build being replaced is discarded, not retained: the retained slot already holds the
            // build we are restoring.
            var live = layout.GetModuleDirectory(moduleId);
            if (Directory.Exists(live))
            {
                displaced = Path.Combine(layout.CreateDevStagingRoot(moduleId), "displaced");
                Directory.Move(live, displaced);
            }

            layout.Promote(previous, moduleId);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            layout.Restore(displaced, moduleId);
            await SetStatusAsync(record, ModuleInstallStatus.Enabled, cancellationToken);
            return new ModuleDevDeployResult(
                false, Deployed: false, Error: "Could not restore the previous build: " + ex.Message);
        }

        // The restored tree is unknown until it is hashed again, so forget the recorded build: the
        // next deploy starts from a fresh fingerprint instead of refusing a schema change it cannot
        // compare against.
        DeleteState(layout, moduleId);

        await SetStatusAsync(record, ModuleInstallStatus.Enabled, cancellationToken);
        var unhealthy = await _processControl.WaitForHealthyAsync(moduleId, cancellationToken);

        await AuditAsync(moduleId, performedByUserId, "dev-restore", cancellationToken);

        return unhealthy is null
            ? new ModuleDevDeployResult(true, Deployed: true)
            : new ModuleDevDeployResult(false, Deployed: true, Error: unhealthy);
    }

    /// <inheritdoc/>
    public ModuleDevState? ReadState(string moduleId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(moduleId);

        var layout = new ModuleInstallLayout(_supervisorOptions.Value);
        var path = layout.GetDevStateFilePath(moduleId);

        try
        {
            return File.Exists(path)
                ? JsonSerializer.Deserialize<ModuleDevState>(File.ReadAllText(path), StateJsonOptions)
                : null;
        }
        catch (Exception ex) when (ex is IOException or JsonException or UnauthorizedAccessException)
        {
            _logger.LogWarning(ex, "Ignoring an unreadable dev state file for module {ModuleId}", moduleId);
            return null;
        }
    }

    private async Task<ModuleDevDeployResult> FailAsync(
        InstalledModule record,
        ModuleDevDeployRequest request,
        string auditDescription,
        string error,
        bool isFirstInstall,
        CancellationToken cancellationToken)
    {
        if (isFirstInstall)
        {
            // Nothing was ever installed, so there is nothing to leave enabled: a row without a
            // module directory would only show up as a broken install.
            _db.InstalledModules.Remove(record);
            await _db.SaveChangesAsync(cancellationToken);
        }
        else
        {
            // A failed deploy leaves the previous build in place, so the module goes back to Enabled.
            await SetStatusAsync(record, ModuleInstallStatus.Enabled, cancellationToken);
        }

        await AuditAsync(request.ModuleId, request.PerformedByUserId, auditDescription, cancellationToken);
        return new ModuleDevDeployResult(false, Deployed: false, Error: error);
    }

    /// <summary>
    /// Prepares a module that has never been installed: validate the build's manifest, provision its
    /// isolated schema when it declares one, and create the installation row.
    /// </summary>
    /// <remarks>
    /// A dev deploy is allowed to be the first install, so this mirrors the installer's first-install
    /// steps without the package: nothing is verified or signed, which is why the command only runs in
    /// Development and audits every deploy.
    /// </remarks>
    private async Task<(InstalledModule? Record, string? Error)> PrepareFirstInstallAsync(
        ModuleDevDeployRequest request, string publishedDirectory, CancellationToken cancellationToken)
    {
        if (RequiredModules.IsRequired(request.ModuleId))
        {
            return (null,
                $"'{request.ModuleId}' is a required platform module: it ships with the core, cannot be "
                + "stopped, and is not developed with 'module dev'.");
        }

        var manifestPath = Path.Combine(publishedDirectory, ModulePackageFormat.ManifestEntryName);
        var validation = new ModuleManifestLoader(NullLogger<ModuleManifestLoader>.Instance)
            .LoadAndValidate(manifestPath, request.ModuleId);

        if (!validation.IsValid || validation.Manifest is null)
        {
            return (null,
                "The build's manifest.json is not valid, so the module cannot be installed: "
                + string.Join("; ", validation.Errors));
        }

        var manifest = validation.Manifest;

        if (manifest.EffectiveDataStoreMode == ModuleDataStoreMode.Isolated)
        {
            var provisioning = await _dataStores.EnsureProvisionedAsync(
                request.ModuleId, cancellationToken: cancellationToken);

            if (!provisioning.Success)
            {
                return (null,
                    "Provisioning the module's data store failed: "
                    + (provisioning.Error ?? "the provisioning service gave no reason"));
            }
        }

        var record = new InstalledModule
        {
            ModuleId = request.ModuleId,
            Version = manifest.Version,
            InstalledAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
            // Not Store: nothing here was verified or signed. Bundled is the tier whose capabilities
            // are granted from the manifest, which is what keeps the loop usable; the row can only be
            // created by a command that refuses to run outside Development.
            TrustTier = ModuleTrustTier.Bundled,
            DataStoreMode = manifest.EffectiveDataStoreMode,
            SandboxStatus = ModuleSandboxStatus.Active,
            Status = ModuleInstallStatus.Disabled,
        };

        _db.InstalledModules.Add(record);
        await _db.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "Installing module {ModuleId} {Version} from a local build",
            request.ModuleId, manifest.Version);

        return (record, null);
    }

    private async Task<InstalledModule?> FindInstallationAsync(string moduleId, CancellationToken cancellationToken) =>
        await _db.InstalledModules
            .AsTracking()
            .FirstOrDefaultAsync(module => module.ModuleId == moduleId, cancellationToken);

    private async Task SetStatusAsync(
        InstalledModule record, string status, CancellationToken cancellationToken)
    {
        if (string.Equals(record.Status, status, StringComparison.Ordinal))
        {
            return;
        }

        record.Status = status;
        record.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(cancellationToken);
    }

    private async Task TryWaitForStoppedAsync(string moduleId, CancellationToken cancellationToken)
    {
        try
        {
            await _processControl.WaitForStoppedAsync(moduleId, cancellationToken);
        }
        catch (TimeoutException ex)
        {
            _logger.LogWarning(ex, "Module {ModuleId} did not stop in time during rollback", moduleId);
        }
    }

    private async Task TryWaitForHealthyAsync(string moduleId, CancellationToken cancellationToken)
    {
        try
        {
            var reason = await _processControl.WaitForHealthyAsync(moduleId, cancellationToken);
            if (reason is not null)
            {
                _logger.LogWarning("Restored module {ModuleId} is not healthy: {Reason}", moduleId, reason);
            }
        }
        catch (TimeoutException ex)
        {
            _logger.LogWarning(ex, "Module {ModuleId} did not become healthy after rollback", moduleId);
        }
    }

    private async Task AuditAsync(
        string moduleId,
        Guid? performedByUserId,
        string description,
        CancellationToken cancellationToken,
        AuditAction action = AuditAction.ModuleUpdated)
    {
        if (_auditLogger is null)
        {
            return;
        }

        try
        {
            await _auditLogger.LogAsync(
                new AuditEntry
                {
                    Caller = performedByUserId is { } userId
                        ? new DotNetCloud.Core.Authorization.CallerContext(
                            userId, Array.Empty<string>(), DotNetCloud.Core.Authorization.CallerType.User)
                        : DotNetCloud.Core.Authorization.CallerContext.CreateSystemContext(),
                    ModuleId = "dotnetcloud.core",
                    Action = action,
                    EntityType = "Module",
                    EntityId = Guid.CreateVersion7(),
                    Description = description,
                },
                cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // An audit sink that is unavailable must not undo a deploy that already happened.
            _logger.LogWarning(ex, "Could not audit the dev deploy of module {ModuleId}", moduleId);
        }
    }

    private void WriteState(ModuleDevState state, ModuleInstallLayout layout)
    {
        var path = layout.GetDevStateFilePath(state.ModuleId);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, JsonSerializer.Serialize(state, StateJsonOptions));
    }

    private static void DeleteState(ModuleInstallLayout layout, string moduleId)
    {
        var path = layout.GetDevStateFilePath(moduleId);
        if (File.Exists(path))
        {
            File.Delete(path);
        }
    }

    private static string CopyToStaging(string source, string staging)
    {
        foreach (var file in Directory.EnumerateFiles(source, "*", SearchOption.AllDirectories))
        {
            var relative = Path.GetRelativePath(source, file);
            var target = Path.Combine(staging, relative);
            Directory.CreateDirectory(Path.GetDirectoryName(target)!);
            File.Copy(file, target, overwrite: true);
        }

        return staging;
    }

    /// <summary>
    /// Hashes the deployed tree: relative path, length and content of every file, in ordinal order.
    /// </summary>
    /// <param name="directory">The tree to hash.</param>
    /// <returns>Lower-case hex SHA-256.</returns>
    public static string HashTree(string directory)
    {
        using var aggregate = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);

        foreach (var file in Directory.EnumerateFiles(directory, "*", SearchOption.AllDirectories)
                     .Select(path => (Path: path, Relative: Path.GetRelativePath(directory, path).Replace('\\', '/')))
                     .OrderBy(entry => entry.Relative, StringComparer.Ordinal))
        {
            aggregate.AppendData(System.Text.Encoding.UTF8.GetBytes(file.Relative));

            using var stream = File.OpenRead(file.Path);
            var buffer = new byte[81920];
            int read;
            while ((read = stream.Read(buffer, 0, buffer.Length)) > 0)
            {
                aggregate.AppendData(buffer, 0, read);
            }
        }

        return Convert.ToHexStringLower(aggregate.GetHashAndReset());
    }

    /// <summary>
    /// Hashes the migration assemblies of a published module tree.
    /// </summary>
    /// <remarks>
    /// The modules put their migrations in <c>DotNetCloud.Modules.&lt;Name&gt;.Data*</c>, so a change in
    /// that set is the only signal available without compiling the build. A model change without a new
    /// migration is invisible here by design; the documented backup step covers that case.
    /// </remarks>
    /// <param name="directory">The published tree.</param>
    /// <returns>Lower-case hex SHA-256, or an empty marker when the module has no data assembly.</returns>
    public static string MigrationFingerprint(string directory)
    {
        var assemblies = Directory.EnumerateFiles(directory, "*.dll", SearchOption.AllDirectories)
            .Where(path => Path.GetFileName(path).StartsWith("DotNetCloud.Modules.", StringComparison.Ordinal)
                           && Path.GetFileNameWithoutExtension(path).Contains(".Data", StringComparison.Ordinal))
            .Select(path => (Name: Path.GetFileName(path), Path: path))
            .OrderBy(entry => entry.Name, StringComparer.Ordinal)
            .ToList();

        if (assemblies.Count == 0)
        {
            return "none";
        }

        using var aggregate = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        foreach (var (name, path) in assemblies)
        {
            aggregate.AppendData(System.Text.Encoding.UTF8.GetBytes(name));
            aggregate.AppendData(File.ReadAllBytes(path));
        }

        return Convert.ToHexStringLower(aggregate.GetHashAndReset());
    }
}
