using DotNetCloud.Core.Authorization;
using DotNetCloud.Core.Capabilities;
using DotNetCloud.Core.Data.Context;
using DotNetCloud.Modules.Files.DTOs;
using DotNetCloud.Modules.Files.Models;
using DotNetCloud.Modules.Files.Options;
using DotNetCloud.Modules.Files.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace DotNetCloud.Modules.Files.Data.Services;

/// <summary>
/// Admin-facing operations for the default-content template: status, browse, validate, seed,
/// preview, convergence and export. The template itself is always treated as read-only.
/// </summary>
public sealed class DefaultUserContentAdminService
{
    /// <summary>Maximum users enumerated for a preview in one request.</summary>
    public const int PreviewUserCap = 2000;

    private readonly FilesDbContext _db;
    private readonly CoreDbContext _coreDb;
    private readonly IDefaultUserContentSettingsProvider _settings;
    private readonly DefaultUserContentStore _store;
    private readonly DefaultUserContentProvisioner _provisioner;
    private readonly DefaultUserContentQueue _queue;
    private readonly IAuditLogger? _auditLogger;
    private readonly ILogger<DefaultUserContentAdminService> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="DefaultUserContentAdminService"/> class.
    /// </summary>
    public DefaultUserContentAdminService(
        FilesDbContext db,
        CoreDbContext coreDb,
        IDefaultUserContentSettingsProvider settings,
        DefaultUserContentStore store,
        DefaultUserContentProvisioner provisioner,
        DefaultUserContentQueue queue,
        ILogger<DefaultUserContentAdminService> logger,
        IAuditLogger? auditLogger = null)
    {
        _db = db;
        _coreDb = coreDb;
        _settings = settings;
        _store = store;
        _provisioner = provisioner;
        _queue = queue;
        _logger = logger;
        _auditLogger = auditLogger;
    }

    /// <summary>Returns the effective state of the template and the feature.</summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    public async Task<DefaultUserContentStatusDto> GetStatusAsync(CancellationToken cancellationToken = default)
    {
        var options = await _settings.GetAsync(cancellationToken).ConfigureAwait(false);
        var validation = await _store.ValidateAsync(options, cancellationToken).ConfigureAwait(false);
        var manifest = validation.Manifest;

        var queue = await _db.UserContentProvisioning
            .AsNoTracking()
            .GroupBy(p => p.Status)
            .Select(g => new { Status = g.Key, Count = g.Count() })
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var pendingUpdates = await _db.UserContentProvisioning
            .AsNoTracking()
            .SumAsync(p => p.PendingUpdates, cancellationToken)
            .ConfigureAwait(false);

        var pendingDeletions = await _db.UserContentProvisioning
            .AsNoTracking()
            .SumAsync(p => p.PendingDeletions, cancellationToken)
            .ConfigureAwait(false);

        var lastApplied = await _db.UserContentProvisioning
            .AsNoTracking()
            .Where(p => p.AppliedAtUtc != null)
            .MaxAsync(p => (DateTime?)p.AppliedAtUtc, cancellationToken)
            .ConfigureAwait(false);

        var converged = queue
            .Where(q => q.Status is UserContentProvisioningStatus.Completed
                or UserContentProvisioningStatus.Partial
                or UserContentProvisioningStatus.Skipped)
            .Sum(q => q.Count);

        var failed = queue
            .Where(q => q.Status == UserContentProvisioningStatus.Failed)
            .Sum(q => q.Count);

        return new DefaultUserContentStatusDto
        {
            Enabled = options.Enabled,
            RootPath = manifest.RootPath,
            RootExists = validation.RootExists,
            Readable = validation.Readable,
            FolderCount = manifest.FolderCount,
            FileCount = manifest.FileCount,
            TotalBytes = manifest.TotalBytes,
            PendingUpdates = pendingUpdates,
            PendingDeletions = pendingDeletions,
            UsersConverged = converged,
            UsersFailed = failed,
            LastAppliedAtUtc = lastApplied,
            Issues = validation.Issues.Select(i => new DefaultContentIssueDto(
                i.RelativePath, i.Code, i.Message, i.Severity.ToString())).ToList()
        };
    }

    /// <summary>Browses the template directory (read-only).</summary>
    /// <param name="relativePath">Relative directory path (empty for the root).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public async Task<DefaultContentBrowseResultDto> BrowseAsync(
        string? relativePath,
        CancellationToken cancellationToken = default)
    {
        var options = await _settings.GetAsync(cancellationToken).ConfigureAwait(false);
        var root = DefaultUserContentStore.ResolveRootPath(options);
        var normalized = (relativePath ?? string.Empty).Replace('\\', '/').Trim('/');

        var target = string.IsNullOrEmpty(normalized)
            ? root
            : Path.GetFullPath(Path.Combine(root, normalized));

        var rootWithSeparator = root.TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
        if (!target.StartsWith(rootWithSeparator, StringComparison.Ordinal)
            && !string.Equals(target.TrimEnd(Path.DirectorySeparatorChar), root.TrimEnd(Path.DirectorySeparatorChar), StringComparison.Ordinal))
        {
            throw new Core.Errors.ValidationException("path", "Path escapes the template root.");
        }

        var validation = await _store.ValidateAsync(options, cancellationToken).ConfigureAwait(false);
        var issuesByPath = validation.Issues
            .GroupBy(i => i.RelativePath, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.ToList(), StringComparer.OrdinalIgnoreCase);

        var entries = new List<DefaultContentBrowseEntryDto>();

        if (Directory.Exists(target))
        {
            foreach (var entry in Directory.EnumerateFileSystemEntries(target)
                         .OrderBy(p => p, StringComparer.OrdinalIgnoreCase))
            {
                cancellationToken.ThrowIfCancellationRequested();

                var info = new FileSystemInfo[] { new FileInfo(entry), new DirectoryInfo(entry) }
                    .FirstOrDefault(i => i.Exists);
                if (info is null)
                    continue;

                var fullRelative = string.IsNullOrEmpty(normalized)
                    ? info.Name
                    : $"{normalized}/{info.Name}";

                var state = issuesByPath.TryGetValue(fullRelative, out var issues)
                    ? issues.Max(i => i.Severity).ToString()
                    : "Ok";

                entries.Add(new DefaultContentBrowseEntryDto(
                    info.Name,
                    fullRelative,
                    info is DirectoryInfo ? "Folder" : "File",
                    info is FileInfo file ? file.Length : 0,
                    state));
            }
        }

        return new DefaultContentBrowseResultDto(normalized, entries);
    }

    /// <summary>Adds the stock folder layout (additive only).</summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    public async Task<int> SeedDefaultsAsync(CancellationToken cancellationToken = default)
    {
        var options = await _settings.GetAsync(cancellationToken).ConfigureAwait(false);
        var created = await _store.SeedDefaultFoldersAsync(options, cancellationToken).ConfigureAwait(false);

        _logger.LogInformation("Default-content seed added {Count} stock folder(s).", created);
        return created;
    }

    /// <summary>
    /// Previews what a convergence run would do, either for one user or for every user.
    /// </summary>
    /// <param name="userId">Optional single user.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public async Task<DefaultContentReconcileResultDto> PreviewAsync(
        Guid? userId,
        CancellationToken cancellationToken = default)
    {
        var userIds = await GetUserIdsAsync(userId, PreviewUserCap, cancellationToken).ConfigureAwait(false);

        int adds = 0, updates = 0, overwrites = 0, deletes = 0;
        var sample = new List<string>();

        foreach (var id in userIds)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var preview = await _provisioner.PreviewAsync(id, cancellationToken).ConfigureAwait(false);
            adds += preview.Adds;
            updates += preview.Updates;
            overwrites += preview.Overwrites;
            deletes += preview.Deletes;

            foreach (var path in preview.SamplePaths)
            {
                if (sample.Count >= 20)
                    break;

                sample.Add(path);
            }
        }

        return new DefaultContentReconcileResultDto
        {
            DryRun = true,
            Adds = adds,
            Updates = updates,
            Overwrites = overwrites,
            Deletes = deletes,
            SamplePaths = sample
        };
    }

    /// <summary>
    /// Runs a convergence pass. Additive-only runs are queued; runs that update or delete existing
    /// copies execute synchronously so the caller gets an exact result.
    /// </summary>
    /// <param name="publishUpdates">Replace unmodified outdated copies.</param>
    /// <param name="overwriteModified">Replace user-modified copies (requires confirmation).</param>
    /// <param name="confirmDeletions">Allow permanent deletion of copies of removed template items.</param>
    /// <param name="confirmOverwrites">Explicit acknowledgement of overwriting user changes.</param>
    /// <param name="dryRun">Preview only.</param>
    /// <param name="caller">Administrator caller (for auditing).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public async Task<DefaultContentReconcileResultDto> ReconcileAsync(
        bool publishUpdates,
        bool overwriteModified,
        bool confirmDeletions,
        bool confirmOverwrites,
        bool dryRun,
        CallerContext caller,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(caller);

        var preview = await PreviewAsync(null, cancellationToken).ConfigureAwait(false);

        if (dryRun)
            return preview;

        if (overwriteModified && !confirmOverwrites)
        {
            return preview with
            {
                RequiresConfirmation = true,
                Reason = "overwrite-modified"
            };
        }

        if (preview.Deletes > 0 && !confirmDeletions)
        {
            return preview with
            {
                RequiresConfirmation = true,
                Reason = "confirm-deletions"
            };
        }

        var mode = ReconcileMode.AddOnly;
        if (publishUpdates || overwriteModified || confirmDeletions)
            mode = ReconcileMode.PublishUpdates;

        if (confirmDeletions)
            mode |= ReconcileMode.AllowDeletions;

        if (overwriteModified)
            mode |= ReconcileMode.OverwriteModified;

        var userIds = await GetUserIdsAsync(null, int.MaxValue, cancellationToken).ConfigureAwait(false);
        var (queued, _) = await _queue.EnqueueAsync(userIds, resetAttempts: true, cancellationToken)
            .ConfigureAwait(false);

        // Additive work is handled by the worker. Destructive/update work is executed here so the
        // administrator gets a deterministic result and the confirmation gate cannot be bypassed.
        var destructiveMode = mode & ~ReconcileMode.AddOnly;
        var processed = 0;
        var created = 0;
        var skipped = 0;
        var updates = 0;
        var deletes = 0;

        if (destructiveMode != ReconcileMode.AddOnly)
        {
            foreach (var id in userIds)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var result = await _provisioner.ReconcileAsync(id, destructiveMode, cancellationToken)
                    .ConfigureAwait(false);

                processed++;
                created += result.ItemsCreated;
                skipped += result.ItemsSkipped;
                updates += result.PendingUpdates;
                deletes += result.PendingDeletions;
            }

            if (_auditLogger is not null)
            {
                await _auditLogger.LogAsync(new AuditEntry
                {
                    Caller = caller,
                    ModuleId = DefaultUserContentOptions.AdminSettings.ModuleId,
                    Action = AuditAction.Update,
                    EntityType = "DefaultUserContent",
                    EntityId = Guid.Empty,
                    Description = $"Confirmed default-content convergence: publishUpdates={publishUpdates}, " +
                                  $"overwriteModified={overwriteModified}, confirmDeletions={confirmDeletions}, " +
                                  $"users={processed}, created={created}, skipped={skipped}."
                }, cancellationToken).ConfigureAwait(false);
            }
        }

        return new DefaultContentReconcileResultDto
        {
            DryRun = false,
            Adds = preview.Adds,
            Updates = updates,
            Overwrites = overwriteModified ? preview.Overwrites : 0,
            Deletes = deletes,
            UsersQueued = queued,
            UsersProcessed = processed,
            SamplePaths = preview.SamplePaths
        };
    }

    /// <summary>Returns queue counts by status.</summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    public async Task<DefaultContentQueueStatusDto> GetQueueStatusAsync(CancellationToken cancellationToken = default)
    {
        var counts = await _db.UserContentProvisioning
            .AsNoTracking()
            .GroupBy(p => p.Status)
            .Select(g => new { Status = g.Key, Count = g.Count() })
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        int Count(UserContentProvisioningStatus status) =>
            counts.Where(c => c.Status == status).Sum(c => c.Count);

        return new DefaultContentQueueStatusDto(
            Count(UserContentProvisioningStatus.Pending),
            Count(UserContentProvisioningStatus.InProgress),
            Count(UserContentProvisioningStatus.Completed),
            Count(UserContentProvisioningStatus.Partial),
            Count(UserContentProvisioningStatus.Failed),
            Count(UserContentProvisioningStatus.Skipped));
    }

    /// <summary>Returns users whose pass failed after the retry cap.</summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    public async Task<IReadOnlyList<DefaultContentFailedUserDto>> GetFailedUsersAsync(
        CancellationToken cancellationToken = default)
    {
        return await _db.UserContentProvisioning
            .AsNoTracking()
            .Where(p => p.Status == UserContentProvisioningStatus.Failed)
            .OrderByDescending(p => p.UpdatedAtUtc)
            .Take(100)
            .Select(p => new DefaultContentFailedUserDto(p.UserId, p.Attempts, p.LastError, p.UpdatedAtUtc))
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    /// <summary>Writes the template as a ZIP archive.</summary>
    /// <param name="destination">Destination stream.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public async Task ExportAsync(Stream destination, CancellationToken cancellationToken = default)
    {
        var options = await _settings.GetAsync(cancellationToken).ConfigureAwait(false);
        await _store.ExportAsync(options, destination, cancellationToken).ConfigureAwait(false);
    }

    private async Task<IReadOnlyList<Guid>> GetUserIdsAsync(
        Guid? userId,
        int cap,
        CancellationToken cancellationToken)
    {
        if (userId is Guid single)
            return [single];

        return await _coreDb.Users
            .AsNoTracking()
            .OrderBy(u => u.Id)
            .Select(u => u.Id)
            .Take(cap)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
    }
}
