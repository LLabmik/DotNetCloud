using Grpc.Core;

namespace DotNetCloud.Core.Server.Tests.Services;

/// <summary>
/// Minimal <see cref="ServerCallContext"/> test double for unit-testing gRPC service methods.
/// </summary>
internal sealed class TestServerCallContext : ServerCallContext
{
    /// <inheritdoc />
    protected override string MethodCore => "test";

    /// <inheritdoc />
    protected override string HostCore => "localhost";

    /// <inheritdoc />
    protected override string PeerCore => "test";

    /// <inheritdoc />
    protected override DateTime DeadlineCore => DateTime.MaxValue;

    /// <inheritdoc />
    protected override Metadata RequestHeadersCore => [];

    /// <inheritdoc />
    protected override CancellationToken CancellationTokenCore => CancellationToken.None;

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
