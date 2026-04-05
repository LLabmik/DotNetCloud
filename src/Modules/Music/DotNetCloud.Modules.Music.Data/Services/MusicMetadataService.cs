using DotNetCloud.Core.DTOs;
using DotNetCloud.Modules.Music.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace DotNetCloud.Modules.Music.Data.Services;

/// <summary>
/// Extracts metadata from audio files and maps to music entities.
/// </summary>
public sealed class MusicMetadataService
{
    private readonly ILogger<MusicMetadataService> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="MusicMetadataService"/> class.
    /// </summary>
    public MusicMetadataService(ILogger<MusicMetadataService> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Extracts metadata from an audio file path and populates track properties.
    /// </summary>
    /// <returns>Extracted metadata, or null if the file cannot be read.</returns>
    public AudioMetadata? ExtractMetadata(string filePath)
    {
        try
        {
            using var tagFile = TagLib.File.Create(filePath);
            var tag = tagFile.Tag;
            var properties = tagFile.Properties;

            return new AudioMetadata
            {
                Title = string.IsNullOrWhiteSpace(tag.Title) ? Path.GetFileNameWithoutExtension(filePath) : tag.Title,
                Artist = tag.FirstPerformer ?? tag.FirstAlbumArtist ?? "Unknown Artist",
                AlbumArtist = tag.FirstAlbumArtist,
                Album = tag.Album ?? "Unknown Album",
                TrackNumber = tag.Track > 0 ? (int)tag.Track : null,
                DiscNumber = tag.Disc > 0 ? (int)tag.Disc : null,
                Year = tag.Year > 0 ? (int)tag.Year : null,
                Genre = tag.FirstGenre,
                DurationTicks = properties.Duration.Ticks,
                Bitrate = properties.AudioBitrate > 0 ? properties.AudioBitrate * 1000L : null,
                SampleRate = properties.AudioSampleRate > 0 ? properties.AudioSampleRate : null,
                Channels = properties.AudioChannels > 0 ? properties.AudioChannels : null,
                HasEmbeddedArt = tag.Pictures.Length > 0
            };
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to extract metadata from {FilePath}", filePath);
            return null;
        }
    }

    /// <summary>
    /// Extracts embedded album art from an audio file.
    /// </summary>
    /// <returns>The image data and MIME type, or null if no art is embedded.</returns>
    public (byte[] Data, string MimeType)? ExtractEmbeddedArt(string filePath)
    {
        try
        {
            using var tagFile = TagLib.File.Create(filePath);
            var picture = tagFile.Tag.Pictures.FirstOrDefault();
            if (picture is null)
                return null;

            return (picture.Data.Data, picture.MimeType ?? "image/jpeg");
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to extract album art from {FilePath}", filePath);
            return null;
        }
    }
}

/// <summary>
/// Metadata extracted from an audio file.
/// </summary>
public sealed class AudioMetadata
{
    /// <summary>Track title.</summary>
    public required string Title { get; init; }

    /// <summary>Track artist name.</summary>
    public required string Artist { get; init; }

    /// <summary>Album artist name (may differ from track artist).</summary>
    public string? AlbumArtist { get; init; }

    /// <summary>Album name.</summary>
    public required string Album { get; init; }

    /// <summary>Track number on the album.</summary>
    public int? TrackNumber { get; init; }

    /// <summary>Disc number.</summary>
    public int? DiscNumber { get; init; }

    /// <summary>Release year.</summary>
    public int? Year { get; init; }

    /// <summary>Primary genre name.</summary>
    public string? Genre { get; init; }

    /// <summary>Duration in ticks.</summary>
    public long DurationTicks { get; init; }

    /// <summary>Audio bitrate in bps.</summary>
    public long? Bitrate { get; init; }

    /// <summary>Sample rate in Hz.</summary>
    public int? SampleRate { get; init; }

    /// <summary>Number of audio channels.</summary>
    public int? Channels { get; init; }

    /// <summary>Whether embedded album art is present.</summary>
    public bool HasEmbeddedArt { get; init; }
}
