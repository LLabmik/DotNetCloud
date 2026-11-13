using DotNetCloud.Core;
using DotNetCloud.Core.Authorization;
using DotNetCloud.Core.Capabilities;
using DotNetCloud.Core.Server.Quotas;
using Grpc.Core;
using Grpc.Core.Interceptors;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace DotNetCloud.Core.Server.Grpc.Interceptors;

/// <summary>
/// Enforces the per-module API rate limits from the effective quota (F10, §4.11).
/// </summary>
/// <remarks>
/// <para>
/// Every capability RPC consumes the module's <c>RequestsPerMinute</c> budget; <c>PublishEvent</c>,
/// <c>SendNotification</c> and <c>LogAudit</c> additionally consume their category budget. Limits
/// are resolved through <see cref="ModuleQuotaCache"/>, so the hot path never touches the
/// database.
/// </para>
/// <para>
/// A rejected call throws <see cref="StatusCode.ResourceExhausted"/> with the stable
/// <c>MODULE_RATE_LIMITED</c> code. Rejections are logged and audited once per window per module
/// and category — never per call — so a runaway module cannot amplify load through the audit path.
/// </para>
/// </remarks>
internal sealed class ModuleRateLimitInterceptor : Interceptor
{
    private static readonly Dictionary<string, ModuleRateLimitKind> MethodRateKinds = new(StringComparer.Ordinal)
    {
        ["PublishEvent"] = ModuleRateLimitKind.Event,
        ["SendNotification"] = ModuleRateLimitKind.Notification,
        ["LogAudit"] = ModuleRateLimitKind.Audit,
    };

    private readonly ModuleQuotaCache _quotas;
    private readonly ModuleRateLimiter _limiter;
    private readonly ILogger<ModuleRateLimitInterceptor> _logger;

    /// <summary>Initializes a new instance of the <see cref="ModuleRateLimitInterceptor"/> class.</summary>
    /// <param name="quotas">Cached effective quotas.</param>
    /// <param name="limiter">The per-module fixed-window limiter.</param>
    /// <param name="logger">Logger.</param>
    public ModuleRateLimitInterceptor(
        ModuleQuotaCache quotas,
        ModuleRateLimiter limiter,
        ILogger<ModuleRateLimitInterceptor> logger)
    {
        _quotas = quotas;
        _limiter = limiter;
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
        if (string.IsNullOrEmpty(moduleId))
        {
            // AuthenticationInterceptor already rejected the call; nothing to meter.
            return;
        }

        var quota = await _quotas.GetAsync(moduleId, context.CancellationToken);

        if (quota.RequestsPerMinute is int requestLimit)
        {
            await EnforceAsync(moduleId, ModuleRateLimitKind.Request, requestLimit, "requests", context);
        }

        if (MethodRateKinds.TryGetValue(GetMethodName(context.Method), out var kind))
        {
            var limit = kind switch
            {
                ModuleRateLimitKind.Event => quota.EventsPerMinute,
                ModuleRateLimitKind.Notification => quota.NotificationsPerMinute,
                _ => quota.AuditEntriesPerMinute,
            };

            if (limit is int categoryLimit)
            {
                await EnforceAsync(moduleId, kind, categoryLimit, kind.ToString().ToLowerInvariant(), context);
            }
        }
    }

    private async Task EnforceAsync(
        string moduleId,
        ModuleRateLimitKind kind,
        int limitPerMinute,
        string category,
        ServerCallContext context)
    {
        var outcome = _limiter.TryAcquire(moduleId, kind, limitPerMinute);
        if (outcome == RateLimitOutcome.Allowed)
        {
            return;
        }

        if (outcome == RateLimitOutcome.LimitedFirstInWindow)
        {
            _logger.LogWarning(
                "Module {ModuleId} exceeded its {Category} quota ({Limit}/min) on {Method}",
                LogSanitizer.Sanitize(moduleId), category, limitPerMinute, context.Method);

            await AuditRateLimitAsync(moduleId, category, limitPerMinute, context);
        }

        throw new RpcException(new Status(
            StatusCode.ResourceExhausted,
            $"MODULE_RATE_LIMITED: the module exceeded its {category} quota ({limitPerMinute}/minute)."));
    }

    /// <summary>
    /// Records the first rejection of a window. Best effort: the audit failure must never replace
    /// the rate-limit error the module needs to see.
    /// </summary>
    private async Task AuditRateLimitAsync(
        string moduleId, string category, int limitPerMinute, ServerCallContext context)
    {
        try
        {
            var services = context.GetHttpContext()?.RequestServices;
            var auditLogger = services?.GetService<IAuditLogger>();
            if (auditLogger is null)
            {
                return;
            }

            await auditLogger.LogAsync(
                new AuditEntry
                {
                    Caller = CallerContext.CreateSystemContext(),
                    ModuleId = "dotnetcloud.core",
                    Action = AuditAction.ModuleRateLimited,
                    EntityType = "Module",
                    EntityId = Guid.CreateVersion7(),
                    Description = $"module-rate-limited:{moduleId}:{category}:{limitPerMinute}/min",
                },
                context.CancellationToken);
        }
        catch (Exception ex)
        {
            // Swallowed on purpose: auditing must never change the outcome of a rejected call.
            _logger.LogDebug(
                ex, "Could not audit the rate-limit rejection for module {ModuleId}", LogSanitizer.Sanitize(moduleId));
        }
    }

    private static string GetMethodName(string method)
    {
        var separator = method.LastIndexOf('/');
        return separator >= 0 && separator < method.Length - 1
            ? method[(separator + 1)..]
            : method;
    }
}
