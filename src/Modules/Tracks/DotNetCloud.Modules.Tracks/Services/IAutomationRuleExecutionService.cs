using DotNetCloud.Core.DTOs;

namespace DotNetCloud.Modules.Tracks.Services;

/// <summary>
/// Executes automation rules when work item events fire.
/// Implementation lives in the Data layer and resolves TracksDbContext.
/// </summary>
public interface IAutomationRuleExecutionService
{
    /// <summary>
    /// Evaluates and executes automation rules for a work item event.
    /// </summary>
    Task ExecuteAsync(string triggerType, Guid workItemId, string? previousSwimlaneId = null, string? newSwimlaneId = null, CancellationToken ct = default);
}
