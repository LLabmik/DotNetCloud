using Api = DotNetCloud.Core.Grpc.ModuleApi.V1;
using Legacy = DotNetCloud.Core.Grpc.Capabilities;
using DotNetCloud.Core.Server.Grpc.Services;
using DotNetCloud.Core.Server.Tests.Services;
using Grpc.Core;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace DotNetCloud.Core.Server.Tests.Grpc;

/// <summary>
/// Tests for <see cref="ModuleApiV1Service"/>: the canonical surface must reuse the legacy handlers
/// (one implementation) while keeping its narrower contract.
/// </summary>
[TestClass]
public class ModuleApiV1ServiceTests
{
    private const string ModuleId = "dotnetcloud.notes";

    private readonly ModuleHealthRegistry _health = new(TimeProvider.System);

    private ModuleApiV1Service CreateService(out Mock<CoreCapabilitiesServiceImpl> legacy)
    {
        legacy = new Mock<CoreCapabilitiesServiceImpl>(
            NullLogger<CoreCapabilitiesServiceImpl>.Instance,
            new ServiceCollection().BuildServiceProvider(),
            null!,
            null!);

        return new ModuleApiV1Service(legacy.Object, _health, NullLogger<ModuleApiV1Service>.Instance);
    }

    [TestMethod]
    public async Task ReportHealth_KnownStatus_IsRecordedAndAcknowledged()
    {
        var service = CreateService(out _);

        var response = await service.ReportHealth(
            new Api.ReportHealthRequest { Status = "Degraded", Detail = "index rebuilding", ActiveOperations = 3 },
            CreateContext());

        Assert.IsTrue(response.Acknowledged);

        var report = _health.Get(ModuleId);
        Assert.IsNotNull(report);
        Assert.AreEqual("degraded", report.Status);
        Assert.AreEqual("index rebuilding", report.Detail);
        Assert.AreEqual(3, report.ActiveOperations);
    }

    [TestMethod]
    public async Task ReportHealth_UnknownStatus_IsRejectedWithoutRecording()
    {
        var service = CreateService(out _);

        var response = await service.ReportHealth(
            new Api.ReportHealthRequest { Status = "banana" },
            CreateContext());

        Assert.IsFalse(response.Acknowledged);
        Assert.IsNull(_health.Get(ModuleId));
    }

    [TestMethod]
    public async Task ReportHealth_DetailWithControlCharacters_IsSanitized()
    {
        var service = CreateService(out _);

        await service.ReportHealth(
            new Api.ReportHealthRequest { Status = "healthy", Detail = "ok\r\nfake log line" },
            CreateContext());

        var report = _health.Get(ModuleId);
        Assert.IsNotNull(report);
        Assert.IsFalse(report.Detail!.Contains('\n'));
        Assert.IsFalse(report.Detail.Contains('\r'));
    }

    [TestMethod]
    public async Task GetUser_LegacyHit_MapsProfileAndWithholdsEmail()
    {
        var service = CreateService(out var legacy);
        legacy
            .Setup(l => l.GetUser(It.IsAny<Legacy.GetUserRequest>(), It.IsAny<ServerCallContext>()))
            .ReturnsAsync(new Legacy.GetUserResponse
            {
                Found = true,
                User = new Legacy.UserInfo
                {
                    Id = "user-1",
                    DisplayName = "Ada",
                    AvatarUrl = "/a.png",
                    IsActive = true,
                    Email = "ada@example.test",
                },
            });

        var response = await service.GetUser(new Api.GetUserRequest { UserId = "user-1" }, CreateContext());

        Assert.IsTrue(response.Found);
        Assert.IsNotNull(response.User);
        Assert.AreEqual("Ada", response.User.DisplayName);
        Assert.AreEqual(string.Empty, response.User.Email, "Email must not be exposed before A3 consent.");
    }

    [TestMethod]
    public async Task SearchUsers_DelegatesQueryAndWithholdsEmail()
    {
        var service = CreateService(out var legacy);
        legacy
            .Setup(l => l.SearchUsers(It.IsAny<Legacy.SearchUsersRequest>(), It.IsAny<ServerCallContext>()))
            .ReturnsAsync(new Legacy.SearchUsersResponse
            {
                Users =
                {
                    new Legacy.UserInfo { Id = "u1", DisplayName = "Ada", Email = "ada@example.test" },
                },
            });

        var response = await service.SearchUsers(
            new Api.SearchUsersRequest { Query = "ada", MaxResults = 5 },
            CreateContext());

        Assert.AreEqual(1, response.Users.Count);
        Assert.AreEqual(string.Empty, response.Users[0].Email);
        legacy.Verify(
            l => l.SearchUsers(
                It.Is<Legacy.SearchUsersRequest>(r => r.Query == "ada" && r.MaxResults == 5),
                It.IsAny<ServerCallContext>()),
            Times.Once);
    }

    [TestMethod]
    public async Task GetCurrentUserContext_MapsIdentityAndRoles()
    {
        var service = CreateService(out var legacy);
        legacy
            .Setup(l => l.GetCurrentUser(It.IsAny<Legacy.GetCurrentUserRequest>(), It.IsAny<ServerCallContext>()))
            .ReturnsAsync(new Legacy.GetCurrentUserResponse
            {
                UserId = "user-1",
                DisplayName = "Ada",
                Locale = "de-DE",
                Timezone = "Europe/Berlin",
                Roles = { "User", "Admin" },
            });

        var response = await service.GetCurrentUserContext(
            new Api.GetCurrentUserContextRequest(),
            CreateContext());

        Assert.AreEqual("user-1", response.UserId);
        Assert.AreEqual("de-DE", response.Locale);
        CollectionAssert.AreEqual(new[] { "User", "Admin" }, response.Roles.ToArray());
    }

    [TestMethod]
    public async Task GetSetting_PassesKeyOnlyAndReturnsValue()
    {
        var service = CreateService(out var legacy);
        legacy
            .Setup(l => l.GetSetting(It.IsAny<Legacy.GetSettingRequest>(), It.IsAny<ServerCallContext>()))
            .ReturnsAsync(new Legacy.GetSettingResponse { Found = true, Value = "v" });

        var response = await service.GetSetting(new Api.GetSettingRequest { Key = "theme" }, CreateContext());

        Assert.IsTrue(response.Found);
        Assert.AreEqual("v", response.Value);
        legacy.Verify(
            l => l.GetSetting(
                It.Is<Legacy.GetSettingRequest>(r => r.Key == "theme" && r.ModuleId.Length == 0),
                It.IsAny<ServerCallContext>()),
            Times.Once);
    }

    [TestMethod]
    public async Task CheckUserConsent_BeforeConsentPlane_ReturnsUnimplemented()
    {
        var service = CreateService(out _);

        var ex = await Assert.ThrowsExactlyAsync<RpcException>(
            () => service.CheckUserConsent(new Api.CheckUserConsentRequest(), CreateContext()));

        Assert.AreEqual(StatusCode.Unimplemented, ex.StatusCode);
    }

    [TestMethod]
    public async Task GetGrantedScopes_BeforeConsentPlane_ReturnsUnimplemented()
    {
        var service = CreateService(out _);

        var ex = await Assert.ThrowsExactlyAsync<RpcException>(
            () => service.GetGrantedScopes(new Api.GetGrantedScopesRequest(), CreateContext()));

        Assert.AreEqual(StatusCode.Unimplemented, ex.StatusCode);
    }

    [TestMethod]
    public async Task ReportHealth_WithoutAuthenticatedModule_ThrowsUnauthenticated()
    {
        var service = CreateService(out _);
        var context = new TestServerCallContext(
            "/dotnetcloud.moduleapi.v1.ModuleApi/ReportHealth",
            []);

        var ex = await Assert.ThrowsExactlyAsync<RpcException>(
            () => service.ReportHealth(new Api.ReportHealthRequest { Status = "healthy" }, context));

        Assert.AreEqual(StatusCode.Unauthenticated, ex.StatusCode);
    }

    private static TestServerCallContext CreateContext()
    {
        var context = new TestServerCallContext("/dotnetcloud.moduleapi.v1.ModuleApi/ReportHealth", []);
        context.UserState["ModuleId"] = ModuleId;
        return context;
    }
}
