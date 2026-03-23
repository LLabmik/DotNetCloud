using DotNetCloud.Core.Authorization;
using DotNetCloud.Core.Events;
using DotNetCloud.Core.Modules;
using DotNetCloud.Modules.Example.Events;
using NoteCreatedEvent = DotNetCloud.Modules.Example.Events.NoteCreatedEvent;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace DotNetCloud.Modules.Example.Tests;

/// <summary>
/// Tests for <see cref="ExampleModule"/> covering lifecycle, note CRUD, and event publishing.
/// </summary>
[TestClass]
public class ExampleModuleTests
{
    private ExampleModule _module = null!;
    private Mock<IEventBus> _mockEventBus = null!;
    private ServiceProvider _serviceProvider = null!;

    [TestInitialize]
    public void Setup()
    {
        _module = new ExampleModule();
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
            ModuleId = "dotnetcloud.example",
            Services = _serviceProvider,
            Configuration = config ?? new Dictionary<string, object>(),
            SystemCaller = new CallerContext(SystemUserId, ["system"], CallerType.System)
        };
    }

    private CallerContext CreateUserCaller()
    {
        return new CallerContext(Guid.NewGuid(), ["user"], CallerType.User);
    }

    // ---- Manifest ----

    [TestMethod]
    public void WhenCreatedThenManifestIsExampleModuleManifest()
    {
        Assert.IsNotNull(_module.Manifest);
        Assert.IsInstanceOfType<ExampleModuleManifest>(_module.Manifest);
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
    public async Task WhenInitializedThenSubscribesToEvents()
    {
        await _module.InitializeAsync(CreateContext());

        _mockEventBus.Verify(
            b => b.SubscribeAsync(It.IsAny<IEventHandler<NoteCreatedEvent>>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [TestMethod]
    public async Task WhenInitializedWithConfigThenReadsConfiguration()
    {
        var config = new Dictionary<string, object> { ["max_notes"] = 100 };

        // Should not throw
        await _module.InitializeAsync(CreateContext(config));
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

        // Should not throw
        await _module.StartAsync();
    }

    [TestMethod]
    public async Task WhenStartedWithoutInitializeThenThrowsInvalidOperationException()
    {
        await Assert.ThrowsExactlyAsync<InvalidOperationException>(
            () => _module.StartAsync());
    }

    // ---- Lifecycle: StopAsync ----

    [TestMethod]
    public async Task WhenStoppedThenUnsubscribesFromEvents()
    {
        await _module.InitializeAsync(CreateContext());
        await _module.StartAsync();

        await _module.StopAsync();

        _mockEventBus.Verify(
            b => b.UnsubscribeAsync(It.IsAny<IEventHandler<NoteCreatedEvent>>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [TestMethod]
    public async Task WhenStoppedThenModuleIsNotRunning()
    {
        await _module.InitializeAsync(CreateContext());
        await _module.StartAsync();
        await _module.StopAsync();

        await Assert.ThrowsExactlyAsync<InvalidOperationException>(
            () => _module.CreateNoteAsync("test", "content", Guid.NewGuid(), CreateUserCaller()));
    }

    // ---- Lifecycle: DisposeAsync ----

    [TestMethod]
    public async Task WhenDisposedThenClearsNotes()
    {
        await _module.InitializeAsync(CreateContext());
        await _module.StartAsync();
        await _module.CreateNoteAsync("test", "content", Guid.NewGuid(), CreateUserCaller());

        await ((IModuleLifecycle)_module).DisposeAsync();

        // After dispose + re-init + start, notes list should be empty
        var newModule = new ExampleModule();
        await newModule.InitializeAsync(CreateContext());
        await newModule.StartAsync();
        Assert.AreEqual(0, newModule.GetNotes().Count);
    }

    [TestMethod]
    public async Task WhenDisposedViaIAsyncDisposableThenSucceeds()
    {
        await _module.InitializeAsync(CreateContext());

        // Should not throw
        await ((IAsyncDisposable)_module).DisposeAsync();
    }

    // ---- Lifecycle: Full Lifecycle ----

    [TestMethod]
    public async Task WhenFullLifecycleExecutedThenAllStepsSucceed()
    {
        await _module.InitializeAsync(CreateContext());
        await _module.StartAsync();
        await _module.StopAsync();
        await ((IAsyncDisposable)_module).DisposeAsync();

        // Verify event bus interactions
        _mockEventBus.Verify(
            b => b.SubscribeAsync(It.IsAny<IEventHandler<NoteCreatedEvent>>(), It.IsAny<CancellationToken>()),
            Times.Once);
        _mockEventBus.Verify(
            b => b.UnsubscribeAsync(It.IsAny<IEventHandler<NoteCreatedEvent>>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    // ---- CreateNoteAsync ----

    [TestMethod]
    public async Task WhenNoteCreatedThenReturnsNoteWithCorrectProperties()
    {
        await _module.InitializeAsync(CreateContext());
        await _module.StartAsync();

        var userId = Guid.NewGuid();
        var note = await _module.CreateNoteAsync("My Note", "Content here", userId, CreateUserCaller());

        Assert.IsNotNull(note);
        Assert.AreEqual("My Note", note.Title);
        Assert.AreEqual("Content here", note.Content);
        Assert.AreEqual(userId, note.CreatedByUserId);
        Assert.AreNotEqual(Guid.Empty, note.Id);
    }

    [TestMethod]
    public async Task WhenNoteCreatedThenPublishesNoteCreatedEvent()
    {
        await _module.InitializeAsync(CreateContext());
        await _module.StartAsync();

        await _module.CreateNoteAsync("Test", "Body", Guid.NewGuid(), CreateUserCaller());

        _mockEventBus.Verify(
            b => b.PublishAsync(It.IsAny<NoteCreatedEvent>(), It.IsAny<CallerContext>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [TestMethod]
    public async Task WhenNoteCreatedThenEventContainsCorrectData()
    {
        NoteCreatedEvent? capturedEvent = null;
        _mockEventBus
            .Setup(b => b.PublishAsync(It.IsAny<NoteCreatedEvent>(), It.IsAny<CallerContext>(), It.IsAny<CancellationToken>()))
            .Callback<NoteCreatedEvent, CallerContext, CancellationToken>((e, _, _) => capturedEvent = e)
            .Returns(Task.CompletedTask);

        await _module.InitializeAsync(CreateContext());
        await _module.StartAsync();

        var userId = Guid.NewGuid();
        var note = await _module.CreateNoteAsync("Event Test", "Body", userId, CreateUserCaller());

        Assert.IsNotNull(capturedEvent);
        Assert.AreEqual(note.Id, capturedEvent.NoteId);
        Assert.AreEqual("Event Test", capturedEvent.Title);
        Assert.AreEqual(userId, capturedEvent.CreatedByUserId);
        Assert.AreNotEqual(Guid.Empty, capturedEvent.EventId);
    }

    [TestMethod]
    public async Task WhenNoteCreatedWhileNotRunningThenThrowsInvalidOperationException()
    {
        await Assert.ThrowsExactlyAsync<InvalidOperationException>(
            () => _module.CreateNoteAsync("Test", "Body", Guid.NewGuid(), CreateUserCaller()));
    }

    // ---- GetNotes ----

    [TestMethod]
    public async Task WhenNoNotesCreatedThenGetNotesReturnsEmptyList()
    {
        await _module.InitializeAsync(CreateContext());
        await _module.StartAsync();

        var notes = _module.GetNotes();

        Assert.IsNotNull(notes);
        Assert.AreEqual(0, notes.Count);
    }

    [TestMethod]
    public async Task WhenMultipleNotesCreatedThenGetNotesReturnsAll()
    {
        await _module.InitializeAsync(CreateContext());
        await _module.StartAsync();
        var caller = CreateUserCaller();

        await _module.CreateNoteAsync("Note 1", "Content 1", Guid.NewGuid(), caller);
        await _module.CreateNoteAsync("Note 2", "Content 2", Guid.NewGuid(), caller);
        await _module.CreateNoteAsync("Note 3", "Content 3", Guid.NewGuid(), caller);

        Assert.AreEqual(3, _module.GetNotes().Count);
    }

    [TestMethod]
    public async Task WhenGetNotesWhileNotRunningThenThrowsInvalidOperationException()
    {
        await _module.InitializeAsync(CreateContext());

        Assert.ThrowsExactly<InvalidOperationException>(() => _module.GetNotes());
    }

    // ---- GetNote ----

    [TestMethod]
    public async Task WhenGetNoteByIdThenReturnsCorrectNote()
    {
        await _module.InitializeAsync(CreateContext());
        await _module.StartAsync();

        var created = await _module.CreateNoteAsync("Find Me", "Body", Guid.NewGuid(), CreateUserCaller());
        var found = _module.GetNote(created.Id);

        Assert.IsNotNull(found);
        Assert.AreEqual(created.Id, found.Id);
        Assert.AreEqual("Find Me", found.Title);
    }

    [TestMethod]
    public async Task WhenGetNoteByNonExistentIdThenReturnsNull()
    {
        await _module.InitializeAsync(CreateContext());
        await _module.StartAsync();

        var found = _module.GetNote(Guid.NewGuid());

        Assert.IsNull(found);
    }

    [TestMethod]
    public async Task WhenGetNoteWhileNotRunningThenThrowsInvalidOperationException()
    {
        await _module.InitializeAsync(CreateContext());

        Assert.ThrowsExactly<InvalidOperationException>(() => _module.GetNote(Guid.NewGuid()));
    }
}
