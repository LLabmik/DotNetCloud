namespace DotNetCloud.Core.Security;

/// <summary>
/// Defines allowed file types with extension whitelist, magic byte signatures, and max sizes.
/// </summary>
public static class AllowedFileTypes
{
    /// <summary>
    /// Represents a file type definition for validation purposes.
    /// </summary>
    public record FileTypeDefinition
    {
        /// <summary>The file extensions allowed for this type (e.g., ".jpg", ".png").</summary>
        public string[] Extensions { get; init; } = [];

        /// <summary>MIME content types that are valid for this type.</summary>
        public string[] MimeTypes { get; init; } = [];

        /// <summary>Magic byte sequences (file signatures) that identify this type.</summary>
        public byte[][] MagicBytes { get; init; } = [];

        /// <summary>Maximum allowed file size in bytes, or null for no limit.</summary>
        public long? MaxSize { get; init; }
    }

    // ── Image types ──────────────────────────────────────────────────────────

    /// <summary>JPEG image — .jpg/.jpeg.</summary>
    public static readonly FileTypeDefinition Jpeg = new()
    {
        Extensions = [".jpg", ".jpeg"],
        MimeTypes = ["image/jpeg"],
        MagicBytes =
        [
            [0xFF, 0xD8, 0xFF],           // JPEG SOI marker
        ],
        MaxSize = 5 * 1024 * 1024, // 5 MB
    };

    /// <summary>PNG image — .png.</summary>
    public static readonly FileTypeDefinition Png = new()
    {
        Extensions = [".png"],
        MimeTypes = ["image/png"],
        MagicBytes =
        [
            [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A], // PNG signature
        ],
        MaxSize = 5 * 1024 * 1024, // 5 MB
    };

    /// <summary>GIF image — .gif.</summary>
    public static readonly FileTypeDefinition Gif = new()
    {
        Extensions = [".gif"],
        MimeTypes = ["image/gif"],
        MagicBytes =
        [
            [0x47, 0x49, 0x46, 0x38, 0x37, 0x61], // GIF87a
            [0x47, 0x49, 0x46, 0x38, 0x39, 0x61], // GIF89a
        ],
        MaxSize = 5 * 1024 * 1024, // 5 MB
    };

    /// <summary>WebP image — .webp.</summary>
    public static readonly FileTypeDefinition WebP = new()
    {
        Extensions = [".webp"],
        MimeTypes = ["image/webp"],
        MagicBytes =
        [
            [0x52, 0x49, 0x46, 0x46], // RIFF (WebP container starts with RIFF)
        ],
        MaxSize = 5 * 1024 * 1024, // 5 MB
    };

    // ── Document types ───────────────────────────────────────────────────────

    /// <summary>CSV file — .csv.</summary>
    public static readonly FileTypeDefinition Csv = new()
    {
        Extensions = [".csv"],
        MimeTypes = ["text/csv", "application/csv", "text/plain"],
        MagicBytes = [], // CSV has no universal magic bytes; validate extension + content
        MaxSize = 10 * 1024 * 1024, // 10 MB
    };

    /// <summary>HTML file — .html/.htm (for Bookmarks import).</summary>
    public static readonly FileTypeDefinition Html = new()
    {
        Extensions = [".html", ".htm"],
        MimeTypes = ["text/html", "text/plain"],
        MagicBytes = [], // HTML has no universal magic bytes; validated by parser
        MaxSize = 10 * 1024 * 1024, // 10 MB
    };

    /// <summary>Generic attachment (Email) — broad but limited to safe extensions.</summary>
    public static readonly FileTypeDefinition EmailAttachment = new()
    {
        Extensions =
        [
            ".pdf", ".doc", ".docx", ".xls", ".xlsx", ".ppt", ".pptx",
            ".txt", ".rtf", ".odt", ".ods", ".odp",
            ".png", ".jpg", ".jpeg", ".gif", ".webp",
            ".zip", ".7z", ".tar", ".gz",
        ],
        MimeTypes = [], // Accept any MIME type for email attachments within extension whitelist
        MagicBytes = [], // Too many types to enumerate; rely on extension whitelist
        MaxSize = 25 * 1024 * 1024, // 25 MB
    };

    // ── Predefined endpoint sets ─────────────────────────────────────────────

    /// <summary>Allowed file types for avatar uploads (Contacts + UserManagement).</summary>
    public static readonly FileTypeDefinition[] AvatarTypes = [Jpeg, Png, Gif, WebP];

    /// <summary>Allowed file types for Tracks CSV import.</summary>
    public static readonly FileTypeDefinition[] CsvImportTypes = [Csv];

    /// <summary>Allowed file types for Bookmarks HTML import.</summary>
    public static readonly FileTypeDefinition[] BookmarkImportTypes = [Html];

    /// <summary>Allowed file types for email attachments.</summary>
    public static readonly FileTypeDefinition[] EmailAttachmentTypes = [EmailAttachment];
}
