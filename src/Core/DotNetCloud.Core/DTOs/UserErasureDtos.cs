namespace DotNetCloud.Core.DTOs;

/// <summary>
/// State of one module inside an account-erasure request.
/// </summary>
/// <remarks>
/// Kept separate from the EF entity so the admin API and the UI never depend on the data layer.
/// </remarks>
public sealed class ModuleUserErasureDto
{
    /// <summary>The module that must erase the user's data.</summary>
    public string ModuleId { get; set; } = string.Empty;

    /// <summary><c>Pending</c>, <c>Acknowledged</c> or <c>Failed</c>.</summary>
    public string Status { get; set; } = string.Empty;

    /// <summary>How many delivery attempts have been made.</summary>
    public int Attempts { get; set; }

    /// <summary>The last transport or module error, if any.</summary>
    public string? LastError { get; set; }

    /// <summary>When the module acknowledged the erasure.</summary>
    public DateTime? CompletedAtUtc { get; set; }
}

/// <summary>
/// An account-erasure request and its per-module progress.
/// </summary>
public sealed class UserErasureRequestDto
{
    /// <summary>The request id (also the module-facing idempotency key).</summary>
    public Guid RequestId { get; set; }

    /// <summary>The deleted user (pseudonymous id).</summary>
    public Guid UserId { get; set; }

    /// <summary>When the request was created.</summary>
    public DateTime RequestedAtUtc { get; set; }

    /// <summary><c>Pending</c>, <c>Completed</c>, <c>Failed</c> or <c>Overridden</c>.</summary>
    public string Status { get; set; } = string.Empty;

    /// <summary>When the request reached a terminal state.</summary>
    public DateTime? CompletedAtUtc { get; set; }

    /// <summary>Per-module progress.</summary>
    public IReadOnlyList<ModuleUserErasureDto> Modules { get; set; } = [];
}
