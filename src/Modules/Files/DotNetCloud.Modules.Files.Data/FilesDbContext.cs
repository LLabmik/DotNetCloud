using DotNetCloud.Modules.Files.Data.Configuration;
using DotNetCloud.Modules.Files.Models;
using Microsoft.EntityFrameworkCore;

namespace DotNetCloud.Modules.Files.Data;

/// <summary>
/// Database context for the Files module.
/// Manages all file system entities: nodes, versions, chunks, shares, tags, comments, and quotas.
/// </summary>
/// <remarks>
/// <para>
/// <b>Module DbContext Pattern:</b>
/// Each module owns its own DbContext, separate from the core <c>CoreDbContext</c>.
/// This provides schema isolation, independent migrations, and testability.
/// </para>
/// <para>
/// <b>Multi-Database Support:</b>
/// Works with PostgreSQL, SQL Server, and MariaDB through provider-specific configuration.
/// </para>
/// </remarks>
public class FilesDbContext : DbContext
{
    /// <summary>
    /// Initializes a new instance of the <see cref="FilesDbContext"/> class.
    /// </summary>
    public FilesDbContext(DbContextOptions<FilesDbContext> options)
        : base(options)
    {
    }

    /// <summary>File and folder nodes in the tree.</summary>
    public DbSet<FileNode> FileNodes => Set<FileNode>();

    /// <summary>Historical versions of files.</summary>
    public DbSet<FileVersion> FileVersions => Set<FileVersion>();

    /// <summary>Content-addressable chunks for deduplication.</summary>
    public DbSet<FileChunk> FileChunks => Set<FileChunk>();

    /// <summary>Mapping of chunks to file versions.</summary>
    public DbSet<FileVersionChunk> FileVersionChunks => Set<FileVersionChunk>();

    /// <summary>File and folder shares.</summary>
    public DbSet<Models.FileShare> FileShares => Set<Models.FileShare>();

    /// <summary>Admin-managed shared-folder definitions.</summary>
    public DbSet<AdminSharedFolderDefinition> AdminSharedFolders => Set<AdminSharedFolderDefinition>();

    /// <summary>Granted groups for admin-managed shared folders.</summary>
    public DbSet<AdminSharedFolderGrant> AdminSharedFolderGrants => Set<AdminSharedFolderGrant>();

    /// <summary>Tags applied to files and folders.</summary>
    public DbSet<FileTag> FileTags => Set<FileTag>();

    /// <summary>Comments on files and folders.</summary>
    public DbSet<FileComment> FileComments => Set<FileComment>();

    /// <summary>User storage quotas.</summary>
    public DbSet<FileQuota> FileQuotas => Set<FileQuota>();

    /// <summary>Active chunked upload sessions.</summary>
    public DbSet<ChunkedUploadSession> UploadSessions => Set<ChunkedUploadSession>();

    /// <summary>Per-user monotonic sequence counters for cursor-based sync.</summary>
    public DbSet<UserSyncCounter> UserSyncCounters => Set<UserSyncCounter>();

    /// <summary>Registered sync devices (one per physical client per user).</summary>
    public DbSet<SyncDevice> SyncDevices => Set<SyncDevice>();

    /// <summary>Per-device sync cursor for server-side tracking and recovery.</summary>
    public DbSet<SyncDeviceCursor> SyncDeviceCursors => Set<SyncDeviceCursor>();

    /// <summary>Persisted virtual mounted node descriptors for admin shared folders.</summary>
    public DbSet<MountedNodeEntry> MountedNodeEntries => Set<MountedNodeEntry>();

    /// <inheritdoc />
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.ApplyConfiguration(new FileNodeConfiguration());
        modelBuilder.ApplyConfiguration(new FileVersionConfiguration());
        modelBuilder.ApplyConfiguration(new FileChunkConfiguration());
        modelBuilder.ApplyConfiguration(new FileVersionChunkConfiguration());
        modelBuilder.ApplyConfiguration(new FileShareConfiguration());
        modelBuilder.ApplyConfiguration(new AdminSharedFolderDefinitionConfiguration());
        modelBuilder.ApplyConfiguration(new AdminSharedFolderGrantConfiguration());
        modelBuilder.ApplyConfiguration(new FileTagConfiguration());
        modelBuilder.ApplyConfiguration(new FileCommentConfiguration());
        modelBuilder.ApplyConfiguration(new FileQuotaConfiguration());
        modelBuilder.ApplyConfiguration(new ChunkedUploadSessionConfiguration());
        modelBuilder.ApplyConfiguration(new UserSyncCounterConfiguration());
        modelBuilder.ApplyConfiguration(new SyncDeviceConfiguration());
        modelBuilder.ApplyConfiguration(new SyncDeviceCursorConfiguration());
        modelBuilder.ApplyConfiguration(new MountedNodeEntryConfiguration());
    }
}
