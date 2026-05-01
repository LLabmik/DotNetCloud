using DotNetCloud.Core.Modules;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace DotNetCloud.Core.Data.Services;

/// <summary>
/// Dispatches schema operations to the correct provider based on the module's
/// declared schema management strategy (core-managed vs self-managed).
/// Providers are resolved from DI via <see cref="IEnumerable{IModuleSchemaProvider}"/>.
/// </summary>
public class ModuleSchemaService
{
    private readonly IReadOnlyList<IModuleSchemaProvider> _providers;
    private readonly ILogger<ModuleSchemaService> _logger;

    public ModuleSchemaService(
        IEnumerable<IModuleSchemaProvider> providers,
        ILogger<ModuleSchemaService> logger)
    {
        _providers = providers.ToList();
        _logger = logger;
    }

    public async Task EnsureModuleSchemaAsync(string moduleId, CancellationToken cancellationToken = default)
    {
        foreach (var provider in _providers)
        {
            if (provider.IsCoreManaged(moduleId))
            {
                await provider.EnsureSchemaAsync(moduleId, cancellationToken);
                return;
            }
        }

        _logger.LogInformation("Module {ModuleId} is self-managed; skipping core-driven schema creation", moduleId);
    }

    public async Task DropModuleSchemaAsync(string moduleId, CancellationToken cancellationToken = default)
    {
        foreach (var provider in _providers)
        {
            if (provider.IsCoreManaged(moduleId))
            {
                await provider.DropSchemaAsync(moduleId, cancellationToken);
                return;
            }
        }

        _logger.LogInformation("Module {ModuleId} is self-managed; skipping core-driven schema drop", moduleId);
    }
}
