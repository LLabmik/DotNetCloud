using DotNetCloud.Core.Authorization;
using DotNetCloud.Core.DTOs;
using DotNetCloud.Core.Errors;
using DotNetCloud.Core.Events;
using DotNetCloud.Modules.Tracks.Data;
using DotNetCloud.Modules.Tracks.Data.Services;
using DotNetCloud.Modules.Tracks.Models;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace DotNetCloud.Modules.Tracks.Tests;

[TestClass]
public class CardTemplateServiceTests
{
    private TracksDbContext _db = null!;
    private CardTemplateService _service = null!;
    private Mock<IEventBus> _eventBusMock = null!;
    private CallerContext _caller;
    private Board _board = null!;
    private BoardList _list = null!;
    private Card _card = null!;

    [TestInitialize]
    public async Task Setup()
    {
        _db = TestHelpers.CreateDb();
        _caller = TestHelpers.CreateCaller();
        _eventBusMock = new Mock<IEventBus>();
        var activityService = new ActivityService(_db, NullLogger<ActivityService>.Instance);
        var teamService = new TeamService(_db, _eventBusMock.Object, NullLogger<TeamService>.Instance);
        var boardService = new BoardService(_db, _eventBusMock.Object, activityService, teamService, NullLogger<BoardService>.Instance);
        var cardService = new CardService(_db, boardService, activityService, _eventBusMock.Object, NullLogger<CardService>.Instance);
        _service = new CardTemplateService(_db, boardService, cardService, NullLogger<CardTemplateService>.Instance);
        _board = await TestHelpers.SeedBoardAsync(_db, _caller.UserId);
        _list = await TestHelpers.SeedListAsync(_db, _board.Id);
        _card = await TestHelpers.SeedCardAsync(_db, _list.Id, _caller.UserId, "Source Card");
    }

    [TestCleanup]
    public void Cleanup() => _db.Dispose();

    // ─── SaveCardAsTemplate ───────────────────────────────────────────

    [TestMethod]
    public async Task SaveCardAsTemplate_ValidCard_CreatesTemplate()
    {
        var dto = new SaveCardAsTemplateDto { Name = "API Bug Template" };

        var result = await _service.SaveCardAsTemplateAsync(_card.Id, dto, _caller);

        Assert.IsNotNull(result);
        Assert.AreEqual("API Bug Template", result.Name);
        Assert.AreEqual(_board.Id, result.BoardId);
    }

    [TestMethod]
    public async Task SaveCardAsTemplate_CardNotFound_Throws()
    {
        await Assert.ThrowsExactlyAsync<ValidationException>(
            () => _service.SaveCardAsTemplateAsync(Guid.NewGuid(), new SaveCardAsTemplateDto { Name = "X" }, _caller));
    }

    [TestMethod]
    public async Task SaveCardAsTemplate_NonMember_Throws()
    {
        var outsider = TestHelpers.CreateCaller();

        await Assert.ThrowsExactlyAsync<ValidationException>(
            () => _service.SaveCardAsTemplateAsync(_card.Id, new SaveCardAsTemplateDto { Name = "X" }, outsider));
    }

    // ─── ListTemplates ────────────────────────────────────────────────

    [TestMethod]
    public async Task ListTemplates_EmptyBoard_ReturnsEmpty()
    {
        var result = await _service.ListTemplatesAsync(_board.Id, _caller);

        Assert.AreEqual(0, result.Count);
    }

    [TestMethod]
    public async Task ListTemplates_AfterSave_ReturnsTemplate()
    {
        await _service.SaveCardAsTemplateAsync(_card.Id, new SaveCardAsTemplateDto { Name = "My Template" }, _caller);

        var result = await _service.ListTemplatesAsync(_board.Id, _caller);

        Assert.AreEqual(1, result.Count);
        Assert.AreEqual("My Template", result[0].Name);
    }

    // ─── GetTemplate ──────────────────────────────────────────────────

    [TestMethod]
    public async Task GetTemplate_NotFound_ReturnsNull()
    {
        var result = await _service.GetTemplateAsync(Guid.NewGuid(), _caller);

        Assert.IsNull(result);
    }

    [TestMethod]
    public async Task GetTemplate_Valid_ReturnsTemplate()
    {
        var saved = await _service.SaveCardAsTemplateAsync(_card.Id, new SaveCardAsTemplateDto { Name = "Template X" }, _caller);

        var result = await _service.GetTemplateAsync(saved.Id, _caller);

        Assert.AreEqual("Template X", result.Name);
    }

    // ─── CreateCardFromTemplate ───────────────────────────────────────

    [TestMethod]
    public async Task CreateCardFromTemplate_ValidTemplate_CreatesCard()
    {
        _card.Title = "Bug Report";
        await _db.SaveChangesAsync();
        var saved = await _service.SaveCardAsTemplateAsync(_card.Id, new SaveCardAsTemplateDto { Name = "Bug Template" }, _caller);
        var dto = new CreateCardFromTemplateDto { Title = "New Bug Card" };

        var result = await _service.CreateCardFromTemplateAsync(saved.Id, _list.Id, dto, _caller);

        Assert.IsNotNull(result);
        Assert.AreEqual("New Bug Card", result.Title);
    }

    // ─── DeleteTemplate ───────────────────────────────────────────────

    [TestMethod]
    public async Task DeleteTemplate_AsCreator_Succeeds()
    {
        var saved = await _service.SaveCardAsTemplateAsync(_card.Id, new SaveCardAsTemplateDto { Name = "Temp" }, _caller);

        await _service.DeleteTemplateAsync(saved.Id, _caller);

        // GetTemplateAsync returns null for deleted/non-existent templates
        var result = await _service.GetTemplateAsync(saved.Id, _caller);
        Assert.IsNull(result);
    }

    [TestMethod]
    public async Task DeleteTemplate_AsNonOwner_Throws()
    {
        var saved = await _service.SaveCardAsTemplateAsync(_card.Id, new SaveCardAsTemplateDto { Name = "Temp" }, _caller);
        var member = TestHelpers.CreateCaller();
        await TestHelpers.AddMemberAsync(_db, _board.Id, member.UserId, BoardMemberRole.Member);

        await Assert.ThrowsExactlyAsync<ValidationException>(
            () => _service.DeleteTemplateAsync(saved.Id, member));
    }
}
