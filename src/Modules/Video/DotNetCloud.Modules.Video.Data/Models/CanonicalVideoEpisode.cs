namespace DotNetCloud.Modules.Video.Models;

/// <summary>
/// Canonical episode within a season — links a season to a canonical video by ContentHash.
/// Shared across all users.
/// </summary>
public sealed class CanonicalVideoEpisode
{
    /// <summary>Unique identifier.</summary>
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>The season this episode belongs to.</summary>
    public Guid SeasonId { get; set; }

    /// <summary>Content hash of the canonical video for this episode.</summary>
    public required string VideoContentHash { get; set; }

    /// <summary>Episode number within the season.</summary>
    public int EpisodeNumber { get; set; }

    /// <summary>Episode title (may differ from video filename).</summary>
    public string? Title { get; set; }

    /// <summary>Episode-specific overview / description.</summary>
    public string? Overview { get; set; }

    /// <summary>Sort order within the season.</summary>
    public int SortOrder { get; set; }

    /// <summary>When added (UTC).</summary>
    public DateTime AddedAt { get; set; } = DateTime.UtcNow;

    /// <summary>Navigation to the canonical season.</summary>
    public CanonicalVideoSeason? Season { get; set; }
}
