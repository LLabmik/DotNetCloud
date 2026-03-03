using DotNetCloud.Core.Server.RealTime;
using Microsoft.AspNetCore.SignalR;

namespace DotNetCloud.Core.Server.Tests.RealTime;

/// <summary>
/// Stub implementation of <see cref="IHubContext{THub}"/> for testing.
/// Records all calls for verification without requiring Moq to proxy internal types.
/// </summary>
internal sealed class StubHubContext : IHubContext<CoreHub>
{
    public StubHubClients StubClients { get; } = new();
    public StubGroupManager StubGroups { get; } = new();

    public IHubClients Clients => StubClients;
    public IGroupManager Groups => StubGroups;
}

internal sealed class StubHubClients : IHubClients
{
    public List<(string Method, IReadOnlyList<string> ConnectionIds)> ClientsCalls { get; } = [];
    public List<string> GroupCalls { get; } = [];

    private readonly StubClientProxy _proxy = new();

    public StubClientProxy LastProxy => _proxy;

    public IClientProxy All => _proxy;
    public IClientProxy AllExcept(IReadOnlyList<string> excludedConnectionIds) => _proxy;

    public IClientProxy Client(string connectionId)
    {
        ClientsCalls.Add((connectionId, [connectionId]));
        return _proxy;
    }

    public IClientProxy Clients(IReadOnlyList<string> connectionIds)
    {
        ClientsCalls.Add(("Clients", connectionIds));
        return _proxy;
    }

    public IClientProxy Group(string groupName)
    {
        GroupCalls.Add(groupName);
        return _proxy;
    }

    public IClientProxy GroupExcept(string groupName, IReadOnlyList<string> excludedConnectionIds) => _proxy;
    public IClientProxy Groups(IReadOnlyList<string> groupNames) => _proxy;
    public IClientProxy User(string userId) => _proxy;
    public IClientProxy Users(IReadOnlyList<string> userIds) => _proxy;
}

internal sealed class StubClientProxy : IClientProxy
{
    public List<(string Method, object?[]? Args)> Invocations { get; } = [];

    public Task SendCoreAsync(string method, object?[]? args, CancellationToken cancellationToken = default)
    {
        Invocations.Add((method, args));
        return Task.CompletedTask;
    }
}

internal sealed class StubGroupManager : IGroupManager
{
    public List<(string ConnectionId, string GroupName, string Action)> Operations { get; } = [];

    public Task AddToGroupAsync(string connectionId, string groupName, CancellationToken cancellationToken = default)
    {
        Operations.Add((connectionId, groupName, "Add"));
        return Task.CompletedTask;
    }

    public Task RemoveFromGroupAsync(string connectionId, string groupName, CancellationToken cancellationToken = default)
    {
        Operations.Add((connectionId, groupName, "Remove"));
        return Task.CompletedTask;
    }
}
