using System.Collections.Concurrent;
using System.Security.Cryptography;

namespace DotNetCloud.Core.Server.Consent;

/// <summary>The user's answer to a consent prompt.</summary>
internal enum ConsentDecision
{
    /// <summary>The prompt has been rendered but not answered.</summary>
    Pending,

    /// <summary>The user approved the requested scopes.</summary>
    Approved,

    /// <summary>The user declined, so the authorization request fails with <c>access_denied</c>.</summary>
    Denied,
}

/// <summary>
/// One in-flight consent prompt: what the client asked for, and the user's answer.
/// </summary>
/// <param name="RequestId">The unguessable id carried through the form and back to the authorize endpoint.</param>
/// <param name="UserId">The signed-in user the prompt belongs to.</param>
/// <param name="ClientId">The OAuth2 client id that triggered the flow.</param>
/// <param name="ModuleId">The installed module the client id maps to.</param>
/// <param name="ClientDisplayName">The client's display name, shown on the prompt.</param>
/// <param name="Scopes">The consent scopes requested that are not yet granted.</param>
/// <param name="AuthorizeQueryString">The original authorize query string, replayed after the decision.</param>
/// <param name="CreatedAtUtc">When the prompt was created (used for expiry).</param>
/// <param name="Decision">The recorded answer.</param>
internal sealed record PendingConsentRequest(
    string RequestId,
    Guid UserId,
    string ClientId,
    string ModuleId,
    string ClientDisplayName,
    IReadOnlyList<string> Scopes,
    string AuthorizeQueryString,
    DateTimeOffset CreatedAtUtc,
    ConsentDecision Decision)
{
    /// <summary>Gets a value indicating whether the prompt is still awaiting an answer.</summary>
    public bool IsPending => Decision is ConsentDecision.Pending;
}

/// <summary>
/// Holds the short-lived state of an in-flight consent prompt.
/// </summary>
/// <remarks>
/// <para>
/// The authorize endpoint cannot render a form itself, and the consent form must not be able to
/// smuggle parameters back into the authorization request. So the original query string is kept
/// server-side, keyed by an unguessable id that is bound to the signed-in user; the form carries
/// only that id and a decision. Everything else — client id, scopes, redirect target — comes from
/// this store, not from the browser.
/// </para>
/// <para>
/// Entries live for <see cref="Lifetime"/>, are capped at <see cref="MaxEntries"/>, and are
/// consumed exactly once by the authorize endpoint, so a captured id cannot flip a decision or
/// replay an approval.
/// </para>
/// </remarks>
internal sealed class ConsentRequestStore
{
    /// <summary>How long a user has to answer a prompt.</summary>
    internal static readonly TimeSpan Lifetime = TimeSpan.FromMinutes(10);

    /// <summary>Upper bound on concurrent prompts; the oldest entry is dropped first.</summary>
    internal const int MaxEntries = 1024;

    private readonly ConcurrentDictionary<string, PendingConsentRequest> _entries =
        new(StringComparer.Ordinal);

    private readonly TimeProvider _timeProvider;

    /// <summary>Initializes a new instance of the <see cref="ConsentRequestStore"/> class.</summary>
    /// <param name="timeProvider">Time source (overridable in tests).</param>
    public ConsentRequestStore(TimeProvider timeProvider)
    {
        _timeProvider = timeProvider;
    }

    /// <summary>Creates a pending prompt and returns its id.</summary>
    /// <param name="userId">The signed-in user.</param>
    /// <param name="clientId">The OAuth2 client id.</param>
    /// <param name="moduleId">The installed module id the client maps to.</param>
    /// <param name="clientDisplayName">The display name for the prompt.</param>
    /// <param name="scopes">The scopes the user has not yet approved.</param>
    /// <param name="authorizeQueryString">The original authorize query string.</param>
    /// <returns>The request id to put in the form.</returns>
    public string Create(
        Guid userId,
        string clientId,
        string moduleId,
        string clientDisplayName,
        IReadOnlyList<string> scopes,
        string authorizeQueryString)
    {
        Prune();

        var requestId = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32))
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');

        var request = new PendingConsentRequest(
            requestId,
            userId,
            clientId,
            moduleId,
            clientDisplayName,
            scopes,
            authorizeQueryString,
            _timeProvider.GetUtcNow(),
            ConsentDecision.Pending);

        _entries[requestId] = request;

        if (_entries.Count > MaxEntries)
        {
            foreach (var stale in _entries.Values
                         .OrderBy(entry => entry.CreatedAtUtc)
                         .Take(_entries.Count - MaxEntries))
            {
                _entries.TryRemove(stale.RequestId, out _);
            }
        }

        return requestId;
    }

    /// <summary>
    /// Finds a pending prompt, checking that it belongs to the given user and has not expired.
    /// </summary>
    /// <param name="requestId">The request id from the form or query string.</param>
    /// <param name="userId">The signed-in user.</param>
    /// <returns>The entry, or <see langword="null"/> when it is unknown, expired or owned by someone else.</returns>
    public PendingConsentRequest? Find(string? requestId, Guid userId)
    {
        if (string.IsNullOrWhiteSpace(requestId) || userId == Guid.Empty)
        {
            return null;
        }

        if (!_entries.TryGetValue(requestId, out var entry))
        {
            return null;
        }

        if (IsExpired(entry))
        {
            _entries.TryRemove(requestId, out _);
            return null;
        }

        return entry.UserId == userId ? entry : null;
    }

    /// <summary>Records the user's answer on a pending prompt.</summary>
    /// <param name="requestId">The request id.</param>
    /// <param name="userId">The signed-in user.</param>
    /// <param name="decision">The answer.</param>
    /// <returns><see langword="true"/> when the answer was recorded.</returns>
    public bool RecordDecision(string? requestId, Guid userId, ConsentDecision decision)
    {
        var entry = Find(requestId, userId);
        if (entry is null || !entry.IsPending)
        {
            return false;
        }

        return _entries.TryUpdate(requestId!, entry with { Decision = decision }, entry);
    }

    /// <summary>Removes and returns an answered prompt (single use).</summary>
    /// <param name="requestId">The request id.</param>
    /// <param name="userId">The signed-in user.</param>
    /// <returns>The entry, or <see langword="null"/> when it is unknown or owned by someone else.</returns>
    public PendingConsentRequest? Consume(string? requestId, Guid userId)
    {
        var entry = Find(requestId, userId);
        if (entry is null)
        {
            return null;
        }

        return _entries.TryRemove(requestId!, out var removed) ? removed : entry;
    }

    private bool IsExpired(PendingConsentRequest entry) =>
        _timeProvider.GetUtcNow() - entry.CreatedAtUtc > Lifetime;

    private void Prune()
    {
        foreach (var entry in _entries.Values)
        {
            if (IsExpired(entry))
            {
                _entries.TryRemove(entry.RequestId, out _);
            }
        }
    }
}
