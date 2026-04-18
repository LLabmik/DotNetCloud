using DotNetCloud.Core.Authorization;
using DotNetCloud.Core.Events;
using DotNetCloud.Core.Modules;
using DotNetCloud.Modules.AI.Events;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace DotNetCloud.Modules.AI.Tests;

/// <summary>
/// Tests for <see cref="AiModule"/> lifecycle.
/// </summary>
[TestClass]
public class AiModuleTests
{
    private AiModule _module;
    private Mock<IEventBus> _eventBusMock;
    private ServiceProvider _serviceProvider;

    [TestInitialize]
    public void Setup()
    {
        _module = new AiModule();
        _eventBusMock = new Mock<IEventBus>();

        var services = new ServiceCollection();
        services.AddSingleton(_eventBusMock.Object);
        services.AddSingleton<ILoggerFactory>(NullLoggerFactory.Instance);
        services.AddSingleton(typeof(ILogger<>), typeof(NullLogger<>));
        _serviceProvider = services.BuildServiceProvider();
    }

    [TestCleanup]
    public void Cleanup()
    {
        _serviceProvider.Dispose();
    }

    private ModuleInitializationContext CreateContext() => new()
    {
        ModuleId = "dotnetcloud.ai",
        Services = _serviceProvider,
        Configuration = new Dictionary<string, object>(),
        SystemCaller = new CallerContext(Guid.Empty, ["system"], CallerType.System)
    };

    [TestMethod]
    public void Manifest_HasCorrectId()
    {
        Assert.AreEqual("dotnetcloud.ai", _module.Manifest.Id);
    }

    [TestMethod]
    public void Manifest_HasCorrectName()
    {
        Assert.AreEqual("AI Assistant", _module.Manifest.Name);
    }

    [TestMethod]
    public void Manifest_RequiresLlmProviderCapability()
    {
        Assert.IsTrue(_module.Manifest.RequiredCapabilities.Contains("ILlmProvider"));
    }

    [TestMethod]
    public void Manifest_PublishesConversationEvents()
    {
        Assert.IsTrue(_module.Manifest.PublishedEvents.Contains(nameof(ConversationCreatedEvent)));
        Assert.IsTrue(_module.Manifest.PublishedEvents.Contains(nameof(ConversationMessageEvent)));
    }

    [TestMethod]
    public async Task InitializeAsync_SetsInitializedTrue()
    {
        var context = CreateContext();

        await _module.InitializeAsync(context);

        Assert.IsTrue(_module.IsInitialized);
    }

    [TestMethod]
    public async Task InitializeAsync_SubscribesToEvents()
    {
        var context = CreateContext();

        await _module.InitializeAsync(context);

        _eventBusMock.Verify(
            bus => bus.SubscribeAsync(It.IsAny<ConversationCreatedEventHandler>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [TestMethod]
    public async Task StartAsync_WithoutInitialize_ThrowsInvalidOperation()
    {
        await Assert.ThrowsExactlyAsync<InvalidOperationException>(
            () => _module.StartAsync());
    }

    [TestMethod]
    public async Task StartAsync_AfterInitialize_SetsRunningTrue()
    {
        var context = CreateContext();
        await _module.InitializeAsync(context);

        await _module.StartAsync();

        Assert.IsTrue(_module.IsRunning);
    }

    [TestMethod]
    public async Task StopAsync_SetsRunningFalse()
    {
        var context = CreateContext();
        await _module.InitializeAsync(context);
        await _module.StartAsync();

        await _module.StopAsync();

        Assert.IsFalse(_module.IsRunning);
    }

    [TestMethod]
    public async Task StopAsync_UnsubscribesFromEvents()
    {
        var context = CreateContext();
        await _module.InitializeAsync(context);
        await _module.StartAsync();

        await _module.StopAsync();

        _eventBusMock.Verify(
            bus => bus.UnsubscribeAsync(It.IsAny<ConversationCreatedEventHandler>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }
}
