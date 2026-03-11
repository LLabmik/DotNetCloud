using DotNetCloud.Client.Android.Chat;
using DotNetCloud.Client.Core;
using Microsoft.Extensions.Logging;
using Xunit;

namespace DotNetCloud.Client.Android.Tests.Chat;

/// <summary>
/// End-to-end tests for Android SignalR chat client connecting to real server.
/// Validates hub path, authentication, event subscriptions, and payload parsing.
/// </summary>
public sealed class SignalRChatClientE2eTests
{
    private readonly ILogger<SignalRChatClient> _logger;

    public SignalRChatClientE2eTests()
    {
        // Use console logging for E2E visibility
        var factory = LoggerFactory.Create(builder => builder.AddConsole());
        _logger = factory.CreateLogger<SignalRChatClient>();
    }

    /// <summary>
    /// Validates Android client successfully connects to server hub at /hubs/core
    /// and receives properly-typed UnreadCountUpdated and NewMessage events.
    /// 
    /// **Prerequisites:**
    /// - Server running at https://mint22:15443/
    /// - OAuth token available (or generated via server's OIDC flow)
    /// - Chat module deployed and live
    /// </summary>
    [Fact(Skip = "E2E test — requires live server. Run manually: https://mint22:15443/")]
    public async Task ConnectAsync_SubscribesAndReceivesEvents()
    {
        // Arrange
        var client = new SignalRChatClient(_logger);
        var serverBaseUrl = "https://mint22:15443";
        
        // TODO: Replace with valid bearer token from server OAuth flow
        var accessToken = "PLACEHOLDER_BEARER_TOKEN";

        var unreadUpdatesReceived = new List<ChatUnreadCountUpdatedEventArgs>();
        var newMessagesReceived = new List<ChatMessageReceivedEventArgs>();

        client.OnUnreadCountUpdated += (sender, args) => unreadUpdatesReceived.Add(args);
        client.OnNewChatMessage += (sender, args) => newMessagesReceived.Add(args);

        try
        {
            // Act
            await client.ConnectAsync(serverBaseUrl, accessToken, CancellationToken.None);
            _logger.LogInformation("Connected to hub at {HubUrl}", $"{serverBaseUrl}/hubs/core");

            // Wait for server to push test events (or trigger them manually from desktop client)
            await Task.Delay(TimeSpan.FromSeconds(10));

            // Assert
            Assert.NotEmpty(unreadUpdatesReceived);
            Assert.NotEmpty(newMessagesReceived);

            var firstUnread = unreadUpdatesReceived[0];
            Assert.NotNull(firstUnread.ChannelId);
            Assert.True(firstUnread.UnreadCount >= 0);
            
            var firstMessage = newMessagesReceived[0];
            Assert.NotNull(firstMessage.ChannelId);
            Assert.NotNull(firstMessage.MessagePreview);

            _logger.LogInformation(
                "✓ Received {UnreadCount} unread updates and {MessageCount} messages",
                unreadUpdatesReceived.Count,
                newMessagesReceived.Count);
        }
        finally
        {
            await client.DisposeAsync();
        }
    }

    /// <summary>
    /// Validates payload DTOs deserialize correctly from server JSON.
    /// </summary>
    [Fact]
    public void UnreadCountUpdatedPayload_DeserializesFromJson()
    {
        // Arrange
        var json = """{"channelId":"ch-001","count":5}""";
        var options = new System.Text.Json.JsonSerializerOptions { PropertyNameCaseInsensitive = true };

        // Act
        var payload = System.Text.Json.JsonSerializer.Deserialize<UnreadCountUpdatedPayload>(json, options);

        // Assert
        Assert.NotNull(payload);
        Assert.Equal("ch-001", payload.ChannelId);
        Assert.Equal(5, payload.Count);
    }

    /// <summary>
    /// Validates NewMessage payload deserializes correctly from server JSON.
    /// </summary>
    [Fact]
    public void NewMessagePayload_DeserializesFromJson()
    {
        // Arrange
        var json = """{"channelId":"ch-002","message":"Hello from server"}""";
        var options = new System.Text.Json.JsonSerializerOptions { PropertyNameCaseInsensitive = true };

        // Act
        var payload = System.Text.Json.JsonSerializer.Deserialize<NewMessagePayload>(json, options);

        // Assert
        Assert.NotNull(payload);
        Assert.Equal("ch-002", payload.ChannelId);
        Assert.Equal("Hello from server", payload.Message);
    }

    /// <summary>
    /// Validates event handler mapping preserves payload data correctly.
    /// </summary>
    [Fact]
    public void EventArgsMapping_PreservesPayloadData()
    {
        // Arrange
        var payload = new UnreadCountUpdatedPayload("ch-003", 12);
        var eventArgs = new ChatUnreadCountUpdatedEventArgs(payload.ChannelId, payload.Count, false);

        // Act & Assert
        Assert.Equal(payload.ChannelId, eventArgs.ChannelId);
        Assert.Equal(payload.Count, eventArgs.UnreadCount);
        Assert.False(eventArgs.HasMention); // Defaults to false
    }
}
