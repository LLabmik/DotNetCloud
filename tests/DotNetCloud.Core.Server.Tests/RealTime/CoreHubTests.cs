using System.Security.Claims;
using DotNetCloud.Core.Server.RealTime;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace DotNetCloud.Core.Server.Tests.RealTime;

[TestClass]
public class CoreHubTests
{
    [TestMethod]
    public async Task WhenUserHasTrackedGroupsThenOnConnectedAddsConnectionToEachGroup()
    {
        var userId = Guid.NewGuid();
        var tracker = new UserConnectionTracker();
        tracker.AddGroupMembership(userId, "chat:channel-a");
        tracker.AddGroupMembership(userId, "chat:channel-b");

        var presence = new PresenceService(tracker, NullLogger<PresenceService>.Instance);
        var hub = new CoreHub(tracker, presence, NullLogger<CoreHub>.Instance);

        var othersProxy = new Mock<IClientProxy>();
        othersProxy
            .Setup(p => p.SendCoreAsync(It.IsAny<string>(), It.IsAny<object?[]>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var clients = new Mock<IHubCallerClients>();
        clients.SetupGet(c => c.Others).Returns(othersProxy.Object);

        var groups = new StubGroupManager();

        hub.Context = new TestHubCallerContext(userId, "conn-1");
        hub.Clients = clients.Object;
        hub.Groups = groups;

        await hub.OnConnectedAsync();

        Assert.IsTrue(groups.Operations.Any(o => o.ConnectionId == "conn-1" && o.GroupName == "chat:channel-a" && o.Action == "Add"));
        Assert.IsTrue(groups.Operations.Any(o => o.ConnectionId == "conn-1" && o.GroupName == "chat:channel-b" && o.Action == "Add"));
    }
}

internal sealed class TestHubCallerContext : HubCallerContext
{
    private readonly ClaimsPrincipal _user;
    private readonly IDictionary<object, object?> _items;
    private readonly IFeatureCollection _features;

    public TestHubCallerContext(Guid userId, string connectionId)
    {
        ConnectionId = connectionId;
        _user = new ClaimsPrincipal(new ClaimsIdentity(
        [
            new Claim(ClaimTypes.NameIdentifier, userId.ToString())
        ],
        "Test"));
        _items = new Dictionary<object, object?>();
        _features = new FeatureCollection();
    }

    public override string ConnectionId { get; }

    public override string? UserIdentifier => _user.FindFirstValue(ClaimTypes.NameIdentifier);

    public override ClaimsPrincipal? User => _user;

    public override IDictionary<object, object?> Items => _items;

    public override IFeatureCollection Features => _features;

    public override CancellationToken ConnectionAborted => CancellationToken.None;

    public override void Abort()
    {
    }
}
