using System.Net;
using System.Net.Http.Headers;
using System.Text;
using DotNetCloud.Client.Core.Sync;
using Microsoft.Extensions.Logging.Abstractions;

namespace DotNetCloud.Client.Core.Tests.Sync;

[TestClass]
public sealed class SyncStreamListenerTests
{
    // ============================================
    // Authorization header
    // ============================================

    [TestMethod]
    public async Task ConnectAsync_SendsBearerTokenInAuthorizationHeader()
    {
        var handler = new ControlledHttpMessageHandler();
        handler.QueueResponse(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StreamContent(new SseStream("event: sync-changed\ndata: {\"latestSequence\":42}\n\n"))
        });

        var listener = CreateListener(handler, accessToken: "test-jwt-token");

        try
        {
            var connectTask = StartAndWaitForConnected(listener);
            var sentRequest = await handler.RequestReceived;

            Assert.AreEqual("Bearer", sentRequest.Headers.Authorization?.Scheme);
            Assert.AreEqual("test-jwt-token", sentRequest.Headers.Authorization?.Parameter);
            Assert.AreEqual("text/event-stream", sentRequest.Headers.Accept.First().MediaType);
        }
        finally
        {
            await listener.StopAsync();
        }
    }

    [TestMethod]
    public async Task ConnectAsync_NoAccessToken_NoAuthorizationHeader()
    {
        var handler = new ControlledHttpMessageHandler();
        handler.QueueResponse(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StreamContent(new SseStream("\n"))
        });

        var listener = CreateListener(handler, accessToken: null);

        try
        {
            await StartAndWaitForConnected(listener);
            var sentRequest = await handler.RequestReceived;
            Assert.IsNull(sentRequest.Headers.Authorization);
        }
        finally
        {
            await listener.StopAsync();
        }
    }

    // ============================================
    // Token refresh on 401
    // ============================================

    [TestMethod]
    public async Task Unauthorized_TriggersTokenRefresh()
    {
        var handler = new ControlledHttpMessageHandler();
        bool refreshCalled = false;

        handler.QueueResponse(new HttpResponseMessage(HttpStatusCode.Unauthorized));
        handler.QueueResponse(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StreamContent(new SseStream("\n"))
        });

        var listener = CreateListener(handler, accessToken: "old-expired-token");
        listener.AccessTokenRefreshCallback = _ =>
        {
            refreshCalled = true;
            return Task.FromResult<string?>("new-refreshed-token");
        };

        try
        {
            await StartAndWaitForConnected(listener);

            Assert.IsTrue(refreshCalled, "Refresh callback was not called on 401.");
            Assert.AreEqual("new-refreshed-token", listener.AccessToken, "AccessToken not updated after refresh.");
        }
        finally
        {
            await listener.StopAsync();
        }
    }

    [TestMethod]
    public async Task Unauthorized_RefreshReturnsNull_StopsAfterBackoff()
    {
        var handler = new ControlledHttpMessageHandler();
        // First request: 401. Refresh fails (returns null).
        // Second attempt also 401. No more refresh (already failed once).
        handler.QueueResponse(new HttpResponseMessage(HttpStatusCode.Unauthorized));
        handler.QueueResponse(new HttpResponseMessage(HttpStatusCode.Unauthorized));

        var listener = CreateListener(handler, accessToken: "bad-token");
        listener.AccessTokenRefreshCallback = _ =>
            Task.FromResult<string?>(null); // refresh fails

        try
        {
            listener.Start();
            // Backoff on second 401 is 2s, so wait 3s total
            await Task.Delay(TimeSpan.FromSeconds(4));

            Assert.IsFalse(listener.IsConnected, "Should have stopped after 401s with failed refresh.");
        }
        finally
        {
            await listener.StopAsync();
        }
    }

    // ============================================
    // SSE event parsing
    // ============================================

    [TestMethod]
    public async Task SyncChangedEvent_FiresOnSseData()
    {
        var handler = new ControlledHttpMessageHandler();
        handler.QueueResponse(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StreamContent(new SseStream(
                "event: sync-changed\ndata: {\"latestSequence\":99}\n\n"))
        });

        SyncChangedEventArgs? capturedEvent = null;
        var listener = CreateListener(handler, accessToken: "tok");
        listener.SyncChanged += (_, e) => capturedEvent = e;

        try
        {
            await StartAndWaitForConnected(listener);
            await Task.Delay(300);

            Assert.IsNotNull(capturedEvent, "SyncChanged event was not fired.");
            Assert.AreEqual(99L, capturedEvent!.LatestSequence);
        }
        finally
        {
            await listener.StopAsync();
        }
    }

    [TestMethod]
    public async Task NonSyncChangedEvent_Ignored()
    {
        var handler = new ControlledHttpMessageHandler();
        handler.QueueResponse(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StreamContent(new SseStream("event: heartbeat\ndata: ping\n\n"))
        });

        int eventFiredCount = 0;
        var listener = CreateListener(handler, accessToken: "tok");
        listener.SyncChanged += (_, _) => eventFiredCount++;

        try
        {
            await StartAndWaitForConnected(listener);
            await Task.Delay(300);
            Assert.AreEqual(0, eventFiredCount, "Non-sync-changed event should not fire SyncChanged.");
        }
        finally
        {
            await listener.StopAsync();
        }
    }

    // ============================================
    // Connection lifecycle
    // ============================================

    [TestMethod]
    public async Task Connected_SetsIsConnectedTrue()
    {
        var handler = new ControlledHttpMessageHandler();
        handler.QueueResponse(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StreamContent(new SseStream("\n"))
        });

        var listener = CreateListener(handler, accessToken: "tok");

        try
        {
            await StartAndWaitForConnected(listener);
            Assert.IsTrue(listener.IsConnected);
        }
        finally
        {
            await listener.StopAsync();
        }

        Assert.IsFalse(listener.IsConnected, "IsConnected should be false after StopAsync.");
    }

    [TestMethod]
    public async Task Non200StatusCode_DoesNotSetConnected()
    {
        var handler = new ControlledHttpMessageHandler();
        handler.QueueResponse(new HttpResponseMessage(HttpStatusCode.ServiceUnavailable));

        var listener = CreateListener(handler, accessToken: "tok");

        try
        {
            listener.Start();
            await Task.Delay(500);
            Assert.IsFalse(listener.IsConnected, "IsConnected should remain false on non-200.");
        }
        finally
        {
            await listener.StopAsync();
        }
    }

    // ============================================
    // Helpers
    // ============================================

    private static SyncStreamListener CreateListener(ControlledHttpMessageHandler handler, string? accessToken)
    {
        var http = new HttpClient(handler) { BaseAddress = new Uri("https://cloud.example.com/") };
        return new SyncStreamListener(http, NullLogger<SyncStreamListener>.Instance)
        {
            AccessToken = accessToken,
        };
    }

    private static async Task StartAndWaitForConnected(SyncStreamListener listener)
    {
        listener.Start();
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        while (!listener.IsConnected && !cts.IsCancellationRequested)
            await Task.Delay(50, cts.Token);
    }
}

// ============================================
// Test Infrastructure
// ============================================

internal sealed class ControlledHttpMessageHandler : HttpMessageHandler
{
    private readonly Queue<HttpResponseMessage> _responses = new();
    private readonly TaskCompletionSource<HttpRequestMessage> _requestTcs =
        new(TaskCreationOptions.RunContinuationsAsynchronously);

    public int CallCount { get; private set; }
    public Task<HttpRequestMessage> RequestReceived => _requestTcs.Task;

    public void QueueResponse(HttpResponseMessage response) => _responses.Enqueue(response);

    protected override Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request, CancellationToken cancellationToken)
    {
        CallCount++;
        _requestTcs.TrySetResult(request);

        if (_responses.TryDequeue(out var response))
            return Task.FromResult(response);

        return Task.FromResult(new HttpResponseMessage(HttpStatusCode.InternalServerError));
    }
}

/// <summary>
/// A readable stream that yields SSE-formatted text, then blocks until disposed
/// (simulating a long-lived SSE connection).
/// </summary>
internal sealed class SseStream : Stream
{
    private readonly byte[] _initialData;
    private int _position;
    private readonly TaskCompletionSource _disposed = new();

    public SseStream(string sseText)
    {
        _initialData = Encoding.UTF8.GetBytes(sseText);
    }

    public override bool CanRead => true;
    public override bool CanSeek => false;
    public override bool CanWrite => false;
    public override long Length => throw new NotSupportedException();
    public override long Position
    {
        get => throw new NotSupportedException();
        set => throw new NotSupportedException();
    }

    public override int Read(byte[] buffer, int offset, int count)
    {
        return ReadAsync(buffer, offset, count).GetAwaiter().GetResult();
    }

    public override async Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
    {
        if (_position < _initialData.Length)
        {
            var remaining = _initialData.Length - _position;
            var toCopy = Math.Min(count, remaining);
            Array.Copy(_initialData, _position, buffer, offset, toCopy);
            _position += toCopy;
            return toCopy;
        }

        // After initial data, wait until disposed (simulates open SSE connection)
        await _disposed.Task.WaitAsync(cancellationToken);
        return 0;
    }

    public override void Flush() { }
    public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
    public override void SetLength(long value) => throw new NotSupportedException();
    public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();

    protected override void Dispose(bool disposing)
    {
        _disposed.TrySetResult();
        base.Dispose(disposing);
    }
}
