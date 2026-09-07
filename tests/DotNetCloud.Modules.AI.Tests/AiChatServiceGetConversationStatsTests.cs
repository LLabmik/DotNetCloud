using DotNetCloud.Core.AI;
using DotNetCloud.Core.Authorization;
using DotNetCloud.Core.Capabilities;
using DotNetCloud.Modules.AI.Data;
using DotNetCloud.Modules.AI.Data.Services;
using DotNetCloud.Modules.AI.Models;
using DotNetCloud.Modules.AI.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace DotNetCloud.Modules.AI.Tests;

/// <summary>
/// Tests for <see cref="AiChatService.GetConversationStatsAsync"/>.
/// </summary>
[TestClass]
public class AiChatServiceGetConversationStatsTests
{
    private AiDbContext _db = null!;
    private AiChatService _service = null!;
    private AiCompletionQueue _queue = null!;
    private Guid _userId = Guid.CreateVersion7();

    [TestInitialize]
    public void Setup()
    {
        var options = new DbContextOptionsBuilder<AiDbContext>()
            .UseInMemoryDatabase(Guid.CreateVersion7().ToString())
            .Options;
        _db = new AiDbContext(options);
        var settingsMock = new Mock<IAiSettingsProvider>();
        settingsMock.Setup(s => s.GetDefaultModelAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync("gpt-oss:20b");
        _queue = new AiCompletionQueue();
        _service = new AiChatService(
            _db,
            new Mock<IOllamaClient>().Object,
            _queue,
            settingsMock.Object,
            Mock.Of<IAuditLogger>(),
            NullLogger<AiChatService>.Instance);
    }

    [TestCleanup]
    public void Cleanup()
    {
        _queue.Dispose();
        _db.Dispose();
    }

    private async Task SeedConversationAsync(Guid ownerId, DateTime updatedAt, bool isDeleted = false)
    {
        _db.Conversations.Add(new Conversation
        {
            Id = Guid.CreateVersion7(),
            OwnerId = ownerId,
            Title = "Conversation",
            Model = "gpt-oss:20b",
            CreatedAt = updatedAt,
            UpdatedAt = updatedAt,
            IsDeleted = isDeleted
        });
        await _db.SaveChangesAsync();
    }

    [TestMethod]
    public async Task GetConversationStats_TotalConversations_CountsOnlyGivenUsersConversations()
    {
        await SeedConversationAsync(_userId, DateTime.UtcNow.AddDays(-1));
        await SeedConversationAsync(_userId, DateTime.UtcNow);
        await SeedConversationAsync(Guid.CreateVersion7(), DateTime.UtcNow);

        var stats = await _service.GetConversationStatsAsync(_userId);

        Assert.AreEqual(2, stats.TotalConversations);
    }

    [TestMethod]
    public async Task GetConversationStats_LastActivityAt_ReturnsMaxUpdatedAtScopedToUser()
    {
        var otherUser = Guid.CreateVersion7();
        var older = DateTime.UtcNow.AddDays(-3);
        var newer = DateTime.UtcNow.AddDays(-1);
        var otherUserNewer = DateTime.UtcNow;
        await SeedConversationAsync(_userId, older);
        await SeedConversationAsync(_userId, newer);
        await SeedConversationAsync(otherUser, otherUserNewer); // newer, but not this user's

        var stats = await _service.GetConversationStatsAsync(_userId);

        Assert.AreEqual(newer, stats.LastActivityAt);
    }

    [TestMethod]
    public async Task GetConversationStats_NoConversations_ReturnsZeroAndNullLastActivity()
    {
        var stats = await _service.GetConversationStatsAsync(_userId);

        Assert.AreEqual(0, stats.TotalConversations);
        Assert.IsNull(stats.LastActivityAt);
    }

    [TestMethod]
    public async Task GetConversationStats_SoftDeletedConversations_AreExcluded()
    {
        var live = DateTime.UtcNow.AddDays(-2);
        var deletedButNewer = DateTime.UtcNow.AddDays(-1);
        await SeedConversationAsync(_userId, live);
        await SeedConversationAsync(_userId, deletedButNewer, isDeleted: true);

        var stats = await _service.GetConversationStatsAsync(_userId);

        Assert.AreEqual(1, stats.TotalConversations);
        Assert.AreEqual(live, stats.LastActivityAt);
    }
}
