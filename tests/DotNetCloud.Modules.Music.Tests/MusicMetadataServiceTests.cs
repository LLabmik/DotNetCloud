using System.Text;
using DotNetCloud.Modules.Music.Data.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace DotNetCloud.Modules.Music.Tests;

/// <summary>
/// Covers the corrupt-ID3v2 fallback: an embedded-art (APIC) frame with a payload shorter than
/// 5 bytes makes TagLib reject the whole file. Metadata extraction must still return the audio
/// properties (duration/bitrate/sample rate) by re-reading the payload with the tag skipped.
/// </summary>
[TestClass]
public sealed class MusicMetadataServiceTests
{
    private static string WriteTempFile(byte[] content)
    {
        var path = Path.Combine(Path.GetTempPath(), $"dnc-music-{Guid.NewGuid():N}.mp3");
        File.WriteAllBytes(path, content);
        return path;
    }

    [TestMethod]
    public void ExtractMetadata_CorruptPictureFrame_StillReadsDurationFromPath()
    {
        var path = WriteTempFile(BuildMp3WithCorruptPictureFrame());
        try
        {
            var service = new MusicMetadataService(NullLogger<MusicMetadataService>.Instance);

            var metadata = service.ExtractMetadata(path);

            Assert.IsNotNull(metadata, "The audio-only fallback should still return metadata.");
            Assert.IsTrue(metadata.DurationTicks > 0, "Duration must be recovered from the audio payload.");
            Assert.IsTrue(metadata.Bitrate is > 0, "Bitrate must be recovered from the audio payload.");
            Assert.AreEqual(Path.GetFileNameWithoutExtension(path), metadata.Title,
                "With the tag skipped, the title falls back to the file name.");
        }
        finally
        {
            File.Delete(path);
        }
    }

    [TestMethod]
    public void ExtractMetadata_CorruptPictureFrame_StillReadsDurationFromStream()
    {
        var bytes = BuildMp3WithCorruptPictureFrame();
        using var stream = new MemoryStream(bytes);
        var service = new MusicMetadataService(NullLogger<MusicMetadataService>.Instance);

        var metadata = service.ExtractMetadata(stream, "audio/mpeg", "track.mp3");

        Assert.IsNotNull(metadata);
        Assert.IsTrue(metadata.DurationTicks > 0, "Duration must be recovered from the audio payload.");
        Assert.AreEqual("track", metadata.Title);
    }

    /// <summary>
    /// Some encoders leave junk between the ID3v2 tag and the first MPEG frame. TagLib parses the
    /// tags but cannot find the frame stream, so it reports a zero duration/bitrate. The MPEG frame
    /// chain must then be walked directly to recover the duration.
    /// </summary>
    [TestMethod]
    public void TryReadMpegAudioInfo_JunkBeforeAudioFrames_MeasuresFrameChain()
    {
        var bytes = BuildMp3WithJunkBeforeFrames();
        using var stream = new MemoryStream(bytes);
        var service = new MusicMetadataService(NullLogger<MusicMetadataService>.Instance);

        var info = service.TryReadMpegAudioInfo(stream);

        Assert.IsNotNull(info, "The frame chain must be located behind the junk.");
        // 50 frames x 1152 samples at 44.1 kHz
        var expected = 50L * 1152 * TimeSpan.TicksPerSecond / 44100;
        Assert.AreEqual(expected, info.DurationTicks, "Duration must come from the frame count.");
        Assert.AreEqual(128000, info.Bitrate, "Bitrate must come from the first frame header.");
        Assert.AreEqual(44100, info.SampleRate, "Sample rate must come from the first frame header.");
        Assert.AreEqual(2, info.Channels, "Channel count must come from the first frame header.");
    }

    [TestMethod]
    public void ExtractMetadata_JunkBeforeAudioFrames_StillReturnsDuration()
    {
        var path = WriteTempFile(BuildMp3WithJunkBeforeFrames());
        try
        {
            var service = new MusicMetadataService(NullLogger<MusicMetadataService>.Instance);

            var metadata = service.ExtractMetadata(path);

            Assert.IsNotNull(metadata, "Metadata must be returned even when TagLib reports no duration.");
            Assert.IsTrue(metadata.DurationTicks > 0, "Duration must be recovered by walking the MPEG frames.");
        }
        finally
        {
            File.Delete(path);
        }
    }

    [TestMethod]
    public void ExtractMetadata_JunkBeforeAudioFrames_StillReturnsDurationFromStream()
    {
        var bytes = BuildMp3WithJunkBeforeFrames();
        using var stream = new MemoryStream(bytes);
        var service = new MusicMetadataService(NullLogger<MusicMetadataService>.Instance);

        var metadata = service.ExtractMetadata(stream, "audio/mpeg", "junk-track.mp3");

        Assert.IsNotNull(metadata);
        Assert.IsTrue(metadata.DurationTicks > 0, "Duration must be recovered by walking the MPEG frames.");
    }

    /// <summary>
    /// Builds an MP3 whose ID3v2.3 tag is followed by junk (as written by some encoders) before the
    /// first MPEG frame, so TagLib cannot locate the frame chain.
    /// </summary>
    private static byte[] BuildMp3WithJunkBeforeFrames()
    {
        var titleFrame = BuildId3v23Frame("TIT2", [0x00, .. Encoding.ASCII.GetBytes("Tagged Title")]);
        var header = new byte[10];
        header[0] = (byte)'I';
        header[1] = (byte)'D';
        header[2] = (byte)'3';
        header[3] = 3; // v2.3.0
        header[5] = 0; // no flags
        var size = titleFrame.Length;
        header[6] = (byte)((size >> 21) & 0x7F);
        header[7] = (byte)((size >> 14) & 0x7F);
        header[8] = (byte)((size >> 7) & 0x7F);
        header[9] = (byte)(size & 0x7F);

        // Junk with no MPEG sync bytes, so the frame scan has to skip over it.
        var junk = new byte[512];
        for (var i = 0; i < junk.Length; i++)
        {
            junk[i] = (byte)(0x11 + (i % 7));
        }

        const int frameCount = 50;
        const int frameLength = 417; // 144 * 128000 / 44100
        var audio = new byte[frameCount * frameLength];
        for (var i = 0; i < frameCount; i++)
        {
            var offset = i * frameLength;
            // MPEG-1 Layer III, no CRC, 128 kbps, 44.1 kHz, no padding
            audio[offset] = 0xFF;
            audio[offset + 1] = 0xFB;
            audio[offset + 2] = 0x90;
            audio[offset + 3] = 0x00;
        }

        return [.. header, .. titleFrame, .. junk, .. audio];
    }

    /// <summary>
    /// Builds a minimal MP3: ID3v2.3 tag (valid TIT2 + 1-byte APIC payload) followed by
    /// 50 CBR MPEG-1 Layer III frames at 128 kbps / 48 kHz (384 bytes each).
    /// </summary>
    private static byte[] BuildMp3WithCorruptPictureFrame()
    {
        var titleFrame = BuildId3v23Frame("TIT2", [0x00, .. Encoding.ASCII.GetBytes("Tagged Title")]);
        var corruptApicFrame = BuildId3v23Frame("APIC", [0x00]);

        var frames = titleFrame.Concat(corruptApicFrame).ToArray();
        var header = new byte[10];
        header[0] = (byte)'I';
        header[1] = (byte)'D';
        header[2] = (byte)'3';
        header[3] = 3; // v2.3.0
        header[4] = 0;
        header[5] = 0; // no flags
        var size = frames.Length;
        header[6] = (byte)((size >> 21) & 0x7F);
        header[7] = (byte)((size >> 14) & 0x7F);
        header[8] = (byte)((size >> 7) & 0x7F);
        header[9] = (byte)(size & 0x7F);

        const int frameCount = 50;
        const int frameLength = 384; // 144 * 128000 / 48000
        var audio = new byte[frameCount * frameLength];
        for (var i = 0; i < frameCount; i++)
        {
            var offset = i * frameLength;
            // MPEG-1 Layer III, no CRC, 128 kbps, 48 kHz, no padding
            audio[offset] = 0xFF;
            audio[offset + 1] = 0xFB;
            audio[offset + 2] = 0x90;
            audio[offset + 3] = 0x64;
        }

        return [.. header, .. frames, .. audio];
    }

    private static byte[] BuildId3v23Frame(string id, byte[] payload)
    {
        var frame = new byte[10 + payload.Length];
        Encoding.ASCII.GetBytes(id).CopyTo(frame, 0);
        frame[4] = (byte)(payload.Length >> 24);
        frame[5] = (byte)(payload.Length >> 16);
        frame[6] = (byte)(payload.Length >> 8);
        frame[7] = (byte)payload.Length;
        frame[8] = 0;
        frame[9] = 0;
        payload.CopyTo(frame, 10);
        return frame;
    }
}
