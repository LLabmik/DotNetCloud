namespace DotNetCloud.Client.Android.Services;

/// <summary>
/// Pure decision helpers for opening (and creating) documents in Collabora Online from the Files tab.
/// Deliberately free of MAUI/Android dependencies so it compiles and runs on plain <c>net10.0</c>.
/// </summary>
public static class CollaboraEditability
{
    /// <summary>
    /// Preferred order for the "New Document" action sheet. Mirrors the Blazor web UI's
    /// <c>_collaboraNewFileExtensions</c> ordering.
    /// </summary>
    private static readonly string[] PreferredNewDocumentExtensions =
        ["docx", "xlsx", "pptx", "odt", "ods", "odp"];

    /// <summary>
    /// Extensions that keep their native handling (download &amp; open) instead of opening in
    /// Collabora — mirrors the Blazor web UI's <c>NativeEditorExtensions</c>, so the Android app
    /// agrees with the web UI about which files the office editor owns.
    /// </summary>
    private static readonly HashSet<string> NativeExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        "txt", "md", "markdown", "json", "xml", "yaml", "yml", "csv", "log",
        "html", "htm", "css", "js", "ts", "cs", "py", "sh", "bash",
        "sql", "ini", "toml", "cfg", "conf", "env"
    };

    /// <summary>
    /// Normalizes an extension (or a file name's extension) to lowercase without a leading dot.
    /// Returns an empty string when there is nothing usable.
    /// </summary>
    public static string NormalizeExtension(string? extension) =>
        string.IsNullOrWhiteSpace(extension)
            ? string.Empty
            : extension.Trim().TrimStart('.').ToLowerInvariant();

    /// <summary>
    /// True when a file should open in the embedded Collabora editor: its extension is reported by
    /// server discovery and does not belong to the native-editor set.
    /// </summary>
    /// <param name="fileName">File name (with extension).</param>
    /// <param name="supportedExtensions">Dot-less extensions reported by WOPI discovery.</param>
    public static bool IsEditable(string? fileName, IReadOnlyCollection<string>? supportedExtensions)
    {
        if (string.IsNullOrEmpty(fileName) || supportedExtensions is null || supportedExtensions.Count == 0)
            return false;

        var extension = NormalizeExtension(Path.GetExtension(fileName));
        if (extension.Length == 0 || NativeExtensions.Contains(extension))
            return false;

        return supportedExtensions.Contains(extension, StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>
    /// The extensions offered by the "New Document" action: the preferred document formats first
    /// (when Collabora supports them), then any remaining Collabora-supported type. Native-editor
    /// extensions are excluded, matching the web UI.
    /// </summary>
    /// <param name="supportedExtensions">Dot-less extensions reported by WOPI discovery.</param>
    public static IReadOnlyList<string> CreatableExtensions(IReadOnlyCollection<string>? supportedExtensions)
    {
        if (supportedExtensions is null || supportedExtensions.Count == 0)
            return [];

        var supported = supportedExtensions
            .Select(NormalizeExtension)
            .Where(ext => ext.Length > 0 && !NativeExtensions.Contains(ext))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var ordered = PreferredNewDocumentExtensions
            .Where(supported.Contains)
            .Concat(supported
                .Where(ext => !PreferredNewDocumentExtensions.Contains(ext, StringComparer.OrdinalIgnoreCase))
                .OrderBy(ext => ext, StringComparer.OrdinalIgnoreCase));

        return [.. ordered];
    }

    /// <summary>
    /// The MIME type recorded for a newly created (empty) document. Mirrors the Blazor web UI's
    /// mapping so both clients create identical metadata.
    /// </summary>
    /// <param name="extension">Dot-less or dotted extension.</param>
    public static string MimeTypeFor(string? extension) => NormalizeExtension(extension) switch
    {
        "docx" => "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
        "docm" => "application/vnd.ms-word.document.macroEnabled.12",
        "odt" => "application/vnd.oasis.opendocument.text",
        "rtf" => "application/rtf",
        "txt" or "log" or "ini" or "cfg" or "conf" or "env" => "text/plain",
        "md" or "markdown" => "text/markdown",
        "json" => "application/json",
        "xml" => "application/xml",
        "html" or "htm" => "text/html",
        "css" => "text/css",
        "js" => "text/javascript",
        "ts" => "text/typescript",
        "cs" => "text/x-csharp",
        "py" => "text/x-python",
        "sh" or "bash" => "text/x-shellscript",
        "sql" => "text/x-sql",
        "yaml" or "yml" => "text/yaml",
        "toml" => "text/toml",
        "xlsx" => "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
        "xlsm" => "application/vnd.ms-excel.sheet.macroEnabled.12",
        "ods" => "application/vnd.oasis.opendocument.spreadsheet",
        "csv" => "text/csv",
        "pptx" => "application/vnd.openxmlformats-officedocument.presentationml.presentation",
        "odp" => "application/vnd.oasis.opendocument.presentation",
        _ => "text/plain"
    };
}
