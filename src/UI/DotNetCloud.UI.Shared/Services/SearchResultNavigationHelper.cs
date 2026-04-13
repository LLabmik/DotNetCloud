using DotNetCloud.Core.DTOs.Search;

namespace DotNetCloud.UI.Shared.Services;

/// <summary>
/// Provides routing helpers for navigating from cross-module search results.
/// </summary>
public static class SearchResultNavigationHelper
{
    /// <summary>
    /// Determines whether a search result from the Files module should be treated as a music file.
    /// </summary>
    /// <param name="item">Search result item.</param>
    /// <returns><c>true</c> when the result appears to be an audio file; otherwise <c>false</c>.</returns>
    public static bool IsMusicFileSearchResult(SearchResultItem item)
    {
        ArgumentNullException.ThrowIfNull(item);

        if (!string.Equals(item.ModuleId, "files", StringComparison.OrdinalIgnoreCase))
            return false;

        if (item.Metadata.TryGetValue("MimeType", out var mimeType) &&
            mimeType.StartsWith("audio/", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (item.Metadata.TryGetValue("Path", out var path) &&
            IsKnownMusicExtension(Path.GetExtension(path)))
        {
            return true;
        }

        var titleExtension = Path.GetExtension(item.Title);
        return IsKnownMusicExtension(titleExtension);
    }

    /// <summary>
    /// Builds the navigation URL for a search result.
    /// </summary>
    /// <param name="item">Search result item.</param>
    /// <param name="navToken">Navigation token used by Files to force selection refresh.</param>
    /// <param name="openMusicModule">
    /// Whether the caller explicitly chose to open a music file result in the Music module.
    /// </param>
    /// <returns>Relative application URL for navigation.</returns>
    public static string GetResultUrl(SearchResultItem item, long navToken, bool openMusicModule = false)
    {
        ArgumentNullException.ThrowIfNull(item);

        if (openMusicModule && IsMusicFileSearchResult(item))
        {
            return $"/apps/music?fileId={item.EntityId}&_nav={navToken}";
        }

        return item.ModuleId switch
        {
            "files" => $"/apps/files?fileId={item.EntityId}&_nav={navToken}",
            "notes" => $"/apps/notes?noteId={item.EntityId}",
            "chat" => item.Metadata.TryGetValue("ChannelId", out var channelId)
                ? $"/apps/chat?channelId={channelId}&messageId={item.EntityId}"
                : $"/apps/chat?messageId={item.EntityId}",
            "contacts" => $"/apps/contacts?contactId={item.EntityId}",
            "calendar" => $"/apps/calendar?eventId={item.EntityId}",
            "photos" => $"/apps/photos?photoId={item.EntityId}",
            "music" => $"/apps/music?trackId={item.EntityId}",
            "video" => $"/apps/video?videoId={item.EntityId}",
            "tracks" => $"/apps/tracks?cardId={item.EntityId}",
            "ai" => $"/apps/ai?conversationId={item.EntityId}",
            _ => $"/search?q={item.Title}"
        };
    }

    private static bool IsKnownMusicExtension(string? extension) =>
        extension is not null &&
        (extension.Equals(".mp3", StringComparison.OrdinalIgnoreCase) ||
         extension.Equals(".flac", StringComparison.OrdinalIgnoreCase) ||
         extension.Equals(".ogg", StringComparison.OrdinalIgnoreCase) ||
         extension.Equals(".opus", StringComparison.OrdinalIgnoreCase) ||
         extension.Equals(".wav", StringComparison.OrdinalIgnoreCase) ||
         extension.Equals(".aac", StringComparison.OrdinalIgnoreCase) ||
         extension.Equals(".m4a", StringComparison.OrdinalIgnoreCase) ||
         extension.Equals(".wma", StringComparison.OrdinalIgnoreCase) ||
         extension.Equals(".alac", StringComparison.OrdinalIgnoreCase));
}