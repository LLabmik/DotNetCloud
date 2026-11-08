using DotNetCloud.Core.Modules;

namespace DotNetCloud.Core.Server.Sandbox;

/// <summary>
/// Resolves the schema a <b>bundled</b> module used before it became sandboxed.
/// </summary>
/// <remarks>
/// <para>
/// First-party optional modules were migrated by the core into a schema named after the module
/// (<c>bookmarks</c>, <c>photos</c>, …). When such a module moves to the sandboxed model its data has
/// to end up in the generated schema the core provisioned for it, so the adoption step needs the
/// historical name to move the objects from.
/// </para>
/// <para>
/// The name is derived here from the <b>module id alone</b> — never from a manifest, a package or an
/// operator-supplied string — and only for the bundled <c>dotnetcloud.*</c> namespace. A store or
/// third-party package therefore cannot ask the core to move objects out of an arbitrary schema
/// (for example <c>core</c>), which is why this resolver refuses <see cref="RequiredModules">required</see>
/// modules and anything outside the bundled namespace.
/// </para>
/// </remarks>
internal static class ModuleLegacySchema
{
    private const string BundledPrefix = "dotnetcloud.";

    /// <summary>
    /// Resolves the historical schema name for a bundled, non-required module.
    /// </summary>
    /// <param name="moduleId">The module id (for example <c>dotnetcloud.bookmarks</c>).</param>
    /// <param name="schemaName">The historical schema name, when one applies.</param>
    /// <param name="reason">Why no historical schema applies, when resolution fails.</param>
    /// <returns><see langword="true"/> when the module had a core-managed schema of its own.</returns>
    public static bool TryResolve(string moduleId, out string schemaName, out string? reason)
    {
        schemaName = string.Empty;
        reason = null;

        if (string.IsNullOrWhiteSpace(moduleId))
        {
            reason = "Module id is required.";
            return false;
        }

        if (!moduleId.StartsWith(BundledPrefix, StringComparison.OrdinalIgnoreCase))
        {
            reason = "Not a bundled module; nothing to move.";
            return false;
        }

        var shortName = moduleId[BundledPrefix.Length..];

        // The short name becomes an identifier: refuse anything that is not one, before the naming
        // strategy can turn it into a schema name.
        if (!ModuleDataStoreNaming.IsPlainIdentifier(shortName))
        {
            reason = "Module id does not map to a plain schema identifier.";
            return false;
        }

        if (RequiredModules.IsRequired(moduleId))
        {
            reason = "Required module — its schema stays core-managed.";
            return false;
        }

        var candidate = RequiredModules.GetSchemaName(shortName);

        if (string.IsNullOrEmpty(candidate)
            || string.Equals(candidate, "core", StringComparison.OrdinalIgnoreCase)
            || !ModuleDataStoreNaming.IsPlainIdentifier(candidate))
        {
            reason = "The module has no dedicated schema of its own.";
            return false;
        }

        schemaName = candidate;
        return true;
    }
}
