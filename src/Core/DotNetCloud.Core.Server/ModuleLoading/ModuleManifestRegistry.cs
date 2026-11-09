using System.Collections.Concurrent;
using DotNetCloud.Core.Modules;

namespace DotNetCloud.Core.Server.ModuleLoading;

/// <summary>
/// Holds the events each discovered module declared it publishes in its manifest.
/// </summary>
/// <remarks>
/// <para>
/// A module manifest is the operator-visible record of what a module does. The core already uses it
/// for capability grants and sandbox mode; this registry extends the same trust model to the event
/// plane so <c>PublishEvent</c> can check the event type against the declaring module instead of a
/// global allow-list (an undeclared type is rejected and audited).
/// </para>
/// <para>
/// The registry is populated by <c>ProcessSupervisor</c> every time it reads a manifest, so it is
/// available before any host is spawned. A module with no recorded entry is not a supervised module
/// and is denied (fail closed).
/// </para>
/// </remarks>
internal interface IModuleManifestRegistry
{
    /// <summary>Records (or replaces) the events a module declares it publishes.</summary>
    /// <param name="moduleId">The module id.</param>
    /// <param name="manifest">The manifest the supervisor loaded for it.</param>
    void Record(string moduleId, ModuleManifestData manifest);

    /// <summary>
    /// Gets the events a module declared it publishes.
    /// </summary>
    /// <param name="moduleId">The module id.</param>
    /// <returns>The declared event names, or <see langword="null"/> when the module is unknown.</returns>
    IReadOnlyCollection<string>? GetPublishedEvents(string moduleId);

    /// <summary>
    /// Gets the real-time event names a module declared it may broadcast.
    /// </summary>
    /// <param name="moduleId">The module id.</param>
    /// <returns>
    /// The declared names (possibly empty), or <see langword="null"/> when the module is unknown.
    /// </returns>
    IReadOnlyCollection<string>? GetRealtimeEvents(string moduleId);

    /// <summary>
    /// Gets the per-user consent scopes a module declared in its manifest.
    /// </summary>
    /// <param name="moduleId">The module id.</param>
    /// <returns>
    /// The declared scopes (possibly empty), or <see langword="null"/> when the module is unknown.
    /// </returns>
    /// <remarks>
    /// An empty set is the transitional case: a required or bundled module that has not opted into
    /// the consent plane yet. A module that declares scopes must hold a live consent row for the
    /// user before it can reach that user's personal data.
    /// </remarks>
    IReadOnlyCollection<string>? GetUserConsentScopes(string moduleId);

    /// <summary>Gets the ids of every module whose manifest has been recorded.</summary>
    /// <returns>The recorded module ids.</returns>
    IReadOnlyCollection<string> GetModuleIds();

    /// <summary>Gets the manifest recorded for a module.</summary>
    /// <param name="moduleId">The module id.</param>
    /// <returns>The manifest, or <see langword="null"/> when the module is unknown.</returns>
    ModuleManifestData? GetManifest(string moduleId);
}

/// <inheritdoc />
internal sealed class ModuleManifestRegistry : IModuleManifestRegistry, IConsentScopeRegistry
{
    private readonly ConcurrentDictionary<string, ModuleManifestData> _manifests =
        new(StringComparer.OrdinalIgnoreCase);

    /// <inheritdoc />
    public void Record(string moduleId, ModuleManifestData manifest)
    {
        ArgumentNullException.ThrowIfNull(manifest);

        if (string.IsNullOrWhiteSpace(moduleId))
        {
            return;
        }

        _manifests[moduleId] = manifest;
    }

    /// <inheritdoc />
    public IReadOnlyCollection<string>? GetPublishedEvents(string moduleId) =>
        GetManifest(moduleId) is { } manifest ? Normalize(manifest.EffectivePublishedEvents) : null;

    /// <inheritdoc />
    public IReadOnlyCollection<string>? GetRealtimeEvents(string moduleId) =>
        GetManifest(moduleId) is { } manifest ? Normalize(manifest.EffectiveRealtimeEvents) : null;

    /// <inheritdoc />
    public IReadOnlyCollection<string>? GetUserConsentScopes(string moduleId) =>
        GetManifest(moduleId) is { } manifest ? Normalize(manifest.UserConsentScopes) : null;

    /// <inheritdoc />
    public IReadOnlyCollection<string> GetModuleIds() => _manifests.Keys.ToArray();

    /// <inheritdoc />
    public ModuleManifestData? GetManifest(string moduleId) =>
        !string.IsNullOrWhiteSpace(moduleId) && _manifests.TryGetValue(moduleId, out var manifest)
            ? manifest
            : null;

    private static IReadOnlyCollection<string> Normalize(IReadOnlyList<string>? declared) =>
        declared is null
            ? []
            : declared
                .Where(name => !string.IsNullOrWhiteSpace(name))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();
}
