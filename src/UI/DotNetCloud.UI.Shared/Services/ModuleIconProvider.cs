namespace DotNetCloud.UI.Shared.Services;

/// <summary>
/// Provides Material Icons ligature names for known module IDs.
/// Centralizes the module-ID-to-icon mapping used across search components and navigation.
/// </summary>
public static class ModuleIconProvider
{
    /// <summary>
    /// Returns the Material Icons ligature name for the given module ID.
    /// Falls back to <paramref name="defaultIcon"/> (default: "search") for unknown modules.
    /// </summary>
    public static string GetIcon(string moduleId, string defaultIcon = "search") => NormalizeModuleId(moduleId) switch
    {
        "files" => "folder",
        "notes" => "edit_note",
        "chat" => "chat",
        "contacts" => "person",
        "calendar" => "calendar_today",
        "photos" => "photo_library",
        "music" => "music_note",
        "video" => "movie",
        "tracks" => "assignment",
        "ai" => "smart_toy",
        "bookmarks" => "bookmark",
        "email" => "email",
        "about" => "info",
        _ => defaultIcon
    };

    /// <summary>
    /// Returns a human-readable display name for the given module ID.
    /// </summary>
    public static string GetDisplayName(string moduleId) => NormalizeModuleId(moduleId) switch
    {
        "files" => "Files",
        "notes" => "Notes",
        "chat" => "Chat",
        "contacts" => "Contacts",
        "calendar" => "Calendar",
        "photos" => "Photos",
        "music" => "Music",
        "video" => "Video",
        "tracks" => "Tracks",
        "ai" => "AI",
        "bookmarks" => "Bookmarks",
        "email" => "Email",
        "about" => "About",
        _ => moduleId
    };

    /// <summary>
    /// Normalizes a module ID so both short (<c>files</c>) and fully-qualified
    /// (<c>dotnetcloud.files</c>) forms resolve to the same entry.
    /// </summary>
    private static string NormalizeModuleId(string moduleId)
    {
        const string prefix = "dotnetcloud.";
        var id = moduleId ?? string.Empty;
        if (id.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
        {
            id = id[prefix.Length..];
        }

        return id.ToLowerInvariant();
    }
}
