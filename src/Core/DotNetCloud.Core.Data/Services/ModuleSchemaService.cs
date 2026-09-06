using DotNetCloud.Core.Modules;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System.Collections.Concurrent;

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

    // Serializes schema operations per module. Multiple startup paths can call
    // EnsureModuleSchemaAsync for the same module concurrently (ProcessSupervisor's
    // pre-spawn schema pass and ModuleUiRegistrationHostedService's lazy seed). Running
    // EF MigrateAsync twice for the same module at the same time collided with 42P07
    // ("relation ... already exists"), which left the schema half-created on first boot.
    private readonly ConcurrentDictionary<string, SemaphoreSlim> _moduleLocks =
        new(StringComparer.OrdinalIgnoreCase);

    public ModuleSchemaService(
        IEnumerable<IModuleSchemaProvider> providers,
        ILogger<ModuleSchemaService> logger)
    {
        _providers = providers.ToList();
        _logger = logger;
    }

    public async Task EnsureModuleSchemaAsync(string moduleId, CancellationToken cancellationToken = default)
    {
        var gate = GetOrAddLock(moduleId);
        await gate.WaitAsync(cancellationToken);
        try
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
        finally
        {
            gate.Release();
        }
    }

    public async Task DropModuleSchemaAsync(string moduleId, CancellationToken cancellationToken = default)
    {
        var gate = GetOrAddLock(moduleId);
        await gate.WaitAsync(cancellationToken);
        try
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
        finally
        {
            gate.Release();
        }
    }

    private SemaphoreSlim GetOrAddLock(string moduleId) =>
        _moduleLocks.GetOrAdd(moduleId, static _ => new SemaphoreSlim(1, 1));
}
