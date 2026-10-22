namespace DotNetCloud.Modules.Files.Services;

/// <summary>
/// Shared MIME-type guessing used by upload completion and the internal import path,
/// so provisioned default content is stored with the same MIME type an upload would produce.
/// </summary>
public static class MimeTypeGuesser
{
    /// <summary>
    /// Guesses a MIME type from a file name's extension.
    /// </summary>
    /// <param name="fileName">File name (or path).</param>
    /// <returns>The guessed MIME type, or <see langword="null"/> when the extension is unknown.</returns>
    public static string? FromFileName(string fileName)
    {
        ArgumentNullException.ThrowIfNull(fileName);

        var ext = Path.GetExtension(fileName);
        if (string.IsNullOrEmpty(ext))
            return null;

        return ext.ToLowerInvariant() switch
        {
            // Audio
            ".mp3" => "audio/mpeg",
            ".flac" => "audio/flac",
            ".ogg" or ".oga" => "audio/ogg",
            ".opus" => "audio/opus",
            ".aac" => "audio/aac",
            ".m4a" => "audio/mp4",
            ".wav" => "audio/wav",
            ".wma" => "audio/x-ms-wma",
            ".aiff" or ".aif" => "audio/aiff",
            // Video
            ".mp4" or ".m4v" => "video/mp4",
            ".mkv" => "video/x-matroska",
            ".avi" => "video/x-msvideo",
            ".mov" => "video/quicktime",
            ".webm" => "video/webm",
            // Images
            ".jpg" or ".jpeg" => "image/jpeg",
            ".png" => "image/png",
            ".gif" => "image/gif",
            ".webp" => "image/webp",
            ".svg" => "image/svg+xml",
            ".bmp" => "image/bmp",
            ".tif" or ".tiff" => "image/tiff",
            // Documents
            ".pdf" => "application/pdf",
            ".txt" => "text/plain",
            ".md" => "text/markdown",
            ".csv" => "text/csv",
            ".html" or ".htm" => "text/html",
            ".json" => "application/json",
            ".xml" => "application/xml",
            ".rtf" => "application/rtf",
            ".odt" => "application/vnd.oasis.opendocument.text",
            ".ods" => "application/vnd.oasis.opendocument.spreadsheet",
            ".odp" => "application/vnd.oasis.opendocument.presentation",
            ".doc" => "application/msword",
            ".xls" => "application/vnd.ms-excel",
            ".ppt" => "application/vnd.ms-powerpoint",
            ".docx" => "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
            ".xlsx" => "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            ".pptx" => "application/vnd.openxmlformats-officedocument.presentationml.presentation",
            _ => null,
        };
    }
}
