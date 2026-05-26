namespace DotNetCloud.Modules.Video.Models;

/// <summary>
/// Identifies the type of a video series.
/// </summary>
public enum SeriesType
{
    /// <summary>A franchise of related movies (e.g. Star Wars, Lord of the Rings).</summary>
    MovieFranchise = 0,

    /// <summary>A TV series with seasons and episodes (e.g. Breaking Bad).</summary>
    TvSeries = 1
}
