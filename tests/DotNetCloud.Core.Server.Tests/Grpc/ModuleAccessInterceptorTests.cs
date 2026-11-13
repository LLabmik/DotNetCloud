using DotNetCloud.Core.Authorization;
using DotNetCloud.Core.Capabilities;
using DotNetCloud.Core.Errors;
using DotNetCloud.Core.Server.Grpc.Interceptors;
using DotNetCloud.Core.Server.Grpc.ModuleAccess;
using DotNetCloud.Core.Server.Tests.Services;
using Grpc.Core;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

namespace DotNetCloud.Core.Server.Tests.Grpc;

/// <summary>
/// Tests for <see cref="ModuleAccessInterceptor"/>: a deny must surface as
/// <c>PERMISSION_DENIED</c> with the stable error code, be audited, and never run the handler.
/// </summary>
[TestClass]
public class ModuleAccessInterceptorTests
{
    private const string ModuleId = "dotnetcloud.notes";

    [TestMethod]
    public async Task UnaryServerHandler_AllowedDecision_RunsHandlerWithoutAudit()
    {
        var (interceptor, audit) = CreateInterceptor(ModuleAccessDecision.Allow(nameof(IUserDirectory)));
        var context = CreateContext();

        var result = await interceptor.UnaryServerHandler(
            "request", context, (_, _) => Task.FromResult("ok"));

        Assert.AreEqual("ok", result);
        Assert.AreEqual(0, audit.Entries.Count);
    }

    [TestMethod]
    public async Task UnaryServerHandler_DeniedDecision_ThrowsPermissionDeniedWithStableCode()
    {
        var (interceptor, _) = CreateInterceptor(
            ModuleAccessDecision.Deny(nameof(IAuditLogger), ModuleAccessDenialReason.CapabilityNotGranted));
        var context = CreateContext();

        var ex = await Assert.ThrowsExactlyAsync<RpcException>(
            () => interceptor.UnaryServerHandler("request", context, (_, _) => Task.FromResult("ok")));

        Assert.AreEqual(StatusCode.PermissionDenied, ex.StatusCode);
        Assert.AreEqual(ErrorCodes.CapabilityNotGranted, ex.Trailers.GetValue("error-code"));
        StringAssert.Contains(ex.Status.Detail, nameof(IAuditLogger));
    }

    [TestMethod]
    public async Task UnaryServerHandler_DeniedDecision_WritesAuditEntryAndDoesNotRunHandler()
    {
        var (interceptor, audit) = CreateInterceptor(
            ModuleAccessDecision.Deny(nameof(IAuditLogger), ModuleAccessDenialReason.CapabilityNotGranted));
        var context = CreateContext();

        var handlerRan = false;

        await Assert.ThrowsExactlyAsync<RpcException>(
            () => interceptor.UnaryServerHandler(
                "request",
                context,
                (_, _) =>
                {
                    handlerRan = true;
                    return Task.FromResult("ok");
                }));

        Assert.IsFalse(handlerRan);
        Assert.AreEqual(1, audit.Entries.Count);
        Assert.AreEqual(AuditAction.ModuleCapabilityDenied, audit.Entries[0].Action);
        Assert.AreEqual(ModuleId, audit.Entries[0].ModuleId);
        Assert.AreEqual("ModuleAccess", audit.Entries[0].EntityType);
    }

    [TestMethod]
    public async Task UnaryServerHandler_UnmappedRpc_Denies()
    {
        var (interceptor, audit) = CreateInterceptor(
            ModuleAccessDecision.Deny(null, ModuleAccessDenialReason.UnmappedRpc));

        var ex = await Assert.ThrowsExactlyAsync<RpcException>(
            () => interceptor.UnaryServerHandler("request", CreateContext(), (_, _) => Task.FromResult("ok")));

        Assert.AreEqual(StatusCode.PermissionDenied, ex.StatusCode);
        StringAssert.Contains(ex.Status.Detail, "no capability mapping");
        Assert.AreEqual(1, audit.Entries.Count);
    }

    [TestMethod]
    public async Task UnaryServerHandler_AuditFailure_StillDenies()
    {
        var services = new ServiceCollection();
        services.AddSingleton<IAuditLogger>(new ThrowingAuditLogger());

        using var provider = services.BuildServiceProvider();
        var interceptor = new ModuleAccessInterceptor(
            new StubEvaluator(ModuleAccessDecision.Deny(nameof(IAuditLogger), ModuleAccessDenialReason.CapabilityNotGranted)),
            provider.GetRequiredService<IServiceScopeFactory>(),
            NullLogger<ModuleAccessInterceptor>.Instance);

        var ex = await Assert.ThrowsExactlyAsync<RpcException>(
            () => interceptor.UnaryServerHandler("request", CreateContext(), (_, _) => Task.FromResult("ok")));

        Assert.AreEqual(StatusCode.PermissionDenied, ex.StatusCode);
    }

    private static (ModuleAccessInterceptor Interceptor, RecordingAuditLogger Audit) CreateInterceptor(
        ModuleAccessDecision decision)
    {
        var audit = new RecordingAuditLogger();
        var services = new ServiceCollection();
        services.AddSingleton<IAuditLogger>(audit);

        var provider = services.BuildServiceProvider();
        var interceptor = new ModuleAccessInterceptor(
            new StubEvaluator(decision),
            provider.GetRequiredService<IServiceScopeFactory>(),
            NullLogger<ModuleAccessInterceptor>.Instance);

        return (interceptor, audit);
    }

    private static TestServerCallContext CreateContext()
    {
        var context = new TestServerCallContext(
            CapabilityCatalog.Rpc(CapabilityCatalog.ModuleApiService, "LogAudit"),
            []);
        context.UserState["ModuleId"] = ModuleId;
        context.UserState["CallerContext"] = CallerContext.CreateSystemContext();
        return context;
    }

    private sealed class StubEvaluator(ModuleAccessDecision decision) : IModuleAccessEvaluator
    {
        public Task<ModuleAccessDecision> EvaluateAsync(
            string? moduleId,
            string? rpcMethod,
            CancellationToken cancellationToken = default) => Task.FromResult(decision);

        public void Invalidate(string moduleId)
        {
        }

        public void InvalidateAll()
        {
        }
    }

    private sealed class RecordingAuditLogger : IAuditLogger
    {
        public List<AuditEntry> Entries { get; } = [];

        public Task LogAsync(AuditEntry entry, CancellationToken cancellationToken = default)
        {
            Entries.Add(entry);
            return Task.CompletedTask;
        }
    }

    private sealed class ThrowingAuditLogger : IAuditLogger
    {
        public Task LogAsync(AuditEntry entry, CancellationToken cancellationToken = default) =>
            throw new System.InvalidOperationException("audit store unavailable");
    }
}
