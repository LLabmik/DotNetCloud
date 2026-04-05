namespace DotNetCloud.Core.Tests.DTOs.Media;

using DotNetCloud.Core.DTOs.Media;
using Microsoft.VisualStudio.TestTools.UnitTesting;

/// <summary>
/// Tests for <see cref="MediaMetadataDto"/> record.
/// </summary>
[TestClass]
public class MediaMetadataDtoTests
{
    [TestMethod]
    public void MediaMetadataDto_Photo_SetsPhotoFields()
    {
        // Arrange & Act
        var dto = new MediaMetadataDto
        {
            MediaType = MediaType.Photo,
            Width = 4000,
            Height = 3000,
            CameraMake = "Canon",
            CameraModel = "EOS R5",
            LensModel = "RF 24-70mm F2.8 L IS USM",
            FocalLengthMm = 50.0,
            Aperture = 2.8,
            ShutterSpeed = "1/250",
            Iso = 400,
            FlashFired = false,
            Orientation = 1,
            TakenAtUtc = new DateTime(2026, 1, 15, 10, 30, 0, DateTimeKind.Utc),
            Location = new GeoCoordinate { Latitude = 48.8566, Longitude = 2.3522 }
        };

        // Assert
        Assert.AreEqual(MediaType.Photo, dto.MediaType);
        Assert.AreEqual(4000, dto.Width);
        Assert.AreEqual(3000, dto.Height);
        Assert.AreEqual("Canon", dto.CameraMake);
        Assert.AreEqual("EOS R5", dto.CameraModel);
        Assert.AreEqual("RF 24-70mm F2.8 L IS USM", dto.LensModel);
        Assert.AreEqual(50.0, dto.FocalLengthMm);
        Assert.AreEqual(2.8, dto.Aperture);
        Assert.AreEqual("1/250", dto.ShutterSpeed);
        Assert.AreEqual(400, dto.Iso);
        Assert.AreEqual(false, dto.FlashFired);
        Assert.AreEqual(1, dto.Orientation);
        Assert.IsNotNull(dto.TakenAtUtc);
        Assert.IsNotNull(dto.Location);
        Assert.AreEqual(48.8566, dto.Location.Latitude);
    }

    [TestMethod]
    public void MediaMetadataDto_Audio_SetsAudioFields()
    {
        // Arrange & Act
        var dto = new MediaMetadataDto
        {
            MediaType = MediaType.Audio,
            Duration = TimeSpan.FromMinutes(3.5),
            Bitrate = 320000,
            SampleRate = 44100,
            Channels = 2,
            Codec = "MPEG Audio Layer 3",
            Title = "Bohemian Rhapsody",
            Artist = "Queen",
            Album = "A Night at the Opera",
            AlbumArtist = "Queen",
            Genre = "Rock",
            TrackNumber = 11,
            TrackCount = 12,
            DiscNumber = 1,
            DiscCount = 1,
            Year = 1975,
            HasEmbeddedArt = true
        };

        // Assert
        Assert.AreEqual(MediaType.Audio, dto.MediaType);
        Assert.AreEqual(TimeSpan.FromMinutes(3.5), dto.Duration);
        Assert.AreEqual(320000, dto.Bitrate);
        Assert.AreEqual(44100, dto.SampleRate);
        Assert.AreEqual(2, dto.Channels);
        Assert.AreEqual("Bohemian Rhapsody", dto.Title);
        Assert.AreEqual("Queen", dto.Artist);
        Assert.AreEqual("A Night at the Opera", dto.Album);
        Assert.AreEqual("Rock", dto.Genre);
        Assert.AreEqual(11, dto.TrackNumber);
        Assert.AreEqual(1975, dto.Year);
        Assert.AreEqual(true, dto.HasEmbeddedArt);
    }

    [TestMethod]
    public void MediaMetadataDto_Video_SetsVideoFields()
    {
        // Arrange & Act
        var dto = new MediaMetadataDto
        {
            MediaType = MediaType.Video,
            Width = 1920,
            Height = 1080,
            Duration = TimeSpan.FromMinutes(120),
            Codec = "h264",
            Bitrate = 8000000,
            FrameRate = 23.976,
            AudioTrackCount = 2,
            SubtitleTrackCount = 3,
            SampleRate = 48000,
            Channels = 6
        };

        // Assert
        Assert.AreEqual(MediaType.Video, dto.MediaType);
        Assert.AreEqual(1920, dto.Width);
        Assert.AreEqual(1080, dto.Height);
        Assert.AreEqual(TimeSpan.FromMinutes(120), dto.Duration);
        Assert.AreEqual("h264", dto.Codec);
        Assert.AreEqual(8000000, dto.Bitrate);
        Assert.AreEqual(23.976, dto.FrameRate);
        Assert.AreEqual(2, dto.AudioTrackCount);
        Assert.AreEqual(3, dto.SubtitleTrackCount);
    }

    [TestMethod]
    public void MediaMetadataDto_MinimalMetadata_AllNullablesAreNull()
    {
        // Arrange & Act
        var dto = new MediaMetadataDto { MediaType = MediaType.Photo };

        // Assert
        Assert.IsNull(dto.Width);
        Assert.IsNull(dto.Height);
        Assert.IsNull(dto.Duration);
        Assert.IsNull(dto.Codec);
        Assert.IsNull(dto.Bitrate);
        Assert.IsNull(dto.SampleRate);
        Assert.IsNull(dto.Channels);
        Assert.IsNull(dto.CameraMake);
        Assert.IsNull(dto.CameraModel);
        Assert.IsNull(dto.LensModel);
        Assert.IsNull(dto.FocalLengthMm);
        Assert.IsNull(dto.Aperture);
        Assert.IsNull(dto.ShutterSpeed);
        Assert.IsNull(dto.Iso);
        Assert.IsNull(dto.FlashFired);
        Assert.IsNull(dto.Orientation);
        Assert.IsNull(dto.TakenAtUtc);
        Assert.IsNull(dto.Location);
        Assert.IsNull(dto.Title);
        Assert.IsNull(dto.Artist);
        Assert.IsNull(dto.Album);
        Assert.IsNull(dto.AlbumArtist);
        Assert.IsNull(dto.Genre);
        Assert.IsNull(dto.TrackNumber);
        Assert.IsNull(dto.TrackCount);
        Assert.IsNull(dto.DiscNumber);
        Assert.IsNull(dto.DiscCount);
        Assert.IsNull(dto.Year);
        Assert.IsNull(dto.HasEmbeddedArt);
        Assert.IsNull(dto.FrameRate);
        Assert.IsNull(dto.AudioTrackCount);
        Assert.IsNull(dto.SubtitleTrackCount);
    }

    [TestMethod]
    public void MediaMetadataDto_Equality_SameValues()
    {
        // Arrange
        var dto1 = new MediaMetadataDto { MediaType = MediaType.Audio, Title = "Test Song" };
        var dto2 = new MediaMetadataDto { MediaType = MediaType.Audio, Title = "Test Song" };

        // Act & Assert
        Assert.AreEqual(dto1, dto2);
    }
}
