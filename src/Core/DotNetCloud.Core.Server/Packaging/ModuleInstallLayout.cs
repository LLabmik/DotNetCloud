using DotNetCloud.Core.Modules.Supervisor;
using DotNetCloud.Core.Server.Supervisor;

namespace DotNetCloud.ModulePackaging;

/// <summary>
/// Resolves the directories the installer works in and performs the contained moves between them.
/// </summary>
/// <remarks>
/// <code>
/// &lt;modules&gt;/
/// ├── &lt;moduleId&gt;/                        live install (what the supervisor discovers)
/// ├── .staging/&lt;moduleId&gt;/&lt;guid&gt;/         in-flight extraction (never discovered)
/// └── .previous/&lt;moduleId&gt;/&lt;version&gt;/     one retained generation, for rollback
/// </code>
/// Both working roots start with a dot, and the discovery service skips dot-directories, so an
/// in-flight or retained tree can never be mistaken for an installed module.
/// </remarks>
internal sealed class ModuleInstallLayout
{
    private const string StagingDirectoryName = ".staging";
    private const string PreviousDirectoryName = ".previous";
    private const string ModulesDirectoryEnvironmentVariable = "DOTNETCLOUD_MODULES_DIR";

    /// <summary>Initializes a new instance of the <see cref="ModuleInstallLayout"/> class.</summary>
    /// <param name="options">Supervisor options supplying the modules directory.</param>
    public ModuleInstallLayout(ProcessSupervisorOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        var configured = Environment.GetEnvironmentVariable(ModulesDirectoryEnvironmentVariable);
        if (string.IsNullOrWhiteSpace(configured))
        {
            configured = string.IsNullOrWhiteSpace(options.ModulesDirectory) ? "modules" : options.ModulesDirectory;
        }

        ModulesRoot = Path.IsPathRooted(configured)
            ? Path.GetFullPath(configured)
            : Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, configured));
    }

    /// <summary>Gets the root that holds the live module directories.</summary>
    public string ModulesRoot { get; }

    /// <summary>
    /// Creates a fresh staging root for one install attempt.
    /// </summary>
    /// <param name="moduleId">The module id.</param>
    /// <returns>The empty staging directory (contained under <see cref="ModulesRoot"/>).</returns>
    public string CreateStagingRoot(string moduleId)
    {
        var root = Path.Combine(ModulesRoot, StagingDirectoryName, moduleId, Guid.CreateVersion7().ToString("N"));
        Directory.CreateDirectory(root);
        return root;
    }

    /// <summary>
    /// Moves the current live install into the retained-previous slot.
    /// </summary>
    /// <param name="moduleId">The module id.</param>
    /// <param name="version">The version being replaced, used as the retained slot name.</param>
    /// <param name="keepVersionIdentity">
    /// When <see langword="false"/> the directory is parked in staging instead of the rollback slot
    /// (used by an explicit rollback, which discards the version it replaces).
    /// </param>
    /// <returns>The parked directory, or <see langword="null"/> when there was nothing to move.</returns>
    public string? ArchiveCurrent(string moduleId, string? version, bool keepVersionIdentity = true)
    {
        var live = GetModuleDirectory(moduleId);
        if (!Directory.Exists(live))
        {
            return null;
        }

        string target;
        if (keepVersionIdentity)
        {
            // Exactly one generation is retained, so stale slots are cleared first.
            var previousRoot = Path.Combine(ModulesRoot, PreviousDirectoryName, moduleId);
            if (Directory.Exists(previousRoot))
            {
                Directory.Delete(previousRoot, recursive: true);
            }

            target = Path.Combine(previousRoot, version ?? "unknown");
        }
        else
        {
            target = Path.Combine(CreateStagingRoot(moduleId), "displaced");
        }

        Directory.CreateDirectory(Path.GetDirectoryName(target)!);
        Directory.Move(live, target);
        return target;
    }

    /// <summary>
    /// Promotes a staged install root to the live module directory.
    /// </summary>
    /// <param name="stagedRoot">The staged install tree.</param>
    /// <param name="moduleId">The module id (the live directory name).</param>
    public void Promote(string stagedRoot, string moduleId)
    {
        var live = GetModuleDirectory(moduleId);

        if (Directory.Exists(live))
        {
            throw new IOException($"'{live}' already exists; the current install must be archived first.");
        }

        Directory.CreateDirectory(ModulesRoot);
        Directory.Move(stagedRoot, live);
    }

    /// <summary>
    /// Promotes the retained version back to the live module directory.
    /// </summary>
    /// <param name="moduleId">The module id.</param>
    /// <param name="retainedDirectory">The retained version directory.</param>
    public void PromotePrevious(string moduleId, string retainedDirectory)
    {
        var live = GetModuleDirectory(moduleId);

        if (Directory.Exists(live))
        {
            throw new IOException($"'{live}' already exists; the current install must be archived first.");
        }

        Directory.Move(retainedDirectory, live);

        // The slot is now empty; remove it so a second rollback reports "nothing retained".
        var previousRoot = Path.Combine(ModulesRoot, PreviousDirectoryName, moduleId);
        TryDeleteDirectory(previousRoot);
    }

    /// <summary>
    /// Restores a parked directory to the live module directory, replacing whatever is there.
    /// </summary>
    /// <param name="parkedDirectory">The directory returned by <see cref="ArchiveCurrent"/>.</param>
    /// <param name="moduleId">The module id.</param>
    public void Restore(string? parkedDirectory, string moduleId)
    {
        var live = GetModuleDirectory(moduleId);

        if (parkedDirectory is null || !Directory.Exists(parkedDirectory))
        {
            return;
        }

        TryDeleteDirectory(live);
        Directory.Move(parkedDirectory, live);
    }

    /// <summary>
    /// Finds the single retained version of a module.
    /// </summary>
    /// <param name="moduleId">The module id.</param>
    /// <returns>The retained directory and version, or <see langword="null"/> when there is none.</returns>
    public (string Directory, string Version)? FindPrevious(string moduleId)
    {
        var previousRoot = Path.Combine(ModulesRoot, PreviousDirectoryName, moduleId);

        if (!Directory.Exists(previousRoot))
        {
            return null;
        }

        var directories = Directory.GetDirectories(previousRoot);
        if (directories.Length != 1)
        {
            return null;
        }

        return (directories[0], Path.GetFileName(directories[0]));
    }

    /// <summary>Deletes a staging tree (or a parked directory) that is no longer needed.</summary>
    /// <param name="path">The directory to remove.</param>
    public void DeleteStaging(string? path)
    {
        if (path is null)
        {
            return;
        }

        TryDeleteDirectory(path);
    }

    /// <summary>Deletes the live module directory of a module that failed its first install.</summary>
    /// <param name="moduleId">The module id.</param>
    public void DeleteLive(string moduleId) => TryDeleteDirectory(GetModuleDirectory(moduleId));

    /// <summary>
    /// Resolves <c>&lt;ModulesRoot&gt;/&lt;moduleId&gt;</c>, refusing to escape the root.
    /// </summary>
    /// <param name="moduleId">The module id.</param>
    /// <returns>The absolute module directory.</returns>
    /// <exception cref="ModulePackageException">The id would resolve outside the modules root.</exception>
    public string GetModuleDirectory(string moduleId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(moduleId);

        var candidate = Path.Combine(ModulesRoot, moduleId);
        if (!PathContainment.TryResolveWithin(ModulesRoot, candidate, out var resolved))
        {
            throw new ModulePackageException($"Module id '{moduleId}' resolves outside the modules directory.");
        }

        return resolved;
    }

    private static void TryDeleteDirectory(string path)
    {
        try
        {
            if (Directory.Exists(path))
            {
                Directory.Delete(path, recursive: true);
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // Best effort: a leftover staging tree is not an install failure.
        }
    }
}
