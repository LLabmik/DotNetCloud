using DotNetCloud.Core.Capabilities;
using DotNetCloud.Core.DTOs.Search;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace DotNetCloud.Modules.Notes.Data.Services;

/// <summary>
/// Exposes Notes module data for full-text search indexing.
/// Provides note title and content as <see cref="SearchDocument"/> instances.
/// </summary>
public sealed class NotesSearchableModule : ISearchableModule
{
    private readonly NotesDbContext _db;
    private readonly ILogger<NotesSearchableModule> _logger;

    /// <summary>Initializes a new instance of the <see cref="NotesSearchableModule"/> class.</summary>
    public NotesSearchableModule(NotesDbContext db, ILogger<NotesSearchableModule> logger)
    {
        _db = db;
        _logger = logger;
    }

    /// <inheritdoc />
    public string ModuleId => "notes";

    /// <inheritdoc />
    public IReadOnlyCollection<string> SupportedEntityTypes { get; } = ["Note"];

    /// <inheritdoc />
    public async Task<IReadOnlyList<SearchDocument>> GetAllSearchableDocumentsAsync(CancellationToken cancellationToken = default)
    {
        var notes = await _db.Notes
            .Where(n => !n.IsDeleted)
            .ToListAsync(cancellationToken);

        _logger.LogDebug("Retrieved {Count} notes for search indexing", notes.Count);

        return notes.Select(ToSearchDocument).ToList();
    }

    /// <inheritdoc />
    public async Task<SearchDocument?> GetSearchableDocumentAsync(string entityId, CancellationToken cancellationToken = default)
    {
        if (!Guid.TryParse(entityId, out var id))
            return null;

        var note = await _db.Notes
            .FirstOrDefaultAsync(n => n.Id == id && !n.IsDeleted, cancellationToken);

        return note is null ? null : ToSearchDocument(note);
    }

    private static SearchDocument ToSearchDocument(Models.Note note)
    {
        var metadata = new Dictionary<string, string>
        {
            ["Format"] = note.Format.ToString()
        };
        if (note.IsPinned) metadata["Pinned"] = "true";
        if (note.IsFavorite) metadata["Favorite"] = "true";

        return new SearchDocument
        {
            ModuleId = "notes",
            EntityId = note.Id.ToString(),
            EntityType = "Note",
            Title = note.Title,
            Content = note.Content,
            Summary = note.Content.Length > 200 ? note.Content[..200] + "..." : note.Content,
            OwnerId = note.OwnerId,
            CreatedAt = new DateTimeOffset(note.CreatedAt, TimeSpan.Zero),
            UpdatedAt = new DateTimeOffset(note.UpdatedAt, TimeSpan.Zero),
            Metadata = metadata
        };
    }
}
