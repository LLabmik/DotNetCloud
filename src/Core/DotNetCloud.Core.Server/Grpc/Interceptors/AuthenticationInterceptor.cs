using System.Collections.Concurrent;
using System.Security.Claims;
using DotNetCloud.Core;
using DotNetCloud.Core.Authorization;
using DotNetCloud.Core.Capabilities;
using DotNetCloud.Core.Grpc;
using DotNetCloud.Core.Server.Supervisor;
using Grpc.Core;
using Grpc.Core.Interceptors;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace DotNetCloud.Core.Server.Grpc.Interceptors;

/// <summary>
/// gRPC interceptor that authenticates module callers against the credential the process
/// supervisor issued when it launched them.
/// </summary>
/// <remarks>
/// <para>
/// A <c>dnc-module-id</c> header on its own is only a claim: the core's gRPC listener is a local
/// cleartext HTTP/2 endpoint, so any process able to reach it could assert any module name. The
/// supervisor mints a unique token per module launch and hands it to the module process out of
/// band; this interceptor requires the matching token (constant-time compare in
/// <see cref="ModuleCredentialStore"/>) and binds the verified identity in
/// <see cref="ServerCallContext.UserState"/> for downstream interceptors and services.
/// </para>
/// <para>
/// Rejections are audited as <see cref="AuditAction.ModuleAuthFailed"/> so credential probing is
/// visible after the fact. The claimed module id is attacker-controlled, so audit writes are
/// throttled per claimant (one entry per window, bounded key set) — the limiter must not become a
/// disk-fill vector — and every logged or persisted value is sanitized.
/// </para>
/// </remarks>
internal sealed class AuthenticationInterceptor : Interceptor
{
    /// <summary>Minimum interval between audit entries for the same claimed identity.</summary>
    internal static readonly TimeSpan FailureAuditWindow = TimeSpan.FromMinutes(1);

    /// <summary>Upper bound on tracked claimants so hostile traffic cannot grow the table.</summary>
    internal const int FailureAuditKeyLimit = 512;

    /// <summary>Upper bound on the recorded claim length (the value is untrusted input).</summary>
    internal const int MaxClaimedModuleIdLength = 100;

    /// <summary>
    /// gRPC services that desktop and mobile clients call directly with a bearer token rather than
    /// with a module credential. Keeping this explicit (rather than inferring it from the headers)
    /// means a new service cannot accidentally inherit the client-facing exemption.
    /// </summary>
    private static readonly string[] ClientFacingServicePrefixes =
    [
        "/dotnetcloud.files.FilesService/",
    ];

    private readonly ILogger<AuthenticationInterceptor> _logger;
    private readonly ModuleCredentialStore _credentials;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly TimeProvider _timeProvider;
    private readonly ConcurrentDictionary<string, DateTimeOffset> _lastFailureAudit =
        new(StringComparer.OrdinalIgnoreCase);

    /// <summary>Initializes a new instance of the <see cref="AuthenticationInterceptor"/> class.</summary>
    /// <param name="logger">Logger.</param>
    /// <param name="credentials">The supervisor-issued module credentials.</param>
    /// <param name="scopeFactory">Scope factory used to write failure audit entries.</param>
    /// <param name="timeProvider">Clock for the failure-audit throttle.</param>
    public AuthenticationInterceptor(
        ILogger<AuthenticationInterceptor> logger,
        ModuleCredentialStore credentials,
        IServiceScopeFactory scopeFactory,
        TimeProvider timeProvider)
    {
        _logger = logger;
        _credentials = credentials;
        _scopeFactory = scopeFactory;
        _timeProvider = timeProvider;
    }

    /// <inheritdoc />
    public override async Task<TResponse> UnaryServerHandler<TRequest, TResponse>(
        TRequest request,
        ServerCallContext context,
        UnaryServerMethod<TRequest, TResponse> continuation)
    {
        await AuthenticateAsync(context);
        return await continuation(request, context);
    }

    /// <inheritdoc />
    public override async Task ServerStreamingServerHandler<TRequest, TResponse>(
        TRequest request,
        IServerStreamWriter<TResponse> responseStream,
        ServerCallContext context,
        ServerStreamingServerMethod<TRequest, TResponse> continuation)
    {
        await AuthenticateAsync(context);
        await continuation(request, responseStream, context);
    }

    /// <inheritdoc />
    public override async Task<TResponse> ClientStreamingServerHandler<TRequest, TResponse>(
        IAsyncStreamReader<TRequest> requestStream,
        ServerCallContext context,
        ClientStreamingServerMethod<TRequest, TResponse> continuation)
    {
        await AuthenticateAsync(context);
        return await continuation(requestStream, context);
    }

    /// <inheritdoc />
    public override async Task DuplexStreamingServerHandler<TRequest, TResponse>(
        IAsyncStreamReader<TRequest> requestStream,
        IServerStreamWriter<TResponse> responseStream,
        ServerCallContext context,
        DuplexStreamingServerMethod<TRequest, TResponse> continuation)
    {
        await AuthenticateAsync(context);
        await continuation(requestStream, responseStream, context);
    }

    private async Task AuthenticateAsync(ServerCallContext context)
    {
        if (IsClientFacing(context.Method))
        {
            AuthenticateClient(context);
            return;
        }

        var claimedModuleId = context.RequestHeaders.Get(ModuleIdentityMetadata.ModuleIdHeader)?.Value;
        var token = context.RequestHeaders.Get(ModuleIdentityMetadata.ModuleTokenHeader)?.Value;

        if (string.IsNullOrEmpty(claimedModuleId))
        {
            await RejectAsync(context, null, "missing dnc-module-id metadata header");
        }

        if (string.IsNullOrEmpty(token))
        {
            await RejectAsync(context, claimedModuleId, "missing dnc-module-token metadata header");
        }

        if (!_credentials.Validate(claimedModuleId!, token!))
        {
            await RejectAsync(context, claimedModuleId, "invalid module credential");
        }

        // Store the verified module identity for downstream use. Everything after this point
        // may trust "ModuleId" because it was proven by the supervisor-issued token.
        context.UserState["ModuleId"] = claimedModuleId!;

        _logger.LogDebug(
            "Authenticated gRPC call from module {ModuleId}",
            LogSanitizer.Sanitize(claimedModuleId!));
    }

    /// <summary>
    /// Rejects a call: logs, records a throttled audit entry, then throws <c>Unauthenticated</c>.
    /// </summary>
    private async Task RejectAsync(
        ServerCallContext context,
        string? claimedModuleId,
        string reason)
    {
        var sanitizedClaim = SanitizeClaim(claimedModuleId);

        _logger.LogWarning(
            "gRPC call rejected: {Reason} from {Peer} (dnc-module-id {ModuleId}, method {Method})",
            reason,
            LogSanitizer.Sanitize(context.Peer ?? "unknown"),
            sanitizedClaim,
            LogSanitizer.Sanitize(context.Method));

        await AuditFailureAsync(context, sanitizedClaim, reason);

        throw new RpcException(new Status(StatusCode.Unauthenticated, reason));
    }

    /// <summary>
    /// Writes the failure audit entry, at most once per claimant per
    /// <see cref="FailureAuditWindow"/>. Auditing never changes the outcome: a failure here is
    /// logged and the call stays rejected.
    /// </summary>
    private async Task AuditFailureAsync(
        ServerCallContext context,
        string claimedModuleId,
        string reason)
    {
        if (!ShouldAudit(claimedModuleId))
        {
            return;
        }

        try
        {
            using var scope = _scopeFactory.CreateScope();
            var auditLogger = scope.ServiceProvider.GetRequiredService<IAuditLogger>();

            await auditLogger.LogAsync(
                new AuditEntry
                {
                    Caller = CallerContext.CreateSystemContext(),
                    ModuleId = claimedModuleId,
                    Action = AuditAction.ModuleAuthFailed,
                    EntityType = "ModuleAuth",
                    EntityId = Guid.Empty,
                    Description =
                        $"Rejected {LogSanitizer.Sanitize(context.Method)}: {reason} " +
                        $"(peer {LogSanitizer.Sanitize(context.Peer ?? "unknown")})",
                },
                context.CancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogWarning(
                ex,
                "Could not record the module authentication-failure audit entry for {ModuleId}",
                claimedModuleId);
        }
    }

    /// <summary>
    /// Returns <see langword="true"/> when an audit entry is due for this claimant, and marks it as
    /// written. Lock-free; a lost race can only duplicate an entry, never skip the throttle.
    /// </summary>
    private bool ShouldAudit(string claimant)
    {
        // Bounded table: a hostile caller can invent unlimited module ids.
        if (_lastFailureAudit.Count >= FailureAuditKeyLimit)
        {
            _lastFailureAudit.Clear();
        }

        var now = _timeProvider.GetUtcNow();

        while (true)
        {
            if (!_lastFailureAudit.TryGetValue(claimant, out var last))
            {
                if (_lastFailureAudit.TryAdd(claimant, now))
                {
                    return true;
                }

                continue;
            }

            if (now - last < FailureAuditWindow)
            {
                return false;
            }

            if (_lastFailureAudit.TryUpdate(claimant, now, last))
            {
                return true;
            }
        }
    }

    /// <summary>Normalizes the claimed module id for logging/audit (never trusted, always bounded).</summary>
    private static string SanitizeClaim(string? claimedModuleId)
    {
        if (string.IsNullOrWhiteSpace(claimedModuleId))
        {
            return "unknown";
        }

        var sanitized = LogSanitizer.Sanitize(claimedModuleId).Trim();

        return sanitized.Length <= MaxClaimedModuleIdLength
            ? sanitized
            : sanitized[..MaxClaimedModuleIdLength];
    }

    private static bool IsClientFacing(string? method) =>
        method is not null
        && ClientFacingServicePrefixes.Any(prefix =>
            method.StartsWith(prefix, StringComparison.Ordinal));

    /// <summary>
    /// Authenticates a direct client call. There is no module credential on this path — the caller
    /// is an end user, authenticated by the ASP.NET Core authentication middleware that runs inside
    /// the gRPC pipeline before interceptors.
    /// </summary>
    private void AuthenticateClient(ServerCallContext context)
    {
        var httpContext = context.GetHttpContext();
        var user = httpContext?.User;

        if (user?.Identity?.IsAuthenticated != true)
        {
            _logger.LogWarning(
                "gRPC client call to {Method} rejected: no authenticated principal from {Peer}",
                LogSanitizer.Sanitize(context.Method),
                LogSanitizer.Sanitize(context.Peer ?? "unknown"));

            throw new RpcException(new Status(StatusCode.Unauthenticated, "Authentication required"));
        }

        // Presence of this key marks the call as client-facing for the caller-context interceptor;
        // it is set even when the claim was missing so the failure is reported accurately.
        context.UserState["ClientUserId"] = user.FindFirst(ClaimTypes.NameIdentifier)?.Value
            ?? user.FindFirst("sub")?.Value
            ?? string.Empty;
    }
}
