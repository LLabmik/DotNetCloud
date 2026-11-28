using System.Net;
using System.Security.Claims;
using DotNetCloud.Modules.Tracks.Web.Services;
using Microsoft.AspNetCore.Components.Authorization;

namespace DotNetCloud.Modules.Tracks.Tests.Web;

/// <summary>
/// The frame's identity: the UI must end up anonymous when the host cannot identify the caller, and
/// carry the module's claims when it can — without asking twice.
/// </summary>
[TestClass]
public sealed class TracksAuthenticationStateProviderTests
{
    private static readonly Guid UserId = Guid.Parse("8d2a5b0f-8a1c-4a94-a6a3-6b7c8d9e0f11");

    private static TracksAuthenticationStateProvider CreateProvider(StubHttpMessageHandler handler)
    {
        var api = new TracksApi(new HttpClient(handler) { BaseAddress = new Uri("https://module.example/") });
        return new TracksAuthenticationStateProvider(api, new RecordingJsRuntime());
    }

    [TestMethod]
    public async Task GetAuthenticationStateAsync_HostCannotIdentifyCaller_ReturnsAnonymous()
    {
        var handler = StubHttpMessageHandler.Json(
            """{"success":false,"error":{"code":"UNAUTHORIZED","message":"no assertion"}}""",
            HttpStatusCode.Unauthorized);

        var state = await CreateProvider(handler).GetAuthenticationStateAsync();

        Assert.IsFalse(state.User.Identity?.IsAuthenticated ?? false);
    }

    [TestMethod]
    public async Task GetAuthenticationStateAsync_WithSession_ReturnsModuleClaims()
    {
        var handler = StubHttpMessageHandler.Json(
            $$$"""{"success":true,"data":{"userId":"{{{UserId}}}","displayName":"Ada Lovelace","email":"ada@example.test"}}""");

        var state = await CreateProvider(handler).GetAuthenticationStateAsync();
        var user = state.User;

        Assert.IsTrue(user.Identity?.IsAuthenticated);
        Assert.AreEqual("ModuleAssertion", user.Identity!.AuthenticationType);
        Assert.AreEqual(UserId.ToString(), user.FindFirstValue(ClaimTypes.NameIdentifier));
        Assert.AreEqual("Ada Lovelace", user.FindFirstValue(ClaimTypes.Name));
        Assert.AreEqual("ada@example.test", user.FindFirstValue(ClaimTypes.Email));
    }

    [TestMethod]
    public async Task GetAuthenticationStateAsync_CalledTwice_ReadsSessionOnce()
    {
        var handler = StubHttpMessageHandler.Json(
            $$$"""{"success":true,"data":{"userId":"{{{UserId}}}","displayName":"Ada Lovelace"}}""");
        var provider = CreateProvider(handler);

        await provider.GetAuthenticationStateAsync();
        await provider.GetAuthenticationStateAsync();

        Assert.AreEqual(1, handler.Requests.Count);
        Assert.AreEqual("/api/v1/me", handler.Requests[0].Uri);
    }

    [TestMethod]
    public async Task GetSessionAsync_WithoutEmail_OmitsEmailClaim()
    {
        var handler = StubHttpMessageHandler.Json(
            $$$"""{"success":true,"data":{"userId":"{{{UserId}}}"}}""");

        var user = (await CreateProvider(handler).GetAuthenticationStateAsync()).User;

        Assert.IsNull(user.FindFirst(ClaimTypes.Email));
        Assert.IsNull(user.FindFirst(ClaimTypes.Name));
        Assert.AreEqual(UserId.ToString(), user.FindFirstValue(ClaimTypes.NameIdentifier));
    }
}
