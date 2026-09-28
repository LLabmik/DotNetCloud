namespace DotNetCloud.Modules.Chat.Services;

/// <summary>
/// Resolves the administrator-configurable <see cref="ChatSettings"/> for the Chat module.
/// </summary>
/// <remarks>
/// Implementations read the core <c>SystemSettings</c> table and therefore need
/// <c>IAdminSettingsService</c> registered in the host. When it is absent (standalone
/// module host without a core database, or unit tests) the provider falls back to
/// <c>Chat:*</c> configuration values and then to the built-in defaults.
/// </remarks>
public interface IChatSettingsProvider
{
    /// <summary>
    /// Gets the current chat settings.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The resolved (and clamped) settings.</returns>
    Task<ChatSettings> GetSettingsAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Drops any cached settings so the next read observes freshly saved admin values.
    /// </summary>
    void Invalidate();
}
