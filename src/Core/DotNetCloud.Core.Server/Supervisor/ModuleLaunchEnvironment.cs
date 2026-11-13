using DotNetCloud.Core.Modules;
using DotNetCloud.Core.Modules.Supervisor;
using DotNetCloud.Core.Server.ModuleLoading;
using DotNetCloud.Core.Server.Sandbox;

namespace DotNetCloud.Core.Server.Supervisor;

/// <summary>
/// Resolves the configuration/data directories a module host receives at launch.
/// </summary>
/// <remarks>
/// <para>
/// Extracted from <see cref="ProcessSupervisor"/> so the sandbox split can be asserted without
/// starting a process. Sandboxed modules (manifest v2, not required) never see
/// <c>DOTNETCLOUD_CONFIG_DIR</c>/<c>DOTNETCLOUD_DATA_DIR</c> — those expose the core connection
/// string, TLS material, the admin identity and the plaintext WOPI signing key. They get
/// <c>DOTNETCLOUD_MODULE_CONFIG_DIR</c>/<c>DOTNETCLOUD_MODULE_DATA_DIR</c> instead, plus
/// <c>DOTNETCLOUD_MODULE_DB_CONNECTION_STRING</c> when the module owns an isolated data store.
/// </para>
/// <para>
/// v1 modules (every product-shipped module until Track C migrates them) keep the legacy shared
/// directories only while <see cref="ProcessSupervisorOptions.AllowLegacyAccess"/> is enabled; with
/// the override off the directories are withheld and the supervisor logs the refusal.
/// </para>
/// </remarks>
internal static class ModuleLaunchEnvironment
{
    /// <summary>Environment variable holding the module's sanitized config directory.</summary>
    internal const string ModuleConfigDirectoryVariable = "DOTNETCLOUD_MODULE_CONFIG_DIR";

    /// <summary>Environment variable holding the module's private data directory.</summary>
    internal const string ModuleDataDirectoryVariable = "DOTNETCLOUD_MODULE_DATA_DIR";

    /// <summary>Environment variable holding the module's scoped connection string.</summary>
    internal const string ModuleDbConnectionStringVariable = "DOTNETCLOUD_MODULE_DB_CONNECTION_STRING";

    /// <summary>Legacy core configuration directory (never given to sandboxed modules).</summary>
    internal const string LegacyConfigDirectoryVariable = "DOTNETCLOUD_CONFIG_DIR";

    /// <summary>Legacy shared data directory (never given to sandboxed modules).</summary>
    internal const string LegacyDataDirectoryVariable = "DOTNETCLOUD_DATA_DIR";

    /// <summary>
    /// Determines whether a module runs in the sandboxed layout: a v2 manifest for a module that is
    /// not required. Required modules stay core-managed regardless of manifest version.
    /// </summary>
    /// <param name="manifest">The module manifest.</param>
    /// <returns><see langword="true"/> when the module is sandboxed.</returns>
    internal static bool IsSandboxed(ModuleManifestData manifest) =>
        manifest.ManifestVersion >= 2 && !RequiredModules.IsRequired(manifest.Id);

    /// <summary>
    /// Resolves the directory environment for a module launch.
    /// </summary>
    /// <param name="manifest">The module manifest.</param>
    /// <param name="options">Supervisor options (legacy-access override).</param>
    /// <param name="moduleId">The module id being launched.</param>
    /// <param name="dataRootDirectory">The server data root module directories live under.</param>
    /// <param name="moduleConnectionString">The scoped connection string, when isolated.</param>
    /// <param name="useLegacyLayout">
    /// Whether the module fell back to the legacy layout (transitional override after a failed
    /// provisioning attempt). Never true for store-tier modules — the caller decides.
    /// </param>
    /// <param name="legacyConfigDirectory">
    /// The process's <c>DOTNETCLOUD_CONFIG_DIR</c>, forwarded only under the legacy override.
    /// </param>
    /// <param name="legacyDataDirectory">
    /// The process's <c>DOTNETCLOUD_DATA_DIR</c>, forwarded only under the legacy override.
    /// </param>
    /// <returns>The resolved environment plan.</returns>
    internal static ModuleLaunchEnvironmentPlan Resolve(
        ModuleManifestData manifest,
        ProcessSupervisorOptions options,
        string moduleId,
        string dataRootDirectory,
        string? moduleConnectionString,
        bool useLegacyLayout,
        string? legacyConfigDirectory = null,
        string? legacyDataDirectory = null)
    {
        if (IsSandboxed(manifest) && !useLegacyLayout)
        {
            var layout = ModuleSandboxLayout.Create(dataRootDirectory, moduleId);

            var sandboxVariables = new Dictionary<string, string>(StringComparer.Ordinal)
            {
                [ModuleConfigDirectoryVariable] = layout.ConfigDirectory,
                [ModuleDataDirectoryVariable] = layout.DataDirectory,
            };

            if (!string.IsNullOrEmpty(moduleConnectionString))
            {
                sandboxVariables[ModuleDbConnectionStringVariable] = moduleConnectionString;
            }

            return new ModuleLaunchEnvironmentPlan(sandboxVariables, layout, LegacyDirectoriesRefused: false);
        }

        if (options.AllowLegacyAccess)
        {
            var legacyVariables = new Dictionary<string, string>(StringComparer.Ordinal);

            foreach (var (name, value) in new[]
                     {
                         (LegacyConfigDirectoryVariable, legacyConfigDirectory),
                         (LegacyDataDirectoryVariable, legacyDataDirectory),
                     })
            {
                if (!string.IsNullOrWhiteSpace(value))
                {
                    legacyVariables[name] = value;
                }
            }

            return new ModuleLaunchEnvironmentPlan(legacyVariables, Layout: null, LegacyDirectoriesRefused: false);
        }

        return new ModuleLaunchEnvironmentPlan(
            new Dictionary<string, string>(StringComparer.Ordinal),
            Layout: null,
            LegacyDirectoriesRefused: true);
    }
}

/// <summary>
/// The directory environment a module host is launched with, plus the sandbox layout the caller
/// must materialize before the process starts.
/// </summary>
/// <param name="Variables">Environment variables to set on the child process.</param>
/// <param name="Layout">The sandbox layout to create and describe in <c>module.json</c>, if any.</param>
/// <param name="LegacyDirectoriesRefused">
/// <see langword="true"/> when neither the sandbox layout nor the legacy override applied; the
/// caller logs the refusal instead of forwarding the core directories.
/// </param>
internal sealed record ModuleLaunchEnvironmentPlan(
    IReadOnlyDictionary<string, string> Variables,
    ModuleSandboxLayout? Layout,
    bool LegacyDirectoriesRefused);
