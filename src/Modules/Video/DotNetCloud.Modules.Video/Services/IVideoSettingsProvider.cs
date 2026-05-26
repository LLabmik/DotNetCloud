namespace DotNetCloud.Modules.Video.Services;

/// <summary>
/// Resolves Video module settings from the system settings DB with fallback to IConfiguration.
/// </summary>
public interface IVideoSettingsProvider
{
    /// <summary>Gets the TMDB API key, or empty string if not configured.</summary>
    Task<string> GetTmdbApiKeyAsync(CancellationToken cancellationToken = default);

    /// <summary>Whether TMDB enrichment is available (API key is configured).</summary>
    Task<bool> IsTmdbAvailableAsync(CancellationToken cancellationToken = default);
}
