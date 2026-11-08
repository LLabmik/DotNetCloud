using DotNetCloud.Core.Modules.Store;
using DotNetCloud.Core.Modules.Supervisor;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace DotNetCloud.ModulePackaging;

/// <summary>
/// The first-release module store client: a local directory holding <c>index.json</c>, the signed
/// package files and an optional <c>yanked.json</c>.
/// </summary>
/// <remarks>
/// <para>
/// Remote index fetch, caching, mirrors and automatic update checks are deferred (decision D6). This
/// implementation exists so the installer can be written once against
/// <see cref="IModuleStoreService"/>: the deferred client replaces this class without touching the
/// verification or install path.
/// </para>
/// <para>
/// The index is data, not authority. Every path it names is resolved with a containment check, and
/// every package is re-verified from its bytes on install; the index's hash is only used to report
/// a mismatch early.
/// </para>
/// </remarks>
public sealed class LocalModuleFeedStoreService : IModuleStoreService
{
    private readonly ModuleStoreOptions _options;
    private readonly ProcessSupervisorOptions _supervisorOptions;
    private readonly ILogger<LocalModuleFeedStoreService> _logger;

    /// <summary>Initializes a new instance of the <see cref="LocalModuleFeedStoreService"/> class.</summary>
    /// <param name="options">The <c>Modules:Store</c> options.</param>
    /// <param name="supervisorOptions">Supervisor options, used only to resolve the default feed location.</param>
    /// <param name="logger">Logger.</param>
    public LocalModuleFeedStoreService(
        IOptions<ModuleStoreOptions> options,
        IOptions<ProcessSupervisorOptions> supervisorOptions,
        ILogger<LocalModuleFeedStoreService> logger)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(supervisorOptions);

        _options = options.Value;
        _supervisorOptions = supervisorOptions.Value;
        _logger = logger;
    }

    /// <inheritdoc/>
    public ModuleStoreSourceKind SourceKind => ModuleStoreSourceKind.LocalFeed;

    /// <inheritdoc/>
    public string SourceId => ResolveFeedDirectory();

    /// <inheritdoc/>
    public async Task<IReadOnlyList<ModuleStorePackage>> ListPackagesAsync(
        CancellationToken cancellationToken = default)
    {
        var root = ResolveFeedDirectory();

        if (!Directory.Exists(root))
        {
            _logger.LogInformation("Local module feed {FeedPath} does not exist", root);
            return [];
        }

        var index = await ModuleFeed.ReadAsync(root, cancellationToken);
        var yanked = await ModuleFeed.ReadYankedAsync(root, cancellationToken);

        return index.Packages
            .Select(entry => Map(entry, yanked))
            .OrderBy(package => package.ModuleId, StringComparer.Ordinal)
            .ThenBy(package => package.Version, StringComparer.Ordinal)
            .ToList()
            .AsReadOnly();
    }

    /// <inheritdoc/>
    public async Task<ModuleStoreResolution?> ResolveAsync(
        string moduleId,
        string? version = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(moduleId);

        var root = ResolveFeedDirectory();
        var index = await ModuleFeed.ReadAsync(root, cancellationToken);

        var candidates = index.Packages
            .Where(entry => string.Equals(entry.Id, moduleId, StringComparison.Ordinal))
            .ToList();

        if (candidates.Count == 0)
        {
            return null;
        }

        ModuleFeedEntry entry;
        if (string.IsNullOrWhiteSpace(version))
        {
            entry = candidates
                .OrderByDescending(candidate => candidate.Version, ModuleStoreVersionComparer.Instance)
                .First();
        }
        else
        {
            var match = candidates.FirstOrDefault(candidate =>
                string.Equals(candidate.Version, version, StringComparison.Ordinal));

            if (match is null)
            {
                return null;
            }

            entry = match;
        }

        var packagePath = Path.GetFullPath(Path.Combine(root, entry.File));

        // The index is untrusted data: the file it names must resolve inside the feed.
        if (!ModuleStorePaths.IsInside(root, packagePath))
        {
            throw new ModulePackageException($"Feed entry '{entry.File}' resolves outside the feed directory.");
        }

        if (!File.Exists(packagePath))
        {
            throw new ModulePackageException($"Feed entry '{entry.File}' names a package that does not exist.");
        }

        var signaturePath = ModulePackageFormat.GetSignaturePath(packagePath);
        var yanked = await ModuleFeed.ReadYankedAsync(root, cancellationToken);

        return new ModuleStoreResolution(
            Map(entry, yanked),
            packagePath,
            File.Exists(signaturePath) ? signaturePath : null);
    }

    private static ModuleStorePackage Map(
        ModuleFeedEntry entry, IReadOnlyList<ModuleFeedYankedVersion> yanked)
    {
        var yank = yanked.FirstOrDefault(candidate =>
            string.Equals(candidate.Id, entry.Id, StringComparison.Ordinal)
            && string.Equals(candidate.Version, entry.Version, StringComparison.Ordinal));

        return new ModuleStorePackage(
            entry.Id,
            entry.Name,
            entry.Version,
            entry.File,
            entry.Size,
            entry.Sha256,
            entry.PublisherId,
            entry.Capabilities ?? [],
            entry.UserConsentScopes ?? [],
            entry.KeyId,
            // A local feed carries no publisher registry and no review record: whoever built the feed
            // decided what to put in it. That is the community tier at best, and the operator's key
            // pinning (not this value) is what actually decides whether an install is trusted.
            ModuleStoreTrustTier.Community,
            yank is not null,
            yank?.Reason,
            yank?.Severity);
    }

    /// <summary>
    /// Resolves the feed directory: an explicit <c>Modules:Store:LocalFeedPath</c>, otherwise
    /// <c>store-feed</c> beside the modules directory.
    /// </summary>
    private string ResolveFeedDirectory()
    {
        if (!string.IsNullOrWhiteSpace(_options.LocalFeedPath))
        {
            var configured = _options.LocalFeedPath;
            return Path.IsPathRooted(configured)
                ? Path.GetFullPath(configured)
                : Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, configured));
        }

        var modules = _supervisorOptions.ModulesDirectory;
        if (string.IsNullOrWhiteSpace(modules))
        {
            modules = "modules";
        }

        var modulesPath = Path.IsPathRooted(modules)
            ? modules
            : Path.Combine(AppContext.BaseDirectory, modules);

        // Sibling of the modules directory: <base>/store-feed.
        return Path.GetFullPath(Path.Combine(Path.GetDirectoryName(Path.GetFullPath(modulesPath))!, "store-feed"));
    }
}
