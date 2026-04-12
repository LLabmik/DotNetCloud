using DotNetCloud.Core.Authorization;
using DotNetCloud.Core.Events;
using DotNetCloud.Core.Events.Search;
using DotNetCloud.Modules.Music.Data;
using DotNetCloud.Modules.Music.Data.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace DotNetCloud.Modules.Music.Tests;

/// <summary>
/// Tests that <see cref="TrackService"/> publishes <see cref="SearchIndexRequestEvent"/>
/// on delete operations.
/// </summary>
[TestClass]
public class TrackServiceSearchIndexTests
{
    private MusicDbContext _db = null!;
    private TrackService _service = null!;
    private Mock<IEventBus> _eventBusMock = null!;
    private CallerContext _caller = null!;

    [TestInitialize]
    public void Setup()
    {
        _db = TestHelpers.CreateDb();
        _eventBusMock = new Mock<IEventBus>();
        _service = new TrackService(_db, _eventBusMock.Object, NullLogger<TrackService>.Instance);
        _caller = TestHelpers.CreateCaller();
    }

    [TestCleanup]
    public void Cleanup() => _db.Dispose();

    [TestMethod]
    public async Task DeleteTrack_PublishesSearchIndexRequestEvent_WithRemoveAction()
    {
        var (_, _, track) = await TestHelpers.SeedCompleteTrackAsync(_db, ownerId: _caller.UserId);

        await _service.DeleteTrackAsync(track.Id, _caller);

        _eventBusMock.Verify(
            eb => eb.PublishAsync(
                It.Is<SearchIndexRequestEvent>(e =>
                    e.ModuleId == "music" &&
                    e.EntityId == track.Id.ToString() &&
                    e.Action == SearchIndexAction.Remove),
                _caller,
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [TestMethod]
    public async Task DeleteTrack_SearchIndexEvent_HasValidProperties()
    {
        SearchIndexRequestEvent? capturedEvent = null;
        _eventBusMock
            .Setup(eb => eb.PublishAsync(It.IsAny<SearchIndexRequestEvent>(), It.IsAny<CallerContext>(), It.IsAny<CancellationToken>()))
            .Callback<object, CallerContext, CancellationToken>((e, _, _) => capturedEvent = e as SearchIndexRequestEvent);

        var (_, _, track) = await TestHelpers.SeedCompleteTrackAsync(_db, ownerId: _caller.UserId);

        await _service.DeleteTrackAsync(track.Id, _caller);

        Assert.IsNotNull(capturedEvent);
        Assert.AreNotEqual(Guid.Empty, capturedEvent.EventId);
        Assert.IsTrue(capturedEvent.CreatedAt > DateTime.UtcNow.AddMinutes(-1));
        Assert.AreEqual("music", capturedEvent.ModuleId);
    }
}
