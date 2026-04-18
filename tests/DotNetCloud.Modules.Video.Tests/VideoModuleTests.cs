using DotNetCloud.Core.Authorization;
using DotNetCloud.Core.Events;
using DotNetCloud.Core.Modules;
using DotNetCloud.Modules.Video.Events;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;

namespace DotNetCloud.Modules.Video.Tests;

[TestClass]
public class VideoModuleTests
{
    [TestMethod]
    public void VideoModuleManifest_HasCorrectId()
    {
        var manifest = new VideoModuleManifest();

        Assert.AreEqual("dotnetcloud.video", manifest.Id);
    }

    [TestMethod]
    public void VideoModuleManifest_HasCorrectName()
    {
        var manifest = new VideoModuleManifest();

        Assert.AreEqual("Video", manifest.Name);
    }

    [TestMethod]
    public void VideoModuleManifest_RequiresCapabilities()
    {
        var manifest = new VideoModuleManifest();

        Assert.IsTrue(manifest.RequiredCapabilities.Count > 0);
    }

    [TestMethod]
    public void VideoModuleManifest_PublishesVideoEvents()
    {
        var manifest = new VideoModuleManifest();

        Assert.IsTrue(manifest.PublishedEvents.Contains(nameof(VideoAddedEvent)));
        Assert.IsTrue(manifest.PublishedEvents.Contains(nameof(VideoDeletedEvent)));
        Assert.IsTrue(manifest.PublishedEvents.Contains(nameof(VideoWatchedEvent)));
    }

    [TestMethod]
    public void VideoModuleManifest_SubscribesToFileUploadedEvent()
    {
        var manifest = new VideoModuleManifest();

        Assert.IsTrue(manifest.SubscribedEvents.Contains("FileUploadedEvent"));
    }

    [TestMethod]
    public async Task VideoModule_InitializeAndStart()
    {
        var eventBus = new Mock<IEventBus>();
        var services = new ServiceCollection();
        services.AddSingleton(eventBus.Object);
        services.AddSingleton(Mock.Of<ILogger<VideoModule>>());
        services.AddSingleton(Mock.Of<ILogger<FileUploadedVideoHandler>>());
        var sp = services.BuildServiceProvider();

        var context = new ModuleInitializationContext
        {
            ModuleId = "dotnetcloud.video",
            Services = sp,
            Configuration = new Dictionary<string, object>(),
            SystemCaller = new CallerContext(Guid.Empty, Array.Empty<string>(), CallerType.System)
        };

        var module = new VideoModule();

        await module.InitializeAsync(context);
        await module.StartAsync(CancellationToken.None);
        await module.StopAsync(CancellationToken.None);

        // Should not throw
        Assert.IsTrue(true);
    }

    [TestMethod]
    public async Task VideoModule_DisposeAsync()
    {
        var module = new VideoModule();

        await module.DisposeAsync();

        // Should not throw
        Assert.IsTrue(true);
    }
}
