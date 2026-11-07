using DotNetCloud.Core.Grpc;
using Grpc.Core;

namespace DotNetCloud.Core.Server.Rest;

/// <summary>
/// A <see cref="ServerCallContext"/> backed by an inbound HTTP request.
/// </summary>
/// <remarks>
/// <para>
/// The REST facade exists so a module backend written in another language can reach the core without
/// a gRPC client. Re-implementing credential checks, caller-context derivation, rate limiting and
/// capability evaluation for JSON callers would give the platform two security models that can drift
/// apart, so the facade instead adapts the HTTP request into a call context and runs the very same
/// interceptor chain the gRPC listener uses.
/// </para>
/// <para>
/// Only the module identity headers are projected into <see cref="RequestHeadersCore"/>: they are the
/// only headers the interceptors read, and <see cref="Metadata"/> rejects non-ASCII values, so a
/// hostile header cannot turn authentication into an exception.
/// </para>
/// </remarks>
internal sealed class ModuleHttpServerCallContext : ServerCallContext
{
    private static readonly string[] ProjectedHeaders =
    [
        ModuleIdentityMetadata.ModuleIdHeader,
        ModuleIdentityMetadata.ModuleTokenHeader,
        ModuleIdentityMetadata.UserAssertionHeader,
    ];

    private readonly string _method;
    private readonly Metadata _requestHeaders;
    private Status _status = Status.DefaultSuccess;
    private WriteOptions? _writeOptions;

    /// <summary>
    /// Initializes a new instance of the <see cref="ModuleHttpServerCallContext"/> class.
    /// </summary>
    /// <param name="httpContext">The HTTP request being served.</param>
    /// <param name="method">
    /// The canonical gRPC method path the endpoint maps to (for example
    /// <c>/dotnetcloud.moduleapi.v1.ModuleApi/GetUser</c>). Both the capability catalog and the
    /// rate limiter key off this value.
    /// </param>
    public ModuleHttpServerCallContext(HttpContext httpContext, string method)
    {
        ArgumentNullException.ThrowIfNull(httpContext);
        ArgumentException.ThrowIfNullOrWhiteSpace(method);

        HttpContext = httpContext;
        _method = method;
        _requestHeaders = BuildHeaders(httpContext);
    }

    /// <summary>Gets the HTTP request this call wraps.</summary>
    public HttpContext HttpContext { get; }

    /// <inheritdoc />
    protected override string MethodCore => _method;

    /// <inheritdoc />
    protected override string HostCore => HttpContext.Request.Host.Value ?? "localhost";

    /// <inheritdoc />
    protected override string PeerCore =>
        HttpContext.Connection.RemoteIpAddress?.ToString() ?? "internal";

    /// <inheritdoc />
    protected override DateTime DeadlineCore => DateTime.MaxValue;

    /// <inheritdoc />
    protected override Metadata RequestHeadersCore => _requestHeaders;

    /// <inheritdoc />
    protected override CancellationToken CancellationTokenCore => HttpContext.RequestAborted;

    /// <inheritdoc />
    protected override Metadata ResponseTrailersCore => [];

    /// <inheritdoc />
    protected override Status StatusCore
    {
        get => _status;
        set => _status = value;
    }

    /// <inheritdoc />
    protected override WriteOptions? WriteOptionsCore
    {
        get => _writeOptions;
        set => _writeOptions = value;
    }

    /// <inheritdoc />
    protected override AuthContext AuthContextCore =>
        new("module-rest", new Dictionary<string, List<AuthProperty>>(StringComparer.Ordinal));

    /// <inheritdoc />
    protected override ContextPropagationToken CreatePropagationTokenCore(
        ContextPropagationOptions? options) =>
        throw new NotSupportedException(
            "A REST module call cannot be propagated as a gRPC call.");

    /// <inheritdoc />
    protected override Task WriteResponseHeadersAsyncCore(Metadata responseHeaders) =>
        Task.CompletedTask;

    private static Metadata BuildHeaders(HttpContext httpContext)
    {
        var metadata = new Metadata();

        foreach (var name in ProjectedHeaders)
        {
            var value = httpContext.Request.Headers[name].ToString();

            if (!string.IsNullOrEmpty(value) && IsAscii(value))
            {
                metadata.Add(name, value);
            }
        }

        return metadata;
    }

    private static bool IsAscii(string value)
    {
        foreach (var ch in value)
        {
            if (ch > 127)
            {
                return false;
            }
        }

        return true;
    }
}
