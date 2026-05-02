using DotNetCloud.Core.Capabilities;
using DotNetCloud.Core.DTOs.Search;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace DotNetCloud.Modules.Email.Data.Services;

/// <summary>
/// Implements <see cref="ISearchableModule"/> to expose email content for full-text search indexing.
/// </summary>
public sealed class EmailSearchableModule : ISearchableModule
{
    private readonly EmailDbContext _db;
    private readonly ILogger<EmailSearchableModule> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="EmailSearchableModule"/> class.
    /// </summary>
    public EmailSearchableModule(EmailDbContext db, ILogger<EmailSearchableModule> logger)
    {
        _db = db;
        _logger = logger;
    }

    /// <inheritdoc />
    public string ModuleId => "email";

    /// <inheritdoc />
    public IReadOnlyCollection<string> SupportedEntityTypes { get; } = ["EmailThread", "EmailMessage"];

    /// <inheritdoc />
    public async Task<IReadOnlyList<SearchDocument>> GetAllSearchableDocumentsAsync(CancellationToken ct = default)
    {
        var threads = await _db.EmailThreads.AsNoTracking()
            .Where(t => t.MessageCount > 0)
            .ToListAsync(ct);

        return threads.Select(ToSearchDocument).ToList();
    }

    /// <inheritdoc />
    public async Task<SearchDocument?> GetSearchableDocumentAsync(string entityId, CancellationToken ct = default)
    {
        if (!Guid.TryParse(entityId, out var id))
            return null;

        var thread = await _db.EmailThreads.AsNoTracking()
            .FirstOrDefaultAsync(t => t.Id == id, ct);

        return thread is null ? null : ToSearchDocument(thread);
    }

    private static SearchDocument ToSearchDocument(Models.EmailThread t)
    {
        var metadata = new Dictionary<string, string>
        {
            ["AccountId"] = t.AccountId.ToString(),
            ["MessageCount"] = t.MessageCount.ToString()
        };

        return new SearchDocument
        {
            ModuleId = "email",
            EntityId = t.Id.ToString(),
            EntityType = "EmailThread",
            Title = t.Subject,
            Content = $"{t.Subject} {t.Snippet ?? ""}",
            Summary = t.Snippet,
            OwnerId = Guid.Empty, // Email threads are tied to accounts; search handles ownership via account association
            CreatedAt = t.CreatedAt,
            UpdatedAt = t.UpdatedAt,
            Metadata = metadata
        };
    }
}
