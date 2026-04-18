using DotNetCloud.Core.Authorization;
using DotNetCloud.Core.Events;
using DotNetCloud.Core.Events.Search;
using DotNetCloud.Core.Modules;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using DotNetCloud.Modules.Search.Data;

namespace DotNetCloud.Modules.Search.Tests;

/// <summary>
/// Tests for <see cref="SearchModule"/> lifecycle.
/// </summary>
[TestClass]
public class SearchModuleTests
{
    private SearchModule _module = null!;
    private Mock<IEventBus> _eventBusMock = null!;
    private ServiceProvider _serviceProvider = null!;

    [TestInitialize]
    public void Setup()
    {
        _module = new SearchModule();
        _eventBusMock = new Mock<IEventBus>();

        var services = new ServiceCollection();
        services.AddSingleton<IEventBus>(_eventBusMock.Object);
        services.AddSingleton<ILoggerFactory>(NullLoggerFactory.Instance);
        services.AddSingleton(typeof(ILogger<>), typeof(NullLogger<>));
        services.AddDbContext<SearchDbContext>(options =>
            options.UseInMemoryDatabase(Guid.NewGuid().ToString()));

        _serviceProvider = services.BuildServiceProvider();
    }

    [TestCleanup]
    public void Cleanup()
    {
        _serviceProvider.Dispose();
    }

    private ModuleInitializationContext CreateContext() => new()
    {
        ModuleId = "dotnetcloud.search",
        Services = _serviceProvider,
        Configuration = new Dictionary<string, object>(),
        SystemCaller = new CallerContext(Guid.Empty, [], CallerType.System)
    };

    [TestMethod]
    public void Manifest_HasCorrectProperties()
    {
        Assert.AreEqual("dotnetcloud.search", _module.Manifest.Id);
        Assert.AreEqual("Search", _module.Manifest.Name);
        Assert.AreEqual("1.0.0", _module.Manifest.Version);
    }

    [TestMethod]
    public void Manifest_RequiredCapabilities_ContainsExpected()
    {
        var capabilities = _module.Manifest.RequiredCapabilities;
        Assert.IsTrue(capabilities.Contains("ISearchableModule"));
        Assert.IsTrue(capabilities.Contains("IEventBus"));
    }

    [TestMethod]
    public void Manifest_PublishedEvents_ContainsSearchIndexCompleted()
    {
        Assert.IsTrue(_module.Manifest.PublishedEvents.Contains(nameof(SearchIndexCompletedEvent)));
    }

    [TestMethod]
    public void Manifest_SubscribedEvents_ContainsSearchIndexRequest()
    {
        Assert.IsTrue(_module.Manifest.SubscribedEvents.Contains(nameof(SearchIndexRequestEvent)));
    }

    [TestMethod]
    public async Task InitializeAsync_SubscribesToEventBus()
    {
        var context = CreateContext();

        await _module.InitializeAsync(context);

        _eventBusMock.Verify(
            b => b.SubscribeAsync(It.IsAny<IEventHandler<SearchIndexRequestEvent>>(), It.IsAny<CancellationToken>()),
            Times.Once);

        Assert.IsTrue(_module.IsInitialized);
    }

    [TestMethod]
    public async Task StartAsync_WithoutInit_ThrowsInvalidOperationException()
    {
        await Assert.ThrowsExactlyAsync<InvalidOperationException>(
            () => _module.StartAsync());
    }

    [TestMethod]
    public async Task StartAsync_AfterInit_SetsRunning()
    {
        var context = CreateContext();

        await _module.InitializeAsync(context);
        await _module.StartAsync();

        Assert.IsTrue(_module.IsRunning);
    }

    [TestMethod]
    public async Task StopAsync_UnsubscribesFromEventBus()
    {
        var context = CreateContext();

        await _module.InitializeAsync(context);
        await _module.StartAsync();
        await _module.StopAsync();

        _eventBusMock.Verify(
            b => b.UnsubscribeAsync(It.IsAny<IEventHandler<SearchIndexRequestEvent>>(), It.IsAny<CancellationToken>()),
            Times.Once);

        Assert.IsFalse(_module.IsRunning);
    }

    [TestMethod]
    public async Task DisposeAsync_DoesNotThrow()
    {
        await ((IAsyncDisposable)_module).DisposeAsync();
    }

    [TestMethod]
    public void InitialState_NotInitializedNotRunning()
    {
        Assert.IsFalse(_module.IsInitialized);
        Assert.IsFalse(_module.IsRunning);
    }

    [TestMethod]
    public async Task InitializeAsync_NullContext_ThrowsArgumentNullException()
    {
        await Assert.ThrowsExactlyAsync<ArgumentNullException>(
            () => _module.InitializeAsync(null!));
    }
}
