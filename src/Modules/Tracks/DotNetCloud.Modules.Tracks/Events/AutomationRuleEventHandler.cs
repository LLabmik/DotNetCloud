using DotNetCloud.Core.Events;
using DotNetCloud.Modules.Tracks.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace DotNetCloud.Modules.Tracks.Events;

/// <summary>
/// Listens for Tracks domain events and delegates to <see cref="IAutomationRuleExecutionService"/>
/// for rule evaluation and action execution.
/// </summary>
internal sealed class AutomationRuleEventHandler :
    IEventHandler<WorkItemCreatedEvent>,
    IEventHandler<WorkItemMovedEvent>,
    IEventHandler<WorkItemUpdatedEvent>,
    IEventHandler<WorkItemAssignedEvent>
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<AutomationRuleEventHandler> _logger;

    public AutomationRuleEventHandler(
        IServiceProvider serviceProvider,
        ILogger<AutomationRuleEventHandler> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    public async Task HandleAsync(WorkItemCreatedEvent @event, CancellationToken ct)
    {
        try
        {
            using var scope = _serviceProvider.CreateScope();
            var executor = scope.ServiceProvider.GetRequiredService<IAutomationRuleExecutionService>();
            await executor.ExecuteAsync("work_item_created", @event.WorkItemId, ct: ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Automation rule evaluation failed for WorkItemCreated event");
        }
    }

    public async Task HandleAsync(WorkItemMovedEvent @event, CancellationToken ct)
    {
        try
        {
            using var scope = _serviceProvider.CreateScope();
            var executor = scope.ServiceProvider.GetRequiredService<IAutomationRuleExecutionService>();
            await executor.ExecuteAsync("work_item_moved", @event.WorkItemId,
                previousSwimlaneId: @event.FromSwimlaneId.ToString(),
                newSwimlaneId: @event.ToSwimlaneId.ToString(),
                ct: ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Automation rule evaluation failed for WorkItemMoved event");
        }
    }

    public async Task HandleAsync(WorkItemUpdatedEvent @event, CancellationToken ct)
    {
        try
        {
            using var scope = _serviceProvider.CreateScope();
            var executor = scope.ServiceProvider.GetRequiredService<IAutomationRuleExecutionService>();
            await executor.ExecuteAsync("status_changed", @event.WorkItemId, ct: ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Automation rule evaluation failed for WorkItemUpdated event");
        }
    }

    public async Task HandleAsync(WorkItemAssignedEvent @event, CancellationToken ct)
    {
        try
        {
            using var scope = _serviceProvider.CreateScope();
            var executor = scope.ServiceProvider.GetRequiredService<IAutomationRuleExecutionService>();
            await executor.ExecuteAsync("assigned", @event.WorkItemId, ct: ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Automation rule evaluation failed for WorkItemAssigned event");
        }
    }
}
