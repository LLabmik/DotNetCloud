using DotNetCloud.Modules.Email.Models;
using DotNetCloud.Modules.Email.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace DotNetCloud.Modules.Email.Data.Services;

/// <summary>
/// Background service that cleans up temporary attachment files older than 24 hours
/// that have no associated <see cref="EmailAttachment"/> record.
/// These are orphaned files from compose uploads that were never sent.
/// </summary>
public sealed class CleanupTempAttachmentsBackgroundService : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<CleanupTempAttachmentsBackgroundService> _logger;
    private static readonly TimeSpan CleanupInterval = TimeSpan.FromHours(1);
    private static readonly TimeSpan AttachmentTtl = TimeSpan.FromHours(24);

    /// <summary>
    /// Initializes a new instance of the <see cref="CleanupTempAttachmentsBackgroundService"/> class.
    /// </summary>
    public CleanupTempAttachmentsBackgroundService(
        IServiceProvider serviceProvider,
        ILogger<CleanupTempAttachmentsBackgroundService> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    /// <inheritdoc />
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Temp attachment cleanup service started");

        // Delay initial run to allow system to settle after startup
        await Task.Delay(TimeSpan.FromMinutes(5), stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await CleanupOrphanedAttachmentsAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during temp attachment cleanup");
            }

            await Task.Delay(CleanupInterval, stoppingToken);
        }
    }

    private async Task CleanupOrphanedAttachmentsAsync(CancellationToken ct)
    {
        using var scope = _serviceProvider.CreateScope();
        var storage = (FileSystemAttachmentStorage)scope.ServiceProvider.GetRequiredService<IAttachmentStorage>();
        var db = scope.ServiceProvider.GetRequiredService<EmailDbContext>();

        var cutoff = DateTime.UtcNow - AttachmentTtl;
        var deletedCount = 0;
        var freedBytes = 0L;

        foreach (var storageKey in storage.GetAllStorageKeys())
        {
            ct.ThrowIfCancellationRequested();

            // Check if this storage key is referenced by any attachment record
            var hasReference = await db.EmailAttachments
                .AnyAsync(a => a.StorageKey == storageKey, ct);

            if (hasReference)
                continue;

            // Check creation time - only delete files older than TTL
            var created = storage.GetCreationTime(storageKey);
            if (created is null || created > cutoff)
                continue;

            var size = await storage.GetSizeAsync(storageKey, ct);
            if (await storage.DeleteAsync(storageKey, ct))
            {
                deletedCount++;
                freedBytes += size;
            }
        }

        if (deletedCount > 0)
        {
            _logger.LogInformation(
                "Temp attachment cleanup: deleted {Count} orphaned files, freed {FreedBytes} bytes",
                deletedCount, freedBytes);
        }
    }
}
