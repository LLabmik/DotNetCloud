using DotNetCloud.Core.Server.Diagnostics;
using Microsoft.AspNetCore.Http.Features;
using System.Security.Claims;

namespace DotNetCloud.Core.Server.Middleware;

/// <summary>
/// Counts client HTTP payload and WebSocket frame bytes for the admin System page.
/// </summary>
/// <remarks>
/// <para>
/// Must be registered <b>after</b> authentication/authorization so
/// <see cref="HttpContext.User"/> is populated: only authenticated traffic is
/// counted. Internal gRPC traffic is excluded (it is branched into a separate
/// pipeline earlier and additionally filtered here by content type).
/// </para>
/// <para>
/// The middleware wraps HTTP features rather than <c>Response.Body</c>, so streaming,
/// range requests, sendfile and WebSockets keep working unchanged. Any failure while
/// installing a feature falls back to the original features — metrics must never
/// break a request.
/// </para>
/// <para>
/// Counters measure <b>application payload</b> bytes (after response compression
/// runs, before TLS/framing), cumulative since the core process started.
/// </para>
/// </remarks>
public sealed class TrafficAccountingMiddleware
{
    /// <summary>Path of the admin System page endpoint, excluded so polling never counts itself.</summary>
    private const string AdminSystemPath = "/api/v1/core/admin/system";

    /// <summary>Key used to cache the resolved user id for the lifetime of the request.</summary>
    private const string UserIdItemKey = "DotNetCloud.Traffic.UserId";

    private readonly RequestDelegate _next;
    private readonly TrafficAccounting _accounting;
    private readonly ILogger<TrafficAccountingMiddleware> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="TrafficAccountingMiddleware"/> class.
    /// </summary>
    /// <param name="next">The next middleware in the pipeline.</param>
    /// <param name="accounting">The process-wide traffic counters.</param>
    /// <param name="logger">The logger.</param>
    public TrafficAccountingMiddleware(
        RequestDelegate next,
        TrafficAccounting accounting,
        ILogger<TrafficAccountingMiddleware> logger)
    {
        _next = next ?? throw new ArgumentNullException(nameof(next));
        _accounting = accounting ?? throw new ArgumentNullException(nameof(accounting));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Invokes the middleware.
    /// </summary>
    /// <param name="context">The HTTP context.</param>
    public async Task InvokeAsync(HttpContext context)
    {
        if (!ShouldCount(context))
        {
            await _next(context);
            return;
        }

        var originalBodyFeature = context.Features.Get<IHttpResponseBodyFeature>();
        var originalRequestPipeFeature = context.Features.Get<IRequestBodyPipeFeature>();
        var originalWebSocketFeature = context.Features.Get<IHttpWebSocketFeature>();

        var counter = new RequestTrafficCounter(_accounting, ResolveUserId(context));
        var requestContentLength = context.Request.ContentLength;

        try
        {
            if (originalBodyFeature is not null)
            {
                context.Features.Set<IHttpResponseBodyFeature>(
                    new CountingResponseBodyFeature(originalBodyFeature, counter.CountOut));
            }

            if (originalWebSocketFeature is not null)
            {
                context.Features.Set<IHttpWebSocketFeature>(
                    new CountingWebSocketFeature(originalWebSocketFeature, counter.CountIn, counter.CountOut));
            }

            if (originalRequestPipeFeature is not null && RequestCanHaveBody(context))
            {
                context.Features.Set<IRequestBodyPipeFeature>(
                    new CountingRequestBodyPipeFeature(originalRequestPipeFeature, counter.CountIn));
            }
        }
        catch (Exception ex)
        {
            // Fail open — restore whatever we managed to replace and continue.
            context.Features.Set(originalBodyFeature);
            context.Features.Set(originalRequestPipeFeature);
            context.Features.Set(originalWebSocketFeature);
            _logger.LogDebug(ex, "Traffic accounting features could not be installed for {Path}", context.Request.Path);
        }

        try
        {
            await _next(context);
        }
        finally
        {
            context.Features.Set(originalBodyFeature);
            context.Features.Set(originalRequestPipeFeature);
            context.Features.Set(originalWebSocketFeature);

            // Bodies that are drained or aborted before the application reads them
            // are still client traffic — top up to Content-Length when needed.
            if (requestContentLength is > 0 && requestContentLength.Value > counter.BytesIn)
            {
                counter.CountIn(requestContentLength.Value - counter.BytesIn);
            }
        }
    }

    private static bool ShouldCount(HttpContext context)
    {
        if (context.User.Identity?.IsAuthenticated != true)
        {
            return false;
        }

        // The admin System page polls this endpoint every 5 s; counting it would put
        // self-inflicted traffic on the watching admin's own row.
        if (context.Request.Path.StartsWithSegments(AdminSystemPath, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        // Module → core capability calls are internal, not user traffic.
        var contentType = context.Request.ContentType;
        return contentType is null
            || !contentType.StartsWith("application/grpc", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Resolves the authenticated user once per request. Anything unparsable falls back to
    /// <c>null</c>, which keeps the bytes in the global counter only.
    /// </summary>
    private static Guid? ResolveUserId(HttpContext context)
    {
        if (context.Items.TryGetValue(UserIdItemKey, out var cached) && cached is Guid cachedId)
        {
            return cachedId;
        }

        var raw = context.User.FindFirstValue(ClaimTypes.NameIdentifier)
                  ?? context.User.FindFirstValue("sub");

        if (Guid.TryParse(raw, out var userId))
        {
            context.Items[UserIdItemKey] = userId;
            return userId;
        }

        return null;
    }

    private static bool RequestCanHaveBody(HttpContext context)
    {
        var detection = context.Features.Get<IHttpRequestBodyDetectionFeature>();
        if (detection is not null)
        {
            return detection.CanHaveBody;
        }

        // Chunked requests have no Content-Length; assume a body unless explicitly
        // declared as zero-length.
        return context.Request.ContentLength is not 0;
    }

    /// <summary>
    /// Per-request accumulator that feeds the process-wide counters.
    /// </summary>
    private sealed class RequestTrafficCounter
    {
        private readonly TrafficAccounting _accounting;
        private readonly Guid? _userId;
        private long _bytesIn;

        public RequestTrafficCounter(TrafficAccounting accounting, Guid? userId)
        {
            _accounting = accounting;
            _userId = userId;
        }

        public long BytesIn => Interlocked.Read(ref _bytesIn);

        public void CountIn(long count)
        {
            if (count <= 0)
            {
                return;
            }

            Interlocked.Add(ref _bytesIn, count);
            if (_userId is { } userId)
            {
                _accounting.AddIn(userId, count);
            }
            else
            {
                _accounting.AddIn(count);
            }
        }

        public void CountOut(long count)
        {
            if (_userId is { } userId)
            {
                _accounting.AddOut(userId, count);
            }
            else
            {
                _accounting.AddOut(count);
            }
        }
    }
}
