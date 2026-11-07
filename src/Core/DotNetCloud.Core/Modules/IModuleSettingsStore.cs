namespace DotNetCloud.Core.Modules;

/// <summary>
/// Persists the key/value settings of a single module.
/// </summary>
/// <remarks>
/// <para>
/// Keys are always namespaced by the <em>authenticated</em> module identity; implementations must
/// never accept a caller-supplied module id. This is the backing store for the
/// <c>IModuleSettings</c> capability and the persistence plane for modules that declare
/// <c>dataStore: "none"</c>.
/// </para>
/// <para>
/// This is deliberately separate from <c>SystemSettings</c>, which is the administrator-managed
/// configuration source and is not writable by modules.
/// </para>
/// </remarks>
public interface IModuleSettingsStore
{
    /// <summary>
    /// Reads a setting value for a module.
    /// </summary>
    /// <param name="moduleId">The authenticated module id.</param>
    /// <param name="key">The setting key.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The stored value, or <see langword="null"/> when the key does not exist.</returns>
    Task<string?> GetAsync(string moduleId, string key, CancellationToken cancellationToken = default);

    /// <summary>
    /// Creates or updates a setting value for a module.
    /// </summary>
    /// <param name="moduleId">The authenticated module id.</param>
    /// <param name="key">The setting key.</param>
    /// <param name="value">The value to persist.</param>
    /// <param name="isSecret">Whether the value is sensitive and must be masked in listings.</param>
    /// <param name="updatedByUserId">The acting user, when a user initiated the change.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task SetAsync(
        string moduleId,
        string key,
        string value,
        bool isSecret = false,
        Guid? updatedByUserId = null,
        CancellationToken cancellationToken = default);
}
