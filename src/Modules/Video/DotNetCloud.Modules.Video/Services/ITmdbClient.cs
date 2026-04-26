using System.Text.Json.Serialization;

namespace DotNetCloud.Modules.Video.Services;

/// <summary>
/// Low-level typed HTTP client for TMDB API v3.
/// Base URL: https://api.themoviedb.org/3/
/// </summary>
public interface ITmdbClient
{
    /// <summary>Searches for movies by title (and optional year).</summary>
    Task<IReadOnlyList<TmdbMovieSearchResult>?> SearchMovieAsync(string title, int? year = null, CancellationToken cancellationToken = default);

    /// <summary>Gets full movie details including genres, rating, overview.</summary>
    Task<TmdbMovieDetail?> GetMovieAsync(int tmdbId, CancellationToken cancellationToken = default);

    /// <summary>Downloads a poster image from TMDB and returns raw bytes + content type.</summary>
    Task<TmdbImageResult?> DownloadPosterAsync(string posterPath, string size = "w500", CancellationToken cancellationToken = default);
}

// ── DTOs ──

public sealed record TmdbMovieSearchResult
{
    public required int Id { get; init; }
    public required string Title { get; init; }
    public string? Overview { get; init; }
    public string? PosterPath { get; init; }
    public string? ReleaseDate { get; init; }
    public double? VoteAverage { get; init; }
    public List<int> GenreIds { get; init; } = [];
}

public sealed record TmdbMovieDetail
{
    public required int Id { get; init; }
    public required string Title { get; init; }
    public string? Overview { get; init; }
    public string? PosterPath { get; init; }
    public DateTime? ReleaseDate { get; init; }
    public double? VoteAverage { get; init; }
    public List<TmdbGenre> Genres { get; init; } = [];
}

public sealed record TmdbGenre
{
    public int Id { get; init; }
    public string Name { get; init; } = "";
}

public sealed record TmdbImageResult
{
    public required byte[] Data { get; init; }
    public required string MimeType { get; init; }
}
