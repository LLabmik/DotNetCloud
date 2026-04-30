namespace DotNetCloud.Core.DTOs;

// ─── Enums ───────────────────────────────────────────────────────────────

public enum ProductMemberRole { Viewer, Member, Admin, Owner }
public enum WorkItemType { Epic, Feature, Item, SubItem }
public enum SwimlaneContainerType { Product, WorkItem }
public enum Priority { None, Low, Medium, High, Urgent }
public enum DependencyType { BlockedBy, RelatesTo }
public enum SprintStatus { Planning, Active, Completed }
public enum PokerSessionStatus { Voting, Revealed, Completed, Cancelled }
public enum PokerScale { Fibonacci, TShirt, PowersOfTwo, Custom }
public enum ReviewSessionStatus { Active, Paused, Ended }
public enum TracksTeamMemberRole { Member, Manager, Owner }

// ─── Response DTOs ────────────────────────────────────────────────────────

public sealed record ProductDto
{
    public required Guid Id { get; init; }
    public required Guid OrganizationId { get; init; }
    public required string Name { get; init; }
    public string? Description { get; init; }
    public string? Color { get; init; }
    public required Guid OwnerId { get; init; }
    public bool SubItemsEnabled { get; init; }
    public bool IsArchived { get; init; }
    public int SwimlaneCount { get; init; }
    public int EpicCount { get; init; }
    public int MemberCount { get; init; }
    public int LabelCount { get; init; }
    public required string ETag { get; init; }
    public required DateTime CreatedAt { get; init; }
    public required DateTime UpdatedAt { get; init; }
    public DateTime? DeletedAt { get; init; }
    public Guid? DeletedByUserId { get; init; }
    public string? DeletedByDisplayName { get; init; }
}

public sealed record ProductMemberDto
{
    public required Guid UserId { get; init; }
    public string? DisplayName { get; init; }
    public required ProductMemberRole Role { get; init; }
    public required DateTime JoinedAt { get; init; }
}

public sealed record SwimlaneDto
{
    public required Guid Id { get; init; }
    public SwimlaneContainerType ContainerType { get; init; }
    public required Guid ContainerId { get; init; }
    public required string Title { get; init; }
    public string? Color { get; init; }
    public required double Position { get; init; }
    public int? CardLimit { get; init; }
    public bool IsDone { get; init; }
    public bool IsArchived { get; init; }
    public int CardCount { get; init; }
    public required DateTime CreatedAt { get; init; }
    public required DateTime UpdatedAt { get; init; }
}

public sealed record WorkItemDto
{
    public required Guid Id { get; init; }
    public required Guid ProductId { get; init; }
    public Guid? ParentWorkItemId { get; init; }
    public required WorkItemType Type { get; init; }
    public Guid? SwimlaneId { get; init; }
    public string? SwimlaneTitle { get; init; }
    public int ItemNumber { get; init; }
    public required string Title { get; init; }
    public string? Description { get; init; }
    public required double Position { get; init; }
    public Priority Priority { get; init; }
    public DateTime? DueDate { get; init; }
    public int? StoryPoints { get; init; }
    public bool IsArchived { get; init; }
    public int CommentCount { get; init; }
    public int AttachmentCount { get; init; }
    public List<WorkItemAssignmentDto> Assignments { get; init; } = new();
    public List<LabelDto> Labels { get; init; } = new();
    public List<WorkItemDto>? ChildWorkItems { get; init; }
    public List<ChecklistDto>? Checklists { get; init; }
    public Guid? SprintId { get; init; }
    public string? SprintTitle { get; init; }
    public int? TotalTrackedMinutes { get; init; }
    public required string ETag { get; init; }
    public required DateTime CreatedAt { get; init; }
    public required DateTime UpdatedAt { get; init; }
}

public sealed record WorkItemAssignmentDto
{
    public required Guid UserId { get; init; }
    public string? DisplayName { get; init; }
    public required DateTime AssignedAt { get; init; }
}

public sealed record LabelDto
{
    public required Guid Id { get; init; }
    public required Guid ProductId { get; init; }
    public required string Title { get; init; }
    public required string Color { get; init; }
    public required DateTime CreatedAt { get; init; }
}

public sealed record WorkItemCommentDto
{
    public required Guid Id { get; init; }
    public required Guid WorkItemId { get; init; }
    public required Guid UserId { get; init; }
    public string? DisplayName { get; init; }
    public required string Content { get; init; }
    public bool IsEdited { get; init; }
    public required DateTime CreatedAt { get; init; }
    public required DateTime UpdatedAt { get; init; }
}

public sealed record WorkItemAttachmentDto
{
    public required Guid Id { get; init; }
    public required Guid WorkItemId { get; init; }
    public Guid? FileNodeId { get; init; }
    public string? Url { get; init; }
    public required string FileName { get; init; }
    public long? FileSize { get; init; }
    public string? MimeType { get; init; }
    public required Guid UploadedByUserId { get; init; }
    public required DateTime CreatedAt { get; init; }
}

public sealed record ChecklistDto
{
    public required Guid Id { get; init; }
    public required Guid ItemId { get; init; }
    public required string Title { get; init; }
    public required double Position { get; init; }
    public List<ChecklistItemDto> Items { get; init; } = new();
    public required DateTime CreatedAt { get; init; }
}

public sealed record ChecklistItemDto
{
    public required Guid Id { get; init; }
    public required Guid ChecklistId { get; init; }
    public required string Title { get; init; }
    public bool IsCompleted { get; init; }
    public required double Position { get; init; }
    public Guid? AssignedToUserId { get; init; }
    public required DateTime CreatedAt { get; init; }
    public required DateTime UpdatedAt { get; init; }
}

public sealed record WorkItemDependencyDto
{
    public required Guid Id { get; init; }
    public required Guid WorkItemId { get; init; }
    public required Guid DependsOnWorkItemId { get; init; }
    public string? DependsOnTitle { get; init; }
    public DependencyType Type { get; init; }
    public required DateTime CreatedAt { get; init; }
}

public sealed record SprintDto
{
    public required Guid Id { get; init; }
    public required Guid EpicId { get; init; }
    public required string Title { get; init; }
    public string? Goal { get; init; }
    public DateTime? StartDate { get; init; }
    public DateTime? EndDate { get; init; }
    public SprintStatus Status { get; init; }
    public int? TargetStoryPoints { get; init; }
    public int? DurationWeeks { get; init; }
    public int? PlannedOrder { get; init; }
    public int ItemCount { get; init; }
    public int TotalStoryPoints { get; init; }
    public int CompletedStoryPoints { get; init; }
    public required DateTime CreatedAt { get; init; }
    public required DateTime UpdatedAt { get; init; }
}

public sealed record SprintItemDto
{
    public required Guid SprintId { get; init; }
    public required Guid ItemId { get; init; }
    public required DateTime AddedAt { get; init; }
}

public sealed record TimeEntryDto
{
    public required Guid Id { get; init; }
    public required Guid WorkItemId { get; init; }
    public required Guid UserId { get; init; }
    public DateTime? StartTime { get; init; }
    public DateTime? EndTime { get; init; }
    public int DurationMinutes { get; init; }
    public string? Description { get; init; }
    public required DateTime CreatedAt { get; init; }
    public required DateTime UpdatedAt { get; init; }
}

public sealed record ActivityDto
{
    public required Guid Id { get; init; }
    public required Guid ProductId { get; init; }
    public required Guid UserId { get; init; }
    public string? DisplayName { get; init; }
    public required string Action { get; init; }
    public required string EntityType { get; init; }
    public required Guid EntityId { get; init; }
    public string? Details { get; init; }
    public required DateTime CreatedAt { get; init; }
}

public sealed record PokerSessionDto
{
    public required Guid Id { get; init; }
    public required Guid EpicId { get; init; }
    public required Guid ItemId { get; init; }
    public required Guid CreatedByUserId { get; init; }
    public PokerScale Scale { get; init; }
    public string? CustomScaleValues { get; init; }
    public PokerSessionStatus Status { get; init; }
    public string? AcceptedEstimate { get; init; }
    public int Round { get; init; }
    public Guid? ReviewSessionId { get; init; }
    public required DateTime CreatedAt { get; init; }
    public required DateTime UpdatedAt { get; init; }
}

public sealed record PokerVoteDto
{
    public required Guid Id { get; init; }
    public required Guid SessionId { get; init; }
    public required Guid UserId { get; init; }
    public string? DisplayName { get; init; }
    public required string Estimate { get; init; }
    public int Round { get; init; }
    public required DateTime VotedAt { get; init; }
}

public sealed record PokerVoteStatusDto
{
    public bool HasVoted { get; init; }
    public string? Estimate { get; init; }
}

public sealed record ReviewSessionDto
{
    public required Guid Id { get; init; }
    public required Guid EpicId { get; init; }
    public required Guid HostUserId { get; init; }
    public Guid? CurrentItemId { get; init; }
    public ReviewSessionStatus Status { get; init; }
    public int ParticipantCount { get; init; }
    public required DateTime CreatedAt { get; init; }
    public DateTime? EndedAt { get; init; }
}

public sealed record ReviewSessionParticipantDto
{
    public required Guid UserId { get; init; }
    public string? DisplayName { get; init; }
    public bool IsConnected { get; init; }
    public required DateTime JoinedAt { get; init; }
}

public sealed record ProductTemplateDto
{
    public required Guid Id { get; init; }
    public required string Name { get; init; }
    public string? Description { get; init; }
    public string? Category { get; init; }
    public bool IsBuiltIn { get; init; }
    public required Guid CreatedByUserId { get; init; }
    public required string DefinitionJson { get; init; }
    public required DateTime CreatedAt { get; init; }
    public required DateTime UpdatedAt { get; init; }
}

public sealed record ItemTemplateDto
{
    public required Guid Id { get; init; }
    public required Guid ProductId { get; init; }
    public required string Name { get; init; }
    public string? TitlePattern { get; init; }
    public string? Description { get; init; }
    public Priority Priority { get; init; }
    public string? LabelIdsJson { get; init; }
    public string? ChecklistsJson { get; init; }
    public required Guid CreatedByUserId { get; init; }
    public required DateTime CreatedAt { get; init; }
    public required DateTime UpdatedAt { get; init; }
}

public sealed record TracksTeamDto
{
    public required Guid Id { get; init; }
    public required Guid TeamId { get; init; }
    public required string Name { get; init; }
    public string? Description { get; init; }
    public int MemberCount { get; init; }
    public required DateTime CreatedAt { get; init; }
}

public sealed record TracksTeamMemberDto
{
    public required Guid UserId { get; init; }
    public string? DisplayName { get; init; }
    public TracksTeamMemberRole Role { get; init; }
    public required DateTime AssignedAt { get; init; }
}

// ─── Analytics DTOs ────────────────────────────────────────────────────────

public sealed record ProductAnalyticsDto
{
    public int TotalItems { get; init; }
    public int TotalEpics { get; init; }
    public int TotalFeatures { get; init; }
    public int ItemsCompletedThisWeek { get; init; }
    public int ActiveSprints { get; init; }
    public double AvgCycleTimeDays { get; init; }
    public List<DailyCompletionDto> DailyCompletions { get; init; } = new();
}

public sealed record DailyCompletionDto
{
    public required DateTime Date { get; init; }
    public int CompletedCount { get; init; }
}

public sealed record SprintVelocityDto
{
    public required Guid SprintId { get; init; }
    public required string SprintTitle { get; init; }
    public int CompletedStoryPoints { get; init; }
    public int TotalStoryPoints { get; init; }
}

public sealed record SprintBurndownDto
{
    public int TotalStoryPoints { get; init; }
    public List<BurndownPointDto> Points { get; init; } = new();
}

public sealed record BurndownPointDto
{
    public required DateTime Date { get; init; }
    public int RemainingStoryPoints { get; init; }
}

public sealed record SprintReportDto
{
    public required SprintDto Sprint { get; init; }
    public int CompletedItems { get; init; }
    public int IncompleteItems { get; init; }
    public int CompletedStoryPoints { get; init; }
    public int TotalStoryPoints { get; init; }
}

// ─── Request DTOs ─────────────────────────────────────────────────────────

public sealed record CreateProductDto
{
    public required string Name { get; init; }
    public string? Description { get; init; }
    public string? Color { get; init; }
    public bool SubItemsEnabled { get; init; }
}

public sealed record UpdateProductDto
{
    public string? Name { get; init; }
    public string? Description { get; init; }
    public string? Color { get; init; }
    public bool? SubItemsEnabled { get; init; }
    public string? ETag { get; init; }
}

public sealed record AddProductMemberDto
{
    public required Guid UserId { get; init; }
    public ProductMemberRole Role { get; init; } = ProductMemberRole.Member;
}

public sealed record UpdateProductMemberRoleDto
{
    public required ProductMemberRole Role { get; init; }
}

public sealed record CreateLabelDto
{
    public required string Title { get; init; }
    public required string Color { get; init; }
}

public sealed record UpdateLabelDto
{
    public string? Title { get; init; }
    public string? Color { get; init; }
}

public sealed record CreateSwimlaneDto
{
    public required string Title { get; init; }
    public string? Color { get; init; }
    public bool IsDone { get; init; }
    public int? CardLimit { get; init; }
}

public sealed record UpdateSwimlaneDto
{
    public string? Title { get; init; }
    public string? Color { get; init; }
    public bool? IsDone { get; init; }
    public int? CardLimit { get; init; }
}

public sealed record ReorderSwimlanesDto
{
    public required List<Guid> OrderedIds { get; init; }
}

public sealed record CreateWorkItemDto
{
    public required string Title { get; init; }
    public string? Description { get; init; }
    public Priority Priority { get; init; }
    public DateTime? DueDate { get; init; }
    public int? StoryPoints { get; init; }
    public List<Guid> AssigneeIds { get; init; } = [];
    public List<Guid> LabelIds { get; init; } = [];
}

public sealed record UpdateWorkItemDto
{
    public string? Title { get; init; }
    public string? Description { get; init; }
    public Priority? Priority { get; init; }
    public DateTime? DueDate { get; init; }
    public int? StoryPoints { get; init; }
    public bool? IsArchived { get; init; }
    public string? ETag { get; init; }
}

public sealed record MoveWorkItemDto
{
    public required Guid TargetSwimlaneId { get; init; }
    public double? Position { get; init; }
}

public sealed record AddWorkItemCommentDto
{
    public required string Content { get; init; }
}

public sealed record UpdateWorkItemCommentDto
{
    public required string Content { get; init; }
}

public sealed record AddWorkItemDependencyDto
{
    public required Guid DependsOnWorkItemId { get; init; }
    public DependencyType Type { get; init; } = DependencyType.BlockedBy;
}

public sealed record CreateChecklistDto
{
    public required string Title { get; init; }
}

public sealed record AddChecklistItemDto
{
    public required string Title { get; init; }
    public Guid? AssignedToUserId { get; init; }
}

public sealed record CreateSprintDto
{
    public required string Title { get; init; }
    public string? Goal { get; init; }
    public DateTime? StartDate { get; init; }
    public DateTime? EndDate { get; init; }
    public int? TargetStoryPoints { get; init; }
    public int? DurationWeeks { get; init; }
}

public sealed record UpdateSprintDto
{
    public string? Title { get; init; }
    public string? Goal { get; init; }
    public DateTime? StartDate { get; init; }
    public DateTime? EndDate { get; init; }
    public int? TargetStoryPoints { get; init; }
}

public sealed record AddSprintItemDto
{
    public required Guid ItemId { get; init; }
}

public sealed record CreateTimeEntryDto
{
    public int DurationMinutes { get; init; }
    public string? Description { get; init; }
    public DateTime? StartTime { get; init; }
}

public sealed record CreatePokerSessionDto
{
    public required Guid ItemId { get; init; }
    public PokerScale Scale { get; init; } = PokerScale.Fibonacci;
    public string? CustomScaleValues { get; init; }
}

public sealed record SubmitPokerVoteDto
{
    public required string Estimate { get; init; }
}

public sealed record CreateReviewSessionDto
{
    public string? Title { get; init; }
}

public sealed record CreateSprintPlanDto
{
    public int NumberOfSprints { get; init; }
    public int SprintDurationWeeks { get; init; } = 2;
    public DateTime? StartDate { get; init; }
}

public sealed record AdjustSprintDto
{
    public int? DurationWeeks { get; init; }
    public DateTime? StartDate { get; init; }
    public DateTime? EndDate { get; init; }
}

public sealed record CreateProductFromTemplateDto
{
    public required string Name { get; init; }
    public required Guid OrganizationId { get; init; }
    public string? Description { get; init; }
    public string? Color { get; init; }
}

public sealed record SaveProductAsTemplateDto
{
    public required string Name { get; init; }
    public string? Description { get; init; }
    public string? Category { get; init; }
}

public sealed record CreateItemFromTemplateDto
{
    public required string Title { get; init; }
    public Guid? SwimlaneId { get; init; }
}

public sealed record SaveItemAsTemplateDto
{
    public required string Name { get; init; }
}

public sealed record CreateTracksTeamDto
{
    public required string Name { get; init; }
    public string? Description { get; init; }
}

public sealed record UpdateTracksTeamDto
{
    public string? Name { get; init; }
    public string? Description { get; init; }
}

public sealed record AddTracksTeamMemberDto
{
    public required Guid UserId { get; init; }
    public TracksTeamMemberRole Role { get; init; } = TracksTeamMemberRole.Member;
}

public sealed record UpdateTeamMemberRoleDto
{
    public required TracksTeamMemberRole Role { get; init; }
}

// ─── Custom Views ─────────────────────────────────────────────────────────

/// <summary>DTO for a saved custom view/filter.</summary>
public sealed record CustomViewDto
{
    public required Guid Id { get; init; }
    public required Guid ProductId { get; init; }
    public required Guid UserId { get; init; }
    public required string Name { get; init; }
    public required string FilterJson { get; init; }
    public required string SortJson { get; init; }
    public string? GroupBy { get; init; }
    public required string Layout { get; init; }
    public bool IsShared { get; init; }
    public required DateTime CreatedAt { get; init; }
    public required DateTime UpdatedAt { get; init; }
}
