using DotNetCloud.Core.Authorization;
using DotNetCloud.Core.Capabilities;
using DotNetCloud.Core.Grpc.Capabilities;
using DotNetCloud.Core.Server.Grpc.Services;
using DotNetCloud.Core.Server.Services;
using Grpc.Core;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace DotNetCloud.Core.Server.Tests.Services;

/// <summary>
/// Tests for <see cref="CoreCapabilitiesServiceImpl.GetCurrentUser"/>.
/// </summary>
/// <remarks>
/// Regression: the RPC used to echo <c>request.Caller.UserId</c>, so any authenticated module could
/// ask the core who "the current user" was and receive whatever id it supplied.
/// </remarks>
[TestClass]
public class CoreCapabilitiesCurrentUserTests
{
    private static CoreCapabilitiesServiceImpl CreateService(Guid verifiedUserId, string displayName)
    {
        var userDirectory = new Mock<IUserDirectory>();
        userDirectory
            .Setup(d => d.GetDisplayNamesAsync(It.IsAny<IEnumerable<Guid>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<Guid, string> { [verifiedUserId] = displayName });

        var services = new ServiceCollection()
            .AddSingleton(userDirectory.Object)
            .BuildServiceProvider();

        return new CoreCapabilitiesServiceImpl(
            NullLogger<CoreCapabilitiesServiceImpl>.Instance,
            services,
            new SearchIndexingService(Mock.Of<IServiceScopeFactory>(), NullLogger<SearchIndexingService>.Instance),
            new ModuleEventInbox(
                Mock.Of<DotNetCloud.Core.Events.IEventBus>(),
                Mock.Of<DotNetCloud.Core.Server.ModuleLoading.IModuleManifestRegistry>(),
                NullLogger<ModuleEventInbox>.Instance));
    }

    private static TestServerCallContext CreateContext(CallerContext? caller)
    {
        var context = new TestServerCallContext();

        if (caller is not null)
        {
            context.UserState["CallerContext"] = caller;
        }

        return context;
    }

    [TestMethod]
    public async Task GetCurrentUser_RequestClaimsAnotherUser_ReturnsVerifiedCallerIdentity()
    {
        var verifiedUserId = Guid.CreateVersion7();
        var claimedUserId = Guid.CreateVersion7();
        var service = CreateService(verifiedUserId, "Alice");
        var context = CreateContext(new CallerContext(verifiedUserId, ["Admin"], CallerType.User));

        var response = await service.GetCurrentUser(
            new GetCurrentUserRequest
            {
                Caller = new CallerContextMessage { UserId = claimedUserId.ToString() }
            },
            context);

        Assert.AreEqual(verifiedUserId.ToString(), response.UserId);
        Assert.AreEqual("Alice", response.DisplayName);
        CollectionAssert.Contains(response.Roles.ToList(), "Admin");
    }

    [TestMethod]
    public async Task GetCurrentUser_ModuleCallerWithoutUserAssertion_ReturnsNoUserIdentity()
    {
        var service = CreateService(Guid.CreateVersion7(), "Alice");
        var context = CreateContext(new CallerContext(Guid.CreateVersion7(), [], CallerType.Module));

        var response = await service.GetCurrentUser(
            new GetCurrentUserRequest
            {
                Caller = new CallerContextMessage { UserId = Guid.CreateVersion7().ToString() }
            },
            context);

        Assert.AreEqual(string.Empty, response.UserId);
        Assert.AreEqual(0, response.Roles.Count);
    }

    [TestMethod]
    public async Task GetCurrentUser_NoVerifiedCallerContext_ThrowsUnauthenticated()
    {
        var service = CreateService(Guid.CreateVersion7(), "Alice");
        var context = CreateContext(caller: null);

        var ex = await Assert.ThrowsExactlyAsync<RpcException>(
            () => service.GetCurrentUser(new GetCurrentUserRequest(), context));

        Assert.AreEqual(StatusCode.Unauthenticated, ex.StatusCode);
    }
}
