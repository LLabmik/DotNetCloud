using System.Security.Claims;
using DotNetCloud.Core.Modules;
using Microsoft.AspNetCore.Components.Authorization;

namespace DotNetCloud.UI.Web.Components.Modules;

/// <summary>
/// Issues the frame ticket a sandboxed module UI needs and appends it to the proxy URL.
/// </summary>
/// <remarks>
/// A frame with an opaque origin never sends the core session cookie, so the ticket in the iframe URL
/// is how the proxy learns who the frame acts as. It is bound to the user and the module and expires
/// within minutes; the SDK bridge copies it from the URL into the module's own API calls.
/// </remarks>
internal static class ModuleFrameSource
{
    /// <summary>Query parameter the proxy reads the ticket from.</summary>
    internal const string TicketQuery = "dncTicket";

    /// <summary>
    /// Issues a ticket for the signed-in user, or returns <see langword="null"/> when there is no
    /// usable identity (the frame will then be loaded unauthenticated and the module will 401).
    /// </summary>
    /// <param name="authenticationState">The component's authentication state provider.</param>
    /// <param name="tickets">The ticket issuer.</param>
    /// <param name="moduleId">The module whose UI is framed.</param>
    /// <returns>The ticket value, or <see langword="null"/>.</returns>
    internal static async Task<string?> IssueAsync(
        AuthenticationStateProvider authenticationState,
        IModuleFrameTickets tickets,
        string moduleId)
    {
        ArgumentNullException.ThrowIfNull(authenticationState);
        ArgumentNullException.ThrowIfNull(tickets);
        ArgumentException.ThrowIfNullOrWhiteSpace(moduleId);

        var state = await authenticationState.GetAuthenticationStateAsync();
        var value = state.User.FindFirst(ClaimTypes.NameIdentifier)?.Value
            ?? state.User.FindFirst("sub")?.Value;

        return Guid.TryParse(value, out var userId) && userId != Guid.Empty
            ? tickets.Issue(userId, moduleId)
            : null;
    }

    /// <summary>
    /// Appends the ticket to a site-relative proxy URL.
    /// </summary>
    /// <param name="source">The proxy URL.</param>
    /// <param name="ticket">The ticket, or <see langword="null"/>.</param>
    /// <returns>The URL the iframe should load.</returns>
    internal static string WithTicket(string source, string? ticket)
    {
        if (string.IsNullOrEmpty(source) || string.IsNullOrEmpty(ticket))
        {
            return source;
        }

        var separator = source.Contains('?', StringComparison.Ordinal) ? '&' : '?';
        return $"{source}{separator}{TicketQuery}={Uri.EscapeDataString(ticket)}";
    }
}
