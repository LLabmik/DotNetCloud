using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;

namespace DotNetCloud.Client.Android.Services;

/// <summary>
/// HTTP implementation of <see cref="IChatAlertPoller"/> against <c>GET /api/v1/chat/alerts</c>.
/// </summary>
/// <remarks>
/// <para>
/// Registered as a typed HTTP client carrying <c>AuthenticatedHttpClientHandler</c>, so the bearer token
/// is attached and refreshed proactively — this class never touches tokens, exactly like the other
/// background REST clients.
/// </para>
/// <para>
/// The poll is <b>conditional</b>: the entity-tag from the previous response is echoed in
/// <c>If-None-Match</c>, so an unchanged instance answers <c>304</c> with no body and does not recompute
/// the aggregate. That is what keeps a poll every minute cheap for the server as well as the phone.
/// </para>
/// </remarks>
public sealed class ChatAlertPoller : IChatAlertPoller
{
    private const string AlertsPath = "/api/v1/chat/alerts";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    private readonly HttpClient _http;
    private readonly IServerConnectionStore _serverStore;
    private readonly ChatAlertStateStore _state;
    private readonly IChatAlertNotifier _notifier;
    private readonly IAppForegroundService _foreground;
    private readonly ILogger<ChatAlertPoller> _logger;

    /// <summary>Initializes a new <see cref="ChatAlertPoller"/>.</summary>
    /// <param name="http">HTTP client carrying the authenticated handler.</param>
    /// <param name="serverStore">Store used to resolve the active server connection.</param>
    /// <param name="state">Persisted poll state (entity-tag and high-water mark).</param>
    /// <param name="notifier">Platform notifier used to raise the alert.</param>
    /// <param name="foreground">Foreground tracker, used to suppress alerts while the app is visible.</param>
    /// <param name="logger">Logger.</param>
    public ChatAlertPoller(
        HttpClient http,
        IServerConnectionStore serverStore,
        ChatAlertStateStore state,
        IChatAlertNotifier notifier,
        IAppForegroundService foreground,
        ILogger<ChatAlertPoller> logger)
    {
        _http = http;
        _serverStore = serverStore;
        _state = state;
        _notifier = notifier;
        _foreground = foreground;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<ChatAlertPollResult> PollAsync(CancellationToken ct = default)
    {
        var connection = _serverStore.GetActive();
        if (connection is null || string.IsNullOrWhiteSpace(connection.ServerBaseUrl))
        {
            // Nothing to poll. Forget the cached state so a later sign-in starts from a clean slate
            // instead of inheriting a stale entity-tag from a different account.
            _state.SetETag(null);
            _state.SetLastAcknowledgedChangedAtUtc(null);
            _state.SetHasUnread(false);
            return ChatAlertPollResult.NoSession;
        }

        try
        {
            using var request = new HttpRequestMessage(
                HttpMethod.Get, $"{connection.ServerBaseUrl.TrimEnd('/')}{AlertsPath}");

            if (_state.GetETag() is { } etag)
                request.Headers.TryAddWithoutValidation("If-None-Match", etag);

            using var response = await _http
                .SendAsync(request, HttpCompletionOption.ResponseContentRead, ct)
                .ConfigureAwait(false);

            if (response.StatusCode == HttpStatusCode.NotModified)
            {
                // Keep the previous "unread outstanding" answer: a 304 body has no counts, and the
                // cadence must stay aggressive while messages are still waiting.
                _logger.LogDebug("Chat alert poll: aggregate unchanged.");
                return new ChatAlertPollResult(ChatAlertPollOutcome.UpToDate, _state.GetHasUnread());
            }

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogDebug("Chat alert poll returned {StatusCode}.", (int)response.StatusCode);
                return new ChatAlertPollResult(ChatAlertPollOutcome.Failed, _state.GetHasUnread());
            }

            if (response.Headers.ETag is { } responseETag)
                _state.SetETag(responseETag.Tag);

            var envelope = await response.Content
                .ReadFromJsonAsync<AlertsEnvelope>(JsonOptions, ct)
                .ConfigureAwait(false);

            if (envelope?.Data is not { } summary)
            {
                _logger.LogWarning("Chat alert poll response carried no payload.");
                return new ChatAlertPollResult(ChatAlertPollOutcome.Failed, _state.GetHasUnread());
            }

            var hasUnread = summary.UnmutedUnread > 0;
            _state.SetHasUnread(hasUnread);

            var decision = ChatAlertPollDecision.Decide(
                summary, _state.GetLastAcknowledgedChangedAtUtc());

            if (!decision.ShouldAlert)
                return new ChatAlertPollResult(ChatAlertPollOutcome.Suppressed, hasUnread);

            var posted = !_foreground.IsInForeground
                         && _notifier.Notify(decision, connection.ServerBaseUrl);

            // Account for the change either way. While the app is on screen the SignalR path and the
            // in-app ding already own the message, so advancing the high-water mark here is what stops a
            // stale notification appearing the moment the user leaves the app.
            if (decision.AcknowledgedChangedAtUtc is { } acknowledged)
                _state.SetLastAcknowledgedChangedAtUtc(acknowledged);

            return new ChatAlertPollResult(
                posted ? ChatAlertPollOutcome.Alerted : ChatAlertPollOutcome.Suppressed,
                hasUnread);
        }
        catch (Exception ex)
        {
            // Deliberately not rethrowing on cancellation: the wake path's time budget expiring is a
            // transient failure, and the chain must re-arm so the next run retries instead of stopping.
            _logger.LogWarning(ex, "Chat alert poll failed.");
            return new ChatAlertPollResult(ChatAlertPollOutcome.Failed, _state.GetHasUnread());
        }
    }

    /// <summary>Envelope shape of the module REST API (<c>{ success, data }</c>).</summary>
    /// <param name="Success">Whether the call succeeded.</param>
    /// <param name="Data">Aggregate payload.</param>
    private sealed record AlertsEnvelope(
        [property: JsonPropertyName("success")] bool Success,
        [property: JsonPropertyName("data")] ChatAlertsSummary? Data);
}
