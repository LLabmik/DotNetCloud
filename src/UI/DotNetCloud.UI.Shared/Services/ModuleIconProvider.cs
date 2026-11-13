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
    /// Resolves the icon to use for a module nav item.
    /// </summary>
    /// <param name="declaredIcon">
    /// The Material Icons ligature name declared by the module (manifest <c>ui.navItems[].icon</c>),
    /// or <see langword="null"/>/empty when the module declared none.
    /// </param>
    /// <param name="fallbackIcon">The icon to use when the declaration is absent or unusable.</param>
    /// <returns>A Material Icons ligature name.</returns>
    /// <remarks>
    /// This is the single place a module-declared icon is resolved, so the shell never renders an
    /// arbitrary string through <c>MaterialIcon</c>. The manifest validator already restricts the
    /// declared value to <c>[a-z][a-z0-9_]*</c>; the check is repeated here because a manifest is
    /// untrusted input by the time it reaches a component.
    /// </remarks>
    public static string ResolveIcon(string? declaredIcon, string fallbackIcon = "search")
    {
        if (string.IsNullOrWhiteSpace(declaredIcon))
        {
            return NormalizeIconName(fallbackIcon, "search");
        }

        var candidate = declaredIcon.Trim();
        if (candidate.Length == 0 || candidate.Length > 64)
        {
            return NormalizeIconName(fallbackIcon, "search");
        }

        if (!char.IsAsciiLetterLower(candidate[0]))
        {
            return NormalizeIconName(fallbackIcon, "search");
        }

        foreach (var ch in candidate)
        {
            if (!char.IsAsciiLetterLower(ch) && !char.IsAsciiDigit(ch) && ch != '_')
            {
                return NormalizeIconName(fallbackIcon, "search");
            }
        }

        return candidate;
    }

    private static string NormalizeIconName(string? icon, string defaultIcon)
    {
        if (string.IsNullOrWhiteSpace(icon) || !char.IsAsciiLetterLower(icon.Trim()[0]))
        {
            return defaultIcon;
        }

        return icon.Trim();
    }

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
