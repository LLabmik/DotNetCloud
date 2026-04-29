namespace DotNetCloud.Modules.Tracks.Models;

/// <summary>
/// File or URL attachment on a work item.
/// </summary>
public sealed class WorkItemAttachment
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid WorkItemId { get; set; }
    public Guid? FileNodeId { get; set; }
    public string? Url { get; set; }
    public required string FileName { get; set; }
    public long? FileSize { get; set; }
    public string? MimeType { get; set; }
    public Guid UploadedByUserId { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public WorkItem? WorkItem { get; set; }
}
