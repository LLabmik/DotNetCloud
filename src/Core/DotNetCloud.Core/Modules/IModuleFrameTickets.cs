namespace DotNetCloud.Core.Modules;

/// <summary>
/// Issues the short-lived ticket that lets a sandboxed module UI call back through the core proxy.
/// </summary>
/// <remarks>
/// <para>
/// A module UI runs in an iframe with <c>sandbox</c> and no <c>allow-same-origin</c>, so its origin is
/// <b>opaque</b>. Chromium therefore treats every subresource and every <c>fetch</c> the frame makes
/// as cross-site, and a <c>SameSite=Lax</c> session cookie is simply not attached — the proxy would
/// answer 401 and the module UI could never load.
/// </para>
/// <para>
/// The ticket is the frame's own credential: bound to one user and one module, signed with the core's
/// key ring, and short-lived. The shell puts it in the iframe URL; the module's bridge copies it into
/// the <c>dnc-frame-ticket</c> request header. It is never a session: it authorizes nothing outside
/// <c>/apps/{moduleId}</c> and expires within minutes.
/// </para>
/// </remarks>
public interface IModuleFrameTickets
{
    /// <summary>
    /// Issues a ticket for one user and one module.
    /// </summary>
    /// <param name="userId">The signed-in user the frame acts as.</param>
    /// <param name="moduleId">The module whose UI the frame hosts.</param>
    /// <returns>The opaque ticket value.</returns>
    string Issue(Guid userId, string moduleId);
}
