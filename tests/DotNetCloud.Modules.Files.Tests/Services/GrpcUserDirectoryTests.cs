using DotNetCloud.Core.Grpc.Capabilities;
using DotNetCloud.Modules.Files.Data.Services;
using Grpc.Core;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace DotNetCloud.Modules.Files.Tests.Services;

/// <summary>
/// Tests for <see cref="GrpcUserDirectory"/> — the CoreCapabilities gRPC calls used to resolve the
/// editing user's display name for the WOPI <c>CheckFileInfo</c> response.
/// </summary>
/// <remarks>
/// Core.Server's <c>AuthenticationInterceptor</c> rejects any capability call that does not carry the
/// <c>module-id</c> metadata header (<c>Unauthenticated: "Missing module-id metadata header"</c>), so the
/// header is part of the contract, not an optional extra: without it no display name is ever resolved
/// and Collabora keeps showing the user GUID.
/// </remarks>
[TestClass]
public class GrpcUserDirectoryTests
{
    private const string TestModuleId = "dotnetcloud.files";

    private string? _originalModuleId;

    [TestInitialize]
    public void SetUp()
    {
        _originalModuleId = Environment.GetEnvironmentVariable("DOTNETCLOUD_MODULE_ID");
        Environment.SetEnvironmentVariable("DOTNETCLOUD_MODULE_ID", TestModuleId);
    }

    [TestCleanup]
    public void CleanUp()
    {
        Environment.SetEnvironmentVariable("DOTNETCLOUD_MODULE_ID", _originalModuleId);
    }

    private static AsyncUnaryCall<GetUserResponse> GetUserCall(GetUserResponse response) =>
        new(
            Task.FromResult(response),
            Task.FromResult(new Metadata()),
            () => Status.DefaultSuccess,
            () => new Metadata(),
            () => { });

    private static AsyncUnaryCall<GetUserResponse> FaultedGetUserCall(RpcException exception) =>
        new(
            Task.FromException<GetUserResponse>(exception),
            Task.FromResult(new Metadata()),
            () => Status.DefaultSuccess,
            () => new Metadata(),
            () => { });

    private static AsyncUnaryCall<SearchUsersResponse> SearchUsersCall(params UserInfo[] users)
    {
        var response = new SearchUsersResponse();
        response.Users.AddRange(users);

        return new AsyncUnaryCall<SearchUsersResponse>(
            Task.FromResult(response),
            Task.FromResult(new Metadata()),
            () => Status.DefaultSuccess,
            () => new Metadata(),
            () => { });
    }

    private static Mock<CoreCapabilities.CoreCapabilitiesClient> CreateClient() => new();

    private static void SetupGetUser(Mock<CoreCapabilities.CoreCapabilitiesClient> client, GetUserResponse response) =>
        client
            .Setup(c => c.GetUserAsync(
                It.IsAny<GetUserRequest>(),
                It.IsAny<Metadata>(),
                It.IsAny<DateTime?>(),
                It.IsAny<CancellationToken>()))
            .Returns(GetUserCall(response));

    private static void SetupGetUserFault(Mock<CoreCapabilities.CoreCapabilitiesClient> client, RpcException exception) =>
        client
            .Setup(c => c.GetUserAsync(
                It.IsAny<GetUserRequest>(),
                It.IsAny<Metadata>(),
                It.IsAny<DateTime?>(),
                It.IsAny<CancellationToken>()))
            .Returns(FaultedGetUserCall(exception));

    private static void SetupSearchUsers(Mock<CoreCapabilities.CoreCapabilitiesClient> client, params UserInfo[] users) =>
        client
            .Setup(c => c.SearchUsersAsync(
                It.IsAny<SearchUsersRequest>(),
                It.IsAny<Metadata>(),
                It.IsAny<DateTime?>(),
                It.IsAny<CancellationToken>()))
            .Returns(SearchUsersCall(users));

    private static GrpcUserDirectory CreateDirectory(Mock<CoreCapabilities.CoreCapabilitiesClient> client) =>
        new(NullLogger<GrpcUserDirectory>.Instance, client.Object);

    [TestMethod]
    public async Task GetDisplayNamesAsync_UserFound_ReturnsDisplayNameAndSendsModuleIdHeader()
    {
        var userId = Guid.CreateVersion7();
        var client = CreateClient();
        SetupGetUser(client, new GetUserResponse
        {
            Found = true,
            User = new UserInfo { Id = userId.ToString(), DisplayName = "Ada Lovelace" }
        });

        using var directory = CreateDirectory(client);
        var names = await directory.GetDisplayNamesAsync([userId]);

        Assert.AreEqual(1, names.Count);
        Assert.AreEqual("Ada Lovelace", names[userId]);
        client.Verify(c => c.GetUserAsync(
            It.Is<GetUserRequest>(r => r.UserId == userId.ToString()),
            It.Is<Metadata>(m => m.GetValue("module-id") == TestModuleId),
            It.IsAny<DateTime?>(),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [TestMethod]
    public async Task GetDisplayNamesAsync_UserNotFound_OmitsUser()
    {
        var userId = Guid.CreateVersion7();
        var client = CreateClient();
        SetupGetUser(client, new GetUserResponse { Found = false });

        using var directory = CreateDirectory(client);
        var names = await directory.GetDisplayNamesAsync([userId]);

        Assert.AreEqual(0, names.Count);
    }

    [TestMethod]
    public async Task GetDisplayNamesAsync_CoreUnreachable_ReturnsEmptyWithoutThrowing()
    {
        var userId = Guid.CreateVersion7();
        var client = CreateClient();
        SetupGetUserFault(client, new RpcException(new Status(StatusCode.Unavailable, "core down")));

        using var directory = CreateDirectory(client);
        var names = await directory.GetDisplayNamesAsync([userId]);

        Assert.AreEqual(0, names.Count);
    }

    [TestMethod]
    public async Task GetDisplayNamesAsync_MissingModuleIdHeaderRejected_ReturnsEmptyWithoutThrowing()
    {
        var userId = Guid.CreateVersion7();
        var client = CreateClient();
        SetupGetUserFault(client, new RpcException(new Status(
            StatusCode.Unauthenticated, "Missing module-id metadata header")));

        using var directory = CreateDirectory(client);
        var names = await directory.GetDisplayNamesAsync([userId]);

        Assert.AreEqual(0, names.Count);
    }

    [TestMethod]
    public async Task GetAvatarUrlsAsync_UserWithAvatar_ReturnsAvatarUrl()
    {
        var userId = Guid.CreateVersion7();
        var client = CreateClient();
        SetupGetUser(client, new GetUserResponse
        {
            Found = true,
            User = new UserInfo { Id = userId.ToString(), DisplayName = "Ada", AvatarUrl = "/api/v1/users/ada/avatar" }
        });

        using var directory = CreateDirectory(client);
        var avatars = await directory.GetAvatarUrlsAsync([userId]);

        Assert.AreEqual(1, avatars.Count);
        Assert.AreEqual("/api/v1/users/ada/avatar", avatars[userId]);
    }

    [TestMethod]
    public async Task FindUserIdByUsernameAsync_ExactDisplayNameMatch_ReturnsUserId()
    {
        var userId = Guid.CreateVersion7();
        var client = CreateClient();
        SetupSearchUsers(
            client,
            new UserInfo { Id = Guid.CreateVersion7().ToString(), DisplayName = "Adam Smith" },
            new UserInfo { Id = userId.ToString(), DisplayName = "Ada Smith" });

        using var directory = CreateDirectory(client);
        var found = await directory.FindUserIdByUsernameAsync("Ada Smith");

        Assert.AreEqual(userId, found);
        client.Verify(c => c.SearchUsersAsync(
            It.IsAny<SearchUsersRequest>(),
            It.Is<Metadata>(m => m.GetValue("module-id") == TestModuleId),
            It.IsAny<DateTime?>(),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [TestMethod]
    public async Task FindUserIdByUsernameAsync_NoExactMatch_ReturnsNull()
    {
        var client = CreateClient();
        SetupSearchUsers(client, new UserInfo { Id = Guid.CreateVersion7().ToString(), DisplayName = "Adam Smith" });

        using var directory = CreateDirectory(client);
        var found = await directory.FindUserIdByUsernameAsync("Ada Smith");

        Assert.IsNull(found);
    }

    [TestMethod]
    public async Task SearchUsersAsync_MapsResults()
    {
        var userId = Guid.CreateVersion7();
        var client = CreateClient();
        SetupSearchUsers(client, new UserInfo { Id = userId.ToString(), DisplayName = "Ada", Email = "ada@example.com" });

        using var directory = CreateDirectory(client);
        var results = await directory.SearchUsersAsync("ada");

        Assert.AreEqual(1, results.Count);
        Assert.AreEqual(userId, results[0].Id);
        Assert.AreEqual("Ada", results[0].DisplayName);
        Assert.AreEqual("ada@example.com", results[0].Email);
    }

    [TestMethod]
    public async Task SearchUsersAsync_EmptyTerm_ReturnsEmptyWithoutCallingCore()
    {
        var client = CreateClient();

        using var directory = CreateDirectory(client);
        var results = await directory.SearchUsersAsync("   ");

        Assert.AreEqual(0, results.Count);
        client.Verify(c => c.SearchUsersAsync(
            It.IsAny<SearchUsersRequest>(),
            It.IsAny<Metadata>(),
            It.IsAny<DateTime?>(),
            It.IsAny<CancellationToken>()), Times.Never);
    }
}
