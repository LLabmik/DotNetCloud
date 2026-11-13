using System.Globalization;
using DotNetCloud.Core.Modules;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Configuration;

namespace DotNetCloud.Core.Server.Proxy;

/// <summary>
/// Mints and verifies the ticket a sandboxed module UI uses to reach the core proxy
/// (see <see cref="IModuleFrameTickets"/> for why a session cookie cannot work there).
/// </summary>
/// <remarks>
/// Payload is <c>{userId}|{moduleId}|{expiresUnixSeconds}</c>, protected with the core key ring. The
/// module id is part of the protected payload <b>and</b> is re-checked against the route, so a ticket
/// minted for one module cannot be replayed against another module's proxy path.
/// </remarks>
internal sealed class ModuleFrameTicketService : IModuleFrameTickets
{
    /// <summary>DataProtection purpose for frame tickets.</summary>
    internal const string Purpose = "DotNetCloud.ModuleUi.FrameTicket";

    /// <summary>How long a freshly issued ticket is valid.</summary>
    /// <remarks>
    /// Fifteen minutes was too short for a real session: a viewer left open past it had every API
    /// call refused (401) with no way to re-mint the ticket except reloading the shell, which threw
    /// away whatever the frame was in the middle of. A day-length session is the unit users think in;
    /// <c>Modules:Ui:FrameTicketMinutes</c> overrides it.
    /// </remarks>
    internal static readonly TimeSpan Lifetime = TimeSpan.FromHours(8);

    /// <summary>
    /// How close to expiry a cached ticket may get before a new one is minted.
    /// </summary>
    /// <remarks>
    /// A ticket is a credential, but it is also embedded in an iframe's <c>src</c>. Minting a fresh
    /// string per call made that <c>src</c> change on every render - and a re-created component
    /// re-mints even when its own state is cached - so the browser restarted the frame navigation and
    /// aborted the in-flight load (<c>ERR_ABORTED</c>) before a cold WebAssembly bundle could boot.
    /// Re-using a ticket while it has time left keeps the frame URL stable.
    /// </remarks>
    private static readonly TimeSpan ReuseWindow = TimeSpan.FromMinutes(5);

    private readonly IDataProtector _protector;
    private readonly TimeProvider _time;
    private readonly TimeSpan _lifetime;
    private readonly object _cacheGate = new();
    private readonly Dictionary<string, (string Ticket, DateTimeOffset ExpiresAt)> _issued = new(StringComparer.Ordinal);

    /// <summary>Initializes a new instance of the <see cref="ModuleFrameTicketService"/> class.</summary>
    /// <param name="protectionProvider">The core DataProtection provider.</param>
    /// <param name="time">Clock (injected so expiry is testable).</param>
    /// <param name="configuration">Optional configuration; reads <c>Modules:Ui:FrameTicketMinutes</c>.</param>
    public ModuleFrameTicketService(
        IDataProtectionProvider protectionProvider,
        TimeProvider time,
        IConfiguration? configuration = null)
    {
        ArgumentNullException.ThrowIfNull(protectionProvider);
        ArgumentNullException.ThrowIfNull(time);

        _protector = protectionProvider.CreateProtector(Purpose);
        _time = time;
        _lifetime = ReadLifetime(configuration);
    }

    private static TimeSpan ReadLifetime(IConfiguration? configuration)
    {
        if (configuration?["Modules:Ui:FrameTicketMinutes"] is { Length: > 0 } raw
            && double.TryParse(raw, NumberStyles.Float, CultureInfo.InvariantCulture, out var minutes)
            && minutes > 0)
        {
            return TimeSpan.FromMinutes(minutes);
        }

        return Lifetime;
    }

    /// <inheritdoc />
    public string Issue(Guid userId, string moduleId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(moduleId);

        if (userId == Guid.Empty)
        {
            throw new ArgumentException("A frame ticket needs a user.", nameof(userId));
        }

        var now = _time.GetUtcNow();
        var key = $"{userId:N}|{moduleId}";

        lock (_cacheGate)
        {
            if (_issued.TryGetValue(key, out var cached) && cached.ExpiresAt - now > ReuseWindow)
            {
                return cached.Ticket;
            }
        }

        var expiresAt = now.Add(_lifetime);
        var ticket = _protector.Protect(string.Create(
            CultureInfo.InvariantCulture,
            $"{userId:N}|{moduleId}|{expiresAt.ToUnixTimeSeconds()}"));

        lock (_cacheGate)
        {
            // Bounded: drop anything that can no longer be handed out before adding.
            if (_issued.Count > 512)
            {
                foreach (var stale in _issued.Where(entry => entry.Value.ExpiresAt - now <= ReuseWindow).ToList())
                {
                    _issued.Remove(stale.Key);
                }
            }

            _issued[key] = (ticket, expiresAt);
        }

        return ticket;
    }

    /// <summary>
    /// Verifies a ticket for a module.
    /// </summary>
    /// <param name="ticket">The ticket to verify.</param>
    /// <param name="moduleId">The module the request is routed to.</param>
    /// <param name="userId">The user the ticket stands for, when valid.</param>
    /// <returns><see langword="true"/> when the ticket is genuine, unexpired and for this module.</returns>
    internal bool TryValidate(string? ticket, string moduleId, out Guid userId)
    {
        userId = Guid.Empty;

        if (string.IsNullOrWhiteSpace(ticket) || string.IsNullOrWhiteSpace(moduleId))
        {
            return false;
        }

        string payload;
        try
        {
            payload = _protector.Unprotect(ticket);
        }
        catch (Exception ex) when (ex is System.Security.Cryptography.CryptographicException or FormatException)
        {
            return false;
        }

        var parts = payload.Split('|');
        if (parts.Length != 3
            || !Guid.TryParseExact(parts[0], "N", out var parsedUser)
            || !string.Equals(parts[1], moduleId, StringComparison.OrdinalIgnoreCase)
            || !long.TryParse(parts[2], NumberStyles.Integer, CultureInfo.InvariantCulture, out var expires))
        {
            return false;
        }

        if (_time.GetUtcNow().ToUnixTimeSeconds() >= expires)
        {
            return false;
        }

        userId = parsedUser;
        return true;
    }
}
