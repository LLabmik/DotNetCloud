using DotNetCloud.Core.Authorization;
using DotNetCloud.Core.Events;
using DotNetCloud.Core.Modules;
using DotNetCloud.Modules.Chat.Events;
using DotNetCloud.Modules.Chat.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace DotNetCloud.Modules.Chat.Tests;

/// <summary>
/// Tests for <see cref="ChatModule"/> covering lifecycle, initialization, and event handling.
/// </summary>
[TestClass]
public class ChatModuleTests
{
    private ChatModule _module = null!;
    private Mock<IEventBus> _mockEventBus = null!;
    private ServiceProvider _serviceProvider = null!;

    [TestInitialize]
    public void Setup()
    {
        _module = new ChatModule();
        _mockEventBus = new Mock<IEventBus>();

        var services = new ServiceCollection();
        services.AddSingleton<IEventBus>(_mockEventBus.Object);
        services.AddSingleton<IChatMessageNotifier>(new Mock<IChatMessageNotifier>().Object);
        services.AddSingleton<ILoggerFactory>(NullLoggerFactory.Instance);
        services.AddSingleton(typeof(ILogger<>), typeof(NullLogger<>));
        _serviceProvider = services.BuildServiceProvider();
    }

    [TestCleanup]
    public void Cleanup()
    {
        _serviceProvider.Dispose();
    }

    private static readonly Guid SystemUserId = Guid.Parse("00000000-0000-0000-0000-000000000001");

    private ModuleInitializationContext CreateContext(Dictionary<string, object>? config = null)
    {
        return new ModuleInitializationContext
        {
            ModuleId = "dotnetcloud.chat",
            Services = _serviceProvider,
            Configuration = config ?? new Dictionary<string, object>(),
            SystemCaller = new CallerContext(SystemUserId, ["system"], CallerType.System)
        };
    }

    // ---- Manifest ----

    [TestMethod]
    public void WhenCreatedThenManifestIsChatModuleManifest()
    {
        Assert.IsNotNull(_module.Manifest);
        Assert.IsInstanceOfType<ChatModuleManifest>(_module.Manifest);
    }

    [TestMethod]
    public void WhenCreatedThenImplementsIModuleLifecycle()
    {
        Assert.IsInstanceOfType<IModuleLifecycle>(_module);
        Assert.IsInstanceOfType<IModule>(_module);
        Assert.IsInstanceOfType<IAsyncDisposable>(_module);
    }

    // ---- Lifecycle: InitializeAsync ----

    [TestMethod]
    public async Task WhenInitializedThenSubscribesToMessageSentEvent()
    {
        await _module.InitializeAsync(CreateContext());

        _mockEventBus.Verify(
            b => b.SubscribeAsync(It.IsAny<IEventHandler<MessageSentEvent>>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [TestMethod]
    public async Task WhenInitializedThenSubscribesToChannelCreatedEvent()
    {
        await _module.InitializeAsync(CreateContext());

        _mockEventBus.Verify(
            b => b.SubscribeAsync(It.IsAny<IEventHandler<ChannelCreatedEvent>>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [TestMethod]
    public async Task WhenInitializedThenIsInitializedIsTrue()
    {
        await _module.InitializeAsync(CreateContext());

        Assert.IsTrue(_module.IsInitialized);
    }

    [TestMethod]
    public async Task WhenInitializedThenIsRunningIsFalse()
    {
        await _module.InitializeAsync(CreateContext());

        Assert.IsFalse(_module.IsRunning);
    }

    [TestMethod]
    public void WhenNotInitializedThenIsInitializedIsFalse()
    {
        Assert.IsFalse(_module.IsInitialized);
    }

    [TestMethod]
    public void WhenNotInitializedThenIsRunningIsFalse()
    {
        Assert.IsFalse(_module.IsRunning);
    }

    // ---- Lifecycle: StartAsync ----

    [TestMethod]
    public async Task WhenStartedAfterInitThenIsRunningIsTrue()
    {
        await _module.InitializeAsync(CreateContext());
        await _module.StartAsync();

        Assert.IsTrue(_module.IsRunning);
    }

    [TestMethod]
    public async Task WhenStartedWithoutInitThenThrows()
    {
        await Assert.ThrowsExactlyAsync<InvalidOperationException>(
            async () => await _module.StartAsync());
    }

    // ---- Lifecycle: StopAsync ----

    [TestMethod]
    public async Task WhenStoppedThenIsRunningIsFalse()
    {
        await _module.InitializeAsync(CreateContext());
        await _module.StartAsync();
        await _module.StopAsync();

        Assert.IsFalse(_module.IsRunning);
    }

    [TestMethod]
    public async Task WhenStoppedThenUnsubscribesFromMessageSentEvent()
    {
        await _module.InitializeAsync(CreateContext());
        await _module.StartAsync();
        await _module.StopAsync();

        _mockEventBus.Verify(
            b => b.UnsubscribeAsync(It.IsAny<IEventHandler<MessageSentEvent>>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [TestMethod]
    public async Task WhenStoppedThenUnsubscribesFromChannelCreatedEvent()
    {
        await _module.InitializeAsync(CreateContext());
        await _module.StartAsync();
        await _module.StopAsync();

        _mockEventBus.Verify(
            b => b.UnsubscribeAsync(It.IsAny<IEventHandler<ChannelCreatedEvent>>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    // ---- Lifecycle: DisposeAsync ----

    [TestMethod]
    public async Task WhenDisposedThenDoesNotThrow()
    {
        await _module.InitializeAsync(CreateContext());
        await _module.StartAsync();
        await ((IAsyncDisposable)_module).DisposeAsync();
    }

    // ---- InitializeAsync null check ----

    [TestMethod]
    public async Task WhenInitializedWithNullContextThenThrows()
    {
        await Assert.ThrowsExactlyAsync<ArgumentNullException>(
            async () => await _module.InitializeAsync(null!));
    }

    // ---- Manifest ID ----

    [TestMethod]
    public void WhenCreatedThenManifestIdIsDotnetcloudChat()
    {
        Assert.AreEqual("dotnetcloud.chat", _module.Manifest.Id);
    }
}
