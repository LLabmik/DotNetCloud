using DotNetCloud.Core.Modules;

namespace DotNetCloud.Core.Data.Services;

/// <summary>
/// Schema provider for third-party modules that manage their own database schema.
/// The core server takes no action — the module process runs its own migrations on startup.
/// </summary>
public class SelfManagedSchemaProvider : IModuleSchemaProvider
{
    /// <inheritdoc/>
    public bool IsCoreManaged(string moduleId) => false;

    /// <inheritdoc/>
    public Task EnsureSchemaAsync(string moduleId, CancellationToken cancellationToken = default)
    {
        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    public Task DropSchemaAsync(string moduleId, CancellationToken cancellationToken = default)
    {
        return Task.CompletedTask;
    }
}
