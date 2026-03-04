using Microsoft.AspNetCore.Components;

namespace DotNetCloud.Modules.Files.UI;

/// <summary>
/// Code-behind for the file preview component.
/// </summary>
public partial class FilePreview : ComponentBase
{
    [Parameter] public FileNodeViewModel? Node { get; set; }
    [Parameter] public EventCallback OnClose { get; set; }

    protected bool IsImage => Node?.MimeType?.StartsWith("image/") == true;
    protected bool IsText => Node?.MimeType?.StartsWith("text/") == true;
    protected bool IsPdf => Node?.MimeType == "application/pdf";
    protected bool IsDocument => Node?.MimeType is not null &&
        (Node.MimeType.Contains("document") || Node.MimeType.Contains("word") ||
         Node.MimeType.Contains("spreadsheet") || Node.MimeType.Contains("excel") ||
         Node.MimeType.Contains("presentation") || Node.MimeType.Contains("powerpoint") ||
         Node.MimeType.Contains("opendocument"));

    protected void Download()
    {
        // In a full implementation, trigger file download via API
    }

    protected void OpenInCollabora()
    {
        // In a full implementation, open Collabora iframe for document editing
    }

    protected static string GetFileIcon(string? mimeType)
    {
        if (mimeType is null) return "[File]";
        if (mimeType.StartsWith("image/")) return "[Image]";
        if (mimeType.StartsWith("video/")) return "[Video]";
        if (mimeType.StartsWith("audio/")) return "[Audio]";
        if (mimeType == "application/pdf") return "[PDF]";
        return "[File]";
    }

    protected static string FormatSize(long bytes)
    {
        if (bytes < 1024) return $"{bytes} B";
        if (bytes < 1024 * 1024) return $"{bytes / 1024.0:F1} KB";
        if (bytes < 1024L * 1024 * 1024) return $"{bytes / (1024.0 * 1024.0):F1} MB";
        return $"{bytes / (1024.0 * 1024.0 * 1024.0):F2} GB";
    }
}
