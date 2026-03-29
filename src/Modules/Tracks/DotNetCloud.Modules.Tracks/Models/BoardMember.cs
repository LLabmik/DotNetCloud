using DotNetCloud.Core.DTOs;

namespace DotNetCloud.Modules.Tracks.Models;

/// <summary>
/// Represents a user's membership on a board with a specific role.
/// </summary>
public sealed class BoardMember
{
    /// <summary>Unique identifier.</summary>
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>The board this membership belongs to.</summary>
    public Guid BoardId { get; set; }

    /// <summary>The user who is a member.</summary>
    public Guid UserId { get; set; }

    /// <summary>Role on the board (Owner, Admin, Member, Viewer).</summary>
    public BoardMemberRole Role { get; set; } = BoardMemberRole.Member;

    /// <summary>Timestamp when the user was added (UTC).</summary>
    public DateTime JoinedAt { get; set; } = DateTime.UtcNow;

    /// <summary>Navigation property to the board.</summary>
    public Board? Board { get; set; }
}
