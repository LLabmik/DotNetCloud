using System.Net;
using DotNetCloud.Modules.Example.Web.Services;
using DotNetCloud.Modules.Sdk.Web;

namespace DotNetCloud.Modules.Example.Tests;

/// <summary>
/// Covers the browser-side API client the Example module UI runs on.
/// </summary>
/// <remarks>
/// The host answers <c>{ "notes": [...] }</c> for the list and a bare object for a create, which is why
/// the client reads the list through an element rather than a typed envelope. These tests pin both
/// shapes plus the failure paths the UI renders inline.
/// </remarks>
[TestClass]
public class ExampleApiTests
{
    [TestMethod]
    public async Task ListNotesAsync_NotesProperty_ReturnsEveryNote()
    {
        var handler = StubHttpMessageHandler.Json(
            """
            {
              "notes": [
                { "id": "0e6b8e26-1fd9-4bd5-9d55-a5d1f1ad0f0d", "title": "First", "content": "Body", "createdAt": "2026-11-11T09:00:00Z", "updatedAt": null },
                { "id": "8a1f0b5f-6c33-4a1f-9c4e-3c7db7f2a8b1", "title": "Second", "content": "", "createdAt": "2026-11-10T09:00:00Z", "updatedAt": "2026-11-10T10:00:00Z" }
              ]
            }
            """);

        var notes = await CreateApi(handler).ListNotesAsync();

        Assert.AreEqual(2, notes.Count);
        Assert.AreEqual("First", notes[0].Title);
        Assert.AreEqual("Body", notes[0].Content);
        Assert.AreEqual("/api/notes", string.Join("|", handler.Requests));
    }

    [TestMethod]
    public async Task ListNotesAsync_NoContent_ReturnsEmptyList()
    {
        var handler = StubHttpMessageHandler.Json(string.Empty);

        var notes = await CreateApi(handler).ListNotesAsync();

        Assert.AreEqual(0, notes.Count);
    }

    [TestMethod]
    public async Task ListNotesAsync_BareArray_ReturnsEveryNote()
    {
        var handler = StubHttpMessageHandler.Json(
            """
            [ { "id": "8a1f0b5f-6c33-4a1f-9c4e-3c7db7f2a8b1", "title": "Only", "content": "Body", "createdAt": "2026-11-10T09:00:00Z" } ]
            """);

        var notes = await CreateApi(handler).ListNotesAsync();

        Assert.AreEqual(1, notes.Count);
        Assert.AreEqual("Only", notes[0].Title);
    }

    [TestMethod]
    public async Task CreateNoteAsync_HostRejects_ThrowsWithTheHostMessage()
    {
        var handler = StubHttpMessageHandler.Json(
            """{ "error": "A title is required." }""",
            HttpStatusCode.BadRequest);

        var exception = await Assert.ThrowsExactlyAsync<ModuleApiException>(
            () => CreateApi(handler).CreateNoteAsync("   ", "body"));

        Assert.AreEqual("A title is required.", exception.Message);
        Assert.AreEqual(400, exception.StatusCode);
    }

    [TestMethod]
    public async Task CreateNoteAsync_Success_PostsToTheRelativeNotesEndpoint()
    {
        var handler = StubHttpMessageHandler.Json(
            """
            { "id": "0e6b8e26-1fd9-4bd5-9d55-a5d1f1ad0f0d", "title": "T", "content": "", "createdAt": "2026-11-11T09:00:00Z" }
            """);

        await CreateApi(handler).CreateNoteAsync("T", null);

        Assert.AreEqual("/api/notes", string.Join("|", handler.Requests));
    }

    [TestMethod]
    public async Task DeleteNoteAsync_UsesTheRelativeUrlAndTheNoteId()
    {
        var handler = StubHttpMessageHandler.Json(string.Empty, HttpStatusCode.NoContent);
        var noteId = Guid.Parse("0e6b8e26-1fd9-4bd5-9d55-a5d1f1ad0f0d");

        await CreateApi(handler).DeleteNoteAsync(noteId);

        Assert.AreEqual($"/api/notes/{noteId}", string.Join("|", handler.Requests));
    }

    [TestMethod]
    public async Task ListNotesAsync_Unauthenticated_ThrowsAnUnauthorizedFailure()
    {
        var handler = StubHttpMessageHandler.Json(string.Empty, HttpStatusCode.Unauthorized);

        var exception = await Assert.ThrowsExactlyAsync<ModuleApiException>(
            () => CreateApi(handler).ListNotesAsync());

        Assert.IsTrue(exception.IsUnauthorized, "the UI distinguishes 'signed out' from 'request failed'");
    }

    private static ExampleApi CreateApi(StubHttpMessageHandler handler) =>
        new(new ModuleApiClient(new HttpClient(handler) { BaseAddress = new Uri("http://module.local/") }));
}
