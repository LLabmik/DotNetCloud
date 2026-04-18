using DotNetCloud.Modules.Tracks.Data;
using DotNetCloud.Modules.Tracks.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace DotNetCloud.Modules.Tracks.Data.Services;

/// <summary>
/// Background service that dispatches due-date reminder notifications for Tracks cards.
/// Runs on a configurable interval (default: every hour) and sends reminders for cards
/// due within the next 24 hours that the user hasn't been reminded of recently.
/// </summary>
public sealed class DueDateReminderService : BackgroundService
{
    private static readonly TimeSpan CheckInterval = TimeSpan.FromHours(1);

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<DueDateReminderService> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="DueDateReminderService"/> class.
    /// </summary>
    public DueDateReminderService(IServiceScopeFactory scopeFactory, ILogger<DueDateReminderService> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    /// <inheritdoc />
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("DueDateReminderService started");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await DispatchRemindersAsync(stoppingToken);
            }
            catch (OperationCanceledException)
            {
                // Normal shutdown — exit loop
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in DueDateReminderService dispatch cycle");
            }

            await Task.Delay(CheckInterval, stoppingToken);
        }

        _logger.LogInformation("DueDateReminderService stopped");
    }

    private async Task DispatchRemindersAsync(CancellationToken cancellationToken)
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<TracksDbContext>();
        var notificationService = scope.ServiceProvider.GetService<ITracksNotificationService>();

        if (notificationService is null)
        {
            _logger.LogDebug("No ITracksNotificationService registered — skipping reminder dispatch");
            return;
        }

        var now = DateTime.UtcNow;
        var window = now.AddHours(24);

        // Find non-archived, non-deleted cards with due dates in the next 24 hours
        var dueCards = await db.Cards
            .AsNoTracking()
            .Include(c => c.Swimlane)
            .Include(c => c.Assignments)
            .Where(c => !c.IsDeleted
                        && !c.IsArchived
                        && c.DueDate.HasValue
                        && c.DueDate.Value >= now
                        && c.DueDate.Value <= window)
            .ToListAsync(cancellationToken);

        if (dueCards.Count == 0)
        {
            _logger.LogDebug("No due-soon cards found in current window");
            return;
        }

        var dispatched = 0;
        foreach (var card in dueCards)
        {
            // Get board ID for action URL
            var boardId = card.Swimlane?.BoardId ?? Guid.Empty;
            var assignees = card.Assignments.Select(a => a.UserId).ToList();

            foreach (var assigneeId in assignees)
            {
                try
                {
                    await notificationService.NotifyDueSoonAsync(
                        boardId,
                        card.Id,
                        card.Title,
                        card.DueDate!.Value,
                        assigneeId,
                        cancellationToken);
                    dispatched++;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to send due-date reminder for card {CardId} to user {UserId}",
                        card.Id, assigneeId);
                }
            }
        }

        if (dispatched > 0)
            _logger.LogInformation("Dispatched {Count} due-date reminders", dispatched);
    }
}
