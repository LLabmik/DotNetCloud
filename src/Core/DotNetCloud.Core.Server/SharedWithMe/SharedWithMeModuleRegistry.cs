using DotNetCloud.Core.SharedWithMe;

namespace DotNetCloud.Core.Server.SharedWithMe;

/// <summary>
/// Core-side implementation of <see cref="ISharedWithMeModuleRegistry"/> that aggregates every
/// registered <see cref="ISharedWithMeProvider"/> into the slim facade the Files module consumes.
/// Aggregation is a core concern: Files.Data never references another module's data/services.
/// </summary>
/// <remarks>
/// Registered as a singleton. Its <see cref="Modules"/> snapshot is built on first resolution, by
/// which point all providers have been registered during host startup.
/// </remarks>
public sealed class SharedWithMeModuleRegistry : ISharedWithMeModuleRegistry
{
    private readonly IReadOnlyDictionary<string, ISharedWithMeProvider> _providersByModuleId;
    private readonly IReadOnlyList<SharedWithMeModule> _modules;

    /// <summary>
    /// Initializes a new instance of the <see cref="SharedWithMeModuleRegistry"/> class.
    /// </summary>
    /// <param name="providers">All registered shared-with-me module providers.</param>
    public SharedWithMeModuleRegistry(IEnumerable<ISharedWithMeProvider> providers)
    {
        _providersByModuleId = providers.ToDictionary(
            provider => provider.ModuleId,
            StringComparer.OrdinalIgnoreCase);

        _modules = _providersByModuleId.Values
            .OrderBy(provider => provider.DisplayName, StringComparer.OrdinalIgnoreCase)
            .Select(provider => new SharedWithMeModule(provider.ModuleId, provider.DisplayName, provider.IconName))
            .ToList();
    }

    /// <inheritdoc />
    public IReadOnlyList<SharedWithMeModule> Modules => _modules;

    /// <inheritdoc />
    public Task<int> CountAsync(string moduleId, Guid userId, CancellationToken cancellationToken = default)
        => _providersByModuleId.TryGetValue(moduleId, out var provider)
            ? provider.CountAsync(userId, cancellationToken)
            : Task.FromResult(0);

    /// <inheritdoc />
    public Task<IReadOnlyList<SharedWithMeModuleItem>> ListAsync(string moduleId, Guid userId, CancellationToken cancellationToken = default)
        => _providersByModuleId.TryGetValue(moduleId, out var provider)
            ? provider.ListAsync(userId, cancellationToken)
            : Task.FromResult<IReadOnlyList<SharedWithMeModuleItem>>([]);
}
