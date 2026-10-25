using System.Net.Http;
using System.Security.Claims;
using DotNetCloud.Core.DTOs;
using DotNetCloud.Core.DTOs.Search;
using DotNetCloud.Core.Localization;
using DotNetCloud.UI.Shared.Components.DataDisplay;
using DotNetCloud.UI.Shared.Components.Dialogs;
using DotNetCloud.UI.Shared.Components.Editors;
using DotNetCloud.UI.Shared.Components.Forms;
using DotNetCloud.UI.Shared.Components.Layout;
using DotNetCloud.UI.Shared.Components.Navigation;
using DotNetCloud.UI.Shared.Components.Notifications;
using DotNetCloud.UI.Shared.Resources;
using DotNetCloud.UI.Shared.Services;
using DotNetCloud.UI.Web.Client.Services;
using DotNetCloud.UI.Web.Client.Shared;
using DotNetCloud.UI.Web.Components.Layout;
using DotNetCloud.UI.Web.Components.Pages;
using DotNetCloud.UI.Web.Components.Pages.Modules.Help;
using DotNetCloud.UI.Web.Components.Shared;
using DotNetCloud.UI.Web.Components.Shared.Search;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.AspNetCore.Components.Routing;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.Localization;
using Microsoft.JSInterop;
using static Microsoft.AspNetCore.Components.Web.RenderMode;

namespace DotNetCloud.UI.Web.Components.Shared.Search;

/// <summary>Code-behind for the <see cref="SearchResultCard"/> component.</summary>
public partial class SearchResultCard : ComponentBase
{
    /// <summary>The search result item to render.</summary>
    [Parameter, EditorRequired]
    public SearchResultItem Item { get; set; } = default!;

    /// <summary>Click callback.</summary>
    [Parameter]
    public EventCallback OnClick { get; set; }

    /// <summary>
    /// Sanitizes highlight markup. Only allows &lt;mark&gt; tags, strips everything else.
    /// The input is already HTML-encoded by SnippetGenerator (XSS-safe), so we only need
    /// to preserve the <c>&lt;mark&gt;</c> tags that were applied after encoding.
    /// </summary>
    private static string SanitizeHighlight(string html)
    {
        if (string.IsNullOrEmpty(html)) return html;

        // The SnippetGenerator already HTML-encodes content before inserting <mark> tags,
        // so the only raw HTML present should be <mark>...</mark> pairs.
        // Strip any tags that are NOT <mark> or </mark> to prevent XSS.
        return StripNonMarkTags(html);
    }

    /// <summary>Removes all HTML tags except &lt;mark&gt; and &lt;/mark&gt;.</summary>
    private static string StripNonMarkTags(string input)
    {
        // Replace <mark> tags with placeholders, strip all other tags, restore marks
        var result = input
            .Replace("<mark>", "\x01MO\x01")
            .Replace("</mark>", "\x01MC\x01");

        // Remove any remaining HTML tags (shouldn't exist if SnippetGenerator is correct)
        result = System.Text.RegularExpressions.Regex.Replace(result, @"<[^>]+>", string.Empty);

        return result
            .Replace("\x01MO\x01", "<mark>")
            .Replace("\x01MC\x01", "</mark>");
    }

    private static string FormatDate(DateTimeOffset date)
    {
        var diff = DateTimeOffset.UtcNow - date;
        if (diff.TotalMinutes < 1) return "Just now";
        if (diff.TotalHours < 1) return $"{(int)diff.TotalMinutes}m ago";
        if (diff.TotalDays < 1) return $"{(int)diff.TotalHours}h ago";
        if (diff.TotalDays < 7) return $"{(int)diff.TotalDays}d ago";
        return date.LocalDateTime.ToString("MMM d, yyyy");
    }

    private static string FormatFileSize(long bytes)
    {
        if (bytes < 1024) return $"{bytes} B";
        if (bytes < 1024 * 1024) return $"{bytes / 1024.0:F1} KB";
        if (bytes < 1024 * 1024 * 1024) return $"{bytes / (1024.0 * 1024.0):F1} MB";
        return $"{bytes / (1024.0 * 1024.0 * 1024.0):F1} GB";
    }

    private static string GetFileTypeLabel(string mimeType) => mimeType switch
    {
        "application/pdf" => "PDF",
        "application/vnd.openxmlformats-officedocument.wordprocessingml.document" => "DOCX",
        "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet" => "XLSX",
        "text/plain" => "Text",
        "text/markdown" => "Markdown",
        "text/csv" => "CSV",
        _ when mimeType.StartsWith("image/") => "Image",
        _ when mimeType.StartsWith("video/") => "Video",
        _ when mimeType.StartsWith("audio/") => "Audio",
        _ => mimeType.Split('/').LastOrDefault() ?? "File"
    };
}
