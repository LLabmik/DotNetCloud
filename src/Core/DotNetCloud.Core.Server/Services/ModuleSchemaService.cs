using DotNetCloud.Core.Data.Services;
using DotNetCloud.Core.Modules;
using Microsoft.Extensions.Logging;

namespace DotNetCloud.Core.Server.Services;

/// <summary>
/// Dispatches schema operations to the correct provider based on the module's
/// declared schema management strategy (core-managed vs self-managed).
/// </summary>
public class ModuleSchemaService
{
    private readonly DbContextSchemaProvider _coreManaged;
    private readonly SelfManagedSchemaProvider _selfManaged;
    private readonly ILogger<ModuleSchemaService> _logger;

    public ModuleSchemaService(
        DbContextSchemaProvider coreManaged,
        SelfManagedSchemaProvider selfManaged,
        ILogger<ModuleSchemaService> logger)
    {
        _coreManaged = coreManaged;
        _selfManaged = selfManaged;
        _logger = logger;
    }

    public async Task EnsureModuleSchemaAsync(string moduleId, CancellationToken cancellationToken = default)
    {
        if (_coreManaged.IsCoreManaged(moduleId))
        {
            await _coreManaged.EnsureSchemaAsync(moduleId, cancellationToken);
        }
        else
        {
            _logger.LogInformation("Module {ModuleId} is self-managed; skipping core-driven schema creation", moduleId);
            await _selfManaged.EnsureSchemaAsync(moduleId, cancellationToken);
        }
    }

    public async Task DropModuleSchemaAsync(string moduleId, CancellationToken cancellationToken = default)
    {
        if (_coreManaged.IsCoreManaged(moduleId))
        {
            await _coreManaged.DropSchemaAsync(moduleId, cancellationToken);
        }
        else
        {
            _logger.LogInformation("Module {ModuleId} is self-managed; skipping core-driven schema drop", moduleId);
        }
    }
}
