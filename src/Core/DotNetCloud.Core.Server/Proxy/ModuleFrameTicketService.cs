using System.Globalization;
using DotNetCloud.Core.Modules;
using Microsoft.AspNetCore.DataProtection;

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
    internal static readonly TimeSpan Lifetime = TimeSpan.FromMinutes(15);

    private readonly IDataProtector _protector;
    private readonly TimeProvider _time;

    /// <summary>Initializes a new instance of the <see cref="ModuleFrameTicketService"/> class.</summary>
    /// <param name="protectionProvider">The core DataProtection provider.</param>
    /// <param name="time">Clock (injected so expiry is testable).</param>
    public ModuleFrameTicketService(IDataProtectionProvider protectionProvider, TimeProvider time)
    {
        ArgumentNullException.ThrowIfNull(protectionProvider);
        ArgumentNullException.ThrowIfNull(time);

        _protector = protectionProvider.CreateProtector(Purpose);
        _time = time;
    }

    /// <inheritdoc />
    public string Issue(Guid userId, string moduleId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(moduleId);

        if (userId == Guid.Empty)
        {
            throw new ArgumentException("A frame ticket needs a user.", nameof(userId));
        }

        var expires = _time.GetUtcNow().Add(Lifetime).ToUnixTimeSeconds();
        var payload = string.Create(
            CultureInfo.InvariantCulture,
            $"{userId:N}|{moduleId}|{expires}");

        return _protector.Protect(payload);
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
