using System.Net.WebSockets;
using Microsoft.AspNetCore.Http.Features;

namespace DotNetCloud.Core.Server.Diagnostics;

/// <summary>
/// WebSocket feature that returns a counting WebSocket wrapper, so SignalR and
/// Blazor circuit frames count toward in/out traffic.
/// </summary>
internal sealed class CountingWebSocketFeature : IHttpWebSocketFeature
{
    private readonly IHttpWebSocketFeature _inner;
    private readonly Action<long> _addIn;
    private readonly Action<long> _addOut;

    /// <summary>
    /// Initializes a new instance of the <see cref="CountingWebSocketFeature"/> class.
    /// </summary>
    /// <param name="inner">The feature being wrapped.</param>
    /// <param name="addIn">Callback invoked with the number of bytes received.</param>
    /// <param name="addOut">Callback invoked with the number of bytes sent.</param>
    public CountingWebSocketFeature(IHttpWebSocketFeature inner, Action<long> addIn, Action<long> addOut)
    {
        _inner = inner ?? throw new ArgumentNullException(nameof(inner));
        _addIn = addIn ?? throw new ArgumentNullException(nameof(addIn));
        _addOut = addOut ?? throw new ArgumentNullException(nameof(addOut));
    }

    /// <inheritdoc />
    public bool IsWebSocketRequest => _inner.IsWebSocketRequest;

    /// <inheritdoc />
    public async Task<WebSocket> AcceptAsync(WebSocketAcceptContext context)
    {
        var socket = await _inner.AcceptAsync(context).ConfigureAwait(false);
        return socket is null ? null! : new CountingWebSocket(socket, _addIn, _addOut);
    }
}

/// <summary>
/// Delegating <see cref="WebSocket"/> wrapper that counts application payload bytes.
/// </summary>
/// <remarks>
/// Every member delegates verbatim so SignalR negotiation, keepalives and close
/// handshakes behave exactly as before. Close frames are not counted as traffic.
/// </remarks>
internal sealed class CountingWebSocket : WebSocket
{
    private readonly WebSocket _inner;
    private readonly Action<long> _addIn;
    private readonly Action<long> _addOut;

    public CountingWebSocket(WebSocket inner, Action<long> addIn, Action<long> addOut)
    {
        _inner = inner ?? throw new ArgumentNullException(nameof(inner));
        _addIn = addIn ?? throw new ArgumentNullException(nameof(addIn));
        _addOut = addOut ?? throw new ArgumentNullException(nameof(addOut));
    }

    public override WebSocketCloseStatus? CloseStatus => _inner.CloseStatus;

    public override string? CloseStatusDescription => _inner.CloseStatusDescription;

    public override WebSocketState State => _inner.State;

    public override string? SubProtocol => _inner.SubProtocol;

    public override void Abort() => _inner.Abort();

    public override Task CloseAsync(
        WebSocketCloseStatus closeStatus,
        string? statusDescription,
        CancellationToken cancellationToken) =>
        _inner.CloseAsync(closeStatus, statusDescription, cancellationToken);

    public override Task CloseOutputAsync(
        WebSocketCloseStatus closeStatus,
        string? statusDescription,
        CancellationToken cancellationToken) =>
        _inner.CloseOutputAsync(closeStatus, statusDescription, cancellationToken);

    public override void Dispose() => _inner.Dispose();

    public override Task<WebSocketReceiveResult> ReceiveAsync(
        ArraySegment<byte> buffer,
        CancellationToken cancellationToken) =>
        CountReceiveAsync(_inner.ReceiveAsync(buffer, cancellationToken));

    public override ValueTask<ValueWebSocketReceiveResult> ReceiveAsync(
        Memory<byte> buffer,
        CancellationToken cancellationToken = default) =>
        CountReceiveAsync(_inner.ReceiveAsync(buffer, cancellationToken));

    public override Task SendAsync(
        ArraySegment<byte> buffer,
        WebSocketMessageType messageType,
        bool endOfMessage,
        CancellationToken cancellationToken) =>
        CountSendAsync(
            _inner.SendAsync(buffer, messageType, endOfMessage, cancellationToken),
            messageType,
            buffer.Count);

    public override ValueTask SendAsync(
        ReadOnlyMemory<byte> buffer,
        WebSocketMessageType messageType,
        bool endOfMessage,
        CancellationToken cancellationToken = default) =>
        CountSendAsync(
            _inner.SendAsync(buffer, messageType, endOfMessage, cancellationToken),
            messageType,
            buffer.Length);

    private async Task<WebSocketReceiveResult> CountReceiveAsync(Task<WebSocketReceiveResult> receiveTask)
    {
        var result = await receiveTask.ConfigureAwait(false);
        if (result.MessageType != WebSocketMessageType.Close && result.Count > 0)
        {
            _addIn(result.Count);
        }

        return result;
    }

    private async ValueTask<ValueWebSocketReceiveResult> CountReceiveAsync(
        ValueTask<ValueWebSocketReceiveResult> receiveTask)
    {
        var result = await receiveTask.ConfigureAwait(false);
        if (result.MessageType != WebSocketMessageType.Close && result.Count > 0)
        {
            _addIn(result.Count);
        }

        return result;
    }

    private async Task CountSendAsync(Task sendTask, WebSocketMessageType messageType, int count)
    {
        await sendTask.ConfigureAwait(false);
        if (messageType != WebSocketMessageType.Close && count > 0)
        {
            _addOut(count);
        }
    }

    private async ValueTask CountSendAsync(
        ValueTask sendTask,
        WebSocketMessageType messageType,
        int count)
    {
        await sendTask.ConfigureAwait(false);
        if (messageType != WebSocketMessageType.Close && count > 0)
        {
            _addOut(count);
        }
    }
}
