using DotNetCloud.Core.Authorization;
using DotNetCloud.Core.Capabilities;
using DotNetCloud.Core.Data.Context;
using DotNetCloud.Core.Errors;
using DotNetCloud.Core.Models;
using AdminSharedFolderDeletedEvent = DotNetCloud.Core.Events.AdminSharedFolderDeletedEvent;
using IEventBus = DotNetCloud.Core.Events.IEventBus;
using MountedEntryInfo = DotNetCloud.Core.Events.MountedEntryInfo;
using ICoreCapabilitiesClient = DotNetCloud.Modules.Files.Services.ICoreCapabilitiesClient;
using DotNetCloud.Modules.Files.Data.Services.Background;
using DotNetCloud.Modules.Files.DTOs;
using DotNetCloud.Modules.Files.Models;
using DotNetCloud.Modules.Files.Services;
using DotNetCloud.Modules.Search.Client;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace DotNetCloud.Modules.Files.Data.Services;

/// <summary>
/// Persists and validates admin-managed shared folder definitions.
/// </summary>
internal sealed class AdminSharedFolderService : IAdminSharedFolderService
{
    private const string FilesModuleId = "files";

    private readonly FilesDbContext _db;
    private readonly CoreDbContext? _coreDb;
    private readonly IAdminSharedFolderPathValidator _pathValidator;
    private readonly IUserOrganizationResolver? _userOrganizationResolver;
    private readonly IGroupDirectory? _groupDirectory;
    private readonly IAdminSharedFolderMaintenanceScheduler? _maintenanceScheduler;
    private readonly ISearchFtsClient? _searchClient;
    private readonly IEventBus? _eventBus;
    private readonly ICoreCapabilitiesClient? _coreClient;
    private readonly ILogger<AdminSharedFolderService> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="AdminSharedFolderService"/> class.
    /// </summary>
    public AdminSharedFolderService(
        FilesDbContext db,
        IAdminSharedFolderPathValidator pathValidator,
        CoreDbContext? coreDb = null,
        IUserOrganizationResolver? userOrganizationResolver = null,
        IGroupDirectory? groupDirectory = null,
        IAdminSharedFolderMaintenanceScheduler? maintenanceScheduler = null,
        ISearchFtsClient? searchClient = null,
        IEventBus? eventBus = null,
        ICoreCapabilitiesClient? coreClient = null,
        ILogger<AdminSharedFolderService>? logger = null)
    {
        _db = db;
        _coreDb = coreDb;
        _pathValidator = pathValidator;
        _userOrganizationResolver = userOrganizationResolver;
        _groupDirectory = groupDirectory;
        _maintenanceScheduler = maintenanceScheduler;
        _searchClient = searchClient;
        _eventBus = eventBus;
        _coreClient = coreClient;
        _logger = logger ?? Microsoft.Extensions.Logging.Abstractions.NullLogger<AdminSharedFolderService>.Instance;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<AdminSharedFolderDto>> GetSharedFoldersAsync(CallerContext caller, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(caller);

        var definitions = await _db.AdminSharedFolders
            .AsNoTracking()
            .Include(folder => folder.Grants)
            .OrderBy(folder => folder.DisplayName)
            .ToListAsync(cancellationToken);

        var groups = await LoadGroupMetadataAsync(definitions.SelectMany(folder => folder.Grants).Select(grant => grant.GroupId), cancellationToken);
        return definitions.Select(folder => ToDto(folder, groups)).ToList();
    }

    /// <inheritdoc />
    public async Task<AdminSharedFolderDto> GetSharedFolderAsync(Guid sharedFolderId, CallerContext caller, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(caller);

        var folder = await LoadDefinitionAsync(sharedFolderId, asNoTracking: true, cancellationToken);
        var groups = await LoadGroupMetadataAsync(folder.Grants.Select(grant => grant.GroupId), cancellationToken);
        return ToDto(folder, groups);
    }

    /// <inheritdoc />
    public async Task<AdminSharedFolderDirectoryBrowseDto> BrowseDirectoriesAsync(string? sourcePath, CallerContext caller, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(caller);

        var resolvedPath = await _pathValidator.ResolveDirectoryAsync(sourcePath, cancellationToken);
        cancellationToken.ThrowIfCancellationRequested();

        var directories = Directory.EnumerateDirectories(resolvedPath.CanonicalPath)
            .Select(path => Path.TrimEndingDirectorySeparator(Path.GetFullPath(path)))
            .OrderBy(path => Path.GetFileName(path), StringComparer.OrdinalIgnoreCase)
            .Select(path => new AdminSharedFolderDirectoryEntryDto
            {
                Name = Path.GetFileName(path),
                SourcePath = path,
                RelativePath = GetNormalizedRelativePath(resolvedPath.RootPath, path),
            })
            .ToList();

        return new AdminSharedFolderDirectoryBrowseDto
        {
            RootPath = resolvedPath.RootPath,
            CurrentPath = resolvedPath.CanonicalPath,
            RelativePath = resolvedPath.RelativePath,
            Directories = directories,
        };
    }

    /// <inheritdoc />
    public async Task<AdminSharedFolderDto> CreateSharedFolderAsync(CreateAdminSharedFolderDto dto, CallerContext caller, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(dto);
        ArgumentNullException.ThrowIfNull(caller);

        var displayName = NormalizeDisplayName(dto.DisplayName);
        var sourcePath = NormalizeSourcePath(dto.SourcePath);
        var validatedPath = await _pathValidator.ValidateAsync(sourcePath, cancellationToken: cancellationToken);
        var accessMode = ParseAccessMode(dto.AccessMode);
        var crawlMode = ParseCrawlMode(dto.CrawlMode);
        var scope = await ResolveScopeAsync(dto.GroupIds, caller, cancellationToken);

        await EnsureUniqueDisplayNameAsync(displayName, scope.OrganizationId, existingDefinitionId: null, cancellationToken);

        var now = DateTime.UtcNow;
        var folder = new AdminSharedFolderDefinition
        {
            OrganizationId = scope.OrganizationId,
            DisplayName = displayName,
            SourcePath = validatedPath.CanonicalPath,
            IsEnabled = dto.IsEnabled,
            AccessMode = accessMode,
            CrawlMode = crawlMode,
            NextScheduledScanAt = ResolveNextScheduledScanAt(crawlMode, dto.NextScheduledScanAt, now),
            LastScanStatus = AdminSharedFolderScanStatus.NeverScanned,
            ReindexState = AdminSharedFolderReindexState.Idle,
            CreatedByUserId = caller.UserId,
            CreatedAt = now,
            UpdatedAt = now,
            Grants = scope.Groups
                .Select(group => new AdminSharedFolderGrant
                {
                    GroupId = group.Id,
                    CreatedAt = now,
                })
                .ToList(),
        };

        _db.AdminSharedFolders.Add(folder);
        await _db.SaveChangesAsync(cancellationToken);

        return ToDto(folder, scope.Groups.ToDictionary(group => group.Id));
    }

    /// <inheritdoc />
    public async Task<AdminSharedFolderDto> UpdateSharedFolderAsync(Guid sharedFolderId, UpdateAdminSharedFolderDto dto, CallerContext caller, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(dto);
        ArgumentNullException.ThrowIfNull(caller);

        var folder = await LoadDefinitionAsync(sharedFolderId, asNoTracking: false, cancellationToken);
        var displayName = NormalizeDisplayName(dto.DisplayName);
        var sourcePath = NormalizeSourcePath(dto.SourcePath);
        var validatedPath = await _pathValidator.ValidateAsync(sourcePath, sharedFolderId, cancellationToken);
        var accessMode = ParseAccessMode(dto.AccessMode);
        var crawlMode = ParseCrawlMode(dto.CrawlMode);
        var scope = await ResolveScopeAsync(dto.GroupIds, caller, cancellationToken);

        await EnsureUniqueDisplayNameAsync(displayName, scope.OrganizationId, sharedFolderId, cancellationToken);

        var now = DateTime.UtcNow;
        folder.OrganizationId = scope.OrganizationId;
        folder.DisplayName = displayName;
        folder.SourcePath = validatedPath.CanonicalPath;
        folder.IsEnabled = dto.IsEnabled;
        folder.AccessMode = accessMode;
        folder.CrawlMode = crawlMode;
        folder.NextScheduledScanAt = ResolveNextScheduledScanAt(crawlMode, dto.NextScheduledScanAt, now);
        folder.UpdatedByUserId = caller.UserId;
        folder.UpdatedAt = now;

        var existingGrants = await _db.AdminSharedFolderGrants
            .Where(grant => grant.AdminSharedFolderId == sharedFolderId)
            .ToListAsync(cancellationToken);

        if (existingGrants.Count > 0)
        {
            _db.AdminSharedFolderGrants.RemoveRange(existingGrants);
        }

        await _db.SaveChangesAsync(cancellationToken);

        var replacementGrants = scope.Groups
            .Select(group => new AdminSharedFolderGrant
            {
                AdminSharedFolderId = sharedFolderId,
                GroupId = group.Id,
                CreatedAt = now,
            })
            .ToList();

        if (replacementGrants.Count > 0)
        {
            await _db.AdminSharedFolderGrants.AddRangeAsync(replacementGrants, cancellationToken);
            await _db.SaveChangesAsync(cancellationToken);
        }

        folder.Grants = replacementGrants;

        return ToDto(folder, scope.Groups.ToDictionary(group => group.Id));
    }

    /// <inheritdoc />
    public async Task<DeleteAdminSharedFolderResult> DeleteSharedFolderAsync(Guid sharedFolderId, CallerContext caller, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(caller);

        var folder = await LoadDefinitionAsync(sharedFolderId, asNoTracking: false, cancellationToken);

        // ── Gather mounted entries BEFORE cascade delete ──
        var mountedEntries = await _db.MountedNodeEntries
            .Where(e => e.SharedFolderId == sharedFolderId)
            .Select(e => new { e.RelativePath, e.IsDirectory })
            .ToListAsync(cancellationToken);

        var displayName = folder.DisplayName;
        var searchEntityIds = new List<string>(mountedEntries.Count + 1);

        // Root folder entity ID
        searchEntityIds.Add(VirtualMountedNodeRegistry.GetAdminSharedFolderRootId(sharedFolderId).ToString());

        // Each mounted entry entity ID
        foreach (var entry in mountedEntries)
        {
            var id = VirtualMountedNodeRegistry.GetMountedNodeId(
                sharedFolderId, entry.RelativePath, entry.IsDirectory);
            searchEntityIds.Add(id.ToString());
        }

        // ── Create cleanup status record ──
        var cleanupJobId = Guid.CreateVersion7();
        var status = new AdminSharedFolderCleanupStatus
        {
            CleanupJobId = cleanupJobId,
            SharedFolderId = sharedFolderId,
            DisplayName = displayName,
            Phase = CleanupPhase.DeletingDefinition,
            SearchDocsTotal = searchEntityIds.Count,
            StartedAt = DateTime.UtcNow,
        };
        _db.AdminSharedFolderCleanupStatuses.Add(status);
        await _db.SaveChangesAsync(cancellationToken);

        // ── Delete definition (cascade deletes grants + mounted entries) ──
        _db.AdminSharedFolders.Remove(folder);
        await _db.SaveChangesAsync(cancellationToken);

        // ── Update status: removing search docs ──
        status.Phase = CleanupPhase.RemovingSearchDocs;
        await _db.SaveChangesAsync(cancellationToken);

        // ── Remove search documents ──
        var searchRemoved = 0;
        if (_searchClient is { IsAvailable: true })
        {
            foreach (var entityId in searchEntityIds)
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (await _searchClient.RemoveDocumentAsync(FilesModuleId, entityId, cancellationToken))
                {
                    searchRemoved++;
                }
            }
        }

        _logger.LogInformation(
            "Admin shared folder {SharedFolderId} ('{DisplayName}') deleted. " +
            "Removed {SearchRemoved}/{SearchTotal} search documents.",
            sharedFolderId, displayName, searchRemoved, searchEntityIds.Count);

        // ── Update status: search cleanup done, ready for media cleanup ──
        status.SearchDocsRemoved = searchRemoved;
        status.Phase = CleanupPhase.CleaningMediaSources;
        await _db.SaveChangesAsync(cancellationToken);

        // ── Trigger media cleanup via Core.Server gRPC ──
        var deleteEvent = new AdminSharedFolderDeletedEvent
        {
            EventId = Guid.CreateVersion7(),
            CreatedAt = DateTime.UtcNow,
            SharedFolderId = sharedFolderId,
            DisplayName = displayName,
            MountedEntries = mountedEntries
                .Select(e => new MountedEntryInfo
                {
                    RelativePath = e.RelativePath,
                    IsDirectory = e.IsDirectory,
                })
                .ToList(),
        };

        // Prefer cross-process gRPC call to Core.Server
        if (_coreClient is { IsAvailable: true })
        {
            try
            {
                var coreResult = await _coreClient.CleanupAdminSharedFolderAsync(deleteEvent, cancellationToken);
                if (coreResult)
                {
                    _logger.LogInformation(
                        "Core.Server media cleanup triggered for shared folder {SharedFolderId}",
                        sharedFolderId);
                }
                else
                {
                    _logger.LogWarning(
                        "Core.Server media cleanup returned false for shared folder {SharedFolderId}",
                        sharedFolderId);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex,
                    "Failed to call Core.Server cleanup for {SharedFolderId}",
                    sharedFolderId);
            }
        }
        // Fallback: publish event for in-process subscribers (standalone/testing)
        else if (_eventBus is not null && caller is not null)
        {
            try
            {
                await _eventBus.PublishAsync(deleteEvent, caller, cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex,
                    "Failed to publish AdminSharedFolderDeletedEvent for {SharedFolderId}. " +
                    "Media cleanup will need to be triggered manually.",
                    sharedFolderId);
            }
        }

        return new DeleteAdminSharedFolderResult
        {
            Deleted = true,
            CleanupJobId = cleanupJobId,
            PendingSearchRemovals = searchEntityIds.Count,
            SearchDocsRemoved = searchRemoved,
            PendingMediaCleanup = true,
            MountedEntries = mountedEntries
                .Select(e => new MountedEntryInfo
                {
                    RelativePath = e.RelativePath,
                    IsDirectory = e.IsDirectory,
                })
                .ToList(),
        };
    }

    /// <inheritdoc />
    public async Task<AdminSharedFolderDto> RequestReindexAsync(Guid sharedFolderId, CallerContext caller, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(caller);

        var folder = await LoadDefinitionAsync(sharedFolderId, asNoTracking: false, cancellationToken);
        var now = DateTime.UtcNow;
        folder.ReindexState = AdminSharedFolderReindexState.Requested;
        folder.NextScheduledScanAt = now;
        folder.UpdatedByUserId = caller.UserId;
        folder.UpdatedAt = now;

        await _db.SaveChangesAsync(cancellationToken);
        _maintenanceScheduler?.TriggerProcessing();

        var groups = await LoadGroupMetadataAsync(folder.Grants.Select(grant => grant.GroupId), cancellationToken);
        return ToDto(folder, groups);
    }

    /// <inheritdoc />
    public async Task<AdminSharedFolderDto> ScheduleRescanAsync(Guid sharedFolderId, DateTime? nextScheduledScanAt, CallerContext caller, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(caller);

        var folder = await LoadDefinitionAsync(sharedFolderId, asNoTracking: false, cancellationToken);
        var now = DateTime.UtcNow;
        folder.NextScheduledScanAt = NormalizeUtc(nextScheduledScanAt) ?? now;
        folder.UpdatedByUserId = caller.UserId;
        folder.UpdatedAt = now;

        await _db.SaveChangesAsync(cancellationToken);
        _maintenanceScheduler?.TriggerProcessing();

        var groups = await LoadGroupMetadataAsync(folder.Grants.Select(grant => grant.GroupId), cancellationToken);
        return ToDto(folder, groups);
    }

    private async Task<AdminSharedFolderDefinition> LoadDefinitionAsync(Guid sharedFolderId, bool asNoTracking, CancellationToken cancellationToken)
    {
        var query = _db.AdminSharedFolders
            .Include(folder => folder.Grants)
            .Where(folder => folder.Id == sharedFolderId);

        if (asNoTracking)
        {
            query = query.AsNoTracking();
        }

        return await query.FirstOrDefaultAsync(cancellationToken)
            ?? throw new NotFoundException("AdminSharedFolderDefinition", sharedFolderId);
    }

    private async Task EnsureUniqueDisplayNameAsync(string displayName, Guid? organizationId, Guid? existingDefinitionId, CancellationToken cancellationToken)
    {
        var candidates = await _db.AdminSharedFolders
            .AsNoTracking()
            .Where(folder => (!existingDefinitionId.HasValue || folder.Id != existingDefinitionId.Value)
                && folder.OrganizationId == organizationId)
            .Select(folder => folder.DisplayName)
            .ToListAsync(cancellationToken);

        if (candidates.Any(candidate => string.Equals(candidate, displayName, StringComparison.OrdinalIgnoreCase)))
        {
            throw new ValidationException(nameof(CreateAdminSharedFolderDto.DisplayName), "A shared folder with this display name already exists in the current organization scope.");
        }
    }

    private async Task<ResolvedScope> ResolveScopeAsync(IEnumerable<Guid>? groupIds, CallerContext caller, CancellationToken cancellationToken)
    {
        var distinctGroupIds = (groupIds ?? [])
            .ToArray();

        if (distinctGroupIds.Any(groupId => groupId == Guid.Empty))
        {
            throw new ValidationException(nameof(CreateAdminSharedFolderDto.GroupIds), "Granted group IDs must be non-empty GUID values.");
        }

        var normalizedGroupIds = distinctGroupIds
            .Distinct()
            .ToArray();

        var callerOrganizationId = _userOrganizationResolver is null
            ? null
            : await _userOrganizationResolver.GetOrganizationIdAsync(caller.UserId, cancellationToken);

        if (normalizedGroupIds.Length == 0)
        {
            return new ResolvedScope
            {
                OrganizationId = callerOrganizationId,
                Groups = [],
            };
        }

        // Query the Groups table directly via the Files module's own DB connection,
        // bypassing gRPC entirely. The Core identity tables (dbo.Groups) live in the
        // same database and are accessible from any module's DbContext.
        var groups = await QueryGroupsDirectAsync(normalizedGroupIds, cancellationToken);

        // Fall back to IGroupDirectory if direct DB query returned no results
        // (e.g. when CoreDbContext is not available or group is not in the DB).
        if (groups.Count == 0 && _groupDirectory is not null)
        {
            _logger.LogDebug("Direct group query returned no results, falling back to IGroupDirectory");
            groups = await QueryGroupsViaDirectoryAsync(normalizedGroupIds, cancellationToken);
        }

        var foundIds = groups.Select(g => g.Id).ToHashSet();
        foreach (var groupId in normalizedGroupIds)
        {
            if (!foundIds.Contains(groupId))
            {
                throw new ValidationException(nameof(CreateAdminSharedFolderDto.GroupIds), $"Granted group '{groupId}' was not found.");
            }
        }

        var groupOrganizationIds = groups
            .Select(group => group.OrganizationId)
            .Distinct()
            .ToArray();

        if (groupOrganizationIds.Length > 1)
        {
            throw new ValidationException(nameof(CreateAdminSharedFolderDto.GroupIds), "Granted groups must all belong to the same organization.");
        }

        if (callerOrganizationId.HasValue && groupOrganizationIds.Length == 1 && groupOrganizationIds[0] != callerOrganizationId.Value)
        {
            throw new ValidationException(nameof(CreateAdminSharedFolderDto.GroupIds), "Granted groups must belong to the caller's organization.");
        }

        return new ResolvedScope
        {
            OrganizationId = callerOrganizationId ?? groupOrganizationIds.Single(),
            Groups = groups,
        };
    }

    /// <summary>
    /// Queries the core identity Groups table via CoreDbContext using proper EF LINQ.
    /// The CoreDbContext is registered as a read-only transient context in the Files module
    /// to avoid gRPC round-trips for group validation.
    /// </summary>
    private async Task<List<GroupInfo>> QueryGroupsDirectAsync(Guid[] groupIds, CancellationToken ct)
    {
        if (_coreDb is null)
        {
            _logger.LogWarning("CoreDbContext not available for group query");
            return [];
        }

        _logger.LogWarning("[DIAG] QueryGroupsDirectAsync: querying {Count} groups via CoreDbContext LINQ", groupIds.Length);

        var groups = await _coreDb.Groups
            .AsNoTracking()
            .Where(g => groupIds.Contains(g.Id))
            .Select(g => new GroupInfo
            {
                Id = g.Id,
                Name = g.Name,
                OrganizationId = g.OrganizationId,
                IsAllUsersGroup = g.IsAllUsersGroup,
                CreatedAt = g.CreatedAt,
            })
            .ToListAsync(ct);

        _logger.LogWarning("[DIAG] QueryGroupsDirectAsync: found {Count} of {Total} groups",
            groups.Count, groupIds.Length);

        return groups;
    }

    /// <summary>
    /// Queries group metadata via <see cref="IGroupDirectory"/> (gRPC or mock).
    /// Used as a fallback when <see cref="QueryGroupsDirectAsync"/> returns no results,
    /// e.g. when CoreDbContext is not available or in test scenarios.
    /// </summary>
    private async Task<List<GroupInfo>> QueryGroupsViaDirectoryAsync(Guid[] groupIds, CancellationToken ct)
    {
        var results = new List<GroupInfo>();
        if (_groupDirectory is null)
            return results;

        foreach (var groupId in groupIds)
        {
            try
            {
                var info = await _groupDirectory.GetGroupAsync(groupId, ct);
                if (info is not null)
                    results.Add(info);
            }
            catch
            {
                // Group not found — skip, validation will catch it below
            }
        }
        return results;
    }

    private async Task<IReadOnlyDictionary<Guid, GroupInfo>> LoadGroupMetadataAsync(IEnumerable<Guid> groupIds, CancellationToken cancellationToken)
    {
        var distinctIds = groupIds
            .Distinct()
            .ToArray();

        if (distinctIds.Length == 0)
        {
            return new Dictionary<Guid, GroupInfo>();
        }

        var rows = await QueryGroupsDirectAsync(distinctIds, cancellationToken);
        return rows.ToDictionary(g => g.Id);
    }

    private static AdminSharedFolderDto ToDto(AdminSharedFolderDefinition folder, IReadOnlyDictionary<Guid, GroupInfo> groups)
    {
        var grantedGroups = folder.Grants
            .OrderBy(grant => groups.TryGetValue(grant.GroupId, out var group)
                ? group.Name
                : grant.GroupId.ToString())
            .Select(grant =>
            {
                groups.TryGetValue(grant.GroupId, out var group);
                return new AdminSharedFolderGroupDto
                {
                    GroupId = grant.GroupId,
                    GroupName = group?.Name,
                    OrganizationId = group?.OrganizationId,
                    IsAllUsersGroup = group?.IsAllUsersGroup ?? false,
                    MemberCount = group?.MemberCount ?? 0,
                };
            })
            .ToList();

        return new AdminSharedFolderDto
        {
            Id = folder.Id,
            OrganizationId = folder.OrganizationId,
            DisplayName = folder.DisplayName,
            SourcePath = folder.SourcePath,
            IsEnabled = folder.IsEnabled,
            AccessMode = folder.AccessMode.ToString(),
            CrawlMode = folder.CrawlMode.ToString(),
            LastIndexedAt = folder.LastIndexedAt,
            NextScheduledScanAt = folder.NextScheduledScanAt,
            LastScanStatus = folder.LastScanStatus.ToString(),
            ReindexState = folder.ReindexState.ToString(),
            CreatedByUserId = folder.CreatedByUserId,
            UpdatedByUserId = folder.UpdatedByUserId,
            CreatedAt = folder.CreatedAt,
            UpdatedAt = folder.UpdatedAt,
            GrantedGroups = grantedGroups,
        };
    }

    private static AdminSharedFolderAccessMode ParseAccessMode(string? accessMode)
    {
        if (string.IsNullOrWhiteSpace(accessMode))
        {
            throw new ValidationException(nameof(CreateAdminSharedFolderDto.AccessMode), "Access mode is required.");
        }

        if (!Enum.TryParse<AdminSharedFolderAccessMode>(accessMode, ignoreCase: true, out var parsedMode))
        {
            throw new ValidationException(nameof(CreateAdminSharedFolderDto.AccessMode), $"Invalid access mode: {accessMode}.");
        }

        if (parsedMode != AdminSharedFolderAccessMode.ReadOnly)
        {
            throw new ValidationException(nameof(CreateAdminSharedFolderDto.AccessMode), "Only read-only admin shared folders are supported in v1.");
        }

        return parsedMode;
    }

    private static AdminSharedFolderCrawlMode ParseCrawlMode(string? crawlMode)
    {
        if (string.IsNullOrWhiteSpace(crawlMode))
        {
            throw new ValidationException(nameof(CreateAdminSharedFolderDto.CrawlMode), "Crawl mode is required.");
        }

        if (!Enum.TryParse<AdminSharedFolderCrawlMode>(crawlMode, ignoreCase: true, out var parsedMode))
        {
            throw new ValidationException(nameof(CreateAdminSharedFolderDto.CrawlMode), $"Invalid crawl mode: {crawlMode}.");
        }

        return parsedMode;
    }

    private static string NormalizeDisplayName(string? displayName)
    {
        var normalized = displayName?.Trim();
        if (string.IsNullOrWhiteSpace(normalized))
        {
            throw new ValidationException(nameof(CreateAdminSharedFolderDto.DisplayName), "Display name is required.");
        }

        return normalized;
    }

    private static string NormalizeSourcePath(string? sourcePath)
    {
        var normalized = sourcePath?.Trim();
        if (string.IsNullOrWhiteSpace(normalized))
        {
            throw new ValidationException(nameof(CreateAdminSharedFolderDto.SourcePath), "Source path is required.");
        }

        return normalized;
    }

    private static DateTime? NormalizeUtc(DateTime? value)
    {
        return value?.ToUniversalTime();
    }

    private static DateTime? ResolveNextScheduledScanAt(AdminSharedFolderCrawlMode crawlMode, DateTime? requestedNextScheduledScanAt, DateTime referenceUtc)
    {
        if (crawlMode != AdminSharedFolderCrawlMode.Scheduled)
        {
            return null;
        }

        return NormalizeUtc(requestedNextScheduledScanAt) ?? referenceUtc.AddHours(24);
    }

    private static string GetNormalizedRelativePath(string rootPath, string candidatePath)
    {
        var relativePath = Path.GetRelativePath(rootPath, candidatePath)
            .Replace('\\', '/');

        return relativePath == "."
            ? string.Empty
            : relativePath;
    }

    private sealed record ResolvedScope
    {
        public Guid? OrganizationId { get; init; }

        public required IReadOnlyList<GroupInfo> Groups { get; init; }
    }
}
