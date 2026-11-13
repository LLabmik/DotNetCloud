using System.Security.Claims;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.JSInterop;

namespace DotNetCloud.Modules.Tracks.Web.Services;

/// <summary>
/// The acting user as the module UI sees it.
/// </summary>
public sealed class ModuleSessionInfo
{
    /// <summary>Gets the user id.</summary>
    public Guid UserId { get; init; }

    /// <summary>Gets the display name, when the core could resolve one.</summary>
    public string? DisplayName { get; init; }

    /// <summary>Gets the email address, when the user consented to the profile scope.</summary>
    public string? Email { get; init; }
}

/// <summary>
/// Supplies the module UI's authentication state from the module host instead of a cookie.
/// </summary>
/// <remarks>
/// The frame carries no identity of its own — the core proxy mints a short-lived assertion per request
/// — so the UI asks its host who the acting user is (<c>GET api/v1/me</c>) and turns that into a
/// <see cref="ClaimsPrincipal"/>. The result is cached for the lifetime of the page: an opaque-origin
/// frame cannot observe a sign-out happening in the parent document.
/// </remarks>
public sealed class TracksAuthenticationStateProvider : AuthenticationStateProvider
{
    private readonly TracksApi _api;
    private readonly IJSRuntime _js;
    private readonly SemaphoreSlim _gate = new(1, 1);
    private Task<AuthenticationState>? _cached;
    private ModuleSessionInfo? _session;

    /// <summary>Initializes a new instance of the <see cref="TracksAuthenticationStateProvider"/> class.</summary>
    /// <param name="api">The module API client.</param>
    /// <param name="js">JS runtime, used only for a diagnostic when the session cannot be read.</param>
    public TracksAuthenticationStateProvider(TracksApi api, IJSRuntime js)
    {
        _api = api;
        _js = js;
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
            _session = await _api.SendAsync<ModuleSessionInfo>(HttpMethod.Get, "api/v1/me", cancellationToken: cancellationToken);
        }
        catch (Exception)
        {
            // A signed-out frame answers 401; report "no session" rather than throwing into a render.
            _session = null;
        }

        return _session;
    }

    /// <inheritdoc />
    public override async Task<AuthenticationState> GetAuthenticationStateAsync()
    {
        if (_cached is not null)
        {
            return await _cached;
        }

        await _gate.WaitAsync();
        try
        {
            _cached ??= BuildAsync();
        }
        finally
        {
            _gate.Release();
        }

        return await _cached;
    }

    private async Task<AuthenticationState> BuildAsync()
    {
        var session = await GetSessionAsync();
        if (session is null)
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
