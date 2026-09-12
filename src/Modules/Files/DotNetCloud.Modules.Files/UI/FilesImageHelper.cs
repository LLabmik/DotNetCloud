namespace DotNetCloud.Modules.Files.UI;

/// <summary>
/// Helpers for detecting and filtering image files for the Files module image gallery view.
/// Shared by <c>FileBrowser</c> (gallery view) and <c>FilePreview</c> (slideshow navigation).
/// </summary>
internal static class FilesImageHelper
{
    /// <summary>
    /// Image file extensions used as a fallback when the MIME type is missing or generic.
    /// </summary>
    private static readonly HashSet<string> ImageExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        "png", "jpg", "jpeg", "gif", "webp", "svg", "bmp", "ico",
        "tif", "tiff", "avif", "heic", "heif", "jfif", "pjpeg", "pjp"
    };

    /// <summary>
    /// Returns <c>true</c> when the node is a real (non-virtual) file that can be displayed as an image.
    /// </summary>
    internal static bool IsImage(FileNodeViewModel? node)
    {
        if (node is null)
            return false;

        if (node.IsVirtual || !string.Equals(node.NodeType, "File", StringComparison.OrdinalIgnoreCase))
            return false;

        return IsImage(node.MimeType, node.Name);
    }

    /// <summary>
    /// Returns <c>true</c> when the MIME type or file name extension indicates an image.
    /// </summary>
    internal static bool IsImage(string? mimeType, string? fileName)
    {
        if (!string.IsNullOrWhiteSpace(mimeType)
            && mimeType.StartsWith("image/", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        var extension = Path.GetExtension(fileName)?.TrimStart('.').ToLowerInvariant();
        return !string.IsNullOrEmpty(extension) && ImageExtensions.Contains(extension);
    }

    /// <summary>
    /// Filters a sequence of nodes down to displayable image files, preserving input order.
    /// </summary>
    internal static IReadOnlyList<FileNodeViewModel> Filter(IEnumerable<FileNodeViewModel> nodes)
        => nodes.Where(IsImage).ToList();
}
