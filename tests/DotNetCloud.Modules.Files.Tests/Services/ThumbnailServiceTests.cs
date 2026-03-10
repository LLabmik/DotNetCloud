using DotNetCloud.Modules.Files.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace DotNetCloud.Modules.Files.Tests.Services;

[TestClass]
public class ThumbnailServiceTests
{
    [TestMethod]
    public async Task GenerateThumbnailAsync_WithVideoMimeType_GeneratesThumbnailsFromExtractedFrame()
    {
        var storageRoot = Path.Combine(Path.GetTempPath(), "dnc-thumb-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(storageRoot);

        try
        {
            var config = new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["Files:Storage:RootPath"] = storageRoot
                })
                .Build();

            var extractor = new FakeVideoFrameExtractor(success: true);
            var service = new ThumbnailService(config, extractor, NullLogger<ThumbnailService>.Instance);

            var fileId = Guid.NewGuid();
            var fakeVideoPath = Path.Combine(storageRoot, "sample.mp4");
            await File.WriteAllTextAsync(fakeVideoPath, "video-binary-placeholder");

            await service.GenerateThumbnailAsync(fileId, fakeVideoPath, "video/mp4");

            var (smallData, smallContentType) = await service.GetThumbnailAsync(fileId, ThumbnailSize.Small);
            Assert.IsNotNull(smallData);
            Assert.AreEqual("image/jpeg", smallContentType);
            Assert.AreEqual(1, extractor.CallCount);
            await smallData.DisposeAsync();
        }
        finally
        {
            if (Directory.Exists(storageRoot))
            {
                Directory.Delete(storageRoot, recursive: true);
            }
        }
    }

    [TestMethod]
    public async Task GenerateThumbnailAsync_WithVideoMimeType_WhenExtractionFails_DoesNotGenerateThumbnails()
    {
        var storageRoot = Path.Combine(Path.GetTempPath(), "dnc-thumb-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(storageRoot);

        try
        {
            var config = new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["Files:Storage:RootPath"] = storageRoot
                })
                .Build();

            var extractor = new FakeVideoFrameExtractor(success: false);
            var service = new ThumbnailService(config, extractor, NullLogger<ThumbnailService>.Instance);

            var fileId = Guid.NewGuid();
            var fakeVideoPath = Path.Combine(storageRoot, "sample-fail.mp4");
            await File.WriteAllTextAsync(fakeVideoPath, "video-binary-placeholder");

            await service.GenerateThumbnailAsync(fileId, fakeVideoPath, "video/mp4");

            var (data, contentType) = await service.GetThumbnailAsync(fileId, ThumbnailSize.Small);
            Assert.IsNull(data);
            Assert.IsNull(contentType);
            Assert.AreEqual(1, extractor.CallCount);
        }
        finally
        {
            if (Directory.Exists(storageRoot))
            {
                Directory.Delete(storageRoot, recursive: true);
            }
        }
    }

    private sealed class FakeVideoFrameExtractor : IVideoFrameExtractor
    {
        private readonly bool _success;

        public FakeVideoFrameExtractor(bool success)
        {
            _success = success;
        }

        public int CallCount { get; private set; }

        public async Task<bool> TryExtractFrameAsync(string inputPath, string outputPath, CancellationToken cancellationToken = default)
        {
            CallCount++;

            if (!_success)
            {
                return false;
            }

            using var image = new Image<Rgba32>(64, 64);
            image[0, 0] = new Rgba32(255, 0, 0, 255);
            await image.SaveAsJpegAsync(outputPath, cancellationToken);
            return true;
        }
    }
}
