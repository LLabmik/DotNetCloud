using DotNetCloud.Core.Capabilities;
using DotNetCloud.Core.DTOs.Search;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace DotNetCloud.Modules.Bookmarks.Data.Services;

/// <summary>
/// Implements <see cref="ISearchableModule"/> to expose bookmark content for full-text search indexing.
/// </summary>
public sealed class BookmarksSearchableModule : ISearchableModule
{
    private readonly BookmarksDbContext _db;
    private readonly ILogger<BookmarksSearchableModule> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="BookmarksSearchableModule"/> class.
    /// </summary>
    public BookmarksSearchableModule(BookmarksDbContext db, ILogger<BookmarksSearchableModule> logger)
    {
        _db = db;
        _logger = logger;
    }

    /// <inheritdoc />
    public string ModuleId => "bookmarks";

    /// <inheritdoc />
    public IReadOnlyCollection<string> SupportedEntityTypes { get; } = ["Bookmark"];

    /// <inheritdoc />
    public async Task<IReadOnlyList<SearchDocument>> GetAllSearchableDocumentsAsync(CancellationToken ct = default)
    {
        var bookmarks = await _db.Bookmarks.AsNoTracking()
            .Where(b => !b.IsDeleted)
            .ToListAsync(ct);

        return bookmarks.Select(ToSearchDocument).ToList();
    }

    /// <inheritdoc />
    public async Task<SearchDocument?> GetSearchableDocumentAsync(string entityId, CancellationToken ct = default)
    {
        if (!Guid.TryParse(entityId, out var id))
            return null;

        var bookmark = await _db.Bookmarks.AsNoTracking()
            .FirstOrDefaultAsync(b => b.Id == id && !b.IsDeleted, ct);

        return bookmark is null ? null : ToSearchDocument(bookmark);
    }

    private static SearchDocument ToSearchDocument(Models.BookmarkItem b)
    {
        var metadata = new Dictionary<string, string>();
        if (b.TagsJson is not null)
            metadata["Tags"] = b.TagsJson;
        if (b.FolderId.HasValue)
            metadata["FolderId"] = b.FolderId.Value.ToString();

        return new SearchDocument
        {
            ModuleId = "bookmarks",
            EntityId = b.Id.ToString(),
            EntityType = "Bookmark",
            Title = b.Title,
            Content = $"{b.Url} {b.Description ?? ""} {b.Notes ?? ""}",
            Summary = b.Description ?? b.Url,
            OwnerId = b.OwnerId,
            CreatedAt = b.CreatedAt,
            UpdatedAt = b.UpdatedAt,
            Metadata = metadata
        };
    }
}
