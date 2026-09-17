using DotNetCloud.Modules.Files.Models;
using Microsoft.EntityFrameworkCore;

namespace DotNetCloud.Modules.Files.Data;

/// <summary>
/// Safe access to the user's <see cref="FileQuota"/> row.
/// <para>
/// The quota row is written by more than one process: the Files module host handles uploads,
/// trash purges and permanent deletes, while the Core.Server process renders the Blazor UI with
/// its own scoped <c>FilesDbContext</c> — and that context lives as long as the circuit. EF Core
/// returns the instance it already tracks for a query instead of the stored values, so a copy
/// read once at page load would be handed out for the rest of the session. Every read therefore
/// has to either bypass tracking (<see cref="GetCurrentAsync"/>) or drop the stale copy before a
/// read-modify-write (<see cref="GetForUpdateAsync"/>).
/// </para>
/// </summary>
internal static class QuotaRowHelper
{
    /// <summary>
    /// Reads the user's quota row with the values currently stored in the database.
    /// Suitable for display; never returns a stale tracked copy.
    /// </summary>
    public static Task<FileQuota?> GetCurrentAsync(FilesDbContext db, Guid userId, CancellationToken cancellationToken = default)
        => db.FileQuotas
            .AsNoTracking()
            .FirstOrDefaultAsync(q => q.UserId == userId, cancellationToken);

    /// <summary>
    /// Loads the user's quota row for a delta update, discarding any copy this context already
    /// tracks first. Without the detach, <c>UsedBytes += delta</c> would be applied to the
    /// snapshot taken when the row was first read and the write would overwrite whatever another
    /// process stored in the meantime.
    /// </summary>
    public static async Task<FileQuota?> GetForUpdateAsync(FilesDbContext db, Guid userId, CancellationToken cancellationToken = default)
    {
        foreach (var entry in db.ChangeTracker.Entries<FileQuota>().Where(e => e.Entity.UserId == userId).ToList())
        {
            entry.State = EntityState.Detached;
        }

        return await db.FileQuotas.FirstOrDefaultAsync(q => q.UserId == userId, cancellationToken);
    }
}
