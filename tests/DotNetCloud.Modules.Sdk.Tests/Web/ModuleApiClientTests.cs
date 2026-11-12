using System.Net;
using DotNetCloud.Modules.Sdk.Web;

namespace DotNetCloud.Modules.Sdk.Tests.Web;

/// <summary>
/// Covers the shared HTTP client every module UI uses to reach its own host.
/// </summary>
/// <remarks>
/// The core answers <c>{"success":true,"data":…}</c> while a few module hosts answer a bare object, so
/// both shapes are pinned here. A refused consent must surface as
/// <see cref="ModuleConsentRequiredException"/> rather than an empty result — swallowing it left the
/// user with an empty UI and no way to approve the scope.
/// </remarks>
[TestClass]
public class ModuleApiClientTests
{
    [TestMethod]
    public async Task SendAsync_Envelope_UnwrapsTheDataProperty()
    {
        var handler = StubHttpMessageHandler.Json("""{ "success": true, "data": { "id": "42", "name": "Widget" } }""");

        var result = await CreateClient(handler).SendAsync<WidgetDto>(HttpMethod.Get, "api/v1/widgets/42");

        Assert.IsNotNull(result);
        Assert.AreEqual(42, result.Id);
        Assert.AreEqual("Widget", result.Name);
    }

    [TestMethod]
    public async Task SendAsync_DoublyNestedData_UnwrapsToTheInnerObject()
    {
        var handler = StubHttpMessageHandler.Json("""{ "success": true, "data": { "data": { "id": "7", "name": "Inner" } } }""");

        var result = await CreateClient(handler).SendAsync<WidgetDto>(HttpMethod.Get, "api/v1/widgets/7");

        Assert.IsNotNull(result);
        Assert.AreEqual(7, result.Id);
        Assert.AreEqual("Inner", result.Name);
    }

    [TestMethod]
    public async Task SendAsync_BarePayload_IsUsedAsIs()
    {
        var handler = StubHttpMessageHandler.Json("""{ "id": "3", "name": "Bare" }""");

        var result = await CreateClient(handler).SendAsync<WidgetDto>(HttpMethod.Get, "api/v1/widgets/3");

        Assert.IsNotNull(result);
        Assert.AreEqual(3, result.Id);
    }

    [TestMethod]
    public async Task SendAsync_ConsentRefused_ThrowsConsentRequiredWithScopes()
    {
        var handler = StubHttpMessageHandler.Json(
            """{ "error": "Consent required." }""",
            HttpStatusCode.Forbidden,
            response =>
            {
                response.Headers.Add("error-code", ModuleConsentRequiredException.ProfileConsentCode);
                response.Headers.Add("consent-scopes", "profile, files:read");
            });

        var exception = await Assert.ThrowsExactlyAsync<ModuleConsentRequiredException>(
            () => CreateClient(handler).SendAsync<WidgetDto>(HttpMethod.Get, "api/v1/organizations"));

        CollectionAssert.AreEqual(new[] { "profile", "files:read" }, exception.Scopes.ToArray());
    }

    [TestMethod]
    public async Task SendAsync_ErrorEnvelope_ReportsTheHostCodeAndMessage()
    {
        var handler = StubHttpMessageHandler.Json(
            """{ "success": false, "error": { "code": "VALIDATION_FAILED", "message": "Title is required." } }""",
            HttpStatusCode.BadRequest);

        var exception = await Assert.ThrowsExactlyAsync<ModuleApiException>(
            () => CreateClient(handler).SendAsync<WidgetDto>(HttpMethod.Get, "api/v1/widgets"));

        Assert.AreEqual("Title is required.", exception.Message);
        Assert.AreEqual("VALIDATION_FAILED", exception.ErrorCode);
        Assert.AreEqual(400, exception.StatusCode);
    }

    [TestMethod]
    public async Task SendAsync_NoContent_SucceedsWithoutAPayload()
    {
        var handler = StubHttpMessageHandler.Json(string.Empty, HttpStatusCode.NoContent);

        await CreateClient(handler).SendAsync(HttpMethod.Delete, "api/v1/widgets/9");

        Assert.AreEqual("/api/v1/widgets/9", string.Join("|", handler.Requests));
    }

    [TestMethod]
    public async Task SendForElementAsync_NonObjectPayload_ReturnsTheArray()
    {
        var handler = StubHttpMessageHandler.Json("""[1, 2, 3]""");

        var element = await CreateClient(handler).SendForElementAsync(HttpMethod.Get, "api/v1/numbers");

        Assert.IsNotNull(element);
        Assert.AreEqual(3, element.Value.GetArrayLength());
    }

    [TestMethod]
    public async Task JsonOptions_ReadEnumNamesAndIntegers()
    {
        var handler = StubHttpMessageHandler.Json("""{ "status": "Running" }""", HttpStatusCode.OK);
        var named = await CreateClient(handler).SendAsync<StatusDto>(HttpMethod.Get, "api/v1/status");
        Assert.AreEqual(ModuleStatus.Running, named!.Status);

        var numeric = StubHttpMessageHandler.Json("""{ "status": 1 }""");
        var fromInt = await CreateClient(numeric).SendAsync<StatusDto>(HttpMethod.Get, "api/v1/status");
        Assert.AreEqual(ModuleStatus.Running, fromInt!.Status);
    }

    private static ModuleApiClient CreateClient(StubHttpMessageHandler handler) =>
        new(new HttpClient(handler) { BaseAddress = new Uri("http://module.local/") });

    private sealed record WidgetDto
    {
        public int Id { get; init; }

        public string? Name { get; init; }
    }

    private sealed record StatusDto
    {
        public ModuleStatus Status { get; init; }
    }

    private enum ModuleStatus
    {
        Stopped = 0,
        Running = 1,
    }
}
