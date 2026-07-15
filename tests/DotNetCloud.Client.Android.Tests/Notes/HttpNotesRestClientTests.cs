using System.Net;
using System.Text.Json;
using DotNetCloud.Client.Android.Notes;
using DotNetCloud.Core.DTOs;
using Moq;
using Moq.Protected;

namespace DotNetCloud.Client.Android.Tests.Notes;

[TestClass]
public sealed class HttpNotesRestClientTests
{
    private const string ServerUrl = "https://example.com:15443";
    private const string AccessToken = "test-access-token-123";

    private Mock<HttpMessageHandler> _handler = null!;
    private HttpClient _httpClient = null!;
    private HttpNotesRestClient _client = null!;

    [TestInitialize]
    public void Setup()
    {
        _handler = new Mock<HttpMessageHandler>(MockBehavior.Strict);
        _httpClient = new HttpClient(_handler.Object);
        _client = new HttpNotesRestClient(_httpClient);
    }

    [TestCleanup]
    public void Cleanup()
    {
        try { _httpClient.Dispose(); } catch { }
    }

    private static string GetUrl(HttpRequestMessage m) =>
        m.RequestUri?.ToString() ?? string.Empty;

    private string SerializeEnvelope(object? data)
    {
        var envelope = new Dictionary<string, object?>
        {
            ["success"] = true,
            ["data"] = data
        };
        return JsonSerializer.Serialize(envelope, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        });
    }

    private void SetupGetResponse(string urlPattern, HttpStatusCode statusCode, object? responseData)
    {
        var json = SerializeEnvelope(responseData);
        _handler.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.Is<HttpRequestMessage>(m =>
                    m.Method == HttpMethod.Get && GetUrl(m).Contains(urlPattern)),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = statusCode,
                Content = new StringContent(json)
            });
    }

    private void SetupMutateResponse(string urlPattern, HttpMethod method, HttpStatusCode statusCode, object? responseData)
    {
        var json = SerializeEnvelope(responseData);
        _handler.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.Is<HttpRequestMessage>(m =>
                    m.Method == method && GetUrl(m).Contains(urlPattern)),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = statusCode,
                Content = new StringContent(json)
            });
    }

    private void SetupDeleteResponse(string urlPattern, HttpStatusCode statusCode)
    {
        _handler.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.Is<HttpRequestMessage>(m =>
                    m.Method == HttpMethod.Delete && GetUrl(m).Contains(urlPattern)),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = statusCode,
                Content = new StringContent("{\"success\":true,\"data\":{\"deleted\":true}}")
            });
    }

    private void SetupNotFound(string urlPattern)
    {
        _handler.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.Is<HttpRequestMessage>(m =>
                    m.Method == HttpMethod.Get && GetUrl(m).Contains(urlPattern)),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.NotFound,
                Content = new StringContent("{\"success\":false,\"data\":null}")
            });
    }

    private static NoteDto CreateNoteDto(Guid id, string title) => new()
    {
        Id = id,
        OwnerId = Guid.NewGuid(),
        Title = title,
        Content = "Hello **world**",
        Format = NoteContentFormat.Markdown,
        CreatedAt = DateTime.UtcNow,
        UpdatedAt = DateTime.UtcNow,
        Version = 1,
        Tags = [],
        Links = [],
        ContentLength = 15
    };

    [TestMethod]
    public async Task ListNotesAsync_ReturnsNotes()
    {
        var notes = new List<NoteDto>
        {
            CreateNoteDto(Guid.NewGuid(), "Note One"),
            CreateNoteDto(Guid.NewGuid(), "Note Two")
        };
        SetupGetResponse("/api/v1/notes?skip=0&take=50", HttpStatusCode.OK, notes);
        var result = await _client.ListNotesAsync(ServerUrl, AccessToken);
        Assert.AreEqual(2, result.Count);
        Assert.AreEqual("Note One", result[0].Title);
        Assert.AreEqual("Note Two", result[1].Title);
    }

    [TestMethod]
    public async Task ListNotesAsync_ReturnsEmpty_WhenNullData()
    {
        SetupGetResponse("/api/v1/notes?skip=0&take=50", HttpStatusCode.OK, null);
        var result = await _client.ListNotesAsync(ServerUrl, AccessToken);
        Assert.AreEqual(0, result.Count);
    }

    [TestMethod]
    public async Task ListNotesAsync_PassesFolderIdQueryParam()
    {
        var folderId = Guid.NewGuid();
        var notes = new List<NoteDto> { CreateNoteDto(Guid.NewGuid(), "Filtered Note") };
        SetupGetResponse($"/api/v1/notes?skip=0&take=50&folderId={folderId}", HttpStatusCode.OK, notes);
        var result = await _client.ListNotesAsync(ServerUrl, AccessToken, folderId);
        Assert.AreEqual(1, result.Count);
        Assert.AreEqual("Filtered Note", result[0].Title);
    }

    [TestMethod]
    public async Task GetNoteAsync_ReturnsNote()
    {
        var noteId = Guid.NewGuid();
        var note = CreateNoteDto(noteId, "My Note");
        SetupGetResponse($"/api/v1/notes/{noteId}", HttpStatusCode.OK, note);
        var result = await _client.GetNoteAsync(ServerUrl, AccessToken, noteId);
        Assert.IsNotNull(result);
        Assert.AreEqual(noteId, result.Id);
        Assert.AreEqual("My Note", result.Title);
    }

    [TestMethod]
    public async Task GetNoteAsync_Throws_WhenNotFound()
    {
        var noteId = Guid.NewGuid();
        SetupNotFound($"/api/v1/notes/{noteId}");
        var threw = false;
        try
        {
            await _client.GetNoteAsync(ServerUrl, AccessToken, noteId);
        }
        catch (HttpRequestException)
        {
            threw = true;
        }
        Assert.IsTrue(threw, "Expected HttpRequestException for not-found note.");
    }

    [TestMethod]
    public async Task CreateNoteAsync_SendsDto_ReturnsCreatedNote()
    {
        var noteId = Guid.NewGuid();
        var dto = new CreateNoteDto
        {
            Title = "New Note",
            Content = "Hello!",
            Format = NoteContentFormat.Markdown
        };
        var created = CreateNoteDto(noteId, "New Note");
        SetupMutateResponse("/api/v1/notes", HttpMethod.Post, HttpStatusCode.Created, created);
        var result = await _client.CreateNoteAsync(ServerUrl, AccessToken, dto);
        Assert.IsNotNull(result);
        Assert.AreEqual(noteId, result.Id);
        Assert.AreEqual("New Note", result.Title);
    }

    [TestMethod]
    public async Task UpdateNoteAsync_SendsDto_ReturnsUpdatedNote()
    {
        var noteId = Guid.NewGuid();
        var dto = new UpdateNoteDto
        {
            Title = "Updated Title",
            Content = "Updated content",
            ExpectedVersion = 1
        };
        var updated = new NoteDto
        {
            Id = noteId,
            OwnerId = Guid.NewGuid(),
            Title = "Updated Title",
            Content = "Updated content",
            Format = NoteContentFormat.Markdown,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
            Version = 2,
            Tags = [],
            Links = [],
            ContentLength = 15
        };
        SetupMutateResponse($"/api/v1/notes/{noteId}", HttpMethod.Put, HttpStatusCode.OK, updated);
        var result = await _client.UpdateNoteAsync(ServerUrl, AccessToken, noteId, dto);
        Assert.IsNotNull(result);
        Assert.AreEqual("Updated Title", result.Title);
        Assert.AreEqual(2, result.Version);
    }

    [TestMethod]
    public async Task DeleteNoteAsync_CallsDeleteEndpoint()
    {
        var noteId = Guid.NewGuid();
        SetupDeleteResponse($"/api/v1/notes/{noteId}", HttpStatusCode.OK);
        await _client.DeleteNoteAsync(ServerUrl, AccessToken, noteId);
        _handler.Protected().Verify(
            "SendAsync",
            Times.Once(),
            ItExpr.Is<HttpRequestMessage>(m =>
                m.Method == HttpMethod.Delete &&
                GetUrl(m).Contains($"/api/v1/notes/{noteId}")),
            ItExpr.IsAny<CancellationToken>());
    }

    [TestMethod]
    public async Task SearchNotesAsync_UrlEncodesQuery()
    {
        var notes = new List<NoteDto> { CreateNoteDto(Guid.NewGuid(), "Result") };
        SetupGetResponse("/api/v1/notes/search?q=test&skip=0&take=50", HttpStatusCode.OK, notes);
        var result = await _client.SearchNotesAsync(ServerUrl, AccessToken, "test");
        Assert.AreEqual(1, result.Count);
        Assert.AreEqual("Result", result[0].Title);
    }

    [TestMethod]
    public async Task GetNotePreviewAsync_ReturnsPreview()
    {
        var noteId = Guid.NewGuid();
        var preview = new NotePreviewResponse
        {
            NoteId = noteId,
            Title = "Preview Note",
            RenderedHtml = "<p>Hello <strong>world</strong></p>",
            Format = NoteContentFormat.Markdown,
            Version = 1
        };
        SetupGetResponse($"/api/v1/notes/{noteId}/preview", HttpStatusCode.OK, preview);
        var result = await _client.GetNotePreviewAsync(ServerUrl, AccessToken, noteId);
        Assert.IsNotNull(result);
        Assert.AreEqual(noteId, result.NoteId);
        Assert.AreEqual("<p>Hello <strong>world</strong></p>", result.RenderedHtml);
    }

    [TestMethod]
    public async Task RenderMarkdownAsync_ReturnsHtml()
    {
        var responseData = new { html = "<p>Rendered</p>" };
        var json = SerializeEnvelope(responseData);
        _handler.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.Is<HttpRequestMessage>(m =>
                    m.Method == HttpMethod.Post &&
                    GetUrl(m).Contains("/api/v1/notes/render")),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.OK,
                Content = new StringContent(json)
            });
        var result = await _client.RenderMarkdownAsync(ServerUrl, AccessToken, "**bold**");
        Assert.AreEqual("<p>Rendered</p>", result);
    }

    [TestMethod]
    public async Task ListFoldersAsync_ReturnsFolders()
    {
        var folders = new List<NoteFolderDto>
        {
            new() { Id = Guid.NewGuid(), OwnerId = Guid.NewGuid(), Name = "Work", CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow },
            new() { Id = Guid.NewGuid(), OwnerId = Guid.NewGuid(), Name = "Personal", CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow }
        };
        SetupGetResponse("/api/v1/notes/folders", HttpStatusCode.OK, folders);
        var result = await _client.ListFoldersAsync(ServerUrl, AccessToken);
        Assert.AreEqual(2, result.Count);
        Assert.AreEqual("Work", result[0].Name);
        Assert.AreEqual("Personal", result[1].Name);
    }

    [TestMethod]
    public async Task CreateFolderAsync_SendsDto_ReturnsCreatedFolder()
    {
        var folderId = Guid.NewGuid();
        var dto = new CreateNoteFolderDto { Name = "New Folder" };
        var created = new NoteFolderDto
        {
            Id = folderId,
            OwnerId = Guid.NewGuid(),
            Name = "New Folder",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        SetupMutateResponse("/api/v1/notes/folders", HttpMethod.Post, HttpStatusCode.Created, created);
        var result = await _client.CreateFolderAsync(ServerUrl, AccessToken, dto);
        Assert.IsNotNull(result);
        Assert.AreEqual("New Folder", result.Name);
    }

    [TestMethod]
    public async Task UpdateFolderAsync_SendsDto_ReturnsUpdatedFolder()
    {
        var folderId = Guid.NewGuid();
        var dto = new UpdateNoteFolderDto { Name = "Renamed Folder" };
        var updated = new NoteFolderDto
        {
            Id = folderId,
            OwnerId = Guid.NewGuid(),
            Name = "Renamed Folder",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        SetupMutateResponse($"/api/v1/notes/folders/{folderId}", HttpMethod.Put, HttpStatusCode.OK, updated);
        var result = await _client.UpdateFolderAsync(ServerUrl, AccessToken, folderId, dto);
        Assert.IsNotNull(result);
        Assert.AreEqual("Renamed Folder", result.Name);
    }

    [TestMethod]
    public async Task DeleteFolderAsync_CallsDeleteEndpoint()
    {
        var folderId = Guid.NewGuid();
        SetupDeleteResponse($"/api/v1/notes/folders/{folderId}", HttpStatusCode.OK);
        await _client.DeleteFolderAsync(ServerUrl, AccessToken, folderId);
        _handler.Protected().Verify(
            "SendAsync",
            Times.Once(),
            ItExpr.Is<HttpRequestMessage>(m =>
                m.Method == HttpMethod.Delete &&
                GetUrl(m).Contains($"/api/v1/notes/folders/{folderId}")),
            ItExpr.IsAny<CancellationToken>());
    }
}