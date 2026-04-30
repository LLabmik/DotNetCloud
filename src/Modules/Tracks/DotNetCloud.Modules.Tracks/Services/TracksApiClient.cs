using System.Net.Http.Json;
using System.Text.Json;
using DotNetCloud.Core.Capabilities;
using DotNetCloud.Core.DTOs;
using DotNetCloud.Modules.Tracks.Models;

namespace DotNetCloud.Modules.Tracks.Services;

public sealed class TracksApiClient : ITracksApiClient
{
    private readonly HttpClient _httpClient;
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public TracksApiClient(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    // ── Products ─────────────────────────────────────────────

    public async Task<IReadOnlyList<ProductDto>> ListProductsAsync(Guid organizationId, CancellationToken ct = default)
        => await ReadDataAsync<IReadOnlyList<ProductDto>>($"api/v1/organizations/{organizationId}/products", ct) ?? [];

    public Task<ProductDto?> GetProductAsync(Guid productId, CancellationToken ct = default)
        => ReadDataAsync<ProductDto>($"api/v1/products/{productId}", ct);

    public async Task<ProductDto?> CreateProductAsync(Guid organizationId, CreateProductDto dto, CancellationToken ct = default)
    {
        var response = await _httpClient.PostAsJsonAsync($"api/v1/organizations/{organizationId}/products", dto, ct);
        await EnsureSuccessOrThrowAsync(response);
        return await ReadDataFromResponseAsync<ProductDto>(response, ct);
    }

    public async Task<ProductDto?> UpdateProductAsync(Guid productId, UpdateProductDto dto, CancellationToken ct = default)
    {
        var response = await _httpClient.PutAsJsonAsync($"api/v1/products/{productId}", dto, ct);
        await EnsureSuccessOrThrowAsync(response);
        return await ReadDataFromResponseAsync<ProductDto>(response, ct);
    }

    public async Task DeleteProductAsync(Guid productId, CancellationToken ct = default)
    {
        var response = await _httpClient.DeleteAsync($"api/v1/products/{productId}", ct);
        await EnsureSuccessOrThrowAsync(response);
    }

    public async Task<IReadOnlyList<ProductDto>> ListDeletedProductsAsync(Guid organizationId, CancellationToken ct = default)
        => await ReadDataAsync<IReadOnlyList<ProductDto>>($"api/v1/organizations/{organizationId}/products/deleted", ct) ?? [];

    public async Task<ProductDto?> RestoreProductAsync(Guid productId, CancellationToken ct = default)
    {
        var response = await _httpClient.PostAsync($"api/v1/products/{productId}/restore", null, ct);
        await EnsureSuccessOrThrowAsync(response);
        return await ReadDataAsync<ProductDto>($"api/v1/products/{productId}", ct);
    }

    public async Task PermanentDeleteProductAsync(Guid productId, CancellationToken ct = default)
    {
        var response = await _httpClient.DeleteAsync($"api/v1/products/{productId}/permanent", ct);
        await EnsureSuccessOrThrowAsync(response);
    }

    // ── Product Members ──────────────────────────────────────

    public async Task<IReadOnlyList<ProductMemberDto>> ListProductMembersAsync(Guid productId, CancellationToken ct = default)
        => await ReadDataAsync<IReadOnlyList<ProductMemberDto>>($"api/v1/products/{productId}/members", ct) ?? [];

    public async Task AddProductMemberAsync(Guid productId, AddProductMemberDto dto, CancellationToken ct = default)
    {
        var response = await _httpClient.PostAsJsonAsync($"api/v1/products/{productId}/members", dto, ct);
        await EnsureSuccessOrThrowAsync(response);
    }

    public async Task RemoveProductMemberAsync(Guid productId, Guid userId, CancellationToken ct = default)
    {
        var response = await _httpClient.DeleteAsync($"api/v1/products/{productId}/members/{userId}", ct);
        await EnsureSuccessOrThrowAsync(response);
    }

    public async Task UpdateProductMemberRoleAsync(Guid productId, Guid userId, ProductMemberRole role, CancellationToken ct = default)
    {
        var response = await _httpClient.PutAsJsonAsync($"api/v1/products/{productId}/members/{userId}/role", new { Role = role }, ct);
        await EnsureSuccessOrThrowAsync(response);
    }

    // ── Labels ───────────────────────────────────────────────

    public async Task<IReadOnlyList<LabelDto>> ListLabelsAsync(Guid productId, CancellationToken ct = default)
        => await ReadDataAsync<IReadOnlyList<LabelDto>>($"api/v1/products/{productId}/labels", ct) ?? [];

    public async Task<LabelDto?> CreateLabelAsync(Guid productId, CreateLabelDto dto, CancellationToken ct = default)
    {
        var response = await _httpClient.PostAsJsonAsync($"api/v1/products/{productId}/labels", dto, ct);
        await EnsureSuccessOrThrowAsync(response);
        return await ReadDataFromResponseAsync<LabelDto>(response, ct);
    }

    public async Task<LabelDto?> UpdateLabelAsync(Guid productId, Guid labelId, UpdateLabelDto dto, CancellationToken ct = default)
    {
        var response = await _httpClient.PutAsJsonAsync($"api/v1/products/{productId}/labels/{labelId}", dto, ct);
        await EnsureSuccessOrThrowAsync(response);
        return await ReadDataFromResponseAsync<LabelDto>(response, ct);
    }

    public async Task DeleteLabelAsync(Guid productId, Guid labelId, CancellationToken ct = default)
    {
        var response = await _httpClient.DeleteAsync($"api/v1/products/{productId}/labels/{labelId}", ct);
        await EnsureSuccessOrThrowAsync(response);
    }

    // ── Swimlanes ────────────────────────────────────────────

    public async Task<IReadOnlyList<SwimlaneDto>> ListProductSwimlanesAsync(Guid productId, CancellationToken ct = default)
        => await ReadDataAsync<IReadOnlyList<SwimlaneDto>>($"api/v1/products/{productId}/swimlanes", ct) ?? [];

    public async Task<SwimlaneDto?> CreateProductSwimlaneAsync(Guid productId, CreateSwimlaneDto dto, CancellationToken ct = default)
    {
        var response = await _httpClient.PostAsJsonAsync($"api/v1/products/{productId}/swimlanes", dto, ct);
        await EnsureSuccessOrThrowAsync(response);
        return await ReadDataFromResponseAsync<SwimlaneDto>(response, ct);
    }

    public async Task<IReadOnlyList<SwimlaneDto>> ListWorkItemSwimlanesAsync(Guid workItemId, CancellationToken ct = default)
        => await ReadDataAsync<IReadOnlyList<SwimlaneDto>>($"api/v1/workitems/{workItemId}/swimlanes", ct) ?? [];

    public async Task<SwimlaneDto?> CreateWorkItemSwimlaneAsync(Guid workItemId, CreateSwimlaneDto dto, CancellationToken ct = default)
    {
        var response = await _httpClient.PostAsJsonAsync($"api/v1/workitems/{workItemId}/swimlanes", dto, ct);
        await EnsureSuccessOrThrowAsync(response);
        return await ReadDataFromResponseAsync<SwimlaneDto>(response, ct);
    }

    public async Task<SwimlaneDto?> UpdateSwimlaneAsync(Guid swimlaneId, UpdateSwimlaneDto dto, CancellationToken ct = default)
    {
        var response = await _httpClient.PutAsJsonAsync($"api/v1/swimlanes/{swimlaneId}", dto, ct);
        await EnsureSuccessOrThrowAsync(response);
        return await ReadDataFromResponseAsync<SwimlaneDto>(response, ct);
    }

    public async Task DeleteSwimlaneAsync(Guid swimlaneId, CancellationToken ct = default)
    {
        var response = await _httpClient.DeleteAsync($"api/v1/swimlanes/{swimlaneId}", ct);
        await EnsureSuccessOrThrowAsync(response);
    }

    public async Task ReorderSwimlanesAsync(IReadOnlyList<Guid> swimlaneIds, CancellationToken ct = default)
    {
        var response = await _httpClient.PutAsJsonAsync("api/v1/swimlanes/reorder", new { OrderedIds = swimlaneIds }, ct);
        await EnsureSuccessOrThrowAsync(response);
    }

    // ── Swimlane Transition Rules ────────────────────────────

    public async Task<IReadOnlyList<SwimlaneTransitionRuleDto>> GetSwimlaneTransitionMatrixAsync(Guid productId, CancellationToken ct = default)
        => await ReadDataAsync<IReadOnlyList<SwimlaneTransitionRuleDto>>($"api/v1/products/{productId}/swimlane-transitions", ct) ?? [];

    public async Task<IReadOnlyList<SwimlaneTransitionRuleDto>> SetSwimlaneTransitionMatrixAsync(Guid productId, List<SetTransitionRuleDto> rules, CancellationToken ct = default)
    {
        var response = await _httpClient.PutAsJsonAsync($"api/v1/products/{productId}/swimlane-transitions", rules, ct);
        await EnsureSuccessOrThrowAsync(response);
        return await ReadDataFromResponseAsync<IReadOnlyList<SwimlaneTransitionRuleDto>>(response, ct) ?? [];
    }

    // ── Work Items ───────────────────────────────────────────

    public async Task<IReadOnlyList<WorkItemDto>> ListWorkItemsAsync(Guid swimlaneId, CancellationToken ct = default)
        => await ReadDataAsync<IReadOnlyList<WorkItemDto>>($"api/v1/swimlanes/{swimlaneId}/items", ct) ?? [];

    public Task<WorkItemDto?> GetWorkItemAsync(Guid workItemId, CancellationToken ct = default)
        => ReadDataAsync<WorkItemDto>($"api/v1/workitems/{workItemId}", ct);

    public Task<WorkItemDto?> GetWorkItemByNumberAsync(Guid productId, int itemNumber, CancellationToken ct = default)
        => ReadDataAsync<WorkItemDto>($"api/v1/workitems/by-number/{productId}/{itemNumber}", ct);

    public async Task<WorkItemDto?> CreateEpicAsync(Guid swimlaneId, CreateWorkItemDto dto, CancellationToken ct = default)
    {
        var response = await _httpClient.PostAsJsonAsync($"api/v1/swimlanes/{swimlaneId}/epics", dto, ct);
        await EnsureSuccessOrThrowAsync(response);
        return await ReadDataFromResponseAsync<WorkItemDto>(response, ct);
    }

    public async Task<WorkItemDto?> CreateFeatureAsync(Guid swimlaneId, CreateWorkItemDto dto, CancellationToken ct = default)
    {
        var response = await _httpClient.PostAsJsonAsync($"api/v1/swimlanes/{swimlaneId}/features", dto, ct);
        await EnsureSuccessOrThrowAsync(response);
        return await ReadDataFromResponseAsync<WorkItemDto>(response, ct);
    }

    public async Task<WorkItemDto?> CreateItemAsync(Guid swimlaneId, CreateWorkItemDto dto, CancellationToken ct = default)
    {
        var response = await _httpClient.PostAsJsonAsync($"api/v1/swimlanes/{swimlaneId}/items", dto, ct);
        await EnsureSuccessOrThrowAsync(response);
        return await ReadDataFromResponseAsync<WorkItemDto>(response, ct);
    }

    public async Task<WorkItemDto?> CreateSubItemAsync(Guid parentItemId, CreateWorkItemDto dto, CancellationToken ct = default)
    {
        var response = await _httpClient.PostAsJsonAsync($"api/v1/workitems/{parentItemId}/subitems", dto, ct);
        await EnsureSuccessOrThrowAsync(response);
        return await ReadDataFromResponseAsync<WorkItemDto>(response, ct);
    }

    public async Task<WorkItemDto?> UpdateWorkItemAsync(Guid workItemId, UpdateWorkItemDto dto, CancellationToken ct = default)
    {
        var response = await _httpClient.PutAsJsonAsync($"api/v1/workitems/{workItemId}", dto, ct);
        await EnsureSuccessOrThrowAsync(response);
        return await ReadDataFromResponseAsync<WorkItemDto>(response, ct);
    }

    public async Task DeleteWorkItemAsync(Guid workItemId, CancellationToken ct = default)
    {
        var response = await _httpClient.DeleteAsync($"api/v1/workitems/{workItemId}", ct);
        await EnsureSuccessOrThrowAsync(response);
    }

    public async Task<WorkItemDto?> MoveWorkItemAsync(Guid workItemId, MoveWorkItemDto dto, CancellationToken ct = default)
    {
        var response = await _httpClient.PutAsJsonAsync($"api/v1/workitems/{workItemId}/move", dto, ct);
        await EnsureSuccessOrThrowAsync(response);
        return await ReadDataFromResponseAsync<WorkItemDto>(response, ct);
    }

    public async Task<IReadOnlyList<WorkItemDto>> GetChildWorkItemsAsync(Guid parentWorkItemId, CancellationToken ct = default)
        => await ReadDataAsync<IReadOnlyList<WorkItemDto>>($"api/v1/workitems/{parentWorkItemId}/children", ct) ?? [];

    // ── Export ───────────────────────────────────────────────

    public async Task<byte[]> ExportWorkItemsCsvAsync(Guid productId, Guid? swimlaneId = null, Guid? labelId = null, Priority? priority = null, CancellationToken ct = default)
    {
        var queryParams = new List<string>();
        if (swimlaneId.HasValue) queryParams.Add($"swimlaneId={swimlaneId.Value}");
        if (labelId.HasValue) queryParams.Add($"labelId={labelId.Value}");
        if (priority.HasValue) queryParams.Add($"priority={priority.Value}");

        var queryString = queryParams.Count > 0 ? "?" + string.Join("&", queryParams) : "";
        var response = await _httpClient.GetAsync($"api/v1/products/{productId}/work-items/export{queryString}", ct);
        await EnsureSuccessOrThrowAsync(response);
        return await response.Content.ReadAsByteArrayAsync(ct);
    }

    // ── Watchers ───────────────────────────────────────────

    public async Task<IReadOnlyList<Guid>> GetWatchersAsync(Guid workItemId, CancellationToken ct = default)
    {
        var result = await ReadDataAsync<List<WatcherDto>>($"api/v1/workitems/{workItemId}/watchers", ct);
        return result?.Select(w => w.UserId).ToList() ?? [];
    }

    public async Task<int> WatchWorkItemAsync(Guid workItemId, CancellationToken ct = default)
    {
        var response = await _httpClient.PostAsync($"api/v1/workitems/{workItemId}/watch", null, ct);
        await EnsureSuccessOrThrowAsync(response);
        var result = await ReadDataFromResponseAsync<WatchResultDto>(response, ct);
        return result?.WatcherCount ?? 0;
    }

    public async Task<int> UnwatchWorkItemAsync(Guid workItemId, CancellationToken ct = default)
    {
        var response = await _httpClient.DeleteAsync($"api/v1/workitems/{workItemId}/watch", ct);
        await EnsureSuccessOrThrowAsync(response);
        var result = await ReadDataFromResponseAsync<WatchResultDto>(response, ct);
        return result?.WatcherCount ?? 0;
    }

    // ── Work Item Assignments ────────────────────────────────

    public async Task AssignUserAsync(Guid workItemId, Guid userId, CancellationToken ct = default)
    {
        var response = await _httpClient.PostAsJsonAsync($"api/v1/workitems/{workItemId}/assignments", new { UserId = userId }, ct);
        await EnsureSuccessOrThrowAsync(response);
    }

    public async Task UnassignUserAsync(Guid workItemId, Guid userId, CancellationToken ct = default)
    {
        var response = await _httpClient.DeleteAsync($"api/v1/workitems/{workItemId}/assignments/{userId}", ct);
        await EnsureSuccessOrThrowAsync(response);
    }

    // ── Work Item Labels ─────────────────────────────────────

    public async Task AddLabelToWorkItemAsync(Guid workItemId, Guid labelId, CancellationToken ct = default)
    {
        var response = await _httpClient.PostAsync($"api/v1/workitems/{workItemId}/labels/{labelId}", null, ct);
        await EnsureSuccessOrThrowAsync(response);
    }

    public async Task RemoveLabelFromWorkItemAsync(Guid workItemId, Guid labelId, CancellationToken ct = default)
    {
        var response = await _httpClient.DeleteAsync($"api/v1/workitems/{workItemId}/labels/{labelId}", ct);
        await EnsureSuccessOrThrowAsync(response);
    }

    // ── Comments ─────────────────────────────────────────────

    public async Task<IReadOnlyList<WorkItemCommentDto>> ListCommentsAsync(Guid workItemId, int skip = 0, int take = 50, CancellationToken ct = default)
        => await ReadDataAsync<IReadOnlyList<WorkItemCommentDto>>($"api/v1/workitems/{workItemId}/comments?skip={skip}&take={take}", ct) ?? [];

    public async Task<WorkItemCommentDto?> CreateCommentAsync(Guid workItemId, string content, CancellationToken ct = default)
    {
        var response = await _httpClient.PostAsJsonAsync($"api/v1/workitems/{workItemId}/comments", new { Content = content }, ct);
        await EnsureSuccessOrThrowAsync(response);
        return await ReadDataFromResponseAsync<WorkItemCommentDto>(response, ct);
    }

    public async Task<WorkItemCommentDto?> UpdateCommentAsync(Guid workItemId, Guid commentId, string content, CancellationToken ct = default)
    {
        var response = await _httpClient.PutAsJsonAsync($"api/v1/workitems/{workItemId}/comments/{commentId}", new { Content = content }, ct);
        await EnsureSuccessOrThrowAsync(response);
        return await ReadDataFromResponseAsync<WorkItemCommentDto>(response, ct);
    }

    public async Task DeleteCommentAsync(Guid workItemId, Guid commentId, CancellationToken ct = default)
    {
        var response = await _httpClient.DeleteAsync($"api/v1/workitems/{workItemId}/comments/{commentId}", ct);
        await EnsureSuccessOrThrowAsync(response);
    }

    // ── Checklists ───────────────────────────────────────────

    public async Task<IReadOnlyList<ChecklistDto>> ListChecklistsAsync(Guid itemId, CancellationToken ct = default)
        => await ReadDataAsync<IReadOnlyList<ChecklistDto>>($"api/v1/workitems/{itemId}/checklists", ct) ?? [];

    public async Task<ChecklistDto?> CreateChecklistAsync(Guid itemId, string title, CancellationToken ct = default)
    {
        var response = await _httpClient.PostAsJsonAsync($"api/v1/workitems/{itemId}/checklists", new { Title = title }, ct);
        await EnsureSuccessOrThrowAsync(response);
        return await ReadDataFromResponseAsync<ChecklistDto>(response, ct);
    }

    public async Task DeleteChecklistAsync(Guid itemId, Guid checklistId, CancellationToken ct = default)
    {
        var response = await _httpClient.DeleteAsync($"api/v1/workitems/{itemId}/checklists/{checklistId}", ct);
        await EnsureSuccessOrThrowAsync(response);
    }

    public async Task<ChecklistItemDto?> AddChecklistItemAsync(Guid itemId, Guid checklistId, string title, CancellationToken ct = default)
    {
        var response = await _httpClient.PostAsJsonAsync($"api/v1/workitems/{itemId}/checklists/{checklistId}/items", new { Title = title }, ct);
        await EnsureSuccessOrThrowAsync(response);
        return await ReadDataFromResponseAsync<ChecklistItemDto>(response, ct);
    }

    public async Task<ChecklistItemDto?> ToggleChecklistItemAsync(Guid itemId, Guid checklistId, Guid checklistItemId, CancellationToken ct = default)
    {
        var response = await _httpClient.PutAsync($"api/v1/workitems/{itemId}/checklists/{checklistId}/items/{checklistItemId}/toggle", null, ct);
        await EnsureSuccessOrThrowAsync(response);
        return await ReadDataFromResponseAsync<ChecklistItemDto>(response, ct);
    }

    public async Task DeleteChecklistItemAsync(Guid itemId, Guid checklistId, Guid checklistItemId, CancellationToken ct = default)
    {
        var response = await _httpClient.DeleteAsync($"api/v1/workitems/{itemId}/checklists/{checklistId}/items/{checklistItemId}", ct);
        await EnsureSuccessOrThrowAsync(response);
    }

    // ── Attachments ──────────────────────────────────────────

    public async Task<IReadOnlyList<WorkItemAttachmentDto>> ListAttachmentsAsync(Guid workItemId, CancellationToken ct = default)
        => await ReadDataAsync<IReadOnlyList<WorkItemAttachmentDto>>($"api/v1/workitems/{workItemId}/attachments", ct) ?? [];

    public async Task<WorkItemAttachmentDto?> AddAttachmentAsync(Guid workItemId, string fileName, string? url, Guid? fileNodeId, CancellationToken ct = default)
    {
        var response = await _httpClient.PostAsJsonAsync($"api/v1/workitems/{workItemId}/attachments",
            new { FileName = fileName, Url = url, FileNodeId = fileNodeId }, ct);
        await EnsureSuccessOrThrowAsync(response);
        return await ReadDataFromResponseAsync<WorkItemAttachmentDto>(response, ct);
    }

    public async Task RemoveAttachmentAsync(Guid workItemId, Guid attachmentId, CancellationToken ct = default)
    {
        var response = await _httpClient.DeleteAsync($"api/v1/workitems/{workItemId}/attachments/{attachmentId}", ct);
        await EnsureSuccessOrThrowAsync(response);
    }

    // ── Dependencies ─────────────────────────────────────────

    public async Task<IReadOnlyList<WorkItemDependencyDto>> ListDependenciesAsync(Guid workItemId, CancellationToken ct = default)
        => await ReadDataAsync<IReadOnlyList<WorkItemDependencyDto>>($"api/v1/workitems/{workItemId}/dependencies", ct) ?? [];

    public async Task<WorkItemDependencyDto?> AddDependencyAsync(Guid workItemId, AddWorkItemDependencyDto dto, CancellationToken ct = default)
    {
        var response = await _httpClient.PostAsJsonAsync($"api/v1/workitems/{workItemId}/dependencies", dto, ct);
        await EnsureSuccessOrThrowAsync(response);
        return await ReadDataFromResponseAsync<WorkItemDependencyDto>(response, ct);
    }

    public async Task RemoveDependencyAsync(Guid workItemId, Guid dependencyId, CancellationToken ct = default)
    {
        var response = await _httpClient.DeleteAsync($"api/v1/workitems/{workItemId}/dependencies/{dependencyId}", ct);
        await EnsureSuccessOrThrowAsync(response);
    }

    // ── Sprints ──────────────────────────────────────────────

    public async Task<IReadOnlyList<SprintDto>> ListSprintsAsync(Guid epicId, CancellationToken ct = default)
        => await ReadDataAsync<IReadOnlyList<SprintDto>>($"api/v1/workitems/{epicId}/sprints", ct) ?? [];

    public Task<SprintDto?> GetSprintAsync(Guid sprintId, CancellationToken ct = default)
        => ReadDataAsync<SprintDto>($"api/v1/sprints/{sprintId}", ct);

    public async Task<SprintDto?> CreateSprintAsync(Guid epicId, CreateSprintDto dto, CancellationToken ct = default)
    {
        var response = await _httpClient.PostAsJsonAsync($"api/v1/workitems/{epicId}/sprints", dto, ct);
        await EnsureSuccessOrThrowAsync(response);
        return await ReadDataFromResponseAsync<SprintDto>(response, ct);
    }

    public async Task<SprintDto?> UpdateSprintAsync(Guid sprintId, UpdateSprintDto dto, CancellationToken ct = default)
    {
        var response = await _httpClient.PutAsJsonAsync($"api/v1/sprints/{sprintId}", dto, ct);
        await EnsureSuccessOrThrowAsync(response);
        return await ReadDataFromResponseAsync<SprintDto>(response, ct);
    }

    public async Task DeleteSprintAsync(Guid sprintId, CancellationToken ct = default)
    {
        var response = await _httpClient.DeleteAsync($"api/v1/sprints/{sprintId}", ct);
        await EnsureSuccessOrThrowAsync(response);
    }

    public async Task<SprintDto?> StartSprintAsync(Guid sprintId, CancellationToken ct = default)
    {
        var response = await _httpClient.PostAsync($"api/v1/sprints/{sprintId}/start", null, ct);
        await EnsureSuccessOrThrowAsync(response);
        return await ReadDataFromResponseAsync<SprintDto>(response, ct);
    }

    public async Task<SprintDto?> CompleteSprintAsync(Guid sprintId, CancellationToken ct = default)
    {
        var response = await _httpClient.PostAsync($"api/v1/sprints/{sprintId}/complete", null, ct);
        await EnsureSuccessOrThrowAsync(response);
        return await ReadDataFromResponseAsync<SprintDto>(response, ct);
    }

    public async Task AddItemToSprintAsync(Guid sprintId, Guid itemId, CancellationToken ct = default)
    {
        var response = await _httpClient.PostAsync($"api/v1/sprints/{sprintId}/items/{itemId}", null, ct);
        await EnsureSuccessOrThrowAsync(response);
    }

    public async Task RemoveItemFromSprintAsync(Guid sprintId, Guid itemId, CancellationToken ct = default)
    {
        var response = await _httpClient.DeleteAsync($"api/v1/sprints/{sprintId}/items/{itemId}", ct);
        await EnsureSuccessOrThrowAsync(response);
    }

    public async Task<IReadOnlyList<WorkItemDto>> GetBacklogItemsAsync(Guid epicId, CancellationToken ct = default)
        => await ReadDataAsync<IReadOnlyList<WorkItemDto>>($"api/v1/workitems/{epicId}/backlog", ct) ?? [];

    // ── Sprint Planning ──────────────────────────────────────

    public async Task<IReadOnlyList<SprintDto>> CreateSprintPlanAsync(Guid epicId, CreateSprintPlanDto dto, CancellationToken ct = default)
    {
        var response = await _httpClient.PostAsJsonAsync($"api/v1/workitems/{epicId}/sprint-plan", dto, ct);
        await EnsureSuccessOrThrowAsync(response);
        return await ReadDataAsync<IReadOnlyList<SprintDto>>("", ct) ?? [];
    }

    public async Task<IReadOnlyList<SprintDto>> GetSprintPlanAsync(Guid epicId, CancellationToken ct = default)
        => await ReadDataAsync<IReadOnlyList<SprintDto>>($"api/v1/workitems/{epicId}/sprint-plan", ct) ?? [];

    public async Task<IReadOnlyList<SprintDto>> AdjustSprintDatesAsync(Guid sprintId, AdjustSprintDto dto, CancellationToken ct = default)
    {
        var response = await _httpClient.PutAsJsonAsync($"api/v1/sprints/{sprintId}/adjust", dto, ct);
        await EnsureSuccessOrThrowAsync(response);
        return await ReadDataFromResponseAsync<IReadOnlyList<SprintDto>>(response, ct) ?? [];
    }

    // ── Time Entries ─────────────────────────────────────────

    public async Task<IReadOnlyList<TimeEntryDto>> ListTimeEntriesAsync(Guid workItemId, CancellationToken ct = default)
        => await ReadDataAsync<IReadOnlyList<TimeEntryDto>>($"api/v1/workitems/{workItemId}/time-entries", ct) ?? [];

    public async Task<TimeEntryDto?> CreateTimeEntryAsync(Guid workItemId, CreateTimeEntryDto dto, CancellationToken ct = default)
    {
        var response = await _httpClient.PostAsJsonAsync($"api/v1/workitems/{workItemId}/time-entries", dto, ct);
        await EnsureSuccessOrThrowAsync(response);
        return await ReadDataFromResponseAsync<TimeEntryDto>(response, ct);
    }

    public async Task DeleteTimeEntryAsync(Guid workItemId, Guid entryId, CancellationToken ct = default)
    {
        var response = await _httpClient.DeleteAsync($"api/v1/workitems/{workItemId}/time-entries/{entryId}", ct);
        await EnsureSuccessOrThrowAsync(response);
    }

    public async Task<TimeEntryDto?> StartTimerAsync(Guid workItemId, CancellationToken ct = default)
    {
        var response = await _httpClient.PostAsync($"api/v1/workitems/{workItemId}/timer/start", null, ct);
        await EnsureSuccessOrThrowAsync(response);
        return await ReadDataFromResponseAsync<TimeEntryDto>(response, ct);
    }

    public async Task<TimeEntryDto?> StopTimerAsync(Guid workItemId, CancellationToken ct = default)
    {
        var response = await _httpClient.PostAsync($"api/v1/workitems/{workItemId}/timer/stop", null, ct);
        await EnsureSuccessOrThrowAsync(response);
        return await ReadDataFromResponseAsync<TimeEntryDto>(response, ct);
    }

    // ── Activity ─────────────────────────────────────────────

    public async Task<IReadOnlyList<ActivityDto>> GetProductActivityAsync(Guid productId, int skip = 0, int take = 50, CancellationToken ct = default)
        => await ReadDataAsync<IReadOnlyList<ActivityDto>>($"api/v1/products/{productId}/activity?skip={skip}&take={take}", ct) ?? [];

    public async Task<IReadOnlyList<ActivityDto>> GetWorkItemActivityAsync(Guid workItemId, int skip = 0, int take = 50, CancellationToken ct = default)
        => await ReadDataAsync<IReadOnlyList<ActivityDto>>($"api/v1/workitems/{workItemId}/activity?skip={skip}&take={take}", ct) ?? [];

    // ── Analytics ────────────────────────────────────────────

    public Task<ProductAnalyticsDto?> GetProductAnalyticsAsync(Guid productId, CancellationToken ct = default)
        => ReadDataAsync<ProductAnalyticsDto>($"api/v1/products/{productId}/analytics", ct);

    public async Task<IReadOnlyList<SprintVelocityDto>> GetVelocityDataAsync(Guid productId, CancellationToken ct = default)
        => await ReadDataAsync<IReadOnlyList<SprintVelocityDto>>($"api/v1/products/{productId}/velocity", ct) ?? [];

    public Task<SprintReportDto?> GetSprintReportAsync(Guid sprintId, CancellationToken ct = default)
        => ReadDataAsync<SprintReportDto>($"api/v1/sprints/{sprintId}/report", ct);

    public Task<SprintBurndownDto?> GetBurndownDataAsync(Guid sprintId, CancellationToken ct = default)
        => ReadDataAsync<SprintBurndownDto>($"api/v1/sprints/{sprintId}/burndown", ct);

    // ── Teams ────────────────────────────────────────────────

    public async Task<IReadOnlyList<TracksTeamDto>> ListTeamsAsync(CancellationToken ct = default)
        => await ReadDataAsync<IReadOnlyList<TracksTeamDto>>("api/v1/teams", ct) ?? [];

    public Task<TracksTeamDto?> GetTeamAsync(Guid teamId, CancellationToken ct = default)
        => ReadDataAsync<TracksTeamDto>($"api/v1/teams/{teamId}", ct);

    public async Task<TracksTeamDto?> CreateTeamAsync(CreateTracksTeamDto dto, CancellationToken ct = default)
    {
        var response = await _httpClient.PostAsJsonAsync("api/v1/teams", dto, ct);
        await EnsureSuccessOrThrowAsync(response);
        return await ReadDataFromResponseAsync<TracksTeamDto>(response, ct);
    }

    public async Task<TracksTeamDto?> UpdateTeamAsync(Guid teamId, UpdateTracksTeamDto dto, CancellationToken ct = default)
    {
        var response = await _httpClient.PutAsJsonAsync($"api/v1/teams/{teamId}", dto, ct);
        await EnsureSuccessOrThrowAsync(response);
        return await ReadDataFromResponseAsync<TracksTeamDto>(response, ct);
    }

    public async Task DeleteTeamAsync(Guid teamId, CancellationToken ct = default)
    {
        var response = await _httpClient.DeleteAsync($"api/v1/teams/{teamId}", ct);
        await EnsureSuccessOrThrowAsync(response);
    }

    public async Task<IReadOnlyList<TracksTeamMemberDto>> ListTeamMembersAsync(Guid teamId, CancellationToken ct = default)
        => await ReadDataAsync<IReadOnlyList<TracksTeamMemberDto>>($"api/v1/teams/{teamId}/members", ct) ?? [];

    public async Task AddTeamMemberAsync(Guid teamId, AddTracksTeamMemberDto dto, CancellationToken ct = default)
    {
        var response = await _httpClient.PostAsJsonAsync($"api/v1/teams/{teamId}/members", dto, ct);
        await EnsureSuccessOrThrowAsync(response);
    }

    public async Task RemoveTeamMemberAsync(Guid teamId, Guid userId, CancellationToken ct = default)
    {
        var response = await _httpClient.DeleteAsync($"api/v1/teams/{teamId}/members/{userId}", ct);
        await EnsureSuccessOrThrowAsync(response);
    }

    public async Task UpdateTeamMemberRoleAsync(Guid teamId, Guid userId, TracksTeamMemberRole role, CancellationToken ct = default)
    {
        var response = await _httpClient.PutAsJsonAsync($"api/v1/teams/{teamId}/members/{userId}/role", new { Role = role }, ct);
        await EnsureSuccessOrThrowAsync(response);
    }

    // ── Review Sessions ──────────────────────────────────────

    public async Task<ReviewSessionDto?> StartReviewSessionAsync(Guid epicId, CancellationToken ct = default)
    {
        var response = await _httpClient.PostAsJsonAsync($"api/v1/workitems/{epicId}/reviews", new { }, ct);
        await EnsureSuccessOrThrowAsync(response);
        return await ReadDataFromResponseAsync<ReviewSessionDto>(response, ct);
    }

    public Task<ReviewSessionDto?> GetReviewSessionAsync(Guid sessionId, CancellationToken ct = default)
        => ReadDataAsync<ReviewSessionDto>($"api/v1/reviews/{sessionId}", ct);

    public async Task<ReviewSessionDto?> JoinReviewSessionAsync(Guid sessionId, CancellationToken ct = default)
    {
        var response = await _httpClient.PostAsync($"api/v1/reviews/{sessionId}/join", null, ct);
        await EnsureSuccessOrThrowAsync(response);
        return await ReadDataFromResponseAsync<ReviewSessionDto>(response, ct);
    }

    public async Task LeaveReviewSessionAsync(Guid sessionId, CancellationToken ct = default)
    {
        var response = await _httpClient.PostAsync($"api/v1/reviews/{sessionId}/leave", null, ct);
        await EnsureSuccessOrThrowAsync(response);
    }

    public async Task<ReviewSessionDto?> SetReviewCurrentItemAsync(Guid sessionId, Guid itemId, CancellationToken ct = default)
    {
        var response = await _httpClient.PutAsJsonAsync($"api/v1/reviews/{sessionId}/current-item", new { ItemId = itemId }, ct);
        await EnsureSuccessOrThrowAsync(response);
        return await ReadDataFromResponseAsync<ReviewSessionDto>(response, ct);
    }

    public async Task EndReviewSessionAsync(Guid sessionId, CancellationToken ct = default)
    {
        var response = await _httpClient.PostAsync($"api/v1/reviews/{sessionId}/end", null, ct);
        await EnsureSuccessOrThrowAsync(response);
    }

    // ── Planning Poker ───────────────────────────────────────

    public async Task<PokerSessionDto?> StartPokerSessionAsync(Guid epicId, CreatePokerSessionDto dto, CancellationToken ct = default)
    {
        var response = await _httpClient.PostAsJsonAsync($"api/v1/workitems/{epicId}/poker", dto, ct);
        await EnsureSuccessOrThrowAsync(response);
        return await ReadDataFromResponseAsync<PokerSessionDto>(response, ct);
    }

    public Task<PokerSessionDto?> GetPokerSessionAsync(Guid sessionId, CancellationToken ct = default)
        => ReadDataAsync<PokerSessionDto>($"api/v1/poker/{sessionId}", ct);

    public async Task<PokerSessionDto?> SubmitPokerVoteAsync(Guid sessionId, SubmitPokerVoteDto dto, CancellationToken ct = default)
    {
        var response = await _httpClient.PostAsJsonAsync($"api/v1/poker/{sessionId}/vote", dto, ct);
        await EnsureSuccessOrThrowAsync(response);
        return await ReadDataFromResponseAsync<PokerSessionDto>(response, ct);
    }

    public async Task<PokerSessionDto?> RevealPokerSessionAsync(Guid sessionId, CancellationToken ct = default)
    {
        var response = await _httpClient.PostAsync($"api/v1/poker/{sessionId}/reveal", null, ct);
        await EnsureSuccessOrThrowAsync(response);
        return await ReadDataFromResponseAsync<PokerSessionDto>(response, ct);
    }

    public async Task<PokerSessionDto?> AcceptPokerEstimateAsync(Guid sessionId, string estimate, CancellationToken ct = default)
    {
        var response = await _httpClient.PostAsJsonAsync($"api/v1/poker/{sessionId}/accept", new { Estimate = estimate }, ct);
        await EnsureSuccessOrThrowAsync(response);
        return await ReadDataFromResponseAsync<PokerSessionDto>(response, ct);
    }

    public async Task<IReadOnlyList<PokerVoteStatusDto>> GetPokerVoteStatusAsync(Guid sessionId, CancellationToken ct = default)
        => await ReadDataAsync<IReadOnlyList<PokerVoteStatusDto>>($"api/v1/poker/{sessionId}/vote-status", ct) ?? [];

    // ── User Search ──────────────────────────────────────────

    public async Task<IReadOnlyList<UserSearchResult>> SearchUsersAsync(string searchTerm, int maxResults = 8, CancellationToken ct = default)
        => await ReadDataAsync<IReadOnlyList<UserSearchResult>>($"api/v1/users/search?q={Uri.EscapeDataString(searchTerm)}&max={maxResults}", ct) ?? [];

    // ── Custom Views ────────────────────────────────────────

    public async Task<IReadOnlyList<CustomViewDto>> ListCustomViewsAsync(Guid productId, CancellationToken ct = default)
        => await ReadDataAsync<IReadOnlyList<CustomViewDto>>($"api/v1/products/{productId}/views", ct) ?? [];

    public async Task<CustomViewDto?> CreateCustomViewAsync(Guid productId, string name, string filterJson, string sortJson, string? groupBy, string layout, bool isShared, CancellationToken ct = default)
    {
        var response = await _httpClient.PostAsJsonAsync($"api/v1/products/{productId}/views", new
        {
            Name = name,
            FilterJson = filterJson,
            SortJson = sortJson,
            GroupBy = groupBy,
            Layout = layout,
            IsShared = isShared
        }, ct);
        await EnsureSuccessOrThrowAsync(response);
        return await ReadDataFromResponseAsync<CustomViewDto>(response, ct);
    }

    public async Task<CustomViewDto?> UpdateCustomViewAsync(Guid productId, Guid viewId, string? name, string? filterJson, string? sortJson, string? groupBy, string? layout, bool? isShared, CancellationToken ct = default)
    {
        var response = await _httpClient.PutAsJsonAsync($"api/v1/products/{productId}/views/{viewId}", new
        {
            Name = name,
            FilterJson = filterJson,
            SortJson = sortJson,
            GroupBy = groupBy,
            Layout = layout,
            IsShared = isShared
        }, ct);
        await EnsureSuccessOrThrowAsync(response);
        return await ReadDataFromResponseAsync<CustomViewDto>(response, ct);
    }

    public async Task DeleteCustomViewAsync(Guid productId, Guid viewId, CancellationToken ct = default)
    {
        var response = await _httpClient.DeleteAsync($"api/v1/products/{productId}/views/{viewId}", ct);
        await EnsureSuccessOrThrowAsync(response);
    }

    // ── Dashboard ──────────────────────────────────────────

    public Task<ProductDashboardDto?> GetProductDashboardAsync(Guid productId, CancellationToken ct = default)
        => ReadDataAsync<ProductDashboardDto>($"api/v1/products/{productId}/dashboard", ct);

    // ── Bulk Actions ───────────────────────────────────────

    public async Task<IReadOnlyList<WorkItemDto>> ListProductWorkItemsAsync(
        Guid productId, Guid? swimlaneId = null, Guid? labelId = null, Priority? priority = null, CancellationToken ct = default)
    {
        var queryParams = new List<string>();
        if (swimlaneId.HasValue) queryParams.Add($"swimlaneId={swimlaneId.Value}");
        if (labelId.HasValue) queryParams.Add($"labelId={labelId.Value}");
        if (priority.HasValue) queryParams.Add($"priority={priority.Value}");
        var queryString = queryParams.Count > 0 ? "?" + string.Join("&", queryParams) : "";
        return await ReadDataAsync<IReadOnlyList<WorkItemDto>>($"api/v1/products/{productId}/work-items{queryString}", ct) ?? [];
    }

    public async Task<int> BulkWorkItemActionAsync(Guid productId, BulkWorkItemActionDto dto, CancellationToken ct = default)
    {
        var response = await _httpClient.PostAsJsonAsync($"api/v1/products/{productId}/work-items/bulk", dto, ct);
        await EnsureSuccessOrThrowAsync(response);
        var result = await ReadDataFromResponseAsync<BulkActionResultDto>(response, ct);
        return result?.Affected ?? 0;
    }

    // ── Webhooks ─────────────────────────────────────────────

    public async Task<IReadOnlyList<WebhookSubscription>> ListProductWebhooksAsync(Guid productId, CancellationToken ct = default)
        => await ReadDataAsync<IReadOnlyList<WebhookSubscription>>($"api/v1/products/{productId}/webhooks", ct) ?? [];

    public async Task<WebhookSubscription?> CreateProductWebhookAsync(Guid productId, string url, List<string> eventTypes, CancellationToken ct = default)
    {
        var response = await _httpClient.PostAsJsonAsync($"api/v1/products/{productId}/webhooks", new { url, eventTypes }, ct);
        await EnsureSuccessOrThrowAsync(response);
        return await ReadDataFromResponseAsync<WebhookSubscription>(response, ct);
    }

    public async Task<WebhookSubscription?> UpdateProductWebhookAsync(Guid productId, Guid subscriptionId, string url, List<string> eventTypes, bool isActive, CancellationToken ct = default)
    {
        var response = await _httpClient.PutAsJsonAsync($"api/v1/products/{productId}/webhooks/{subscriptionId}", new { url, eventTypes, isActive }, ct);
        await EnsureSuccessOrThrowAsync(response);
        return await ReadDataFromResponseAsync<WebhookSubscription>(response, ct);
    }

    public async Task DeleteProductWebhookAsync(Guid productId, Guid subscriptionId, CancellationToken ct = default)
    {
        var response = await _httpClient.DeleteAsync($"api/v1/products/{productId}/webhooks/{subscriptionId}", ct);
        await EnsureSuccessOrThrowAsync(response);
    }

    public async Task<WebhookTestResult> TestProductWebhookAsync(Guid productId, Guid subscriptionId, CancellationToken ct = default)
    {
        var response = await _httpClient.PostAsync($"api/v1/products/{productId}/webhooks/{subscriptionId}/test", null, ct);
        await EnsureSuccessOrThrowAsync(response);

        var json = await response.Content.ReadAsStringAsync(ct);
        using var doc = JsonDocument.Parse(json);

        if (TryGetPropertyIgnoreCase(doc.RootElement, "data", out var data))
        {
            var success = TryGetPropertyIgnoreCase(data, "success", out var s) && s.GetBoolean();
            var statusCode = TryGetPropertyIgnoreCase(data, "statusCode", out var sc) ? sc.GetInt32() : (int?)null;
            var durationMs = TryGetPropertyIgnoreCase(data, "durationMs", out var d) ? d.GetInt64() : 0;
            var error = TryGetPropertyIgnoreCase(data, "error", out var e) ? e.GetString() : null;
            return new WebhookTestResult(success, statusCode, durationMs, error);
        }

        return new WebhookTestResult(false, null, 0, "Unable to parse response");
    }

    // ── Roadmap ──────────────────────────────────────────────

    public async Task<RoadmapDataDto?> GetRoadmapDataAsync(Guid productId, CancellationToken ct = default)
        => await ReadDataAsync<RoadmapDataDto>($"api/v1/products/{productId}/roadmap", ct);

    // ── Automation Rules ─────────────────────────────────────

    public async Task<List<AutomationRuleDto>> ListAutomationRulesAsync(Guid productId, CancellationToken ct = default)
    {
        var result = await ReadDataAsync<List<AutomationRuleDto>>($"api/v1/products/{productId}/automation-rules", ct);
        return result ?? new List<AutomationRuleDto>();
    }

    public async Task<AutomationRuleDto?> CreateAutomationRuleAsync(Guid productId, CreateAutomationRuleDto dto, CancellationToken ct = default)
    {
        var response = await _httpClient.PostAsJsonAsync($"api/v1/products/{productId}/automation-rules", dto, ct);
        await EnsureSuccessOrThrowAsync(response);
        return await ReadDataFromResponseAsync<AutomationRuleDto>(response, ct);
    }

    public async Task<AutomationRuleDto?> UpdateAutomationRuleAsync(Guid ruleId, UpdateAutomationRuleDto dto, CancellationToken ct = default)
    {
        var response = await _httpClient.PutAsJsonAsync($"api/v1/automation-rules/{ruleId}", dto, ct);
        await EnsureSuccessOrThrowAsync(response);
        return await ReadDataFromResponseAsync<AutomationRuleDto>(response, ct);
    }

    public async Task DeleteAutomationRuleAsync(Guid ruleId, CancellationToken ct = default)
    {
        var response = await _httpClient.DeleteAsync($"api/v1/automation-rules/{ruleId}", ct);
        await EnsureSuccessOrThrowAsync(response);
    }

    // ── Goals / OKRs ─────────────────────────────────────────

    public async Task<List<GoalDto>> ListGoalsAsync(Guid productId, CancellationToken ct = default)
    {
        var result = await ReadDataAsync<List<GoalDto>>($"api/v1/products/{productId}/goals", ct);
        return result ?? new List<GoalDto>();
    }

    public async Task<GoalDto?> GetGoalAsync(Guid goalId, CancellationToken ct = default)
        => await ReadDataAsync<GoalDto>($"api/v1/goals/{goalId}", ct);

    public async Task<GoalDto?> CreateGoalAsync(Guid productId, CreateGoalDto dto, CancellationToken ct = default)
    {
        var response = await _httpClient.PostAsJsonAsync($"api/v1/products/{productId}/goals", dto, ct);
        await EnsureSuccessOrThrowAsync(response);
        return await ReadDataFromResponseAsync<GoalDto>(response, ct);
    }

    public async Task<GoalDto?> UpdateGoalAsync(Guid goalId, UpdateGoalDto dto, CancellationToken ct = default)
    {
        var response = await _httpClient.PutAsJsonAsync($"api/v1/goals/{goalId}", dto, ct);
        await EnsureSuccessOrThrowAsync(response);
        return await ReadDataFromResponseAsync<GoalDto>(response, ct);
    }

    public async Task DeleteGoalAsync(Guid goalId, CancellationToken ct = default)
    {
        var response = await _httpClient.DeleteAsync($"api/v1/goals/{goalId}", ct);
        await EnsureSuccessOrThrowAsync(response);
    }

    public async Task LinkGoalWorkItemAsync(Guid goalId, LinkGoalWorkItemDto dto, CancellationToken ct = default)
    {
        var response = await _httpClient.PostAsJsonAsync($"api/v1/goals/{goalId}/work-items", dto, ct);
        await EnsureSuccessOrThrowAsync(response);
    }

    public async Task UnlinkGoalWorkItemAsync(Guid goalId, Guid workItemId, CancellationToken ct = default)
    {
        var response = await _httpClient.DeleteAsync($"api/v1/goals/{goalId}/work-items/{workItemId}", ct);
        await EnsureSuccessOrThrowAsync(response);
    }

    // ── Capacity Planning ────────────────────────────────────

    public async Task<ProductCapacityDto?> GetProductCapacityAsync(Guid productId, CancellationToken ct = default)
        => await ReadDataAsync<ProductCapacityDto>($"api/v1/products/{productId}/analytics/capacity", ct);

    public async Task<SprintCapacityDto?> GetSprintCapacityAsync(Guid sprintId, CancellationToken ct = default)
        => await ReadDataAsync<SprintCapacityDto>($"api/v1/sprints/{sprintId}/capacity", ct);

    // ── Helpers ──────────────────────────────────────────────

    private async Task<T?> ReadDataAsync<T>(string url, CancellationToken ct)
    {
        var response = await _httpClient.GetAsync(url, ct);
        await EnsureSuccessOrThrowAsync(response);
        return await ReadDataFromResponseAsync<T>(response, ct);
    }

    private static async Task<T?> ReadDataFromResponseAsync<T>(HttpResponseMessage response, CancellationToken ct)
    {
        var json = await response.Content.ReadAsStringAsync(ct);
        using var document = JsonDocument.Parse(json);

        if (!TryGetPropertyIgnoreCase(document.RootElement, "data", out var dataElement))
            return default;

        if (dataElement.ValueKind == JsonValueKind.Object && TryGetPropertyIgnoreCase(dataElement, "data", out var nestedData))
            dataElement = nestedData;

        return JsonSerializer.Deserialize<T>(dataElement.GetRawText(), JsonOptions);
    }

    private static async Task EnsureSuccessOrThrowAsync(HttpResponseMessage response)
    {
        if (response.IsSuccessStatusCode)
            return;

        var body = await response.Content.ReadAsStringAsync();
        string message;

        try
        {
            using var doc = JsonDocument.Parse(body);
            if (TryGetPropertyIgnoreCase(doc.RootElement, "error", out var error) &&
                TryGetPropertyIgnoreCase(error, "message", out var msg))
            {
                message = msg.GetString() ?? $"Request failed ({(int)response.StatusCode}).";
            }
            else
            {
                message = $"Request failed ({(int)response.StatusCode}).";
            }
        }
        catch
        {
            message = $"Request failed ({(int)response.StatusCode}).";
        }

        throw new HttpRequestException(message, null, response.StatusCode);
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

// ── Internal DTOs for API deserialization ──────────────────────

internal sealed record WatcherDto
{
    public Guid UserId { get; init; }
    public DateTime SubscribedAt { get; init; }
}

internal sealed record BulkActionResultDto
{
    public int Affected { get; init; }
}

internal sealed record WatchResultDto
{
    public bool Watching { get; init; }
    public int WatcherCount { get; init; }
}
