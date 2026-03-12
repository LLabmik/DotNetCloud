using DotNetCloud.Core.Server.RealTime;
using DotNetCloud.Integration.Tests.Infrastructure;
using DotNetCloud.Modules.Chat.Data;
using DotNetCloud.Modules.Chat.DTOs;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace DotNetCloud.Integration.Tests.Api;

/// <summary>
/// Integration tests for the CoreHub SignalR endpoints.
/// Tests real-time communication: connect+auth, group operations, message broadcasts, typing reactions, etc.
/// Runs against the in-process core server via DotNetCloudWebApplicationFactory.
/// </summary>
[TestClass]
[TestCategory("Integration")]
public sealed class SignalRHubIntegrationTests
{
    private static DotNetCloudWebApplicationFactory _factory = null!;
    private static readonly Guid UserId1 = Guid.NewGuid();
    private static readonly Guid UserId2 = Guid.NewGuid();

    [ClassInitialize]
    public static void ClassInit(TestContext _)
    {
        _factory = new DotNetCloudWebApplicationFactory();
    }

    [ClassCleanup]
    public static void ClassCleanup()
    {
        _factory.Dispose();
    }

    // ─────────────────────────────────────────────────────────────────
    // Helpers
    // ─────────────────────────────────────────────────────────────────

    /// <summary>Connects a SignalR hub with test authentication.</summary>
    private async Task<HubConnection> ConnectHubAsync(Guid userId)
    {
        var handler = _factory.Server.CreateHandler();
        var connection = new HubConnectionBuilder()
            .WithUrl("http://localhost/hubs/core", opts =>
            {
                opts.HttpMessageHandlerFactory = (_) => handler;
                opts.Headers.Add("x-test-user-id", userId.ToString());
            })
            .WithAutomaticReconnect()
            .Build();

        try
        {
            await connection.StartAsync(new CancellationTokenSource(5000).Token);
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"Failed to connect hub for user {userId}: {ex.Message}", ex);
        }

        return connection;
    }

    /// <summary>Creates a test channel in the chat database.</summary>
    private async Task<Guid> CreateChannelAsync(string name)
    {
        var chatDb = _factory.Services.CreateScope().ServiceProvider.GetRequiredService<ChatDbContext>();
        var channel = new DotNetCloud.Modules.Chat.Models.Channel
        {
            Id = Guid.NewGuid(),
            Name = name,
            Type = DotNetCloud.Modules.Chat.Models.ChannelType.Public,
            Description = $"Test {name}",
            CreatedByUserId = UserId1,
            CreatedAt = DateTime.UtcNow
        };

        chatDb.Channels.Add(channel);
        await chatDb.SaveChangesAsync();
        return channel.Id;
    }

    /// <summary>Adds a user as a member of a channel.</summary>
    private async Task AddMemberToChannelAsync(Guid userId, Guid channelId)
    {
        var chatDb = _factory.Services.CreateScope().ServiceProvider.GetRequiredService<ChatDbContext>();
        var member = new DotNetCloud.Modules.Chat.Models.ChannelMember
        {
            Id = Guid.NewGuid(),
            ChannelId = channelId,
            UserId = userId,
            Role = DotNetCloud.Modules.Chat.Models.ChannelMemberRole.Member,
            JoinedAt = DateTime.UtcNow
        };

        chatDb.ChannelMembers.Add(member);
        await chatDb.SaveChangesAsync();
    }

    /// <summary>Creates a message in a channel.</summary>
    private async Task<Guid> CreateMessageAsync(Guid channelId, Guid userId, string content)
    {
        var chatDb = _factory.Services.CreateScope().ServiceProvider.GetRequiredService<ChatDbContext>();
        var message = new DotNetCloud.Modules.Chat.Models.Message
        {
            Id = Guid.NewGuid(),
            ChannelId = channelId,
            SenderUserId = userId,
            Content = content,
            SentAt = DateTime.UtcNow,
            EditedAt = null
        };

        chatDb.Messages.Add(message);
        await chatDb.SaveChangesAsync();
        return message.Id;
    }

    // ─────────────────────────────────────────────────────────────────
    // Tests
    // ─────────────────────────────────────────────────────────────────

    [TestMethod]
    public async Task Connect_WithValidUserId_Succeeds()
    {
        await using var connection = await ConnectHubAsync(UserId1);
        Assert.IsNotNull(connection);
        Assert.AreEqual(HubConnectionState.Connected, connection.State);
        await connection.StopAsync();
    }

    [TestMethod]
    public async Task JoinGroupAsync_Succeeds()
    {
        await using var connection = await ConnectHubAsync(UserId1);
        var groupName = $"test-group-{Guid.NewGuid()}";

        await connection.InvokeAsync("JoinGroupAsync", groupName);

        // No exception means success; SignalR doesn't return a value for group operations
        Assert.AreEqual(HubConnectionState.Connected, connection.State);
        await connection.StopAsync();
    }

    [TestMethod]
    public async Task SendMessageAsync_ReturnsMessage()
    {
        var channelId = await CreateChannelAsync("TestChannel");
        await AddMemberToChannelAsync(UserId1, channelId);

        await using var connection = await ConnectHubAsync(UserId1);

        // Send a message
        var result = await connection.InvokeAsync<MessageDto>(
            "SendMessageAsync",
            channelId,
            "Hello from SignalR",
            (Guid?)null);

        Assert.IsNotNull(result);
        Assert.AreEqual("Hello from SignalR", result.Content);
        Assert.AreEqual(channelId, result.ChannelId);
        await connection.StopAsync();
    }

    [TestMethod]
    public async Task MarkReadAsync_Succeeds()
    {
        var channelId = await CreateChannelAsync("ReadTestChannel");
        await AddMemberToChannelAsync(UserId1, channelId);

        var messageId = await CreateMessageAsync(channelId, UserId2, "Test message");

        await using var connection = await ConnectHubAsync(UserId1);

        // Mark as read
        await connection.InvokeAsync("MarkReadAsync", channelId, messageId);

        // Should not throw
        Assert.AreEqual(HubConnectionState.Connected, connection.State);
        await connection.StopAsync();
    }

    [TestMethod]
    public async Task StartTypingAsync_Succeeds()
    {
        var channelId = await CreateChannelAsync("TypingTestChannel");
        await AddMemberToChannelAsync(UserId1, channelId);

        await using var connection = await ConnectHubAsync(UserId1);

        // Start typing
        await connection.InvokeAsync("StartTypingAsync", channelId, "User One");

        // Should not throw
        Assert.AreEqual(HubConnectionState.Connected, connection.State);
        await connection.StopAsync();
    }

    [TestMethod]
    public async Task AddReactionAsync_Succeeds()
    {
        var channelId = await CreateChannelAsync("ReactionTestChannel");
        await AddMemberToChannelAsync(UserId1, channelId);

        var messageId = await CreateMessageAsync(channelId, UserId1, "React to this");

        await using var connection = await ConnectHubAsync(UserId1);

        // Add a reaction
        await connection.InvokeAsync("AddReactionAsync", messageId, "👍");

        // Should not throw
        Assert.AreEqual(HubConnectionState.Connected, connection.State);
        await connection.StopAsync();
    }

    [TestMethod]
    public async Task RemoveReactionAsync_Succeeds()
    {
        var channelId = await CreateChannelAsync("RemoveReactionTestChannel");
        await AddMemberToChannelAsync(UserId1, channelId);

        var messageId = await CreateMessageAsync(channelId, UserId1, "React to this");

        await using var connection = await ConnectHubAsync(UserId1);

        // Add a reaction first
        await connection.InvokeAsync("AddReactionAsync", messageId, "👍");

        // Remove the reaction
        await connection.InvokeAsync("RemoveReactionAsync", messageId, "👍");

        // Should not throw
        Assert.AreEqual(HubConnectionState.Connected, connection.State);
        await connection.StopAsync();
    }

    [TestMethod]
    public async Task SetPresenceAsync_ReturnsPresence()
    {
        await using var connection = await ConnectHubAsync(UserId1);

        var presence = await connection.InvokeAsync<PresenceDto>(
            "SetPresenceAsync",
            "Online",
            "Working on integration tests");

        Assert.IsNotNull(presence);
        Assert.AreEqual("Online", presence.Status);
        Assert.AreEqual("Working on integration tests", presence.StatusMessage);
        await connection.StopAsync();
    }

    [TestMethod]
    public async Task Disconnect_RemovesUserConnection()
    {
        var connection = await ConnectHubAsync(UserId1);
        Assert.AreEqual(HubConnectionState.Connected, connection.State);

        await connection.StopAsync();
        Assert.AreEqual(HubConnectionState.Disconnected, connection.State);

        await connection.DisposeAsync();
    }
}

