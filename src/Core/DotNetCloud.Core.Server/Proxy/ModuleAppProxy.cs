using System.Collections.Concurrent;
using System.Net.Http.Headers;
using DotNetCloud.Core;
using DotNetCloud.Core.Grpc;
using DotNetCloud.Core.Modules;
using DotNetCloud.Core.Modules.Supervisor;
using DotNetCloud.Core.Server.ModuleLoading;
using DotNetCloud.Core.Server.Quotas;
using DotNetCloud.Core.Server.Supervisor;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Logging;
using Yarp.ReverseProxy.Forwarder;

namespace DotNetCloud.Core.Server.Proxy;

/// <summary>
/// The module UI plane: reverse-proxies <c>/apps/{moduleId}/**</c> to the module's own host process
/// so a non-required module can ship its own UI without a single line of core code.
/// </summary>
/// <remarks>
/// <para>
/// Routing is deliberately constrained to <b>reverse-DNS</b> module ids (they must contain a dot).
/// The shell's first-party pages live at <c>/apps/files</c>, <c>/apps/chat</c>, … and a
/// <c>{moduleId}/{**catchAll}</c> pattern with an empty catch-all would otherwise swallow them.
/// </para>
/// <para>
/// The proxy is a security boundary, not a convenience: it strips the browser's credentials before
/// forwarding (the module never sees the user's core session), strips the module's ability to set
/// cookies on the core origin, and replaces the module's framing/CSP headers with the core's own.
/// </para>
/// </remarks>
internal static class ModuleAppProxy
{
    /// <summary>The route constraint key registered for module ids.</summary>
    internal const string ModuleIdConstraint = "dncmoduleid";

    /// <summary>Maximum response body size accepted from a module UI, in bytes.</summary>
    internal const long MaxResponseBytes = 32L * 1024 * 1024;

    /// <summary>The assertion operation minted for UI-plane requests.</summary>
    internal const string UiOperation = "ui";

    /// <summary>
    /// Maps the module app proxy routes.
    /// </summary>
    /// <param name="app">The web application.</param>
    public static void MapModuleAppProxies(WebApplication app)
    {
        ArgumentNullException.ThrowIfNull(app);

        var supervisor = app.Services.GetRequiredService<IProcessSupervisor>();
        var manifests = app.Services.GetRequiredService<IModuleManifestRegistry>();
        var forwarder = app.Services.GetRequiredService<IHttpForwarder>();
        var assertionKeyRing = app.Services.GetRequiredService<ModuleAssertionKeyRing>();
        var invokerCache = app.Services.GetRequiredService<ModuleHttpInvokerCache>();
        var rateLimiter = app.Services.GetRequiredService<ModuleRateLimiter>();
        var quotaCache = app.Services.GetRequiredService<ModuleQuotaCache>();
        var logger = app.Services.GetRequiredService<ILoggerFactory>().CreateLogger("ModuleAppProxy");

        // One transformer per module: it mints the end-user assertion scoped to that module.
        var transformers = new ConcurrentDictionary<string, ModuleAppProxyTransformer>(StringComparer.OrdinalIgnoreCase);

        async Task HandleAsync(HttpContext context)
        {
            var moduleId = context.Request.RouteValues["moduleId"] as string;
            if (string.IsNullOrEmpty(moduleId) || !ModuleIdRouteConstraint.IsValidModuleId(moduleId))
            {
                context.Response.StatusCode = StatusCodes.Status404NotFound;
                return;
            }

            if (context.User?.Identity?.IsAuthenticated != true)
            {
                // 401 rather than a login redirect: this endpoint is loaded by an iframe, where a
                // redirect to the sign-in page would be framed instead of navigating the user.
                context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                return;
            }

            // Only a module that declared an iframe UI may be served through the core origin.
            var manifest = manifests.GetManifest(moduleId);
            if (manifest?.Ui is null
                || !string.Equals(manifest.Ui.Type, "iframe", StringComparison.Ordinal)
                || RequiredModules.IsRequired(moduleId))
            {
                context.Response.StatusCode = StatusCodes.Status404NotFound;
                return;
            }

            var moduleInfo = supervisor.GetModuleInfo(moduleId);
            if (moduleInfo?.GrpcEndpoint is null || moduleInfo.Status != ModuleProcessStatus.Running)
            {
                context.Response.StatusCode = StatusCodes.Status503ServiceUnavailable;
                return;
            }

            var quota = await quotaCache.GetAsync(moduleId, context.RequestAborted);
            var outcome = rateLimiter.TryAcquire(moduleId, ModuleRateLimitKind.Ui, quota.RequestsPerMinute ?? 0);
            if (outcome != RateLimitOutcome.Allowed)
            {
                if (outcome == RateLimitOutcome.LimitedFirstInWindow)
                {
                    logger.LogWarning(
                        "Module UI request rate limit reached for {ModuleId}", LogSanitizer.Sanitize(moduleId));
                }

                context.Response.StatusCode = StatusCodes.Status429TooManyRequests;
                context.Response.Headers["Retry-After"] = "60";
                return;
            }

            if (!TryStripAppBasePath(context.Request, moduleId))
            {
                context.Response.StatusCode = StatusCodes.Status404NotFound;
                return;
            }

            var (destinationPrefix, invoker) = invokerCache.Resolve(moduleId, moduleInfo.GrpcEndpoint);
            var transformer = transformers.GetOrAdd(moduleId, id => new ModuleAppProxyTransformer(id, assertionKeyRing));

            var originalBody = context.Response.Body;
            await using var capped = new CappedResponseStream(originalBody, MaxResponseBytes);
            context.Response.Body = capped;

            try
            {
                var error = await forwarder.SendAsync(
                    context,
                    destinationPrefix,
                    invoker,
                    new ForwarderRequestConfig
                    {
                        ActivityTimeout = TimeSpan.FromMinutes(5),
                        Version = System.Net.HttpVersion.Version20,
                        VersionPolicy = System.Net.Http.HttpVersionPolicy.RequestVersionOrHigher,
                    },
                    transformer);

                if (error != ForwarderError.None && !context.Response.HasStarted)
                {
                    var errorFeature = context.GetForwarderErrorFeature();
                    logger.LogWarning(
                        errorFeature?.Exception,
                        "Module UI proxy failure for {Path} → {Module} ({Destination}): {Error}",
                        LogSanitizer.Sanitize(context.Request.Path.ToString()),
                        LogSanitizer.Sanitize(moduleId),
                        destinationPrefix,
                        error);

                    context.Response.StatusCode = StatusCodes.Status502BadGateway;
                }
            }
            finally
            {
                await capped.FlushAsync(context.RequestAborted);
                context.Response.Body = originalBody;
            }
        }

        app.Map($"/apps/{{moduleId:{ModuleIdConstraint}}}/{{**catchAll}}", HandleAsync);
        app.Map($"/apps/{{moduleId:{ModuleIdConstraint}}}", HandleAsync);

        logger.LogInformation(
            "Module UI app proxy mapped at /apps/{Constraint}/** (reverse-DNS module ids only)",
            ModuleIdConstraint);
    }

    /// <summary>
    /// Moves <c>/apps/{moduleId}</c> from the request path into <see cref="HttpRequest.PathBase"/> so
    /// the forwarded request is module-rooted.
    /// </summary>
    /// <param name="request">The inbound request.</param>
    /// <param name="moduleId">The module id.</param>
    /// <returns><see langword="true"/> when the prefix was found and stripped.</returns>
    internal static bool TryStripAppBasePath(HttpRequest request, string moduleId)
    {
        ArgumentNullException.ThrowIfNull(request);

        var path = request.Path.Value ?? string.Empty;
        var prefix = $"/apps/{moduleId}";

        if (!path.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var remainder = path[prefix.Length..];
        if (remainder.Length == 0)
        {
            remainder = "/";
        }
        else if (remainder[0] != '/')
        {
            // "/apps/dotnetcloud.filesx" must not be treated as "/apps/dotnetcloud.files".
            return false;
        }

        request.PathBase = new PathString(path[..prefix.Length]);
        request.Path = new PathString(remainder);
        return true;
    }
}

/// <summary>
/// Route constraint that only matches reverse-DNS module ids.
/// </summary>
/// <remarks>
/// Same shape as the manifest validator's id rule. Requiring at least one dot is what keeps
/// <c>/apps/files</c> (a first-party Blazor page) out of the module app proxy.
/// </remarks>
internal sealed partial class ModuleIdRouteConstraint : IRouteConstraint
{
    /// <inheritdoc />
    public bool Match(
        HttpContext? httpContext,
        IRouter? route,
        string routeKey,
        RouteValueDictionary values,
        RouteDirection routeDirection)
    {
        ArgumentNullException.ThrowIfNull(routeKey);
        ArgumentNullException.ThrowIfNull(values);

        return values.TryGetValue(routeKey, out var value)
            && value is string moduleId
            && IsValidModuleId(moduleId);
    }

    /// <summary>
    /// Validates a module id: lowercase reverse-DNS, at least one dot, no leading/trailing dashes.
    /// </summary>
    /// <param name="moduleId">The candidate id.</param>
    /// <returns><see langword="true"/> when the id is well formed.</returns>
    public static bool IsValidModuleId(string? moduleId) =>
        !string.IsNullOrEmpty(moduleId)
        && moduleId.Length <= 200
        && ModuleIdPattern().IsMatch(moduleId);

    [System.Text.RegularExpressions.GeneratedRegex(
        "^[a-z0-9](?:[a-z0-9-]*[a-z0-9])?(?:\\.[a-z0-9](?:[a-z0-9-]*[a-z0-9])?)+$",
        System.Text.RegularExpressions.RegexOptions.CultureInvariant)]
    private static partial System.Text.RegularExpressions.Regex ModuleIdPattern();
}

/// <summary>
/// Transformer for module UI requests: drops the browser's credentials, injects the core-signed
/// end-user assertion, and normalizes the module's response headers.
/// </summary>
/// <remarks>
/// The assertion is the same token type the REST API proxy mints — audience-bound to the module and
/// valid for 60 seconds — with the operation set to <c>ui</c> so a module can tell UI-originated
/// context apart from an API-proxied call. No cookie ever reaches the module.
/// </remarks>
internal sealed class ModuleAppProxyTransformer : HttpTransformer
{
    /// <summary>Request headers that must never reach a module UI backend.</summary>
    internal static readonly string[] StrippedRequestHeaders =
    [
        "Cookie",
        "Cookie2",
        "Authorization",
        "Proxy-Authorization",
        ModuleIdentityMetadata.UserAssertionHeader,
        ModuleIdentityMetadata.ModuleIdHeader,
        ModuleIdentityMetadata.ModuleTokenHeader,
        "X-Forwarded-User",
    ];

    private readonly string _moduleId;
    private readonly ModuleAssertionKeyRing _assertionKeyRing;

    /// <summary>
    /// Initializes a new instance of the <see cref="ModuleAppProxyTransformer"/> class.
    /// </summary>
    /// <param name="moduleId">The module the proxied request belongs to.</param>
    /// <param name="assertionKeyRing">The core's assertion signer.</param>
    public ModuleAppProxyTransformer(string moduleId, ModuleAssertionKeyRing assertionKeyRing)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(moduleId);
        ArgumentNullException.ThrowIfNull(assertionKeyRing);

        _moduleId = moduleId;
        _assertionKeyRing = assertionKeyRing;
    }

    /// <inheritdoc />
    public override async ValueTask TransformRequestAsync(
        HttpContext httpContext,
        HttpRequestMessage proxyRequest,
        string destinationPrefix,
        CancellationToken cancellationToken)
    {
        await base.TransformRequestAsync(httpContext, proxyRequest, destinationPrefix, cancellationToken);

        // The browser's core session must not be replayed to the module. The module learns who the
        // user is only from the assertion below, and that assertion is bound to this module.
        foreach (var header in StrippedRequestHeaders)
        {
            proxyRequest.Headers.Remove(header);
        }

        proxyRequest.Headers.TryAddWithoutValidation("X-Forwarded-Proto", "https");
        proxyRequest.Headers.TryAddWithoutValidation("X-Forwarded-Host", httpContext.Request.Host.Value);

        var userId = httpContext.User?.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value
            ?? httpContext.User?.FindFirst("sub")?.Value;

        if (Guid.TryParse(userId, out var parsedUserId) && parsedUserId != Guid.Empty)
        {
            var assertion = _assertionKeyRing.Mint(parsedUserId, _moduleId, ModuleAppProxy.UiOperation);
            proxyRequest.Headers.TryAddWithoutValidation(
                ModuleIdentityMetadata.UserAssertionHeader, assertion);
        }
    }

    /// <inheritdoc />
    public override async ValueTask<bool> TransformResponseAsync(
        HttpContext httpContext,
        HttpResponseMessage? proxyResponse,
        CancellationToken cancellationToken)
    {
        var result = await base.TransformResponseAsync(httpContext, proxyResponse, cancellationToken);

        if (proxyResponse is not null)
        {
            // Applied here rather than in OnStarting: the module's own Set-Cookie / CSP headers must
            // be gone before anything (including the core's security-header middleware) reads them.
            ModuleAppResponsePolicy.Apply(
                proxyResponse.Headers,
                proxyResponse.Content?.Headers.ContentType?.MediaType);
        }

        return result;
    }
}

/// <summary>
/// Response header policy for the module UI plane.
/// </summary>
/// <remarks>
/// A module must not be able to set cookies on the core origin (that would let it act as the user
/// on core endpoints), and it must not be able to relax the framing/CSP rules that keep its own
/// document inside the core's sandbox boundary. Everything framing-related is therefore replaced,
/// not merged.
/// </remarks>
internal static class ModuleAppResponsePolicy
{
    /// <summary>The CSP applied to every module UI response.</summary>
    internal const string ContentSecurityPolicy =
        "frame-ancestors 'self'; base-uri 'none'; object-src 'none'; form-action 'self'";

    /// <summary>The <c>Referrer-Policy</c> applied to every module UI response.</summary>
    internal const string ReferrerPolicy = "no-referrer";

    /// <summary>Response headers removed from every module UI response.</summary>
    internal static readonly string[] StrippedResponseHeaders =
    [
        "Set-Cookie",
        "Set-Cookie2",
        "Server",
        "X-Powered-By",
        "X-AspNet-Version",
        "X-Frame-Options",
        "Content-Security-Policy",
        "Content-Security-Policy-Report-Only",
        "X-Content-Security-Policy",
    ];

    /// <summary>
    /// Applies the policy to a proxied response.
    /// </summary>
    /// <param name="headers">The module's response headers.</param>
    /// <param name="contentType">
    /// The response media type. Content headers live on <c>HttpContent.Headers</c>, so the caller
    /// passes it in explicitly.
    /// </param>
    public static void Apply(HttpResponseHeaders headers, string? contentType = null)
    {
        ArgumentNullException.ThrowIfNull(headers);

        foreach (var name in StrippedResponseHeaders)
        {
            headers.Remove(name);
        }

        headers.TryAddWithoutValidation("Content-Security-Policy", ContentSecurityPolicy);
        headers.TryAddWithoutValidation("X-Content-Type-Options", "nosniff");
        headers.TryAddWithoutValidation("Referrer-Policy", ReferrerPolicy);

        if (IsHtmlDocument(contentType))
        {
            // A module update replaces the markup behind the same URL; a cached document would
            // keep an old build mounted in every open frame.
            headers.CacheControl = new CacheControlHeaderValue
            {
                NoStore = true,
                NoCache = true,
                MustRevalidate = true,
            };
        }
    }

    private static bool IsHtmlDocument(string? contentType) =>
        contentType is not null
        && contentType.StartsWith("text/html", StringComparison.OrdinalIgnoreCase);
}

/// <summary>
/// Wraps a response body and fails the write that would exceed a byte cap.
/// </summary>
/// <remarks>
/// Module UIs are plain HTTP apps, so a single unbounded response (a runaway generator, a huge
/// export) would otherwise be streamed straight through the core's memory budget.
/// </remarks>
internal sealed class CappedResponseStream : Stream
{
    private readonly Stream _inner;
    private readonly long _maxBytes;
    private long _written;

    /// <summary>
    /// Initializes a new instance of the <see cref="CappedResponseStream"/> class.
    /// </summary>
    /// <param name="inner">The destination stream.</param>
    /// <param name="maxBytes">The maximum number of bytes written before the stream faults.</param>
    public CappedResponseStream(Stream inner, long maxBytes)
    {
        ArgumentNullException.ThrowIfNull(inner);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maxBytes);

        _inner = inner;
        _maxBytes = maxBytes;
    }

    /// <summary>Gets the number of bytes written so far.</summary>
    public long BytesWritten => _written;

    /// <inheritdoc />
    public override bool CanRead => false;

    /// <inheritdoc />
    public override bool CanSeek => false;

    /// <inheritdoc />
    public override bool CanWrite => true;

    /// <inheritdoc />
    public override long Length => throw new NotSupportedException();

    /// <inheritdoc />
    public override long Position
    {
        get => _written;
        set => throw new NotSupportedException();
    }

    /// <inheritdoc />
    public override void Write(byte[] buffer, int offset, int count) =>
        WriteCore(buffer.AsSpan(offset, count));

    /// <inheritdoc />
    public override void Write(ReadOnlySpan<byte> buffer) => WriteCore(buffer);

    /// <inheritdoc />
    public override Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken) =>
        WriteAsync(buffer.AsMemory(offset, count), cancellationToken).AsTask();

    /// <inheritdoc />
    public override ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default)
    {
        EnsureCapacity(buffer.Length);
        return _inner.WriteAsync(buffer, cancellationToken);
    }

    /// <inheritdoc />
    public override void Flush() => _inner.Flush();

    /// <inheritdoc />
    public override Task FlushAsync(CancellationToken cancellationToken) => _inner.FlushAsync(cancellationToken);

    /// <inheritdoc />
    public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();

    /// <inheritdoc />
    public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

    /// <inheritdoc />
    public override void SetLength(long value) => throw new NotSupportedException();

    private void WriteCore(ReadOnlySpan<byte> buffer)
    {
        EnsureCapacity(buffer.Length);
        _inner.Write(buffer);
    }

    private void EnsureCapacity(int count)
    {
        if (_written + count > _maxBytes)
        {
            throw new IOException(
                $"Module UI response exceeded the {_maxBytes} byte limit.");
        }

        _written += count;
    }
}
