using DotNetCloud.Core.Authorization;
using DotNetCloud.Core.Capabilities;
using DotNetCloud.Core.DTOs;
using DotNetCloud.Core.Grpc.Capabilities;
using DotNetCloud.Core.Server.Grpc.Services;
using DotNetCloud.Core.Server.Services;
using Grpc.Core;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace DotNetCloud.Core.Server.Tests.Services;

/// <summary>
/// Tests for the <see cref="CoreCapabilitiesServiceImpl.GetUserOrganizations"/> RPC handler — the
/// capability a sandboxed module host uses to resolve the acting user's organizations.
/// </summary>
[TestClass]
public class CoreCapabilitiesOrganizationDirectoryTests
{
    private static CoreCapabilitiesServiceImpl CreateService(Mock<IOrganizationDirectory> organizationDirectory)
    {
        var services = new ServiceCollection();
        services.AddScoped(_ => organizationDirectory.Object);
        var provider = services.BuildServiceProvider();

        var indexingService = new SearchIndexingService(
            Mock.Of<IServiceScopeFactory>(),
            NullLogger<SearchIndexingService>.Instance);

        return new CoreCapabilitiesServiceImpl(
            NullLogger<CoreCapabilitiesServiceImpl>.Instance,
            provider,
            indexingService,
            new ModuleEventInbox(
                Mock.Of<DotNetCloud.Core.Events.IEventBus>(),
                Mock.Of<DotNetCloud.Core.Server.ModuleLoading.IModuleManifestRegistry>(),
                NullLogger<ModuleEventInbox>.Instance),
            ConsentTestDoubles.NoConsent());
    }

    private static GetUserOrganizationsRequest CreateRequest(Guid? userId = null) => new()
    {
        UserId = (userId ?? Guid.CreateVersion7()).ToString(),
    };

    /// <summary>
    /// A call from a module acting for itself (no end-user assertion). The capability grant is the
    /// control on this path, so any target user is allowed.
    /// </summary>
    private static TestServerCallContext CreateModuleContext()
    {
        var context = new TestServerCallContext();
        context.UserState["ModuleId"] = "dotnetcloud.tracks";
        context.UserState["CallerContext"] = CallerContext.CreateModuleContext(Guid.CreateVersion7());
        return context;
    }

    /// <summary>A call on behalf of a signed-in user (core-signed assertion already verified).</summary>
    private static TestServerCallContext CreateUserContext(Guid userId)
    {
        var context = new TestServerCallContext();
        context.UserState["ModuleId"] = "dotnetcloud.tracks";
        context.UserState["CallerContext"] = new CallerContext(userId, ["User"], CallerType.User);
        return context;
    }

    private static OrganizationDto CreateOrganization(string name = "Acme") => new()
    {
        Id = Guid.CreateVersion7(),
        Name = name,
        Description = "desc",
        MemberCount = 4,
        TeamCount = 2,
        CreatedAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc),
    };

    [TestMethod]
    public async Task GetUserOrganizations_ValidUser_ReturnsMappedOrganizations()
    {
        var first = CreateOrganization("Acme");
        var second = CreateOrganization("Globex");

        var directory = new Mock<IOrganizationDirectory>();
        directory
            .Setup(x => x.GetUserOrganizationsAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { first, second });

        var service = CreateService(directory);

        var response = await service.GetUserOrganizations(CreateRequest(), CreateModuleContext());

        Assert.AreEqual(2, response.Organizations.Count);
        Assert.AreEqual(first.Id.ToString(), response.Organizations[0].Id);
        Assert.AreEqual(first.Name, response.Organizations[0].Name);
        Assert.AreEqual(first.Description, response.Organizations[0].Description);
        Assert.AreEqual(first.MemberCount, response.Organizations[0].MemberCount);
        Assert.AreEqual(first.TeamCount, response.Organizations[0].TeamCount);
        Assert.AreEqual("Globex", response.Organizations[1].Name);
    }

    [TestMethod]
    public async Task GetUserOrganizations_NoOrganizations_ReturnsEmpty()
    {
        var directory = new Mock<IOrganizationDirectory>();
        directory
            .Setup(x => x.GetUserOrganizationsAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<OrganizationDto>());

        var service = CreateService(directory);

        var response = await service.GetUserOrganizations(CreateRequest(), CreateModuleContext());

        Assert.IsNotNull(response);
        Assert.AreEqual(0, response.Organizations.Count);
    }

    [TestMethod]
    public async Task GetUserOrganizations_InvalidUserId_ReturnsEmptyWithoutQuerying()
    {
        var directory = new Mock<IOrganizationDirectory>();
        var service = CreateService(directory);

        var response = await service.GetUserOrganizations(
            new GetUserOrganizationsRequest { UserId = "not-a-guid" },
            CreateModuleContext());

        Assert.AreEqual(0, response.Organizations.Count);
        directory.Verify(
            x => x.GetUserOrganizationsAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [TestMethod]
    public async Task GetUserOrganizations_UserScaledCallForOwnUserId_ReturnsOrganizations()
    {
        var userId = Guid.CreateVersion7();

        var directory = new Mock<IOrganizationDirectory>();
        directory
            .Setup(x => x.GetUserOrganizationsAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { CreateOrganization() });

        var service = CreateService(directory);

        var response = await service.GetUserOrganizations(CreateRequest(userId), CreateUserContext(userId));

        Assert.AreEqual(1, response.Organizations.Count);
    }

    [TestMethod]
    public async Task GetUserOrganizations_UserScaledCallForAnotherUser_IsDenied()
    {
        var caller = Guid.CreateVersion7();
        var victim = Guid.CreateVersion7();

        var directory = new Mock<IOrganizationDirectory>();
        var service = CreateService(directory);

        var ex = await Assert.ThrowsExactlyAsync<RpcException>(
            () => service.GetUserOrganizations(CreateRequest(victim), CreateUserContext(caller)));

        Assert.AreEqual(StatusCode.PermissionDenied, ex.StatusCode);
        Assert.AreEqual("AUTH_SUBJECT_MISMATCH", ex.Trailers.GetValue("error-code"));
        directory.Verify(
            x => x.GetUserOrganizationsAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()),
            Times.Never,
            "a mismatched call must not reach the directory");
    }

    [TestMethod]
    public async Task GetUserOrganizations_NoVerifiedCaller_FailsClosed()
    {
        var directory = new Mock<IOrganizationDirectory>();
        var service = CreateService(directory);

        var ex = await Assert.ThrowsExactlyAsync<RpcException>(
            () => service.GetUserOrganizations(CreateRequest(), new TestServerCallContext()));

        Assert.AreEqual(StatusCode.Unauthenticated, ex.StatusCode);
    }

    [TestMethod]
    public async Task GetUserOrganizations_DirectoryThrows_ReturnsEmpty()
    {
        var directory = new Mock<IOrganizationDirectory>();
        directory
            .Setup(x => x.GetUserOrganizationsAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("db down"));

        var service = CreateService(directory);

        var response = await service.GetUserOrganizations(CreateRequest(), CreateModuleContext());

        Assert.AreEqual(0, response.Organizations.Count);
    }

    [TestMethod]
    public void GetUserOrganizations_BothSurfaces_MapToTheOrganizationDirectoryCapability()
    {
        var legacy = CapabilityCatalog.ResolveCapability(
            CapabilityCatalog.Rpc(CapabilityCatalog.CoreCapabilitiesService, "GetUserOrganizations"));
        var canonical = CapabilityCatalog.ResolveCapability(
            CapabilityCatalog.Rpc(CapabilityCatalog.ModuleApiService, "GetUserOrganizations"));

        Assert.AreEqual(nameof(IOrganizationDirectory), legacy);
        Assert.AreEqual(nameof(IOrganizationDirectory), canonical);
    }
}
