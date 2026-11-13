namespace DotNetCloud.Core.Modules;

/// <summary>
/// A single persisted module setting, as returned by <see cref="IModuleSettingsStore.ListAsync"/>.
/// </summary>
/// <param name="ModuleId">The module that owns the setting.</param>
/// <param name="Key">The setting key.</param>
/// <param name="IsSecret">Whether the value is sensitive.</param>
/// <param name="HasValue">Whether a stored value exists.</param>
/// <param name="Value">
/// The plain-text value, or <see langword="null"/> for a secret setting. Secret values are never
/// returned by a listing; the admin UI shows <see cref="IsSecret"/> and lets an operator replace it.
/// </param>
/// <param name="UpdatedAtUtc">When the value last changed.</param>
/// <param name="UpdatedByUserId">The user who last changed the value, when a user initiated it.</param>
public sealed record ModuleSettingEntry(
    string ModuleId,
    string Key,
    bool IsSecret,
    bool HasValue,
    string? Value,
    DateTime UpdatedAtUtc,
    Guid? UpdatedByUserId);

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
/// <para>
/// Settings marked <c>isSecret</c> are protected with <see cref="IModuleSettingCipher"/> before they
/// are written, so an API key never sits in the database as plain text.
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
    /// Lists every setting of a module. Secret values are masked.
    /// </summary>
    /// <param name="moduleId">The module id.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The setting entries, ordered by key.</returns>
    Task<IReadOnlyList<ModuleSettingEntry>> ListAsync(string moduleId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Creates or updates a setting value for a module.
    /// </summary>
    /// <param name="moduleId">The authenticated module id.</param>
    /// <param name="key">The setting key.</param>
    /// <param name="value">The value to persist.</param>
    /// <param name="isSecret">Whether the value is sensitive and must be encrypted and masked.</param>
    /// <param name="updatedByUserId">The acting user, when a user initiated the change.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task SetAsync(
        string moduleId,
        string key,
        string value,
        bool isSecret = false,
        Guid? updatedByUserId = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes a setting. Removing a setting the module does not have is not an error.
    /// </summary>
    /// <param name="moduleId">The module id.</param>
    /// <param name="key">The setting key.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns><see langword="true"/> when a row was deleted.</returns>
    Task<bool> DeleteAsync(string moduleId, string key, CancellationToken cancellationToken = default);
}
