using DotNetCloud.Core.Authorization;
using DotNetCloud.Modules.Chat.Data.Services;

namespace DotNetCloud.Modules.Chat.Tests;

/// <summary>
/// Tests for <see cref="TypingIndicatorService"/>.
/// </summary>
[TestClass]
public class TypingIndicatorServiceTests
{
    private TypingIndicatorService _service = null!;
    private CallerContext _caller = null!;
    private readonly Guid _channelId = Guid.NewGuid();

    [TestInitialize]
    public void Setup()
    {
        _service = new TypingIndicatorService();
        _caller = new CallerContext(Guid.NewGuid(), ["user"], CallerType.User);
    }

    [TestMethod]
    public async Task WhenNotifyTypingThenUserAppearsInList()
    {
        await _service.NotifyTypingAsync(_channelId, _caller);

        var users = await _service.GetTypingUsersAsync(_channelId);

        Assert.AreEqual(1, users.Count);
        Assert.AreEqual(_caller.UserId, users[0].UserId);
    }

    [TestMethod]
    public async Task WhenNoTypingThenEmptyListIsReturned()
    {
        var users = await _service.GetTypingUsersAsync(_channelId);

        Assert.AreEqual(0, users.Count);
    }

    [TestMethod]
    public async Task WhenMultipleUsersTypeThenAllAppear()
    {
        var caller2 = new CallerContext(Guid.NewGuid(), ["user"], CallerType.User);

        await _service.NotifyTypingAsync(_channelId, _caller);
        await _service.NotifyTypingAsync(_channelId, caller2);

        var users = await _service.GetTypingUsersAsync(_channelId);

        Assert.AreEqual(2, users.Count);
    }

    [TestMethod]
    public async Task WhenDifferentChannelsThenTypingIsSeparate()
    {
        var otherChannel = Guid.NewGuid();

        await _service.NotifyTypingAsync(_channelId, _caller);

        var users1 = await _service.GetTypingUsersAsync(_channelId);
        var users2 = await _service.GetTypingUsersAsync(otherChannel);

        Assert.AreEqual(1, users1.Count);
        Assert.AreEqual(0, users2.Count);
    }

    [TestMethod]
    public async Task WhenNotifyTypingWithEmptyChannelThenThrowsArgumentException()
    {
        await Assert.ThrowsAsync<ArgumentException>(() =>
            _service.NotifyTypingAsync(Guid.Empty, _caller));
    }

    [TestMethod]
    public async Task WhenTypingEntryExpiresThenUserIsRemoved()
    {
        await _service.NotifyTypingAsync(_channelId, _caller);
        await Task.Delay(TimeSpan.FromSeconds(6));

        var users = await _service.GetTypingUsersAsync(_channelId);

        Assert.AreEqual(0, users.Count);
    }

    [TestMethod]
    public void WhenCleanupExpiredThenNoError()
    {
        _service.CleanupExpired();
        // Should not throw on empty state
    }
}
