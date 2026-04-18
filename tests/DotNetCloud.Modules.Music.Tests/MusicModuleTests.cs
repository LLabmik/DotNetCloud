using DotNetCloud.Core.Authorization;
using DotNetCloud.Core.Events;
using DotNetCloud.Core.Modules;
using DotNetCloud.Modules.Files.Events;
using DotNetCloud.Modules.Music.Events;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace DotNetCloud.Modules.Music.Tests;

[TestClass]
public class MusicModuleTests
{
    private static ModuleInitializationContext CreateContext(IServiceProvider sp) => new()
    {
        ModuleId = "dotnetcloud.music",
        Services = sp,
        Configuration = new Dictionary<string, object>(),
        SystemCaller = new CallerContext(Guid.Empty, ["system"], CallerType.System)
    };

    // ─── Module Lifecycle ─────────────────────────────────────────────

    [TestMethod]
    public void Module_ManifestId_IsDotnetcloudMusic()
    {
        var module = new MusicModule();

        Assert.AreEqual("dotnetcloud.music", module.Manifest.Id);
    }

    [TestMethod]
    public void Module_ManifestVersion_IsSet()
    {
        var module = new MusicModule();

        Assert.IsFalse(string.IsNullOrWhiteSpace(module.Manifest.Version));
    }

    [TestMethod]
    public void Module_InitialState_NotInitializedNotRunning()
    {
        var module = new MusicModule();

        Assert.IsFalse(module.IsInitialized);
        Assert.IsFalse(module.IsRunning);
    }

    [TestMethod]
    public async Task Module_Initialize_SetsInitialized()
    {
        var module = new MusicModule();
        var eventBus = new Mock<IEventBus>();
        var sp = new Mock<IServiceProvider>();
        sp.Setup(s => s.GetService(typeof(IEventBus))).Returns(eventBus.Object);

        await module.InitializeAsync(CreateContext(sp.Object));

        Assert.IsTrue(module.IsInitialized);
    }

    [TestMethod]
    public async Task Module_Start_SetsRunning()
    {
        var module = new MusicModule();
        var eventBus = new Mock<IEventBus>();
        var sp = new Mock<IServiceProvider>();
        sp.Setup(s => s.GetService(typeof(IEventBus))).Returns(eventBus.Object);

        await module.InitializeAsync(CreateContext(sp.Object));
        await module.StartAsync(CancellationToken.None);

        Assert.IsTrue(module.IsRunning);
    }

    [TestMethod]
    public async Task Module_Stop_ClearsRunning()
    {
        var module = new MusicModule();
        var eventBus = new Mock<IEventBus>();
        var sp = new Mock<IServiceProvider>();
        sp.Setup(s => s.GetService(typeof(IEventBus))).Returns(eventBus.Object);

        await module.InitializeAsync(CreateContext(sp.Object));
        await module.StartAsync(CancellationToken.None);
        await module.StopAsync(CancellationToken.None);

        Assert.IsFalse(module.IsRunning);
    }

    // ─── FileUploadedMusicHandler ─────────────────────────────────────

    [TestMethod]
    public void FileUploadedMusicHandler_IsEventHandler()
    {
        var handler = new FileUploadedMusicHandler(NullLogger<FileUploadedMusicHandler>.Instance);

        Assert.IsInstanceOfType<IEventHandler<FileUploadedEvent>>(handler);
    }
}
