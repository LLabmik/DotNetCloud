using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using DotNetCloud.Integration.Tests.Infrastructure;

namespace DotNetCloud.Integration.Tests.Api;

/// <summary>
/// Integration tests for the Chat module REST API (channel, message, member,
/// reaction, pin, typing, announcement, attachment, and push endpoints).
/// Runs against the in-memory Chat.Host via <see cref="ChatHostWebApplicationFactory"/>.
/// </summary>
[TestClass]
public sealed class ChatRestApiIntegrationTests
{
    private static ChatHostWebApplicationFactory _factory = null!;
    private static HttpClient _client = null!;
    private static HttpClient _clientB = null!;

    private static readonly Guid UserA = Guid.Parse("00000000-0000-0000-0000-000000000001");
    private static readonly Guid UserB = Guid.Parse("00000000-0000-0000-0000-000000000002");

    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    [ClassInitialize]
    public static void ClassInit(TestContext _)
    {
        _factory = new ChatHostWebApplicationFactory();
        _client = _factory.CreateAuthenticatedApiClient(UserA);
        _clientB = _factory.CreateAuthenticatedApiClient(UserB);
    }

    [ClassCleanup]
    public static void ClassCleanup()
    {
        _clientB.Dispose();
        _client.Dispose();
        _factory.Dispose();
    }

    // ── Helpers ──────────────────────────────────────────────────────

    /// <summary>Creates a public channel and returns its ID.</summary>
    private static async Task<Guid> CreateChannelAsync(string name, Guid? userId = null)
    {
        var client = (userId.HasValue && userId.Value == UserB) ? _clientB : _client;
        var body = new { name, type = "Public", description = $"Test channel {name}" };
        var response = await client.PostAsJsonAsync("/api/v1/chat/channels", body);
        response.EnsureSuccessStatusCode();

        var doc = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync());
        return doc.RootElement.GetProperty("data").GetProperty("id").GetGuid();
    }

    /// <summary>Sends a message and returns its ID.</summary>
    private static async Task<Guid> SendMessageAsync(Guid channelId, string content, Guid? userId = null)
    {
        var client = (userId.HasValue && userId.Value == UserB) ? _clientB : _client;
        var body = new { content };
        var response = await client.PostAsJsonAsync($"/api/v1/chat/channels/{channelId}/messages", body);
        response.EnsureSuccessStatusCode();

        var doc = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync());
        return doc.RootElement.GetProperty("data").GetProperty("id").GetGuid();
    }

    /// <summary>Reads the <c>data</c> element from a success envelope.</summary>
    private static async Task<JsonElement> DataAsync(HttpResponseMessage response)
    {
        var doc = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync());
        Assert.IsTrue(doc.RootElement.GetProperty("success").GetBoolean());
        return doc.RootElement.GetProperty("data");
    }

    /// <summary>Reads the <c>error</c> element from an error envelope.</summary>
    private static async Task<JsonElement> ErrorAsync(HttpResponseMessage response)
    {
        var doc = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync());
        Assert.IsFalse(doc.RootElement.GetProperty("success").GetBoolean());
        return doc.RootElement.GetProperty("error");
    }

    // ══════════════════════════════════════════════════════════════════
    //  Auth Enforcement
    // ══════════════════════════════════════════════════════════════════

    [TestMethod]
    public async Task Unauthenticated_Request_Returns401()
    {
        // Create a bare client with NO x-test-user-id header
        using var unauthClient = _factory.CreateClient(new Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
        });

        var response = await unauthClient.GetAsync("/api/v1/chat/channels");

        Assert.AreEqual(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    // ══════════════════════════════════════════════════════════════════
    //  Channel CRUD
    // ══════════════════════════════════════════════════════════════════

    [TestMethod]
    public async Task CreateChannel_Returns201_WithEnvelope()
    {
        var body = new { name = "int-create-ch", type = "Public", description = "Integration test channel" };
        var response = await _client.PostAsJsonAsync($"/api/v1/chat/channels", body);

        Assert.AreEqual(HttpStatusCode.Created, response.StatusCode);
        var data = await DataAsync(response);
        Assert.AreEqual("int-create-ch", data.GetProperty("name").GetString());
        Assert.AreEqual("Public", data.GetProperty("type").GetString());
    }

    [TestMethod]
    public async Task CreateChannel_DuplicateName_ReturnsConflict()
    {
        var body = new { name = "int-dup-ch", type = "Public" };
        await _client.PostAsJsonAsync($"/api/v1/chat/channels", body);

        var duplicate = await _client.PostAsJsonAsync($"/api/v1/chat/channels", body);

        Assert.AreEqual(HttpStatusCode.Conflict, duplicate.StatusCode);
        var error = await ErrorAsync(duplicate);
        Assert.AreEqual("DUPLICATE_CHANNEL_NAME", error.GetProperty("code").GetString());
    }

    [TestMethod]
    public async Task ListChannels_ReturnsUserChannels()
    {
        var channelId = await CreateChannelAsync("int-list-ch");

        var response = await _client.GetAsync($"/api/v1/chat/channels");

        Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
        var data = await DataAsync(response);
        Assert.IsTrue(data.GetArrayLength() > 0, "Expected at least one channel");
    }

    [TestMethod]
    public async Task GetChannel_ReturnsChannel()
    {
        var channelId = await CreateChannelAsync("int-get-ch");

        var response = await _client.GetAsync($"/api/v1/chat/channels/{channelId}");

        Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
        var data = await DataAsync(response);
        Assert.AreEqual(channelId, data.GetProperty("id").GetGuid());
    }

    [TestMethod]
    public async Task GetChannel_NonExistent_Returns404()
    {
        var response = await _client.GetAsync($"/api/v1/chat/channels/{Guid.NewGuid()}");

        Assert.AreEqual(HttpStatusCode.NotFound, response.StatusCode);
    }

    [TestMethod]
    public async Task UpdateChannel_ReturnsUpdatedChannel()
    {
        var channelId = await CreateChannelAsync("int-upd-ch");
        var body = new { name = "int-upd-ch-new", description = "Updated" };

        var response = await _client.PutAsJsonAsync($"/api/v1/chat/channels/{channelId}", body);

        Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
        var data = await DataAsync(response);
        Assert.AreEqual("int-upd-ch-new", data.GetProperty("name").GetString());
    }

    [TestMethod]
    public async Task DeleteChannel_ReturnsOk()
    {
        var channelId = await CreateChannelAsync("int-del-ch");

        var response = await _client.DeleteAsync($"/api/v1/chat/channels/{channelId}");

        Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
        var data = await DataAsync(response);
        Assert.IsTrue(data.GetProperty("deleted").GetBoolean());
    }

    [TestMethod]
    public async Task ArchiveChannel_ReturnsOk()
    {
        var channelId = await CreateChannelAsync("int-arch-ch");

        var response = await _client.PostAsync($"/api/v1/chat/channels/{channelId}/archive", null);

        Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
        var data = await DataAsync(response);
        Assert.IsTrue(data.GetProperty("archived").GetBoolean());
    }

    [TestMethod]
    public async Task GetOrCreateDm_ReturnsChannel()
    {
        var response = await _client.PostAsync($"/api/v1/chat/channels/dm/{UserB}", null);

        Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
        var data = await DataAsync(response);
        Assert.AreEqual("DirectMessage", data.GetProperty("type").GetString());
    }

    // ══════════════════════════════════════════════════════════════════
    //  Member Management
    // ══════════════════════════════════════════════════════════════════

    [TestMethod]
    public async Task AddMember_ReturnsOk()
    {
        var channelId = await CreateChannelAsync("int-member-add");
        var body = new { userId = UserB };

        var response = await _client.PostAsJsonAsync($"/api/v1/chat/channels/{channelId}/members", body);

        Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
        var data = await DataAsync(response);
        Assert.IsTrue(data.GetProperty("added").GetBoolean());
    }

    [TestMethod]
    public async Task ListMembers_ReturnsChannelMembers()
    {
        var channelId = await CreateChannelAsync("int-member-list");
        var body = new { userId = UserB };
        await _client.PostAsJsonAsync($"/api/v1/chat/channels/{channelId}/members", body);

        var response = await _client.GetAsync($"/api/v1/chat/channels/{channelId}/members");

        Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
        var data = await DataAsync(response);
        Assert.IsTrue(data.GetArrayLength() >= 2, "Expected at least creator + added member");
    }

    [TestMethod]
    public async Task UpdateMemberRole_ReturnsOk()
    {
        var channelId = await CreateChannelAsync("int-role-upd");
        await _client.PostAsJsonAsync($"/api/v1/chat/channels/{channelId}/members", new { userId = UserB });
        var body = new { role = "Admin" };

        var response = await _client.PutAsJsonAsync($"/api/v1/chat/channels/{channelId}/members/{UserB}/role", body);

        Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
    }

    [TestMethod]
    public async Task RemoveMember_ReturnsOk()
    {
        var channelId = await CreateChannelAsync("int-member-rm");
        await _client.PostAsJsonAsync($"/api/v1/chat/channels/{channelId}/members", new { userId = UserB });

        var response = await _client.DeleteAsync($"/api/v1/chat/channels/{channelId}/members/{UserB}");

        Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
        var data = await DataAsync(response);
        Assert.IsTrue(data.GetProperty("removed").GetBoolean());
    }

    [TestMethod]
    public async Task UpdateNotificationPreference_ReturnsOk()
    {
        var channelId = await CreateChannelAsync("int-notif-pref");
        var body = new { preference = "Mentions" };

        var response = await _client.PutAsJsonAsync($"/api/v1/chat/channels/{channelId}/notifications", body);

        Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
    }

    [TestMethod]
    public async Task GetUnreadCounts_ReturnsOk()
    {
        var response = await _client.GetAsync($"/api/v1/chat/unread");

        Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
    }

    // ══════════════════════════════════════════════════════════════════
    //  Message CRUD
    // ══════════════════════════════════════════════════════════════════

    [TestMethod]
    public async Task SendMessage_ReturnsMessage()
    {
        var channelId = await CreateChannelAsync("int-msg-send");
        var body = new { content = "Hello, integration tests!" };

        var response = await _client.PostAsJsonAsync($"/api/v1/chat/channels/{channelId}/messages", body);

        Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
        var data = await DataAsync(response);
        Assert.AreEqual("Hello, integration tests!", data.GetProperty("content").GetString());
    }

    [TestMethod]
    public async Task GetMessages_ReturnsPaginatedList()
    {
        var channelId = await CreateChannelAsync("int-msg-list");
        await SendMessageAsync(channelId, "msg1");
        await SendMessageAsync(channelId, "msg2");

        var response = await _client.GetAsync($"/api/v1/chat/channels/{channelId}/messages?page=1&pageSize=10");

        Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
        var doc = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync());
        Assert.IsTrue(doc.RootElement.GetProperty("success").GetBoolean());
        Assert.IsTrue(doc.RootElement.GetProperty("data").GetArrayLength() >= 2);
        Assert.IsTrue(doc.RootElement.TryGetProperty("pagination", out _));
    }

    [TestMethod]
    public async Task GetMessage_ReturnsSingleMessage()
    {
        var channelId = await CreateChannelAsync("int-msg-get");
        var messageId = await SendMessageAsync(channelId, "find me");

        var response = await _client.GetAsync($"/api/v1/chat/channels/{channelId}/messages/{messageId}");

        Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
        var data = await DataAsync(response);
        Assert.AreEqual("find me", data.GetProperty("content").GetString());
    }

    [TestMethod]
    public async Task EditMessage_ReturnsEditedMessage()
    {
        var channelId = await CreateChannelAsync("int-msg-edit");
        var messageId = await SendMessageAsync(channelId, "original");
        var body = new { content = "edited" };

        var response = await _client.PutAsJsonAsync($"/api/v1/chat/channels/{channelId}/messages/{messageId}", body);

        Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
        var data = await DataAsync(response);
        Assert.AreEqual("edited", data.GetProperty("content").GetString());
        Assert.IsTrue(data.GetProperty("isEdited").GetBoolean());
    }

    [TestMethod]
    public async Task DeleteMessage_ReturnsOk()
    {
        var channelId = await CreateChannelAsync("int-msg-del");
        var messageId = await SendMessageAsync(channelId, "delete me");

        var response = await _client.DeleteAsync($"/api/v1/chat/channels/{channelId}/messages/{messageId}");

        Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
        var data = await DataAsync(response);
        Assert.IsTrue(data.GetProperty("deleted").GetBoolean());
    }

    [TestMethod]
    public async Task DeleteMessage_NonExistent_Returns404()
    {
        var channelId = await CreateChannelAsync("int-msg-del-404");

        var response = await _client.DeleteAsync($"/api/v1/chat/channels/{channelId}/messages/{Guid.NewGuid()}");

        Assert.AreEqual(HttpStatusCode.NotFound, response.StatusCode);
    }

    [TestMethod]
    public async Task SearchMessages_ReturnsMatchingMessages()
    {
        var channelId = await CreateChannelAsync("int-msg-search");
        await SendMessageAsync(channelId, "The quick brown fox integration");
        await SendMessageAsync(channelId, "Another message");

        var response = await _client.GetAsync($"/api/v1/chat/channels/{channelId}/messages/search?q=fox");

        Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
        var doc = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync());
        Assert.IsTrue(doc.RootElement.GetProperty("success").GetBoolean());
    }

    [TestMethod]
    public async Task SearchMessages_EmptyQuery_Returns400()
    {
        var channelId = await CreateChannelAsync("int-msg-search-empty");

        var response = await _client.GetAsync($"/api/v1/chat/channels/{channelId}/messages/search?q=");

        Assert.AreEqual(HttpStatusCode.BadRequest, response.StatusCode);
    }

    // ══════════════════════════════════════════════════════════════════
    //  Reactions
    // ══════════════════════════════════════════════════════════════════

    [TestMethod]
    public async Task AddReaction_ReturnsOk()
    {
        var channelId = await CreateChannelAsync("int-react-add");
        var messageId = await SendMessageAsync(channelId, "react to me");
        var body = new { emoji = "👍" };

        var response = await _client.PostAsJsonAsync($"/api/v1/chat/messages/{messageId}/reactions", body);

        Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
    }

    [TestMethod]
    public async Task GetReactions_ReturnsReactionList()
    {
        var channelId = await CreateChannelAsync("int-react-get");
        var messageId = await SendMessageAsync(channelId, "react to me too");
        await _client.PostAsJsonAsync($"/api/v1/chat/messages/{messageId}/reactions", new { emoji = "🎉" });

        var response = await _client.GetAsync($"/api/v1/chat/messages/{messageId}/reactions");

        Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
        var data = await DataAsync(response);
        Assert.IsTrue(data.GetArrayLength() > 0, "Expected at least one reaction");
    }

    [TestMethod]
    public async Task RemoveReaction_ReturnsOk()
    {
        var channelId = await CreateChannelAsync("int-react-rm");
        var messageId = await SendMessageAsync(channelId, "react then remove");
        await _client.PostAsJsonAsync($"/api/v1/chat/messages/{messageId}/reactions", new { emoji = "✅" });

        var response = await _client.DeleteAsync($"/api/v1/chat/messages/{messageId}/reactions/✅");

        Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
    }

    // ══════════════════════════════════════════════════════════════════
    //  Pins
    // ══════════════════════════════════════════════════════════════════

    [TestMethod]
    public async Task PinMessage_ReturnsOk()
    {
        var channelId = await CreateChannelAsync("int-pin-add");
        var messageId = await SendMessageAsync(channelId, "pin this");

        var response = await _client.PostAsync($"/api/v1/chat/channels/{channelId}/pins/{messageId}", null);

        Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
    }

    [TestMethod]
    public async Task GetPinnedMessages_ReturnsList()
    {
        var channelId = await CreateChannelAsync("int-pin-list");
        var messageId = await SendMessageAsync(channelId, "pin me too");
        await _client.PostAsync($"/api/v1/chat/channels/{channelId}/pins/{messageId}", null);

        var response = await _client.GetAsync($"/api/v1/chat/channels/{channelId}/pins");

        Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
        var data = await DataAsync(response);
        Assert.IsTrue(data.GetArrayLength() > 0, "Expected at least one pinned message");
    }

    [TestMethod]
    public async Task UnpinMessage_ReturnsOk()
    {
        var channelId = await CreateChannelAsync("int-pin-rm");
        var messageId = await SendMessageAsync(channelId, "pin then unpin");
        await _client.PostAsync($"/api/v1/chat/channels/{channelId}/pins/{messageId}", null);

        var response = await _client.DeleteAsync($"/api/v1/chat/channels/{channelId}/pins/{messageId}");

        Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
    }

    // ══════════════════════════════════════════════════════════════════
    //  Typing Indicators
    // ══════════════════════════════════════════════════════════════════

    [TestMethod]
    public async Task NotifyTyping_ReturnsOk()
    {
        var channelId = await CreateChannelAsync("int-typing");

        var response = await _client.PostAsync($"/api/v1/chat/channels/{channelId}/typing", null);

        Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
    }

    [TestMethod]
    public async Task GetTypingUsers_ReturnsOk()
    {
        var channelId = await CreateChannelAsync("int-typing-get");

        var response = await _client.GetAsync($"/api/v1/chat/channels/{channelId}/typing");

        Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
    }

    // ══════════════════════════════════════════════════════════════════
    //  File Attachments
    // ══════════════════════════════════════════════════════════════════

    [TestMethod]
    public async Task AddAttachment_ReturnsOk()
    {
        var channelId = await CreateChannelAsync("int-attach");
        var messageId = await SendMessageAsync(channelId, "attach here");
        var body = new { fileName = "test.png", mimeType = "image/png", fileSize = 1024L };

        var response = await _client.PostAsJsonAsync($"/api/v1/chat/channels/{channelId}/messages/{messageId}/attachments", body);

        Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
    }

    [TestMethod]
    public async Task GetChannelFiles_ReturnsList()
    {
        var channelId = await CreateChannelAsync("int-files-list");
        var messageId = await SendMessageAsync(channelId, "has file");
        await _client.PostAsJsonAsync(
            $"/api/v1/chat/channels/{channelId}/messages/{messageId}/attachments",
            new { fileName = "doc.pdf", mimeType = "application/pdf", fileSize = 2048L });

        var response = await _client.GetAsync($"/api/v1/chat/channels/{channelId}/files");

        Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
    }

    // ══════════════════════════════════════════════════════════════════
    //  Announcements
    // ══════════════════════════════════════════════════════════════════

    [TestMethod]
    public async Task CreateAnnouncement_Returns201()
    {
        var body = new { title = "int-announce", content = "Integration announcement", priority = "Normal" };

        var response = await _client.PostAsJsonAsync($"/api/v1/announcements", body);

        Assert.AreEqual(HttpStatusCode.Created, response.StatusCode);
        var data = await DataAsync(response);
        Assert.AreEqual("int-announce", data.GetProperty("title").GetString());
    }

    [TestMethod]
    public async Task ListAnnouncements_ReturnsOk()
    {
        await _client.PostAsJsonAsync($"/api/v1/announcements",
            new { title = "int-list-announce", content = "Listed", priority = "Normal" });

        var response = await _client.GetAsync($"/api/v1/announcements");

        Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
    }

    [TestMethod]
    public async Task GetAnnouncement_NonExistent_Returns404()
    {
        var response = await _client.GetAsync($"/api/v1/announcements/{Guid.NewGuid()}");

        Assert.AreEqual(HttpStatusCode.NotFound, response.StatusCode);
    }

    [TestMethod]
    public async Task UpdateAnnouncement_ReturnsOk()
    {
        var createBody = new { title = "int-upd-announce", content = "Before update", priority = "Normal" };
        var createResponse = await _client.PostAsJsonAsync($"/api/v1/announcements", createBody);
        var id = (await DataAsync(createResponse)).GetProperty("id").GetGuid();

        var updateBody = new { title = "int-upd-announce-new", content = "After update", priority = "Normal" };
        var response = await _client.PutAsJsonAsync($"/api/v1/announcements/{id}", updateBody);

        Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
    }

    [TestMethod]
    public async Task DeleteAnnouncement_ReturnsOk()
    {
        var createBody = new { title = "int-del-announce", content = "Delete me", priority = "Normal" };
        var createResponse = await _client.PostAsJsonAsync($"/api/v1/announcements", createBody);
        var id = (await DataAsync(createResponse)).GetProperty("id").GetGuid();

        var response = await _client.DeleteAsync($"/api/v1/announcements/{id}");

        Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
    }

    [TestMethod]
    public async Task AcknowledgeAnnouncement_ReturnsOk()
    {
        var createBody = new { title = "int-ack-announce", content = "Ack me", priority = "Normal" };
        var createResponse = await _client.PostAsJsonAsync($"/api/v1/announcements", createBody);
        var id = (await DataAsync(createResponse)).GetProperty("id").GetGuid();

        var response = await _client.PostAsync($"/api/v1/announcements/{id}/acknowledge", null);

        Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
    }

    [TestMethod]
    public async Task GetAcknowledgements_ReturnsOk()
    {
        var createBody = new { title = "int-ack-list", content = "Check acks", priority = "Normal" };
        var createResponse = await _client.PostAsJsonAsync($"/api/v1/announcements", createBody);
        var id = (await DataAsync(createResponse)).GetProperty("id").GetGuid();
        await _client.PostAsync($"/api/v1/announcements/{id}/acknowledge", null);

        var response = await _client.GetAsync($"/api/v1/announcements/{id}/acknowledgements");

        Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
    }

    // ══════════════════════════════════════════════════════════════════
    //  Push Device Registration
    // ══════════════════════════════════════════════════════════════════

    [TestMethod]
    public async Task RegisterPushDevice_ReturnsOk()
    {
        var body = new { deviceToken = "test-token-12345", provider = "Fcm" };

        var response = await _client.PostAsJsonAsync($"/api/v1/notifications/devices/register", body);

        Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
        var data = await DataAsync(response);
        Assert.IsTrue(data.GetProperty("registered").GetBoolean());
    }

    [TestMethod]
    public async Task RegisterPushDevice_EmptyToken_Returns400()
    {
        var body = new { deviceToken = "", provider = "Fcm" };

        var response = await _client.PostAsJsonAsync($"/api/v1/notifications/devices/register", body);

        Assert.AreEqual(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [TestMethod]
    public async Task RegisterPushDevice_InvalidProvider_Returns400()
    {
        var body = new { deviceToken = "token", provider = "NotReal" };

        var response = await _client.PostAsJsonAsync($"/api/v1/notifications/devices/register", body);

        Assert.AreEqual(HttpStatusCode.BadRequest, response.StatusCode);
    }

    // ══════════════════════════════════════════════════════════════════
    //  Mark Read + Unread
    // ══════════════════════════════════════════════════════════════════

    [TestMethod]
    public async Task MarkRead_ReturnsOk()
    {
        var channelId = await CreateChannelAsync("int-mark-read");
        var messageId = await SendMessageAsync(channelId, "read this");

        var response = await _client.PostAsJsonAsync($"/api/v1/chat/channels/{channelId}/read",
            new { messageId });

        Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
    }

    // ══════════════════════════════════════════════════════════════════
    //  Module Health endpoint
    // ══════════════════════════════════════════════════════════════════

    [TestMethod]
    public async Task ChatModuleHealth_ReturnsHealthy()
    {
        var response = await _client.GetAsync("/health");

        Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
    }

    // ══════════════════════════════════════════════════════════════════
    //  Module Info endpoint
    // ══════════════════════════════════════════════════════════════════

    [TestMethod]
    public async Task ChatModuleInfo_ReturnsModuleMetadata()
    {
        var response = await _client.GetAsync("/");

        Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
        var doc = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync());
        Assert.AreEqual("dotnetcloud.chat", doc.RootElement.GetProperty("module").GetString());
    }

    // ══════════════════════════════════════════════════════════════════
    //  End-to-End Flow: Create→Message→React→Pin→MarkRead
    // ══════════════════════════════════════════════════════════════════

    [TestMethod]
    public async Task FullChatFlow_CreateChannelMessageReactPinRead()
    {
        // 1. Create channel
        var channelId = await CreateChannelAsync("int-full-flow");

        // 2. Add second member
        await _client.PostAsJsonAsync($"/api/v1/chat/channels/{channelId}/members", new { userId = UserB });

        // 3. Send messages from both users
        var msg1 = await SendMessageAsync(channelId, "Hello from A", UserA);
        var msg2 = await SendMessageAsync(channelId, "Hello from B", UserB);

        // 4. React to message
        var reactResponse = await _clientB.PostAsJsonAsync($"/api/v1/chat/messages/{msg1}/reactions", new { emoji = "❤️" });
        Assert.AreEqual(HttpStatusCode.OK, reactResponse.StatusCode);

        // 5. Pin message
        var pinResponse = await _client.PostAsync($"/api/v1/chat/channels/{channelId}/pins/{msg1}", null);
        Assert.AreEqual(HttpStatusCode.OK, pinResponse.StatusCode);

        // 6. Mark read
        var readResponse = await _clientB.PostAsJsonAsync($"/api/v1/chat/channels/{channelId}/read",
            new { messageId = msg2 });
        Assert.AreEqual(HttpStatusCode.OK, readResponse.StatusCode);

        // 7. Verify pinned messages
        var pinsResponse = await _client.GetAsync($"/api/v1/chat/channels/{channelId}/pins");
        Assert.AreEqual(HttpStatusCode.OK, pinsResponse.StatusCode);
        var pins = await DataAsync(pinsResponse);
        Assert.IsTrue(pins.GetArrayLength() >= 1);

        // 8. Verify reactions
        var reactionsResponse = await _client.GetAsync($"/api/v1/chat/messages/{msg1}/reactions");
        Assert.AreEqual(HttpStatusCode.OK, reactionsResponse.StatusCode);
        var reactions = await DataAsync(reactionsResponse);
        Assert.IsTrue(reactions.GetArrayLength() >= 1);
    }
}
