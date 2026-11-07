using DotNetCloud.Core;
using DotNetCloud.Core.Authorization;
using DotNetCloud.Core.Capabilities;
using DotNetCloud.Core.Errors;
using DotNetCloud.Core.Server.Grpc.ModuleAccess;
using Grpc.Core;
using Grpc.Core.Interceptors;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace DotNetCloud.Core.Server.Grpc.Interceptors;

/// <summary>
/// gRPC interceptor that enforces module access decisions from <see cref="IModuleAccessEvaluator"/>.
/// </summary>
/// <remarks>
/// <para>
/// Thin adapter: the evaluator decides (catalog lookup + cached grant set), this interceptor turns
/// a deny into <c>PERMISSION_DENIED</c> with <c>error-code: CAPABILITY_NOT_GRANTED</c> and writes an
/// audit entry. Keeping policy out of the interceptor is what lets the internal REST facade reuse
/// the same decision.
/// </para>
/// <para>
/// Module-facing RPCs with no mapping are denied (fail closed) instead of being skipped: an
/// unmapped RPC used to be enforced by nothing.
/// </para>
/// </remarks>
internal sealed class ModuleAccessInterceptor : Interceptor
{
    private readonly IModuleAccessEvaluator _evaluator;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<ModuleAccessInterceptor> _logger;

    /// <summary>Initializes a new instance of the <see cref="ModuleAccessInterceptor"/> class.</summary>
    /// <param name="evaluator">The access evaluator.</param>
    /// <param name="scopeFactory">Scope factory used to write denial audit entries.</param>
    /// <param name="logger">Logger.</param>
    public ModuleAccessInterceptor(
        IModuleAccessEvaluator evaluator,
        IServiceScopeFactory scopeFactory,
        ILogger<ModuleAccessInterceptor> logger)
    {
        _evaluator = evaluator;
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    /// <inheritdoc />
    public override async Task<TResponse> UnaryServerHandler<TRequest, TResponse>(
        TRequest request,
        ServerCallContext context,
        UnaryServerMethod<TRequest, TResponse> continuation)
    {
        await EnforceAsync(context);
        return await continuation(request, context);
    }

    /// <inheritdoc />
    public override async Task ServerStreamingServerHandler<TRequest, TResponse>(
        TRequest request,
        IServerStreamWriter<TResponse> responseStream,
        ServerCallContext context,
        ServerStreamingServerMethod<TRequest, TResponse> continuation)
    {
        await EnforceAsync(context);
        await continuation(request, responseStream, context);
    }

    /// <inheritdoc />
    public override async Task<TResponse> ClientStreamingServerHandler<TRequest, TResponse>(
        IAsyncStreamReader<TRequest> requestStream,
        ServerCallContext context,
        ClientStreamingServerMethod<TRequest, TResponse> continuation)
    {
        await EnforceAsync(context);
        return await continuation(requestStream, context);
    }

    /// <inheritdoc />
    public override async Task DuplexStreamingServerHandler<TRequest, TResponse>(
        IAsyncStreamReader<TRequest> requestStream,
        IServerStreamWriter<TResponse> responseStream,
        ServerCallContext context,
        DuplexStreamingServerMethod<TRequest, TResponse> continuation)
    {
        await EnforceAsync(context);
        await continuation(requestStream, responseStream, context);
    }

    private async Task EnforceAsync(ServerCallContext context)
    {
        var moduleId = context.UserState.TryGetValue("ModuleId", out var value) ? value as string : null;

        var decision = await _evaluator.EvaluateAsync(moduleId, context.Method, context.CancellationToken);
        if (decision.IsAllowed)
        {
            return;
        }

        await AuditDenialAsync(context, moduleId, decision);
        throw Denied(moduleId, decision);
    }

    private static RpcException Denied(string? moduleId, ModuleAccessDecision decision)
    {
        var subject = decision.Capability ?? "unmapped RPC";
        var reason = decision.DenialReason switch
        {
            ModuleAccessDenialReason.CapabilityNotGranted => "is not granted",
            ModuleAccessDenialReason.CapabilityForbidden => "is forbidden",
            ModuleAccessDenialReason.UnmappedRpc => "has no capability mapping",
            ModuleAccessDenialReason.UnknownModule => "requires an authenticated module identity",
            ModuleAccessDenialReason.EvaluationFailed => "could not be evaluated",
            _ => "is denied",
        };

        return new RpcException(
            new Status(
                StatusCode.PermissionDenied,
                $"[CAPABILITY_NOT_GRANTED] Capability '{subject}' {reason} for module '{moduleId ?? "unknown"}'."),
            new Metadata { { "error-code", ErrorCodes.CapabilityNotGranted } });
    }

    /// <summary>
    /// Writes the denial audit entry. Auditing must never change the outcome: a failure here is
    /// logged and the call stays denied.
    /// </summary>
    private async Task AuditDenialAsync(
        ServerCallContext context,
        string? moduleId,
        ModuleAccessDecision decision)
    {
        _logger.LogWarning(
            "Module {ModuleId} denied {Method} ({Capability}: {Reason})",
            LogSanitizer.Sanitize(moduleId ?? "unknown"),
            context.Method,
            decision.Capability ?? "none",
            decision.DenialReason);

        try
        {
            using var scope = _scopeFactory.CreateScope();
            var auditLogger = scope.ServiceProvider.GetRequiredService<IAuditLogger>();

            var caller = context.UserState.TryGetValue("CallerContext", out var value)
                && value is CallerContext callerContext
                    ? callerContext
                    : CallerContext.CreateSystemContext();

            await auditLogger.LogAsync(
                new AuditEntry
                {
                    Caller = caller,
                    ModuleId = moduleId ?? "unknown",
                    Action = AuditAction.ModuleCapabilityDenied,
                    EntityType = "ModuleAccess",
                    EntityId = Guid.Empty,
                    Description =
                        $"Denied {LogSanitizer.Sanitize(context.Method)} " +
                        $"(capability: {decision.Capability ?? "none"}, reason: {decision.DenialReason})",
                },
                context.CancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogWarning(
                ex,
                "Could not record the capability-denied audit entry for module {ModuleId}",
                LogSanitizer.Sanitize(moduleId ?? "unknown"));
        }
    }
}
