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

    private AudioMetadata BuildMetadata(TagLib.File tagFile, string fileNameOrPath)
    {
        var tag = tagFile.Tag;
        var properties = tagFile.Properties;

        var trackNum = TryGetTrackNumber(tag, tagFile) ?? TryExtractTrackNumberFromFileName(fileNameOrPath);
        var discNum = TryGetDiscNumber(tag, tagFile);

        if (trackNum is null)
        {
            _logger.LogWarning("TrackNumber extraction returned NULL: tag.Track={TagTrack}, TagTypes={TagTypes}, file={File}",
                tag.Track, tag.TagTypes, Path.GetFileName(fileNameOrPath));
        }

        var result = new AudioMetadata
        {
            Title = string.IsNullOrWhiteSpace(tag.Title) ? Path.GetFileNameWithoutExtension(fileNameOrPath) : tag.Title,
            Artist = tag.FirstPerformer ?? tag.FirstAlbumArtist ?? "Unknown Artist",
            AlbumArtist = tag.FirstAlbumArtist,
            Album = tag.Album ?? "Unknown Album",
            TrackNumber = TryGetTrackNumber(tag, tagFile) ?? TryExtractTrackNumberFromFileName(fileNameOrPath),
            DiscNumber = TryGetDiscNumber(tag, tagFile),
            Year = tag.Year > 0 ? (int)tag.Year : null,
            Genre = tag.FirstGenre,
            DurationTicks = properties.Duration.Ticks,
            Bitrate = properties.AudioBitrate > 0 ? properties.AudioBitrate * 1000L : null,
            SampleRate = properties.AudioSampleRate > 0 ? properties.AudioSampleRate : null,
            Channels = properties.AudioChannels > 0 ? properties.AudioChannels : null,
            HasEmbeddedArt = tag.Pictures.Length > 0,
            MusicBrainzTrackId = GetMusicBrainzId(tagFile, "MUSICBRAINZ_TRACK_ID") ?? GetMusicBrainzId(tagFile, "MusicBrainz Recording Id"),
            MusicBrainzArtistId = GetMusicBrainzId(tagFile, "MUSICBRAINZ_ARTIST_ID") ?? GetMusicBrainzId(tagFile, "MusicBrainz Artist Id"),
            MusicBrainzAlbumId = GetMusicBrainzId(tagFile, "MUSICBRAINZ_ALBUM_ID") ?? GetMusicBrainzId(tagFile, "MusicBrainz Album Id"),
            MusicBrainzReleaseGroupId = GetMusicBrainzId(tagFile, "MUSICBRAINZ_RELEASE_GROUP_ID"),
            MusicBrainzReleaseArtistId = GetMusicBrainzId(tagFile, "MUSICBRAINZ_RELEASE_ARTIST_ID"),
            MusicBrainzDiscId = GetMusicBrainzId(tagFile, "MUSICBRAINZ_DISC_ID"),
            Isrc = tag.ISRC,
            Bpm = tag.BeatsPerMinute > 0 ? (int)tag.BeatsPerMinute : null,
            Composers = string.Join("; ", tag.Composers ?? [])
        };

        return result;
    }

    private static string? GetMusicBrainzId(TagLib.File tagFile, string fieldName)
    {
        var tag = tagFile.Tag;

        // Try TagLib's built-in properties first, then fall back to raw tag iteration
        try
        {
            // Check Xiph comments (FLAC, Vorbis)
            if (tag.TagTypes.HasFlag(TagLib.TagTypes.Xiph))
            {
                var xiph = (TagLib.Ogg.XiphComment)tagFile.GetTag(TagLib.TagTypes.Xiph);
                if (xiph.GetField(fieldName) is { Length: > 0 } fields)
                    return fields[0];
            }
        }
        catch
        {
            // Ignore — fall through to next attempt
        }

        try
        {
            // Check ID3v2 TXXX frames (MP3)
            if (tag.TagTypes.HasFlag(TagLib.TagTypes.Id3v2))
            {
                var id3v2 = (TagLib.Id3v2.Tag)tagFile.GetTag(TagLib.TagTypes.Id3v2);
                foreach (var frame in id3v2.GetFrames<TagLib.Id3v2.UserTextInformationFrame>())
                {
                    if (frame.Description == fieldName && frame.Text?.Length > 0)
                        return frame.Text[0];
                }
            }
        }
        catch
        {
            // Ignore — fall through
        }

        return null;
    }

    private static (byte[] Data, string MimeType)? ExtractArtFromTag(TagLib.File tagFile)
    {
        var picture = tagFile.Tag.Pictures.FirstOrDefault();
        if (picture is null || picture.Data.Data is null || picture.Data.Data.Length == 0)
            return null;

        return (picture.Data.Data, picture.MimeType ?? "image/jpeg");
    }

    /// <summary>
    /// Tries to get the track number from the tag, falling back to raw ID3v2 TRCK frame
    /// when <see cref="TagLib.Tag.Track"/> returns 0 (which happens for many MP3 files).
    /// </summary>
    private int? TryGetTrackNumber(TagLib.Tag tag, TagLib.File tagFile)
    {
        if (tag.Track > 0)
            return (int)tag.Track;

        try
        {
            return TryParseRawTagFrame(tagFile, "TRCK");
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "TryParseRawTagFrame(TRCK) threw for {File}", Path.GetFileName(tagFile.Name));
            return null;
        }
    }

    /// <summary>
    /// Tries to get the disc number from the tag, falling back to raw ID3v2 TPOS frame.
    /// </summary>
    private int? TryGetDiscNumber(TagLib.Tag tag, TagLib.File tagFile)
    {
        if (tag.Disc > 0)
            return (int)tag.Disc;

        try
        {
            return TryParseRawTagFrame(tagFile, "TPOS");
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "TryParseRawTagFrame(TPOS) threw for {File}", Path.GetFileName(tagFile.Name));
            return null;
        }
    }

    /// <summary>
    /// Parses a numeric prefix from raw ID3/APE/Xiph tag frames (e.g., "1/9" → 1, "01" → 1).
    /// Tries ID3v1 first (most reliable for track numbers), then ID3v2, APE, and Xiph.
    /// </summary>
    private static int? TryParseRawTagFrame(TagLib.File tagFile, string frameId)
    {
        try
        {
            // ── 1. ID3v1 (most reliable for Track/Disc numbers) ──
            if (tagFile.Tag.TagTypes.HasFlag(TagLib.TagTypes.Id3v1))
            {
                var id3v1 = (TagLib.Id3v1.Tag)tagFile.GetTag(TagLib.TagTypes.Id3v1);
                var val = frameId == "TRCK" ? id3v1.Track : 0U;
                if (val > 0)
                    return (int)val;
            }

            // ── 2. ID3v2 TextInformationFrame ──
            if (tagFile.Tag.TagTypes.HasFlag(TagLib.TagTypes.Id3v2))
            {
                var id3v2 = (TagLib.Id3v2.Tag)tagFile.GetTag(TagLib.TagTypes.Id3v2);
                var frame = id3v2.GetFrames<TagLib.Id3v2.TextInformationFrame>()
                    .FirstOrDefault(f => f.FrameId == frameId);
                if (frame?.Text?.Length > 0)
                {
                    var text = frame.Text[0];
                    var slashIdx = text.IndexOf('/');
                    var numPart = slashIdx > 0 ? text[..slashIdx] : text;
                    if (int.TryParse(numPart, out var num) && num > 0)
                        return num;
                }
            }

            // ── 3. APE tags ──
            if (tagFile.Tag.TagTypes.HasFlag(TagLib.TagTypes.Ape))
            {
                var ape = (TagLib.Ape.Tag)tagFile.GetTag(TagLib.TagTypes.Ape);
                var item = ape.GetItem(frameId == "TRCK" ? "Track" : "Disc");
                if (item is not null && !item.IsEmpty)
                {
                    var text = item.ToString();
                    var slashIdx = text.IndexOf('/');
                    var numPart = slashIdx > 0 ? text[..slashIdx] : text;
                    if (int.TryParse(numPart, out var num) && num > 0)
                        return num;
                }
            }

            // ── 4. Xiph comments (FLAC, Vorbis) ──
            if (tagFile.Tag.TagTypes.HasFlag(TagLib.TagTypes.Xiph))
            {
                var xiph = (TagLib.Ogg.XiphComment)tagFile.GetTag(TagLib.TagTypes.Xiph);
                var fieldName = frameId == "TRCK" ? "TRACKNUMBER" : "DISCNUMBER";
                var fields = xiph.GetField(fieldName);
                if (fields is { Length: > 0 })
                {
                    var text = fields[0];
                    var slashIdx = text.IndexOf('/');
                    var numPart = slashIdx > 0 ? text[..slashIdx] : text;
                    if (int.TryParse(numPart, out var num) && num > 0)
                        return num;
                }
            }
        }
        catch
        {
            // Ignore — fall through to filename extraction
        }

        return null;
    }

    /// <summary>
    /// Extracts a numeric track number from the beginning of a filename.
    /// Handles patterns like "01 Title.mp3", "01 - Title.mp3", "1. Title.mp3".
    /// Returns null if no leading digits are found.
    /// </summary>
    private static int? TryExtractTrackNumberFromFileName(string fileNameOrPath)
    {
        var name = Path.GetFileNameWithoutExtension(fileNameOrPath);
        if (string.IsNullOrEmpty(name))
            return null;

        int i = 0;
        while (i < name.Length && !char.IsDigit(name[i]))
            i++;

        if (i >= name.Length)
            return null;

        int start = i;
        while (i < name.Length && char.IsDigit(name[i]))
            i++;

        var span = name.AsSpan(start, i - start);
        return int.TryParse(span, out var num) && num > 0 ? num : null;
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

    /// <summary>MusicBrainz Track ID (recording MBID).</summary>
    public string? MusicBrainzTrackId { get; init; }

    /// <summary>MusicBrainz Artist ID.</summary>
    public string? MusicBrainzArtistId { get; init; }

    /// <summary>MusicBrainz Album ID (release MBID).</summary>
    public string? MusicBrainzAlbumId { get; init; }

    /// <summary>MusicBrainz Release Group ID.</summary>
    public string? MusicBrainzReleaseGroupId { get; init; }

    /// <summary>MusicBrainz Release Artist ID.</summary>
    public string? MusicBrainzReleaseArtistId { get; init; }

    /// <summary>MusicBrainz Disc ID.</summary>
    public string? MusicBrainzDiscId { get; init; }

    /// <summary>International Standard Recording Code.</summary>
    public string? Isrc { get; init; }

    /// <summary>Beats per minute.</summary>
    public int? Bpm { get; init; }

    /// <summary>Composer(s), semicolon-separated for multiple.</summary>
    public string? Composers { get; init; }
}
