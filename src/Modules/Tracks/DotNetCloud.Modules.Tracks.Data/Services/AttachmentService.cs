using DotNetCloud.Core.DTOs;
using DotNetCloud.Modules.Tracks.Models;
using Microsoft.EntityFrameworkCore;

namespace DotNetCloud.Modules.Tracks.Data.Services;

public sealed class AttachmentService
{
    private readonly TracksDbContext _db;

    public AttachmentService(TracksDbContext db)
    {
        _db = db;
    }

    public async Task<WorkItemAttachmentDto> AddAttachmentAsync(
        Guid workItemId,
        Guid uploadedByUserId,
        string fileName,
        long? fileSize,
        string? mimeType,
        Guid? fileNodeId,
        string? url,
        CancellationToken ct)
    {
        var attachment = new WorkItemAttachment
        {
            WorkItemId = workItemId,
            UploadedByUserId = uploadedByUserId,
            FileName = fileName,
            FileSize = fileSize,
            MimeType = mimeType,
            FileNodeId = fileNodeId,
            Url = url,
            CreatedAt = DateTime.UtcNow
        };

        _db.WorkItemAttachments.Add(attachment);
        await _db.SaveChangesAsync(ct);

        return Map(attachment);
    }

    public async Task<List<WorkItemAttachmentDto>> GetAttachmentsByWorkItemAsync(
        Guid workItemId,
        CancellationToken ct)
    {
        return await _db.WorkItemAttachments
            .Where(a => a.WorkItemId == workItemId)
            .OrderBy(a => a.CreatedAt)
            .Select(a => Map(a))
            .ToListAsync(ct);
    }

    public async Task RemoveAttachmentAsync(Guid attachmentId, CancellationToken ct)
    {
        var attachment = await _db.WorkItemAttachments.FindAsync(new object[] { attachmentId }, ct);

        if (attachment is not null)
        {
            _db.WorkItemAttachments.Remove(attachment);
            await _db.SaveChangesAsync(ct);
        }
    }

    public async Task ClearFileReferencesAsync(Guid fileNodeId, CancellationToken ct)
    {
        var attachments = await _db.WorkItemAttachments
            .Where(a => a.FileNodeId == fileNodeId)
            .ToListAsync(ct);

        foreach (var attachment in attachments)
        {
            attachment.FileNodeId = null;
        }

        if (attachments.Count > 0)
        {
            await _db.SaveChangesAsync(ct);
        }
    }

    private static WorkItemAttachmentDto Map(WorkItemAttachment a) => new()
    {
        Id = a.Id,
        WorkItemId = a.WorkItemId,
        FileNodeId = a.FileNodeId,
        Url = a.Url,
        FileName = a.FileName,
        FileSize = a.FileSize,
        MimeType = a.MimeType,
        UploadedByUserId = a.UploadedByUserId,
        CreatedAt = a.CreatedAt
    };
}
