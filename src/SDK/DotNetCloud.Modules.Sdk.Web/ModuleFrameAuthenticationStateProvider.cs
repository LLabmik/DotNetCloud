using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.Extensions.Logging;

namespace DotNetCloud.Modules.Sdk.Web;

/// <summary>
/// Supplies a module UI's authentication state from the module host instead of a cookie.
/// </summary>
/// <remarks>
/// The frame carries no identity of its own — the core proxy mints a short-lived assertion per
/// request — so the UI asks its host who the acting user is (<c>GET api/v1/me</c>) and turns that
/// into a <see cref="ClaimsPrincipal"/>. The result is cached for the lifetime of the page: an
/// opaque-origin frame cannot observe a sign-out happening in the parent document. A host that does
/// not expose the endpoint, or a signed-out frame, yields an anonymous principal rather than an
/// exception thrown into a render.
/// </remarks>
public sealed class ModuleFrameAuthenticationStateProvider : AuthenticationStateProvider
{
    private readonly ModuleApiClient _api;
    private readonly ILogger<ModuleFrameAuthenticationStateProvider> _logger;
    private readonly SemaphoreSlim _gate = new(1, 1);
    private Task<AuthenticationState>? _cached;
    private ModuleSessionInfo? _session;

    /// <summary>Initializes a new instance of the <see cref="ModuleFrameAuthenticationStateProvider"/> class.</summary>
    /// <param name="api">The module API client.</param>
    /// <param name="logger">Logger.</param>
    public ModuleFrameAuthenticationStateProvider(
        ModuleApiClient api,
        ILogger<ModuleFrameAuthenticationStateProvider> logger)
    {
        _api = api;
        _logger = logger;
    }

    /// <summary>Gets the acting user, loading it once.</summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The session, or <see langword="null"/> when the host could not identify the caller.</returns>
    public async Task<ModuleSessionInfo?> GetSessionAsync(CancellationToken cancellationToken = default)
    {
        if (_session is not null)
        {
            return _session;
        }

        try
        {
            _session = await _api.SendAsync<ModuleSessionInfo>(
                HttpMethod.Get,
                "api/v1/me",
                cancellationToken: cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is ModuleApiException or HttpRequestException or NotSupportedException or JsonException)
        {
            // A signed-out frame answers 401 and a host without the endpoint answers 404; both mean
            // "no session", not a broken page.
            _logger.LogDebug(ex, "The module host did not report a session");
            _session = null;
        }

        return _session;
    }

    /// <inheritdoc />
    public override async Task<AuthenticationState> GetAuthenticationStateAsync()
    {
        if (_cached is not null)
        {
            return await _cached.ConfigureAwait(false);
        }

        await _gate.WaitAsync().ConfigureAwait(false);
        try
        {
            _cached ??= BuildAsync();
        }
        finally
        {
            _gate.Release();
        }

        return await _cached.ConfigureAwait(false);
    }

    private async Task<AuthenticationState> BuildAsync()
    {
        var session = await GetSessionAsync().ConfigureAwait(false);
        if (session is null || session.UserId == Guid.Empty)
        {
            return new AuthenticationState(new ClaimsPrincipal(new ClaimsIdentity()));
        }

        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, session.UserId.ToString()),
        };

        if (!string.IsNullOrWhiteSpace(session.DisplayName))
        {
            claims.Add(new Claim(ClaimTypes.Name, session.DisplayName));
        }

        if (!string.IsNullOrWhiteSpace(session.Email))
        {
            claims.Add(new Claim(ClaimTypes.Email, session.Email));
        }

        var identity = new ClaimsIdentity(claims, "ModuleAssertion");
        return new AuthenticationState(new ClaimsPrincipal(identity));
    }
}
