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
            return BuildMetadata(tagFile, filePath);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to extract metadata from {FilePath}", filePath);
            return null;
        }
    }

    /// <summary>
    /// Extracts metadata from a seekable audio stream using the specified MIME type.
    /// Used for chunk-based storage where the complete file must be reassembled into a stream.
    /// </summary>
    /// <param name="audioStream">Seekable stream containing the complete audio file.</param>
    /// <param name="mimeType">MIME type (e.g. "audio/mpeg") so TagLib knows the format.</param>
    /// <param name="fileName">Display file name (used as fallback title).</param>
    /// <returns>Extracted metadata, or null if the stream cannot be read.</returns>
    public AudioMetadata? ExtractMetadata(Stream audioStream, string mimeType, string fileName)
    {
        try
        {
            var abstraction = new StreamFileAbstraction(fileName, audioStream);

            // When MIME type is null, empty, or generic (application/octet-stream),
            // let TagLib auto-detect the format from the file extension instead.
            TagLib.File tagFile;
            if (string.IsNullOrWhiteSpace(mimeType) || mimeType == "application/octet-stream")
            {
                tagFile = TagLib.File.Create(abstraction, TagLib.ReadStyle.Average);
            }
            else
            {
                tagFile = TagLib.File.Create(abstraction, mimeType, TagLib.ReadStyle.Average);
            }

            using (tagFile)
            {
                return BuildMetadata(tagFile, fileName);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to extract metadata from stream for {FileName}", fileName);
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
            return ExtractArtFromTag(tagFile);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to extract album art from {FilePath}", filePath);
            return null;
        }
    }

    /// <summary>
    /// Extracts embedded album art from a seekable audio stream.
    /// </summary>
    /// <returns>The image data and MIME type, or null if no art is embedded.</returns>
    public (byte[] Data, string MimeType)? ExtractEmbeddedArt(Stream audioStream, string mimeType, string fileName)
    {
        try
        {
            var abstraction = new StreamFileAbstraction(fileName, audioStream);
            TagLib.File tagFile;
            if (string.IsNullOrWhiteSpace(mimeType) || mimeType == "application/octet-stream")
            {
                tagFile = TagLib.File.Create(abstraction, TagLib.ReadStyle.Average);
            }
            else
            {
                tagFile = TagLib.File.Create(abstraction, mimeType, TagLib.ReadStyle.Average);
            }

            using (tagFile)
            {
                return ExtractArtFromTag(tagFile);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to extract album art from stream for {FileName}", fileName);
            return null;
        }
    }

    private static AudioMetadata BuildMetadata(TagLib.File tagFile, string fileNameOrPath)
    {
        var tag = tagFile.Tag;
        var properties = tagFile.Properties;

        return new AudioMetadata
        {
            Title = string.IsNullOrWhiteSpace(tag.Title) ? Path.GetFileNameWithoutExtension(fileNameOrPath) : tag.Title,
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

    private static (byte[] Data, string MimeType)? ExtractArtFromTag(TagLib.File tagFile)
    {
        var picture = tagFile.Tag.Pictures.FirstOrDefault();
        if (picture is null)
            return null;

        return (picture.Data.Data, picture.MimeType ?? "image/jpeg");
    }

    /// <summary>
    /// TagLib file abstraction that reads from an existing stream.
    /// The caller owns the stream lifetime — CloseStream is a no-op.
    /// </summary>
    private sealed class StreamFileAbstraction : TagLib.File.IFileAbstraction
    {
        private readonly Stream _stream;

        public StreamFileAbstraction(string name, Stream stream)
        {
            Name = name;
            _stream = stream;
        }

        public string Name { get; }
        public Stream ReadStream => _stream;
        public Stream WriteStream => _stream;
        public void CloseStream(Stream stream) { /* caller owns the stream */ }
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
