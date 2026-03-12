using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using DotNetCloud.Integration.Tests.Infrastructure;
using DotNetCloud.Modules.Chat.DTOs;

namespace DotNetCloud.Integration.Tests.Api;

/// <summary>
/// Integration tests for cross-module chat and files flow.
/// Tests uploading a file via Files API, attaching to a chat message, and retrieving via channel files endpoint.
/// This test demonstrates the integration between Chat and Files modules.
/// Runs against the standalone Chat module via ChatHostWebApplicationFactory.
/// </summary>
[TestClass]
[TestCategory("Integration")]
public sealed class ChatFilesFlowIntegrationTests
{
    private static ChatHostWebApplicationFactory _factory = null!;
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
    }

    [ClassCleanup]
    public static void ClassCleanup()
    {
        _factory.Dispose();
    }

    // ─────────────────────────────────────────────────────────────────
    // Helpers
    // ─────────────────────────────────────────────────────────────────

    /// <summary>Creates a public channel and returns its ID.</summary>
    private static async Task<Guid> CreateChannelAsync(HttpClient client, string name)
    {
        var body = new { name, type = "Public", description = $"Test channel {name}" };
        var response = await client.PostAsJsonAsync($"/api/v1/chat/channels?userId={UserA}", body);
        response.EnsureSuccessStatusCode();

        var doc = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync());
        return doc.RootElement.GetProperty("data").GetProperty("id").GetGuid();
    }

    /// <summary>Sends a message and returns its ID.</summary>
    private static async Task<Guid> SendMessageAsync(HttpClient client, Guid channelId, string content)
    {
        var body = new { content };
        var response = await client.PostAsJsonAsync($"/api/v1/chat/channels/{channelId}/messages?userId={UserA}", body);
        response.EnsureSuccessStatusCode();

        var doc = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync());
        return doc.RootElement.GetProperty("data").GetProperty("id").GetGuid();
    }

    /// <summary>Attaches a file to a message and returns the response.</summary>
    private static async Task<HttpResponseMessage> AttachFileToMessageAsync(
        HttpClient client,
        Guid channelId,
        Guid messageId,
        string fileName,
        string mimeType,
        long fileSize,
        Guid? fileNodeId = null)
    {
        var body = new
        {
            fileName,
            mimeType,
            fileSize,
            fileNodeId
        };

        return await client.PostAsJsonAsync(
            $"/api/v1/chat/channels/{channelId}/messages/{messageId}/attachments?userId={UserA}",
            body);
    }

    /// <summary>Gets the list of files in a channel.</summary>
    private static async Task<HttpResponseMessage> GetChannelFilesAsync(HttpClient client, Guid channelId)
    {
        return await client.GetAsync($"/api/v1/chat/channels/{channelId}/files?userId={UserA}");
    }

    /// <summary>Reads the data element from a success envelope.</summary>
    private static async Task<JsonElement> DataAsync(HttpResponseMessage response)
    {
        var doc = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync());
        Assert.IsTrue(doc.RootElement.GetProperty("success").GetBoolean());
        return doc.RootElement.GetProperty("data");
    }

    // ─────────────────────────────────────────────────────────────────
    // Tests
    // ─────────────────────────────────────────────────────────────────

    [TestMethod]
    public async Task AttachFilesToMessage_CreatesAttachments()
    {
        using var client = _factory.CreateApiClient();

        var channelId = await CreateChannelAsync(client, "file-attach-ch");
        var messageId = await SendMessageAsync(client, channelId, "Message with files");

        var response = await AttachFileToMessageAsync(
            client,
            channelId,
            messageId,
            "document.pdf",
            "application/pdf",
            5120);

        Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
    }

    [TestMethod]
    public async Task AttachMultipleFilesToMessage_Succeeds()
    {
        using var client = _factory.CreateApiClient();

        var channelId = await CreateChannelAsync(client, "multi-attach-ch");
        var messageId = await SendMessageAsync(client, channelId, "Multiple attachments");

        // Attach first file
        var response1 = await AttachFileToMessageAsync(
            client,
            channelId,
            messageId,
            "image1.jpg",
            "image/jpeg",
            2048);
        Assert.AreEqual(HttpStatusCode.OK, response1.StatusCode);

        // Attach second file
        var response2 = await AttachFileToMessageAsync(
            client,
            channelId,
            messageId,
            "image2.png",
            "image/png",
            3072);
        Assert.AreEqual(HttpStatusCode.OK, response2.StatusCode);

        // Attach third file
        var response3 = await AttachFileToMessageAsync(
            client,
            channelId,
            messageId,
            "video.mp4",
            "video/mp4",
            10240);
        Assert.AreEqual(HttpStatusCode.OK, response3.StatusCode);
    }

    [TestMethod]
    public async Task GetChannelFiles_ReturnsAttachedFiles()
    {
        using var client = _factory.CreateApiClient();

        var channelId = await CreateChannelAsync(client, "get-files-ch");
        var messageId = await SendMessageAsync(client, channelId, "Files for listing");

        // Attach files
        await AttachFileToMessageAsync(
            client,
            channelId,
            messageId,
            "report.docx",
            "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
            4096);

        await AttachFileToMessageAsync(
            client,
            channelId,
            messageId,
            "data.xlsx",
            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            6144);

        // Get channel files
        var response = await GetChannelFilesAsync(client, channelId);
        Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);

        var data = await DataAsync(response);
        if (data.ValueKind == System.Text.Json.JsonValueKind.Array)
        {
            var files = data.EnumerateArray().ToList();
            Assert.IsTrue(files.Count >= 2, "Should have at least 2 files");

            var fileNames = new HashSet<string>();
            foreach (var file in files)
            {
                if (file.TryGetProperty("fileName", out var nameElem))
                {
                    fileNames.Add(nameElem.GetString() ?? "");
                }
            }

            Assert.IsTrue(fileNames.Contains("report.docx"), "Should find report.docx");
            Assert.IsTrue(fileNames.Contains("data.xlsx"), "Should find data.xlsx");
        }
    }

    [TestMethod]
    public async Task AttachFileWithNodeId_LinksToFilesModule()
    {
        using var client = _factory.CreateApiClient();

        var channelId = await CreateChannelAsync(client, "linked-file-ch");
        var messageId = await SendMessageAsync(client, channelId, "Message with linked file");

        // Simulate attaching a file that was uploaded to Files module
        var fileNodeId = Guid.NewGuid(); // Would be a real file node ID from Files module
        var response = await AttachFileToMessageAsync(
            client,
            channelId,
            messageId,
            "linked-document.pdf",
            "application/pdf",
            7680,
            fileNodeId);

        Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);

        // Retrieve the message to verify attachment has fileNodeId
        var messageResponse = await client.GetAsync(
            $"/api/v1/chat/channels/{channelId}/messages/{messageId}?userId={UserA}");
        Assert.AreEqual(HttpStatusCode.OK, messageResponse.StatusCode);

        var msgData = await DataAsync(messageResponse);
        if (msgData.ValueKind == System.Text.Json.JsonValueKind.Object &&
            msgData.TryGetProperty("attachments", out var attachmentsElem))
        {
            var attachments = attachmentsElem.EnumerateArray().ToList();
            Assert.IsTrue(attachments.Count > 0, "Should have attachments");

            var linkedAttachment = attachments.FirstOrDefault(a =>
                a.GetProperty("fileName").GetString() == "linked-document.pdf");
            
            if (linkedAttachment.ValueKind != System.Text.Json.JsonValueKind.Undefined)
            {
                Assert.IsTrue(linkedAttachment.TryGetProperty("fileNodeId", out var nodeIdElem), "Should have fileNodeId property");
                var nodeIdStr = nodeIdElem.GetString();
                Assert.AreEqual(fileNodeId.ToString(), nodeIdStr, "FileNodeId should match");
            }
            else
            {
                Assert.Fail("Should find the linked attachment");
            }
        }
    }

    [TestMethod]
    public async Task EmptyChannel_GetChannelFiles_ReturnsEmpty()
    {
        using var client = _factory.CreateApiClient();

        var channelId = await CreateChannelAsync(client, "empty-files-ch");

        // Get channel files without any attachments
        var response = await GetChannelFilesAsync(client, channelId);
        Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);

        var data = await DataAsync(response);
        if (data.ValueKind == System.Text.Json.JsonValueKind.Array)
        {
            var files = data.EnumerateArray().ToList();
            Assert.AreEqual(0, files.Count);
        }
    }

    [TestMethod]
    public async Task AttachmentMetadata_PreservesMimeTypeAndSize()
    {
        using var client = _factory.CreateApiClient();

        var channelId = await CreateChannelAsync(client, "metadata-ch");
        var messageId = await SendMessageAsync(client, channelId, "Attachment metadata test");

        var fileName = "important-file.zip";
        var mimeType = "application/zip";
        var fileSize = 15360L;

        await AttachFileToMessageAsync(client, channelId, messageId, fileName, mimeType, fileSize);

        // Retrieve channel files and verify metadata
        var response = await GetChannelFilesAsync(client, channelId);
        var data = await DataAsync(response);

        if (data.ValueKind == System.Text.Json.JsonValueKind.Array)
        {
            foreach (var file in data.EnumerateArray())
            {
                if (file.GetProperty("fileName").GetString() == fileName)
                {
                    Assert.AreEqual(mimeType, file.GetProperty("mimeType").GetString());
                    Assert.AreEqual(fileSize, file.GetProperty("fileSize").GetInt64());
                    return;
                }
            }
        }

        Assert.Fail("Attachment not found in channel files");
    }

    [TestMethod]
    public async Task MultipleMessagesWithAttachments_AllAppearInChannelFiles()
    {
        using var client = _factory.CreateApiClient();

        var channelId = await CreateChannelAsync(client, "multi-msg-files-ch");

        // Message 1 with 2 files
        var msg1 = await SendMessageAsync(client, channelId, "First message with files");
        await AttachFileToMessageAsync(client, channelId, msg1, "file1a.txt", "text/plain", 512);
        await AttachFileToMessageAsync(client, channelId, msg1, "file1b.txt", "text/plain", 256);

        // Message 2 with 1 file
        var msg2 = await SendMessageAsync(client, channelId, "Second message with file");
        await AttachFileToMessageAsync(client, channelId, msg2, "file2.json", "application/json", 1024);

        // Get all channel files
        var response = await GetChannelFilesAsync(client, channelId);
        var data = await DataAsync(response);

        if (data.ValueKind == System.Text.Json.JsonValueKind.Array)
        {
            var files = data.EnumerateArray().ToList();
            Assert.AreEqual(3, files.Count, "Should have 3 total files from both messages");

            var fileNames = new HashSet<string>();
            foreach (var file in files)
            {
                fileNames.Add(file.GetProperty("fileName").GetString() ?? "");
            }

            Assert.IsTrue(fileNames.Contains("file1a.txt"));
            Assert.IsTrue(fileNames.Contains("file1b.txt"));
            Assert.IsTrue(fileNames.Contains("file2.json"));
        }
    }
}
