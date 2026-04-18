namespace DotNetCloud.Modules.Photos.Models;

/// <summary>
/// Records a non-destructive edit operation on a photo.
/// Edit operations are stored as JSON and applied on demand.
/// </summary>
public sealed class PhotoEditRecord
{
    /// <summary>Unique identifier.</summary>
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>The photo this edit applies to.</summary>
    public Guid PhotoId { get; set; }

    /// <summary>The type of edit operation (Crop, Rotate, Flip, etc.).</summary>
    public required string OperationType { get; set; }

    /// <summary>JSON-serialized operation parameters.</summary>
    public required string ParametersJson { get; set; }

    /// <summary>Order in the edit stack (applied sequentially).</summary>
    public int StackOrder { get; set; }

    /// <summary>User who applied the edit.</summary>
    public Guid EditedByUserId { get; set; }

    /// <summary>When the edit was applied (UTC).</summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>Navigation property to the parent photo.</summary>
    public Photo? Photo { get; set; }
}
