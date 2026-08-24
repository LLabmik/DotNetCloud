using Microsoft.EntityFrameworkCore;

namespace DotNetCloud.Core.Data.Extensions;

/// <summary>
/// Cheap, exception-safe database reachability probe for health checks.
/// Reuses the resilience-configured <see cref="DbContext"/> and never throws —
/// callers treat a <see langword="false"/> result as "database unavailable".
/// </summary>
public static class DbHealthProbe
{
    private static readonly TimeSpan ProbeTimeout = TimeSpan.FromSeconds(5);

    /// <summary>
    /// Returns <see langword="true"/> when the database backing <paramref name="db"/>
    /// can be connected to within the probe timeout; otherwise <see langword="false"/>.
    /// </summary>
    /// <param name="db">The configured DbContext whose database is probed.</param>
    /// <param name="cancellationToken">Optional cancellation token.</param>
    /// <returns><see langword="true"/> when reachable, otherwise <see langword="false"/>.</returns>
    public static async Task<bool> CanConnectAsync(
        DbContext db,
        CancellationToken cancellationToken = default)
    {
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        cts.CancelAfter(ProbeTimeout);

        try
        {
            return await db.Database.CanConnectAsync(cts.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return false;
        }
        catch (Exception)
        {
            return false;
        }
    }
}
