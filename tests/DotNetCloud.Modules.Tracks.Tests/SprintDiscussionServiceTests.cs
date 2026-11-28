using DotNetCloud.Core.DTOs;
using DotNetCloud.Core.Services.ModuleApis;
using DotNetCloud.Modules.Tracks.Data;
using DotNetCloud.Modules.Tracks.Data.Services;
using DotNetCloud.Modules.Tracks.Models;
using DotNetCloud.Modules.Tracks.Services;
using Microsoft.Extensions.Logging;
using Moq;

namespace DotNetCloud.Modules.Tracks.Tests;

[TestClass]
public class SprintDiscussionServiceTests
{
    private TracksDbContext _db = null!;
    private SprintDiscussionService _service = null!;
    private Mock<ITracksRealtimeService> _realtimeServiceMock = null!;

    [TestInitialize]
    public void Setup()
    {
        _db = TestHelpers.CreateDb();
        _realtimeServiceMock = new Mock<ITracksRealtimeService>();
        var logger = new LoggerFactory().CreateLogger<SprintDiscussionService>();
        _service = new SprintDiscussionService(_db, _realtimeServiceMock.Object, logger);
    }

    [TestCleanup]
    public void Cleanup() => _db.Dispose();

    private async Task<Sprint> SeedSprintAsync()
    {
        var product = await TestHelpers.SeedProductAsync(_db, Guid.CreateVersion7(), Guid.CreateVersion7());
        var epic = await TestHelpers.SeedEpicAsync(_db, product.Id, Guid.CreateVersion7());
        var sprint = new Sprint
        {
            EpicId = epic.Id,
            Title = "Sprint 1",
            Status = SprintStatus.Planning,
            DurationWeeks = 2,
            PlannedOrder = 1
        };
        _db.Sprints.Add(sprint);
        await _db.SaveChangesAsync();
        return sprint;
    }

    private async Task<(ReviewSessionDto Session, ReviewSessionService Svc)> SeedReviewSessionAsync()
    {
        var product = await TestHelpers.SeedProductAsync(_db, Guid.CreateVersion7(), Guid.CreateVersion7());
        var epic = await TestHelpers.SeedEpicAsync(_db, product.Id, Guid.CreateVersion7());
        var reviewService = new ReviewSessionService(_db);
        var session = await reviewService.StartReviewSessionAsync(epic.Id, Guid.CreateVersion7(), CancellationToken.None);
        return (session, reviewService);
    }

    [TestMethod]
    public async Task SendSprintMessage_ValidContent_CreatesMessage()
    {
        var sprint = await SeedSprintAsync();
        var userId = Guid.CreateVersion7();

        var result = await _service.SendSprintMessageAsync(
            sprint.Id, userId, "Test User", "Hello world", CancellationToken.None);

        Assert.IsNotNull(result);
        Assert.AreEqual("Hello world", result.Content);
        Assert.AreEqual(userId, result.UserId);
        Assert.AreEqual("Test User", result.UserDisplayName);
        Assert.AreEqual(sprint.Id, result.SprintId);
        Assert.IsNull(result.ReviewSessionId);
    }

    [TestMethod]
    public async Task SendSprintMessage_EmptyContent_ThrowsValidationException()
    {
        var sprint = await SeedSprintAsync();
        var userId = Guid.CreateVersion7();

        try
        {
            await _service.SendSprintMessageAsync(sprint.Id, userId, "User", "", CancellationToken.None);
            Assert.Fail("Expected ValidationException was not thrown.");
        }
        catch (DotNetCloud.Core.Errors.ValidationException)
        {
            // Expected
        }
    }

    [TestMethod]
    public async Task SendSprintMessage_ContentTooLong_ThrowsValidationException()
    {
        var sprint = await SeedSprintAsync();
        var userId = Guid.CreateVersion7();
        var longContent = new string('x', 2001);

        try
        {
            await _service.SendSprintMessageAsync(sprint.Id, userId, "User", longContent, CancellationToken.None);
            Assert.Fail("Expected ValidationException was not thrown.");
        }
        catch (DotNetCloud.Core.Errors.ValidationException)
        {
            // Expected
        }
    }

    [TestMethod]
    public async Task GetSprintMessages_Paginated_ReturnsCorrectPage()
    {
        var sprint = await SeedSprintAsync();
        var userId = Guid.CreateVersion7();

        for (var i = 0; i < 10; i++)
            await _service.SendSprintMessageAsync(sprint.Id, userId, "User", $"Message {i}", CancellationToken.None);

        var result = await _service.GetSprintMessagesAsync(sprint.Id, skip: 0, take: 5, ct: CancellationToken.None);

        Assert.AreEqual(5, result.Count);
        Assert.AreEqual("Message 0", result[0].Content);
        Assert.AreEqual("Message 1", result[1].Content);
        Assert.AreEqual("Message 4", result[4].Content);
    }

    [TestMethod]
    public async Task GetSprintMessages_EmptySprint_ReturnsEmptyList()
    {
        var sprint = await SeedSprintAsync();

        var result = await _service.GetSprintMessagesAsync(sprint.Id, ct: CancellationToken.None);

        Assert.AreEqual(0, result.Count);
    }

    [TestMethod]
    public async Task SendReviewSessionMessage_ValidContent_CreatesMessage()
    {
        var (session, _) = await SeedReviewSessionAsync();
        var userId = Guid.CreateVersion7();

        var result = await _service.SendReviewSessionMessageAsync(
            session.Id, userId, "Reviewer", "Looks good", CancellationToken.None);

        Assert.IsNotNull(result);
        Assert.AreEqual("Looks good", result.Content);
        Assert.AreEqual(userId, result.UserId);
        Assert.AreEqual(session.Id, result.ReviewSessionId);
        Assert.IsNull(result.SprintId);
    }

    [TestMethod]
    public async Task GetReviewSessionMessages_OrderedByCreatedAt()
    {
        var (session, _) = await SeedReviewSessionAsync();
        var userId = Guid.CreateVersion7();

        await _service.SendReviewSessionMessageAsync(session.Id, userId, "User", "First", CancellationToken.None);
        await Task.Delay(10);
        await _service.SendReviewSessionMessageAsync(session.Id, userId, "User", "Second", CancellationToken.None);
        await Task.Delay(10);
        await _service.SendReviewSessionMessageAsync(session.Id, userId, "User", "Third", CancellationToken.None);

        var result = await _service.GetReviewSessionMessagesAsync(session.Id, ct: CancellationToken.None);

        Assert.AreEqual(3, result.Count);
        Assert.AreEqual("First", result[0].Content);
        Assert.AreEqual("Second", result[1].Content);
        Assert.AreEqual("Third", result[2].Content);
    }

    [TestMethod]
    public async Task SendSprintMessage_BroadcastsRealtimeEvent()
    {
        var sprint = await SeedSprintAsync();
        var userId = Guid.CreateVersion7();

        var result = await _service.SendSprintMessageAsync(
            sprint.Id, userId, "Test User", "Broadcast test", CancellationToken.None);

        _realtimeServiceMock.Verify(
            r => r.BroadcastSprintDiscussionMessageAsync(
                sprint.Id,
                It.Is<DotNetCloud.Core.Services.ModuleApis.SprintDiscussionDto>(d => d.Content == "Broadcast test"),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }
}
