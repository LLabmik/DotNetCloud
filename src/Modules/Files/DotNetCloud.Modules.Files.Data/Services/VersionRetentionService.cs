using DotNetCloud.Modules.Files.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace DotNetCloud.Modules.Files.Data.Services;

/// <summary>
/// Applies the effective file version retention policy to every file that has version history.
/// </summary>
/// <remarks>
/// The policy comes from <see cref="IFileVersioningSettingsProvider"/>, so the values saved on
/// <c>/admin/files</c> are the ones enforced. Background scheduling lives in
/// <c>VersionCleanupService</c>; the same implementation backs the "Run retention now" action on the
/// admin page.
/// </remarks>
public sealed class VersionRetentionService : IVersionRetentionService
{
    private readonly FilesDbContext _db;
    private readonly IFileVersioningSettingsProvider _settings;
    private readonly ILogger<VersionRetentionService> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="VersionRetentionService"/> class.
    /// </summary>
    /// <param name="db">Database context.</param>
    /// <param name="settings">Resolves the effective versioning policy.</param>
    /// <param name="logger">Logger instance.</param>
    public VersionRetentionService(
        FilesDbContext db,
        IFileVersioningSettingsProvider settings,
        ILogger<VersionRetentionService> logger)
    {
        _db = db;
        _settings = settings;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<VersionRetentionSweepResult> RunAsync(CancellationToken cancellationToken = default)
    {
        var options = await _settings.GetAsync(cancellationToken);

        var maxVersionCount = FileVersioningAdminSettings.EffectiveMaxVersionCount(options);
        if (maxVersionCount <= 0 && options.RetentionDays <= 0)
        {
            // Unlimited history and no age policy — nothing is ever pruned.
            return new VersionRetentionSweepResult(options.Enabled, 0, 0);
        }

        var fileNodeIds = await _db.FileVersions
            .AsNoTracking()
            .Select(v => v.FileNodeId)
            .Distinct()
            .ToListAsync(cancellationToken);

        var totalDeleted = 0;

        foreach (var nodeId in fileNodeIds)
        {
            totalDeleted += await VersionRetentionEnforcer.ApplyAsync(_db, nodeId, options, cancellationToken);
        }

        if (totalDeleted > 0)
        {
            await _db.SaveChangesAsync(cancellationToken);
            _logger.LogInformation(
                "Version cleanup: pruned {Count} excess/expired versions across {Files} files",
                totalDeleted, fileNodeIds.Count);
        }

        return new VersionRetentionSweepResult(options.Enabled, fileNodeIds.Count, totalDeleted);
    }
}
