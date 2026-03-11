using DotNetCloud.Client.Android.Chat;
using DotNetCloud.Client.Core;
using Microsoft.Extensions.Logging;
using Xunit;

namespace DotNetCloud.Client.Android.Tests.Chat;

/// <summary>
/// End-to-end tests for Android SignalR chat client connecting to real server.
/// Validates hub path, authentication, event subscriptions, and payload parsing.
/// 
/// **EXECUTION REQUIREMENTS:**
/// Before running live E2E test:
/// 1. Remove `[Fact(Skip = "...")]` annotation and replace with `[Fact]`
/// 2. Set environment variable: `DOTNETCLOUD_E2E_BEARER_TOKEN=your-valid-user-bearer-token`
/// 3. Bearer token must be from user OAuth auth-code flow (not client-credentials)
/// 4. Test user must be member of the target chat channel
/// 
/// **TRIGGER PROTOCOL (run BEFORE starting test):**
/// From another authenticated client, send a test message:
/// ```
/// POST https://mint22:15443/api/v1/chat/channels/{channelId}/messages?userId={senderUserId}
/// Authorization: Bearer {senderAccessToken}
/// { "content": "e2e-signalr-probe" }
/// ```
/// This will fire:
/// - `NewMessage` event: { channelId, message }
/// - `UnreadCountUpdated` event: { channelId, count }
/// 
/// **Expected result:**
/// Test connects, receives both events, validates payloads, and completes successfully.
/// </summary>
public sealed class SignalRChatClientE2eTests
{
    private readonly ILogger<SignalRChatClient> _logger;
    private const string ServerBaseUrl = "https://mint22:15443";
    private const int ConnectionTimeoutSeconds = 15;

    public SignalRChatClientE2eTests()
    {
        // Use console logging for E2E visibility
        var factory = LoggerFactory.Create(builder => builder.AddConsole());
        _logger = factory.CreateLogger<SignalRChatClient>();
    }

    /// <summary>
    /// Validates Android client successfully connects to server hub at /hubs/core,
    /// authenticates with bearer token, and receives properly-typed events.
    /// </summary>
    [Fact(Skip = "E2E test — set DOTNETCLOUD_E2E_BEARER_TOKEN env var and remove Skip to run live")]
    public async Task ConnectAsync_SubscribesAndReceivesEvents_Live()
    {
        // Arrange
        var client = new SignalRChatClient(_logger);
        
        var accessToken = Environment.GetEnvironmentVariable("DOTNETCLOUD_E2E_BEARER_TOKEN");
        if (string.IsNullOrWhiteSpace(accessToken))
        {
            throw new InvalidOperationException(
                "Bearer token not found. Set DOTNETCLOUD_E2E_BEARER_TOKEN environment variable to a valid user token from OAuth auth-code flow.");
        }

        var unreadUpdatesReceived = new List<ChatUnreadCountUpdatedEventArgs>();
        var newMessagesReceived = new List<ChatMessageReceivedEventArgs>();
        var connectionError = (Exception?)null;

        client.OnUnreadCountUpdated += (sender, args) =>
        {
            _logger.LogInformation("✓ UnreadCountUpdated received: ChannelId={ChannelId}, Count={Count}", args.ChannelId, args.UnreadCount);
            unreadUpdatesReceived.Add(args);
        };

        client.OnNewChatMessage += (sender, args) =>
        {
            _logger.LogInformation("✓ NewMessage received: ChannelId={ChannelId}, Preview={Preview}", args.ChannelId, args.MessagePreview);
            newMessagesReceived.Add(args);
        };

        try
        {
            // Act
            _logger.LogInformation("Connecting to {HubUrl} with bearer token...", $"{ServerBaseUrl}/hubs/core");
            await client.ConnectAsync(ServerBaseUrl, accessToken, CancellationToken.None);
            _logger.LogInformation("✓ Hub connection established");

            // Wait for server to push test events (or trigger them manually as per trigger protocol)
            _logger.LogInformation("Waiting {Seconds}s for events from server...", ConnectionTimeoutSeconds);
            await Task.Delay(TimeSpan.FromSeconds(ConnectionTimeoutSeconds));

            // Assert
            if (unreadUpdatesReceived.Count == 0 && newMessagesReceived.Count == 0)
            {
                _logger.LogWarning("⚠ No events received. Did you trigger the protocol? (See test doc comments)");
            }

            Assert.NotEmpty(unreadUpdatesReceived);
            Assert.NotEmpty(newMessagesReceived);

            var firstUnread = unreadUpdatesReceived[0];
            Assert.NotNull(firstUnread.ChannelId);
            Assert.True(firstUnread.UnreadCount >= 0);
            
            var firstMessage = newMessagesReceived[0];
            Assert.NotNull(firstMessage.ChannelId);
            Assert.NotNull(firstMessage.MessagePreview);

            _logger.LogInformation(
                "✓ TEST PASSED: Received {UnreadCount} unread updates and {MessageCount} messages",
                unreadUpdatesReceived.Count,
                newMessagesReceived.Count);
        }
        catch (Exception ex)
        {
            connectionError = ex;
            _logger.LogError(ex, "✗ Connection failed: {Message}", ex.Message);
            throw;
        }
        finally
        {
            await client.DisposeAsync();
            _logger.LogInformation("Disconnected from hub");
        }
    }

    /// <summary>
    /// Validates payload DTOs deserialize correctly from server JSON.
    /// (Unit test — no server required)
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
    /// (Unit test — no server required)
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
    /// (Unit test — no server required)
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

    /// <summary>
    /// Validates NewMessage event args mapping.
    /// (Unit test — no server required)
    /// </summary>
    [Fact]
    public void NewMessageEventArgsMapping_PreservesPayloadData()
    {
        // Arrange
        var payload = new NewMessagePayload("ch-004", "Test message content");
        var eventArgs = new ChatMessageReceivedEventArgs(payload.ChannelId, string.Empty, string.Empty, payload.Message, false);

        // Act & Assert
        Assert.Equal(payload.ChannelId, eventArgs.ChannelId);
        Assert.Equal(payload.Message, eventArgs.MessagePreview);
        Assert.Empty(eventArgs.ChannelDisplayName); // Defaults to empty
        Assert.Empty(eventArgs.SenderDisplayName); // Defaults to empty
        Assert.False(eventArgs.IsMention); // Defaults to false
    }
}
