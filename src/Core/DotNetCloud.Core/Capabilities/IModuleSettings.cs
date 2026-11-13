namespace DotNetCloud.Core.Capabilities;

/// <summary>
/// Provides access to module-specific settings persisted by the core.
/// </summary>
/// <remarks>
/// <para>
/// Settings are namespaced by the module that owns them; the core stamps the module identity from
/// the authenticated transport credential, so an implementation never needs to (and cannot) name
/// another module.
/// </para>
/// <para>
/// Modules that declare <c>dataStore: "none"</c> use this plane as their only persistence.
/// </para>
/// </remarks>
public interface IModuleSettings : ICapabilityInterface
{
    /// <summary>
    /// Reads a setting value.
    /// </summary>
    /// <param name="key">The setting key.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The stored value, or <see langword="null"/> when the key does not exist.</returns>
    Task<string?> GetAsync(string key, CancellationToken cancellationToken = default);

    /// <summary>
    /// Creates or updates a setting value.
    /// </summary>
    /// <param name="key">The setting key.</param>
    /// <param name="value">The value to persist.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task SetAsync(string key, string value, CancellationToken cancellationToken = default);
}
