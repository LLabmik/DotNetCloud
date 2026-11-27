using DotNetCloud.Modules.AI.Web.Services;
using DotNetCloud.Modules.Sdk.Web;

namespace DotNetCloud.Modules.AI.Tests;

/// <summary>
/// Covers the browser-side API client the AI Assistant module UI runs on.
/// </summary>
/// <remarks>
/// This host answers bare objects (no success envelope), so the client must accept a payload without a
/// <c>data</c> property.
/// </remarks>
[TestClass]
public class AiApiTests
{
    [TestMethod]
    public async Task ListConversationsAsync_ReadsABareArray()
    {
        var handler = StubHttpMessageHandler.Json(
            """
            [ { "id": "0e6b8e26-1fd9-4bd5-9d55-a5d1f1ad0f0d", "title": "First", "model": "llama3", "createdAt": "2026-11-11T09:00:00Z", "updatedAt": "2026-11-11T10:00:00Z" } ]
            """);

        var conversations = await CreateApi(handler).ListConversationsAsync();

        Assert.AreEqual(1, conversations.Count);
        Assert.AreEqual("First", conversations[0].Title);
        Assert.AreEqual("/api/v1/ai/conversations", handler.Requests[0]);
    }

    [TestMethod]
    public async Task GetConversationAsync_ReadsTheMessages()
    {
        var handler = StubHttpMessageHandler.Json(
            """
            { "id": "0e6b8e26-1fd9-4bd5-9d55-a5d1f1ad0f0d", "title": "T", "messages": [ { "id": "8a1f0b5f-6c33-4a1f-9c4e-3c7db7f2a8b1", "role": "user", "content": "Hi", "createdAt": "2026-11-11T09:00:00Z" } ] }
            """);

        var conversation = await CreateApi(handler).GetConversationAsync(Guid.NewGuid());

        Assert.IsNotNull(conversation);
        Assert.AreEqual(1, conversation.Messages.Count);
        Assert.AreEqual("user", conversation.Messages[0].Role);
    }

    [TestMethod]
    public async Task RenameConversationAsync_SendsPatchWithTheTitle()
    {
        string? body = null;
        var handler = new StubHttpMessageHandler(request =>
        {
            body = request.Content?.ReadAsStringAsync().GetAwaiter().GetResult();
            return new HttpResponseMessage(System.Net.HttpStatusCode.OK)
            {
                Content = new StringContent("""{ "success": true }"""),
            };
        });

        var id = Guid.NewGuid();
        await CreateApi(handler).RenameConversationAsync(id, "Renamed");

        Assert.AreEqual(HttpMethod.Patch, handler.Methods[0]);
        Assert.AreEqual($"/api/v1/ai/conversations/{id}/title", handler.Requests[0]);
        StringAssert.Contains(body, "\"title\":\"Renamed\"");
    }

    [TestMethod]
    public async Task IsModelHealthyAsync_ServiceUnavailable_IsFalse()
    {
        var handler = StubHttpMessageHandler.Json("""{ "status": "unhealthy" }""", System.Net.HttpStatusCode.ServiceUnavailable);

        Assert.IsFalse(await CreateApi(handler).IsModelHealthyAsync());
    }

    [TestMethod]
    public async Task GetSettingsAsync_ReadsTheBareObject()
    {
        var handler = StubHttpMessageHandler.Json("""{ "defaultModel": "llama3", "provider": "ollama" }""");

        var settings = await CreateApi(handler).GetSettingsAsync();

        Assert.IsNotNull(settings);
        Assert.AreEqual("llama3", settings.DefaultModel);
    }

    [TestMethod]
    public void StreamUrl_IsRelativeToTheFrame()
    {
        var id = Guid.NewGuid();

        Assert.AreEqual($"api/v1/ai/conversations/{id}/messages/stream", AiApi.StreamUrl(id));
    }

    private static AiApi CreateApi(StubHttpMessageHandler handler) =>
        new(new ModuleApiClient(new HttpClient(handler) { BaseAddress = new Uri("http://module.local/") }));
}
