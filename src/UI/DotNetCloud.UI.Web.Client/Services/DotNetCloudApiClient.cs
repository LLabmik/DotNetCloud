using System.Net.Http.Json;
using System.Text.Json;
using DotNetCloud.Core.DTOs;
using DotNetCloud.Core.DTOs.Search;

namespace DotNetCloud.UI.Web.Client.Services;

/// <summary>
/// HTTP client for communicating with the DotNetCloud REST API.
/// Used by InteractiveAuto components that may run in WebAssembly.
/// </summary>
public sealed class DotNetCloudApiClient
{
    private readonly HttpClient _http;
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public DotNetCloudApiClient(HttpClient http)
    {
        _http = http;
    }

    // -----------------------------------------------------------------------
    // Users
    // -----------------------------------------------------------------------

    /// <summary>
    /// Lists users with optional pagination and filtering.
    /// </summary>
    public async Task<PaginatedResult<UserDto>> ListUsersAsync(
        int page = 1,
        int pageSize = 25,
        string? search = null,
        bool? isActive = null,
        CancellationToken ct = default)
    {
        var query = $"api/v1/core/users?page={page}&pageSize={pageSize}";
        if (!string.IsNullOrWhiteSpace(search))
            query += $"&search={Uri.EscapeDataString(search)}";
        if (isActive.HasValue)
            query += $"&isActive={isActive.Value}";

        var response = await _http.GetAsync(query, ct);
        response.EnsureSuccessStatusCode();

        var json = await response.Content.ReadAsStringAsync(ct);
        return ParseUsersListResponse(json, page, pageSize);
    }

    private static PaginatedResult<UserDto> ParseUsersListResponse(string json, int fallbackPage, int fallbackPageSize)
    {
        using var document = JsonDocument.Parse(json);
        var root = document.RootElement;

        if (!TryGetPropertyIgnoreCase(root, "data", out var dataElement))
        {
            return new PaginatedResult<UserDto>();
        }

        JsonElement? paginationElement = null;

        if (TryGetPropertyIgnoreCase(root, "pagination", out var rootPagination) && rootPagination.ValueKind == JsonValueKind.Object)
        {
            paginationElement = rootPagination;
        }

        // Handle nested envelopes: { success, data: { success, data, pagination } }
        if (dataElement.ValueKind == JsonValueKind.Object && TryGetPropertyIgnoreCase(dataElement, "data", out var nestedData))
        {
            if (TryGetPropertyIgnoreCase(dataElement, "pagination", out var nestedPagination) && nestedPagination.ValueKind == JsonValueKind.Object)
            {
                paginationElement = nestedPagination;
            }

            dataElement = nestedData;
        }

        // Shape A: data is an array of users
        if (dataElement.ValueKind == JsonValueKind.Array)
        {
            var users = JsonSerializer.Deserialize<IReadOnlyList<UserDto>>(dataElement.GetRawText(), JsonOptions) ?? [];
            return new PaginatedResult<UserDto>
            {
                Items = users,
                Page = TryGetInt(paginationElement, "page") ?? fallbackPage,
                PageSize = TryGetInt(paginationElement, "pageSize") ?? fallbackPageSize,
                TotalCount = TryGetInt(paginationElement, "totalCount") ?? users.Count,
            };
        }

        // Shape B: data is PaginatedResult<UserDto>-like object
        if (dataElement.ValueKind == JsonValueKind.Object)
        {
            if (TryGetPropertyIgnoreCase(dataElement, "items", out var itemsElement) && itemsElement.ValueKind == JsonValueKind.Array)
            {
                var users = JsonSerializer.Deserialize<IReadOnlyList<UserDto>>(itemsElement.GetRawText(), JsonOptions) ?? [];
                var totalCount = TryGetInt(dataElement, "totalCount")
                                 ?? TryGetInt(paginationElement, "totalCount")
                                 ?? users.Count;
                var parsedPage = TryGetInt(dataElement, "page")
                                 ?? TryGetInt(paginationElement, "page")
                                 ?? fallbackPage;
                var parsedPageSize = TryGetInt(dataElement, "pageSize")
                                     ?? TryGetInt(paginationElement, "pageSize")
                                     ?? fallbackPageSize;

                return new PaginatedResult<UserDto>
                {
                    Items = users,
                    TotalCount = totalCount,
                    Page = parsedPage,
                    PageSize = parsedPageSize,
                };
            }
        }

        return new PaginatedResult<UserDto>();
    }

    private static int? TryGetInt(JsonElement? element, string propertyName)
    {
        if (element is null || element.Value.ValueKind != JsonValueKind.Object)
        {
            return null;
        }

        if (!TryGetPropertyIgnoreCase(element.Value, propertyName, out var valueElement))
        {
            return null;
        }

        if (valueElement.ValueKind == JsonValueKind.Number && valueElement.TryGetInt32(out var intValue))
        {
            return intValue;
        }

        return null;
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

    /// <summary>
    /// Gets a user by ID.
    /// </summary>
    public async Task<UserDto?> GetUserAsync(Guid userId, CancellationToken ct = default)
    {
        var envelope = await _http.GetFromJsonAsync<ApiEnvelope<UserDto>>($"api/v1/core/users/{userId}", ct);
        return envelope?.Data;
    }

    /// <summary>
    /// Updates a user profile.
    /// </summary>
    public async Task<UserDto?> UpdateUserAsync(Guid userId, UpdateUserDto dto, CancellationToken ct = default)
    {
        var response = await _http.PutAsJsonAsync($"api/v1/core/users/{userId}", dto, ct);
        response.EnsureSuccessStatusCode();
        var envelope = await response.Content.ReadFromJsonAsync<ApiEnvelope<UserDto>>(ct);
        return envelope?.Data;
    }

    /// <summary>
    /// Creates a new user via admin registration.
    /// Returns (success, userId) tuple.
    /// </summary>
    public async Task<(bool Success, string? ErrorMessage)> CreateUserAsync(RegisterRequest request, CancellationToken ct = default)
    {
        var response = await _http.PostAsJsonAsync("api/v1/core/auth/register", request, ct);
        if (response.IsSuccessStatusCode)
        {
            return (true, null);
        }

        var body = await response.Content.ReadAsStringAsync(ct);
        return (false, body);
    }

    /// <summary>
    /// Deletes a user.
    /// </summary>
    public async Task<bool> DeleteUserAsync(Guid userId, CancellationToken ct = default)
    {
        var response = await _http.DeleteAsync($"api/v1/core/users/{userId}", ct);
        return response.IsSuccessStatusCode;
    }

    /// <summary>
    /// Disables a user.
    /// </summary>
    public async Task<bool> DisableUserAsync(Guid userId, CancellationToken ct = default)
    {
        var response = await _http.PostAsync($"api/v1/core/users/{userId}/disable", null, ct);
        return response.IsSuccessStatusCode;
    }

    /// <summary>
    /// Enables a user.
    /// </summary>
    public async Task<bool> EnableUserAsync(Guid userId, CancellationToken ct = default)
    {
        var response = await _http.PostAsync($"api/v1/core/users/{userId}/enable", null, ct);
        return response.IsSuccessStatusCode;
    }

    /// <summary>
    /// Admin password reset.
    /// </summary>
    public async Task<bool> AdminResetPasswordAsync(Guid userId, string newPassword, CancellationToken ct = default)
    {
        var response = await _http.PostAsJsonAsync(
            $"api/v1/core/users/{userId}/reset-password",
            new AdminResetPasswordRequest { NewPassword = newPassword },
            ct);
        return response.IsSuccessStatusCode;
    }

    // -----------------------------------------------------------------------
    // Modules
    // -----------------------------------------------------------------------

    /// <summary>
    /// Lists all installed modules.
    /// </summary>
    public async Task<IReadOnlyList<ModuleDto>> ListModulesAsync(CancellationToken ct = default)
    {
        var envelope = await _http.GetFromJsonAsync<ApiEnvelope<IReadOnlyList<ModuleDto>>>("api/v1/core/admin/modules", ct);
        return envelope?.Data ?? [];
    }

    /// <summary>
    /// Gets a specific module by ID (admin only).
    /// </summary>
    public async Task<ModuleDto?> GetModuleAsync(string moduleId, CancellationToken ct = default)
    {
        var envelope = await _http.GetFromJsonAsync<ApiEnvelope<ModuleDto>>(
            $"api/v1/core/admin/modules/{Uri.EscapeDataString(moduleId)}", ct);
        return envelope?.Data;
    }

    /// <summary>
    /// Checks whether a module is installed and available. Accessible by any authenticated user.
    /// </summary>
    public async Task<bool> IsModuleAvailableAsync(string moduleId, CancellationToken ct = default)
    {
        try
        {
            var envelope = await _http.GetFromJsonAsync<ApiEnvelope<ModuleAvailabilityDto>>(
                $"api/v1/core/modules/{Uri.EscapeDataString(moduleId)}/available", ct);
            return envelope?.Data?.Installed ?? false;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Starts a module.
    /// </summary>
    public async Task<bool> StartModuleAsync(string moduleId, CancellationToken ct = default)
    {
        var response = await _http.PostAsync(
            $"api/v1/core/admin/modules/{Uri.EscapeDataString(moduleId)}/start", null, ct);
        return response.IsSuccessStatusCode;
    }

    /// <summary>
    /// Stops a module.
    /// </summary>
    public async Task<bool> StopModuleAsync(string moduleId, CancellationToken ct = default)
    {
        var response = await _http.PostAsync(
            $"api/v1/core/admin/modules/{Uri.EscapeDataString(moduleId)}/stop", null, ct);
        return response.IsSuccessStatusCode;
    }

    /// <summary>
    /// Restarts a module.
    /// </summary>
    public async Task<bool> RestartModuleAsync(string moduleId, CancellationToken ct = default)
    {
        var response = await _http.PostAsync(
            $"api/v1/core/admin/modules/{Uri.EscapeDataString(moduleId)}/restart", null, ct);
        return response.IsSuccessStatusCode;
    }

    // -----------------------------------------------------------------------
    // Settings
    // -----------------------------------------------------------------------

    /// <summary>
    /// Lists all system settings.
    /// </summary>
    public async Task<IReadOnlyList<SystemSettingDto>> ListSettingsAsync(string? module = null, CancellationToken ct = default)
    {
        var url = "api/v1/core/admin/settings";
        if (!string.IsNullOrWhiteSpace(module))
            url += $"?module={Uri.EscapeDataString(module)}";
        var envelope = await _http.GetFromJsonAsync<ApiEnvelope<IReadOnlyList<SystemSettingDto>>>(url, ct);
        return envelope?.Data ?? [];
    }

    /// <summary>
    /// Gets a specific setting.
    /// </summary>
    public async Task<SystemSettingDto?> GetSettingAsync(string module, string key, CancellationToken ct = default)
    {
        var envelope = await _http.GetFromJsonAsync<ApiEnvelope<SystemSettingDto>>(
            $"api/v1/core/admin/settings/{Uri.EscapeDataString(module)}/{Uri.EscapeDataString(key)}", ct);
        return envelope?.Data;
    }

    /// <summary>
    /// Creates or updates a setting.
    /// </summary>
    public async Task<bool> UpsertSettingAsync(string module, string key, UpsertSystemSettingDto dto, CancellationToken ct = default)
    {
        var response = await _http.PutAsJsonAsync(
            $"api/v1/core/admin/settings/{Uri.EscapeDataString(module)}/{Uri.EscapeDataString(key)}",
            dto, ct);
        return response.IsSuccessStatusCode;
    }

    /// <summary>
    /// Deletes a setting.
    /// </summary>
    public async Task<bool> DeleteSettingAsync(string module, string key, CancellationToken ct = default)
    {
        var response = await _http.DeleteAsync(
            $"api/v1/core/admin/settings/{Uri.EscapeDataString(module)}/{Uri.EscapeDataString(key)}", ct);
        return response.IsSuccessStatusCode;
    }

    /// <summary>
    /// Gets a specific setting for the current authenticated user.
    /// </summary>
    public async Task<UserSettingDto?> GetUserSettingAsync(string module, string key, CancellationToken ct = default)
    {
        var response = await _http.GetAsync(
            $"api/v1/core/user-settings/{Uri.EscapeDataString(module)}/{Uri.EscapeDataString(key)}", ct);

        if (!response.IsSuccessStatusCode)
        {
            return null;
        }

        var envelope = await response.Content.ReadFromJsonAsync<ApiEnvelope<UserSettingDto>>(ct);
        return envelope?.Data;
    }

    /// <summary>
    /// Creates or updates a setting for the current authenticated user.
    /// </summary>
    public async Task<bool> UpsertUserSettingAsync(string module, string key, UpsertUserSettingDto dto, CancellationToken ct = default)
    {
        var response = await _http.PutAsJsonAsync(
            $"api/v1/core/user-settings/{Uri.EscapeDataString(module)}/{Uri.EscapeDataString(key)}",
            dto,
            ct);
        return response.IsSuccessStatusCode;
    }

    // -----------------------------------------------------------------------
    // Media Library
    // -----------------------------------------------------------------------

    /// <summary>
    /// Gets the current user's media library paths.
    /// </summary>
    public async Task<MediaLibraryPathsResponse?> GetMediaLibraryPathsAsync(CancellationToken ct = default)
    {
        var response = await _http.GetAsync("api/v1/media-library/paths", ct);
        if (!response.IsSuccessStatusCode) return null;
        var envelope = await response.Content.ReadFromJsonAsync<ApiEnvelope<MediaLibraryPathsResponse>>(ct);
        return envelope?.Data;
    }

    /// <summary>
    /// Updates the current user's media library paths.
    /// </summary>
    public async Task<bool> UpdateMediaLibraryPathsAsync(MediaLibraryPathsResponse paths, CancellationToken ct = default)
    {
        var response = await _http.PutAsJsonAsync("api/v1/media-library/paths", paths, ct);
        return response.IsSuccessStatusCode;
    }

    /// <summary>
    /// Triggers a media library scan for the specified media type.
    /// </summary>
    public async Task<MediaScanResultResponse?> ScanMediaLibraryAsync(string mediaType, CancellationToken ct = default)
    {
        var response = await _http.PostAsJsonAsync("api/v1/media-library/scan", new { mediaType }, ct);
        if (!response.IsSuccessStatusCode)
        {
            var errorText = await response.Content.ReadAsStringAsync(ct);
            throw new HttpRequestException(errorText);
        }
        var envelope = await response.Content.ReadFromJsonAsync<ApiEnvelope<MediaScanResultResponse>>(ct);
        return envelope?.Data;
    }

    // -----------------------------------------------------------------------
    // Health
    // -----------------------------------------------------------------------

    /// <summary>
    /// Gets the system health report.
    /// </summary>
    public async Task<HealthReportDto?> GetHealthAsync(CancellationToken ct = default)
    {
        var envelope = await _http.GetFromJsonAsync<ApiEnvelope<HealthReportDto>>("api/v1/core/admin/health", ct);
        return envelope?.Data;
    }

    /// <summary>
    /// Gets the status of all tracked background services.
    /// </summary>
    public async Task<IReadOnlyList<BackgroundServiceStatusDto>> GetBackgroundServicesAsync(CancellationToken ct = default)
    {
        var envelope = await _http.GetFromJsonAsync<ApiEnvelope<IReadOnlyList<BackgroundServiceStatusDto>>>(
            "api/v1/core/admin/background-services", ct);
        return envelope?.Data ?? [];
    }

    // -----------------------------------------------------------------------
    // Sync Device Status (Admin)
    // -----------------------------------------------------------------------

    /// <summary>
    /// Gets sync status for all registered devices across all users (admin-only).
    /// </summary>
    public async Task<IReadOnlyList<DeviceSyncStatusDto>> ListDeviceSyncStatusAsync(CancellationToken ct = default)
    {
        var envelope = await _http.GetFromJsonAsync<ApiEnvelope<IReadOnlyList<DeviceSyncStatusDto>>>(
            "api/v1/files/sync/admin/device-status", ct);
        return envelope?.Data ?? [];
    }

    /// <summary>
    /// Activates or deactivates a sync device (admin-only).
    /// </summary>
    public async Task SetDeviceActiveAsync(Guid deviceId, bool isActive, CancellationToken ct = default)
    {
        var response = await _http.PutAsJsonAsync(
            $"api/v1/files/sync/admin/device/{deviceId}/active",
            new { isActive }, ct);
        response.EnsureSuccessStatusCode();
    }

    // -----------------------------------------------------------------------
    // Notifications
    // -----------------------------------------------------------------------

    /// <summary>
    /// Gets unread notifications for the current user.
    /// </summary>
    public async Task<IReadOnlyList<NotificationDto>> GetUnreadNotificationsAsync(int maxResults = 25, CancellationToken ct = default)
    {
        var response = await _http.GetAsync($"api/v1/notifications/unread?maxResults={Math.Clamp(maxResults, 1, 200)}", ct);
        if (!response.IsSuccessStatusCode)
        {
            return [];
        }

        var json = await response.Content.ReadAsStringAsync(ct);

        using var document = JsonDocument.Parse(json);
        if (!TryGetPropertyIgnoreCase(document.RootElement, "data", out var dataElement))
        {
            return [];
        }

        if (dataElement.ValueKind == JsonValueKind.Object && TryGetPropertyIgnoreCase(dataElement, "data", out var nestedData))
        {
            dataElement = nestedData;
        }

        return JsonSerializer.Deserialize<IReadOnlyList<NotificationDto>>(dataElement.GetRawText(), JsonOptions) ?? [];
    }

    /// <summary>
    /// Gets unread notification count for the current user.
    /// </summary>
    public async Task<int> GetUnreadNotificationCountAsync(CancellationToken ct = default)
    {
        var response = await _http.GetAsync("api/v1/notifications/unread-count", ct);
        if (!response.IsSuccessStatusCode)
        {
            return 0;
        }

        var json = await response.Content.ReadAsStringAsync(ct);

        using var document = JsonDocument.Parse(json);
        if (!TryGetPropertyIgnoreCase(document.RootElement, "data", out var dataElement))
        {
            return 0;
        }

        if (dataElement.ValueKind == JsonValueKind.Object && TryGetPropertyIgnoreCase(dataElement, "data", out var nestedData))
        {
            dataElement = nestedData;
        }

        return dataElement.ValueKind == JsonValueKind.Number && dataElement.TryGetInt32(out var count)
            ? count
            : 0;
    }

    /// <summary>
    /// Marks a notification as read.
    /// </summary>
    public async Task MarkNotificationReadAsync(Guid notificationId, CancellationToken ct = default)
    {
        var response = await _http.PostAsync($"api/v1/notifications/{notificationId}/mark-read", null, ct);
        response.EnsureSuccessStatusCode();
    }

    /// <summary>
    /// Marks all notifications as read.
    /// </summary>
    public async Task MarkAllNotificationsReadAsync(CancellationToken ct = default)
    {
        var response = await _http.PostAsync("api/v1/notifications/mark-all-read", null, ct);
        response.EnsureSuccessStatusCode();
    }

    // -----------------------------------------------------------------------
    // Quotas (Admin)
    // -----------------------------------------------------------------------

    /// <summary>
    /// Gets a specific user's storage quota (admin).
    /// </summary>
    public async Task<QuotaResponse?> GetUserQuotaAsync(Guid targetUserId, CancellationToken ct = default)
    {
        var response = await _http.GetAsync($"api/v1/files/quota/{targetUserId}", ct);
        if (!response.IsSuccessStatusCode) return null;
        var envelope = await response.Content.ReadFromJsonAsync<ApiEnvelope<QuotaResponse>>(JsonOptions, ct);
        return envelope?.Data;
    }

    /// <summary>
    /// Gets all user quotas (admin). Returns a dictionary keyed by UserId.
    /// </summary>
    public async Task<Dictionary<Guid, QuotaResponse>> GetAllQuotasAsync(CancellationToken ct = default)
    {
        var response = await _http.GetAsync("api/v1/files/quota/all", ct);
        if (!response.IsSuccessStatusCode) return [];
        var envelope = await response.Content.ReadFromJsonAsync<ApiEnvelope<List<QuotaResponse>>>(JsonOptions, ct);
        return envelope?.Data?.ToDictionary(q => q.UserId) ?? [];
    }

    /// <summary>
    /// Sets a user's storage quota in bytes (admin). 0 = unlimited.
    /// </summary>
    public async Task<bool> SetUserQuotaAsync(Guid targetUserId, long maxBytes, CancellationToken ct = default)
    {
        var response = await _http.PutAsJsonAsync(
            $"api/v1/files/quota/{targetUserId}",
            new { maxBytes }, ct);
        return response.IsSuccessStatusCode;
    }

    // -----------------------------------------------------------------------
    // Organizations
    // -----------------------------------------------------------------------

    /// <summary>
    /// Lists all organizations.
    /// </summary>
    public async Task<IReadOnlyList<OrganizationDto>> ListOrganizationsAsync(CancellationToken ct = default)
    {
        var envelope = await _http.GetFromJsonAsync<ApiEnvelope<IReadOnlyList<OrganizationDto>>>(
            "api/v1/core/admin/organizations", ct);
        return envelope?.Data ?? [];
    }

    /// <summary>
    /// Creates a new organization.
    /// </summary>
    public async Task<OrganizationDto?> CreateOrganizationAsync(CreateOrganizationDto dto, CancellationToken ct = default)
    {
        var response = await _http.PostAsJsonAsync("api/v1/core/admin/organizations", dto, ct);
        response.EnsureSuccessStatusCode();
        var envelope = await response.Content.ReadFromJsonAsync<ApiEnvelope<OrganizationDto>>(ct);
        return envelope?.Data;
    }

    /// <summary>
    /// Updates an organization.
    /// </summary>
    public async Task<bool> UpdateOrganizationAsync(Guid id, UpdateOrganizationDto dto, CancellationToken ct = default)
    {
        var response = await _http.PutAsJsonAsync($"api/v1/core/admin/organizations/{id}", dto, ct);
        return response.IsSuccessStatusCode;
    }

    /// <summary>
    /// Deletes an organization.
    /// </summary>
    public async Task<bool> DeleteOrganizationAsync(Guid id, CancellationToken ct = default)
    {
        var response = await _http.DeleteAsync($"api/v1/core/admin/organizations/{id}", ct);
        return response.IsSuccessStatusCode;
    }

    /// <summary>
    /// Lists members of an organization.
    /// </summary>
    public async Task<IReadOnlyList<OrganizationMemberDto>> ListOrganizationMembersAsync(Guid orgId, CancellationToken ct = default)
    {
        var envelope = await _http.GetFromJsonAsync<ApiEnvelope<IReadOnlyList<OrganizationMemberDto>>>(
            $"api/v1/core/admin/organizations/{orgId}/members", ct);
        return envelope?.Data ?? [];
    }

    /// <summary>
    /// Lists users that are not members of the specified organization.
    /// </summary>
    public async Task<IReadOnlyList<OrganizationMemberDto>> ListNonMembersAsync(Guid orgId, string? search = null, CancellationToken ct = default)
    {
        var url = $"api/v1/core/admin/organizations/{orgId}/non-members";
        if (!string.IsNullOrWhiteSpace(search))
            url += $"?search={Uri.EscapeDataString(search)}";
        var envelope = await _http.GetFromJsonAsync<ApiEnvelope<IReadOnlyList<OrganizationMemberDto>>>(url, ct);
        return envelope?.Data ?? [];
    }

    /// <summary>
    /// Adds a user to an organization.
    /// </summary>
    public async Task<bool> AddOrganizationMemberAsync(Guid orgId, Guid userId, CancellationToken ct = default)
    {
        var response = await _http.PostAsJsonAsync(
            $"api/v1/core/admin/organizations/{orgId}/members",
            new AddOrganizationMemberDto { UserId = userId }, ct);
        return response.IsSuccessStatusCode;
    }

    /// <summary>
    /// Removes a user from an organization.
    /// </summary>
    public async Task<bool> RemoveOrganizationMemberAsync(Guid orgId, Guid userId, CancellationToken ct = default)
    {
        var response = await _http.DeleteAsync($"api/v1/core/admin/organizations/{orgId}/members/{userId}", ct);
        return response.IsSuccessStatusCode;
    }

    // -----------------------------------------------------------------------
    // Groups
    // -----------------------------------------------------------------------

    /// <summary>
    /// Lists groups for the specified organization.
    /// </summary>
    public async Task<IReadOnlyList<GroupDto>> ListGroupsAsync(Guid organizationId, CancellationToken ct = default)
    {
        var envelope = await _http.GetFromJsonAsync<ApiEnvelope<IReadOnlyList<GroupDto>>>(
            $"api/v1/core/admin/groups?organizationId={organizationId}", ct);
        return envelope?.Data ?? [];
    }

    /// <summary>
    /// Creates a new group.
    /// </summary>
    public async Task<GroupDto?> CreateGroupAsync(CreateGroupDto dto, CancellationToken ct = default)
    {
        var response = await _http.PostAsJsonAsync("api/v1/core/admin/groups", dto, ct);
        response.EnsureSuccessStatusCode();
        var envelope = await response.Content.ReadFromJsonAsync<ApiEnvelope<GroupDto>>(ct);
        return envelope?.Data;
    }

    /// <summary>
    /// Updates a group.
    /// </summary>
    public async Task<bool> UpdateGroupAsync(Guid id, UpdateGroupDto dto, CancellationToken ct = default)
    {
        var response = await _http.PutAsJsonAsync($"api/v1/core/admin/groups/{id}", dto, ct);
        return response.IsSuccessStatusCode;
    }

    /// <summary>
    /// Deletes a group.
    /// </summary>
    public async Task<bool> DeleteGroupAsync(Guid id, CancellationToken ct = default)
    {
        var response = await _http.DeleteAsync($"api/v1/core/admin/groups/{id}", ct);
        return response.IsSuccessStatusCode;
    }

    /// <summary>
    /// Lists members of a group.
    /// </summary>
    public async Task<IReadOnlyList<GroupMemberDto>> ListGroupMembersAsync(Guid groupId, CancellationToken ct = default)
    {
        var envelope = await _http.GetFromJsonAsync<ApiEnvelope<IReadOnlyList<GroupMemberDto>>>(
            $"api/v1/core/admin/groups/{groupId}/members", ct);
        return envelope?.Data ?? [];
    }

    /// <summary>
    /// Adds a user to a group.
    /// </summary>
    public async Task<bool> AddGroupMemberAsync(Guid groupId, Guid userId, CancellationToken ct = default)
    {
        var response = await _http.PostAsJsonAsync(
            $"api/v1/core/admin/groups/{groupId}/members",
            new AddGroupMemberDto { UserId = userId }, ct);
        return response.IsSuccessStatusCode;
    }

    /// <summary>
    /// Removes a user from a group.
    /// </summary>
    public async Task<bool> RemoveGroupMemberAsync(Guid groupId, Guid userId, CancellationToken ct = default)
    {
        var response = await _http.DeleteAsync($"api/v1/core/admin/groups/{groupId}/members/{userId}", ct);
        return response.IsSuccessStatusCode;
    }

    // -----------------------------------------------------------------------
    // Current User Profile
    // -----------------------------------------------------------------------

    /// <summary>
    /// Gets the current authenticated user's profile.
    /// </summary>
    public async Task<UserProfileResponse?> GetCurrentUserProfileAsync(CancellationToken ct = default)
    {
        var response = await _http.GetAsync("api/v1/core/auth/user", ct);
        if (!response.IsSuccessStatusCode) return null;
        var envelope = await response.Content.ReadFromJsonAsync<ApiEnvelope<UserProfileResponse>>(JsonOptions, ct);
        return envelope?.Data;
    }

    /// <summary>
    /// Changes the current user's password.
    /// </summary>
    public async Task<(bool Success, string? ErrorMessage)> ChangePasswordAsync(ChangePasswordRequest request, CancellationToken ct = default)
    {
        var response = await _http.PostAsJsonAsync("api/v1/core/auth/password/change", request, ct);
        if (response.IsSuccessStatusCode)
        {
            return (true, null);
        }

        var body = await response.Content.ReadAsStringAsync(ct);
        return (false, body);
    }

    /// <summary>
    /// Uploads an avatar image for the specified user.
    /// </summary>
    public async Task<string?> UploadAvatarAsync(Guid userId, StreamContent fileContent, string fileName, CancellationToken ct = default)
    {
        using var form = new MultipartFormDataContent();
        form.Add(fileContent, "file", fileName);

        var response = await _http.PostAsync($"api/v1/core/users/{userId}/avatar", form, ct);
        if (!response.IsSuccessStatusCode) return null;

        var json = await response.Content.ReadAsStringAsync(ct);
        using var doc = System.Text.Json.JsonDocument.Parse(json);
        if (doc.RootElement.TryGetProperty("data", out var data) &&
            data.TryGetProperty("avatarUrl", out var url))
        {
            return url.GetString();
        }

        return null;
    }

    /// <summary>
    /// Deletes the avatar for the specified user.
    /// </summary>
    public async Task<bool> DeleteAvatarAsync(Guid userId, CancellationToken ct = default)
    {
        var response = await _http.DeleteAsync($"api/v1/core/users/{userId}/avatar", ct);
        return response.IsSuccessStatusCode;
    }

    // -----------------------------------------------------------------------
    // Search
    // -----------------------------------------------------------------------

    /// <summary>
    /// Executes a full-text search query.
    /// </summary>
    public async Task<SearchResultDto?> SearchAsync(
        string query,
        string? module = null,
        string? type = null,
        int page = 1,
        int pageSize = 20,
        string sort = "relevance",
        CancellationToken ct = default)
    {
        var url = $"api/v1/search?q={Uri.EscapeDataString(query)}&page={page}&pageSize={pageSize}&sort={Uri.EscapeDataString(sort)}";
        if (!string.IsNullOrWhiteSpace(module))
            url += $"&module={Uri.EscapeDataString(module)}";
        if (!string.IsNullOrWhiteSpace(type))
            url += $"&type={Uri.EscapeDataString(type)}";

        var envelope = await _http.GetFromJsonAsync<ApiEnvelope<SearchResultDto>>(url, JsonOptions, ct);
        return envelope?.Data;
    }

    /// <summary>
    /// Returns autocomplete suggestions for a search prefix.
    /// </summary>
    public async Task<IReadOnlyList<SearchResultItem>?> SearchSuggestAsync(
        string query,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(query) || query.Length < 2)
            return [];

        var url = $"api/v1/search/suggest?q={Uri.EscapeDataString(query)}";
        var envelope = await _http.GetFromJsonAsync<ApiEnvelope<IReadOnlyList<SearchResultItem>>>(url, JsonOptions, ct);
        return envelope?.Data ?? [];
    }

    /// <summary>
    /// Returns comprehensive search admin status (index stats, queue depth, reindex progress).
    /// </summary>
    public async Task<SearchAdminStatusDto?> GetSearchAdminStatusAsync(CancellationToken ct = default)
    {
        var envelope = await _http.GetFromJsonAsync<ApiEnvelope<SearchAdminStatusDto>>(
            "api/v1/search/admin/status", JsonOptions, ct);
        return envelope?.Data;
    }

    /// <summary>
    /// Triggers a full search reindex of all modules.
    /// </summary>
    public async Task<bool> TriggerSearchReindexAsync(CancellationToken ct = default)
    {
        var response = await _http.PostAsync("api/v1/search/admin/reindex", null, ct);
        return response.IsSuccessStatusCode;
    }

    /// <summary>
    /// Triggers a search reindex for a specific module.
    /// </summary>
    public async Task<bool> TriggerSearchModuleReindexAsync(string moduleId, CancellationToken ct = default)
    {
        var response = await _http.PostAsync($"api/v1/search/admin/reindex/{Uri.EscapeDataString(moduleId)}", null, ct);
        return response.IsSuccessStatusCode;
    }

    // -----------------------------------------------------------------------
    // Updates
    // -----------------------------------------------------------------------

    /// <summary>
    /// Checks whether a newer version of DotNetCloud is available.
    /// </summary>
    public async Task<UpdateCheckResult?> CheckForUpdateAsync(CancellationToken ct = default)
    {
        var envelope = await _http.GetFromJsonAsync<ApiEnvelope<UpdateCheckResult>>(
            "api/v1/core/updates/check", JsonOptions, ct);
        return envelope?.Data;
    }

    /// <summary>
    /// Returns a list of recent releases.
    /// </summary>
    public async Task<IReadOnlyList<ReleaseInfo>> GetRecentReleasesAsync(int count = 5, CancellationToken ct = default)
    {
        var envelope = await _http.GetFromJsonAsync<ApiEnvelope<IReadOnlyList<ReleaseInfo>>>(
            $"api/v1/core/updates/releases?count={count}", JsonOptions, ct);
        return envelope?.Data ?? [];
    }

    /// <summary>
    /// Returns the latest release.
    /// </summary>
    public async Task<ReleaseInfo?> GetLatestReleaseAsync(CancellationToken ct = default)
    {
        var envelope = await _http.GetFromJsonAsync<ApiEnvelope<ReleaseInfo>>(
            "api/v1/core/updates/releases/latest", JsonOptions, ct);
        return envelope?.Data;
    }

    // -----------------------------------------------------------------------
    // Response envelope
    // -----------------------------------------------------------------------

    /// <summary>
    /// Standard API response envelope for deserialization.
    /// </summary>
    internal sealed class ApiEnvelope<T>
    {
        public bool Success { get; set; }
        public T? Data { get; set; }
    }

}

/// <summary>
/// DTO for deserializing the health report from the admin API.
/// </summary>
public sealed class HealthReportDto
{
    public string Status { get; set; } = string.Empty;
    public Dictionary<string, HealthEntryDto> Entries { get; set; } = [];
}

/// <summary>
/// Individual health check entry.
/// </summary>
public sealed class HealthEntryDto
{
    public string Status { get; set; } = string.Empty;
    public string? Description { get; set; }
    public double Duration { get; set; }
    public Dictionary<string, object>? Data { get; set; }
}

/// <summary>
/// Status snapshot for a tracked background service.
/// </summary>
public sealed class BackgroundServiceStatusDto
{
    public string ServiceName { get; set; } = string.Empty;
    public DateTimeOffset LastRunAt { get; set; }
    public double LastRunDurationMs { get; set; }
    public bool LastRunSuccess { get; set; }
    public string? LastRunMessage { get; set; }
    public int TotalRuns { get; set; }
    public int TotalFailures { get; set; }
}

/// <summary>
/// Admin view of a device's sync status including lag.
/// </summary>
public sealed class DeviceSyncStatusDto
{
    public Guid DeviceId { get; set; }
    public Guid UserId { get; set; }
    public string? UserDisplayName { get; set; }
    public string DeviceName { get; set; } = string.Empty;
    public string? Platform { get; set; }
    public string? ClientVersion { get; set; }
    public bool IsActive { get; set; }
    public long CurrentSequence { get; set; }
    public long? LastAcknowledgedSequence { get; set; }
    public long Lag { get; set; }
    public DateTime LastSeenAt { get; set; }
    public DateTime? CursorUpdatedAt { get; set; }
}

/// <summary>
/// Response DTO for user storage quota.
/// </summary>
public sealed class QuotaResponse
{
    public Guid UserId { get; set; }
    public long MaxBytes { get; set; }
    public long UsedBytes { get; set; }
    public long RemainingBytes { get; set; }
    public double UsagePercent { get; set; }
}

/// <summary>
/// Media library paths for the current user.
/// </summary>
public sealed class MediaLibraryPathsResponse
{
    /// <summary>Path to photos directory.</summary>
    public string PhotosPath { get; set; } = string.Empty;

    /// <summary>Path to music directory.</summary>
    public string MusicPath { get; set; } = string.Empty;

    /// <summary>Path to video directory.</summary>
    public string VideoPath { get; set; } = string.Empty;
}

/// <summary>
/// Result of a media library scan operation.
/// </summary>
public sealed class MediaScanResultResponse
{
    /// <summary>Total media files found in the directory.</summary>
    public int TotalFound { get; set; }

    /// <summary>Files successfully imported.</summary>
    public int Imported { get; set; }

    /// <summary>Files skipped (already imported).</summary>
    public int Skipped { get; set; }

    /// <summary>Files that failed to import.</summary>
    public int Failed { get; set; }

    /// <summary>Error messages for failed imports.</summary>
    public List<string> Errors { get; set; } = [];
}

/// <summary>
/// Admin status DTO for the search engine management page.
/// </summary>
public sealed class SearchAdminStatusDto
{
    /// <summary>Total documents currently in the search index.</summary>
    public int TotalDocuments { get; set; }

    /// <summary>Document counts per module.</summary>
    public Dictionary<string, int> DocumentsPerModule { get; set; } = [];

    /// <summary>When the last full reindex completed.</summary>
    public DateTimeOffset? LastFullReindexAt { get; set; }

    /// <summary>When the last incremental index completed.</summary>
    public DateTimeOffset? LastIncrementalIndexAt { get; set; }

    /// <summary>Number of items waiting in the real-time indexing queue.</summary>
    public int PendingQueueCount { get; set; }

    /// <summary>Total documents processed by the real-time indexer since startup.</summary>
    public long RealtimeProcessed { get; set; }

    /// <summary>Total failed real-time indexing operations since startup.</summary>
    public long RealtimeFailed { get; set; }

    /// <summary>Whether a full reindex is currently in progress.</summary>
    public bool IsReindexing { get; set; }

    /// <summary>Live progress data for an active reindex, or null.</summary>
    public ReindexProgressDto? ReindexProgress { get; set; }

    /// <summary>Most recent indexing job (any type).</summary>
    public IndexingJobDto? LastJob { get; set; }

    /// <summary>Most recent completed full reindex job.</summary>
    public LastFullReindexJobDto? LastFullReindexJob { get; set; }
}

/// <summary>
/// Live progress of an active full reindex operation.
/// </summary>
public sealed class ReindexProgressDto
{
    /// <summary>Module currently being indexed.</summary>
    public string? CurrentModule { get; set; }

    /// <summary>Documents processed so far.</summary>
    public int DocumentsProcessed { get; set; }

    /// <summary>Total documents to process.</summary>
    public int DocumentsTotal { get; set; }

    /// <summary>When the reindex started.</summary>
    public DateTimeOffset? StartedAt { get; set; }
}

/// <summary>
/// Summary of a recent indexing job.
/// </summary>
public sealed class IndexingJobDto
{
    /// <summary>Job identifier.</summary>
    public Guid Id { get; set; }

    /// <summary>Module targeted, or null for global.</summary>
    public string? ModuleId { get; set; }

    /// <summary>Job type (Full, Incremental).</summary>
    public string Type { get; set; } = string.Empty;

    /// <summary>Job status (Pending, Running, Completed, Failed).</summary>
    public string Status { get; set; } = string.Empty;

    /// <summary>When the job started.</summary>
    public DateTimeOffset? StartedAt { get; set; }

    /// <summary>When the job completed.</summary>
    public DateTimeOffset? CompletedAt { get; set; }

    /// <summary>Documents processed.</summary>
    public int DocumentsProcessed { get; set; }

    /// <summary>Total documents in the job.</summary>
    public int DocumentsTotal { get; set; }

    /// <summary>Error message if failed.</summary>
    public string? ErrorMessage { get; set; }
}

/// <summary>
/// Summary of the last completed full reindex.
/// </summary>
public sealed class LastFullReindexJobDto
{
    /// <summary>When the reindex completed.</summary>
    public DateTimeOffset? CompletedAt { get; set; }

    /// <summary>Documents processed.</summary>
    public int DocumentsProcessed { get; set; }

    /// <summary>Total documents found.</summary>
    public int DocumentsTotal { get; set; }

    /// <summary>Duration in seconds.</summary>
    public double? DurationSeconds { get; set; }
}

/// <summary>
/// Response DTO for the module availability check endpoint.
/// </summary>
public sealed class ModuleAvailabilityDto
{
    /// <summary>Whether the module is installed.</summary>
    public bool Installed { get; set; }
}
