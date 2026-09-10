namespace DotNetCloud.Core.Services;

/// <summary>
/// Reports genuine user activity (click/tap/key/scroll/nav) from the in-process Blazor UI to
/// the presence service, resetting the signed-in user's idle clock so their presence dot stays
/// green while they interact with any page of the web app.
/// </summary>
/// <remarks>
/// Implemented in Core.Server (the web host) and consumed by the global UI activity reporter.
/// This is deliberately <b>not</b> an <c>ICapabilityInterface</c> — it is a core-internal helper
/// (the presence engine is owned by the core process) rather than a module capability.
/// </remarks>
public interface IPresenceActivityReporter
{
    /// <summary>
    /// Reports activity for the current signed-in user (if any). Safe to call frequently;
    /// the presence service only broadcasts when the derived state actually changes.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task ReportActivityAsync(CancellationToken cancellationToken = default);
}
