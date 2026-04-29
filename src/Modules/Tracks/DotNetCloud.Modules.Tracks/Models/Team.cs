namespace DotNetCloud.Modules.Tracks.Models;

/// <summary>
/// A named team that groups users for collaboration on Tracks boards.
/// </summary>
public sealed class Team
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public Guid CreatedByUserId { get; set; }

    public ICollection<TeamRole> TeamRoles { get; set; } = new List<TeamRole>();
}
