using System.Net;
using DotNetCloud.Modules.Tracks.Web.Services;
using Microsoft.Extensions.Logging.Abstractions;

namespace DotNetCloud.Modules.Tracks.Tests.Web;

/// <summary>
/// The browser-side capability clients: the frame can only see its own data through the module host, so
/// a host failure must degrade to "nothing", never to an exception during render.
/// </summary>
[TestClass]
public sealed class ModuleDirectoryTests
{
    private static readonly Guid OrgId = Guid.Parse("6c3a0b1f-2d4e-4f50-8a91-0c2d3e4f5a6b");
    private static readonly Guid UserId = Guid.Parse("8d2a5b0f-8a1c-4a94-a6a3-6b7c8d9e0f11");

    private static ModuleOrganizationDirectory CreateOrganizations(StubHttpMessageHandler handler)
        => new(
            new TracksApi(new HttpClient(handler) { BaseAddress = new Uri("https://module.example/") }),
            NullLogger<ModuleOrganizationDirectory>.Instance);

    private static ModuleUserDirectory CreateUsers(StubHttpMessageHandler handler)
        => new(
            new TracksApi(new HttpClient(handler) { BaseAddress = new Uri("https://module.example/") }),
            NullLogger<ModuleUserDirectory>.Instance);

    [TestMethod]
    public async Task GetUserOrganizationsAsync_ReturnsHostListing()
    {
        var handler = StubHttpMessageHandler.Json(
            $$$"""{"success":true,"data":[{"id":"{{{OrgId}}}","name":"Acme","createdAt":"2026-01-01T00:00:00Z"}]}""");

        var organizations = await CreateOrganizations(handler).GetUserOrganizationsAsync(UserId);

        Assert.AreEqual(1, organizations.Count);
        Assert.AreEqual("Acme", organizations[0].Name);
        Assert.AreEqual("/api/v1/organizations", handler.Requests.Single().Uri);
    }

    [TestMethod]
    public async Task GetUserOrganizationsAsync_HostFailure_ReturnsEmpty()
    {
        var handler = StubHttpMessageHandler.Json(
            """{"success":false,"error":{"code":"UNAVAILABLE","message":"core down"}}""",
            HttpStatusCode.ServiceUnavailable);

        var organizations = await CreateOrganizations(handler).GetUserOrganizationsAsync(UserId);

        Assert.AreEqual(0, organizations.Count);
    }

    /// <summary>
    /// A missing profile consent must reach the page so it can ask the shell for it.
    /// </summary>
    /// <remarks>
    /// Found live: the endpoint answered 200 with an empty list while the core was returning
    /// <c>MODULE_CONSENT_REQUIRED</c>, so the product picker stayed empty with nothing to click and no
    /// way for the user to approve the scope.
    /// </remarks>
    [TestMethod]
    public async Task GetUserOrganizationsAsync_MissingProfileConsent_AsksForConsent()
    {
        var handler = StubHttpMessageHandler.Json(
            """{"success":false,"error":{"code":"PROFILE_CONSENT_REQUIRED","message":"Tracks needs your consent"}}""",
            HttpStatusCode.Forbidden);

        var exception = await Assert.ThrowsExactlyAsync<ConsentRequiredException>(
            () => CreateOrganizations(handler).GetUserOrganizationsAsync(UserId));

        CollectionAssert.AreEqual(new[] { "profile" }, exception.Scopes.ToArray());
    }

    [TestMethod]
    public async Task IsOrganizationMemberAsync_MembershipInListing_ReturnsTrue()
    {
        var handler = StubHttpMessageHandler.Json(
            $$$"""{"success":true,"data":[{"id":"{{{OrgId}}}","name":"Acme"}]}""");

        Assert.IsTrue(await CreateOrganizations(handler).IsOrganizationMemberAsync(OrgId, UserId));
    }

    [TestMethod]
    public async Task IsOrganizationMemberAsync_NotListed_ReturnsFalse()
    {
        var handler = StubHttpMessageHandler.Json("""{"success":true,"data":[]}""");

        Assert.IsFalse(await CreateOrganizations(handler).IsOrganizationMemberAsync(OrgId, UserId));
    }

    [TestMethod]
    public async Task HasOrgRoleAsync_NotSupported_ReturnsFalseWithoutCallingHost()
    {
        var handler = StubHttpMessageHandler.Json("""{"success":true,"data":[]}""");

        Assert.IsFalse(await CreateOrganizations(handler).HasOrgRoleAsync(OrgId, UserId, Guid.NewGuid()));
        Assert.AreEqual(0, handler.Requests.Count);
    }

    [TestMethod]
    public async Task GetDisplayNamesAsync_NoIds_DoesNotCallHost()
    {
        var handler = StubHttpMessageHandler.Json("""{"success":true,"data":{}}""");

        var names = await CreateUsers(handler).GetDisplayNamesAsync([]);

        Assert.AreEqual(0, names.Count);
        Assert.AreEqual(0, handler.Requests.Count);
    }

    [TestMethod]
    public async Task GetDisplayNamesAsync_ReturnsHostMapping()
    {
        var handler = StubHttpMessageHandler.Json(
            $$$"""{"success":true,"data":{"{{{UserId}}}":"Ada Lovelace"}}""");

        var names = await CreateUsers(handler).GetDisplayNamesAsync([UserId]);

        Assert.AreEqual("Ada Lovelace", names[UserId]);
        Assert.AreEqual("/api/v1/users/display-names", handler.Requests.Single().Uri);
        Assert.AreEqual(HttpMethod.Post, handler.Requests.Single().Method);
    }

    [TestMethod]
    public async Task GetDisplayNamesAsync_HostFailure_ReturnsEmpty()
    {
        var handler = StubHttpMessageHandler.Json(
            """{"success":false,"error":{"code":"UNAVAILABLE","message":"core down"}}""",
            HttpStatusCode.ServiceUnavailable);

        var names = await CreateUsers(handler).GetDisplayNamesAsync([UserId]);

        Assert.AreEqual(0, names.Count);
    }

    [TestMethod]
    public async Task SearchUsersAsync_BlankTerm_ReturnsEmptyWithoutCallingHost()
    {
        var handler = StubHttpMessageHandler.Json("""{"success":true,"data":[]}""");

        var results = await CreateUsers(handler).SearchUsersAsync("  ");

        Assert.AreEqual(0, results.Count);
        Assert.AreEqual(0, handler.Requests.Count);
    }

    [TestMethod]
    public async Task SearchUsersAsync_EscapesTermInQuery()
    {
        var handler = StubHttpMessageHandler.Json("""{"success":true,"data":[]}""");

        await CreateUsers(handler).SearchUsersAsync("ada & bob");

        Assert.AreEqual("/api/v1/users/search?q=ada%20%26%20bob&max=20", handler.Requests.Single().Uri);
    }
}
