using System.Net;
using DotNetCloud.Modules.Tracks.Web.Services;
using Microsoft.Extensions.Logging.Abstractions;

namespace DotNetCloud.Modules.Tracks.Tests.Web;

/// <summary>
/// The command palette's browser-side search: one round trip, and an empty result instead of an
/// exception when the host cannot answer.
/// </summary>
[TestClass]
public sealed class BrowserCommandPaletteTests
{
    private static readonly Guid OrgId = Guid.Parse("6c3a0b1f-2d4e-4f50-8a91-0c2d3e4f5a6b");

    private static BrowserCommandPalette CreatePalette(StubHttpMessageHandler handler)
        => new(
            new TracksApi(new HttpClient(handler) { BaseAddress = new Uri("https://module.example/") }),
            NullLogger<BrowserCommandPalette>.Instance);

    [TestMethod]
    public async Task SearchAsync_BlankQuery_DoesNotCallHost()
    {
        var handler = StubHttpMessageHandler.Json("""{"success":true,"data":{}}""");

        var result = await CreatePalette(handler).SearchAsync(OrgId, "   ", null, CancellationToken.None);

        Assert.IsNotNull(result);
        Assert.AreEqual(0, handler.Requests.Count);
    }

    [TestMethod]
    public async Task SearchAsync_WithProduct_IncludesProductFilter()
    {
        var handler = StubHttpMessageHandler.Json("""{"success":true,"data":{}}""");
        var productId = Guid.Parse("5f1d2c3b-0a49-4c2e-9a1e-1d2c3b4a5f60");

        await CreatePalette(handler).SearchAsync(OrgId, "login bug", productId, CancellationToken.None);

        var uri = handler.Requests.Single().Uri;
        Assert.AreEqual($"/api/v1/organizations/{OrgId}/palette?q=login%20bug&productId={productId}", uri);
    }

    [TestMethod]
    public async Task SearchAsync_WithoutProduct_OmitsProductFilter()
    {
        var handler = StubHttpMessageHandler.Json("""{"success":true,"data":{}}""");

        await CreatePalette(handler).SearchAsync(OrgId, "login", null, CancellationToken.None);

        Assert.AreEqual($"/api/v1/organizations/{OrgId}/palette?q=login", handler.Requests.Single().Uri);
    }

    [TestMethod]
    public async Task SearchAsync_HostFailure_ReturnsEmptyResult()
    {
        var handler = StubHttpMessageHandler.Json(
            """{"success":false,"error":{"code":"UNAVAILABLE","message":"core down"}}""",
            HttpStatusCode.ServiceUnavailable);

        var result = await CreatePalette(handler).SearchAsync(OrgId, "login", null, CancellationToken.None);

        Assert.IsNotNull(result);
    }
}
