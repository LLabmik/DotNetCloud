using System.Net;
using System.Text;
using DotNetCloud.Core.Capabilities;
using DotNetCloud.Core.Grpc;
using DotNetCloud.Modules.Sdk.Api;
using DotNetCloud.Modules.Sdk.Testing;

namespace DotNetCloud.Modules.Sdk.Tests;

/// <summary>
/// Tests for the SDK REST client: it must send the module credential and user assertion on every
/// call, and surface the core's stable error codes rather than a bare status.
/// </summary>
[TestClass]
public class DncModuleRestClientTests
{
    private const string ModuleId = "org.example.customapp";
    private const string ModuleToken = "supervisor-issued-token";

    [TestMethod]
    public async Task GetUser_SendsCredentialAndAssertion_AndMapsTheProfile()
    {
        var handler = new RecordingHandler(_ => Json(
            """{"found":true,"user":{"id":"u1","displayName":"Alice","avatarUrl":"/a.png","isActive":true,"email":""}}"""));

        using var client = CreateClient(handler, assertion: "signed-assertion");

        var user = await client.GetUserAsync("u1");

        Assert.IsNotNull(user);
        Assert.AreEqual("Alice", user.DisplayName);
        Assert.AreEqual("/internal/moduleapi/v1/users/u1", handler.LastRequest!.RequestUri!.AbsolutePath);
        Assert.AreEqual(ModuleId, handler.LastRequest.Headers.GetValues(ModuleIdentityMetadata.ModuleIdHeader).Single());
        Assert.AreEqual(ModuleToken, handler.LastRequest.Headers.GetValues(ModuleIdentityMetadata.ModuleTokenHeader).Single());
        Assert.AreEqual("signed-assertion", handler.LastRequest.Headers.GetValues(ModuleIdentityMetadata.UserAssertionHeader).Single());
    }

    [TestMethod]
    public async Task GetUser_NotFound_ReturnsNull()
    {
        var handler = new RecordingHandler(_ => Json("""{"found":false,"user":null}"""));
        using var client = CreateClient(handler);

        Assert.IsNull(await client.GetUserAsync("missing"));
    }

    [TestMethod]
    public async Task SearchUsers_SendsPagingParameters()
    {
        var handler = new RecordingHandler(_ => Json(
            """{"users":[{"id":"u1","displayName":"Alice","avatarUrl":"","isActive":true,"email":""}]}"""));

        using var client = CreateClient(handler);

        var users = await client.SearchUsersAsync("al", 5);

        Assert.HasCount(1, users);
        StringAssert.Contains(handler.LastRequest!.RequestUri!.Query, "query=al");
        StringAssert.Contains(handler.LastRequest.RequestUri.Query, "limit=5");
    }

    [TestMethod]
    public async Task SendNotification_ReturnsDeliveredCount()
    {
        var handler = new RecordingHandler(_ => Json("""{"success":true,"deliveredCount":2}"""));
        using var client = CreateClient(handler);

        var delivered = await client.SendNotificationAsync(
            new DncNotification("Hi", null, null, "/files", ["u1", "u2"]));

        Assert.AreEqual(2, delivered);
        Assert.AreEqual("/internal/moduleapi/v1/notifications", handler.LastRequest!.RequestUri!.AbsolutePath);
    }

    [TestMethod]
    public async Task GetSetting_AbsentValue_ReturnsNull()
    {
        var handler = new RecordingHandler(_ => Json("""{"found":false,"value":null}"""));
        using var client = CreateClient(handler);

        Assert.IsNull(await client.GetSettingAsync("theme"));
    }

    [TestMethod]
    public async Task SetSetting_SendsPutWithTheValue()
    {
        string? body = null;
        var handler = new RecordingHandler(request =>
        {
            body = request.Content!.ReadAsStringAsync().GetAwaiter().GetResult();
            return Json("""{"success":true}""");
        });

        using var client = CreateClient(handler);

        Assert.IsTrue(await client.SetSettingAsync("theme", "dark"));
        Assert.AreEqual(HttpMethod.Put, handler.LastRequest!.Method);
        StringAssert.Contains(body!, "dark");
    }

    [TestMethod]
    public async Task LogAudit_SendsTheNumericAction()
    {
        string? body = null;
        var handler = new RecordingHandler(request =>
        {
            body = request.Content!.ReadAsStringAsync().GetAwaiter().GetResult();
            return Json("""{"success":true}""");
        });

        using var client = CreateClient(handler);

        Assert.IsTrue(await client.LogAuditAsync(AuditAction.Delete, "Thing", Guid.CreateVersion7()));
        StringAssert.Contains(body!, $"\"action\":{(int)AuditAction.Delete}");
    }

    [TestMethod]
    public async Task CapabilityDenied_SurfacesTheCoreErrorCode()
    {
        var handler = new RecordingHandler(_ => Json(
            """{"success":false,"code":"CAP_NOT_GRANTED","message":"Capability 'IUserDirectory' is not granted."}""",
            HttpStatusCode.Forbidden));

        using var client = CreateClient(handler);

        var ex = await Assert.ThrowsExactlyAsync<DncModuleApiException>(() => client.GetUserAsync("u1"));

        Assert.AreEqual("CAP_NOT_GRANTED", ex.Code);
        Assert.AreEqual(403, ex.StatusCode);
    }

    [TestMethod]
    public async Task NonJsonError_FallsBackToTheHttpStatus()
    {
        var handler = new RecordingHandler(_ => new HttpResponseMessage(HttpStatusCode.BadGateway));
        using var client = CreateClient(handler);

        var ex = await Assert.ThrowsExactlyAsync<DncModuleApiException>(() => client.GetUserAsync("u1"));

        Assert.AreEqual("HTTP_502", ex.Code);
    }

    [TestMethod]
    public async Task InMemoryHarness_MatchesTheClientContract()
    {
        // The point of the interface: module code is testable without a core process.
        IDncModuleApi api = new InMemoryDncModuleApi();
        await api.SetSettingAsync("theme", "dark");

        Assert.AreEqual("dark", await api.GetSettingAsync("theme"));
        Assert.IsTrue(await api.LogAuditAsync(AuditAction.Create, "Thing", Guid.CreateVersion7()));
        Assert.AreEqual(1, await api.SendNotificationAsync(new DncNotification("Hi", null, null, null, ["u1"])));
    }

    private static DncModuleRestClient CreateClient(RecordingHandler handler, string? assertion = null) =>
        new(
            new Uri("https://core.example/internal/moduleapi/v1"),
            ModuleId,
            ModuleToken,
            new HttpClient(handler),
            assertion);

    private static HttpResponseMessage Json(string json, HttpStatusCode status = HttpStatusCode.OK) =>
        new(status)
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json"),
        };

    private sealed class RecordingHandler : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, HttpResponseMessage> _responder;

        public RecordingHandler(Func<HttpRequestMessage, HttpResponseMessage> responder) =>
            _responder = responder;

        public HttpRequestMessage? LastRequest { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            LastRequest = request;
            return Task.FromResult(_responder(request));
        }
    }
}
