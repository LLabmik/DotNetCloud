using DotNetCloud.Core.Authorization;
using DotNetCloud.Modules.Chat.DTOs;
using DotNetCloud.Modules.Chat.Host.Controllers;
using DotNetCloud.Modules.Chat.Models;
using DotNetCloud.Modules.Chat.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace DotNetCloud.Modules.Chat.Tests;

/// <summary>
/// Tests for deterministic exception mapping in <see cref="ChatController"/>.
/// </summary>
[TestClass]
public class ChatControllerTests
{
    private Mock<IChannelService> _channelService = null!;
    private Mock<IChannelMemberService> _memberService = null!;
    private Mock<IMessageService> _messageService = null!;
    private Mock<IReactionService> _reactionService = null!;
    private Mock<IPinService> _pinService = null!;
    private Mock<ITypingIndicatorService> _typingService = null!;
    private ChatController _controller = null!;

    [TestInitialize]
    public void Setup()
    {
        _channelService = new Mock<IChannelService>();
        _memberService = new Mock<IChannelMemberService>();
        _messageService = new Mock<IMessageService>();
        _reactionService = new Mock<IReactionService>();
        _pinService = new Mock<IPinService>();
        _typingService = new Mock<ITypingIndicatorService>();

        _controller = new ChatController(
            _channelService.Object,
            _memberService.Object,
            _messageService.Object,
            _reactionService.Object,
            _pinService.Object,
            _typingService.Object,
            NullLogger<ChatController>.Instance);
    }

    [TestMethod]
    public async Task AddReactionAsync_WhenUnauthorized_ThenReturnsForbidResult()
    {
        _reactionService
            .Setup(s => s.AddReactionAsync(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<CallerContext>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new UnauthorizedAccessException("forbidden"));

        var result = await _controller.AddReactionAsync(Guid.NewGuid(), new AddReactionDto { Emoji = "👍" }, Guid.NewGuid());

        Assert.IsInstanceOfType<ForbidResult>(result);
    }

    [TestMethod]
    public async Task PinMessageAsync_WhenUnauthorized_ThenReturnsForbidResult()
    {
        _pinService
            .Setup(s => s.PinMessageAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<CallerContext>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new UnauthorizedAccessException("forbidden"));

        var result = await _controller.PinMessageAsync(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid());

        Assert.IsInstanceOfType<ForbidResult>(result);
    }

    [TestMethod]
    public async Task RemoveMemberAsync_WhenUnauthorized_ThenReturnsForbidResult()
    {
        _memberService
            .Setup(s => s.RemoveMemberAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<CallerContext>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new UnauthorizedAccessException("forbidden"));

        var result = await _controller.RemoveMemberAsync(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid());

        Assert.IsInstanceOfType<ForbidResult>(result);
    }

    [TestMethod]
    public async Task NotifyTypingAsync_WhenInvalidArgument_ThenReturnsBadRequest()
    {
        _typingService
            .Setup(s => s.NotifyTypingAsync(It.IsAny<Guid>(), It.IsAny<CallerContext>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new ArgumentException("Channel id is required."));

        var result = await _controller.NotifyTypingAsync(Guid.NewGuid(), Guid.NewGuid());

        Assert.IsInstanceOfType<BadRequestObjectResult>(result);
    }

    [TestMethod]
    public async Task GetPinnedMessagesAsync_WhenInvalidOperation_ThenReturnsNotFound()
    {
        _pinService
            .Setup(s => s.GetPinnedMessagesAsync(It.IsAny<Guid>(), It.IsAny<CallerContext>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Channel not found."));

        var result = await _controller.GetPinnedMessagesAsync(Guid.NewGuid(), Guid.NewGuid());

        Assert.IsInstanceOfType<NotFoundObjectResult>(result);
    }
}
