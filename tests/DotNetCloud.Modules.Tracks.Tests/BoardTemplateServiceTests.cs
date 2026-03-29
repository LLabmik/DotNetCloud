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
public class BoardTemplateServiceTests
{
    private TracksDbContext _db = null!;
    private BoardTemplateService _service = null!;
    private CallerContext _caller;

    [TestInitialize]
    public async Task Setup()
    {
        _db = TestHelpers.CreateDb();
        _caller = TestHelpers.CreateCaller();
        var mock = new Mock<IEventBus>();
        var activityService = new ActivityService(_db, NullLogger<ActivityService>.Instance);
        var teamService = new TeamService(_db, mock.Object, NullLogger<TeamService>.Instance);
        var boardService = new BoardService(_db, mock.Object, activityService, teamService, NullLogger<BoardService>.Instance);
        var listService = new ListService(_db, boardService, activityService, NullLogger<ListService>.Instance);
        var labelService = new LabelService(_db, boardService, activityService, NullLogger<LabelService>.Instance);
        _service = new BoardTemplateService(_db, boardService, listService, labelService, NullLogger<BoardTemplateService>.Instance);
        await _service.SeedBuiltInTemplatesAsync();
    }

    [TestCleanup]
    public void Cleanup() => _db.Dispose();

    // ─── SeedBuiltInTemplates ─────────────────────────────────────────

    [TestMethod]
    public async Task SeedBuiltInTemplates_RunTwice_NoDuplicates()
    {
        await _service.SeedBuiltInTemplatesAsync();

        var templates = await _service.ListTemplatesAsync(_caller);
        var builtInCount = templates.Count(t => t.IsBuiltIn);

        Assert.AreEqual(4, builtInCount); // Kanban, Scrum, Bug Tracking, Personal TODO
    }

    // ─── ListTemplates ────────────────────────────────────────────────

    [TestMethod]
    public async Task ListTemplates_ReturnsBuiltIns()
    {
        var result = await _service.ListTemplatesAsync(_caller);

        Assert.IsTrue(result.Count >= 4);
        Assert.IsTrue(result.Any(t => t.Name == "Kanban"));
        Assert.IsTrue(result.Any(t => t.Name == "Scrum"));
    }

    [TestMethod]
    public async Task ListTemplates_IncludesUserCreatedTemplates()
    {
        var board = await TestHelpers.SeedBoardAsync(_db, _caller.UserId, "My Board");
        var dto = new SaveBoardAsTemplateDto { Name = "Custom Template", Description = "My template", Category = "custom" };
        await _service.SaveBoardAsTemplateAsync(board.Id, dto, _caller);

        var result = await _service.ListTemplatesAsync(_caller);

        Assert.IsTrue(result.Any(t => t.Name == "Custom Template"));
    }

    // ─── GetTemplate ──────────────────────────────────────────────────

    [TestMethod]
    public async Task GetTemplate_ValidId_ReturnsTemplate()
    {
        var templates = await _service.ListTemplatesAsync(_caller);
        var kanban = templates.First(t => t.Name == "Kanban");

        var result = await _service.GetTemplateAsync(kanban.Id, _caller);

        Assert.IsNotNull(result);
        Assert.AreEqual("Kanban", result.Name);
    }

    [TestMethod]
    public async Task GetTemplate_NotFound_ReturnsNull()
    {
        var result = await _service.GetTemplateAsync(Guid.NewGuid(), _caller);

        Assert.IsNull(result);
    }

    // ─── CreateBoardFromTemplate ──────────────────────────────────────

    [TestMethod]
    public async Task CreateBoardFromTemplate_KanbanTemplate_CreatesBoard()
    {
        var templates = await _service.ListTemplatesAsync(_caller);
        var kanban = templates.First(t => t.Name == "Kanban");
        var dto = new CreateBoardFromTemplateDto { Title = "My Kanban" };

        var board = await _service.CreateBoardFromTemplateAsync(kanban.Id, dto, _caller);

        Assert.IsNotNull(board);
        Assert.AreEqual("My Kanban", board.Title);
    }

    // ─── SaveBoardAsTemplate ──────────────────────────────────────────

    [TestMethod]
    public async Task SaveBoardAsTemplate_ValidBoard_CreatesTemplate()
    {
        var board = await TestHelpers.SeedBoardAsync(_db, _caller.UserId, "Project Board");
        var dto = new SaveBoardAsTemplateDto { Name = "Project Starter", Description = "A project board", Category = "project" };

        var result = await _service.SaveBoardAsTemplateAsync(board.Id, dto, _caller);

        Assert.IsNotNull(result);
        Assert.AreEqual("Project Starter", result.Name);
        Assert.IsFalse(result.IsBuiltIn);
    }

    // ─── DeleteTemplate ───────────────────────────────────────────────

    [TestMethod]
    public async Task DeleteTemplate_UserCreated_Succeeds()
    {
        var board = await TestHelpers.SeedBoardAsync(_db, _caller.UserId, "Temp Board");
        var dto = new SaveBoardAsTemplateDto { Name = "Temp Template", Description = "", Category = "other" };
        var template = await _service.SaveBoardAsTemplateAsync(board.Id, dto, _caller);

        await _service.DeleteTemplateAsync(template.Id, _caller);

        // GetTemplateAsync returns null for deleted/non-existent templates
        var result = await _service.GetTemplateAsync(template.Id, _caller);
        Assert.IsNull(result);
    }

    [TestMethod]
    public async Task DeleteTemplate_BuiltIn_Throws()
    {
        var templates = await _service.ListTemplatesAsync(_caller);
        var kanban = templates.First(t => t.IsBuiltIn);

        await Assert.ThrowsExactlyAsync<ValidationException>(
            () => _service.DeleteTemplateAsync(kanban.Id, _caller));
    }
}
