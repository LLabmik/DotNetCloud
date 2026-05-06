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

        var documents = new List<SearchDocument>();
        foreach (var thread in threads)
        {
            documents.Add(await ToSearchDocumentWithAttachmentsAsync(thread, ct));
        }
        return documents;
    }

    /// <inheritdoc />
    public async Task<SearchDocument?> GetSearchableDocumentAsync(string entityId, CancellationToken ct = default)
    {
        if (!Guid.TryParse(entityId, out var id))
            return null;

        var thread = await _db.EmailThreads.AsNoTracking()
            .FirstOrDefaultAsync(t => t.Id == id, ct);

        return thread is null ? null : await ToSearchDocumentWithAttachmentsAsync(thread, ct);
    }

    private async Task<SearchDocument> ToSearchDocumentWithAttachmentsAsync(Models.EmailThread thread, CancellationToken ct)
    {
        var metadata = new Dictionary<string, string>
        {
            ["AccountId"] = thread.AccountId.ToString(),
            ["MessageCount"] = thread.MessageCount.ToString()
        };

        // Include attachment filenames in searchable content
        var attachmentNames = await _db.EmailMessages
            .AsNoTracking()
            .Where(m => m.ThreadId == thread.Id)
            .SelectMany(m => m.Attachments)
            .Select(a => a.FileName)
            .Distinct()
            .ToListAsync(ct);

        var attachmentContent = attachmentNames.Count > 0
            ? " " + string.Join(" ", attachmentNames)
            : "";

        return new SearchDocument
        {
            ModuleId = "email",
            EntityId = thread.Id.ToString(),
            EntityType = "EmailThread",
            Title = thread.Subject,
            Content = $"{thread.Subject} {thread.Snippet ?? ""}{attachmentContent}",
            Summary = thread.Snippet,
            OwnerId = Guid.Empty, // Email threads are tied to accounts; search handles ownership via account association
            CreatedAt = thread.CreatedAt,
            UpdatedAt = thread.UpdatedAt,
            Metadata = metadata
        };
    }
}
