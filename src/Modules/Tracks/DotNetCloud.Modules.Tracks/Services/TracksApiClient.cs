using System.Net.Http.Json;
using System.Text.Json;
using DotNetCloud.Core.DTOs;

namespace DotNetCloud.Modules.Tracks.Services;

/// <summary>
/// HTTP implementation of <see cref="ITracksApiClient"/>.
/// </summary>
public sealed class TracksApiClient : ITracksApiClient
{
    private readonly HttpClient _httpClient;
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public TracksApiClient(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    // ── Boards ──────────────────────────────────────────────

    public async Task<IReadOnlyList<BoardDto>> ListBoardsAsync(bool includeArchived = false, CancellationToken ct = default)
    {
        var url = "api/v1/boards";
        if (includeArchived) url += "?includeArchived=true";
        return await ReadDataAsync<IReadOnlyList<BoardDto>>(url, ct) ?? [];
    }

    public Task<BoardDto?> GetBoardAsync(Guid boardId, CancellationToken ct = default)
        => ReadDataAsync<BoardDto>($"api/v1/boards/{boardId}", ct);

    public async Task<BoardDto?> CreateBoardAsync(CreateBoardDto dto, CancellationToken ct = default)
    {
        var response = await _httpClient.PostAsJsonAsync("api/v1/boards", dto, ct);
        response.EnsureSuccessStatusCode();
        return await ReadDataFromResponseAsync<BoardDto>(response, ct);
    }

    public async Task<BoardDto?> UpdateBoardAsync(Guid boardId, UpdateBoardDto dto, CancellationToken ct = default)
    {
        var response = await _httpClient.PutAsJsonAsync($"api/v1/boards/{boardId}", dto, ct);
        response.EnsureSuccessStatusCode();
        return await ReadDataFromResponseAsync<BoardDto>(response, ct);
    }

    public async Task DeleteBoardAsync(Guid boardId, CancellationToken ct = default)
    {
        var response = await _httpClient.DeleteAsync($"api/v1/boards/{boardId}", ct);
        response.EnsureSuccessStatusCode();
    }

    // ── Board Members ───────────────────────────────────────

    public async Task<IReadOnlyList<BoardMemberDto>> ListBoardMembersAsync(Guid boardId, CancellationToken ct = default)
        => await ReadDataAsync<IReadOnlyList<BoardMemberDto>>($"api/v1/boards/{boardId}/members", ct) ?? [];

    public async Task AddBoardMemberAsync(Guid boardId, Guid userId, BoardMemberRole role, CancellationToken ct = default)
    {
        var response = await _httpClient.PostAsJsonAsync($"api/v1/boards/{boardId}/members", new { UserId = userId, Role = role }, ct);
        response.EnsureSuccessStatusCode();
    }

    public async Task RemoveBoardMemberAsync(Guid boardId, Guid userId, CancellationToken ct = default)
    {
        var response = await _httpClient.DeleteAsync($"api/v1/boards/{boardId}/members/{userId}", ct);
        response.EnsureSuccessStatusCode();
    }

    public async Task UpdateBoardMemberRoleAsync(Guid boardId, Guid userId, BoardMemberRole role, CancellationToken ct = default)
    {
        var response = await _httpClient.PutAsJsonAsync($"api/v1/boards/{boardId}/members/{userId}/role", new { Role = role }, ct);
        response.EnsureSuccessStatusCode();
    }

    // ── Labels ──────────────────────────────────────────────

    public async Task<IReadOnlyList<LabelDto>> ListLabelsAsync(Guid boardId, CancellationToken ct = default)
        => await ReadDataAsync<IReadOnlyList<LabelDto>>($"api/v1/boards/{boardId}/labels", ct) ?? [];

    public async Task<LabelDto?> CreateLabelAsync(Guid boardId, CreateLabelDto dto, CancellationToken ct = default)
    {
        var response = await _httpClient.PostAsJsonAsync($"api/v1/boards/{boardId}/labels", dto, ct);
        response.EnsureSuccessStatusCode();
        return await ReadDataFromResponseAsync<LabelDto>(response, ct);
    }

    public async Task<LabelDto?> UpdateLabelAsync(Guid boardId, Guid labelId, UpdateLabelDto dto, CancellationToken ct = default)
    {
        var response = await _httpClient.PutAsJsonAsync($"api/v1/boards/{boardId}/labels/{labelId}", dto, ct);
        response.EnsureSuccessStatusCode();
        return await ReadDataFromResponseAsync<LabelDto>(response, ct);
    }

    public async Task DeleteLabelAsync(Guid boardId, Guid labelId, CancellationToken ct = default)
    {
        var response = await _httpClient.DeleteAsync($"api/v1/boards/{boardId}/labels/{labelId}", ct);
        response.EnsureSuccessStatusCode();
    }

    // ── Lists ───────────────────────────────────────────────

    public async Task<IReadOnlyList<BoardListDto>> ListListsAsync(Guid boardId, CancellationToken ct = default)
        => await ReadDataAsync<IReadOnlyList<BoardListDto>>($"api/v1/boards/{boardId}/lists", ct) ?? [];

    public async Task<BoardListDto?> CreateListAsync(Guid boardId, CreateBoardListDto dto, CancellationToken ct = default)
    {
        var response = await _httpClient.PostAsJsonAsync($"api/v1/boards/{boardId}/lists", dto, ct);
        response.EnsureSuccessStatusCode();
        return await ReadDataFromResponseAsync<BoardListDto>(response, ct);
    }

    public async Task<BoardListDto?> UpdateListAsync(Guid boardId, Guid listId, UpdateBoardListDto dto, CancellationToken ct = default)
    {
        var response = await _httpClient.PutAsJsonAsync($"api/v1/boards/{boardId}/lists/{listId}", dto, ct);
        response.EnsureSuccessStatusCode();
        return await ReadDataFromResponseAsync<BoardListDto>(response, ct);
    }

    public async Task DeleteListAsync(Guid boardId, Guid listId, CancellationToken ct = default)
    {
        var response = await _httpClient.DeleteAsync($"api/v1/boards/{boardId}/lists/{listId}", ct);
        response.EnsureSuccessStatusCode();
    }

    public async Task ReorderListsAsync(Guid boardId, IReadOnlyList<Guid> listIds, CancellationToken ct = default)
    {
        var response = await _httpClient.PutAsJsonAsync($"api/v1/boards/{boardId}/lists/reorder", new { ListIds = listIds }, ct);
        response.EnsureSuccessStatusCode();
    }

    // ── Cards ───────────────────────────────────────────────

    public async Task<IReadOnlyList<CardDto>> ListCardsAsync(Guid listId, bool includeArchived = false, CancellationToken ct = default)
    {
        var url = $"api/v1/lists/{listId}/cards";
        if (includeArchived) url += "?includeArchived=true";
        return await ReadDataAsync<IReadOnlyList<CardDto>>(url, ct) ?? [];
    }

    public Task<CardDto?> GetCardAsync(Guid cardId, CancellationToken ct = default)
        => ReadDataAsync<CardDto>($"api/v1/cards/{cardId}", ct);

    public async Task<CardDto?> CreateCardAsync(Guid listId, CreateCardDto dto, CancellationToken ct = default)
    {
        var response = await _httpClient.PostAsJsonAsync($"api/v1/lists/{listId}/cards", dto, ct);
        response.EnsureSuccessStatusCode();
        return await ReadDataFromResponseAsync<CardDto>(response, ct);
    }

    public async Task<CardDto?> UpdateCardAsync(Guid cardId, UpdateCardDto dto, CancellationToken ct = default)
    {
        var response = await _httpClient.PutAsJsonAsync($"api/v1/cards/{cardId}", dto, ct);
        response.EnsureSuccessStatusCode();
        return await ReadDataFromResponseAsync<CardDto>(response, ct);
    }

    public async Task DeleteCardAsync(Guid cardId, CancellationToken ct = default)
    {
        var response = await _httpClient.DeleteAsync($"api/v1/cards/{cardId}", ct);
        response.EnsureSuccessStatusCode();
    }

    public async Task<CardDto?> MoveCardAsync(Guid cardId, MoveCardDto dto, CancellationToken ct = default)
    {
        var response = await _httpClient.PutAsJsonAsync($"api/v1/cards/{cardId}/move", dto, ct);
        response.EnsureSuccessStatusCode();
        return await ReadDataFromResponseAsync<CardDto>(response, ct);
    }

    public async Task AssignUserAsync(Guid cardId, Guid userId, CancellationToken ct = default)
    {
        var response = await _httpClient.PostAsJsonAsync($"api/v1/cards/{cardId}/assign", new { UserId = userId }, ct);
        response.EnsureSuccessStatusCode();
    }

    public async Task UnassignUserAsync(Guid cardId, Guid userId, CancellationToken ct = default)
    {
        var response = await _httpClient.DeleteAsync($"api/v1/cards/{cardId}/assign/{userId}", ct);
        response.EnsureSuccessStatusCode();
    }

    public async Task AddLabelToCardAsync(Guid cardId, Guid labelId, CancellationToken ct = default)
    {
        var response = await _httpClient.PostAsJsonAsync($"api/v1/cards/{cardId}/labels", new { LabelId = labelId }, ct);
        response.EnsureSuccessStatusCode();
    }

    public async Task RemoveLabelFromCardAsync(Guid cardId, Guid labelId, CancellationToken ct = default)
    {
        var response = await _httpClient.DeleteAsync($"api/v1/cards/{cardId}/labels/{labelId}", ct);
        response.EnsureSuccessStatusCode();
    }

    // ── Comments ────────────────────────────────────────────

    public async Task<IReadOnlyList<CardCommentDto>> ListCommentsAsync(Guid cardId, CancellationToken ct = default)
        => await ReadDataAsync<IReadOnlyList<CardCommentDto>>($"api/v1/cards/{cardId}/comments", ct) ?? [];

    public async Task<CardCommentDto?> CreateCommentAsync(Guid cardId, string content, CancellationToken ct = default)
    {
        var response = await _httpClient.PostAsJsonAsync($"api/v1/cards/{cardId}/comments", new { Content = content }, ct);
        response.EnsureSuccessStatusCode();
        return await ReadDataFromResponseAsync<CardCommentDto>(response, ct);
    }

    public async Task<CardCommentDto?> UpdateCommentAsync(Guid cardId, Guid commentId, string content, CancellationToken ct = default)
    {
        var response = await _httpClient.PutAsJsonAsync($"api/v1/cards/{cardId}/comments/{commentId}", new { Content = content }, ct);
        response.EnsureSuccessStatusCode();
        return await ReadDataFromResponseAsync<CardCommentDto>(response, ct);
    }

    public async Task DeleteCommentAsync(Guid cardId, Guid commentId, CancellationToken ct = default)
    {
        var response = await _httpClient.DeleteAsync($"api/v1/cards/{cardId}/comments/{commentId}", ct);
        response.EnsureSuccessStatusCode();
    }

    // ── Checklists ──────────────────────────────────────────

    public async Task<IReadOnlyList<CardChecklistDto>> ListChecklistsAsync(Guid cardId, CancellationToken ct = default)
        => await ReadDataAsync<IReadOnlyList<CardChecklistDto>>($"api/v1/cards/{cardId}/checklists", ct) ?? [];

    public async Task<CardChecklistDto?> CreateChecklistAsync(Guid cardId, string title, CancellationToken ct = default)
    {
        var response = await _httpClient.PostAsJsonAsync($"api/v1/cards/{cardId}/checklists", new { Title = title }, ct);
        response.EnsureSuccessStatusCode();
        return await ReadDataFromResponseAsync<CardChecklistDto>(response, ct);
    }

    public async Task DeleteChecklistAsync(Guid cardId, Guid checklistId, CancellationToken ct = default)
    {
        var response = await _httpClient.DeleteAsync($"api/v1/cards/{cardId}/checklists/{checklistId}", ct);
        response.EnsureSuccessStatusCode();
    }

    public async Task<ChecklistItemDto?> AddChecklistItemAsync(Guid cardId, Guid checklistId, string title, CancellationToken ct = default)
    {
        var response = await _httpClient.PostAsJsonAsync($"api/v1/cards/{cardId}/checklists/{checklistId}/items", new { Title = title }, ct);
        response.EnsureSuccessStatusCode();
        return await ReadDataFromResponseAsync<ChecklistItemDto>(response, ct);
    }

    public async Task<ChecklistItemDto?> ToggleChecklistItemAsync(Guid cardId, Guid checklistId, Guid itemId, CancellationToken ct = default)
    {
        var response = await _httpClient.PutAsync($"api/v1/cards/{cardId}/checklists/{checklistId}/items/{itemId}/toggle", null, ct);
        response.EnsureSuccessStatusCode();
        return await ReadDataFromResponseAsync<ChecklistItemDto>(response, ct);
    }

    public async Task DeleteChecklistItemAsync(Guid cardId, Guid checklistId, Guid itemId, CancellationToken ct = default)
    {
        var response = await _httpClient.DeleteAsync($"api/v1/cards/{cardId}/checklists/{checklistId}/items/{itemId}", ct);
        response.EnsureSuccessStatusCode();
    }

    // ── Attachments ─────────────────────────────────────────

    public async Task<IReadOnlyList<CardAttachmentDto>> ListAttachmentsAsync(Guid cardId, CancellationToken ct = default)
        => await ReadDataAsync<IReadOnlyList<CardAttachmentDto>>($"api/v1/cards/{cardId}/attachments", ct) ?? [];

    public async Task<CardAttachmentDto?> AddAttachmentAsync(Guid cardId, string fileName, string? url, Guid? fileNodeId, CancellationToken ct = default)
    {
        var response = await _httpClient.PostAsJsonAsync($"api/v1/cards/{cardId}/attachments",
            new { FileName = fileName, Url = url, FileNodeId = fileNodeId }, ct);
        response.EnsureSuccessStatusCode();
        return await ReadDataFromResponseAsync<CardAttachmentDto>(response, ct);
    }

    public async Task RemoveAttachmentAsync(Guid cardId, Guid attachmentId, CancellationToken ct = default)
    {
        var response = await _httpClient.DeleteAsync($"api/v1/cards/{cardId}/attachments/{attachmentId}", ct);
        response.EnsureSuccessStatusCode();
    }

    // ── Dependencies ────────────────────────────────────────

    public async Task<IReadOnlyList<CardDependencyDto>> ListDependenciesAsync(Guid cardId, CancellationToken ct = default)
        => await ReadDataAsync<IReadOnlyList<CardDependencyDto>>($"api/v1/cards/{cardId}/dependencies", ct) ?? [];

    public async Task<CardDependencyDto?> AddDependencyAsync(Guid cardId, Guid dependsOnCardId, CardDependencyType type, CancellationToken ct = default)
    {
        var response = await _httpClient.PostAsJsonAsync($"api/v1/cards/{cardId}/dependencies",
            new { DependsOnCardId = dependsOnCardId, Type = type }, ct);
        response.EnsureSuccessStatusCode();
        return await ReadDataFromResponseAsync<CardDependencyDto>(response, ct);
    }

    public async Task RemoveDependencyAsync(Guid cardId, Guid dependsOnCardId, CancellationToken ct = default)
    {
        var response = await _httpClient.DeleteAsync($"api/v1/cards/{cardId}/dependencies/{dependsOnCardId}", ct);
        response.EnsureSuccessStatusCode();
    }

    // ── Sprints ─────────────────────────────────────────────

    public async Task<IReadOnlyList<SprintDto>> ListSprintsAsync(Guid boardId, CancellationToken ct = default)
        => await ReadDataAsync<IReadOnlyList<SprintDto>>($"api/v1/boards/{boardId}/sprints", ct) ?? [];

    public Task<SprintDto?> GetSprintAsync(Guid boardId, Guid sprintId, CancellationToken ct = default)
        => ReadDataAsync<SprintDto>($"api/v1/boards/{boardId}/sprints/{sprintId}", ct);

    public async Task<SprintDto?> CreateSprintAsync(Guid boardId, CreateSprintDto dto, CancellationToken ct = default)
    {
        var response = await _httpClient.PostAsJsonAsync($"api/v1/boards/{boardId}/sprints", dto, ct);
        response.EnsureSuccessStatusCode();
        return await ReadDataFromResponseAsync<SprintDto>(response, ct);
    }

    public async Task<SprintDto?> UpdateSprintAsync(Guid boardId, Guid sprintId, UpdateSprintDto dto, CancellationToken ct = default)
    {
        var response = await _httpClient.PutAsJsonAsync($"api/v1/boards/{boardId}/sprints/{sprintId}", dto, ct);
        response.EnsureSuccessStatusCode();
        return await ReadDataFromResponseAsync<SprintDto>(response, ct);
    }

    public async Task DeleteSprintAsync(Guid boardId, Guid sprintId, CancellationToken ct = default)
    {
        var response = await _httpClient.DeleteAsync($"api/v1/boards/{boardId}/sprints/{sprintId}", ct);
        response.EnsureSuccessStatusCode();
    }

    public async Task<SprintDto?> StartSprintAsync(Guid boardId, Guid sprintId, CancellationToken ct = default)
    {
        var response = await _httpClient.PostAsync($"api/v1/boards/{boardId}/sprints/{sprintId}/start", null, ct);
        response.EnsureSuccessStatusCode();
        return await ReadDataFromResponseAsync<SprintDto>(response, ct);
    }

    public async Task<SprintDto?> CompleteSprintAsync(Guid boardId, Guid sprintId, CancellationToken ct = default)
    {
        var response = await _httpClient.PostAsync($"api/v1/boards/{boardId}/sprints/{sprintId}/complete", null, ct);
        response.EnsureSuccessStatusCode();
        return await ReadDataFromResponseAsync<SprintDto>(response, ct);
    }

    public async Task AddCardToSprintAsync(Guid boardId, Guid sprintId, Guid cardId, CancellationToken ct = default)
    {
        var response = await _httpClient.PostAsync($"api/v1/boards/{boardId}/sprints/{sprintId}/cards/{cardId}", null, ct);
        response.EnsureSuccessStatusCode();
    }

    public async Task RemoveCardFromSprintAsync(Guid boardId, Guid sprintId, Guid cardId, CancellationToken ct = default)
    {
        var response = await _httpClient.DeleteAsync($"api/v1/boards/{boardId}/sprints/{sprintId}/cards/{cardId}", ct);
        response.EnsureSuccessStatusCode();
    }

    // ── Time Entries ────────────────────────────────────────

    public async Task<IReadOnlyList<TimeEntryDto>> ListTimeEntriesAsync(Guid cardId, CancellationToken ct = default)
        => await ReadDataAsync<IReadOnlyList<TimeEntryDto>>($"api/v1/cards/{cardId}/time-entries", ct) ?? [];

    public async Task<TimeEntryDto?> CreateTimeEntryAsync(Guid cardId, CreateTimeEntryDto dto, CancellationToken ct = default)
    {
        var response = await _httpClient.PostAsJsonAsync($"api/v1/cards/{cardId}/time-entries", dto, ct);
        response.EnsureSuccessStatusCode();
        return await ReadDataFromResponseAsync<TimeEntryDto>(response, ct);
    }

    public async Task DeleteTimeEntryAsync(Guid cardId, Guid entryId, CancellationToken ct = default)
    {
        var response = await _httpClient.DeleteAsync($"api/v1/cards/{cardId}/time-entries/{entryId}", ct);
        response.EnsureSuccessStatusCode();
    }

    public async Task<TimeEntryDto?> StartTimerAsync(Guid cardId, CancellationToken ct = default)
    {
        var response = await _httpClient.PostAsync($"api/v1/cards/{cardId}/timer/start", null, ct);
        response.EnsureSuccessStatusCode();
        return await ReadDataFromResponseAsync<TimeEntryDto>(response, ct);
    }

    public async Task<TimeEntryDto?> StopTimerAsync(Guid cardId, CancellationToken ct = default)
    {
        var response = await _httpClient.PostAsync($"api/v1/cards/{cardId}/timer/stop", null, ct);
        response.EnsureSuccessStatusCode();
        return await ReadDataFromResponseAsync<TimeEntryDto>(response, ct);
    }

    // ── Activity ────────────────────────────────────────────

    public async Task<IReadOnlyList<BoardActivityDto>> GetBoardActivityAsync(Guid boardId, int skip = 0, int take = 50, CancellationToken ct = default)
        => await ReadDataAsync<IReadOnlyList<BoardActivityDto>>($"api/v1/boards/{boardId}/activity?skip={skip}&take={take}", ct) ?? [];

    public async Task<IReadOnlyList<BoardActivityDto>> GetCardActivityAsync(Guid cardId, int skip = 0, int take = 50, CancellationToken ct = default)
        => await ReadDataAsync<IReadOnlyList<BoardActivityDto>>($"api/v1/cards/{cardId}/activity?skip={skip}&take={take}", ct) ?? [];

    // ── Teams ───────────────────────────────────────────────

    public async Task<IReadOnlyList<TracksTeamDto>> ListTeamsAsync(CancellationToken ct = default)
        => await ReadDataAsync<IReadOnlyList<TracksTeamDto>>("api/v1/teams", ct) ?? [];

    public Task<TracksTeamDto?> GetTeamAsync(Guid teamId, CancellationToken ct = default)
        => ReadDataAsync<TracksTeamDto>($"api/v1/teams/{teamId}", ct);

    public async Task<TracksTeamDto?> CreateTeamAsync(CreateTracksTeamDto dto, CancellationToken ct = default)
    {
        var response = await _httpClient.PostAsJsonAsync("api/v1/teams", dto, ct);
        response.EnsureSuccessStatusCode();
        return await ReadDataFromResponseAsync<TracksTeamDto>(response, ct);
    }

    public async Task<TracksTeamDto?> UpdateTeamAsync(Guid teamId, UpdateTracksTeamDto dto, CancellationToken ct = default)
    {
        var response = await _httpClient.PutAsJsonAsync($"api/v1/teams/{teamId}", dto, ct);
        response.EnsureSuccessStatusCode();
        return await ReadDataFromResponseAsync<TracksTeamDto>(response, ct);
    }

    public async Task DeleteTeamAsync(Guid teamId, bool cascade = false, CancellationToken ct = default)
    {
        var url = $"api/v1/teams/{teamId}";
        if (cascade) url += "?cascade=true";
        var response = await _httpClient.DeleteAsync(url, ct);
        response.EnsureSuccessStatusCode();
    }

    public async Task<IReadOnlyList<TracksTeamMemberDto>> ListTeamMembersAsync(Guid teamId, CancellationToken ct = default)
        => await ReadDataAsync<IReadOnlyList<TracksTeamMemberDto>>($"api/v1/teams/{teamId}/members", ct) ?? [];

    public async Task AddTeamMemberAsync(Guid teamId, Guid userId, TracksTeamMemberRole role, CancellationToken ct = default)
    {
        var response = await _httpClient.PostAsJsonAsync($"api/v1/teams/{teamId}/members", new { UserId = userId, Role = role }, ct);
        response.EnsureSuccessStatusCode();
    }

    public async Task RemoveTeamMemberAsync(Guid teamId, Guid userId, CancellationToken ct = default)
    {
        var response = await _httpClient.DeleteAsync($"api/v1/teams/{teamId}/members/{userId}", ct);
        response.EnsureSuccessStatusCode();
    }

    public async Task UpdateTeamMemberRoleAsync(Guid teamId, Guid userId, TracksTeamMemberRole role, CancellationToken ct = default)
    {
        var response = await _httpClient.PutAsJsonAsync($"api/v1/teams/{teamId}/members/{userId}/role", new { Role = role }, ct);
        response.EnsureSuccessStatusCode();
    }

    public async Task<IReadOnlyList<BoardDto>> ListTeamBoardsAsync(Guid teamId, bool includeArchived = false, CancellationToken ct = default)
    {
        var url = $"api/v1/teams/{teamId}/boards";
        if (includeArchived) url += "?includeArchived=true";
        return await ReadDataAsync<IReadOnlyList<BoardDto>>(url, ct) ?? [];
    }

    public async Task TransferBoardAsync(Guid boardId, Guid? teamId, CancellationToken ct = default)
    {
        var response = await _httpClient.PostAsJsonAsync($"api/v1/boards/{boardId}/transfer", new TransferBoardDto { TeamId = teamId }, ct);
        response.EnsureSuccessStatusCode();
    }

    // ── User Search ─────────────────────────────────────────

    public async Task<IReadOnlyList<UserSearchResultDto>> SearchUsersAsync(string searchTerm, CancellationToken ct = default)
        => await ReadDataAsync<IReadOnlyList<UserSearchResultDto>>($"api/v1/teams/users/search?q={Uri.EscapeDataString(searchTerm)}", ct) ?? [];

    // ── Helpers ─────────────────────────────────────────────

    private async Task<T?> ReadDataAsync<T>(string url, CancellationToken ct)
    {
        var response = await _httpClient.GetAsync(url, ct);
        response.EnsureSuccessStatusCode();
        return await ReadDataFromResponseAsync<T>(response, ct);
    }

    private static async Task<T?> ReadDataFromResponseAsync<T>(HttpResponseMessage response, CancellationToken ct)
    {
        var json = await response.Content.ReadAsStringAsync(ct);
        using var document = JsonDocument.Parse(json);

        if (!TryGetPropertyIgnoreCase(document.RootElement, "data", out var dataElement))
        {
            return default;
        }

        // Handle double-wrapped envelope { data: { data: ... } }
        if (dataElement.ValueKind == JsonValueKind.Object && TryGetPropertyIgnoreCase(dataElement, "data", out var nestedData))
        {
            dataElement = nestedData;
        }

        return JsonSerializer.Deserialize<T>(dataElement.GetRawText(), JsonOptions);
    }

    private static bool TryGetPropertyIgnoreCase(JsonElement element, string propertyName, out JsonElement value)
    {
        if (element.ValueKind != JsonValueKind.Object)
        {
            value = default;
            return false;
        }

        foreach (var property in element.EnumerateObject())
        {
            if (string.Equals(property.Name, propertyName, StringComparison.OrdinalIgnoreCase))
            {
                value = property.Value;
                return true;
            }
        }

        value = default;
        return false;
    }
}
