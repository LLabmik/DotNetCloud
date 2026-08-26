using DotNetCloud.Modules.Video.Services;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace DotNetCloud.Modules.Video.Tests.Services;

/// <summary>
/// Tests for <see cref="FfmpegArgumentBuilder"/>.
/// Since FfmpegArgumentBuilder is a stateless, purely-functional class,
/// these tests don't require any mocking or DI setup.
/// </summary>
[TestClass]
public sealed class FfmpegArgumentBuilderTests
{
    private readonly FfmpegArgumentBuilder _builder = new();
    private readonly VideoTranscodingOptions _defaultOptions = new()
    {
        FfmpegPath = "ffmpeg",
        VideoCodec = "libx264",
        VideoCrf = 23,
        EncoderPreset = "veryfast",
        MaxWidth = 1920,
        MaxHeight = 1080,
        AudioCodec = "aac",
        AudioBitrateKbps = 128,
        ThreadCount = 0
    };

    // ─── CanDirectPlay tests ──────────────────────────────────────

    [TestMethod]
    public void CanDirectPlay_H264AacMp4_ReturnsTrue()
    {
        bool result = _builder.CanDirectPlay("video/mp4", "h264", "aac", "mp4");
        Assert.IsTrue(result);
    }

    [TestMethod]
    public void CanDirectPlay_Avc1AacMp4_ReturnsTrue()
    {
        bool result = _builder.CanDirectPlay("video/mp4", "avc1", "aac", "mp4");
        Assert.IsTrue(result);
    }

    [TestMethod]
    public void CanDirectPlay_HevcAacMp4_ReturnsFalse()
    {
        // HEVC video codec has partial browser support — not fully universal
        bool result = _builder.CanDirectPlay("video/mp4", "hevc", "aac", "mp4");
        Assert.IsFalse(result);
    }

    [TestMethod]
    public void CanDirectPlay_MkvContainer_ReturnsFalse()
    {
        bool result = _builder.CanDirectPlay("video/x-matroska", "h264", "aac", "mkv");
        Assert.IsFalse(result);
    }

    [TestMethod]
    public void CanDirectPlay_Vp9Codec_ReturnsFalse()
    {
        bool result = _builder.CanDirectPlay("video/mp4", "vp9", "aac", "mp4");
        Assert.IsFalse(result);
    }

    [TestMethod]
    public void CanDirectPlay_WrongMimeType_ReturnsFalse()
    {
        bool result = _builder.CanDirectPlay("video/webm", "h264", "aac", "webm");
        Assert.IsFalse(result);
    }

    [TestMethod]
    public void CanDirectPlay_H264NoAudioCodec_ReturnsTrue()
    {
        // If audio codec is null/empty, treat as direct-playable
        // (some files don't have audio, e.g. silent test clips)
        bool result = _builder.CanDirectPlay("video/mp4", "h264", null, "mp4");
        Assert.IsTrue(result);
    }

    [TestMethod]
    public void CanDirectPlay_H264Ac3Audio_ReturnsFalse()
    {
        // AC3 audio is not natively supported in HTML5 browsers
        bool result = _builder.CanDirectPlay("video/mp4", "h264", "ac3", "mp4");
        Assert.IsFalse(result);
    }

    [TestMethod]
    public void CanDirectPlay_H264Mp3Audio_ReturnsTrue()
    {
        bool result = _builder.CanDirectPlay("video/mp4", "h264", "mp3", "mp4");
        Assert.IsTrue(result);
    }

    // ─── BuildProgressiveMp4Args tests ────────────────────────────

    [TestMethod]
    public void BuildProgressiveMp4Args_ShouldContainAllCodecArgs()
    {
        var args = _builder.BuildProgressiveMp4Args(
            "/videos/input.mkv", "/output/test.mp4", _defaultOptions);

        Assert.IsTrue(args.Contains("-c:v libx264"));
        Assert.IsTrue(args.Contains("-preset veryfast"));
        Assert.IsTrue(args.Contains("-crf 23"));
        Assert.IsTrue(args.Contains("-c:a aac"));
        Assert.IsTrue(args.Contains("-b:a 128k"));
        Assert.IsTrue(args.Contains("-f mp4"));
        Assert.IsTrue(args.Contains("-movflags +faststart"));
        Assert.IsTrue(args.Contains("-pix_fmt yuv420p"));
    }

    [TestMethod]
    public void BuildProgressiveMp4Args_ShouldContainInputAndOutputPaths()
    {
        var args = _builder.BuildProgressiveMp4Args(
            "/videos/source.mkv", "/output/result.mp4", _defaultOptions);

        Assert.IsTrue(args.Contains("-i \"/videos/source.mkv\""));
        Assert.IsTrue(args.Contains("\"/output/result.mp4\""));
        Assert.IsTrue(args.Contains("-y"));
    }

    [TestMethod]
    public void BuildProgressiveMp4Args_ShouldContainScaleFilter()
    {
        var args = _builder.BuildProgressiveMp4Args(
            "/i.mkv", "/o.mp4", _defaultOptions);

        Assert.IsTrue(args.Contains("-vf"));
        Assert.IsTrue(args.Contains("scale"));
        Assert.IsTrue(args.Contains("1920"));
        Assert.IsTrue(args.Contains("1080"));
    }

    [TestMethod]
    public void BuildProgressiveMp4Args_ShouldContainSeekStart()
    {
        var args = _builder.BuildProgressiveMp4Args(
            "/i.mkv", "/o.mp4", _defaultOptions,
            seekStart: TimeSpan.FromSeconds(30));

        Assert.IsTrue(args.Contains("-ss 30.000"));
    }

    [TestMethod]
    public void BuildProgressiveMp4Args_ShouldContainDuration()
    {
        var args = _builder.BuildProgressiveMp4Args(
            "/i.mkv", "/o.mp4", _defaultOptions,
            seekDuration: TimeSpan.FromMinutes(5));

        Assert.IsTrue(args.Contains("-t 300.000"));
    }

    [TestMethod]
    public void BuildProgressiveMp4Args_ShouldContainBothSeekAndDuration()
    {
        var args = _builder.BuildProgressiveMp4Args(
            "/i.mkv", "/o.mp4", _defaultOptions,
            seekStart: TimeSpan.FromSeconds(60),
            seekDuration: TimeSpan.FromSeconds(120));

        Assert.IsTrue(args.Contains("-ss 60.000"));
        Assert.IsTrue(args.Contains("-t 120.000"));
    }

    [TestMethod]
    public void BuildProgressiveMp4Args_ShouldNotSeekWhenZero()
    {
        var args = _builder.BuildProgressiveMp4Args(
            "/i.mkv", "/o.mp4", _defaultOptions,
            seekStart: TimeSpan.Zero);

        // No -ss argument should appear
        Assert.IsFalse(args.Contains("-ss"));
    }

    [TestMethod]
    public void BuildProgressiveMp4Args_ShouldMapVideoAndAudio()
    {
        var args = _builder.BuildProgressiveMp4Args(
            "/i.mkv", "/o.mp4", _defaultOptions);

        Assert.IsTrue(args.Contains("-map 0:v:0? -map 0:a:0?"));
    }

    [TestMethod]
    public void BuildProgressiveMp4Args_WithCustomOptions_ReflectsChanges()
    {
        var customOpts = new VideoTranscodingOptions
        {
            VideoCodec = "libx265",
            VideoCrf = 28,
            EncoderPreset = "medium",
            AudioCodec = "libmp3lame",
            AudioBitrateKbps = 192,
            MaxWidth = 1280,
            MaxHeight = 720,
            ThreadCount = 4
        };

        var args = _builder.BuildProgressiveMp4Args(
            "/i.mkv", "/o.mp4", customOpts);

        Assert.IsTrue(args.Contains("-c:v libx265"));
        Assert.IsTrue(args.Contains("-preset medium"));
        Assert.IsTrue(args.Contains("-crf 28"));
        Assert.IsTrue(args.Contains("-c:a libmp3lame"));
        Assert.IsTrue(args.Contains("-b:a 192k"));
        Assert.IsTrue(args.Contains("-threads 4"));
    }

    [TestMethod]
    public void BuildProgressiveMp4Args_ThreadCountZero_OmitsThreadArg()
    {
        var args = _builder.BuildProgressiveMp4Args(
            "/i.mkv", "/o.mp4", _defaultOptions);

        // ThreadCount defaults to 0, so no -threads arg
        Assert.IsFalse(args.Contains("-threads"));
    }

    // ─── BuildFfprobeArgs tests ───────────────────────────────────

    [TestMethod]
    public void BuildFfprobeArgs_ShouldContainShowStreams()
    {
        var args = _builder.BuildFfprobeArgs("/videos/test.mkv");

        Assert.IsTrue(args.Contains("-show_streams"));
        Assert.IsTrue(args.Contains("-show_format"));
        Assert.IsTrue(args.Contains("-print_format json"));
        Assert.IsTrue(args.Contains("/videos/test.mkv"));
    }

    [TestMethod]
    public void BuildFfprobeArgs_ShouldBeQuiet()
    {
        var args = _builder.BuildFfprobeArgs("/test.mkv");

        Assert.IsTrue(args.Contains("-v quiet"));
    }

    // ─── BuildHlsArgs seek A/V sync tests ─────────────────────────

    [TestMethod]
    public void BuildHlsArgs_NoSeek_ContainsSingleInputSeekOnly()
    {
        var args = _builder.BuildHlsArgs("/i.mkv", "/out", _defaultOptions);

        Assert.IsFalse(args.Contains("-ss"));
        Assert.IsTrue(args.Contains("-i \"/i.mkv\""));
    }

    [TestMethod]
    public void BuildHlsArgs_SmallSeek_UsesAccurateSeekOnlyNoFastSeek()
    {
        // Seek target within the 10s accurate-seek window — no fast pre-input
        // seek needed, the whole seek happens as an accurate post-input seek
        // so audio and video both land on the exact requested timestamp.
        var args = _builder.BuildHlsArgs(
            "/i.mkv", "/out", _defaultOptions,
            seekStart: TimeSpan.FromSeconds(5));

        var iIndex = args.IndexOf("-i \"/i.mkv\"", StringComparison.Ordinal);
        Assert.IsTrue(iIndex >= 0);
        // No -ss should appear before -i
        Assert.IsFalse(args[..iIndex].Contains("-ss"));
        // The accurate seek (5s) should appear after -i
        Assert.IsTrue(args[iIndex..].Contains("-ss 5.000"));
    }

    [TestMethod]
    public void BuildHlsArgs_LargeSeek_SplitsIntoFastAndAccurateSeek()
    {
        // Seeking to 90s should fast-seek to 80s (before -i) then accurately
        // seek the remaining 10s (after -i), so both audio and video streams
        // are decoded up to the exact same target instant and stay in sync.
        var args = _builder.BuildHlsArgs(
            "/i.mkv", "/out", _defaultOptions,
            seekStart: TimeSpan.FromSeconds(90));

        var iIndex = args.IndexOf("-i \"/i.mkv\"", StringComparison.Ordinal);
        Assert.IsTrue(iIndex >= 0);
        Assert.IsTrue(args[..iIndex].Contains("-ss 80.000"), $"Expected fast seek before -i. Args: {args}");
        Assert.IsTrue(args[iIndex..].Contains("-ss 10.000"), $"Expected accurate seek after -i. Args: {args}");
    }

    [TestMethod]
    public void BuildHlsArgs_ZeroSeek_OmitsSeekArgs()
    {
        var args = _builder.BuildHlsArgs(
            "/i.mkv", "/out", _defaultOptions,
            seekStart: TimeSpan.Zero);

        Assert.IsFalse(args.Contains("-ss"));
    }

    [TestMethod]
    public void BuildHlsArgs_AlwaysForcesConstantFrameRate()
    {
        // VFR sources (common in HEVC rips) drift out of sync with audio over
        // time unless output frame timestamps are forced to be evenly spaced.
        var args = _builder.BuildHlsArgs("/i.mkv", "/out", _defaultOptions);

        Assert.IsTrue(args.Contains("-fps_mode:v:0 cfr"));
    }

    [TestMethod]
    public void BuildHlsArgs_AudioTranscoded_AppliesResampleDriftCorrection()
    {
        // Audio that must be re-encoded (e.g. DTS -> AAC, not in the copyable
        // codec set) gets an aresample filter to correct any drift introduced
        // by resampling/downmixing.
        var args = _builder.BuildHlsArgs(
            "/i.mkv", "/out", _defaultOptions,
            sourceVideoCodec: "hevc", sourceAudioCodec: "dts");

        Assert.IsTrue(args.Contains("-af aresample=async=1"));
    }

    [TestMethod]
    public void BuildHlsArgs_AudioCopied_OmitsResampleFilter()
    {
        // -af requires re-encoding; it must not be applied when audio is
        // stream-copied (e.g. FLAC), which would break ffmpeg's argument mapping.
        var args = _builder.BuildHlsArgs(
            "/i.mkv", "/out", _defaultOptions,
            sourceVideoCodec: "hevc", sourceAudioCodec: "flac");

        Assert.IsFalse(args.Contains("-af "));
        Assert.IsTrue(args.Contains("-c:a:0 copy"));
    }

    // ─── Path escaping tests ──────────────────────────────────────

    [TestMethod]
    public void BuildProgressiveMp4Args_EscapesBackslashes()
    {
        var args = _builder.BuildProgressiveMp4Args(
            "C:\\Users\\test\\video.mkv",
            "C:\\output\\result.mp4",
            _defaultOptions);

        // Backslashes should be converted to forward slashes for ffmpeg
        Assert.IsTrue(args.Contains("-i \"C:/Users/test/video.mkv\""));
        Assert.IsTrue(args.Contains("\"C:/output/result.mp4\""));
    }

    [TestMethod]
    public void BuildProgressiveMp4Args_EscapesQuotesInPath()
    {
        var args = _builder.BuildProgressiveMp4Args(
            "/home/user/my video.mkv",
            "/output/result.mp4",
            _defaultOptions);

        // Paths with spaces should be quoted properly
        Assert.IsTrue(args.Contains("-i \"/home/user/my video.mkv\""));
    }

    // ─── Edge cases ───────────────────────────────────────────────

    [TestMethod]
    public void BuildProgressiveMp4Args_MinimalOptions_DoesNotThrow()
    {
        var minimal = new VideoTranscodingOptions
        {
            FfmpegPath = "ffmpeg",
            VideoCodec = "libx264",
            AudioCodec = "aac"
        };

        var args = _builder.BuildProgressiveMp4Args("/i.mkv", "/o.mp4", minimal);

        Assert.IsNotNull(args);
        Assert.IsTrue(args.Length > 0);
        Assert.IsTrue(args.Contains("-c:v libx264"));
    }

    [TestMethod]
    public void BuildProgressiveMp4Args_OverwriteFlag_AlwaysPresent()
    {
        var args = _builder.BuildProgressiveMp4Args(
            "/i.mkv", "/o.mp4", _defaultOptions);

        Assert.IsTrue(args.Contains("-y"));
    }

    [TestMethod]
    public void BuildProgressiveMp4Args_AudioChannels_AlwaysStereo()
    {
        var args = _builder.BuildProgressiveMp4Args(
            "/i.mkv", "/o.mp4", _defaultOptions);

        Assert.IsTrue(args.Contains("-ac 2"));
    }

    [TestMethod]
    public void BuildProgressiveMp4Args_StripMetadata()
    {
        var args = _builder.BuildProgressiveMp4Args(
            "/i.mkv", "/o.mp4", _defaultOptions);

        Assert.IsTrue(args.Contains("-map_metadata -1"));
    }

    // ─── Audio stream selection tests ──────────────────────────────

    [TestMethod]
    public void GetStreamCopyArgs_DefaultAudioStream_MapsFirstAudio()
    {
        var args = _builder.GetStreamCopyArgs("/i.mkv", "h264", "aac");

        Assert.IsTrue(args.Contains("-map 0:a:0?"));
        Assert.IsFalse(args.Contains("-map 0:a:1?"));
    }

    [TestMethod]
    public void GetStreamCopyArgs_AudioStreamIndex_MapsSelectedAudio()
    {
        var args = _builder.GetStreamCopyArgs("/i.mkv", "h264", "aac", audioStreamIndex: 1);

        Assert.IsTrue(args.Contains("-map 0:a:1?"));
        Assert.IsFalse(args.Contains("-map 0:a:0?"));
    }

    [TestMethod]
    public void BuildHlsArgs_AudioStreamIndex_MapsSelectedAudio()
    {
        var args = _builder.BuildHlsArgs(
            "/i.mkv", "/out", _defaultOptions,
            sourceVideoCodec: "hevc", sourceAudioCodec: "aac",
            audioStreamIndex: 1);

        Assert.IsTrue(args.Contains("-map 0:a:1?"));
        Assert.IsFalse(args.Contains("-map 0:a:0?"));
    }

    [TestMethod]
    public void BuildHlsArgs_DefaultAudioStream_MapsFirstAudio()
    {
        var args = _builder.BuildHlsArgs("/i.mkv", "/out", _defaultOptions);

        Assert.IsTrue(args.Contains("-map 0:a:0?"));
        Assert.IsFalse(args.Contains("-map 0:a:1?"));
    }

    [TestMethod]
    public void BuildProgressiveMp4Args_AudioStreamIndex_MapsSelectedAudio()
    {
        var args = _builder.BuildProgressiveMp4Args(
            "/i.mkv", "/o.mp4", _defaultOptions, audioStreamIndex: 2);

        Assert.IsTrue(args.Contains("-map 0:a:2?"));
        Assert.IsFalse(args.Contains("-map 0:a:0?"));
    }
}
