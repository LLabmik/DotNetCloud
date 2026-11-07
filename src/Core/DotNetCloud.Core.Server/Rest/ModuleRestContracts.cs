using Api = DotNetCloud.Core.Grpc.ModuleApi.V1;

namespace DotNetCloud.Core.Server.Rest;

/// <summary>
/// Request bodies accepted by the internal module REST facade.
/// </summary>
/// <remarks>
/// These are the JSON counterparts of the canonical <c>module_api.proto</c> requests. None of them
/// carries a caller or module identity: identity always comes from the transport headers, exactly
/// as on the gRPC surface.
/// </remarks>
internal static class ModuleRestContracts
{
    /// <summary>Body of <c>POST /notifications</c>.</summary>
    /// <param name="Title">Notification title (bounded by <c>ModuleNotificationLimits</c>).</param>
    /// <param name="Body">Optional notification body.</param>
    /// <param name="Category">Optional category (for example <c>Reminder</c>).</param>
    /// <param name="Link">Optional site-relative action link.</param>
    /// <param name="RecipientUserIds">Recipient user ids.</param>
    public sealed record NotificationRequest(
        string? Title,
        string? Body,
        string? Category,
        string? Link,
        IReadOnlyList<string>? RecipientUserIds)
    {
        /// <summary>Maps the request onto the canonical message.</summary>
        /// <returns>The canonical request.</returns>
        public Api.SendNotificationRequest ToProto()
        {
            var request = new Api.SendNotificationRequest
            {
                Title = Title ?? string.Empty,
                Body = Body ?? string.Empty,
                Category = Category ?? string.Empty,
                Link = Link ?? string.Empty,
            };

            if (RecipientUserIds is not null)
            {
                request.RecipientUserIds.AddRange(RecipientUserIds);
            }

            return request;
        }
    }

    /// <summary>Body of <c>POST /events</c>.</summary>
    /// <param name="EventType">The platform event type name.</param>
    /// <param name="PayloadJson">The event payload as JSON.</param>
    public sealed record EventRequest(string? EventType, string? PayloadJson)
    {
        /// <summary>Maps the request onto the canonical message.</summary>
        /// <returns>The canonical request.</returns>
        public Api.PublishEventRequest ToProto() => new()
        {
            EventType = EventType ?? string.Empty,
            PayloadJson = PayloadJson ?? string.Empty,
        };
    }

    /// <summary>Body of <c>POST /search/index</c>.</summary>
    /// <param name="EntityId">The entity being indexed.</param>
    /// <param name="Action">0 = index/upsert, 1 = remove.</param>
    public sealed record SearchIndexRequest(string? EntityId, int Action)
    {
        /// <summary>Maps the request onto the canonical message.</summary>
        /// <returns>The canonical request.</returns>
        public Api.SubmitSearchIndexRequest ToProto() => new()
        {
            EntityId = EntityId ?? string.Empty,
            Action = Action,
        };
    }

    /// <summary>Body of <c>POST /realtime/broadcast</c>.</summary>
    /// <param name="Group">The real-time group to target.</param>
    /// <param name="EventName">The SignalR event name.</param>
    /// <param name="PayloadJson">The payload as JSON.</param>
    /// <param name="TargetUserId">Optional single-user target.</param>
    public sealed record RealtimeBroadcastRequest(
        string? Group,
        string? EventName,
        string? PayloadJson,
        string? TargetUserId)
    {
        /// <summary>Maps the request onto the canonical message.</summary>
        /// <returns>The canonical request.</returns>
        public Api.BroadcastRealtimeEventRequest ToProto() => new()
        {
            Group = Group ?? string.Empty,
            EventName = EventName ?? string.Empty,
            PayloadJson = PayloadJson ?? string.Empty,
            TargetUserId = TargetUserId ?? string.Empty,
        };
    }

    /// <summary>Body of <c>POST /audit</c>.</summary>
    /// <param name="Action">The numeric <c>AuditAction</c> value.</param>
    /// <param name="EntityType">The entity type.</param>
    /// <param name="EntityId">The entity id.</param>
    /// <param name="Description">Optional description.</param>
    public sealed record AuditRequest(int Action, string? EntityType, string? EntityId, string? Description)
    {
        /// <summary>Maps the request onto the canonical message.</summary>
        /// <returns>The canonical request.</returns>
        public Api.LogAuditRequest ToProto() => new()
        {
            Action = Action,
            EntityType = EntityType ?? string.Empty,
            EntityId = EntityId ?? string.Empty,
            Description = Description ?? string.Empty,
        };
    }

    /// <summary>Body of <c>PUT /settings/{key}</c>.</summary>
    /// <param name="Value">The value to store.</param>
    public sealed record SettingRequest(string? Value)
    {
        /// <summary>Maps the request onto the canonical message.</summary>
        /// <returns>The canonical request.</returns>
        public Api.SetSettingRequest ToProto() => new() { Value = Value ?? string.Empty };
    }
}
