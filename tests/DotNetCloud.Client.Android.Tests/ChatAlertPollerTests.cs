using System.Net;
using System.Text;
using DotNetCloud.Client.Android.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace DotNetCloud.Client.Android.Tests;

/// <summary>
/// Tests for <see cref="ChatAlertPoller"/>, the client half of the adopted "our code only" chat-alert
/// transport (plan §12): a conditional GET of the aggregate, then a decision.
/// </summary>
[TestClass]
public class ChatAlertPollerTests
{
    private const string ServerUrl = "https://cloud.example.test";
    private const string AlertsUrl = ServerUrl + "/api/v1/chat/alerts";

    private static readonly DateTime Changed = new(2026, 1, 1, 12, 0, 0, DateTimeKind.Utc);

    private Mock<IServerConnectionStore> _serverStore = null!;
    private Mock<IChatAlertNotifier> _notifier = null!;
    private Mock<IAppForegroundService> _foreground = null!;
    private InMemoryPreferences _preferences = null!;
    private ChatAlertStateStore _state = null!;
    private StubHandler _handler = null!;
    private ChatAlertPoller _poller = null!;

    [TestInitialize]
    public void Setup()
    {
        _serverStore = new Mock<IServerConnectionStore>();
        _serverStore.Setup(s => s.GetActive())
            .Returns(new ServerConnection(ServerUrl, "Test server", "test@example.test"));

        _notifier = new Mock<IChatAlertNotifier>();
        _notifier.Setup(n => n.Notify(It.IsAny<ChatAlertDecision>(), It.IsAny<string>()))
            .Returns(true);

        _foreground = new Mock<IAppForegroundService>();
        _foreground.SetupGet(f => f.IsInForeground).Returns(false);

        _preferences = new InMemoryPreferences();
        _state = new ChatAlertStateStore(_preferences);
        _handler = new StubHandler();
        _poller = new ChatAlertPoller(
            new HttpClient(_handler),
            _serverStore.Object,
            _state,
            _notifier.Object,
            _foreground.Object,
            NullLogger<ChatAlertPoller>.Instance);
    }

    // ── No session ──────────────────────────────────────────────────────────

    [TestMethod]
    public async Task PollAsync_WhenNoActiveConnection_ThenNoSessionAndCachedStateIsDropped()
    {
        _serverStore.Setup(s => s.GetActive()).Returns((ServerConnection?)null);
        _state.SetETag("\"stale\"");
        _state.SetHasUnread(true);
        _state.SetLastAcknowledgedChangedAtUtc(Changed);

        var result = await _poller.PollAsync();

        Assert.AreEqual(ChatAlertPollOutcome.NoSession, result.Outcome);
        Assert.IsFalse(result.HasUnread);
        Assert.IsNull(_state.GetETag(), "a stale token from another account must not survive sign-out");
        Assert.IsNull(_state.GetLastAcknowledgedChangedAtUtc());
        Assert.IsFalse(_state.GetHasUnread());
        _notifier.Verify(n => n.Notify(It.IsAny<ChatAlertDecision>(), It.IsAny<string>()), Times.Never);
    }

    // ── Conditional polling ─────────────────────────────────────────────────

    [TestMethod]
    public async Task PollAsync_WhenNotModified_ThenUpToDateAndUnreadFlagIsKept()
    {
        _state.SetETag("\"abc123\"");
        _state.SetHasUnread(true);
        _handler.Respond = _ => new HttpResponseMessage(HttpStatusCode.NotModified);

        var result = await _poller.PollAsync();

        Assert.AreEqual(ChatAlertPollOutcome.UpToDate, result.Outcome);
        Assert.IsTrue(result.HasUnread, "a 304 carries no counts, so the cadence must stay aggressive");
        _notifier.Verify(n => n.Notify(It.IsAny<ChatAlertDecision>(), It.IsAny<string>()), Times.Never);
    }

    [TestMethod]
    public async Task PollAsync_ThenSendsStoredETagAsIfNoneMatchToTheAlertsEndpoint()
    {
        _state.SetETag("\"abc123\"");
        _handler.Respond = _ => new HttpResponseMessage(HttpStatusCode.NotModified);

        await _poller.PollAsync();

        Assert.AreEqual(AlertsUrl, _handler.LastRequestUri?.ToString());
        Assert.AreEqual(HttpMethod.Get, _handler.LastMethod);
        Assert.AreEqual("\"abc123\"", _handler.LastIfNoneMatch);
    }

    [TestMethod]
    public async Task PollAsync_WhenNoETagIsStored_ThenSendsNoConditionalHeader()
    {
        _handler.Respond = _ => Json(SummaryJson());

        await _poller.PollAsync();

        Assert.IsNull(_handler.LastIfNoneMatch);
    }

    // ── Alerting ────────────────────────────────────────────────────────────

    [TestMethod]
    public async Task PollAsync_WhenUnreadReported_ThenAlertsAndRemembersState()
    {
        var channelId = Guid.CreateVersion7();
        _handler.Respond = _ => Json(
            SummaryJson(unmutedUnread: 2, unread: 3, topChannelId: channelId),
            etag: "\"e1\"");

        ChatAlertDecision? captured = null;
        string? capturedServer = null;
        _notifier
            .Setup(n => n.Notify(It.IsAny<ChatAlertDecision>(), It.IsAny<string>()))
            .Callback<ChatAlertDecision, string?>((decision, server) =>
            {
                captured = decision;
                capturedServer = server;
            })
            .Returns(true);

        var result = await _poller.PollAsync();

        Assert.AreEqual(ChatAlertPollOutcome.Alerted, result.Outcome);
        Assert.IsTrue(result.HasUnread);
        Assert.IsNotNull(captured);
        Assert.AreEqual(NotificationPayloadContract.PayloadTypeMessage, captured!.PayloadType);
        Assert.AreEqual(channelId, captured.ChannelId);
        Assert.AreEqual(ServerUrl, capturedServer);
        Assert.AreEqual("\"e1\"", _state.GetETag());
        Assert.IsTrue(_state.GetHasUnread());
        Assert.AreEqual(Changed, _state.GetLastAcknowledgedChangedAtUtc());
    }

    [TestMethod]
    public async Task PollAsync_WhenUnreadMentionInUnmutedChannel_ThenMentionWording()
    {
        _handler.Respond = _ => Json(SummaryJson(unmutedMentions: 1, mentions: 1));
        ChatAlertDecision? captured = null;
        _notifier
            .Setup(n => n.Notify(It.IsAny<ChatAlertDecision>(), It.IsAny<string>()))
            .Callback<ChatAlertDecision, string?>((decision, _) => captured = decision)
            .Returns(true);

        await _poller.PollAsync();

        Assert.IsNotNull(captured);
        Assert.AreEqual(NotificationPayloadContract.PayloadTypeMention, captured!.PayloadType);
    }

    [TestMethod]
    public async Task PollAsync_WhenAppIsInForeground_ThenNoNotificationButChangeIsAcknowledged()
    {
        _foreground.SetupGet(f => f.IsInForeground).Returns(true);
        _handler.Respond = _ => Json(SummaryJson());

        var result = await _poller.PollAsync();

        Assert.AreEqual(ChatAlertPollOutcome.Suppressed, result.Outcome);
        _notifier.Verify(n => n.Notify(It.IsAny<ChatAlertDecision>(), It.IsAny<string>()), Times.Never);
        Assert.AreEqual(
            Changed, _state.GetLastAcknowledgedChangedAtUtc(),
            "the in-app path already surfaced this message; leaving the app must not re-alert it");
    }

    [TestMethod]
    public async Task PollAsync_WhenChangeWasAlreadyAcknowledged_ThenNoSecondAlert()
    {
        _state.SetLastAcknowledgedChangedAtUtc(Changed);
        _handler.Respond = _ => Json(SummaryJson());

        var result = await _poller.PollAsync();

        Assert.AreEqual(ChatAlertPollOutcome.Suppressed, result.Outcome);
        _notifier.Verify(n => n.Notify(It.IsAny<ChatAlertDecision>(), It.IsAny<string>()), Times.Never);
    }

    [TestMethod]
    public async Task PollAsync_WhenNewerMessageArrivesAfterAcknowledged_ThenAlertsAgain()
    {
        _state.SetLastAcknowledgedChangedAtUtc(Changed.AddMinutes(-3));
        _handler.Respond = _ => Json(
            SummaryJson(changedAt: "\"2026-01-01T12:05:00Z\""));

        var result = await _poller.PollAsync();

        Assert.AreEqual(ChatAlertPollOutcome.Alerted, result.Outcome);
        Assert.AreEqual(
            new DateTime(2026, 1, 1, 12, 5, 0, DateTimeKind.Utc),
            _state.GetLastAcknowledgedChangedAtUtc());
    }

    [TestMethod]
    public async Task PollAsync_WhenOnlyMutedChannelsAreUnread_ThenNoAlert()
    {
        _handler.Respond = _ => Json(SummaryJson(unmutedUnread: 0, unread: 5, mentions: 2));

        var result = await _poller.PollAsync();

        Assert.AreEqual(ChatAlertPollOutcome.Suppressed, result.Outcome);
        Assert.IsFalse(result.HasUnread);
        _notifier.Verify(n => n.Notify(It.IsAny<ChatAlertDecision>(), It.IsAny<string>()), Times.Never);
    }

    [TestMethod]
    public async Task PollAsync_WhenNotifierDeclines_ThenSuppressedRatherThanAlerted()
    {
        _handler.Respond = _ => Json(SummaryJson());
        _notifier
            .Setup(n => n.Notify(It.IsAny<ChatAlertDecision>(), It.IsAny<string>()))
            .Returns(false);

        var result = await _poller.PollAsync();

        Assert.AreEqual(ChatAlertPollOutcome.Suppressed, result.Outcome);
    }

    [TestMethod]
    public async Task PollAsync_WhenTimestampHasNoKind_ThenStoresUtcHighWaterMark()
    {
        _handler.Respond = _ => Json(SummaryJson(changedAt: "\"2026-01-01T12:00:00\""));

        var result = await _poller.PollAsync();

        Assert.AreEqual(ChatAlertPollOutcome.Alerted, result.Outcome);
        var stored = _state.GetLastAcknowledgedChangedAtUtc();
        Assert.IsNotNull(stored);
        Assert.AreEqual(DateTimeKind.Utc, stored!.Value.Kind);
        Assert.AreEqual(Changed, stored.Value);
    }

    // ── Failure handling ────────────────────────────────────────────────────

    [TestMethod]
    public async Task PollAsync_WhenServerReturnsError_ThenFailed()
    {
        _state.SetHasUnread(true);
        _handler.Respond = _ => new HttpResponseMessage(HttpStatusCode.InternalServerError);

        var result = await _poller.PollAsync();

        Assert.AreEqual(ChatAlertPollOutcome.Failed, result.Outcome);
        Assert.IsTrue(result.HasUnread, "a failed poll must not downgrade the cadence");
    }

    [TestMethod]
    public async Task PollAsync_WhenBodyIsNotJson_ThenFailed()
    {
        _handler.Respond = _ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("<html>not json</html>", Encoding.UTF8, "text/html"),
        };

        var result = await _poller.PollAsync();

        Assert.AreEqual(ChatAlertPollOutcome.Failed, result.Outcome);
    }

    [TestMethod]
    public async Task PollAsync_WhenEnvelopeHasNoData_ThenFailed()
    {
        _handler.Respond = _ => Json("""{"success": true, "data": null}""");

        var result = await _poller.PollAsync();

        Assert.AreEqual(ChatAlertPollOutcome.Failed, result.Outcome);
    }

    [TestMethod]
    public async Task PollAsync_WhenTransportThrows_ThenFailedInsteadOfPropagating()
    {
        _handler.ThrowOnSend = new HttpRequestException("no network");

        var result = await _poller.PollAsync();

        Assert.AreEqual(ChatAlertPollOutcome.Failed, result.Outcome);
    }

    // ── Helpers ─────────────────────────────────────────────────────────────

    private static HttpResponseMessage Json(string body, string? etag = null)
    {
        var response = new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(body, Encoding.UTF8, "application/json"),
        };

        if (etag is not null)
            response.Headers.ETag = new System.Net.Http.Headers.EntityTagHeaderValue(etag);

        return response;
    }

    /// <summary>
    /// Builds the wire response shape by hand — deliberately not via the client DTO, so a contract
    /// mismatch (e.g. a renamed JSON property) fails the test instead of being papered over.
    /// </summary>
    private static string SummaryJson(
        int unmutedUnread = 1,
        int unmutedMentions = 0,
        int unread = 1,
        int mentions = 0,
        Guid? topChannelId = null,
        string changedAt = "\"2026-01-01T12:00:00Z\"")
    {
        var channel = topChannelId is { } id ? $"\"{id:D}\"" : "null";

        return $$"""
            {
              "success": true,
              "data": {
                "v": 1,
                "unread": {{unread}},
                "mentions": {{mentions}},
                "unmutedUnread": {{unmutedUnread}},
                "unmutedMentions": {{unmutedMentions}},
                "topChannelId": {{channel}},
                "changedAt": {{changedAt}}
              }
            }
            """;
    }

    /// <summary>Programmable <see cref="HttpMessageHandler"/> that records the last request.</summary>
    private sealed class StubHandler : HttpMessageHandler
    {
        public Func<HttpRequestMessage, HttpResponseMessage>? Respond { get; set; }

        public Exception? ThrowOnSend { get; set; }

        public Uri? LastRequestUri { get; private set; }

        public HttpMethod? LastMethod { get; private set; }

        public string? LastIfNoneMatch { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            LastRequestUri = request.RequestUri;
            LastMethod = request.Method;
            LastIfNoneMatch = request.Headers.IfNoneMatch.Count > 0
                ? request.Headers.IfNoneMatch.ToString()
                : null;

            if (ThrowOnSend is { } exception)
                throw exception;

            return Task.FromResult(
                Respond?.Invoke(request) ?? new HttpResponseMessage(HttpStatusCode.NotImplemented));
        }
    }
}
