using DotNetCloud.Modules.Tracks.Services;
using Microsoft.Extensions.Logging;

namespace DotNetCloud.Modules.Tracks.Data.Services;

/// <summary>
/// Clears orphaned file references from card attachments using TracksDbContext.
/// </summary>
internal sealed class CardAttachmentCleanupService : ICardAttachmentCleanupService
{
    private readonly TracksDbContext _db;
    private readonly ILogger<CardAttachmentCleanupService> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="CardAttachmentCleanupService"/> class.
    /// </summary>
    public CardAttachmentCleanupService(TracksDbContext db, ILogger<CardAttachmentCleanupService> logger)
    {
        _db = db;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<int> ClearFileReferencesAsync(Guid fileNodeId, CancellationToken cancellationToken)
    {
        var attachments = _db.CardAttachments
            .Where(a => a.FileNodeId == fileNodeId)
            .ToList();

        if (attachments.Count == 0)
        {
            _logger.LogDebug("No card attachments reference FileNodeId {FileNodeId}", fileNodeId);
            return 0;
        }

        foreach (var attachment in attachments)
        {
            attachment.FileNodeId = null;
            _logger.LogInformation("Cleared FileNodeId on card attachment {AttachmentId} (was referencing {FileNodeId})",
                attachment.Id, fileNodeId);
        }

        await _db.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Cleaned up {Count} card attachments for deleted file {FileNodeId}",
            attachments.Count, fileNodeId);

        return attachments.Count;
    }
}
