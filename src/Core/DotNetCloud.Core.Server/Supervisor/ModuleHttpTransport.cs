using System.Collections.Concurrent;
using System.IO.Pipes;
using System.Net.Sockets;
using DotNetCloud.Core.Modules.Supervisor;

namespace DotNetCloud.Core.Server.Supervisor;

/// <summary>
/// Builds HTTP transports that can dial Unix domain sockets and Windows named pipes.
/// </summary>
/// <remarks>
/// <para>
/// <see cref="System.Net.Http.SocketsHttpHandler"/> cannot parse <c>unix://</c> or
/// <c>net.pipe://</c> URLs, so both the gRPC channel manager and the YARP REST proxy advertise the
/// endpoint as <c>http://localhost</c> and redirect the TCP connect to the socket through
/// <see cref="System.Net.Http.SocketsHttpHandler.ConnectCallback"/>. HTTP/2 cleartext (h2c) is
/// negotiated on top of the stream, exactly as it was over loopback TCP.
/// </para>
/// </remarks>
internal static class ModuleHttpTransport
{
    /// <summary>
    /// Creates a handler that dials the endpoint's socket or pipe.
    /// </summary>
    /// <param name="endpoint">The module endpoint.</param>
    /// <param name="connectTimeout">Optional connect timeout.</param>
    /// <returns>The handler.</returns>
    public static SocketsHttpHandler CreateHandler(ModuleEndpoint endpoint, TimeSpan? connectTimeout = null)
    {
        ArgumentNullException.ThrowIfNull(endpoint);

        var handler = new SocketsHttpHandler
        {
            UseProxy = false,
            AllowAutoRedirect = false,
            UseCookies = false,
            EnableMultipleHttp2Connections = true,
            ConnectTimeout = connectTimeout ?? TimeSpan.FromSeconds(10),
            PooledConnectionLifetime = TimeSpan.FromMinutes(2),
            PooledConnectionIdleTimeout = TimeSpan.FromSeconds(30),
        };

        if (endpoint.IsSocketTransport)
        {
            handler.ConnectCallback = (context, cancellationToken) => ConnectAsync(endpoint, cancellationToken);
        }

        return handler;
    }

    /// <summary>
    /// Opens a stream to a socket or pipe endpoint.
    /// </summary>
    /// <param name="endpoint">The module endpoint.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The connected stream.</returns>
    /// <exception cref="NotSupportedException">The endpoint is not a socket or pipe.</exception>
    public static async ValueTask<Stream> ConnectAsync(ModuleEndpoint endpoint, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(endpoint);

        switch (endpoint.Transport)
        {
            case ModuleTransportKind.UnixDomainSocket:
                var socket = new Socket(AddressFamily.Unix, SocketType.Stream, ProtocolType.Unspecified);
                try
                {
                    await socket.ConnectAsync(new UnixDomainSocketEndPoint(endpoint.Address), cancellationToken)
                        .ConfigureAwait(false);
                    return new NetworkStream(socket, ownsSocket: true);
                }
                catch
                {
                    socket.Dispose();
                    throw;
                }

            case ModuleTransportKind.NamedPipe:
                var pipe = new NamedPipeClientStream(
                    ".",
                    endpoint.Address,
                    PipeDirection.InOut,
                    PipeOptions.Asynchronous);
                try
                {
                    await pipe.ConnectAsync(cancellationToken).ConfigureAwait(false);
                    return pipe;
                }
                catch
                {
                    await pipe.DisposeAsync().ConfigureAwait(false);
                    throw;
                }

            default:
                throw new NotSupportedException(
                    $"Endpoint transport {endpoint.Transport} is not a socket or pipe.");
        }
    }
}

/// <summary>
/// Caches the HTTP invoker used by the module REST proxy for each module.
/// </summary>
/// <remarks>
/// A socket endpoint can only be reached by a handler bound to that socket, so the invoker is
/// per module. TCP endpoints keep a single shared invoker because the destination authority
/// already identifies the module host.
/// </remarks>
internal sealed class ModuleHttpInvokerCache : IDisposable
{
    private readonly ConcurrentDictionary<string, HttpMessageInvoker> _socketInvokers = new(StringComparer.Ordinal);
    private readonly HttpMessageInvoker _tcpInvoker;
    private bool _disposed;

    /// <summary>
    /// Initializes a new instance of the <see cref="ModuleHttpInvokerCache"/> class.
    /// </summary>
    public ModuleHttpInvokerCache()
    {
        _tcpInvoker = new HttpMessageInvoker(ModuleHttpTransport.CreateHandler(ModuleEndpoint.Tcp(80)));
    }

    /// <summary>
    /// Resolves the forwarder destination prefix and invoker for a module.
    /// </summary>
    /// <param name="moduleId">The module id.</param>
    /// <param name="liveEndpoint">The endpoint the supervisor reports for the running module.</param>
    /// <returns>The destination prefix (absolute URI) and the invoker to use.</returns>
    public (string DestinationPrefix, HttpMessageInvoker Invoker) Resolve(string moduleId, string liveEndpoint)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(moduleId);
        ArgumentException.ThrowIfNullOrWhiteSpace(liveEndpoint);
        ObjectDisposedException.ThrowIf(_disposed, this);

        if (ModuleEndpoint.TryParse(liveEndpoint, out var endpoint) && endpoint.IsSocketTransport)
        {
            var invoker = _socketInvokers.GetOrAdd(
                moduleId,
                _ => new HttpMessageInvoker(ModuleHttpTransport.CreateHandler(endpoint)));

            // The socket is dialled by the ConnectCallback, so the authority is a placeholder.
            return ("http://localhost/", invoker);
        }

        return (liveEndpoint.EndsWith('/') ? liveEndpoint : liveEndpoint + "/", _tcpInvoker);
    }

    /// <summary>
    /// Drops the cached invoker for a module (called when a module stops).
    /// </summary>
    /// <param name="moduleId">The module id.</param>
    public void Invalidate(string moduleId)
    {
        if (_socketInvokers.TryRemove(moduleId, out var invoker))
        {
            invoker.Dispose();
        }
    }

    /// <inheritdoc />
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        foreach (var invoker in _socketInvokers.Values)
        {
            invoker.Dispose();
        }

        _socketInvokers.Clear();
        _tcpInvoker.Dispose();
        _disposed = true;
    }
}
