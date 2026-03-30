using DotNetCloud.Core.DTOs;

namespace DotNetCloud.Modules.Tracks.Services;

/// <summary>
/// HTTP API client for Tracks REST endpoints.
/// </summary>
public interface ITracksApiClient
{
    // ── Boards ──────────────────────────────────────────────

    Task<IReadOnlyList<BoardDto>> ListBoardsAsync(bool includeArchived = false, CancellationToken ct = default);
    Task<BoardDto?> GetBoardAsync(Guid boardId, CancellationToken ct = default);
    Task<BoardDto?> CreateBoardAsync(CreateBoardDto dto, CancellationToken ct = default);
    Task<BoardDto?> UpdateBoardAsync(Guid boardId, UpdateBoardDto dto, CancellationToken ct = default);
    Task DeleteBoardAsync(Guid boardId, CancellationToken ct = default);

    // ── Board Members ───────────────────────────────────────

    Task<IReadOnlyList<BoardMemberDto>> ListBoardMembersAsync(Guid boardId, CancellationToken ct = default);
    Task AddBoardMemberAsync(Guid boardId, Guid userId, BoardMemberRole role, CancellationToken ct = default);
    Task RemoveBoardMemberAsync(Guid boardId, Guid userId, CancellationToken ct = default);
    Task UpdateBoardMemberRoleAsync(Guid boardId, Guid userId, BoardMemberRole role, CancellationToken ct = default);

    // ── Labels ──────────────────────────────────────────────

    Task<IReadOnlyList<LabelDto>> ListLabelsAsync(Guid boardId, CancellationToken ct = default);
    Task<LabelDto?> CreateLabelAsync(Guid boardId, CreateLabelDto dto, CancellationToken ct = default);
    Task<LabelDto?> UpdateLabelAsync(Guid boardId, Guid labelId, UpdateLabelDto dto, CancellationToken ct = default);
    Task DeleteLabelAsync(Guid boardId, Guid labelId, CancellationToken ct = default);

    // ── Lists ───────────────────────────────────────────────

    Task<IReadOnlyList<BoardListDto>> ListListsAsync(Guid boardId, CancellationToken ct = default);
    Task<BoardListDto?> CreateListAsync(Guid boardId, CreateBoardListDto dto, CancellationToken ct = default);
    Task<BoardListDto?> UpdateListAsync(Guid boardId, Guid listId, UpdateBoardListDto dto, CancellationToken ct = default);
    Task DeleteListAsync(Guid boardId, Guid listId, CancellationToken ct = default);
    Task ReorderListsAsync(Guid boardId, IReadOnlyList<Guid> listIds, CancellationToken ct = default);

    // ── Cards ───────────────────────────────────────────────

    Task<IReadOnlyList<CardDto>> ListCardsAsync(Guid listId, bool includeArchived = false, CancellationToken ct = default);
    Task<CardDto?> GetCardAsync(Guid cardId, CancellationToken ct = default);
    Task<CardDto?> CreateCardAsync(Guid listId, CreateCardDto dto, CancellationToken ct = default);
    Task<CardDto?> UpdateCardAsync(Guid cardId, UpdateCardDto dto, CancellationToken ct = default);
    Task DeleteCardAsync(Guid cardId, CancellationToken ct = default);
    Task<CardDto?> MoveCardAsync(Guid cardId, MoveCardDto dto, CancellationToken ct = default);
    Task AssignUserAsync(Guid cardId, Guid userId, CancellationToken ct = default);
    Task UnassignUserAsync(Guid cardId, Guid userId, CancellationToken ct = default);
    Task AddLabelToCardAsync(Guid cardId, Guid labelId, CancellationToken ct = default);
    Task RemoveLabelFromCardAsync(Guid cardId, Guid labelId, CancellationToken ct = default);

    // ── Comments ────────────────────────────────────────────

    Task<IReadOnlyList<CardCommentDto>> ListCommentsAsync(Guid cardId, CancellationToken ct = default);
    Task<CardCommentDto?> CreateCommentAsync(Guid cardId, string content, CancellationToken ct = default);
    Task<CardCommentDto?> UpdateCommentAsync(Guid cardId, Guid commentId, string content, CancellationToken ct = default);
    Task DeleteCommentAsync(Guid cardId, Guid commentId, CancellationToken ct = default);

    // ── Checklists ──────────────────────────────────────────

    Task<IReadOnlyList<CardChecklistDto>> ListChecklistsAsync(Guid cardId, CancellationToken ct = default);
    Task<CardChecklistDto?> CreateChecklistAsync(Guid cardId, string title, CancellationToken ct = default);
    Task DeleteChecklistAsync(Guid cardId, Guid checklistId, CancellationToken ct = default);
    Task<ChecklistItemDto?> AddChecklistItemAsync(Guid cardId, Guid checklistId, string title, CancellationToken ct = default);
    Task<ChecklistItemDto?> ToggleChecklistItemAsync(Guid cardId, Guid checklistId, Guid itemId, CancellationToken ct = default);
    Task DeleteChecklistItemAsync(Guid cardId, Guid checklistId, Guid itemId, CancellationToken ct = default);

    // ── Attachments ─────────────────────────────────────────

    Task<IReadOnlyList<CardAttachmentDto>> ListAttachmentsAsync(Guid cardId, CancellationToken ct = default);
    Task<CardAttachmentDto?> AddAttachmentAsync(Guid cardId, string fileName, string? url, Guid? fileNodeId, CancellationToken ct = default);
    Task RemoveAttachmentAsync(Guid cardId, Guid attachmentId, CancellationToken ct = default);

    // ── Dependencies ────────────────────────────────────────

    Task<IReadOnlyList<CardDependencyDto>> ListDependenciesAsync(Guid cardId, CancellationToken ct = default);
    Task<CardDependencyDto?> AddDependencyAsync(Guid cardId, Guid dependsOnCardId, CardDependencyType type, CancellationToken ct = default);
    Task RemoveDependencyAsync(Guid cardId, Guid dependsOnCardId, CancellationToken ct = default);

    // ── Sprints ─────────────────────────────────────────────

    Task<IReadOnlyList<SprintDto>> ListSprintsAsync(Guid boardId, CancellationToken ct = default);
    Task<SprintDto?> GetSprintAsync(Guid boardId, Guid sprintId, CancellationToken ct = default);
    Task<SprintDto?> CreateSprintAsync(Guid boardId, CreateSprintDto dto, CancellationToken ct = default);
    Task<SprintDto?> UpdateSprintAsync(Guid boardId, Guid sprintId, UpdateSprintDto dto, CancellationToken ct = default);
    Task DeleteSprintAsync(Guid boardId, Guid sprintId, CancellationToken ct = default);
    Task<SprintDto?> StartSprintAsync(Guid boardId, Guid sprintId, CancellationToken ct = default);
    Task<SprintDto?> CompleteSprintAsync(Guid boardId, Guid sprintId, CancellationToken ct = default);
    Task AddCardToSprintAsync(Guid boardId, Guid sprintId, Guid cardId, CancellationToken ct = default);
    Task RemoveCardFromSprintAsync(Guid boardId, Guid sprintId, Guid cardId, CancellationToken ct = default);

    // ── Time Entries ────────────────────────────────────────

    Task<IReadOnlyList<TimeEntryDto>> ListTimeEntriesAsync(Guid cardId, CancellationToken ct = default);
    Task<TimeEntryDto?> CreateTimeEntryAsync(Guid cardId, CreateTimeEntryDto dto, CancellationToken ct = default);
    Task DeleteTimeEntryAsync(Guid cardId, Guid entryId, CancellationToken ct = default);
    Task<TimeEntryDto?> StartTimerAsync(Guid cardId, CancellationToken ct = default);
    Task<TimeEntryDto?> StopTimerAsync(Guid cardId, CancellationToken ct = default);

    // ── Activity ────────────────────────────────────────────

    Task<IReadOnlyList<BoardActivityDto>> GetBoardActivityAsync(Guid boardId, int skip = 0, int take = 50, CancellationToken ct = default);
    Task<IReadOnlyList<BoardActivityDto>> GetCardActivityAsync(Guid cardId, int skip = 0, int take = 50, CancellationToken ct = default);

    // ── Teams ───────────────────────────────────────────────

    Task<IReadOnlyList<TracksTeamDto>> ListTeamsAsync(CancellationToken ct = default);
    Task<TracksTeamDto?> GetTeamAsync(Guid teamId, CancellationToken ct = default);
    Task<TracksTeamDto?> CreateTeamAsync(CreateTracksTeamDto dto, CancellationToken ct = default);
    Task<TracksTeamDto?> UpdateTeamAsync(Guid teamId, UpdateTracksTeamDto dto, CancellationToken ct = default);
    Task DeleteTeamAsync(Guid teamId, bool cascade = false, CancellationToken ct = default);
    Task<IReadOnlyList<TracksTeamMemberDto>> ListTeamMembersAsync(Guid teamId, CancellationToken ct = default);
    Task AddTeamMemberAsync(Guid teamId, Guid userId, TracksTeamMemberRole role, CancellationToken ct = default);
    Task RemoveTeamMemberAsync(Guid teamId, Guid userId, CancellationToken ct = default);
    Task UpdateTeamMemberRoleAsync(Guid teamId, Guid userId, TracksTeamMemberRole role, CancellationToken ct = default);
    Task<IReadOnlyList<BoardDto>> ListTeamBoardsAsync(Guid teamId, bool includeArchived = false, CancellationToken ct = default);
    Task TransferBoardAsync(Guid boardId, Guid? teamId, CancellationToken ct = default);

    // ── User Search ─────────────────────────────────────────

    Task<IReadOnlyList<UserSearchResultDto>> SearchUsersAsync(string searchTerm, CancellationToken ct = default);
}
