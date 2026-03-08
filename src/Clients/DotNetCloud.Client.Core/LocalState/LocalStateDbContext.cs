using Microsoft.EntityFrameworkCore;

namespace DotNetCloud.Client.Core.LocalState;

/// <summary>
/// EF Core DbContext for the per-context SQLite state database.
/// </summary>
public sealed class LocalStateDbContext : DbContext
{
    /// <summary>Tracked local files.</summary>
    public DbSet<LocalFileRecord> FileRecords => Set<LocalFileRecord>();

    /// <summary>Pending sync operations.</summary>
    public DbSet<PendingOperationDbRow> PendingOperations => Set<PendingOperationDbRow>();

    /// <summary>Operations that permanently failed after all retries.</summary>
    public DbSet<FailedOperationDbRow> FailedOperations => Set<FailedOperationDbRow>();

    /// <summary>Sync checkpoint (single row).</summary>
    public DbSet<SyncCheckpointRow> Checkpoints => Set<SyncCheckpointRow>();

    /// <summary>Initializes a new <see cref="LocalStateDbContext"/>.</summary>
    public LocalStateDbContext(DbContextOptions<LocalStateDbContext> options) : base(options) { }

    /// <inheritdoc/>
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<LocalFileRecord>(e =>
        {
            e.HasKey(r => r.Id);
            e.Property(r => r.LocalPath).IsRequired();
            e.HasIndex(r => r.LocalPath).IsUnique();
            e.HasIndex(r => r.NodeId);
        });

        modelBuilder.Entity<PendingOperationDbRow>(e =>
        {
            e.HasKey(r => r.Id);
            e.Property(r => r.OperationType).IsRequired();
        });

        modelBuilder.Entity<FailedOperationDbRow>(e =>
        {
            e.HasKey(r => r.Id);
            e.Property(r => r.OperationType).IsRequired();
        });

        modelBuilder.Entity<SyncCheckpointRow>(e =>
        {
            e.HasKey(r => r.Id);
        });
    }
}

/// <summary>
/// Flat DB row for a pending operation (polymorphism via discriminator column).
/// </summary>
public sealed class PendingOperationDbRow
{
    /// <summary>Row ID.</summary>
    public int Id { get; set; }

    /// <summary>"Upload" or "Download".</summary>
    public required string OperationType { get; set; }

    /// <summary>Local file path (for uploads).</summary>
    public string? LocalPath { get; set; }

    /// <summary>Server node ID (for downloads and uploads with existing node).</summary>
    public Guid? NodeId { get; set; }

    /// <summary>UTC time queued.</summary>
    public DateTime QueuedAt { get; set; } = DateTime.UtcNow;

    /// <summary>Retry count.</summary>
    public int RetryCount { get; set; }

    /// <summary>UTC time after which this operation is eligible for retry. Null means immediately eligible.</summary>
    public DateTime? NextRetryAt { get; set; }

    /// <summary>Error message from the most recent failure.</summary>
    public string? LastError { get; set; }
}

/// <summary>
/// Row for a sync operation that permanently failed after exhausting all retries.
/// </summary>
public sealed class FailedOperationDbRow
{
    /// <summary>Row ID.</summary>
    public int Id { get; set; }

    /// <summary>"Upload" or "Download".</summary>
    public required string OperationType { get; set; }

    /// <summary>Local file path.</summary>
    public string? LocalPath { get; set; }

    /// <summary>Server node ID.</summary>
    public Guid? NodeId { get; set; }

    /// <summary>UTC time the operation was originally queued.</summary>
    public DateTime QueuedAt { get; set; }

    /// <summary>Total retry count at the time of failure.</summary>
    public int RetryCount { get; set; }

    /// <summary>Error message from the final failure.</summary>
    public string? LastError { get; set; }

    /// <summary>UTC time the operation was permanently failed.</summary>
    public DateTime FailedAt { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// Single-row checkpoint table storing the last sync timestamp.
/// </summary>
public sealed class SyncCheckpointRow
{
    /// <summary>Always 1.</summary>
    public int Id { get; set; } = 1;

    /// <summary>UTC timestamp of the last successful sync.</summary>
    public DateTime? LastSyncedAt { get; set; }
}
