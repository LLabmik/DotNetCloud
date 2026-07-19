namespace DotNetCloud.UI.Shared.Services;

/// <summary>
/// Provides Material Icons ligature names for file types based on MIME type.
/// Centralizes the MIME-type-to-icon mapping used across modules (Files, Email, etc.).
/// </summary>
public static class FileTypeIconProvider
{
    /// <summary>
    /// Returns the Material Icons ligature name for the given MIME type.
    /// Falls back to <paramref name="defaultIcon"/> (default: "description") for unknown types.
    /// </summary>
    public static string GetIcon(string? mimeType, string defaultIcon = "description") =>
        mimeType switch
        {
            null or "" => defaultIcon,
            // Images
            string m when m.StartsWith("image/") => "image",
            // Videos
            string m when m.StartsWith("video/") => "movie",
            // Audio
            string m when m.StartsWith("audio/") => "music_note",
            // Text / code
            string m when m.StartsWith("text/") => "text_snippet",
            // PDF
            "application/pdf" => "picture_as_pdf",
            // Spreadsheets
            "application/vnd.ms-excel" or
            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet" or
            "application/vnd.oasis.opendocument.spreadsheet" => "table_chart",
            // Presentations
            "application/vnd.ms-powerpoint" or
            "application/vnd.openxmlformats-officedocument.presentationml.presentation" or
            "application/vnd.oasis.opendocument.presentation" => "slideshow",
            // Word / documents
            "application/msword" or
            "application/vnd.openxmlformats-officedocument.wordprocessingml.document" or
            "application/vnd.oasis.opendocument.text" => "article",
            // Compressed archives
            "application/zip" or
            "application/gzip" or
            "application/x-tar" or
            "application/x-7z-compressed" or
            "application/x-rar-compressed" => "folder_zip",
            // Generic binary / fallback
            _ => defaultIcon
        };

    /// <summary>
    /// Returns the Material Icons ligature name for a folder node.
    /// </summary>
    public static string GetFolderIcon() => "folder";

    /// <summary>
    /// Returns the Material Icons ligature name for an open folder node.
    /// </summary>
    public static string GetOpenFolderIcon() => "folder_open";
}
