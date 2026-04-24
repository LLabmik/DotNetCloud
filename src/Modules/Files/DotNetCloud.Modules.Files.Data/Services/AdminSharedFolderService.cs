using DotNetCloud.Core.Authorization;
using DotNetCloud.Core.Capabilities;
using DotNetCloud.Core.Errors;
using DotNetCloud.Modules.Files.DTOs;
using DotNetCloud.Modules.Files.Models;
using DotNetCloud.Modules.Files.Services;
using Microsoft.EntityFrameworkCore;

namespace DotNetCloud.Modules.Files.Data.Services;

/// <summary>
/// Persists and validates admin-managed shared folder definitions.
/// </summary>
internal sealed class AdminSharedFolderService : IAdminSharedFolderService
{
    private readonly FilesDbContext _db;
    private readonly IAdminSharedFolderPathValidator _pathValidator;
    private readonly IUserOrganizationResolver? _userOrganizationResolver;
    private readonly IGroupDirectory? _groupDirectory;

    /// <summary>
    /// Initializes a new instance of the <see cref="AdminSharedFolderService"/> class.
    /// </summary>
    public AdminSharedFolderService(
        FilesDbContext db,
        IAdminSharedFolderPathValidator pathValidator,
        IUserOrganizationResolver? userOrganizationResolver = null,
        IGroupDirectory? groupDirectory = null)
    {
        _db = db;
        _pathValidator = pathValidator;
        _userOrganizationResolver = userOrganizationResolver;
        _groupDirectory = groupDirectory;
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
    public async Task DeleteSharedFolderAsync(Guid sharedFolderId, CallerContext caller, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(caller);

        var folder = await LoadDefinitionAsync(sharedFolderId, asNoTracking: false, cancellationToken);
        _db.AdminSharedFolders.Remove(folder);
        await _db.SaveChangesAsync(cancellationToken);
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

        if (_groupDirectory is null)
        {
            throw new Core.Errors.InvalidOperationException("Group directory capability is required to validate shared-folder grants.");
        }

        var groups = new List<GroupInfo>(normalizedGroupIds.Length);
        foreach (var groupId in normalizedGroupIds)
        {
            var group = await _groupDirectory.GetGroupAsync(groupId, cancellationToken);
            if (group is null)
            {
                throw new ValidationException(nameof(CreateAdminSharedFolderDto.GroupIds), $"Granted group '{groupId}' was not found.");
            }

            groups.Add(group);
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

    private async Task<IReadOnlyDictionary<Guid, GroupInfo>> LoadGroupMetadataAsync(IEnumerable<Guid> groupIds, CancellationToken cancellationToken)
    {
        if (_groupDirectory is null)
        {
            return new Dictionary<Guid, GroupInfo>();
        }

        var distinctIds = groupIds
            .Distinct()
            .ToArray();

        if (distinctIds.Length == 0)
        {
            return new Dictionary<Guid, GroupInfo>();
        }

        var groups = new Dictionary<Guid, GroupInfo>(distinctIds.Length);
        foreach (var groupId in distinctIds)
        {
            var group = await _groupDirectory.GetGroupAsync(groupId, cancellationToken);
            if (group is not null)
            {
                groups[groupId] = group;
            }
        }

        return groups;
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