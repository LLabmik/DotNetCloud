using DotNetCloud.Client.Core;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace DotNetCloud.Client.Android.Tests.Chat;

/// <summary>
/// End-to-end tests for Android SignalR chat client connecting to real server.
/// Validates hub path, authentication, event subscriptions, and payload parsing.
/// 
/// **EXECUTION REQUIREMENTS:**
/// Before running live E2E test:
/// 1. Set environment variable: `DOTNETCLOUD_E2E_BEARER_TOKEN=your-valid-user-bearer-token`
/// 2. Optionally set `DOTNETCLOUD_E2E_BASE_URL` (default: https://mint22:15443)
/// 3. Optionally set `DOTNETCLOUD_E2E_EXPECTED_CHANNEL_ID` to assert channel IDs
/// 4. Bearer token must be from user OAuth auth-code flow (not client-credentials)
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
[TestClass]
public sealed class SignalRChatClientE2eTests
{
    /// <summary>
    /// Validates Android client successfully connects to server hub at /hubs/core,
    /// authenticates with bearer token, and receives properly-typed events.
    /// </summary>
    [TestMethod]
    public async Task ConnectAsync_SubscribesAndReceivesEvents_Live()
    {
        var bearerToken = Environment.GetEnvironmentVariable("DOTNETCLOUD_E2E_BEARER_TOKEN");
        if (string.IsNullOrWhiteSpace(bearerToken))
        {
            Assert.Inconclusive("DOTNETCLOUD_E2E_BEARER_TOKEN is not set.");
            return;
        }

        var baseUrl = Environment.GetEnvironmentVariable("DOTNETCLOUD_E2E_BASE_URL");
        if (string.IsNullOrWhiteSpace(baseUrl))
        {
            baseUrl = "https://mint22:15443";
        }

        var expectedChannelId = Environment.GetEnvironmentVariable("DOTNETCLOUD_E2E_EXPECTED_CHANNEL_ID");
        var hubUrl = $"{baseUrl.TrimEnd('/')}/hubs/core";

        var unreadEventSeen = new TaskCompletionSource<ChatUnreadCountUpdatedEventArgs>(TaskCreationOptions.RunContinuationsAsynchronously);
        var messageEventSeen = new TaskCompletionSource<ChatMessageReceivedEventArgs>(TaskCreationOptions.RunContinuationsAsynchronously);

        await using var connection = new HubConnectionBuilder()
            .WithUrl(hubUrl, options => options.AccessTokenProvider = () => Task.FromResult<string?>(bearerToken))
            .WithAutomaticReconnect()
            .Build();

        connection.On<string, int>("UnreadCountUpdated", (channelId, count) =>
        {
            unreadEventSeen.TrySetResult(new ChatUnreadCountUpdatedEventArgs(channelId, count, false));
        });

        connection.On<string, string>("NewMessage", (channelId, message) =>
        {
            messageEventSeen.TrySetResult(new ChatMessageReceivedEventArgs(channelId, string.Empty, string.Empty, message, false));
        });

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(90));
        await connection.StartAsync(cts.Token);

        var unreadTask = unreadEventSeen.Task.WaitAsync(cts.Token);
        var messageTask = messageEventSeen.Task.WaitAsync(cts.Token);

        await Task.WhenAll(unreadTask, messageTask);

        var unread = await unreadTask;
        var message = await messageTask;

        if (!string.IsNullOrWhiteSpace(expectedChannelId))
        {
            Assert.AreEqual(expectedChannelId, unread.ChannelId);
            Assert.AreEqual(expectedChannelId, message.ChannelId);
        }

        Assert.IsTrue(unread.UnreadCount >= 0, "UnreadCount must be >= 0.");
        Assert.IsFalse(string.IsNullOrWhiteSpace(message.MessagePreview), "NewMessage payload did not include message content.");

        await connection.DisposeAsync();
    }

    /// <summary>
    /// Validates payload DTOs deserialize correctly from server JSON.
    /// (Unit test — no server required)
    /// </summary>
    [TestMethod]
    public void UnreadCountUpdatedPayload_DeserializesFromJson()
    {
        // Arrange
        var json = """{"channelId":"ch-001","count":5}""";
        var options = new System.Text.Json.JsonSerializerOptions { PropertyNameCaseInsensitive = true };

        // Act
        var payload = System.Text.Json.JsonSerializer.Deserialize<UnreadCountUpdatedPayloadDto>(json, options);

        // Assert
        Assert.IsNotNull(payload);
        Assert.AreEqual("ch-001", payload.ChannelId);
        Assert.AreEqual(5, payload.Count);
    }

    /// <summary>
    /// Validates NewMessage payload deserializes correctly from server JSON.
    /// (Unit test — no server required)
    /// </summary>
    [TestMethod]
    public void NewMessagePayload_DeserializesFromJson()
    {
        // Arrange
        var json = """{"channelId":"ch-002","message":"Hello from server"}""";
        var options = new System.Text.Json.JsonSerializerOptions { PropertyNameCaseInsensitive = true };

        // Act
        var payload = System.Text.Json.JsonSerializer.Deserialize<NewMessagePayloadDto>(json, options);

        // Assert
        Assert.IsNotNull(payload);
        Assert.AreEqual("ch-002", payload.ChannelId);
        Assert.AreEqual("Hello from server", payload.Message);
    }

    /// <summary>
    /// Validates event handler mapping preserves payload data correctly.
    /// (Unit test — no server required)
    /// </summary>
    [TestMethod]
    public void EventArgsMapping_PreservesPayloadData()
    {
        // Arrange
        var payload = new UnreadCountUpdatedPayloadDto("ch-003", 12);
        var eventArgs = new ChatUnreadCountUpdatedEventArgs(payload.ChannelId, payload.Count, false);

        // Act & Assert
        Assert.AreEqual(payload.ChannelId, eventArgs.ChannelId);
        Assert.AreEqual(payload.Count, eventArgs.UnreadCount);
        Assert.IsFalse(eventArgs.HasMention); // Defaults to false
    }

    /// <summary>
    /// Validates NewMessage event args mapping.
    /// (Unit test — no server required)
    /// </summary>
    [TestMethod]
    public void NewMessageEventArgsMapping_PreservesPayloadData()
    {
        // Arrange
        var payload = new NewMessagePayloadDto("ch-004", "Test message content");
        var eventArgs = new ChatMessageReceivedEventArgs(payload.ChannelId, string.Empty, string.Empty, payload.Message, false);

        // Act & Assert
        Assert.AreEqual(payload.ChannelId, eventArgs.ChannelId);
        Assert.AreEqual(payload.Message, eventArgs.MessagePreview);
        Assert.AreEqual(string.Empty, eventArgs.ChannelDisplayName); // Defaults to empty
        Assert.AreEqual(string.Empty, eventArgs.SenderDisplayName); // Defaults to empty
        Assert.IsFalse(eventArgs.IsMention); // Defaults to false
    }

    private sealed record UnreadCountUpdatedPayloadDto(string ChannelId, int Count);

    private sealed record NewMessagePayloadDto(string ChannelId, string Message);
}
