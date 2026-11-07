namespace DotNetCloud.Core.Data.Entities.Modules;

/// <summary>
/// Per-module progress within a <see cref="UserErasureRequest"/>.
/// </summary>
public class ModuleUserErasure
{
    /// <summary>Owning erasure request. Part of the composite primary key.</summary>
    public Guid RequestId { get; set; }

    /// <summary>Module that must erase the user's data. Part of the composite primary key.</summary>
    public string ModuleId { get; set; } = string.Empty;

    /// <summary>Current state for this module.</summary>
    public ModuleUserErasureStatus Status { get; set; } = ModuleUserErasureStatus.Pending;

    /// <summary>Number of delivery attempts made.</summary>
    public int Attempts { get; set; }

    /// <summary>Sanitized administrator-facing failure reason.</summary>
    public string? LastError { get; set; }

    /// <summary>When the module acknowledged the erasure.</summary>
    public DateTime? CompletedAtUtc { get; set; }

    /// <summary>Navigation to the owning request.</summary>
    public virtual UserErasureRequest? Request { get; set; }
}
