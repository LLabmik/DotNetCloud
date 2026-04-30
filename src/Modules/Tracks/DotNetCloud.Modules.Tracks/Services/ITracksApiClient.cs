using DotNetCloud.Core.Capabilities;
using DotNetCloud.Core.DTOs;
using DotNetCloud.Modules.Tracks.Models;

namespace DotNetCloud.Modules.Tracks.Services;

/// <summary>
/// HTTP API client for Tracks REST endpoints.
/// </summary>
public interface ITracksApiClient
{
    // Products
    Task<IReadOnlyList<ProductDto>> ListProductsAsync(Guid organizationId, CancellationToken ct = default);
    Task<ProductDto?> GetProductAsync(Guid productId, CancellationToken ct = default);
    Task<ProductDto?> CreateProductAsync(Guid organizationId, CreateProductDto dto, CancellationToken ct = default);
    Task<ProductDto?> UpdateProductAsync(Guid productId, UpdateProductDto dto, CancellationToken ct = default);
    Task DeleteProductAsync(Guid productId, CancellationToken ct = default);
    Task<IReadOnlyList<ProductDto>> ListDeletedProductsAsync(Guid organizationId, CancellationToken ct = default);
    Task<ProductDto?> RestoreProductAsync(Guid productId, CancellationToken ct = default);
    Task PermanentDeleteProductAsync(Guid productId, CancellationToken ct = default);

    // Product Members
    Task<IReadOnlyList<ProductMemberDto>> ListProductMembersAsync(Guid productId, CancellationToken ct = default);
    Task AddProductMemberAsync(Guid productId, AddProductMemberDto dto, CancellationToken ct = default);
    Task RemoveProductMemberAsync(Guid productId, Guid userId, CancellationToken ct = default);
    Task UpdateProductMemberRoleAsync(Guid productId, Guid userId, ProductMemberRole role, CancellationToken ct = default);

    // Labels
    Task<IReadOnlyList<LabelDto>> ListLabelsAsync(Guid productId, CancellationToken ct = default);
    Task<LabelDto?> CreateLabelAsync(Guid productId, CreateLabelDto dto, CancellationToken ct = default);
    Task<LabelDto?> UpdateLabelAsync(Guid productId, Guid labelId, UpdateLabelDto dto, CancellationToken ct = default);
    Task DeleteLabelAsync(Guid productId, Guid labelId, CancellationToken ct = default);

    // Swimlanes
    Task<IReadOnlyList<SwimlaneDto>> ListProductSwimlanesAsync(Guid productId, CancellationToken ct = default);
    Task<SwimlaneDto?> CreateProductSwimlaneAsync(Guid productId, CreateSwimlaneDto dto, CancellationToken ct = default);
    Task<IReadOnlyList<SwimlaneDto>> ListWorkItemSwimlanesAsync(Guid workItemId, CancellationToken ct = default);
    Task<SwimlaneDto?> CreateWorkItemSwimlaneAsync(Guid workItemId, CreateSwimlaneDto dto, CancellationToken ct = default);
    Task<SwimlaneDto?> UpdateSwimlaneAsync(Guid swimlaneId, UpdateSwimlaneDto dto, CancellationToken ct = default);
    Task DeleteSwimlaneAsync(Guid swimlaneId, CancellationToken ct = default);
    Task ReorderSwimlanesAsync(IReadOnlyList<Guid> swimlaneIds, CancellationToken ct = default);

    // Work Items
    Task<IReadOnlyList<WorkItemDto>> ListWorkItemsAsync(Guid swimlaneId, CancellationToken ct = default);
    Task<WorkItemDto?> GetWorkItemAsync(Guid workItemId, CancellationToken ct = default);
    Task<WorkItemDto?> GetWorkItemByNumberAsync(Guid productId, int itemNumber, CancellationToken ct = default);
    Task<WorkItemDto?> CreateEpicAsync(Guid swimlaneId, CreateWorkItemDto dto, CancellationToken ct = default);
    Task<WorkItemDto?> CreateFeatureAsync(Guid swimlaneId, CreateWorkItemDto dto, CancellationToken ct = default);
    Task<WorkItemDto?> CreateItemAsync(Guid swimlaneId, CreateWorkItemDto dto, CancellationToken ct = default);
    Task<WorkItemDto?> CreateSubItemAsync(Guid parentItemId, CreateWorkItemDto dto, CancellationToken ct = default);
    Task<WorkItemDto?> UpdateWorkItemAsync(Guid workItemId, UpdateWorkItemDto dto, CancellationToken ct = default);
    Task DeleteWorkItemAsync(Guid workItemId, CancellationToken ct = default);
    Task<WorkItemDto?> MoveWorkItemAsync(Guid workItemId, MoveWorkItemDto dto, CancellationToken ct = default);
    Task<IReadOnlyList<WorkItemDto>> GetChildWorkItemsAsync(Guid parentWorkItemId, CancellationToken ct = default);

    // Export
    /// <summary>Exports work items as a CSV file. Returns raw bytes.</summary>
    Task<byte[]> ExportWorkItemsCsvAsync(Guid productId, Guid? swimlaneId = null, Guid? labelId = null, Priority? priority = null, CancellationToken ct = default);

    // Watchers
    /// <summary>Gets the list of user IDs watching a work item.</summary>
    Task<IReadOnlyList<Guid>> GetWatchersAsync(Guid workItemId, CancellationToken ct = default);
    /// <summary>Start watching a work item. Returns the new watcher count.</summary>
    Task<int> WatchWorkItemAsync(Guid workItemId, CancellationToken ct = default);
    /// <summary>Stop watching a work item. Returns the new watcher count.</summary>
    Task<int> UnwatchWorkItemAsync(Guid workItemId, CancellationToken ct = default);

    // Work Item Assignments
    Task AssignUserAsync(Guid workItemId, Guid userId, CancellationToken ct = default);
    Task UnassignUserAsync(Guid workItemId, Guid userId, CancellationToken ct = default);

    // Work Item Labels
    Task AddLabelToWorkItemAsync(Guid workItemId, Guid labelId, CancellationToken ct = default);
    Task RemoveLabelFromWorkItemAsync(Guid workItemId, Guid labelId, CancellationToken ct = default);

    // Comments
    Task<IReadOnlyList<WorkItemCommentDto>> ListCommentsAsync(Guid workItemId, int skip = 0, int take = 50, CancellationToken ct = default);
    Task<WorkItemCommentDto?> CreateCommentAsync(Guid workItemId, string content, CancellationToken ct = default);
    Task<WorkItemCommentDto?> UpdateCommentAsync(Guid workItemId, Guid commentId, string content, CancellationToken ct = default);
    Task DeleteCommentAsync(Guid workItemId, Guid commentId, CancellationToken ct = default);

    // Checklists
    Task<IReadOnlyList<ChecklistDto>> ListChecklistsAsync(Guid itemId, CancellationToken ct = default);
    Task<ChecklistDto?> CreateChecklistAsync(Guid itemId, string title, CancellationToken ct = default);
    Task DeleteChecklistAsync(Guid itemId, Guid checklistId, CancellationToken ct = default);
    Task<ChecklistItemDto?> AddChecklistItemAsync(Guid itemId, Guid checklistId, string title, CancellationToken ct = default);
    Task<ChecklistItemDto?> ToggleChecklistItemAsync(Guid itemId, Guid checklistId, Guid checklistItemId, CancellationToken ct = default);
    Task DeleteChecklistItemAsync(Guid itemId, Guid checklistId, Guid checklistItemId, CancellationToken ct = default);

    // Attachments
    Task<IReadOnlyList<WorkItemAttachmentDto>> ListAttachmentsAsync(Guid workItemId, CancellationToken ct = default);
    Task<WorkItemAttachmentDto?> AddAttachmentAsync(Guid workItemId, string fileName, string? url, Guid? fileNodeId, CancellationToken ct = default);
    Task RemoveAttachmentAsync(Guid workItemId, Guid attachmentId, CancellationToken ct = default);

    // Dependencies
    Task<IReadOnlyList<WorkItemDependencyDto>> ListDependenciesAsync(Guid workItemId, CancellationToken ct = default);
    Task<WorkItemDependencyDto?> AddDependencyAsync(Guid workItemId, AddWorkItemDependencyDto dto, CancellationToken ct = default);
    Task RemoveDependencyAsync(Guid workItemId, Guid dependencyId, CancellationToken ct = default);

    // Sprints
    Task<IReadOnlyList<SprintDto>> ListSprintsAsync(Guid epicId, CancellationToken ct = default);
    Task<SprintDto?> GetSprintAsync(Guid sprintId, CancellationToken ct = default);
    Task<SprintDto?> CreateSprintAsync(Guid epicId, CreateSprintDto dto, CancellationToken ct = default);
    Task<SprintDto?> UpdateSprintAsync(Guid sprintId, UpdateSprintDto dto, CancellationToken ct = default);
    Task DeleteSprintAsync(Guid sprintId, CancellationToken ct = default);
    Task<SprintDto?> StartSprintAsync(Guid sprintId, CancellationToken ct = default);
    Task<SprintDto?> CompleteSprintAsync(Guid sprintId, CancellationToken ct = default);
    Task AddItemToSprintAsync(Guid sprintId, Guid itemId, CancellationToken ct = default);
    Task RemoveItemFromSprintAsync(Guid sprintId, Guid itemId, CancellationToken ct = default);
    Task<IReadOnlyList<WorkItemDto>> GetBacklogItemsAsync(Guid epicId, CancellationToken ct = default);

    // Sprint Planning
    Task<IReadOnlyList<SprintDto>> CreateSprintPlanAsync(Guid epicId, CreateSprintPlanDto dto, CancellationToken ct = default);
    Task<IReadOnlyList<SprintDto>> GetSprintPlanAsync(Guid epicId, CancellationToken ct = default);
    Task<IReadOnlyList<SprintDto>> AdjustSprintDatesAsync(Guid sprintId, AdjustSprintDto dto, CancellationToken ct = default);

    // Time Entries
    Task<IReadOnlyList<TimeEntryDto>> ListTimeEntriesAsync(Guid workItemId, CancellationToken ct = default);
    Task<TimeEntryDto?> CreateTimeEntryAsync(Guid workItemId, CreateTimeEntryDto dto, CancellationToken ct = default);
    Task DeleteTimeEntryAsync(Guid workItemId, Guid entryId, CancellationToken ct = default);
    Task<TimeEntryDto?> StartTimerAsync(Guid workItemId, CancellationToken ct = default);
    Task<TimeEntryDto?> StopTimerAsync(Guid workItemId, CancellationToken ct = default);

    // Activity
    Task<IReadOnlyList<ActivityDto>> GetProductActivityAsync(Guid productId, int skip = 0, int take = 50, CancellationToken ct = default);
    Task<IReadOnlyList<ActivityDto>> GetWorkItemActivityAsync(Guid workItemId, int skip = 0, int take = 50, CancellationToken ct = default);

    // Analytics
    Task<ProductAnalyticsDto?> GetProductAnalyticsAsync(Guid productId, CancellationToken ct = default);
    Task<IReadOnlyList<SprintVelocityDto>> GetVelocityDataAsync(Guid productId, CancellationToken ct = default);
    Task<SprintReportDto?> GetSprintReportAsync(Guid sprintId, CancellationToken ct = default);
    Task<SprintBurndownDto?> GetBurndownDataAsync(Guid sprintId, CancellationToken ct = default);
    Task<ProductDashboardDto?> GetProductDashboardAsync(Guid productId, CancellationToken ct = default);

    // Bulk Actions
    /// <summary>Performs a bulk action on multiple work items.</summary>
    Task<int> BulkWorkItemActionAsync(Guid productId, BulkWorkItemActionDto dto, CancellationToken ct = default);
    /// <summary>Lists all non-deleted work items for a product across all swimlanes.</summary>
    Task<IReadOnlyList<WorkItemDto>> ListProductWorkItemsAsync(Guid productId, Guid? swimlaneId = null, Guid? labelId = null, Priority? priority = null, CancellationToken ct = default);

    // Teams
    Task<IReadOnlyList<TracksTeamDto>> ListTeamsAsync(CancellationToken ct = default);
    Task<TracksTeamDto?> GetTeamAsync(Guid teamId, CancellationToken ct = default);
    Task<TracksTeamDto?> CreateTeamAsync(CreateTracksTeamDto dto, CancellationToken ct = default);
    Task<TracksTeamDto?> UpdateTeamAsync(Guid teamId, UpdateTracksTeamDto dto, CancellationToken ct = default);
    Task DeleteTeamAsync(Guid teamId, CancellationToken ct = default);
    Task<IReadOnlyList<TracksTeamMemberDto>> ListTeamMembersAsync(Guid teamId, CancellationToken ct = default);
    Task AddTeamMemberAsync(Guid teamId, AddTracksTeamMemberDto dto, CancellationToken ct = default);
    Task RemoveTeamMemberAsync(Guid teamId, Guid userId, CancellationToken ct = default);
    Task UpdateTeamMemberRoleAsync(Guid teamId, Guid userId, TracksTeamMemberRole role, CancellationToken ct = default);

    // Review Sessions
    Task<ReviewSessionDto?> StartReviewSessionAsync(Guid epicId, CancellationToken ct = default);
    Task<ReviewSessionDto?> GetReviewSessionAsync(Guid sessionId, CancellationToken ct = default);
    Task<ReviewSessionDto?> JoinReviewSessionAsync(Guid sessionId, CancellationToken ct = default);
    Task LeaveReviewSessionAsync(Guid sessionId, CancellationToken ct = default);
    Task<ReviewSessionDto?> SetReviewCurrentItemAsync(Guid sessionId, Guid itemId, CancellationToken ct = default);
    Task EndReviewSessionAsync(Guid sessionId, CancellationToken ct = default);

    // Planning Poker
    Task<PokerSessionDto?> StartPokerSessionAsync(Guid epicId, CreatePokerSessionDto dto, CancellationToken ct = default);
    Task<PokerSessionDto?> GetPokerSessionAsync(Guid sessionId, CancellationToken ct = default);
    Task<PokerSessionDto?> SubmitPokerVoteAsync(Guid sessionId, SubmitPokerVoteDto dto, CancellationToken ct = default);
    Task<PokerSessionDto?> RevealPokerSessionAsync(Guid sessionId, CancellationToken ct = default);
    Task<PokerSessionDto?> AcceptPokerEstimateAsync(Guid sessionId, string estimate, CancellationToken ct = default);
    Task<IReadOnlyList<PokerVoteStatusDto>> GetPokerVoteStatusAsync(Guid sessionId, CancellationToken ct = default);

    // User Search (for @mentions)
    /// <summary>Searches users by display name or email for @mention typeahead.</summary>
    Task<IReadOnlyList<UserSearchResult>> SearchUsersAsync(string searchTerm, int maxResults = 8, CancellationToken ct = default);

    // Custom Views (Saved Filters)
    /// <summary>Lists saved custom views for a product.</summary>
    Task<IReadOnlyList<CustomViewDto>> ListCustomViewsAsync(Guid productId, CancellationToken ct = default);
    /// <summary>Creates a new saved custom view.</summary>
    Task<CustomViewDto?> CreateCustomViewAsync(Guid productId, string name, string filterJson, string sortJson, string? groupBy, string layout, bool isShared, CancellationToken ct = default);
    /// <summary>Updates a saved custom view.</summary>
    Task<CustomViewDto?> UpdateCustomViewAsync(Guid productId, Guid viewId, string? name, string? filterJson, string? sortJson, string? groupBy, string? layout, bool? isShared, CancellationToken ct = default);
    /// <summary>Deletes a saved custom view.</summary>
    Task DeleteCustomViewAsync(Guid productId, Guid viewId, CancellationToken ct = default);

    // Webhooks
    /// <summary>Lists all webhook subscriptions for a product.</summary>
    Task<IReadOnlyList<WebhookSubscription>> ListProductWebhooksAsync(Guid productId, CancellationToken ct = default);
    /// <summary>Creates a new webhook subscription.</summary>
    Task<WebhookSubscription?> CreateProductWebhookAsync(Guid productId, string url, List<string> eventTypes, CancellationToken ct = default);
    /// <summary>Updates an existing webhook subscription.</summary>
    Task<WebhookSubscription?> UpdateProductWebhookAsync(Guid productId, Guid subscriptionId, string url, List<string> eventTypes, bool isActive, CancellationToken ct = default);
    /// <summary>Deletes a webhook subscription.</summary>
    Task DeleteProductWebhookAsync(Guid productId, Guid subscriptionId, CancellationToken ct = default);
    /// <summary>Sends a test ping to a webhook subscription.</summary>
    Task<WebhookTestResult> TestProductWebhookAsync(Guid productId, Guid subscriptionId, CancellationToken ct = default);

    // Roadmap
    /// <summary>Gets roadmap data for a product.</summary>
    Task<RoadmapDataDto?> GetRoadmapDataAsync(Guid productId, CancellationToken ct = default);

    // Automation Rules
    /// <summary>Lists all automation rules for a product.</summary>
    Task<List<AutomationRuleDto>> ListAutomationRulesAsync(Guid productId, CancellationToken ct = default);
    /// <summary>Creates a new automation rule.</summary>
    Task<AutomationRuleDto?> CreateAutomationRuleAsync(Guid productId, CreateAutomationRuleDto dto, CancellationToken ct = default);
    /// <summary>Updates an automation rule.</summary>
    Task<AutomationRuleDto?> UpdateAutomationRuleAsync(Guid ruleId, UpdateAutomationRuleDto dto, CancellationToken ct = default);
    /// <summary>Deletes an automation rule.</summary>
    Task DeleteAutomationRuleAsync(Guid ruleId, CancellationToken ct = default);

    // Goals / OKRs
    /// <summary>Lists all goals for a product.</summary>
    Task<List<GoalDto>> ListGoalsAsync(Guid productId, CancellationToken ct = default);
    /// <summary>Gets a single goal.</summary>
    Task<GoalDto?> GetGoalAsync(Guid goalId, CancellationToken ct = default);
    /// <summary>Creates a new goal.</summary>
    Task<GoalDto?> CreateGoalAsync(Guid productId, CreateGoalDto dto, CancellationToken ct = default);
    /// <summary>Updates a goal.</summary>
    Task<GoalDto?> UpdateGoalAsync(Guid goalId, UpdateGoalDto dto, CancellationToken ct = default);
    /// <summary>Deletes a goal.</summary>
    Task DeleteGoalAsync(Guid goalId, CancellationToken ct = default);
    /// <summary>Links a work item to a goal.</summary>
    Task LinkGoalWorkItemAsync(Guid goalId, LinkGoalWorkItemDto dto, CancellationToken ct = default);
    /// <summary>Unlinks a work item from a goal.</summary>
    Task UnlinkGoalWorkItemAsync(Guid goalId, Guid workItemId, CancellationToken ct = default);

    // Capacity Planning
    /// <summary>Gets capacity data for all members in a product's active sprints.</summary>
    Task<ProductCapacityDto?> GetProductCapacityAsync(Guid productId, CancellationToken ct = default);
    /// <summary>Gets capacity data for a specific sprint.</summary>
    Task<SprintCapacityDto?> GetSprintCapacityAsync(Guid sprintId, CancellationToken ct = default);
}

/// <summary>
/// Result of a webhook test ping.
/// </summary>
public sealed record WebhookTestResult(bool Success, int? StatusCode, long DurationMs, string? Error);
