using System.Net;
using DotNetCloud.Core.DTOs;
using DotNetCloud.Modules.Tracks.Web.Services;

namespace DotNetCloud.Modules.Tracks.Tests.Web;

/// <summary>
/// The browser-side <c>ITracksApiClient</c>: envelope unwrapping, product-scoped URLs, and the
/// behaviour of the shared helpers the whole module UI uses.
/// </summary>
[TestClass]
public sealed class TracksHttpApiClientTests
{
    private static readonly Guid ProductId = Guid.Parse("5f1d2c3b-0a49-4c2e-9a1e-1d2c3b4a5f60");

    private static TracksHttpApiClient CreateClient(StubHttpMessageHandler handler)
        => new(new HttpClient(handler) { BaseAddress = new Uri("https://module.example/") });

    private const string ProductJson =
        """{"success":true,"data":{"id":"5f1d2c3b-0a49-4c2e-9a1e-1d2c3b4a5f60","organizationId":"0b6c1c9e-1d1a-4a3f-9d0f-1a2b3c4d5e6f","name":"Board","ownerId":"0b6c1c9e-1d1a-4a3f-9d0f-1a2b3c4d5e6f","etag":"abc","createdAt":"2026-01-01T00:00:00Z","updatedAt":"2026-01-02T00:00:00Z"}}""";

    [TestMethod]
    public async Task GetProductAsync_Envelope_ReturnsProduct()
    {
        var handler = StubHttpMessageHandler.Json(ProductJson);

        var product = await CreateClient(handler).GetProductAsync(ProductId);

        Assert.IsNotNull(product);
        Assert.AreEqual("Board", product.Name);
        Assert.AreEqual($"/api/v1/products/{ProductId}", handler.Requests.Single().Uri);
        Assert.AreEqual(HttpMethod.Get, handler.Requests.Single().Method);
    }

    /// <summary>
    /// The host emits enum <b>names</b> (<c>AddModuleJsonOptions</c>), so the browser client must read
    /// them back — a Web-default options instance without the string-enum converter failed with
    /// "DeserializeUnableToConvertValue ... containerType" the first time a product with default
    /// swimlanes was created in a real browser.
    /// </summary>
    [TestMethod]
    public async Task GetSwimlanesAsync_EnumNamesFromTheHost_Deserialise()
    {
        const string json =
            """{"success":true,"data":[{"id":"6a2e3d4c-1b5a-4c3e-8a2f-2e3d4c5b6a71","containerType":"Product","containerId":"5f1d2c3b-0a49-4c2e-9a1e-1d2c3b4a5f60","title":"To Do","position":1000,"cardLimit":null,"isDone":false,"isArchived":false,"cardCount":0,"createdAt":"2026-01-01T00:00:00Z","updatedAt":"2026-01-01T00:00:00Z"}]}""";
        var handler = StubHttpMessageHandler.Json(json);

        var swimlanes = await CreateClient(handler).ListProductSwimlanesAsync(ProductId);

        Assert.AreEqual(1, swimlanes.Count);
        Assert.AreEqual(SwimlaneContainerType.Product, swimlanes[0].ContainerType);
        Assert.AreEqual("To Do", swimlanes[0].Title);
    }

    [TestMethod]
    public async Task GetProductAsync_NoDataProperty_ReturnsNull()
    {
        var handler = StubHttpMessageHandler.Json("""{"success":true}""");

        var product = await CreateClient(handler).GetProductAsync(ProductId);

        Assert.IsNull(product);
    }

    [TestMethod]
    public async Task ListProductsAsync_EmptyData_ReturnsEmptyList()
    {
        var handler = StubHttpMessageHandler.Json("""{"success":true,"data":[]}""");

        var products = await CreateClient(handler).ListProductsAsync(Guid.NewGuid());

        Assert.AreEqual(0, products.Count);
    }

    [TestMethod]
    public async Task GetProductAsync_ErrorStatus_ThrowsHttpRequestExceptionWithStatus()
    {
        var handler = StubHttpMessageHandler.Json(
            """{"success":false,"error":{"code":"NOT_FOUND","message":"No such product"}}""",
            HttpStatusCode.NotFound);

        var ex = await Assert.ThrowsExactlyAsync<HttpRequestException>(() =>
            CreateClient(handler).GetProductAsync(ProductId));

        Assert.AreEqual(HttpStatusCode.NotFound, ex.StatusCode);
    }

    [TestMethod]
    public async Task CreateProductAsync_PostsToOrganizationRoute()
    {
        var handler = StubHttpMessageHandler.Json(ProductJson);
        var organizationId = Guid.Parse("0b6c1c9e-1d1a-4a3f-9d0f-1a2b3c4d5e6f");

        await CreateClient(handler).CreateProductAsync(
            organizationId,
            new CreateProductDto { Name = "Board" });

        Assert.AreEqual($"/api/v1/organizations/{organizationId}/products", handler.Requests.Single().Uri);
        Assert.AreEqual(HttpMethod.Post, handler.Requests.Single().Method);
    }
}
