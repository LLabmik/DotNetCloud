using System.Net;
using DotNetCloud.Core.DTOs;
using DotNetCloud.Modules.Tracks.Web.Services;

namespace DotNetCloud.Modules.Tracks.Tests.Web;

/// <summary>
/// Envelope and error handling of <see cref="TracksApi"/> — the contract the sandboxed UI relies on to
/// turn the module host's <c>{ success, data }</c> / <c>{ success, error }</c> answers into values and
/// typed exceptions.
/// </summary>
[TestClass]
public sealed class TracksApiTests
{
    private static TracksApi CreateApi(StubHttpMessageHandler handler)
        => new(new HttpClient(handler) { BaseAddress = new Uri("https://module.example/") });

    private const string ProductJson =
        """{"success":true,"data":{"id":"5f1d2c3b-0a49-4c2e-9a1e-1d2c3b4a5f60","organizationId":"0b6c1c9e-1d1a-4a3f-9d0f-1a2b3c4d5e6f","name":"Board","ownerId":"0b6c1c9e-1d1a-4a3f-9d0f-1a2b3c4d5e6f","etag":"abc","createdAt":"2026-01-01T00:00:00Z","updatedAt":"2026-01-02T00:00:00Z"}}""";

    [TestMethod]
    public async Task SendAsync_SuccessEnvelope_ReturnsData()
    {
        var handler = StubHttpMessageHandler.Json(ProductJson);

        var product = await CreateApi(handler).SendAsync<ProductDto>(HttpMethod.Get, "api/v1/products/x");

        Assert.IsNotNull(product);
        Assert.AreEqual("Board", product.Name);
    }

    [TestMethod]
    public async Task SendAsync_ErrorEnvelope_ThrowsWithModuleCode()
    {
        var handler = StubHttpMessageHandler.Json(
            """{"success":false,"error":{"code":"FORBIDDEN","message":"Not a member"}}""",
            HttpStatusCode.Forbidden);

        var ex = await Assert.ThrowsExactlyAsync<TracksApiException>(() =>
            CreateApi(handler).SendAsync<ProductDto>(HttpMethod.Get, "api/v1/products/x"));

        Assert.AreEqual("FORBIDDEN", ex.Code);
        Assert.AreEqual("Not a member", ex.Message);
        Assert.AreEqual(HttpStatusCode.Forbidden, ex.StatusCode);
    }

    [TestMethod]
    public async Task SendAsync_NonJsonErrorBody_FallsBackToStatusCode()
    {
        var handler = new StubHttpMessageHandler((_, _) => new HttpResponseMessage(HttpStatusCode.BadGateway)
        {
            Content = new StringContent("<html>bad gateway</html>"),
        });

        var ex = await Assert.ThrowsExactlyAsync<TracksApiException>(() =>
            CreateApi(handler).SendAsync<ProductDto>(HttpMethod.Get, "api/v1/products/x"));

        Assert.AreEqual("UNKNOWN", ex.Code);
        Assert.AreEqual(HttpStatusCode.BadGateway, ex.StatusCode);
    }

    [TestMethod]
    public async Task SendAsync_WithBody_SerialisesEnumsByName()
    {
        var handler = StubHttpMessageHandler.Json(ProductJson);

        await CreateApi(handler).SendAsync<ProductDto>(
            HttpMethod.Post,
            $"api/v1/products/{Guid.NewGuid()}/priority",
            new PriorityBody { Priority = Priority.Urgent });

        var body = handler.Requests.Single().Body;
        Assert.IsNotNull(body);
        StringAssert.Contains(body, "\"priority\":\"Urgent\"");
    }

    private sealed record PriorityBody
    {
        public Priority Priority { get; init; }
    }

    [TestMethod]
    public async Task GetModuleInfoAsync_UnreachableHost_ReturnsNull()
    {
        var handler = new StubHttpMessageHandler((_, _) => throw new HttpRequestException("connection refused"));

        var info = await CreateApi(handler).GetModuleInfoAsync();

        Assert.IsNull(info);
    }

    [TestMethod]
    public async Task GetModuleInfoAsync_Success_ReturnsInfo()
    {
        var handler = StubHttpMessageHandler.Json("""{"module":"dotnetcloud.tracks","version":"1.0.0","status":"Running"}""");

        var info = await CreateApi(handler).GetModuleInfoAsync();

        Assert.IsNotNull(info);
        Assert.AreEqual("dotnetcloud.tracks", info.Module);
    }
}
