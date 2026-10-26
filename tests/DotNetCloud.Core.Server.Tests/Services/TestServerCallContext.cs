using Grpc.Core;

namespace DotNetCloud.Core.Server.Tests.Services;

/// <summary>
/// Minimal <see cref="ServerCallContext"/> test double for unit-testing gRPC service methods.
/// </summary>
internal sealed class TestServerCallContext : ServerCallContext
{
    private readonly string _method;
    private readonly Metadata _requestHeaders;
    private readonly CancellationToken _cancellationToken;

    /// <summary>
    /// Initializes a new instance of the <see cref="TestServerCallContext"/> class with no
    /// request headers.
    /// </summary>
    public TestServerCallContext()
        : this("test", [], CancellationToken.None)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="TestServerCallContext"/> class.
    /// </summary>
    /// <param name="method">The fully-qualified gRPC method name.</param>
    /// <param name="requestHeaders">The request metadata.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    public TestServerCallContext(
        string method,
        Metadata requestHeaders,
        CancellationToken cancellationToken = default)
    {
        _method = method;
        _requestHeaders = requestHeaders;
        _cancellationToken = cancellationToken;
    }

    /// <inheritdoc />
    protected override string MethodCore => _method;

    /// <inheritdoc />
    protected override string HostCore => "localhost";

    /// <inheritdoc />
    protected override string PeerCore => "test";

    /// <inheritdoc />
    protected override DateTime DeadlineCore => DateTime.MaxValue;

    /// <inheritdoc />
    protected override Metadata RequestHeadersCore => _requestHeaders;

    /// <inheritdoc />
    protected override CancellationToken CancellationTokenCore => _cancellationToken;

    /// <inheritdoc />
    protected override Metadata ResponseTrailersCore => [];

    /// <inheritdoc />
    protected override Status StatusCore { get => Status.DefaultSuccess; set { } }

    /// <inheritdoc />
    protected override WriteOptions? WriteOptionsCore { get => null; set { } }

    /// <inheritdoc />
    protected override AuthContext AuthContextCore => new("test", []);

    /// <inheritdoc />
    protected override ContextPropagationToken CreatePropagationTokenCore(ContextPropagationOptions? options) => throw new NotSupportedException();

    /// <inheritdoc />
    protected override Task WriteResponseHeadersAsyncCore(Metadata responseHeaders) => Task.CompletedTask;
}
