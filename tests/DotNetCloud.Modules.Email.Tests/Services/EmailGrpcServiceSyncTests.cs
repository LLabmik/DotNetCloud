using DotNetCloud.Core.Grpc;
using DotNetCloud.Modules.Email.Data;
using DotNetCloud.Modules.Email.Host.Protos;
using DotNetCloud.Modules.Email.Host.Services;
using DotNetCloud.Modules.Email.Models;
using DotNetCloud.Modules.Email.Services;
using Grpc.Core;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace DotNetCloud.Modules.Email.Tests.Services;

/// <summary>
/// Tests for the core-driven sync surface of <see cref="EmailGrpcService"/>.
/// </summary>
/// <remarks>
/// The Email module dropped its own sync timer, so <c>TriggerSync</c> is now the only way an account
/// is synced. It must run as a user the core signed for: a call with no assertion, or one whose
/// payload names somebody other than the asserted user, is refused outright.
/// </remarks>
[TestClass]
public class EmailGrpcServiceSyncTests
{
    private static EmailGrpcService CreateService(
        Mock<IModuleUserAssertionAccessor> accessor,
        Mock<IEmailSyncService>? syncService = null)
    {
        var options = new DbContextOptionsBuilder<EmailDbContext>()
            .UseInMemoryDatabase(Guid.CreateVersion7().ToString())
            .Options;

        return new EmailGrpcService(
            Mock.Of<IEmailAccountService>(),
            Mock.Of<IEmailSendService>(),
            Mock.Of<IEmailRuleService>(),
            (syncService ?? new Mock<IEmailSyncService>()).Object,
            Mock.Of<IAttachmentStorage>(),
            accessor.Object,
            new EmailDbContext(options),
            new ConfigurationBuilder().Build(),
            NullLogger<EmailGrpcService>.Instance);
    }

    private static Mock<IModuleUserAssertionAccessor> AssertedAs(Guid userId)
    {
        var accessor = new Mock<IModuleUserAssertionAccessor>();
        accessor.Setup(a => a.GetCurrentUserId()).Returns(userId);
        return accessor;
    }

    private static Mock<IModuleUserAssertionAccessor> NoAssertion()
    {
        var accessor = new Mock<IModuleUserAssertionAccessor>();
        accessor.Setup(a => a.GetCurrentUserId()).Returns((Guid?)null);
        return accessor;
    }

    [TestMethod]
    public async Task TriggerSync_NoCoreAssertion_ThrowsPermissionDenied()
    {
        var syncService = new Mock<IEmailSyncService>();
        var service = CreateService(NoAssertion(), syncService);

        var ex = await Assert.ThrowsExactlyAsync<RpcException>(() => service.TriggerSync(
            new TriggerSyncRequest
            {
                AccountId = Guid.CreateVersion7().ToString(),
                UserId = Guid.CreateVersion7().ToString()
            },
            TestServerCallContext.Create()));

        Assert.AreEqual(StatusCode.PermissionDenied, ex.StatusCode);
        syncService.Verify(
            s => s.SyncAccountAsync(
                It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [TestMethod]
    public async Task TriggerSync_PayloadNamesAnotherUser_ThrowsPermissionDenied()
    {
        var assertedUserId = Guid.CreateVersion7();
        var syncService = new Mock<IEmailSyncService>();
        var service = CreateService(AssertedAs(assertedUserId), syncService);

        var ex = await Assert.ThrowsExactlyAsync<RpcException>(() => service.TriggerSync(
            new TriggerSyncRequest
            {
                AccountId = Guid.CreateVersion7().ToString(),
                UserId = Guid.CreateVersion7().ToString()
            },
            TestServerCallContext.Create()));

        Assert.AreEqual(StatusCode.PermissionDenied, ex.StatusCode);
        syncService.Verify(
            s => s.SyncAccountAsync(
                It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [TestMethod]
    public async Task TriggerSync_AssertedUser_SyncsThatUser()
    {
        var assertedUserId = Guid.CreateVersion7();
        var accountId = Guid.CreateVersion7();

        var syncService = new Mock<IEmailSyncService>();
        syncService
            .Setup(s => s.SyncAccountAsync(accountId, assertedUserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var service = CreateService(AssertedAs(assertedUserId), syncService);

        var response = await service.TriggerSync(
            new TriggerSyncRequest
            {
                AccountId = accountId.ToString(),
                UserId = assertedUserId.ToString()
            },
            TestServerCallContext.Create());

        Assert.IsTrue(response.Success);
        syncService.Verify(
            s => s.SyncAccountAsync(accountId, assertedUserId, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [TestMethod]
    public async Task TriggerSync_AccountNotOwnedByAssertedUser_ReportsFailure()
    {
        var assertedUserId = Guid.CreateVersion7();
        var accountId = Guid.CreateVersion7();

        var syncService = new Mock<IEmailSyncService>();
        syncService
            .Setup(s => s.SyncAccountAsync(accountId, assertedUserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        var service = CreateService(AssertedAs(assertedUserId), syncService);

        var response = await service.TriggerSync(
            new TriggerSyncRequest
            {
                AccountId = accountId.ToString(),
                UserId = assertedUserId.ToString()
            },
            TestServerCallContext.Create());

        Assert.IsFalse(response.Success);
        StringAssert.Contains(response.ErrorMessage, "not found");
    }

    [TestMethod]
    public async Task ListSyncCandidates_MapsReportedAccounts()
    {
        var accountId = Guid.CreateVersion7();
        var ownerId = Guid.CreateVersion7();
        var syncedAt = new DateTime(2026, 10, 25, 6, 0, 0, DateTimeKind.Utc);

        var syncService = new Mock<IEmailSyncService>();
        syncService
            .Setup(s => s.ListSyncCandidatesAsync(TimeSpan.FromMinutes(5), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { new EmailSyncCandidate(accountId, ownerId, syncedAt) });

        var service = CreateService(NoAssertion(), syncService);

        var response = await service.ListSyncCandidates(
            new ListSyncCandidatesRequest { MinAgeSeconds = 300 },
            TestServerCallContext.Create());

        Assert.IsTrue(response.Success);
        Assert.AreEqual(1, response.Candidates.Count);
        Assert.AreEqual(accountId.ToString(), response.Candidates[0].AccountId);
        Assert.AreEqual(ownerId.ToString(), response.Candidates[0].UserId);
        Assert.AreEqual(syncedAt.ToString("O"), response.Candidates[0].LastSyncedAt);
    }
}

/// <summary>
/// Minimal <see cref="ServerCallContext"/> for unit-testing the Email gRPC service.
/// </summary>
internal sealed class TestServerCallContext : ServerCallContext
{
    public static TestServerCallContext Create() => new();

    protected override string MethodCore => "test";
    protected override string HostCore => "localhost";
    protected override string PeerCore => "test";
    protected override DateTime DeadlineCore => DateTime.MaxValue;
    protected override Metadata RequestHeadersCore => [];
    protected override CancellationToken CancellationTokenCore => CancellationToken.None;
    protected override Metadata ResponseTrailersCore => [];
    protected override Status StatusCore { get; set; } = Status.DefaultSuccess;
    protected override WriteOptions? WriteOptionsCore { get; set; }
    protected override AuthContext AuthContextCore => new("test", []);

    protected override ContextPropagationToken CreatePropagationTokenCore(ContextPropagationOptions? options)
        => throw new NotSupportedException();

    protected override Task WriteResponseHeadersAsyncCore(Metadata responseHeaders)
        => Task.CompletedTask;
}
