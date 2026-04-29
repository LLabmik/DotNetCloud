using DotNetCloud.Core.DTOs;

namespace DotNetCloud.Modules.Tracks.Models;

/// <summary>
/// Membership of a user in a Product with a specific role.
/// </summary>
public sealed class ProductMember
{
    public Guid ProductId { get; set; }
    public Guid UserId { get; set; }
    public ProductMemberRole Role { get; set; } = ProductMemberRole.Viewer;
    public DateTime JoinedAt { get; set; } = DateTime.UtcNow;

    public Product? Product { get; set; }
}
