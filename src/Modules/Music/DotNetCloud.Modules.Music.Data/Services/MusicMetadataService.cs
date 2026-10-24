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

            // Some encoders leave junk between the ID3v2 tag and the first MPEG frame. TagLib
            // parses the tags fine but cannot locate the frame stream, so it reports a zero
            // duration and a zero bitrate. Fall back to walking the MPEG frames ourselves.
            var mpegInfo = tagFile.Properties.Duration.Ticks <= 0 ? TryReadMpegAudioInfo(filePath) : null;
            return BuildMetadata(tagFile, filePath, mpegInfo);
        }
        catch (Exception ex)
        {
            // A malformed embedded-art frame (APIC payload < 5 bytes, seen in real libraries)
            // makes TagLib throw while parsing the ID3v2 tag, which discarded the whole file and
            // left DurationTicks/Bitrate/SampleRate at 0. Retry against the audio payload only
            // (ID3v2 tag skipped) so the audio properties are still read; tags then fall back to
            // the file name / directory convention exactly as before.
            var audioOnly = TryExtractFromAudioOnlyStream(filePath);
            if (audioOnly is not null && audioOnly.DurationTicks > 0)
            {
                return audioOnly;
            }

            // Last resort: derive the audio properties from the MPEG frame chain so the track
            // still gets a real duration instead of being stored with DurationTicks = 0.
            var mpegInfo = TryReadMpegAudioInfo(filePath);
            if (mpegInfo is not null)
            {
                _logger.LogDebug(
                    "Recovered audio properties for {FilePath} by walking MPEG frames ({Duration})",
                    filePath, TimeSpan.FromTicks(mpegInfo.DurationTicks));
                return BuildFrameOnlyMetadata(filePath, mpegInfo);
            }

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
                MpegAudioInfo? mpegInfo = null;
                if (tagFile.Properties.Duration.Ticks <= 0 && audioStream.CanSeek)
                {
                    var position = audioStream.Position;
                    mpegInfo = TryReadMpegAudioInfo(audioStream);
                    audioStream.Position = position;
                }

                return BuildMetadata(tagFile, fileName, mpegInfo);
            }
        }
        catch (Exception ex)
        {
            // See ExtractMetadata(string): retry with the ID3v2 tag skipped so a corrupt tag
            // cannot discard the audio properties.
            var audioOnly = audioStream.CanSeek
                ? TryExtractFromAudioOnlyStream(audioStream, mimeType, fileName)
                : null;
            if (audioOnly is not null && audioOnly.DurationTicks > 0)
            {
                return audioOnly;
            }

            // Last resort: walk the MPEG frames so the track still gets a real duration.
            if (audioStream.CanSeek)
            {
                var position = audioStream.Position;
                var mpegInfo = TryReadMpegAudioInfo(audioStream);
                audioStream.Position = position;
                if (mpegInfo is not null)
                {
                    _logger.LogDebug(
                        "Recovered audio properties for {FileName} by walking MPEG frames ({Duration})",
                        fileName, TimeSpan.FromTicks(mpegInfo.DurationTicks));
                    return BuildFrameOnlyMetadata(fileName, mpegInfo);
                }
            }

            _logger.LogWarning(ex, "Failed to extract metadata from stream for {FileName}", fileName);
            return null;
        }
    }

    /// <summary>
    /// Reads metadata from the audio payload only, skipping an ID3v2 tag that TagLib cannot parse.
    /// </summary>
    private AudioMetadata? TryExtractFromAudioOnlyStream(string filePath)
    {
        try
        {
            using var stream = File.Open(filePath, FileMode.Open, FileAccess.Read, FileShare.Read);
            return TryExtractFromAudioOnlyStream(stream, mimeType: null, Path.GetFileName(filePath));
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Audio-only metadata fallback failed for {FilePath}", filePath);
            return null;
        }
    }

    /// <summary>
    /// Reads metadata from <paramref name="stream"/> positioned past the ID3v2 tag.
    /// Returns null when there is no ID3v2 tag to skip (so nothing is gained) or when the
    /// audio payload is still unreadable.
    /// </summary>
    private AudioMetadata? TryExtractFromAudioOnlyStream(Stream stream, string? mimeType, string fileName)
    {
        if (!stream.CanSeek)
        {
            return null;
        }

        var tagLength = ReadId3v2Length(stream);
        if (tagLength <= 0)
        {
            return null;
        }

        try
        {
            var abstraction = new StreamFileAbstraction(fileName, new TagOffsetStream(stream, tagLength));
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
                var metadata = BuildMetadata(tagFile, fileName);
                if (metadata.DurationTicks <= 0)
                {
                    return null;
                }

                _logger.LogDebug(
                    "Read metadata for {FileName} from the audio payload only (skipped {TagLength}-byte ID3v2 tag)",
                    fileName, tagLength);
                return metadata;
            }
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Audio-only metadata fallback failed for {FileName}", fileName);
            return null;
        }
    }

    /// <summary>Returns the total length (header + payload) of a leading ID3v2 tag, or 0 when absent.</summary>
    private static int ReadId3v2Length(Stream stream)
    {
        var originalPosition = stream.Position;
        try
        {
            Span<byte> header = stackalloc byte[10];
            stream.Position = 0;
            if (stream.Read(header) < header.Length)
            {
                return 0;
            }

            if (header[0] != (byte)'I' || header[1] != (byte)'D' || header[2] != (byte)'3')
            {
                return 0;
            }

            // Size is a 28-bit synchsafe integer in bytes 6-9 (v2.2/v2.3/v2.4 alike);
            // v2.4 may append a 10-byte footer when the footer flag (0x10) is set.
            var size = (header[6] & 0x7F) << 21
                     | (header[7] & 0x7F) << 14
                     | (header[8] & 0x7F) << 7
                     | (header[9] & 0x7F);
            var footer = (header[5] & 0x10) != 0 ? 10 : 0;
            return 10 + size + footer;
        }
        finally
        {
            stream.Position = originalPosition;
        }
    }

    /// <summary>
    /// Audio properties measured directly from an MPEG audio frame chain.
    /// </summary>
    /// <param name="DurationTicks">Duration in ticks derived from the frame count.</param>
    /// <param name="Bitrate">Bitrate of the first frame, in bits per second.</param>
    /// <param name="SampleRate">Sample rate of the first frame, in Hz.</param>
    /// <param name="Channels">Channel count of the first frame.</param>
    internal sealed record MpegAudioInfo(long DurationTicks, int Bitrate, int SampleRate, int Channels);

    // MPEG audio bitrate tables in kbps, keyed by (version bits, layer bits).
    // Version bits: 0 = MPEG-2.5, 2 = MPEG-2, 3 = MPEG-1. Layer bits: 1 = III, 2 = II, 3 = I.
    private static readonly int[][] MpegBitrates =
    [
        [0, 32, 40, 48, 56, 64, 80, 96, 112, 128, 160, 192, 224, 256, 320],   // MPEG-1 Layer III
        [0, 32, 48, 56, 64, 80, 96, 112, 128, 160, 192, 224, 256, 320, 384],  // MPEG-1 Layer II
        [0, 32, 64, 96, 128, 160, 192, 224, 256, 288, 320, 352, 384, 416, 448], // MPEG-1 Layer I
        [0, 8, 16, 24, 32, 40, 48, 56, 64, 80, 96, 112, 128, 144, 160],       // MPEG-2/2.5 Layer III
        [0, 8, 16, 24, 32, 40, 48, 56, 64, 80, 96, 112, 128, 144, 160],       // MPEG-2/2.5 Layer II
        [0, 32, 48, 56, 64, 80, 96, 112, 128, 144, 160, 176, 192, 224, 256]   // MPEG-2/2.5 Layer I
    ];

    private static readonly int[][] MpegSampleRates =
    [
        [44100, 48000, 32000], // MPEG-1
        [22050, 24000, 16000], // MPEG-2
        [11025, 12000, 8000]   // MPEG-2.5
    ];

    /// <summary>Upper bound on how much of a file is read when walking MPEG frames (256 MB).</summary>
    private const long MaxMpegScanBytes = 256L * 1024 * 1024;

    /// <summary>How many consecutive valid frames are required before a sync is trusted.</summary>
    private const int MpegSyncFramesRequired = 3;

    /// <summary>
    /// Measures duration/bitrate/sample-rate/channels by walking the MPEG frame chain.
    /// </summary>
    /// <remarks>
    /// Some encoders leave junk (padding, a stale tag) between the ID3v2 tag and the first frame.
    /// TagLib then parses the tags but cannot find the frame stream, reporting a zero duration and
    /// a zero bitrate. This scanner locates the first frame chain (validated by consecutive frame
    /// headers) and counts frames, which reproduces the duration reported by ffprobe.
    /// </remarks>
    /// <returns>Measured audio properties, or null when no MPEG frame chain is present.</returns>
    private MpegAudioInfo? TryReadMpegAudioInfo(string filePath)
    {
        try
        {
            using var stream = File.Open(filePath, FileMode.Open, FileAccess.Read, FileShare.Read);
            return TryReadMpegAudioInfo(stream);
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "MPEG frame scan failed for {FilePath}", filePath);
            return null;
        }
    }

    /// <summary>
    /// Measures duration/bitrate/sample-rate/channels by walking the MPEG frame chain in a stream.
    /// </summary>
    /// <returns>Measured audio properties, or null when no MPEG frame chain is present.</returns>
    internal MpegAudioInfo? TryReadMpegAudioInfo(Stream stream)
    {
        if (!stream.CanRead || !stream.CanSeek)
        {
            return null;
        }

        var originalPosition = stream.Position;
        try
        {
            stream.Position = 0;
            var length = (int)Math.Min(stream.Length, MaxMpegScanBytes);
            if (length < 4)
            {
                return null;
            }

            var buffer = new byte[length];
            stream.ReadExactly(buffer, 0, length);

            if (!TryFindFirstMpegFrame(buffer, out var offset, out var header))
            {
                return null;
            }

            var frames = 0;
            var position = offset;
            var firstHeader = header;
            while (true)
            {
                if (!TryParseMpegHeader(buffer, position, out var current))
                {
                    // Tolerate a few damaged frames (padding/id3v1 tail) by resyncing.
                    var resync = position + 1;
                    if (resync >= buffer.Length || !TryFindFirstMpegFrame(buffer, out var next, out var nextHeader, resync))
                    {
                        break;
                    }

                    position = next;
                    continue;
                }

                frames++;
                position += current.FrameLength;
            }

            if (frames == 0)
            {
                return null;
            }

            var durationTicks = frames * (long)firstHeader.SamplesPerFrame * TimeSpan.TicksPerSecond / firstHeader.SampleRate;
            if (durationTicks <= 0)
            {
                return null;
            }

            return new MpegAudioInfo(
                durationTicks,
                firstHeader.Bitrate * 1000,
                firstHeader.SampleRate,
                firstHeader.Channels);
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "MPEG frame scan failed");
            return null;
        }
        finally
        {
            stream.Position = originalPosition;
        }
    }

    /// <summary>Locates the first trusted MPEG frame chain at or after <paramref name="start"/>.</summary>
    private static bool TryFindFirstMpegFrame(byte[] buffer, out int offset, out MpegFrameHeader header, int start = 0)
    {
        offset = -1;
        header = default;

        for (var i = start; i < buffer.Length - 4; i++)
        {
            if (buffer[i] != 0xFF || (buffer[i + 1] & 0xE0) != 0xE0)
            {
                continue;
            }

            var candidate = i;
            var consecutive = 0;
            while (consecutive < MpegSyncFramesRequired && TryParseMpegHeader(buffer, candidate, out var parsed))
            {
                candidate += parsed.FrameLength;
                consecutive++;
            }

            if (consecutive == MpegSyncFramesRequired && TryParseMpegHeader(buffer, i, out var first))
            {
                offset = i;
                header = first;
                return true;
            }
        }

        return false;
    }

    /// <summary>Parses a four-byte MPEG audio frame header at <paramref name="position"/>.</summary>
    private static bool TryParseMpegHeader(byte[] buffer, int position, out MpegFrameHeader header)
    {
        header = default;
        if (position < 0 || position + 4 > buffer.Length)
        {
            return false;
        }

        if (buffer[position] != 0xFF || (buffer[position + 1] & 0xE0) != 0xE0)
        {
            return false;
        }

        var versionBits = (buffer[position + 1] >> 3) & 0x03;
        var layerBits = (buffer[position + 1] >> 1) & 0x03;
        if (versionBits == 1 || layerBits == 0)
        {
            return false;
        }

        var bitrateIndex = (buffer[position + 2] >> 4) & 0x0F;
        var sampleRateIndex = (buffer[position + 2] >> 2) & 0x03;
        if (bitrateIndex is 0 or 15 || sampleRateIndex == 3)
        {
            return false;
        }

        var tableIndex = versionBits switch
        {
            3 => layerBits switch { 1 => 0, 2 => 1, _ => 2 },
            _ => layerBits switch { 1 => 3, 2 => 4, _ => 5 }
        };
        var sampleRateTable = versionBits switch { 3 => 0, 2 => 1, _ => 2 };

        var bitrate = MpegBitrates[tableIndex][bitrateIndex];
        var sampleRate = MpegSampleRates[sampleRateTable][sampleRateIndex];
        if (bitrate == 0 || sampleRate == 0)
        {
            return false;
        }

        var padding = (buffer[position + 2] >> 1) & 0x01;
        int frameLength;
        int samplesPerFrame;
        switch (layerBits)
        {
            case 3: // Layer I
                frameLength = (12 * bitrate * 1000 / sampleRate) + padding;
                samplesPerFrame = 384;
                break;
            case 2: // Layer II
                frameLength = (144 * bitrate * 1000 / sampleRate) + padding;
                samplesPerFrame = 1152;
                break;
            default: // Layer III
                if (versionBits == 3)
                {
                    frameLength = (144 * bitrate * 1000 / sampleRate) + padding;
                    samplesPerFrame = 1152;
                }
                else
                {
                    frameLength = (72 * bitrate * 1000 / sampleRate) + padding;
                    samplesPerFrame = 576;
                }

                break;
        }

        if (frameLength <= 4)
        {
            return false;
        }

        var channelMode = (buffer[position + 3] >> 6) & 0x03;
        header = new MpegFrameHeader(frameLength, samplesPerFrame, bitrate, sampleRate, channelMode == 3 ? 1 : 2);
        return true;
    }

    /// <summary>Parsed four-byte MPEG audio frame header.</summary>
    private readonly record struct MpegFrameHeader(
        int FrameLength,
        int SamplesPerFrame,
        int Bitrate,
        int SampleRate,
        int Channels);

    /// <summary>
    /// Builds metadata for a file whose tags could not be read at all, using frame-derived audio properties.
    /// </summary>
    private static AudioMetadata BuildFrameOnlyMetadata(string fileNameOrPath, MpegAudioInfo mpegInfo) => new()
    {
        Title = Path.GetFileNameWithoutExtension(fileNameOrPath),
        Artist = "Unknown Artist",
        Album = "Unknown Album",
        DurationTicks = mpegInfo.DurationTicks,
        Bitrate = mpegInfo.Bitrate,
        SampleRate = mpegInfo.SampleRate,
        Channels = mpegInfo.Channels
    };

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

    private AudioMetadata BuildMetadata(TagLib.File tagFile, string fileNameOrPath, MpegAudioInfo? mpegInfo = null)
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

        // TagLib reports zero for these when it cannot locate the MPEG frame stream; prefer the
        // values measured from the frame chain so the track is not stored with DurationTicks = 0.
        var durationTicks = properties.Duration.Ticks > 0
            ? properties.Duration.Ticks
            : mpegInfo?.DurationTicks ?? 0;
        var bitrate = properties.AudioBitrate > 0
            ? properties.AudioBitrate * 1000L
            : (long?)mpegInfo?.Bitrate;
        var sampleRate = properties.AudioSampleRate > 0
            ? properties.AudioSampleRate
            : mpegInfo?.SampleRate;
        var channels = properties.AudioChannels > 0
            ? properties.AudioChannels
            : mpegInfo?.Channels;

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
            DurationTicks = durationTicks,
            Bitrate = bitrate,
            SampleRate = sampleRate,
            Channels = channels,
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

    /// <summary>
    /// Read-only view over an inner stream that starts at a byte offset,
    /// used to hand TagLib the audio payload without the (corrupt) ID3v2 tag in front of it.
    /// </summary>
    private sealed class TagOffsetStream : Stream
    {
        private readonly Stream _inner;
        private readonly long _startOffset;

        public TagOffsetStream(Stream inner, long startOffset)
        {
            _inner = inner;
            _startOffset = startOffset;
        }

        public override bool CanRead => true;

        public override bool CanSeek => _inner.CanSeek;

        public override bool CanWrite => false;

        public override long Length => Math.Max(0, _inner.Length - _startOffset);

        public override long Position
        {
            get => _inner.Position - _startOffset;
            set => _inner.Position = _startOffset + value;
        }

        public override int Read(byte[] buffer, int offset, int count) => _inner.Read(buffer, offset, count);

        public override int Read(Span<byte> buffer) => _inner.Read(buffer);

        public override long Seek(long offset, SeekOrigin origin)
        {
            Position = origin switch
            {
                SeekOrigin.Begin => offset,
                SeekOrigin.Current => Position + offset,
                SeekOrigin.End => Length + offset,
                _ => throw new ArgumentOutOfRangeException(nameof(origin))
            };
            return Position;
        }

        public override void Flush() { }

        public override void SetLength(long value) => throw new NotSupportedException();

        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
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
