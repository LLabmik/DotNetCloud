using DotNetCloud.Modules.Files.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace DotNetCloud.Modules.Files.Data;

/// <summary>
/// Initializes the files database with default root folders, quotas, and tags for users.
/// </summary>
public static class FilesDbInitializer
{
    /// <summary>
    /// Default storage quota per user in bytes (10 GB).
    /// Can be overridden via the <c>defaultQuotaBytes</c> parameter on initialization methods.
    /// </summary>
    public const long DefaultQuotaBytes = 10L * 1024 * 1024 * 1024;

    /// <summary>
    /// Creates a default root folder for the specified user if one does not already exist.
    /// </summary>
    /// <param name="db">The <see cref="FilesDbContext"/> instance.</param>
    /// <param name="userId">The user to create a root folder for.</param>
    /// <param name="logger">Optional logger for diagnostic output.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The root <see cref="FileNode"/> (existing or newly created).</returns>
    public static async Task<FileNode> EnsureRootFolderAsync(
        FilesDbContext db,
        Guid userId,
        ILogger? logger = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(db);

        var existing = await db.FileNodes
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(
                n => n.OwnerId == userId && n.ParentId == null && n.NodeType == FileNodeType.Folder,
                cancellationToken);

        if (existing is not null)
        {
            logger?.LogDebug("Root folder already exists for user {UserId}", userId);
            return existing;
        }

        var rootId = Guid.NewGuid();
        var root = new FileNode
        {
            Id = rootId,
            Name = "Root",
            NodeType = FileNodeType.Folder,
            OwnerId = userId,
            MaterializedPath = $"/{rootId}",
            Depth = 0
        };

        db.FileNodes.Add(root);
        await db.SaveChangesAsync(cancellationToken);

        logger?.LogInformation("Created root folder for user {UserId}", userId);
        return root;
    }

    /// <summary>
    /// Ensures a storage quota record exists for the specified user.
    /// </summary>
    /// <param name="db">The <see cref="FilesDbContext"/> instance.</param>
    /// <param name="userId">The user to create a quota for.</param>
    /// <param name="defaultQuotaBytes">
    /// Maximum storage in bytes. Defaults to <see cref="DefaultQuotaBytes"/> (10 GB).
    /// Pass 0 for unlimited.
    /// </param>
    /// <param name="logger">Optional logger for diagnostic output.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The <see cref="FileQuota"/> (existing or newly created).</returns>
    public static async Task<FileQuota> EnsureQuotaAsync(
        FilesDbContext db,
        Guid userId,
        long defaultQuotaBytes = DefaultQuotaBytes,
        ILogger? logger = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(db);

        var existing = await db.FileQuotas
            .FirstOrDefaultAsync(q => q.UserId == userId, cancellationToken);

        if (existing is not null)
        {
            logger?.LogDebug("Quota already exists for user {UserId}", userId);
            return existing;
        }

        var quota = new FileQuota
        {
            UserId = userId,
            MaxBytes = defaultQuotaBytes
        };

        db.FileQuotas.Add(quota);
        await db.SaveChangesAsync(cancellationToken);

        logger?.LogInformation(
            "Created default quota for user {UserId}: {QuotaGB} GB",
            userId,
            defaultQuotaBytes / (1024.0 * 1024 * 1024));

        return quota;
    }

    /// <summary>
    /// Seeds default tags for a user's file if they don't already have any tags.
    /// Creates "Important", "Work", and "Personal" tags.
    /// </summary>
    /// <param name="db">The <see cref="FilesDbContext"/> instance.</param>
    /// <param name="userId">The user to create default tags for.</param>
    /// <param name="rootFileNodeId">The root folder ID to attach default tags to.</param>
    /// <param name="logger">Optional logger for diagnostic output.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public static async Task SeedDefaultTagsAsync(
        FilesDbContext db,
        Guid userId,
        Guid rootFileNodeId,
        ILogger? logger = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(db);

        var hasTags = await db.FileTags
            .AnyAsync(t => t.CreatedByUserId == userId, cancellationToken);

        if (hasTags)
        {
            logger?.LogDebug("Tags already exist for user {UserId}; skipping seed", userId);
            return;
        }

        var defaultTags = new[]
        {
            new FileTag
            {
                FileNodeId = rootFileNodeId,
                Name = "Important",
                Color = "#E53E3E",
                CreatedByUserId = userId
            },
            new FileTag
            {
                FileNodeId = rootFileNodeId,
                Name = "Work",
                Color = "#3182CE",
                CreatedByUserId = userId
            },
            new FileTag
            {
                FileNodeId = rootFileNodeId,
                Name = "Personal",
                Color = "#38A169",
                CreatedByUserId = userId
            }
        };

        db.FileTags.AddRange(defaultTags);
        await db.SaveChangesAsync(cancellationToken);

        logger?.LogInformation(
            "Seeded {Count} default tags for user {UserId}: {Names}",
            defaultTags.Length,
            userId,
            string.Join(", ", "Important", "Work", "Personal"));
    }

    /// <summary>
    /// Performs full initialization for a user: root folder, quota, and default tags.
    /// All operations are idempotent.
    /// </summary>
    /// <param name="db">The <see cref="FilesDbContext"/> instance.</param>
    /// <param name="userId">The user to initialize.</param>
    /// <param name="defaultQuotaBytes">
    /// Maximum storage in bytes. Defaults to <see cref="DefaultQuotaBytes"/> (10 GB).
    /// </param>
    /// <param name="logger">Optional logger for diagnostic output.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public static async Task InitializeUserAsync(
        FilesDbContext db,
        Guid userId,
        long defaultQuotaBytes = DefaultQuotaBytes,
        ILogger? logger = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(db);

        logger?.LogInformation("Initializing files module data for user {UserId}...", userId);

        var root = await EnsureRootFolderAsync(db, userId, logger, cancellationToken);
        await EnsureQuotaAsync(db, userId, defaultQuotaBytes, logger, cancellationToken);
        await SeedDefaultTagsAsync(db, userId, root.Id, logger, cancellationToken);

        logger?.LogInformation("Files module initialization complete for user {UserId}", userId);
    }
}
