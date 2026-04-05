namespace DotNetCloud.Core.Tests.Media;

using DotNetCloud.Core.Capabilities;
using DotNetCloud.Core.DTOs.Media;
using DotNetCloud.Core.ServiceDefaults.Media;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.VisualStudio.TestTools.UnitTesting;

/// <summary>
/// Tests for <see cref="MediaServiceCollectionExtensions.AddMediaMetadataExtractors"/>.
/// Validates that all extractors are correctly registered in the DI container
/// both as keyed services and as an enumerable.
/// </summary>
[TestClass]
public class MediaServiceRegistrationTests
{
    private ServiceProvider _provider = null!;

    [TestInitialize]
    public void Setup()
    {
        var services = new ServiceCollection();
        services.AddLogging(b => b.AddConsole());
        services.AddSingleton<IConfiguration>(new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Media:FfprobePath"] = "ffprobe"
            })
            .Build());
        services.AddMediaMetadataExtractors();
        _provider = services.BuildServiceProvider();
    }

    [TestCleanup]
    public void Cleanup()
    {
        _provider.Dispose();
    }

    [TestMethod]
    public void ResolveKeyedExtractor_Photo_ReturnsExifExtractor()
    {
        // Act
        var extractor = _provider.GetKeyedService<IMediaMetadataExtractor>(MediaType.Photo);

        // Assert
        Assert.IsNotNull(extractor);
        Assert.IsInstanceOfType(extractor, typeof(ExifMetadataExtractor));
        Assert.AreEqual(MediaType.Photo, extractor.SupportedMediaType);
    }

    [TestMethod]
    public void ResolveKeyedExtractor_Audio_ReturnsAudioExtractor()
    {
        // Act
        var extractor = _provider.GetKeyedService<IMediaMetadataExtractor>(MediaType.Audio);

        // Assert
        Assert.IsNotNull(extractor);
        Assert.IsInstanceOfType(extractor, typeof(AudioMetadataExtractor));
        Assert.AreEqual(MediaType.Audio, extractor.SupportedMediaType);
    }

    [TestMethod]
    public void ResolveKeyedExtractor_Video_ReturnsVideoExtractor()
    {
        // Act
        var extractor = _provider.GetKeyedService<IMediaMetadataExtractor>(MediaType.Video);

        // Assert
        Assert.IsNotNull(extractor);
        Assert.IsInstanceOfType(extractor, typeof(VideoMetadataExtractor));
        Assert.AreEqual(MediaType.Video, extractor.SupportedMediaType);
    }

    [TestMethod]
    public void ResolveAllExtractors_ReturnsThree()
    {
        // Act
        var extractors = _provider.GetServices<IMediaMetadataExtractor>().ToList();

        // Assert
        Assert.AreEqual(3, extractors.Count);
        Assert.IsTrue(extractors.Any(e => e.SupportedMediaType == MediaType.Photo));
        Assert.IsTrue(extractors.Any(e => e.SupportedMediaType == MediaType.Audio));
        Assert.IsTrue(extractors.Any(e => e.SupportedMediaType == MediaType.Video));
    }

    [TestMethod]
    public void KeyedExtractors_AreSingletons()
    {
        // Act
        var photo1 = _provider.GetKeyedService<IMediaMetadataExtractor>(MediaType.Photo);
        var photo2 = _provider.GetKeyedService<IMediaMetadataExtractor>(MediaType.Photo);

        // Assert
        Assert.AreSame(photo1, photo2);
    }

    [TestMethod]
    public void AllExtractors_ImplementICapabilityInterface()
    {
        // Act
        var extractors = _provider.GetServices<IMediaMetadataExtractor>().ToList();

        // Assert
        foreach (var extractor in extractors)
        {
            Assert.IsTrue(typeof(ICapabilityInterface).IsAssignableFrom(extractor.GetType()),
                $"{extractor.GetType().Name} should implement ICapabilityInterface");
        }
    }

    [TestMethod]
    public void AllExtractors_CanExtractAtLeastOneMimeType()
    {
        // Arrange
        var sampleMimeTypes = new Dictionary<MediaType, string>
        {
            [MediaType.Photo] = "image/jpeg",
            [MediaType.Audio] = "audio/mpeg",
            [MediaType.Video] = "video/mp4"
        };

        // Act
        var extractors = _provider.GetServices<IMediaMetadataExtractor>().ToList();

        // Assert
        foreach (var extractor in extractors)
        {
            var mimeType = sampleMimeTypes[extractor.SupportedMediaType];
            Assert.IsTrue(extractor.CanExtract(mimeType),
                $"{extractor.GetType().Name} should support {mimeType}");
        }
    }
}
