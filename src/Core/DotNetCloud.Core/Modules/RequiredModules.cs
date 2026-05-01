namespace DotNetCloud.Core.Modules;

/// <summary>
/// Static registry of architecturally required modules. Required modules cannot be
/// disabled or uninstalled at runtime, and they share the <c>core</c> database schema.
/// </summary>
public static class RequiredModules
{
    /// <summary>
    /// The set of module IDs that are architecturally required.
    /// Comparison is case-insensitive.
    /// </summary>
    public static readonly IReadOnlySet<string> ModuleIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        "dotnetcloud.files",
        "dotnetcloud.chat",
        "dotnetcloud.search"
    };

    /// <summary>
    /// Returns <c>true</c> if <paramref name="moduleId"/> identifies a required module.
    /// Accepts both fully-qualified ("dotnetcloud.files") and short ("files") forms.
    /// </summary>
    public static bool IsRequired(string moduleId)
    {
        var shortName = moduleId.StartsWith("dotnetcloud.", StringComparison.OrdinalIgnoreCase)
            ? moduleId["dotnetcloud.".Length..]
            : moduleId;
        return ModuleIds.Contains("dotnetcloud." + shortName);
    }

    /// <summary>
    /// Returns the database schema name for a module. Required modules share the
    /// <c>core</c> schema; optional modules get a dedicated schema named after their
    /// short module name (e.g., "contacts", "calendar").
    /// </summary>
    public static string GetSchemaName(string moduleId)
    {
        if (IsRequired(moduleId))
            return "core";

        return moduleId.StartsWith("dotnetcloud.", StringComparison.OrdinalIgnoreCase)
            ? moduleId["dotnetcloud.".Length..].ToLowerInvariant()
            : moduleId.ToLowerInvariant();
    }
}
