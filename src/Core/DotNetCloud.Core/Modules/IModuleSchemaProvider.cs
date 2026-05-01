namespace DotNetCloud.Core.Modules;

/// <summary>
/// Creates or migrates database schemas for a module.
/// Implementations handle both first-party (core-driven) and
/// third-party (self-managed) schema strategies.
/// </summary>
public interface IModuleSchemaProvider
{
    /// <summary>
    /// Ensures the module's database schema exists and is up to date.
    /// For core-managed modules this runs EF migrations.
    /// For self-managed modules this is a no-op (the module process handles it).
    /// </summary>
    Task EnsureSchemaAsync(string moduleId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Drops the module's database schema. Only applicable to core-managed modules.
    /// </summary>
    Task DropSchemaAsync(string moduleId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns true if this module's schema is managed by the core server
    /// (as opposed to being self-managed by the module process).
    /// </summary>
    bool IsCoreManaged(string moduleId);
}
