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

    /// <summary>Searches for TV series by title.</summary>
    Task<IReadOnlyList<TmdbTvSeriesSearchResult>?> SearchTvSeriesAsync(string query, CancellationToken cancellationToken = default);

    /// <summary>Gets full TV series details including seasons, status, genres.</summary>
    Task<TmdbTvSeriesDetail?> GetTvSeriesAsync(int tmdbId, CancellationToken cancellationToken = default);

    /// <summary>Gets season details with episode list for a TV series.</summary>
    Task<TmdbTvSeasonDetail?> GetTvSeasonAsync(int seriesTmdbId, int seasonNumber, CancellationToken cancellationToken = default);

    /// <summary>Searches for movie collections (franchises) by name.</summary>
    Task<IReadOnlyList<TmdbCollectionSearchResult>?> SearchCollectionAsync(string query, CancellationToken cancellationToken = default);

    /// <summary>Gets full collection details including parts (movies in the franchise).</summary>
    Task<TmdbCollectionDetail?> GetCollectionAsync(int collectionId, CancellationToken cancellationToken = default);

    /// <summary>Downloads a poster image from TMDB and returns raw bytes + content type.</summary>
    Task<TmdbImageResult?> DownloadPosterAsync(string posterPath, string size = "w500", CancellationToken cancellationToken = default);
}

// ── Movie DTOs ──

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

public sealed record TmdbCollectionInfo
{
    public int Id { get; init; }
    public string? Name { get; init; }
    public string? PosterPath { get; init; }
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
    public TmdbCollectionInfo? BelongsToCollection { get; init; }
}

public sealed record TmdbGenre
{
    public int Id { get; init; }
    public string Name { get; init; } = "";
}

// ── TV Series DTOs ──

public sealed record TmdbTvSeriesSearchResult
{
    public required int Id { get; init; }
    public required string Name { get; init; }
    public string? Overview { get; init; }
    public string? PosterPath { get; init; }
    public string? FirstAirDate { get; init; }
    public double? VoteAverage { get; init; }
    public List<int> GenreIds { get; init; } = [];
}

public sealed record TmdbTvSeriesDetail
{
    public required int Id { get; init; }
    public required string Name { get; init; }
    public string? Overview { get; init; }
    public string? PosterPath { get; init; }
    public double? VoteAverage { get; init; }
    public string? Status { get; init; }
    public int NumberOfSeasons { get; init; }
    public int NumberOfEpisodes { get; init; }
    public List<TmdbGenre> Genres { get; init; } = [];
    public List<TmdbTvSeasonSummary> Seasons { get; init; } = [];
}

public sealed record TmdbTvSeasonSummary
{
    public int SeasonNumber { get; init; }
    public int EpisodeCount { get; init; }
    public string? Name { get; init; }
    public string? Overview { get; init; }
    public string? PosterPath { get; init; }
    public string? AirDate { get; init; }
}

public sealed record TmdbTvSeasonDetail
{
    public required int Id { get; init; }
    public int SeasonNumber { get; init; }
    public string? Name { get; init; }
    public string? Overview { get; init; }
    public string? PosterPath { get; init; }
    public string? AirDate { get; init; }
    public List<TmdbTvEpisode> Episodes { get; init; } = [];
}

public sealed record TmdbTvEpisode
{
    public required int Id { get; init; }
    public int EpisodeNumber { get; init; }
    public int SeasonNumber { get; init; }
    public string? Name { get; init; }
    public string? Overview { get; init; }
    public string? StillPath { get; init; }
    public double? VoteAverage { get; init; }
}

// ── Collection (Franchise) DTOs ──

public sealed record TmdbCollectionSearchResult
{
    public required int Id { get; init; }
    public required string Name { get; init; }
    public string? Overview { get; init; }
    public string? PosterPath { get; init; }
}

public sealed record TmdbCollectionDetail
{
    public required int Id { get; init; }
    public required string Name { get; init; }
    public string? Overview { get; init; }
    public string? PosterPath { get; init; }
    public List<TmdbCollectionPart> Parts { get; init; } = [];
}

public sealed record TmdbCollectionPart
{
    public required int Id { get; init; }
    public required string Title { get; init; }
    public string? Overview { get; init; }
    public string? PosterPath { get; init; }
    public string? ReleaseDate { get; init; }
    public double? VoteAverage { get; init; }
}

// ── Shared DTOs ──

public sealed record TmdbImageResult
{
    public required byte[] Data { get; init; }
    public required string MimeType { get; init; }
}
