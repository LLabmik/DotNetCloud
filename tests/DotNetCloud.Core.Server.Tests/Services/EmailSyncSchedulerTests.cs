using DotNetCloud.Core.Grpc;
using DotNetCloud.Core.Modules.Supervisor;
using DotNetCloud.Core.Server.Grpc.Clients;
using DotNetCloud.Core.Server.Grpc.Interceptors;
using DotNetCloud.Core.Server.Services;
using DotNetCloud.Core.Server.Supervisor;
using Microsoft.Extensions.Logging.Abstractions;
using EmailProto = DotNetCloud.Modules.Email.Host.Protos;

namespace DotNetCloud.Core.Server.Tests.Services;

/// <summary>
/// Tests for the decisions <see cref="EmailSyncScheduler"/> makes before it invokes the Email module.
/// </summary>
/// <remarks>
/// The scheduler is what replaced the Email module's own sync timer. The security-relevant choices
/// are made here — which candidates are usable, and whether the core is willing to vouch that a user
/// may have their mailboxes read — so those are pinned directly; the RPC itself is exercised by the
/// module-side tests and the live deploy.
/// </remarks>
[TestClass]
public class EmailSyncSchedulerTests
{
    /// <summary>
    /// Identity-store stand-in. <see cref="ICallerIdentityResolver"/> is internal to Core.Server, so
    /// a hand-written fake is used rather than opening the assembly to dynamic proxies.
    /// </summary>
    private sealed class FakeIdentityResolver : ICallerIdentityResolver
    {
        private readonly IReadOnlyList<string>? _roles;
        public int Calls { get; private set; }

        public FakeIdentityResolver(IReadOnlyList<string>? roles) => _roles = roles;

        public Task<IReadOnlyList<string>?> ResolveRolesAsync(
            Guid userId, CancellationToken cancellationToken = default)
        {
            Calls++;
            return Task.FromResult(_roles);
        }
    }

    private static EmailSyncScheduler CreateScheduler(ICallerIdentityResolver identityResolver)
    {
        // The endpoint provider is only touched when a cycle actually calls the module, which these
        // tests do not do — they cover the decisions taken before any RPC.
        var endpointProvider = new ModuleEndpointProvider(
            supervisor: null!,
            channelProvider: null!,
            Microsoft.Extensions.Options.Options.Create(new ProcessSupervisorOptions()),
            NullLogger<ModuleEndpointProvider>.Instance);

        return new EmailSyncScheduler(
            endpointProvider,
            new ModuleAssertionKeyRing(),
            identityResolver,
            NullLogger<EmailSyncScheduler>.Instance);
    }

    private static EmailProto.SyncCandidate CreateCandidate(
        string? accountId = null, string? userId = null) => new()
        {
            AccountId = accountId ?? Guid.CreateVersion7().ToString(),
            UserId = userId ?? Guid.CreateVersion7().ToString()
        };

    [TestMethod]
    public void TryParseCandidate_ValidIds_ReturnsTrue()
    {
        var candidate = CreateCandidate();

        var parsed = EmailSyncScheduler.TryParseCandidate(candidate, out var accountId, out var userId);

        Assert.IsTrue(parsed);
        Assert.AreEqual(candidate.AccountId, accountId.ToString());
        Assert.AreEqual(candidate.UserId, userId.ToString());
    }

    [TestMethod]
    public void TryParseCandidate_EmptyUserId_ReturnsFalse()
    {
        var candidate = CreateCandidate(userId: Guid.Empty.ToString());

        Assert.IsFalse(EmailSyncScheduler.TryParseCandidate(candidate, out _, out _));
    }

    [TestMethod]
    public void TryParseCandidate_UnparsableAccountId_ReturnsFalse()
    {
        var candidate = CreateCandidate(accountId: "not-a-guid");

        Assert.IsFalse(EmailSyncScheduler.TryParseCandidate(candidate, out _, out _));
    }

    [TestMethod]
    public async Task CanActForUserAsync_UnknownOrInactiveUser_ReturnsFalse()
    {
        var scheduler = CreateScheduler(new FakeIdentityResolver(roles: null));

        Assert.IsFalse(await scheduler.CanActForUserAsync(Guid.CreateVersion7(), CancellationToken.None));
    }

    [TestMethod]
    public async Task CanActForUserAsync_EmptyUserId_DoesNotQueryTheIdentityStore()
    {
        var identityResolver = new FakeIdentityResolver(new[] { "user" });
        var scheduler = CreateScheduler(identityResolver);

        Assert.IsFalse(await scheduler.CanActForUserAsync(Guid.Empty, CancellationToken.None));
        Assert.AreEqual(0, identityResolver.Calls, "An empty user id must not reach the identity store.");
    }

    [TestMethod]
    public async Task CanActForUserAsync_ActiveUser_ReturnsTrue()
    {
        var scheduler = CreateScheduler(new FakeIdentityResolver(new[] { "user" }));

        Assert.IsTrue(await scheduler.CanActForUserAsync(Guid.CreateVersion7(), CancellationToken.None));
    }

    [TestMethod]
    public void Mint_EmailSyncAssertion_VerifiesOnlyForTheEmailModule()
    {
        using var keyRing = new ModuleAssertionKeyRing();
        using var publicKey = ModuleUserAssertions.TryImportPublicKey(keyRing.PublicKeyBase64);
        Assert.IsNotNull(publicKey);

        var userId = Guid.CreateVersion7();
        var token = keyRing.Mint(userId, EmailSyncScheduler.ModuleId, operation: "email.sync");

        Assert.IsTrue(ModuleUserAssertions.TryVerify(
            token, publicKey, EmailSyncScheduler.ModuleId, DateTimeOffset.UtcNow, out var assertion));
        Assert.AreEqual(userId, assertion!.UserId);
        Assert.AreEqual("email.sync", assertion.Operation);

        // The same assertion must be useless to any other module.
        Assert.IsFalse(ModuleUserAssertions.TryVerify(
            token, publicKey, "dotnetcloud.files", DateTimeOffset.UtcNow, out _));
    }
}
