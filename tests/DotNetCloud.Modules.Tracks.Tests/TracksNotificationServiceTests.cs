using DotNetCloud.Core.Capabilities;
using DotNetCloud.Core.DTOs;
using DotNetCloud.Modules.Tracks.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace DotNetCloud.Modules.Tracks.Tests;

[TestClass]
public class TracksNotificationServiceTests
{
    private Mock<INotificationService> _notificationService = null!;
    private Mock<IUserDirectory> _userDirectory = null!;
    private ITracksNotificationService _service = null!;

    [TestInitialize]
    public void Setup()
    {
        _notificationService = new Mock<INotificationService>();
        _userDirectory = new Mock<IUserDirectory>();
        _service = new TracksNotificationService(
            NullLogger<TracksNotificationService>.Instance,
            _notificationService.Object,
            _userDirectory.Object);
    }

    // ── Card Assignment ─────────────────────────────────────

    [TestMethod]
    public async Task NotifyCardAssigned_SendsToAssignedUser()
    {
        var boardId = Guid.NewGuid();
        var cardId = Guid.NewGuid();
        var assignedUserId = Guid.NewGuid();
        var assignedByUserId = Guid.NewGuid();

        await _service.NotifyCardAssignedAsync(boardId, cardId, "Test Card", assignedUserId, assignedByUserId);

        _notificationService.Verify(n => n.SendAsync(
            assignedUserId,
            It.Is<NotificationDto>(dto =>
                dto.Type == NotificationType.Update &&
                dto.Title.Contains("Test Card") &&
                dto.SourceModuleId == "dotnetcloud.tracks"),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [TestMethod]
    public async Task NotifyCardAssigned_SelfAssignment_NoNotification()
    {
        var userId = Guid.NewGuid();

        await _service.NotifyCardAssignedAsync(Guid.NewGuid(), Guid.NewGuid(), "Card", userId, userId);

        _notificationService.Verify(n => n.SendAsync(
            It.IsAny<Guid>(),
            It.IsAny<NotificationDto>(),
            It.IsAny<CancellationToken>()), Times.Never);
    }

    // ── Mentions ────────────────────────────────────────────

    [TestMethod]
    public async Task NotifyMentions_ResolvesUsernameAndSends()
    {
        var boardId = Guid.NewGuid();
        var cardId = Guid.NewGuid();
        var authorId = Guid.NewGuid();
        var mentionedUserId = Guid.NewGuid();

        _userDirectory.Setup(u => u.FindUserIdByUsernameAsync("alice", It.IsAny<CancellationToken>()))
            .ReturnsAsync(mentionedUserId);

        await _service.NotifyMentionsAsync(boardId, cardId, "Test Card", authorId, "Hey @alice check this");

        _notificationService.Verify(n => n.SendAsync(
            mentionedUserId,
            It.Is<NotificationDto>(dto =>
                dto.Type == NotificationType.Mention &&
                dto.Title.Contains("Test Card")),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [TestMethod]
    public async Task NotifyMentions_AuthorMentionsSelf_NoNotification()
    {
        var authorId = Guid.NewGuid();

        _userDirectory.Setup(u => u.FindUserIdByUsernameAsync("me", It.IsAny<CancellationToken>()))
            .ReturnsAsync(authorId);

        await _service.NotifyMentionsAsync(Guid.NewGuid(), Guid.NewGuid(), "Card", authorId, "Hey @me check this");

        _notificationService.Verify(n => n.SendAsync(
            It.IsAny<Guid>(),
            It.IsAny<NotificationDto>(),
            It.IsAny<CancellationToken>()), Times.Never);
    }

    [TestMethod]
    public async Task NotifyMentions_UnknownUsername_NoNotification()
    {
        _userDirectory.Setup(u => u.FindUserIdByUsernameAsync("unknown", It.IsAny<CancellationToken>()))
            .ReturnsAsync((Guid?)null);

        await _service.NotifyMentionsAsync(Guid.NewGuid(), Guid.NewGuid(), "Card", Guid.NewGuid(), "Hey @unknown");

        _notificationService.Verify(n => n.SendAsync(
            It.IsAny<Guid>(),
            It.IsAny<NotificationDto>(),
            It.IsAny<CancellationToken>()), Times.Never);
    }

    [TestMethod]
    public async Task NotifyMentions_NoMentions_NoNotification()
    {
        await _service.NotifyMentionsAsync(Guid.NewGuid(), Guid.NewGuid(), "Card", Guid.NewGuid(), "No mentions here");

        _userDirectory.Verify(u => u.FindUserIdByUsernameAsync(
            It.IsAny<string>(),
            It.IsAny<CancellationToken>()), Times.Never);
    }

    // ── Sprint Notifications ────────────────────────────────

    [TestMethod]
    public async Task NotifySprintStarted_SendsToAllExceptStarter()
    {
        var startedBy = Guid.NewGuid();
        var members = new List<Guid> { startedBy, Guid.NewGuid(), Guid.NewGuid() };

        await _service.NotifySprintStartedAsync(Guid.NewGuid(), "Sprint 1", startedBy, members);

        _notificationService.Verify(n => n.SendToManyAsync(
            It.Is<IEnumerable<Guid>>(ids => ids.Count() == 2 && !ids.Contains(startedBy)),
            It.Is<NotificationDto>(dto => dto.Title.Contains("Sprint 1")),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [TestMethod]
    public async Task NotifySprintCompleted_SendsToAllExceptCompleter()
    {
        var completedBy = Guid.NewGuid();
        var members = new List<Guid> { completedBy, Guid.NewGuid() };

        await _service.NotifySprintCompletedAsync(Guid.NewGuid(), "Sprint 1", completedBy, members);

        _notificationService.Verify(n => n.SendToManyAsync(
            It.Is<IEnumerable<Guid>>(ids => ids.Count() == 1 && !ids.Contains(completedBy)),
            It.Is<NotificationDto>(dto => dto.Title.Contains("completed")),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    // ── No Service (Standalone) ─────────────────────────────

    [TestMethod]
    public async Task AllMethods_NoNotificationService_NoOps()
    {
        ITracksNotificationService service = new TracksNotificationService(NullLogger<TracksNotificationService>.Instance);

        // All should complete without throwing
        await service.NotifyCardAssignedAsync(Guid.NewGuid(), Guid.NewGuid(), "Card", Guid.NewGuid(), Guid.NewGuid());
        await service.NotifyMentionsAsync(Guid.NewGuid(), Guid.NewGuid(), "Card", Guid.NewGuid(), "@alice test");
        await service.NotifySprintStartedAsync(Guid.NewGuid(), "Sprint", Guid.NewGuid(), [Guid.NewGuid()]);
        await service.NotifySprintCompletedAsync(Guid.NewGuid(), "Sprint", Guid.NewGuid(), [Guid.NewGuid()]);
    }
}
