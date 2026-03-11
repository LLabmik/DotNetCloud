using DotNetCloud.Client.Core;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace DotNetCloud.Client.Android.Tests.Chat;

/// <summary>
/// End-to-end tests for Android SignalR chat client connecting to real server.
/// Validates hub path, authentication, event subscriptions, and payload parsing.
///
/// **EXECUTION REQUIREMENTS:**
/// 1. Set environment variable: `DOTNETCLOUD_E2E_BEARER_TOKEN=your-valid-user-bearer-token`
/// 2. Optionally set `DOTNETCLOUD_E2E_BASE_URL` (default: https://mint22:15443)
/// 3. Set `DOTNETCLOUD_E2E_CHANNEL_ID` to the GUID of a channel the token user is a member of
/// 4. Bearer token must be from user OAuth auth-code flow (not client-credentials)
///
/// The test is fully self-contained: it connects, joins the channel group,
/// sends a message via the hub, marks it read, and asserts both events fire.
/// </summary>
[TestClass]
public sealed class SignalRChatClientE2eTests
{
    /// <summary>
    /// Validates Android client successfully connects to server hub at /hubs/core,
    /// authenticates with bearer token, joins a channel group, sends a message,
    /// and receives the NewMessage + UnreadCountUpdated events.
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

        var channelIdStr = Environment.GetEnvironmentVariable("DOTNETCLOUD_E2E_CHANNEL_ID");
        if (string.IsNullOrWhiteSpace(channelIdStr) || !Guid.TryParse(channelIdStr, out var channelId))
        {
            Assert.Inconclusive("DOTNETCLOUD_E2E_CHANNEL_ID is not set or is not a valid GUID.");
            return;
        }

        var baseUrl = Environment.GetEnvironmentVariable("DOTNETCLOUD_E2E_BASE_URL");
        if (string.IsNullOrWhiteSpace(baseUrl))
        {
            baseUrl = "https://mint22:15443";
        }

        var hubUrl = $"{baseUrl.TrimEnd('/')}/hubs/core";
        var channelGroup = $"chat-channel-{channelId}";

        // Event completions — server sends single-object payloads
        var messageEventSeen = new TaskCompletionSource<NewMessageBroadcastPayload>(TaskCreationOptions.RunContinuationsAsynchronously);
        var unreadEventSeen = new TaskCompletionSource<UnreadCountBroadcastPayload>(TaskCreationOptions.RunContinuationsAsynchronously);

        await using var connection = new HubConnectionBuilder()
            .WithUrl(hubUrl, options =>
            {
                options.AccessTokenProvider = () => Task.FromResult<string?>(bearerToken);
                // Accept self-signed / dev certificates (test-only, matches curl -k)
                options.HttpMessageHandlerFactory = handler =>
                {
                    if (handler is HttpClientHandler clientHandler)
                    {
                        clientHandler.ServerCertificateCustomValidationCallback =
                            HttpClientHandler.DangerousAcceptAnyServerCertificateValidator;
                    }
                    return handler;
                };
            })
            .WithAutomaticReconnect()
            .Build();

        // Server broadcasts single anonymous objects: { channelId, message } and { channelId, count }
        connection.On<NewMessageBroadcastPayload>("NewMessage", payload =>
        {
            messageEventSeen.TrySetResult(payload);
        });

        connection.On<UnreadCountBroadcastPayload>("UnreadCountUpdated", payload =>
        {
            unreadEventSeen.TrySetResult(payload);
        });

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(60));

        // 1. Connect
        await connection.StartAsync(cts.Token);

        // 2. Join channel group so we receive NewMessage broadcasts
        await connection.InvokeAsync("JoinGroupAsync", channelGroup, cts.Token);

        // 3. Send a message via the hub — triggers NewMessage broadcast to the group
        var sentMessage = await connection.InvokeAsync<JsonElement>(
            "SendMessageAsync", channelId, "e2e-signalr-probe", (Guid?)null, cts.Token);

        var sentMessageId = sentMessage.GetProperty("id").GetGuid();

        // 4. Wait for NewMessage
        var receivedMessage = await messageEventSeen.Task.WaitAsync(cts.Token);

        // 5. Mark as read — triggers UnreadCountUpdated to the user
        await connection.InvokeAsync("MarkReadAsync", channelId, sentMessageId, cts.Token);

        // 6. Wait for UnreadCountUpdated
        var receivedUnread = await unreadEventSeen.Task.WaitAsync(cts.Token);

        // Assertions
        Assert.AreEqual(channelId.ToString(), receivedMessage.ChannelId.ToString(),
            "NewMessage channelId must match.");
        Assert.IsNotNull(receivedMessage.Message,
            "NewMessage must include a message object.");

        Assert.AreEqual(channelId.ToString(), receivedUnread.ChannelId.ToString(),
            "UnreadCountUpdated channelId must match.");
        Assert.IsTrue(receivedUnread.Count >= 0,
            "UnreadCount must be >= 0.");
    }

    /// <summary>Server broadcast payload for NewMessage: { channelId, message }.</summary>
    private sealed class NewMessageBroadcastPayload
    {
        [JsonPropertyName("channelId")]
        public Guid ChannelId { get; set; }

        [JsonPropertyName("message")]
        public JsonElement? Message { get; set; }
    }

    /// <summary>Server broadcast payload for UnreadCountUpdated: { channelId, count }.</summary>
    private sealed class UnreadCountBroadcastPayload
    {
        [JsonPropertyName("channelId")]
        public Guid ChannelId { get; set; }

        [JsonPropertyName("count")]
        public int Count { get; set; }
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
