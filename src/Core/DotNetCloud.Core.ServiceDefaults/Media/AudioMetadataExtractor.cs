using DotNetCloud.Core.Capabilities;
using DotNetCloud.Core.DTOs.Media;
using Microsoft.Extensions.Logging;
using TagLib;

namespace DotNetCloud.Core.ServiceDefaults.Media;

/// <summary>
/// Extracts metadata from audio files (MP3, FLAC, OGG, AAC, OPUS, WAV, WMA)
/// using TagLibSharp for ID3v2, Vorbis comments, FLAC tags, and album art detection.
/// </summary>
public sealed class AudioMetadataExtractor : IMediaMetadataExtractor
{
    private static readonly HashSet<string> SupportedMimeTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "audio/mpeg",
        "audio/mp3",
        "audio/flac",
        "audio/ogg",
        "audio/vorbis",
        "audio/opus",
        "audio/aac",
        "audio/mp4",
        "audio/m4a",
        "audio/x-m4a",
        "audio/wav",
        "audio/x-wav",
        "audio/wave",
        "audio/x-ms-wma",
        "audio/webm"
    };

    private readonly ILogger<AudioMetadataExtractor> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="AudioMetadataExtractor"/> class.
    /// </summary>
    /// <param name="logger">Logger instance.</param>
    public AudioMetadataExtractor(ILogger<AudioMetadataExtractor> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <inheritdoc />
    public MediaType SupportedMediaType => MediaType.Audio;

    /// <inheritdoc />
    public bool CanExtract(string mimeType) => SupportedMimeTypes.Contains(mimeType);

    /// <inheritdoc />
    public Task<MediaMetadataDto?> ExtractAsync(
        string filePath,
        string mimeType,
        CancellationToken cancellationToken = default)
    {
        if (!System.IO.File.Exists(filePath))
        {
            _logger.LogWarning("Audio file not found for metadata extraction: {FilePath}", filePath);
            return Task.FromResult<MediaMetadataDto?>(null);
        }

        try
        {
            using var tagFile = TagLib.File.Create(filePath);
            var tag = tagFile.Tag;
            var properties = tagFile.Properties;

            var dto = new MediaMetadataDto
            {
                MediaType = MediaType.Audio,
                Duration = properties.Duration > TimeSpan.Zero ? properties.Duration : null,
                Bitrate = properties.AudioBitrate > 0 ? properties.AudioBitrate * 1000L : null,
                SampleRate = properties.AudioSampleRate > 0 ? properties.AudioSampleRate : null,
                Channels = properties.AudioChannels > 0 ? properties.AudioChannels : null,
                Codec = GetCodecDescription(properties),
                Title = NullIfEmpty(tag.Title),
                Artist = NullIfEmpty(tag.FirstPerformer),
                Album = NullIfEmpty(tag.Album),
                AlbumArtist = NullIfEmpty(tag.FirstAlbumArtist),
                Genre = NullIfEmpty(tag.FirstGenre),
                TrackNumber = tag.Track > 0 ? (int)tag.Track : null,
                TrackCount = tag.TrackCount > 0 ? (int)tag.TrackCount : null,
                DiscNumber = tag.Disc > 0 ? (int)tag.Disc : null,
                DiscCount = tag.DiscCount > 0 ? (int)tag.DiscCount : null,
                Year = tag.Year > 0 ? (int)tag.Year : null,
                HasEmbeddedArt = tag.Pictures.Length > 0
            };

            return Task.FromResult<MediaMetadataDto?>(dto);
        }
        catch (CorruptFileException ex)
        {
            _logger.LogWarning(ex, "Corrupt audio file, cannot extract metadata: {FilePath}", filePath);
            return Task.FromResult<MediaMetadataDto?>(null);
        }
        catch (UnsupportedFormatException ex)
        {
            _logger.LogWarning(ex, "Unsupported audio format for metadata extraction: {FilePath}", filePath);
            return Task.FromResult<MediaMetadataDto?>(null);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to extract audio metadata from {FilePath}.", filePath);
            return Task.FromResult<MediaMetadataDto?>(null);
        }
    }

    private static string? GetCodecDescription(TagLib.Properties properties)
    {
        if (properties.Codecs is null) return null;

        foreach (var codec in properties.Codecs)
        {
            if (codec is TagLib.IAudioCodec audioCodec)
            {
                var desc = audioCodec.Description;
                if (!string.IsNullOrWhiteSpace(desc))
                    return desc;
            }
        }

        return null;
    }

    private static string? NullIfEmpty(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
