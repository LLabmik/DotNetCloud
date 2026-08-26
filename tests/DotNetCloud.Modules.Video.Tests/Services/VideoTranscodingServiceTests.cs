using DotNetCloud.Modules.Video.Host.Services;
using DotNetCloud.Modules.Video.Services;

namespace DotNetCloud.Modules.Video.Tests.Services;

/// <summary>
/// Tests for <see cref="VideoTranscodingService.ParseCodecInfo"/> (internal, tested via
/// the Host assembly's InternalsVisibleTo) covering audio stream enumeration used by the
/// audio-track selector.
/// </summary>
[TestClass]
public sealed class VideoTranscodingServiceTests
{
    private const string TwoAudioStreamsJson = """
    {
      "streams": [
        { "index": 0, "codec_type": "video", "codec_name": "h264" },
        { "index": 1, "codec_type": "audio", "codec_name": "aac", "channels": 2,
          "tags": { "language": "eng", "title": "Stereo" },
          "disposition": { "default": 1 } },
        { "index": 2, "codec_type": "audio", "codec_name": "ac3", "channels": 6,
          "tags": { "language": "jpn" },
          "disposition": { "default": 0 } },
        { "index": 3, "codec_type": "subtitle", "codec_name": "subrip" }
      ],
      "format": { "format_name": "matroska,webm" }
    }
    """;

    [TestMethod]
    public void ParseCodecInfo_MultipleAudioStreams_ReturnsAll()
    {
        var (videoCodec, audioCodec, container, audioStreams) =
            VideoTranscodingService.ParseCodecInfo(TwoAudioStreamsJson);

        Assert.AreEqual("h264", videoCodec);
        Assert.AreEqual("aac", audioCodec);
        Assert.AreEqual("matroska", container);
        Assert.AreEqual(2, audioStreams.Count);

        var first = audioStreams[0];
        Assert.AreEqual(0, first.Index);
        Assert.AreEqual("aac", first.Codec);
        Assert.AreEqual("eng", first.Language);
        Assert.AreEqual("Stereo", first.Title);
        Assert.AreEqual(2, first.Channels);
        Assert.IsTrue(first.IsDefault);

        var second = audioStreams[1];
        Assert.AreEqual(1, second.Index);
        Assert.AreEqual("ac3", second.Codec);
        Assert.AreEqual("jpn", second.Language);
        Assert.IsNull(second.Title);
        Assert.AreEqual(6, second.Channels);
        Assert.IsFalse(second.IsDefault);
    }

    [TestMethod]
    public void ParseCodecInfo_Mp4FormatName_PrefersMp4()
    {
        const string json = """
        {
          "streams": [
            { "index": 0, "codec_type": "video", "codec_name": "h264" },
            { "index": 1, "codec_type": "audio", "codec_name": "aac", "channels": 2,
              "tags": { "language": "eng" }, "disposition": { "default": 1 } }
          ],
          "format": { "format_name": "mov,mp4,m4a,3gp,3g2,mj2" }
        }
        """;

        var (_, _, container, audioStreams) = VideoTranscodingService.ParseCodecInfo(json);

        Assert.AreEqual("mp4", container);
        Assert.AreEqual(1, audioStreams.Count);
        Assert.AreEqual("aac", audioStreams[0].Codec);
        Assert.IsTrue(audioStreams[0].IsDefault);
    }

    [TestMethod]
    public void ParseCodecInfo_NoAudioStreams_ReturnsEmptyList()
    {
        const string json = """
        {
          "streams": [
            { "index": 0, "codec_type": "video", "codec_name": "h264" }
          ],
          "format": { "format_name": "mp4" }
        }
        """;

        var (videoCodec, audioCodec, _, audioStreams) = VideoTranscodingService.ParseCodecInfo(json);

        Assert.AreEqual("h264", videoCodec);
        Assert.IsNull(audioCodec);
        Assert.AreEqual(0, audioStreams.Count);
    }

    [TestMethod]
    public void ParseCodecInfo_StreamsMissing_ReturnsNullsAndEmpty()
    {
        const string json = """{ "format": { "format_name": "mp4" } }""";

        var (videoCodec, audioCodec, container, audioStreams) =
            VideoTranscodingService.ParseCodecInfo(json);

        Assert.IsNull(videoCodec);
        Assert.IsNull(audioCodec);
        Assert.AreEqual("mp4", container);
        Assert.AreEqual(0, audioStreams.Count);
    }
}
