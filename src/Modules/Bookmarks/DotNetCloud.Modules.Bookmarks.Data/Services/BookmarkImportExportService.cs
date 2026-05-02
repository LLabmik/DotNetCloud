using DotNetCloud.Core.Authorization;
using DotNetCloud.Modules.Bookmarks.Models;
using DotNetCloud.Modules.Bookmarks.Services;
using AngleSharp.Html.Parser;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System.Text;

namespace DotNetCloud.Modules.Bookmarks.Data.Services;

/// <summary>
/// Imports and exports bookmarks in the Netscape Bookmark File Format (bookmarks.html).
/// </summary>
public sealed class BookmarkImportExportService : IBookmarkImportExportService
{
    private readonly BookmarksDbContext _db;
    private readonly ILogger<BookmarkImportExportService> _logger;

    public BookmarkImportExportService(BookmarksDbContext db, ILogger<BookmarkImportExportService> logger)
    {
        _db = db;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<BookmarkImportResult> ImportHtmlAsync(Stream htmlStream, Guid? targetFolderId, CallerContext caller, CancellationToken ct = default)
    {
        var result = new BookmarkImportResult();
        var parser = new HtmlParser();
        using var document = await parser.ParseDocumentAsync(htmlStream, ct);

        var rootDl = document.QuerySelector("body > dl") ?? document.QuerySelector("dl");
        if (rootDl is null)
        {
            _logger.LogWarning("No bookmark structure found in import file");
            return result;
        }

        var folderMap = new Dictionary<string, Guid>();
        await ImportFolderContentsAsync(rootDl, targetFolderId, caller, result, folderMap, ct);

        await _db.SaveChangesAsync(ct);
        _logger.LogInformation("Bookmark import complete: {Imported} bookmarks, {Folders} folders, {Skipped} skipped",
            result.ImportedCount, result.FolderCount, result.SkippedCount);

        return result;
    }

    private async Task ImportFolderContentsAsync(AngleSharp.Dom.IElement dlElement, Guid? parentFolderId, CallerContext caller,
        BookmarkImportResult result, Dictionary<string, Guid> folderMap, CancellationToken ct)
    {
        foreach (var dt in dlElement.Children)
        {
            if (dt.TagName is not "DT") continue;

            var heading = dt.QuerySelector("h3");
            if (heading is not null)
            {
                var folderName = heading.TextContent.Trim();
                if (string.IsNullOrWhiteSpace(folderName)) continue;

                var importPath = BuildImportPath(folderMap, parentFolderId, folderName);

                var folder = new BookmarkFolder
                {
                    OwnerId = caller.UserId,
                    Name = folderName,
                    ParentId = parentFolderId
                };
                _db.BookmarkFolders.Add(folder);
                folderMap[importPath] = folder.Id;
                result.FolderCount++;

                var nestedDl = dt.QuerySelector("dl");
                if (nestedDl is not null)
                    await ImportFolderContentsAsync(nestedDl, folder.Id, caller, result, folderMap, ct);
            }
            else
            {
                var anchor = dt.QuerySelector("a");
                if (anchor is null) continue;

                var url = anchor.GetAttribute("href");
                if (string.IsNullOrWhiteSpace(url))
                {
                    result.SkippedCount++;
                    continue;
                }

                if (!Uri.TryCreate(url, UriKind.Absolute, out var uri)
                    || (uri.Scheme != "http" && uri.Scheme != "https"))
                {
                    result.SkippedCount++;
                    continue;
                }

                var title = anchor.TextContent.Trim();
                var addDateAttr = anchor.GetAttribute("add_date");
                var tags = anchor.GetAttribute("tags");

                var bookmark = new BookmarkItem
                {
                    OwnerId = caller.UserId,
                    Url = url,
                    NormalizedUrl = NormalizeUrl(url),
                    Title = string.IsNullOrWhiteSpace(title) ? url : title,
                    FolderId = parentFolderId,
                    TagsJson = !string.IsNullOrWhiteSpace(tags)
                        ? System.Text.Json.JsonSerializer.Serialize(tags.Split(',').Select(t => t.Trim()).Where(t => t.Length > 0).ToList())
                        : null
                };

                if (!string.IsNullOrWhiteSpace(addDateAttr) && long.TryParse(addDateAttr, out var unixSeconds))
                    bookmark.CreatedAt = DateTimeOffset.FromUnixTimeSeconds(unixSeconds).UtcDateTime;

                _db.Bookmarks.Add(bookmark);
                result.ImportedCount++;
            }
        }
    }

    private static string BuildImportPath(Dictionary<string, Guid> folderMap, Guid? parentId, string name)
    {
        var parentPath = parentId.HasValue && folderMap.ContainsValue(parentId.Value)
            ? folderMap.First(kvp => kvp.Value == parentId.Value).Key
            : "";
        return string.IsNullOrEmpty(parentPath) ? name : $"{parentPath}/{name}";
    }

    /// <inheritdoc />
    public async Task<Stream> ExportHtmlAsync(CallerContext caller, Guid? folderId, CancellationToken ct = default)
    {
        var folders = await _db.BookmarkFolders
            .AsNoTracking()
            .Where(f => f.OwnerId == caller.UserId && !f.IsDeleted)
            .OrderBy(f => f.SortOrder).ThenBy(f => f.Name)
            .ToListAsync(ct);

        var bookmarks = await _db.Bookmarks
            .AsNoTracking()
            .Where(b => b.OwnerId == caller.UserId && !b.IsDeleted
                        && (!folderId.HasValue || b.FolderId == folderId.Value))
            .OrderBy(b => b.CreatedAt)
            .ToListAsync(ct);

        var rootFolders = folders.Where(f => f.ParentId == null).ToList();
        var childFoldersByParent = folders
            .Where(f => f.ParentId.HasValue)
            .GroupBy(f => f.ParentId!.Value)
            .ToDictionary(g => g.Key, g => g.ToList());

        var bookmarksByFolder = bookmarks
            .GroupBy(b => b.FolderId ?? Guid.Empty)
            .ToDictionary(g => g.Key, g => g.ToList());

        // Bookmarks not in any folder have FolderId == null, mapped to Guid.Empty
        var unassigned = bookmarksByFolder.GetValueOrDefault(Guid.Empty, []);

        var sb = new StringBuilder();
        sb.AppendLine("<!DOCTYPE NETSCAPE-Bookmark-file-1>");
        sb.AppendLine("<!-- This is an automatically generated file. -->");
        sb.AppendLine("<META HTTP-EQUIV=\"Content-Type\" CONTENT=\"text/html; charset=UTF-8\">");
        sb.AppendLine("<TITLE>Bookmarks</TITLE>");
        sb.AppendLine("<H1>Bookmarks</H1>");
        sb.AppendLine("<DL><p>");

        WriteFolders(sb, rootFolders, childFoldersByParent, bookmarksByFolder, 1);

        foreach (var bm in unassigned)
            WriteBookmarkEntry(sb, bm, 2);

        sb.AppendLine("</DL><p>");

        var bytes = Encoding.UTF8.GetBytes(sb.ToString());
        return new MemoryStream(bytes);
    }

    private static void WriteFolders(StringBuilder sb, List<BookmarkFolder> folders,
        Dictionary<Guid, List<BookmarkFolder>> childrenByParent,
        Dictionary<Guid, List<BookmarkItem>> bookmarksByFolder,
        int indent)
    {
        var pad = new string(' ', indent * 4);
        foreach (var folder in folders)
        {
            sb.AppendLine($"{pad}<DT><H3>{EscapeHtml(folder.Name)}</H3>");
            sb.AppendLine($"{pad}<DL><p>");

            if (bookmarksByFolder.TryGetValue(folder.Id, out var folderBookmarks))
            {
                foreach (var bm in folderBookmarks)
                    WriteBookmarkEntry(sb, bm, indent + 1);
            }

            if (childrenByParent.TryGetValue(folder.Id, out var children))
                WriteFolders(sb, children, childrenByParent, bookmarksByFolder, indent + 1);

            sb.AppendLine($"{pad}</DL><p>");
        }
    }

    private static void WriteBookmarkEntry(StringBuilder sb, BookmarkItem bookmark, int indent)
    {
        var pad = new string(' ', indent * 4);
        var addDate = new DateTimeOffset(bookmark.CreatedAt, TimeSpan.Zero).ToUnixTimeSeconds();
        var tags = bookmark.TagsJson is not null
            ? string.Join(',', System.Text.Json.JsonSerializer.Deserialize<List<string>>(bookmark.TagsJson) ?? [])
            : "";
        sb.AppendLine($"{pad}<DT><A HREF=\"{EscapeHtml(bookmark.Url)}\" ADD_DATE=\"{addDate}\" TAGS=\"{EscapeHtml(tags)}\">{EscapeHtml(bookmark.Title)}</A>");
    }

    private static string EscapeHtml(string value) =>
        System.Net.WebUtility.HtmlEncode(value);

    private static string NormalizeUrl(string url)
    {
        if (Uri.TryCreate(url, UriKind.Absolute, out var uri))
            return uri.GetLeftPart(UriPartial.Authority) + uri.AbsolutePath + uri.Query;
        return url.ToLowerInvariant().TrimEnd('/');
    }
}
