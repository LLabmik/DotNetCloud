using DotNetCloud.Core.Authorization;
using DotNetCloud.Core.Capabilities;
using DotNetCloud.Core.Data.Context;
using DotNetCloud.Core.Data.Entities.Modules;
using DotNetCloud.Core.DTOs;
using DotNetCloud.Core.Modules;
using DotNetCloud.Core.Server.Grpc.Clients;
using DotNetCloud.Core.Server.ModuleLoading;
using DotNetCloud.Core.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace DotNetCloud.Core.Server.Services;

/// <summary>
/// Delivers account erasure to every module and tracks the acknowledgements.
/// </summary>
/// <remarks>
/// <para>
/// Only modules whose manifest declares <c>handlesUserErasure</c> get a row: a module that holds no
/// per-user data has nothing to erase, and creating a row for it would leave every deletion pending
/// forever. The module-side base class answers <c>not_applicable</c> for those, which the
/// orchestrator treats as acknowledged if such a row ever exists.
/// </para>
/// <para>
/// Consent rows for the user are deleted as soon as the request is created — they are personal data
/// and must not wait on a module that may be offline for days.
/// </para>
/// </remarks>
internal sealed class UserErasureService : IUserErasureService
{
    /// <summary>How long a request keeps retrying before it is surfaced as failed.</summary>
    internal static readonly TimeSpan RetryWindow = TimeSpan.FromHours(72);

    /// <summary>Per-module call deadline. A module that has not answered by then is retried.</summary>
    internal static readonly TimeSpan PerModuleTimeout = TimeSpan.FromSeconds(30);

    private readonly CoreDbContext _db;
    private readonly IModuleManifestRegistry _manifests;
    private readonly IModuleErasureDispatcher _dispatcher;
    private readonly IModuleConsentService _consent;
    private readonly IAuditLogger _audit;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<UserErasureService> _logger;

    public UserErasureService(
        CoreDbContext db,
        IModuleManifestRegistry manifests,
        IModuleErasureDispatcher dispatcher,
        IModuleConsentService consent,
        IAuditLogger audit,
        TimeProvider timeProvider,
        ILogger<UserErasureService> logger)
    {
        _db = db;
        _manifests = manifests;
        _dispatcher = dispatcher;
        _consent = consent;
        _audit = audit;
        _timeProvider = timeProvider;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<UserErasureRequestDto> RequestErasureAsync(
        Guid userId,
        Guid? requestedByUserId,
        CancellationToken cancellationToken = default)
    {
        if (userId == Guid.Empty)
        {
            throw new ArgumentException("A user id is required.", nameof(userId));
        }

        var existing = await _db.UserErasureRequests
            .Include(request => request.Modules)
            .Where(request => request.UserId == userId
                && (request.Status == UserErasureRequestStatus.Pending
                    || request.Status == UserErasureRequestStatus.Failed))
            .OrderByDescending(request => request.RequestedAtUtc)
            .FirstOrDefaultAsync(cancellationToken);

        if (existing is not null)
        {
            return Map(existing);
        }

        var now = _timeProvider.GetUtcNow().UtcDateTime;
        var request = new UserErasureRequest
        {
            RequestId = Guid.CreateVersion7(),
            UserId = userId,
            RequestedAtUtc = now,
            RequestedByUserId = requestedByUserId,
            Status = UserErasureRequestStatus.Pending,
        };

        var moduleIds = await _db.InstalledModules
            .AsNoTracking()
            .Select(module => module.ModuleId)
            .ToListAsync(cancellationToken);

        foreach (var moduleId in moduleIds)
        {
            var manifest = _manifests.GetManifest(moduleId);
            if (manifest is null || manifest.HandlesUserErasure != true)
            {
                continue;
            }

            request.Modules.Add(new ModuleUserErasure
            {
                RequestId = request.RequestId,
                ModuleId = moduleId,
                Status = ModuleUserErasureStatus.Pending,
            });
        }

        _db.UserErasureRequests.Add(request);
        await _db.SaveChangesAsync(cancellationToken);

        // Consent rows are personal data: remove them without waiting for the modules.
        await _consent.DeleteAllForUserAsync(userId, cancellationToken);

        _logger.LogInformation(
            "User erasure {RequestId} queued for {ModuleCount} module(s)",
            request.RequestId,
            request.Modules.Count);

        return Map(request);
    }

    /// <inheritdoc />
    public async Task<UserErasureRequestDto?> GetAsync(
        Guid requestId,
        CancellationToken cancellationToken = default)
    {
        var request = await _db.UserErasureRequests
            .AsNoTracking()
            .Include(entity => entity.Modules)
            .FirstOrDefaultAsync(entity => entity.RequestId == requestId, cancellationToken);

        return request is null ? null : Map(request);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<UserErasureRequestDto>> ListAsync(
        bool includeCompleted = false,
        CancellationToken cancellationToken = default)
    {
        var query = _db.UserErasureRequests
            .AsNoTracking()
            .Include(request => request.Modules)
            .AsQueryable();

        if (!includeCompleted)
        {
            query = query.Where(request => request.Status == UserErasureRequestStatus.Pending);
        }

        var requests = await query
            .OrderByDescending(request => request.RequestedAtUtc)
            .Take(500)
            .ToListAsync(cancellationToken);

        return requests.Select(Map).ToArray();
    }

    /// <inheritdoc />
    public async Task<int> ProcessPendingAsync(CancellationToken cancellationToken = default)
    {
        var now = _timeProvider.GetUtcNow().UtcDateTime;

        var requests = await _db.UserErasureRequests
            .Include(request => request.Modules)
            .Where(request => request.Status == UserErasureRequestStatus.Pending)
            .OrderBy(request => request.RequestedAtUtc)
            .Take(100)
            .ToListAsync(cancellationToken);

        var completedRows = 0;

        foreach (var request in requests)
        {
            foreach (var moduleRow in request.Modules
                         .Where(row => row.Status != ModuleUserErasureStatus.Acknowledged)
                         .ToArray())
            {
                var acknowledged = await DeliverAsync(request, moduleRow, cancellationToken);

                if (acknowledged)
                {
                    moduleRow.Status = ModuleUserErasureStatus.Acknowledged;
                    moduleRow.CompletedAtUtc = now;
                    moduleRow.LastError = null;
                    completedRows++;
                }
                else
                {
                    moduleRow.Status = ModuleUserErasureStatus.Failed;
                }

                moduleRow.Attempts++;
            }

            if (request.Modules.All(row => row.Status == ModuleUserErasureStatus.Acknowledged))
            {
                request.Status = UserErasureRequestStatus.Completed;
                request.CompletedAtUtc = now;
                _logger.LogInformation("User erasure {RequestId} completed.", request.RequestId);
            }
            else if (now - request.RequestedAtUtc > RetryWindow)
            {
                request.Status = UserErasureRequestStatus.Failed;
                _logger.LogWarning(
                    "User erasure {RequestId} exhausted its {Hours}-hour window with {Outstanding} module(s) outstanding.",
                    request.RequestId,
                    RetryWindow.TotalHours,
                    request.Modules.Count(row => row.Status != ModuleUserErasureStatus.Acknowledged));
            }
        }

        if (requests.Count > 0)
        {
            await _db.SaveChangesAsync(cancellationToken);
        }

        return completedRows;
    }

    /// <inheritdoc />
    public async Task<bool> OverrideAsync(
        Guid requestId,
        Guid adminUserId,
        CancellationToken cancellationToken = default)
    {
        var request = await _db.UserErasureRequests
            .Include(entity => entity.Modules)
            .FirstOrDefaultAsync(entity => entity.RequestId == requestId, cancellationToken);

        if (request is null || request.Status is UserErasureRequestStatus.Completed
            or UserErasureRequestStatus.Overridden)
        {
            return false;
        }

        request.Status = UserErasureRequestStatus.Overridden;
        request.CompletedAtUtc = _timeProvider.GetUtcNow().UtcDateTime;

        await _db.SaveChangesAsync(cancellationToken);

        // Audit the override with the pseudonymous user id only — no email or display name.
        await _audit.LogAsync(
            new AuditEntry
            {
                Caller = CallerContext.CreateSystemContext(),
                ModuleId = "core",
                Action = AuditAction.ModuleUserErasureOverridden,
                EntityType = nameof(UserErasureRequest),
                EntityId = requestId,
                Description = "Administrator force-completed a user-erasure request with outstanding modules.",
            },
            cancellationToken);

        _logger.LogWarning(
            "User erasure {RequestId} overridden by administrator {AdminUserId}.",
            requestId,
            adminUserId);

        return true;
    }

    private async Task<bool> DeliverAsync(
        UserErasureRequest request,
        ModuleUserErasure moduleRow,
        CancellationToken cancellationToken)
    {
        var declared = _manifests.GetUserConsentScopes(moduleRow.ModuleId) ?? [];

        var result = await _dispatcher.EraseAsync(
            moduleRow.ModuleId,
            request.RequestId,
            request.UserId,
            declared,
            cancellationToken);

        if (result.Acknowledged)
        {
            moduleRow.LastError = null;
            return true;
        }

        moduleRow.LastError = Truncate(result.Error);
        _logger.LogWarning(
            "Module {ModuleId} declined user erasure {RequestId}: {Error}",
            LogSanitizer.Sanitize(moduleRow.ModuleId),
            request.RequestId,
            LogSanitizer.Sanitize(moduleRow.LastError));

        return false;
    }

    private static string Truncate(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return "The module did not acknowledge the erasure.";
        }

        return value.Length <= 2000 ? value : value[..2000];
    }

    private static UserErasureRequestDto Map(UserErasureRequest request) => new()
    {
        RequestId = request.RequestId,
        UserId = request.UserId,
        RequestedAtUtc = request.RequestedAtUtc,
        Status = request.Status.ToString(),
        CompletedAtUtc = request.CompletedAtUtc,
        Modules = request.Modules
            .OrderBy(row => row.ModuleId, StringComparer.OrdinalIgnoreCase)
            .Select(row => new ModuleUserErasureDto
            {
                ModuleId = row.ModuleId,
                Status = row.Status.ToString(),
                Attempts = row.Attempts,
                LastError = row.LastError,
                CompletedAtUtc = row.CompletedAtUtc,
            })
            .ToArray(),
    };
}
