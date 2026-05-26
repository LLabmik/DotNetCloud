namespace DotNetCloud.Modules.Video.UI;

/// <summary>
/// View model for the Video module admin settings form.
/// </summary>
public sealed class VideoAdminSettingsViewModel
{
    /// <summary>TMDB API key for metadata enrichment.</summary>
    public string TmdbApiKey { get; set; } = string.Empty;
}
