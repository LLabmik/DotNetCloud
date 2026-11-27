namespace DotNetCloud.Modules.Tracks.Models;

/// <summary>DTO for a sprint/review discussion message.</summary>
public sealed record SprintDiscussionDto(
    Guid Id,
    Guid? SprintId,
    Guid? ReviewSessionId,
    Guid UserId,
    string UserDisplayName,
    string Content,
    DateTime CreatedAt
);

/// <summary>Request DTO for sending a discussion message.</summary>
public sealed record SendSprintDiscussionDto(string Content);
