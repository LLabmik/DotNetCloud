using DotNetCloud.Modules.Tracks.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace DotNetCloud.Modules.Tracks.Data.Services;

/// <summary>
/// Clears orphaned file references from work item attachments using <see cref="TracksDbContext"/>.
/// </summary>
internal sealed class AttachmentCleanupService : ICardAttachmentCleanupService
{
    private readonly TracksDbContext _db;
    private readonly ILogger<AttachmentCleanupService> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="AttachmentCleanupService"/> class.
    /// </summary>
    public AttachmentCleanupService(TracksDbContext db, ILogger<AttachmentCleanupService> logger)
    {
        _db = db;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<int> ClearFileReferencesAsync(Guid fileNodeId, CancellationToken cancellationToken)
    {
        var attachments = await _db.WorkItemAttachments
            .Where(a => a.FileNodeId == fileNodeId)
            .ToListAsync(cancellationToken);

        if (attachments.Count == 0)
        {
            _logger.LogDebug("No work item attachments reference FileNodeId {FileNodeId}", fileNodeId);
            return 0;
        }

        foreach (var attachment in attachments)
        {
            attachment.FileNodeId = null;
            _logger.LogInformation("Cleared FileNodeId on work item attachment {AttachmentId} (was referencing {FileNodeId})",
                attachment.Id, fileNodeId);
        }

        await _db.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Cleaned up {Count} work item attachments for deleted file {FileNodeId}",
            attachments.Count, fileNodeId);

        return attachments.Count;
    }
}
