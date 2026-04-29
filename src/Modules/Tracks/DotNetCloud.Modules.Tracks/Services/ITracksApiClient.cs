using DotNetCloud.Core.DTOs;

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
}
