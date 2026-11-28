using DotNetCloud.Modules.Email.Web.Services;
using DotNetCloud.Modules.Sdk.Web;

namespace DotNetCloud.Modules.Email.Tests;

/// <summary>
/// Covers the browser-side API client the Email module UI runs on.
/// </summary>
/// <remarks>
/// This host binds enums as numbers, so the client's rule payloads must carry integers — a client that
/// wrote enum names would be rejected. The file browser answers <c>{ nodes: [...] }</c>.
/// </remarks>
[TestClass]
public class EmailApiTests
{
    [TestMethod]
    public async Task ListAccountsAsync_ReadsTheEnvelope()
    {
        var handler = StubHttpMessageHandler.Json(
            """
            { "success": true, "data": [ { "id": "0e6b8e26-1fd9-4bd5-9d55-a5d1f1ad0f0d", "providerType": 0, "displayName": "Work", "emailAddress": "w@example.com", "isEnabled": true } ] }
            """);

        var accounts = await CreateApi(handler).ListAccountsAsync();

        Assert.AreEqual(1, accounts.Count);
        Assert.AreEqual("Work", accounts[0].DisplayName);
        Assert.AreEqual(0, accounts[0].ProviderType);
        Assert.AreEqual("/api/v1/email/accounts", handler.Requests[0]);
    }

    [TestMethod]
    public async Task ListThreadsAsync_UsesTheAccountMailboxPath()
    {
        var accountId = Guid.NewGuid();
        var mailboxId = Guid.NewGuid();
        var handler = StubHttpMessageHandler.Json(
            """
            { "success": true, "data": [ { "id": "8a1f0b5f-6c33-4a1f-9c4e-3c7db7f2a8b1", "subject": "Hello", "messageCount": 2 } ] }
            """);

        var threads = await CreateApi(handler).ListThreadsAsync(accountId, mailboxId);

        Assert.AreEqual(1, threads.Count);
        Assert.AreEqual($"/api/v1/email/accounts/{accountId}/mailboxes/{mailboxId}/threads", handler.Requests[0]);
    }

    [TestMethod]
    public async Task CreateRuleAsync_WritesEnumValuesAsIntegers()
    {
        var handler = StubHttpMessageHandler.Json("""{ "success": true, "data": {} }""");

        await CreateApi(handler).CreateRuleAsync(new
        {
            name = "Newsletters",
            isEnabled = true,
            priority = 1,
            stopProcessing = false,
            conditionGroups = new[]
            {
                new { matchMode = 0, conditions = new[] { new { field = 3, @operator = 0, value = "newsletter" } } },
            },
            actions = new[] { new { actionType = 4, targetValue = "News" } },
        });

        var body = handler.Bodies[0];
        Assert.IsNotNull(body);
        StringAssert.Contains(body, "\"field\":3");
        StringAssert.Contains(body, "\"operator\":0");
        StringAssert.Contains(body, "\"actionType\":4");
        Assert.IsFalse(body.Contains("\"Rotate\"", StringComparison.Ordinal), "enums must travel as numbers");
    }

    [TestMethod]
    public async Task ListFilesAsync_ReadsTheNodesProperty()
    {
        var handler = StubHttpMessageHandler.Json(
            """
            { "success": true, "data": { "nodes": [ { "id": "0e6b8e26-1fd9-4bd5-9d55-a5d1f1ad0f0d", "name": "report.pdf", "nodeType": "File", "mimeType": "application/pdf", "size": 1024 } ], "totalCount": 1 } }
            """);

        var nodes = await CreateApi(handler).ListFilesAsync(null);

        Assert.AreEqual(1, nodes.Count);
        Assert.AreEqual("report.pdf", nodes[0].Name);
        Assert.AreEqual("/api/v1/email/files", handler.Requests[0]);
    }

    [TestMethod]
    public async Task SaveAttachmentToFilesAsync_PostsTheParentId()
    {
        var handler = StubHttpMessageHandler.Json("""{ "success": true, "data": { "node": { "name": "report.pdf" } } }""");
        var attachmentId = Guid.NewGuid();
        var parentId = Guid.NewGuid();

        await CreateApi(handler).SaveAttachmentToFilesAsync(attachmentId, parentId);

        Assert.AreEqual($"/api/v1/email/attachments/{attachmentId}/save-to-files", handler.Requests[0]);
        StringAssert.Contains(handler.Bodies[0], parentId.ToString());
    }

    [TestMethod]
    public void AttachmentAndGmailUrls_StayRelative()
    {
        var id = Guid.NewGuid();

        Assert.AreEqual($"api/v1/email/attachments/{id}/download", EmailApi.AttachmentDownloadUrl(id));
        Assert.AreEqual("api/v1/email/gmail/oauth/start", EmailApi.GmailStartUrl);
    }

    private static EmailApi CreateApi(StubHttpMessageHandler handler) =>
        new(new ModuleApiClient(new HttpClient(handler) { BaseAddress = new Uri("http://module.local/") }));
}
