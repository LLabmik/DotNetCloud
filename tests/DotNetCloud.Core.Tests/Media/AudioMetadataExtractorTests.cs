namespace DotNetCloud.Core.Tests.Media;

using DotNetCloud.Core.DTOs.Media;
using DotNetCloud.Core.ServiceDefaults.Media;
using Microsoft.Extensions.Logging;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;

/// <summary>
/// Tests for <see cref="AudioMetadataExtractor"/>.
/// Since TagLibSharp requires real (valid) audio files for full extraction,
/// these tests verify the extractor's behavior with file-existence checks,
/// MIME type support, and error handling.
/// </summary>
[TestClass]
public class AudioMetadataExtractorTests
{
    private AudioMetadataExtractor _extractor = null!;
    private string _tempDir = null!;

    [TestInitialize]
    public void Setup()
    {
        var logger = new Mock<ILogger<AudioMetadataExtractor>>();
        _extractor = new AudioMetadataExtractor(logger.Object);
        _tempDir = Path.Combine(Path.GetTempPath(), "dotnetcloud-test-" + Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(_tempDir);
    }

    [TestCleanup]
    public void Cleanup()
    {
        if (Directory.Exists(_tempDir))
            Directory.Delete(_tempDir, recursive: true);
    }

    [TestMethod]
    public void SupportedMediaType_ReturnsAudio()
    {
        Assert.AreEqual(MediaType.Audio, _extractor.SupportedMediaType);
    }

    [TestMethod]
    [DataRow("audio/mpeg", true)]
    [DataRow("audio/mp3", true)]
    [DataRow("audio/flac", true)]
    [DataRow("audio/ogg", true)]
    [DataRow("audio/vorbis", true)]
    [DataRow("audio/opus", true)]
    [DataRow("audio/aac", true)]
    [DataRow("audio/mp4", true)]
    [DataRow("audio/m4a", true)]
    [DataRow("audio/x-m4a", true)]
    [DataRow("audio/wav", true)]
    [DataRow("audio/x-wav", true)]
    [DataRow("audio/wave", true)]
    [DataRow("audio/x-ms-wma", true)]
    [DataRow("audio/webm", true)]
    [DataRow("image/jpeg", false)]
    [DataRow("video/mp4", false)]
    [DataRow("text/plain", false)]
    [DataRow("application/octet-stream", false)]
    public void CanExtract_ReturnsExpected(string mimeType, bool expected)
    {
        Assert.AreEqual(expected, _extractor.CanExtract(mimeType));
    }

    [TestMethod]
    public async Task ExtractAsync_NonexistentFile_ReturnsNull()
    {
        // Act
        var result = await _extractor.ExtractAsync("/nonexistent/song.mp3", "audio/mpeg");

        // Assert
        Assert.IsNull(result);
    }

    [TestMethod]
    public async Task ExtractAsync_EmptyFile_ReturnsNull()
    {
        // Arrange
        var filePath = Path.Combine(_tempDir, "empty.mp3");
        await File.WriteAllBytesAsync(filePath, Array.Empty<byte>());

        // Act
        var result = await _extractor.ExtractAsync(filePath, "audio/mpeg");

        // Assert — TagLib should handle empty files gracefully
        Assert.IsNull(result);
    }

    [TestMethod]
    public async Task ExtractAsync_CorruptFile_ReturnsNull()
    {
        // Arrange
        var filePath = Path.Combine(_tempDir, "corrupt.mp3");
        await File.WriteAllBytesAsync(filePath, new byte[] { 0x00, 0x01, 0x02, 0x03, 0x04 });

        // Act
        var result = await _extractor.ExtractAsync(filePath, "audio/mpeg");

        // Assert
        Assert.IsNull(result);
    }

    [TestMethod]
    public async Task ExtractAsync_MinimalWavFile_ExtractsBasicProperties()
    {
        // Arrange — create a minimal valid WAV file
        var filePath = Path.Combine(_tempDir, "minimal.wav");
        var wavData = CreateMinimalWavFile(sampleRate: 44100, channels: 2, durationSeconds: 1);
        await File.WriteAllBytesAsync(filePath, wavData);

        // Act
        var result = await _extractor.ExtractAsync(filePath, "audio/wav");

        // Assert
        Assert.IsNotNull(result);
        Assert.AreEqual(MediaType.Audio, result.MediaType);
        Assert.AreEqual(44100, result.SampleRate);
        Assert.AreEqual(2, result.Channels);
        Assert.IsNotNull(result.Duration);
        Assert.IsTrue(result.Duration.Value.TotalSeconds > 0, "Duration should be > 0");
    }

    [TestMethod]
    public async Task ExtractAsync_WavFile_HasNoTagMetadata()
    {
        // Arrange — WAV without tags
        var filePath = Path.Combine(_tempDir, "notags.wav");
        var wavData = CreateMinimalWavFile(sampleRate: 48000, channels: 1, durationSeconds: 1);
        await File.WriteAllBytesAsync(filePath, wavData);

        // Act
        var result = await _extractor.ExtractAsync(filePath, "audio/wav");

        // Assert
        Assert.IsNotNull(result);
        Assert.IsNull(result.Title);
        Assert.IsNull(result.Artist);
        Assert.IsNull(result.Album);
        Assert.IsNull(result.Genre);
        Assert.AreEqual(false, result.HasEmbeddedArt);
    }

    [TestMethod]
    public void CanExtract_CaseInsensitive_ReturnsTrue()
    {
        Assert.IsTrue(_extractor.CanExtract("AUDIO/MPEG"));
        Assert.IsTrue(_extractor.CanExtract("Audio/Flac"));
    }

    /// <summary>
    /// Creates a minimal valid WAV file (PCM 16-bit) with silence.
    /// </summary>
    private static byte[] CreateMinimalWavFile(int sampleRate, int channels, int durationSeconds)
    {
        int bitsPerSample = 16;
        int bytesPerSample = bitsPerSample / 8;
        int dataSize = sampleRate * channels * bytesPerSample * durationSeconds;
        int fileSize = 44 + dataSize; // 44-byte header + data

        using var ms = new MemoryStream();
        using var writer = new BinaryWriter(ms);

        // RIFF header
        writer.Write("RIFF"u8);
        writer.Write(fileSize - 8);
        writer.Write("WAVE"u8);

        // fmt chunk
        writer.Write("fmt "u8);
        writer.Write(16);               // chunk size
        writer.Write((short)1);         // PCM format
        writer.Write((short)channels);
        writer.Write(sampleRate);
        writer.Write(sampleRate * channels * bytesPerSample); // byte rate
        writer.Write((short)(channels * bytesPerSample));     // block align
        writer.Write((short)bitsPerSample);

        // data chunk
        writer.Write("data"u8);
        writer.Write(dataSize);
        writer.Write(new byte[dataSize]); // silence

        return ms.ToArray();
    }
}
