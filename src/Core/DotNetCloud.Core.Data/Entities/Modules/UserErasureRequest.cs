namespace DotNetCloud.Core.Data.Entities.Modules;

/// <summary>
/// Tracks a request to erase one user's data from every module.
/// </summary>
/// <remarks>
/// Account deletion is only reported complete once every reachable module acknowledges the
/// erasure. Unreachable modules leave the request <see cref="UserErasureRequestStatus.Pending"/>
/// and it is retried; an administrator can override, which is audit-logged.
/// </remarks>
public class UserErasureRequest
{
    /// <summary>Idempotency key for the request.</summary>
    public Guid RequestId { get; set; }

    /// <summary>The user whose data is being erased.</summary>
    public Guid UserId { get; set; }

    /// <summary>When the request was created.</summary>
    public DateTime RequestedAtUtc { get; set; }

    /// <summary>Who requested the erasure (null for self-service).</summary>
    public Guid? RequestedByUserId { get; set; }

    /// <summary>Current state of the request.</summary>
    public UserErasureRequestStatus Status { get; set; } = UserErasureRequestStatus.Pending;

    /// <summary>When the request reached a terminal state.</summary>
    public DateTime? CompletedAtUtc { get; set; }

    /// <summary>Per-module erasure state.</summary>
    public virtual ICollection<ModuleUserErasure> Modules { get; set; } = new List<ModuleUserErasure>();
}
