using DotNetCloud.Core.Server.Rest;

namespace DotNetCloud.Core.Server.Extensions;

/// <summary>
/// Maps the internal module REST facade.
/// </summary>
/// <remarks>
/// The facade is mounted under <c>/internal/moduleapi/v1</c> on the core's own listener. It is for
/// process-isolated module backends that have no gRPC client: identity comes from the same
/// <c>dnc-module-id</c>/<c>dnc-module-token</c> credential and optional <c>dnc-user-assertion</c> the
/// gRPC surface uses, and every request is evaluated by the same interceptors (see
/// <see cref="ModuleRestApiService"/>). It is not a browser-facing API: module UI traffic goes
/// through the <c>/api/v1/*</c> proxy with the user's own session.
/// </remarks>
internal static class ModuleRestEndpointExtensions
{
    /// <summary>The path prefix the facade is mounted under.</summary>
    internal const string Prefix = "/internal/moduleapi/v1";

    /// <summary>Maps the module REST endpoints.</summary>
    /// <param name="app">The web application.</param>
    /// <returns>The web application for chaining.</returns>
    public static WebApplication MapModuleApiRestEndpoints(this WebApplication app)
    {
        ArgumentNullException.ThrowIfNull(app);

        var group = app.MapGroup(Prefix);

        // Health proves the credential; it deliberately maps to no data.
        group.MapGet("/health", (HttpContext http, ModuleRestApiService api) => api.GetHealthAsync(http));

        group.MapGet("/me", (HttpContext http, ModuleRestApiService api) => api.GetMeAsync(http));
        group.MapGet("/users", (HttpContext http, ModuleRestApiService api, string? query, int? limit) =>
            api.SearchUsersAsync(http, query, limit));
        group.MapGet("/users/{userId}", (HttpContext http, ModuleRestApiService api, string userId) =>
            api.GetUserAsync(http, userId));
        group.MapGet("/users/{userId}/groups", (HttpContext http, ModuleRestApiService api, string userId) =>
            api.GetGroupsForUserAsync(http, userId));
        group.MapGet("/users/{userId}/teams", (HttpContext http, ModuleRestApiService api, string userId) =>
            api.GetTeamsForUserAsync(http, userId));

        group.MapGet("/groups/{groupId}", (HttpContext http, ModuleRestApiService api, string groupId) =>
            api.GetGroupAsync(http, groupId));
        group.MapGet("/teams/{teamId}", (HttpContext http, ModuleRestApiService api, string teamId) =>
            api.GetTeamAsync(http, teamId));

        group.MapPost("/notifications",
            (HttpContext http, ModuleRestApiService api, ModuleRestContracts.NotificationRequest request) =>
                api.SendNotificationAsync(http, request));
        group.MapPost("/events",
            (HttpContext http, ModuleRestApiService api, ModuleRestContracts.EventRequest request) =>
                api.PublishEventAsync(http, request));
        group.MapPost("/search/index",
            (HttpContext http, ModuleRestApiService api, ModuleRestContracts.SearchIndexRequest request) =>
                api.SubmitSearchIndexAsync(http, request));
        group.MapPost("/realtime/broadcast",
            (HttpContext http, ModuleRestApiService api, ModuleRestContracts.RealtimeBroadcastRequest request) =>
                api.BroadcastRealtimeEventAsync(http, request));
        group.MapPost("/audit",
            (HttpContext http, ModuleRestApiService api, ModuleRestContracts.AuditRequest request) =>
                api.LogAuditAsync(http, request));

        group.MapGet("/settings/{key}", (HttpContext http, ModuleRestApiService api, string key) =>
            api.GetSettingAsync(http, key));
        group.MapPut("/settings/{key}",
            (HttpContext http, ModuleRestApiService api, string key, ModuleRestContracts.SettingRequest request) =>
                api.SetSettingAsync(http, key, request));

        group.MapGet("/consent/scopes", (HttpContext http, ModuleRestApiService api) =>
            api.GetGrantedScopesAsync(http));
        group.MapGet("/consent/{userId}", (HttpContext http, ModuleRestApiService api, string userId) =>
            api.CheckUserConsentAsync(http, userId));

        // Files (IFileDirectory / IFileWriter). The request body is the file content on POST, and the
        // response body is the file content on GET, so neither side needs a base64 envelope.
        group.MapGet("/files", (HttpContext http, ModuleRestApiService api, string? parentId, int? page, int? pageSize) =>
            api.ListFilesAsync(http, parentId, page, pageSize));
        group.MapGet("/files/{nodeId}/content", (HttpContext http, ModuleRestApiService api, string nodeId) =>
            api.DownloadFileAsync(http, nodeId));
        group.MapPost("/files", (HttpContext http, ModuleRestApiService api, string? parentId, string fileName, string? contentType) =>
            api.UploadFileAsync(http, parentId, fileName, contentType));

        return app;
    }
}
