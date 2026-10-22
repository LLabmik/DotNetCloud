using DotNetCloud.Core.Authorization;
using DotNetCloud.Modules.Files.Models;
using DotNetCloud.Modules.Files.Options;
using DotNetCloud.Modules.Files.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace DotNetCloud.Modules.Files.Data.Services;

/// <summary>Result of one reconciliation pass for one user.</summary>
/// <param name="Status">Final status.</param>
/// <param name="ItemsCreated">Folders and files added.</param>
/// <param name="ItemsSkipped">Entries skipped.</param>
/// <param name="PendingUpdates">Outdated items awaiting a publish run.</param>
/// <param name="PendingDeletions">Removed template items awaiting a confirmed run.</param>
/// <param name="BytesCopied">Bytes of newly provisioned files.</param>
/// <param name="Error">Failure reason, if any.</param>
public sealed record ProvisioningPassResult(
    UserContentProvisioningStatus Status,
    int ItemsCreated,
    int ItemsSkipped,
    int PendingUpdates,
    int PendingDeletions,
    long BytesCopied,
    string? Error = null);

/// <summary>What a reconciliation pass would do (no writes performed).</summary>
/// <param name="Adds">Items that would be created.</param>
/// <param name="Updates">Unmodified copies that would be updated.</param>
/// <param name="Overwrites">User-modified copies that would be replaced.</param>
/// <param name="Deletes">Copies that would be deleted.</param>
/// <param name="SamplePaths">Up to 20 example actions.</param>
public sealed record ProvisioningPreview(
    int Adds,
    int Updates,
    int Overwrites,
    int Deletes,
    IReadOnlyList<string> SamplePaths);

/// <summary>
/// Reconciles one user's tree against the default-content template.
/// </summary>
/// <remarks>
/// The pass is idempotent and add-only unless the caller passes an explicit destructive
/// <see cref="ReconcileMode"/>. Default content has <b>no version history</b>: an update replaces
/// the file's single current version in place.
/// </remarks>
public sealed class DefaultUserContentProvisioner
{
    private readonly FilesDbContext _db;
    private readonly IDefaultUserContentSettingsProvider _settings;
    private readonly DefaultUserContentStore _store;
    private readonly FileImportService _import;
    private readonly IQuotaService _quotaService;
    private readonly ITrashService? _trashService;
    private readonly ILogger<DefaultUserContentProvisioner> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="DefaultUserContentProvisioner"/> class.
    /// </summary>
    public DefaultUserContentProvisioner(
        FilesDbContext db,
        IDefaultUserContentSettingsProvider settings,
        DefaultUserContentStore store,
        FileImportService import,
        IQuotaService quotaService,
        ILogger<DefaultUserContentProvisioner> logger,
        ITrashService? trashService = null)
    {
        _db = db;
        _settings = settings;
        _store = store;
        _import = import;
        _quotaService = quotaService;
        _logger = logger;
        _trashService = trashService;
    }

    /// <summary>
    /// Runs one reconciliation pass.
    /// </summary>
    /// <param name="userId">User to reconcile.</param>
    /// <param name="mode">Mutation permissions for this pass.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public async Task<ProvisioningPassResult> ReconcileAsync(
        Guid userId,
        ReconcileMode mode,
        CancellationToken cancellationToken = default)
    {
        var options = await _settings.GetAsync(cancellationToken).ConfigureAwait(false);

        if (!options.Enabled)
        {
            _logger.LogDebug("Default content is disabled; skipping convergence for {UserId}.", userId);
            return await SaveStatusAsync(userId, new ProvisioningPassResult(
                UserContentProvisioningStatus.Skipped, 0, 0, 0, 0, 0), cancellationToken)
                .ConfigureAwait(false);
        }

        var validation = await _store.ValidateAsync(options, cancellationToken).ConfigureAwait(false);
        var manifest = validation.Manifest;

        if (!validation.RootExists)
        {
            var message = $"Default-content template root '{validation.RootPath}' does not exist.";
            _logger.LogWarning("{Message}", message);
            return await SaveStatusAsync(userId, new ProvisioningPassResult(
                UserContentProvisioningStatus.Failed, 0, 0, 0, 0, 0, message), cancellationToken)
                .ConfigureAwait(false);
        }

        if (!validation.Readable)
        {
            var message = $"Default-content template root '{validation.RootPath}' is not readable by the service user.";
            _logger.LogWarning("{Message}", message);
            return await SaveStatusAsync(userId, new ProvisioningPassResult(
                UserContentProvisioningStatus.Failed, 0, 0, 0, 0, 0, message), cancellationToken)
                .ConfigureAwait(false);
        }

        if (manifest.IsEmpty)
        {
            // An empty template is a no-op only when this user has nothing tracked. When tracking
            // rows exist, the removal pass below must still classify the removed template items
            // (as pending deletions, or as deleted by a confirmed run).
            var hasTracking = await _db.UserDefaultContentItems
                .AnyAsync(i => i.UserId == userId, cancellationToken)
                .ConfigureAwait(false);

            if (!hasTracking)
            {
                _logger.LogInformation(
                    "Default-content template at {Root} is empty; user {UserId} is up to date.",
                    manifest.RootPath, userId);
                return await SaveStatusAsync(userId, new ProvisioningPassResult(
                    UserContentProvisioningStatus.Skipped, 0, 0, 0, 0, 0), cancellationToken)
                    .ConfigureAwait(false);
            }
        }

        var caller = CallerContext.CreateSystemContext();

        // Every user gets a quota row so the exemption has somewhere to live — new accounts have none.
        await _quotaService.GetOrCreateQuotaAsync(userId, caller, cancellationToken).ConfigureAwait(false);

        var tracked = await _db.UserDefaultContentItems
            .Where(i => i.UserId == userId)
            .ToDictionaryAsync(i => i.RelativePathKey, StringComparer.Ordinal, cancellationToken)
            .ConfigureAwait(false);

        var created = 0;
        var skipped = 0;
        var pendingUpdates = 0;
        var pendingDeletions = 0;
        var bytesCopied = 0L;

        // Manifest folders resolved during this pass (folder key -> node).
        var folders = new Dictionary<string, FileNode>(StringComparer.Ordinal);
        // Folders created during this pass (unsaved rows that DB queries cannot see yet).
        var createdFolderKeys = new HashSet<string>(StringComparer.Ordinal);
        // Folders the user deleted (or whose tracked node is gone): nothing below them is created.
        var blockedPrefixes = new List<string>();

        var manifestKeys = new HashSet<string>(manifest.Entries.Select(e => e.RelativePathKey), StringComparer.Ordinal);

        foreach (var entry in manifest.Entries)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var parentKey = GetParentKey(entry.RelativePath);

            if (IsUnderBlockedPrefix(entry.RelativePathKey, blockedPrefixes)
                || IsUnderMissingParent(parentKey, manifestKeys, folders))
            {
                skipped++;
                continue;
            }

            tracked.TryGetValue(entry.RelativePathKey, out var row);
            var node = await LoadTrackedNodeAsync(userId, row, cancellationToken).ConfigureAwait(false);

            // A tracking row whose node is gone means the user purged the item: never recreate.
            if (row is not null && row.State == DefaultContentItemState.RemovedByUser)
            {
                if (row.FileNodeId is null || node is null)
                {
                    if (row.FileNodeId is not null && node is null)
                    {
                        row.FileNodeId = null;
                        row.LastCheckedAtUtc = DateTime.UtcNow;
                    }

                    if (entry.Kind == DefaultContentItemKind.Folder)
                        blockedPrefixes.Add(entry.RelativePathKey);

                    skipped++;
                    continue;
                }
            }

            // A soft-deleted node is a user deletion: tombstone it and leave it in the trash.
            if (node is { IsDeleted: true })
            {
                row ??= CreateRow(userId, entry);
                row.State = DefaultContentItemState.RemovedByUser;
                row.LastCheckedAtUtc = DateTime.UtcNow;

                if (entry.Kind == DefaultContentItemKind.Folder)
                    blockedPrefixes.Add(entry.RelativePathKey);

                skipped++;
                continue;
            }

            // Adoption: look for an existing node unless its parent was created in this pass
            // (unsaved parents are invisible to queries, and nothing pre-existed beneath them).
            if (node is null && !(parentKey is not null && createdFolderKeys.Contains(parentKey)))
            {
                node = await ResolveExistingNodeAsync(userId, entry, cancellationToken).ConfigureAwait(false);
            }

            if (entry.Kind == DefaultContentItemKind.Folder)
            {
                if (node is null)
                {
                    node = await CreateFolderAsync(userId, entry, folders, cancellationToken).ConfigureAwait(false);
                    created++;
                    createdFolderKeys.Add(entry.RelativePathKey);
                }

                folders[entry.RelativePathKey] = node;

                if (row is null)
                {
                    row = CreateRow(userId, entry);
                    _db.UserDefaultContentItems.Add(row);
                }

                row.FileNodeId = node.Id;
                row.State = DefaultContentItemState.Present;
                row.LastCheckedAtUtc = DateTime.UtcNow;
                continue;
            }

            // ── File ──
            if (node is null)
            {
                var parentId = ResolveParentId(parentKey, folders);
                var result = await _import.ImportFileAsync(
                    userId, parentId, GetFileName(entry.RelativePath), entry.FullPath,
                    isReadOnly: true, caller, cancellationToken).ConfigureAwait(false);

                node = result.Node;
                created++;
                bytesCopied += result.Node.Size;

                if (row is null)
                {
                    row = CreateRow(userId, entry);
                    _db.UserDefaultContentItems.Add(row);
                }

                row.FileNodeId = node.Id;
                row.ProvisionedContentHash = entry.ContentHash;
                row.ProvisionedSize = entry.Size;
                row.State = DefaultContentItemState.Present;
                row.LastCheckedAtUtc = DateTime.UtcNow;
                continue;
            }

            if (row is null)
            {
                // Adoption: a same-named node already exists (pre-upgrade install). Never overwrite —
                // start tracking it, and only mark it read-only when the content is identical.
                var identical = string.Equals(node.ContentHash, entry.ContentHash, StringComparison.OrdinalIgnoreCase)
                                && node.Size == entry.Size;

                node.IsReadOnly = identical;

                row = CreateRow(userId, entry);
                row.FileNodeId = node.Id;
                row.ProvisionedContentHash = entry.ContentHash;
                row.ProvisionedSize = entry.Size;
                row.State = identical ? DefaultContentItemState.Present : DefaultContentItemState.ChangedByUser;
                row.LastCheckedAtUtc = DateTime.UtcNow;
                _db.UserDefaultContentItems.Add(row);
                continue;
            }

            row.FileNodeId = node.Id;
            row.LastCheckedAtUtc = DateTime.UtcNow;

            var matchesProvisioned = string.Equals(
                node.ContentHash, row.ProvisionedContentHash, StringComparison.OrdinalIgnoreCase);

            if (!matchesProvisioned)
            {
                // The user changed the node (or it was adopted with different content).
                row.State = DefaultContentItemState.ChangedByUser;

                if (mode.HasFlag(ReconcileMode.OverwriteModified))
                {
                    await _import.ReplaceCurrentContentAsync(node, entry.FullPath, caller, cancellationToken)
                        .ConfigureAwait(false);

                    node.IsReadOnly = true;
                    row.ProvisionedContentHash = entry.ContentHash;
                    row.ProvisionedSize = entry.Size;
                    row.State = DefaultContentItemState.Present;
                }

                continue;
            }

            var templateChanged = !string.Equals(entry.ContentHash, row.ProvisionedContentHash, StringComparison.OrdinalIgnoreCase);

            if (!templateChanged)
            {
                row.State = DefaultContentItemState.Present;
                row.ProvisionedSize = entry.Size;
                node.IsReadOnly = true;
                continue;
            }

            // Unmodified copy whose template content changed.
            row.State = DefaultContentItemState.Outdated;

            if (mode.HasFlag(ReconcileMode.PublishUpdates))
            {
                await _import.ReplaceCurrentContentAsync(node, entry.FullPath, caller, cancellationToken)
                    .ConfigureAwait(false);

                node.IsReadOnly = true;
                row.ProvisionedContentHash = entry.ContentHash;
                row.ProvisionedSize = entry.Size;
                row.State = DefaultContentItemState.Present;
            }
            else
            {
                pendingUpdates++;
            }
        }

        // ── Removal pass ──
        // Deepest entries first: a removed template file must be deleted before its folder is
        // examined, otherwise the folder still looks non-empty and is kept. Files go before folders,
        // and nested folders before their parents.
        var removalRows = tracked.Values
            .Where(r => !manifestKeys.Contains(r.RelativePathKey))
            .OrderBy(r => r.Kind == DefaultContentItemKind.File ? 0 : 1)
            .ThenByDescending(r => r.RelativePathKey.Length)
            .ToList();

        // Newly created rows are tracked in `tracked` only if inserted above; a row added during this
        // pass is by definition in the manifest, so no extra filtering is needed.
        foreach (var row in removalRows)
        {
            switch (row.State)
            {
                case DefaultContentItemState.ChangedByUser:
                    // No longer managed by the template.
                    _db.UserDefaultContentItems.Remove(row);
                    break;

                case DefaultContentItemState.Present when mode.HasFlag(ReconcileMode.AllowDeletions):
                    await DeleteTrackedCopyAsync(row, caller, cancellationToken).ConfigureAwait(false);
                    break;

                case DefaultContentItemState.Present:
                    pendingDeletions++;
                    break;

                case DefaultContentItemState.RemovedByUser when mode.HasFlag(ReconcileMode.AllowDeletions):
                    if (row.FileNodeId is Guid remainingNodeId)
                    {
                        await SafePermanentDeleteAsync(remainingNodeId, row, caller, cancellationToken)
                            .ConfigureAwait(false);
                    }
                    else
                    {
                        _db.UserDefaultContentItems.Remove(row);
                    }
                    break;
            }
        }

        await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        var exemptBytes = await _quotaService.RecomputeExemptionAsync(userId, cancellationToken).ConfigureAwait(false);

        var status = skipped > 0
            ? UserContentProvisioningStatus.Partial
            : UserContentProvisioningStatus.Completed;

        _logger.LogInformation(
            "Default-content pass for {UserId}: {Created} created, {Skipped} skipped, {Updates} pending update(s), {Deletions} pending deletion(s), {Exempt} exempt byte(s).",
            userId, created, skipped, pendingUpdates, pendingDeletions, exemptBytes);

        return await SaveStatusAsync(userId, new ProvisioningPassResult(
            status, created, skipped, pendingUpdates, pendingDeletions, bytesCopied),
            cancellationToken, manifest.Fingerprint).ConfigureAwait(false);
    }

    private async Task<FileNode?> LoadTrackedNodeAsync(
        Guid userId,
        UserDefaultContentItem? row,
        CancellationToken cancellationToken)
    {
        if (row?.FileNodeId is not Guid nodeId)
            return null;

        // IgnoreQueryFilters is mandatory: a trashed node must look deleted, not missing.
        return await _db.FileNodes
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(n => n.Id == nodeId && n.OwnerId == userId, cancellationToken)
            .ConfigureAwait(false);
    }

    /// <summary>
    /// Finds an existing node at the template path (adoption path for installs that predate the
    /// feature, or for template items added after a user already created the same path).
    /// </summary>
    private async Task<FileNode?> ResolveExistingNodeAsync(
        Guid userId,
        DefaultContentEntry entry,
        CancellationToken cancellationToken)
    {
        var segments = entry.RelativePath.Replace('\\', '/').Split('/', StringSplitOptions.RemoveEmptyEntries);
        if (segments.Length == 0)
            return null;

        Guid? parentId = null;
        FileNode? current = null;

        for (var i = 0; i < segments.Length; i++)
        {
            var name = segments[i];
            var isLast = i == segments.Length - 1;

            var query = _db.FileNodes
                .IgnoreQueryFilters()
                .Where(n => n.OwnerId == userId && !n.IsDeleted && n.Name.ToLower() == name.ToLower());

            query = parentId.HasValue
                ? query.Where(n => n.ParentId == parentId.Value)
                : query.Where(n => n.ParentId == null);

            if (!isLast)
                query = query.Where(n => n.NodeType == FileNodeType.Folder);
            else
                query = query.Where(n => n.NodeType == (entry.Kind == DefaultContentItemKind.Folder
                    ? FileNodeType.Folder
                    : FileNodeType.File));

            current = await query.FirstOrDefaultAsync(cancellationToken).ConfigureAwait(false);
            if (current is null)
                return null;

            parentId = current.Id;
        }

        return current;
    }

    private async Task<FileNode> CreateFolderAsync(
        Guid userId,
        DefaultContentEntry entry,
        IReadOnlyDictionary<string, FileNode> folders,
        CancellationToken cancellationToken)
    {
        var parentKey = GetParentKey(entry.RelativePath);
        var parent = parentKey is not null && folders.TryGetValue(parentKey, out var found) ? found : null;

        var folder = new FileNode
        {
            Name = GetFileName(entry.RelativePath),
            NodeType = FileNodeType.Folder,
            ParentId = parent?.Id,
            OwnerId = userId,
            Depth = parent is null ? 0 : parent.Depth + 1,
            IsReadOnly = false
        };

        folder.MaterializedPath = parent is null
            ? $"/{folder.Id}"
            : $"{parent.MaterializedPath}/{folder.Id}";

        _db.FileNodes.Add(folder);

        await SyncCursorHelper.AssignNextSequenceAsync(_db, folder, userId, null, cancellationToken)
            .ConfigureAwait(false);

        return folder;
    }

    private static Guid? ResolveParentId(string? parentKey, IReadOnlyDictionary<string, FileNode> folders) =>
        parentKey is not null && folders.TryGetValue(parentKey, out var parent) ? parent.Id : null;

    private static string[] SplitPath(string relativePath) =>
        relativePath.Replace('\\', '/').Split('/', StringSplitOptions.RemoveEmptyEntries);

    private static string GetFileName(string relativePath)
    {
        var segments = SplitPath(relativePath);
        return segments.Length == 0 ? relativePath : segments[^1];
    }

    private static string? GetParentKey(string relativePath)
    {
        var path = relativePath.Replace('\\', '/');
        var lastSlash = path.LastIndexOf('/');
        return lastSlash <= 0 ? null : DefaultUserContentStore.ToKey(path[..lastSlash]);
    }

    /// <summary>
    /// True when the entry's parent is itself a manifest folder that was not resolved.
    /// That happens when the user deleted the folder; its children must not be recreated.
    /// </summary>
    private static bool IsUnderMissingParent(
        string? parentKey,
        HashSet<string> manifestKeys,
        IReadOnlyDictionary<string, FileNode> folders) =>
        parentKey is not null && manifestKeys.Contains(parentKey) && !folders.ContainsKey(parentKey);

    private async Task DeleteTrackedCopyAsync(
        UserDefaultContentItem row,
        CallerContext caller,
        CancellationToken cancellationToken)
    {
        if (row.FileNodeId is not Guid nodeId)
        {
            _db.UserDefaultContentItems.Remove(row);
            return;
        }

        if (row.Kind == DefaultContentItemKind.Folder)
        {
            // Only delete an empty folder: user content inside it must survive.
            var hasChildren = await _db.FileNodes
                .AnyAsync(n => n.ParentId == nodeId, cancellationToken)
                .ConfigureAwait(false);

            if (hasChildren)
            {
                // Drop the tracking row only; the folder itself stays.
                _db.UserDefaultContentItems.Remove(row);
                return;
            }
        }

        await SafePermanentDeleteAsync(nodeId, row, caller, cancellationToken).ConfigureAwait(false);
    }

    private async Task SafePermanentDeleteAsync(
        Guid nodeId,
        UserDefaultContentItem row,
        CallerContext caller,
        CancellationToken cancellationToken)
    {
        try
        {
            if (_trashService is not null)
            {
                await _trashService.PermanentDeleteAsync(nodeId, caller, cancellationToken).ConfigureAwait(false);
            }
        }
        catch (DotNetCloud.Core.Errors.NotFoundException)
        {
            // Already gone — the tombstone is still correct.
        }

        _db.UserDefaultContentItems.Remove(row);
    }

    private static bool IsUnderBlockedPrefix(string key, List<string> blockedPrefixes) =>
        blockedPrefixes.Any(prefix => key.StartsWith(prefix + "/", StringComparison.Ordinal));

    /// <summary>
    /// Computes what a pass would do for one user without writing anything (dry run / preview).
    /// </summary>
    /// <param name="userId">User to preview.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public async Task<ProvisioningPreview> PreviewAsync(
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        var options = await _settings.GetAsync(cancellationToken).ConfigureAwait(false);
        if (!options.Enabled)
            return new ProvisioningPreview(0, 0, 0, 0, []);

        var validation = await _store.ValidateAsync(options, cancellationToken).ConfigureAwait(false);
        var manifest = validation.Manifest;
        if (!validation.RootExists || !validation.Readable || manifest.IsEmpty)
            return new ProvisioningPreview(0, 0, 0, 0, []);

        var tracked = await _db.UserDefaultContentItems
            .Where(i => i.UserId == userId)
            .ToDictionaryAsync(i => i.RelativePathKey, StringComparer.Ordinal, cancellationToken)
            .ConfigureAwait(false);

        var manifestKeys = new HashSet<string>(manifest.Entries.Select(e => e.RelativePathKey), StringComparer.Ordinal);
        var sample = new List<string>();
        int adds = 0, updates = 0, overwrites = 0, deletes = 0;

        foreach (var entry in manifest.Entries)
        {
            cancellationToken.ThrowIfCancellationRequested();

            tracked.TryGetValue(entry.RelativePathKey, out var row);
            var node = await LoadTrackedNodeAsync(userId, row, cancellationToken).ConfigureAwait(false);

            if (row is not null && row.State == DefaultContentItemState.RemovedByUser)
                continue;

            if (node is { IsDeleted: true })
                continue;

            if (node is null)
                node = await ResolveExistingNodeAsync(userId, entry, cancellationToken).ConfigureAwait(false);

            if (node is null)
            {
                if (entry.Kind == DefaultContentItemKind.File || row is null)
                {
                    adds++;
                    AddSample(sample, $"add {entry.RelativePath}");
                }

                continue;
            }

            if (entry.Kind == DefaultContentItemKind.Folder || row is null)
                continue;

            if (!string.Equals(node.ContentHash, row.ProvisionedContentHash, StringComparison.OrdinalIgnoreCase))
            {
                overwrites++;
                AddSample(sample, $"overwrite {entry.RelativePath}");
                continue;
            }

            if (!string.Equals(entry.ContentHash, row.ProvisionedContentHash, StringComparison.OrdinalIgnoreCase))
            {
                updates++;
                AddSample(sample, $"update {entry.RelativePath}");
            }
        }

        foreach (var row in tracked.Values.Where(r => !manifestKeys.Contains(r.RelativePathKey)))
        {
            if (row.State == DefaultContentItemState.Present)
            {
                deletes++;
                AddSample(sample, $"delete {row.RelativePath}");
            }
        }

        return new ProvisioningPreview(adds, updates, overwrites, deletes, sample);
    }

    private static void AddSample(List<string> sample, string text)
    {
        if (sample.Count < 20)
            sample.Add(text);
    }

    private static UserDefaultContentItem CreateRow(Guid userId, DefaultContentEntry entry) => new()
    {
        UserId = userId,
        RelativePath = entry.RelativePath,
        RelativePathKey = entry.RelativePathKey,
        Kind = entry.Kind,
        FileNodeId = null,
        ProvisionedContentHash = entry.ContentHash,
        ProvisionedSize = entry.Size,
        State = DefaultContentItemState.Present,
        FirstProvisionedAtUtc = DateTime.UtcNow,
        LastCheckedAtUtc = DateTime.UtcNow
    };

    private async Task<ProvisioningPassResult> SaveStatusAsync(
        Guid userId,
        ProvisioningPassResult result,
        CancellationToken cancellationToken,
        string? fingerprint = null)
    {
        var row = await _db.UserContentProvisioning
            .FirstOrDefaultAsync(p => p.UserId == userId, cancellationToken)
            .ConfigureAwait(false);

        var now = DateTime.UtcNow;
        if (row is null)
        {
            row = new UserContentProvisioning { UserId = userId, CreatedAtUtc = now };
            _db.UserContentProvisioning.Add(row);
        }

        row.Status = result.Status;
        row.ItemsCreated = result.ItemsCreated;
        row.ItemsSkipped = result.ItemsSkipped;
        row.PendingUpdates = result.PendingUpdates;
        row.PendingDeletions = result.PendingDeletions;
        row.BytesCopied = result.BytesCopied;
        row.LastError = result.Error is { Length: > 1024 } ? result.Error[..1024] : result.Error;
        row.LeaseExpiresAtUtc = null;
        row.UpdatedAtUtc = now;

        if (fingerprint is not null)
            row.TemplateFingerprint = fingerprint;

        if (result.Status is UserContentProvisioningStatus.Completed or UserContentProvisioningStatus.Partial
            or UserContentProvisioningStatus.Skipped)
        {
            row.AppliedAtUtc = now;
        }

        await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return result;
    }
}
