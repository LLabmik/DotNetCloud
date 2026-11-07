using DotNetCloud.Core.Authorization;
using DotNetCloud.Core.Grpc;
using DotNetCloud.Core.Server.Grpc.Interceptors;
using DotNetCloud.Core.Server.Supervisor;
using DotNetCloud.Core.Server.Tests.Services;
using Grpc.Core;
using Microsoft.Extensions.Logging.Abstractions;

namespace DotNetCloud.Core.Server.Tests.Grpc;

/// <summary>
/// Tests for the module transport identity: the core must only accept capability calls that carry
/// the credential the process supervisor issued for the calling module, and it must never let a
/// caller claim a system identity or a caller type it did not earn.
/// </summary>
/// <remarks>
/// The audit finding was that a cleartext loopback listener accepted any non-empty
/// <c>module-id</c> header, so an arbitrary local process could read every user's directory data,
/// write notifications into other principals' sessions and forge audit attribution.
/// </remarks>
[TestClass]
public class ModuleTransportAuthenticationTests
{
    private const string ModuleId = "dotnetcloud.files";

    [TestMethod]
    public async Task Authenticate_MissingModuleId_ThrowsUnauthenticated()
    {
        var interceptor = CreateAuthenticationInterceptor(out _);
        var context = CreateContext(new Metadata { { ModuleIdentityMetadata.ModuleTokenHeader, "token" } });

        var ex = await Assert.ThrowsExactlyAsync<RpcException>(
            () => interceptor.UnaryServerHandler("request", context, (_, _) => Task.FromResult("ok")));

        Assert.AreEqual(StatusCode.Unauthenticated, ex.StatusCode);
    }

    [TestMethod]
    public async Task Authenticate_MissingToken_ThrowsUnauthenticated()
    {
        var interceptor = CreateAuthenticationInterceptor(out _);
        var context = CreateContext(new Metadata { { ModuleIdentityMetadata.ModuleIdHeader, ModuleId } });

        var ex = await Assert.ThrowsExactlyAsync<RpcException>(
            () => interceptor.UnaryServerHandler("request", context, (_, _) => Task.FromResult("ok")));

        Assert.AreEqual(StatusCode.Unauthenticated, ex.StatusCode);
    }

    [TestMethod]
    public async Task Authenticate_TokenForAnotherModule_ThrowsUnauthenticated()
    {
        var interceptor = CreateAuthenticationInterceptor(out var store);
        var foreignToken = store.Issue("dotnetcloud.chat");

        var context = CreateContext(new Metadata
        {
            { ModuleIdentityMetadata.ModuleIdHeader, ModuleId },
            { ModuleIdentityMetadata.ModuleTokenHeader, foreignToken },
        });

        var ex = await Assert.ThrowsExactlyAsync<RpcException>(
            () => interceptor.UnaryServerHandler("request", context, (_, _) => Task.FromResult("ok")));

        Assert.AreEqual(StatusCode.Unauthenticated, ex.StatusCode);
    }

    [TestMethod]
    public async Task Authenticate_RevokedCredential_ThrowsUnauthenticated()
    {
        var interceptor = CreateAuthenticationInterceptor(out var store);
        var token = store.Issue(ModuleId);
        store.Revoke(ModuleId);

        var context = CreateContext(new Metadata
        {
            { ModuleIdentityMetadata.ModuleIdHeader, ModuleId },
            { ModuleIdentityMetadata.ModuleTokenHeader, token },
        });

        var ex = await Assert.ThrowsExactlyAsync<RpcException>(
            () => interceptor.UnaryServerHandler("request", context, (_, _) => Task.FromResult("ok")));

        Assert.AreEqual(StatusCode.Unauthenticated, ex.StatusCode);
    }

    [TestMethod]
    public async Task Authenticate_ValidCredential_ContinuesAndRecordsModuleId()
    {
        var interceptor = CreateAuthenticationInterceptor(out var store);
        var token = store.Issue(ModuleId);

        var context = CreateContext(new Metadata
        {
            { ModuleIdentityMetadata.ModuleIdHeader, ModuleId },
            { ModuleIdentityMetadata.ModuleTokenHeader, token },
        });

        var result = await interceptor.UnaryServerHandler("request", context, (_, _) => Task.FromResult("ok"));

        Assert.AreEqual("ok", result);
        Assert.AreEqual(ModuleId, context.UserState["ModuleId"]);
    }

    [TestMethod]
    public void InjectCallerContext_CallerTypeSystemHeaderWithoutUser_IsDerivedAsModule()
    {
        var interceptor = CreateCallerContextInterceptor(out _, out _, null);
        var context = CreateAuthenticatedContext(
            ModuleId,
            new Metadata { { "caller-type", "System" } });

        interceptor.UnaryServerHandler("request", context, (_, _) => Task.FromResult("ok"))
            .GetAwaiter().GetResult();

        var caller = (CallerContext)context.UserState["CallerContext"];
        Assert.AreEqual(CallerType.Module, caller.Type);
        Assert.AreNotEqual(Guid.Empty, caller.UserId);
    }

    [TestMethod]
    public void InjectCallerContext_CallerTypeSystemHeaderWithUser_IsDerivedAsUser()
    {
        var userId = Guid.CreateVersion7();
        var interceptor = CreateCallerContextInterceptor(out _, out var keyRing, userId);
        var context = CreateAuthenticatedContext(
            ModuleId,
            new Metadata
            {
                { ModuleIdentityMetadata.UserAssertionHeader, keyRing.Mint(userId, ModuleId) },
                { "caller-type", "System" },
                { "caller-roles", "Administrator" },
            });

        interceptor.UnaryServerHandler("request", context, (_, _) => Task.FromResult("ok"))
            .GetAwaiter().GetResult();

        var caller = (CallerContext)context.UserState["CallerContext"];
        Assert.AreEqual(CallerType.User, caller.Type);
        Assert.AreEqual(userId, caller.UserId);
        Assert.IsNotNull(context.UserState["UserAssertion"]);
    }

    [TestMethod]
    public void InjectCallerContext_CallerRolesHeader_IsIgnoredInFavourOfStoredRoles()
    {
        var userId = Guid.CreateVersion7();
        var interceptor = CreateCallerContextInterceptor(out _, out var keyRing, userId);
        var context = CreateAuthenticatedContext(
            ModuleId,
            new Metadata
            {
                { ModuleIdentityMetadata.UserAssertionHeader, keyRing.Mint(userId, ModuleId) },
                { "caller-roles", "Administrator" },
            });

        interceptor.UnaryServerHandler("request", context, (_, _) => Task.FromResult("ok"))
            .GetAwaiter().GetResult();

        var caller = (CallerContext)context.UserState["CallerContext"];
        Assert.IsFalse(caller.HasRole("Administrator"),
            "A module must not be able to widen the end user's roles over the wire.");
        Assert.IsTrue(caller.HasRole("User"));
    }

    [TestMethod]
    public void InjectCallerContext_UnknownOrInactiveEndUser_ThrowsUnauthenticated()
    {
        var userId = Guid.CreateVersion7();
        var interceptor = CreateCallerContextInterceptor(out _, out var keyRing, null);
        var context = CreateAuthenticatedContext(
            ModuleId,
            new Metadata
            {
                { ModuleIdentityMetadata.UserAssertionHeader, keyRing.Mint(userId, ModuleId) },
            });

        var ex = Assert.ThrowsExactlyAsync<RpcException>(
            () => interceptor.UnaryServerHandler("request", context, (_, _) => Task.FromResult("ok")))
            .GetAwaiter().GetResult();

        Assert.AreEqual(StatusCode.Unauthenticated, ex.StatusCode);
    }

    [TestMethod]
    public void InjectCallerContext_AssertionScopedToAnotherModule_IsRejected()
    {
        // A Files assertion must not be replayable by Chat.
        var userId = Guid.CreateVersion7();
        var interceptor = CreateCallerContextInterceptor(out _, out var keyRing, userId);
        var context = CreateAuthenticatedContext(
            ModuleId,
            new Metadata
            {
                { ModuleIdentityMetadata.UserAssertionHeader, keyRing.Mint(userId, "dotnetcloud.chat") },
            });

        var ex = Assert.ThrowsExactlyAsync<RpcException>(
            () => interceptor.UnaryServerHandler("request", context, (_, _) => Task.FromResult("ok")))
            .GetAwaiter().GetResult();

        Assert.AreEqual(StatusCode.Unauthenticated, ex.StatusCode);
    }

    [TestMethod]
    public void InjectCallerContext_ExpiredAssertion_IsRejected()
    {
        var userId = Guid.CreateVersion7();
        var interceptor = CreateCallerContextInterceptor(out _, out var keyRing, userId);
        var expired = keyRing.Mint(userId, ModuleId, lifetime: TimeSpan.FromSeconds(-1));

        var context = CreateAuthenticatedContext(
            ModuleId,
            new Metadata { { ModuleIdentityMetadata.UserAssertionHeader, expired } });

        var ex = Assert.ThrowsExactlyAsync<RpcException>(
            () => interceptor.UnaryServerHandler("request", context, (_, _) => Task.FromResult("ok")))
            .GetAwaiter().GetResult();

        Assert.AreEqual(StatusCode.Unauthenticated, ex.StatusCode);
    }

    [TestMethod]
    public void InjectCallerContext_TamperedAssertion_IsRejected()
    {
        var userId = Guid.CreateVersion7();
        var interceptor = CreateCallerContextInterceptor(out _, out var keyRing, userId);

        // Sign a different user, then splice that signature onto a payload naming the victim.
        var otherUser = Guid.CreateVersion7();
        var genuine = keyRing.Mint(otherUser, ModuleId);
        var forged = keyRing.Mint(userId, ModuleId);
        var tampered = forged.Split('.')[0] + "." + genuine.Split('.')[1];

        var context = CreateAuthenticatedContext(
            ModuleId,
            new Metadata { { ModuleIdentityMetadata.UserAssertionHeader, tampered } });

        var ex = Assert.ThrowsExactlyAsync<RpcException>(
            () => interceptor.UnaryServerHandler("request", context, (_, _) => Task.FromResult("ok")))
            .GetAwaiter().GetResult();

        Assert.AreEqual(StatusCode.Unauthenticated, ex.StatusCode);
    }

    [TestMethod]
    [DoNotParallelize]
    public async Task Authenticate_MetadataBuiltByModuleIdentityMetadata_IsAccepted()
    {
        // Guards against header/env drift between the producer used by every module host and the
        // core's consumer: renaming one side without the other must fail this test.
        var interceptor = CreateAuthenticationInterceptor(out var store);
        var token = store.Issue(ModuleId);
        var previous = Environment.GetEnvironmentVariable(ModuleIdentityMetadata.ModuleTokenEnvironmentVariable);

        try
        {
            Environment.SetEnvironmentVariable(ModuleIdentityMetadata.ModuleTokenEnvironmentVariable, token);

            var context = CreateContext(ModuleIdentityMetadata.Create(ModuleId));

            var result = await interceptor.UnaryServerHandler(
                "request", context, (_, _) => Task.FromResult("ok"));

            Assert.AreEqual("ok", result);
            Assert.AreEqual(ModuleId, context.UserState["ModuleId"]);
        }
        finally
        {
            Environment.SetEnvironmentVariable(
                ModuleIdentityMetadata.ModuleTokenEnvironmentVariable, previous);
        }
    }

    [TestMethod]
    [DoNotParallelize]
    public async Task ModulePeerAuthentication_MetadataBuiltByModuleIdentityMetadata_IsAccepted()
    {
        // The core→module direction reads the same token variable; both sides must agree on it.
        var previous = Environment.GetEnvironmentVariable(ModuleIdentityMetadata.ModuleTokenEnvironmentVariable);

        try
        {
            Environment.SetEnvironmentVariable(
                ModuleIdentityMetadata.ModuleTokenEnvironmentVariable, "peer-token");

            var interceptor = new ModulePeerAuthenticationInterceptor(
                NullLogger<ModulePeerAuthenticationInterceptor>.Instance);
            var context = CreateContext(ModuleIdentityMetadata.Create(ModuleId));

            var result = await interceptor.UnaryServerHandler(
                "request", context, (_, _) => Task.FromResult("ok"));

            Assert.AreEqual("ok", result);
        }
        finally
        {
            Environment.SetEnvironmentVariable(
                ModuleIdentityMetadata.ModuleTokenEnvironmentVariable, previous);
        }
    }

    [TestMethod]
    public void InjectCallerContext_ModuleCallerWithoutEndUser_HasNoInheritedRoles()
    {
        var interceptor = CreateCallerContextInterceptor(out _, out _, Guid.CreateVersion7());
        var context = CreateAuthenticatedContext(
            ModuleId,
            new Metadata { { "caller-roles", "Administrator" } });

        interceptor.UnaryServerHandler("request", context, (_, _) => Task.FromResult("ok"))
            .GetAwaiter().GetResult();

        var caller = (CallerContext)context.UserState["CallerContext"];
        Assert.AreEqual(CallerType.Module, caller.Type);
        Assert.AreEqual(0, caller.Roles.Count);
    }

    [TestMethod]
    public void ToUserId_SameModuleId_IsStable()
    {
        Assert.AreEqual(
            ModuleCallerIdentity.ToUserId(ModuleId),
            ModuleCallerIdentity.ToUserId(ModuleId));
        Assert.AreNotEqual(
            ModuleCallerIdentity.ToUserId(ModuleId),
            ModuleCallerIdentity.ToUserId("dotnetcloud.chat"));
    }

    private static AuthenticationInterceptor CreateAuthenticationInterceptor(out ModuleCredentialStore store)
    {
        store = new ModuleCredentialStore();
        return new AuthenticationInterceptor(NullLogger<AuthenticationInterceptor>.Instance, store);
    }

    private static CallerContextInterceptor CreateCallerContextInterceptor(
        out ICallerIdentityResolver resolver,
        out ModuleAssertionKeyRing keyRing,
        Guid? existingUserId)
    {
        resolver = new StubCallerIdentityResolver(existingUserId);
        keyRing = new ModuleAssertionKeyRing();
        return new CallerContextInterceptor(
            NullLogger<CallerContextInterceptor>.Instance,
            resolver,
            keyRing);
    }

    /// <summary>
    /// Minimal identity resolver double: the stamped user is the only known, active account and
    /// it holds the plain <c>User</c> role.
    /// </summary>
    private sealed class StubCallerIdentityResolver : ICallerIdentityResolver
    {
        private readonly Guid? _existingUserId;

        public StubCallerIdentityResolver(Guid? existingUserId) => _existingUserId = existingUserId;

        public Task<IReadOnlyList<string>?> ResolveRolesAsync(Guid userId, CancellationToken cancellationToken = default)
        {
            IReadOnlyList<string>? roles = _existingUserId == userId ? ["User"] : null;
            return Task.FromResult(roles);
        }
    }

    private static TestServerCallContext CreateContext(Metadata headers) =>
        new("/dotnetcloud.core.capabilities.CoreCapabilities/GetUser", headers);

    private static TestServerCallContext CreateAuthenticatedContext(string moduleId, Metadata headers)
    {
        var context = CreateContext(headers);
        context.UserState["ModuleId"] = moduleId;
        return context;
    }
}
