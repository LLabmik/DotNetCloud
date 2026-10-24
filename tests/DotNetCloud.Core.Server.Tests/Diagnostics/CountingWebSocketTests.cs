using System.Net.WebSockets;
using System.Text;
using DotNetCloud.Core.Server.Diagnostics;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;

namespace DotNetCloud.Core.Server.Tests.Diagnostics;

/// <summary>
/// Tests for <see cref="CountingWebSocketFeature"/> and <see cref="CountingWebSocket"/>.
/// </summary>
[TestClass]
public sealed class CountingWebSocketTests
{
    [TestMethod]
    public void IsWebSocketRequest_Delegates()
    {
        var inner = new FakeWebSocketFeature { IsWebSocketRequest = true };
        var feature = new CountingWebSocketFeature(inner, _ => { }, _ => { });

        Assert.IsTrue(feature.IsWebSocketRequest);
    }

    [TestMethod]
    public async Task AcceptAsync_WrapsSocket()
    {
        var socket = new FakeWebSocket();
        var inner = new FakeWebSocketFeature { SocketToReturn = socket };
        var feature = new CountingWebSocketFeature(inner, _ => { }, _ => { });

        var accepted = await feature.AcceptAsync(new WebSocketAcceptContext());

        Assert.IsNotNull(accepted);
        Assert.IsTrue(accepted is CountingWebSocket);
    }

    [TestMethod]
    public async Task SendAsync_PayloadBytes_CountAsOut()
    {
        long outBytes = 0;
        var counting = new CountingWebSocket(new FakeWebSocket(), _ => { }, n => outBytes += n);

        await counting.SendAsync(new byte[32], WebSocketMessageType.Binary, true, CancellationToken.None);

        Assert.AreEqual(32, outBytes);
    }

    [TestMethod]
    public async Task SendAsync_FragmentedMessages_CountEachFragment()
    {
        long outBytes = 0;
        var counting = new CountingWebSocket(new FakeWebSocket(), _ => { }, n => outBytes += n);

        await counting.SendAsync(new byte[10], WebSocketMessageType.Text, false, CancellationToken.None);
        await counting.SendAsync(new byte[5], WebSocketMessageType.Text, true, CancellationToken.None);

        Assert.AreEqual(15, outBytes);
    }

    [TestMethod]
    public async Task SendAsync_CloseFrame_CountsNothing()
    {
        long outBytes = 0;
        var counting = new CountingWebSocket(new FakeWebSocket(), _ => { }, n => outBytes += n);

        await counting.SendAsync(new byte[2], WebSocketMessageType.Close, true, CancellationToken.None);

        Assert.AreEqual(0, outBytes);
    }

    [TestMethod]
    public async Task ReceiveAsync_PayloadBytes_CountAsIn()
    {
        long inBytes = 0;
        var socket = new FakeWebSocket { NextReceive = (Encoding.UTF8.GetBytes("hello"), WebSocketMessageType.Text) };
        var counting = new CountingWebSocket(socket, n => inBytes += n, _ => { });

        var result = await counting.ReceiveAsync(new byte[64], CancellationToken.None);

        Assert.AreEqual(5, result.Count);
        Assert.AreEqual(5, inBytes);
    }

    [TestMethod]
    public async Task ReceiveAsync_CloseFrame_CountsNothing()
    {
        long inBytes = 0;
        var socket = new FakeWebSocket { NextReceive = ([], WebSocketMessageType.Close) };
        var counting = new CountingWebSocket(socket, n => inBytes += n, _ => { });

        await counting.ReceiveAsync(new byte[64], CancellationToken.None);

        Assert.AreEqual(0, inBytes);
    }

    [TestMethod]
    public async Task CloseAsync_DelegatesToInnerSocket()
    {
        var socket = new FakeWebSocket();
        var counting = new CountingWebSocket(socket, _ => { }, _ => { });

        await counting.CloseAsync(WebSocketCloseStatus.NormalClosure, "bye", CancellationToken.None);

        Assert.AreEqual(WebSocketCloseStatus.NormalClosure, socket.CloseStatus);
        Assert.AreEqual("bye", socket.CloseStatusDescription);
    }

    [TestMethod]
    public void StateAndSubProtocol_Delegate()
    {
        var socket = new FakeWebSocket();
        var counting = new CountingWebSocket(socket, _ => { }, _ => { });

        Assert.AreEqual(WebSocketState.Open, counting.State);
        Assert.AreEqual("signalr", counting.SubProtocol);
    }

    private sealed class FakeWebSocketFeature : IHttpWebSocketFeature
    {
        public bool IsWebSocketRequest { get; set; }

        public WebSocket? SocketToReturn { get; set; }

        public Task<WebSocket> AcceptAsync(WebSocketAcceptContext context) =>
            Task.FromResult(SocketToReturn!);
    }

    private sealed class FakeWebSocket : WebSocket
    {
        private WebSocketCloseStatus? _closeStatus;
        private string? _closeStatusDescription;

        public (byte[] Payload, WebSocketMessageType Type) NextReceive { get; set; } = ([], WebSocketMessageType.Binary);

        public override WebSocketCloseStatus? CloseStatus => _closeStatus;

        public override string? CloseStatusDescription => _closeStatusDescription;

        public override WebSocketState State => WebSocketState.Open;

        public override string? SubProtocol => "signalr";

        public override void Abort() { }

        public override Task CloseAsync(
            WebSocketCloseStatus closeStatus,
            string? statusDescription,
            CancellationToken cancellationToken)
        {
            _closeStatus = closeStatus;
            _closeStatusDescription = statusDescription;
            return Task.CompletedTask;
        }

        public override Task CloseOutputAsync(
            WebSocketCloseStatus closeStatus,
            string? statusDescription,
            CancellationToken cancellationToken)
        {
            _closeStatus = closeStatus;
            _closeStatusDescription = statusDescription;
            return Task.CompletedTask;
        }

        public override void Dispose() { }

        public override Task<WebSocketReceiveResult> ReceiveAsync(
            ArraySegment<byte> buffer,
            CancellationToken cancellationToken) =>
            Task.FromResult(new WebSocketReceiveResult(
                NextReceive.Payload.Length, NextReceive.Type, endOfMessage: true));

        public override Task SendAsync(
            ArraySegment<byte> buffer,
            WebSocketMessageType messageType,
            bool endOfMessage,
            CancellationToken cancellationToken) => Task.CompletedTask;
    }
}
