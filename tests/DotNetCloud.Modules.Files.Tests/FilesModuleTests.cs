using DotNetCloud.Core.Authorization;
using DotNetCloud.Core.Events;
using DotNetCloud.Core.Modules;
using DotNetCloud.Modules.Files.Events;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace DotNetCloud.Modules.Files.Tests;

/// <summary>
/// Tests for <see cref="FilesModule"/> covering lifecycle, initialization, and event handling.
/// </summary>
[TestClass]
public class FilesModuleTests
{
    private FilesModule _module = null!;
    private Mock<IEventBus> _mockEventBus = null!;
    private ServiceProvider _serviceProvider = null!;

    [TestInitialize]
    public void Setup()
    {
        _module = new FilesModule();
        _mockEventBus = new Mock<IEventBus>();

        var services = new ServiceCollection();
        services.AddSingleton<IEventBus>(_mockEventBus.Object);
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
            ModuleId = "dotnetcloud.files",
            Services = _serviceProvider,
            Configuration = config ?? new Dictionary<string, object>(),
            SystemCaller = new CallerContext(SystemUserId, ["system"], CallerType.System)
        };
    }

    // ---- Manifest ----

    [TestMethod]
    public void WhenCreatedThenManifestIsFilesModuleManifest()
    {
        Assert.IsNotNull(_module.Manifest);
        Assert.IsInstanceOfType<FilesModuleManifest>(_module.Manifest);
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
    public async Task WhenInitializedThenSubscribesToFileUploadedEvent()
    {
        await _module.InitializeAsync(CreateContext());

        _mockEventBus.Verify(
            b => b.SubscribeAsync(It.IsAny<IEventHandler<FileUploadedEvent>>(), It.IsAny<CancellationToken>()),
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
    public async Task WhenInitializedWithConfigThenReadsStoragePath()
    {
        var config = new Dictionary<string, object> { ["storage_path"] = "/data/files" };

        // Should not throw
        await _module.InitializeAsync(CreateContext(config));

        Assert.IsTrue(_module.IsInitialized);
    }

    [TestMethod]
    public async Task WhenInitializedWithMaxUploadSizeConfigThenReadsIt()
    {
        var config = new Dictionary<string, object> { ["max_upload_size"] = 104857600 };

        // Should not throw
        await _module.InitializeAsync(CreateContext(config));

        Assert.IsTrue(_module.IsInitialized);
    }

    [TestMethod]
    public async Task WhenInitializedWithNullContextThenThrowsArgumentNullException()
    {
        await Assert.ThrowsExactlyAsync<ArgumentNullException>(
            () => _module.InitializeAsync(null!));
    }

    // ---- Lifecycle: StartAsync ----

    [TestMethod]
    public async Task WhenStartedAfterInitializeThenSucceeds()
    {
        await _module.InitializeAsync(CreateContext());

        await _module.StartAsync();

        Assert.IsTrue(_module.IsRunning);
    }

    [TestMethod]
    public async Task WhenStartedWithoutInitializeThenThrowsInvalidOperationException()
    {
        await Assert.ThrowsExactlyAsync<InvalidOperationException>(
            () => _module.StartAsync());
    }

    [TestMethod]
    public async Task WhenStartedThenIsInitializedRemainsTrue()
    {
        await _module.InitializeAsync(CreateContext());
        await _module.StartAsync();

        Assert.IsTrue(_module.IsInitialized);
        Assert.IsTrue(_module.IsRunning);
    }

    // ---- Lifecycle: StopAsync ----

    [TestMethod]
    public async Task WhenStoppedThenUnsubscribesFromEvents()
    {
        await _module.InitializeAsync(CreateContext());
        await _module.StartAsync();

        await _module.StopAsync();

        _mockEventBus.Verify(
            b => b.UnsubscribeAsync(It.IsAny<IEventHandler<FileUploadedEvent>>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [TestMethod]
    public async Task WhenStoppedThenIsRunningIsFalse()
    {
        await _module.InitializeAsync(CreateContext());
        await _module.StartAsync();

        await _module.StopAsync();

        Assert.IsFalse(_module.IsRunning);
    }

    // ---- Lifecycle: DisposeAsync ----

    [TestMethod]
    public async Task WhenDisposedViaIAsyncDisposableThenSucceeds()
    {
        await _module.InitializeAsync(CreateContext());

        // Should not throw
        await ((IAsyncDisposable)_module).DisposeAsync();
    }

    [TestMethod]
    public async Task WhenDisposedViaModuleLifecycleThenSucceeds()
    {
        await _module.InitializeAsync(CreateContext());

        // Should not throw
        await ((IModuleLifecycle)_module).DisposeAsync();
    }

    // ---- Lifecycle: Full ----

    [TestMethod]
    public async Task WhenFullLifecycleExecutedThenAllStepsSucceed()
    {
        await _module.InitializeAsync(CreateContext());
        await _module.StartAsync();
        await _module.StopAsync();
        await ((IAsyncDisposable)_module).DisposeAsync();

        _mockEventBus.Verify(
            b => b.SubscribeAsync(It.IsAny<IEventHandler<FileUploadedEvent>>(), It.IsAny<CancellationToken>()),
            Times.Once);
        _mockEventBus.Verify(
            b => b.UnsubscribeAsync(It.IsAny<IEventHandler<FileUploadedEvent>>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    // ---- Initial state ----

    [TestMethod]
    public void WhenCreatedThenIsInitializedIsFalse()
    {
        Assert.IsFalse(_module.IsInitialized);
    }

    [TestMethod]
    public void WhenCreatedThenIsRunningIsFalse()
    {
        Assert.IsFalse(_module.IsRunning);
    }
}
