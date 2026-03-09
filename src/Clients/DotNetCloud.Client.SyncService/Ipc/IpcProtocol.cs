using System.Text.Json.Serialization;

namespace DotNetCloud.Client.SyncService.Ipc;

/// <summary>IPC command names sent from client to service.</summary>
public static class IpcCommands
{
    /// <summary>List all registered sync contexts with their status.</summary>
    public const string ListContexts = "list-contexts";

    /// <summary>Add a new sync account and start its engine.</summary>
    public const string AddAccount = "add-account";

    /// <summary>Remove an existing sync account and stop its engine.</summary>
    public const string RemoveAccount = "remove-account";

    /// <summary>Get the current status of a single context.</summary>
    public const string GetStatus = "get-status";

    /// <summary>Pause automatic sync for a context.</summary>
    public const string Pause = "pause";

    /// <summary>Resume automatic sync for a context.</summary>
    public const string Resume = "resume";

    /// <summary>Trigger an immediate sync pass for a context.</summary>
    public const string SyncNow = "sync-now";

    /// <summary>Subscribe to push events on this connection.</summary>
    public const string Subscribe = "subscribe";

    /// <summary>Unsubscribe from push events on this connection.</summary>
    public const string Unsubscribe = "unsubscribe";
}

/// <summary>IPC event names pushed from service to subscribed clients.</summary>
public static class IpcEvents
{
    /// <summary>A sync pass is in progress.</summary>
    public const string SyncProgress = "sync-progress";

    /// <summary>A sync pass completed.</summary>
    public const string SyncComplete = "sync-complete";

    /// <summary>A sync conflict was detected and a conflict copy created.</summary>
    public const string ConflictDetected = "conflict-detected";

    /// <summary>A sync error occurred.</summary>
    public const string Error = "error";

    /// <summary>Per-file transfer progress (upload or download).</summary>
    public const string TransferProgress = "transfer-progress";

    /// <summary>A file transfer completed.</summary>
    public const string TransferComplete = "transfer-complete";
}

// ── Wire types ────────────────────────────────────────────────────────────────

/// <summary>
/// Newline-delimited JSON command sent from an IPC client to the service.
/// </summary>
public sealed class IpcCommand
{
    /// <summary>Command name (see <see cref="IpcCommands"/>).</summary>
    [JsonPropertyName("command")]
    public required string Command { get; init; }

    /// <summary>Context ID for context-scoped commands (null for global commands).</summary>
    [JsonPropertyName("contextId")]
    public Guid? ContextId { get; init; }

    /// <summary>
    /// Optional JSON payload for commands that carry additional data
    /// (e.g. <c>add-account</c> carries <see cref="AddAccountData"/>).
    /// </summary>
    [JsonPropertyName("data")]
    public System.Text.Json.JsonElement? Data { get; init; }
}

/// <summary>
/// Newline-delimited JSON message sent from the service to an IPC client.
/// Used for both command responses (<c>type=response</c>) and push events (<c>type=event</c>).
/// </summary>
public sealed class IpcMessage
{
    /// <summary><c>response</c> for replies to commands; <c>event</c> for push notifications.</summary>
    [JsonPropertyName("type")]
    public required string Type { get; init; }

    /// <summary>Echoed command name (for <c>type=response</c>).</summary>
    [JsonPropertyName("command")]
    public string? Command { get; init; }

    /// <summary>Event name (for <c>type=event</c>, see <see cref="IpcEvents"/>).</summary>
    [JsonPropertyName("event")]
    public string? Event { get; init; }

    /// <summary>Context ID for context-scoped events/responses.</summary>
    [JsonPropertyName("contextId")]
    public Guid? ContextId { get; init; }

    /// <summary><c>true</c> when the command succeeded.</summary>
    [JsonPropertyName("success")]
    public bool Success { get; init; }

    /// <summary>Error message when <see cref="Success"/> is <c>false</c>.</summary>
    [JsonPropertyName("error")]
    public string? Error { get; init; }

    /// <summary>Response payload or event data (type depends on command/event).</summary>
    [JsonPropertyName("data")]
    public object? Data { get; init; }
}

// ── Command payloads ──────────────────────────────────────────────────────────

/// <summary>Payload for the <c>add-account</c> command.</summary>
public sealed class AddAccountData
{
    /// <summary>Base URL of the DotNetCloud server.</summary>
    [JsonPropertyName("serverUrl")]
    public required string ServerUrl { get; init; }

    /// <summary>Authenticated user ID on the server.</summary>
    [JsonPropertyName("userId")]
    public required Guid UserId { get; init; }

    /// <summary>Absolute local folder path to synchronise.</summary>
    [JsonPropertyName("localFolderPath")]
    public required string LocalFolderPath { get; init; }

    /// <summary>Human-readable display name for the tray UI.</summary>
    [JsonPropertyName("displayName")]
    public required string DisplayName { get; init; }

    /// <summary>OAuth2 access token.</summary>
    [JsonPropertyName("accessToken")]
    public required string AccessToken { get; init; }

    /// <summary>OAuth2 refresh token (optional).</summary>
    [JsonPropertyName("refreshToken")]
    public string? RefreshToken { get; init; }

    /// <summary>UTC expiry time of the access token.</summary>
    [JsonPropertyName("expiresAt")]
    public DateTimeOffset ExpiresAt { get; init; }
}

// ── Response data types ───────────────────────────────────────────────────────

/// <summary>Context summary returned by the <c>list-contexts</c> command.</summary>
public sealed class ContextInfo
{
    /// <summary>Unique context identifier.</summary>
    [JsonPropertyName("id")]
    public required Guid Id { get; init; }

    /// <summary>Human-readable display name.</summary>
    [JsonPropertyName("displayName")]
    public required string DisplayName { get; init; }

    /// <summary>Server base URL.</summary>
    [JsonPropertyName("serverBaseUrl")]
    public required string ServerBaseUrl { get; init; }

    /// <summary>Absolute local sync folder path.</summary>
    [JsonPropertyName("localFolderPath")]
    public required string LocalFolderPath { get; init; }

    /// <summary>Current sync state string (e.g. <c>Idle</c>, <c>Syncing</c>, <c>Paused</c>).</summary>
    [JsonPropertyName("state")]
    public required string State { get; init; }

    /// <summary>Number of pending upload operations.</summary>
    [JsonPropertyName("pendingUploads")]
    public int PendingUploads { get; init; }

    /// <summary>Number of pending download operations.</summary>
    [JsonPropertyName("pendingDownloads")]
    public int PendingDownloads { get; init; }

    /// <summary>UTC timestamp of the last completed sync pass.</summary>
    [JsonPropertyName("lastSyncedAt")]
    public DateTime? LastSyncedAt { get; init; }

    /// <summary>Last error message (null when healthy).</summary>
    [JsonPropertyName("lastError")]
    public string? LastError { get; init; }
}

/// <summary>Payload for the <c>transfer-progress</c> push event.</summary>
public sealed class TransferProgressPayload
{
    /// <summary>File name (leaf only).</summary>
    [JsonPropertyName("fileName")]
    public string? FileName { get; init; }

    /// <summary><c>"upload"</c> or <c>"download"</c>.</summary>
    [JsonPropertyName("direction")]
    public string? Direction { get; init; }

    /// <summary>Bytes transferred so far.</summary>
    [JsonPropertyName("bytesTransferred")]
    public long BytesTransferred { get; init; }

    /// <summary>Total file size in bytes.</summary>
    [JsonPropertyName("totalBytes")]
    public long TotalBytes { get; init; }

    /// <summary>Chunks completed so far.</summary>
    [JsonPropertyName("chunksCompleted")]
    public int ChunksCompleted { get; init; }

    /// <summary>Total number of chunks.</summary>
    [JsonPropertyName("chunksTotal")]
    public int ChunksTotal { get; init; }

    /// <summary>Percentage complete (0–100).</summary>
    [JsonPropertyName("percentComplete")]
    public double PercentComplete { get; init; }
}

/// <summary>Payload for the <c>transfer-complete</c> push event.</summary>
public sealed class TransferCompletePayload
{
    /// <summary>File name (leaf only).</summary>
    [JsonPropertyName("fileName")]
    public string? FileName { get; init; }

    /// <summary><c>"upload"</c> or <c>"download"</c>.</summary>
    [JsonPropertyName("direction")]
    public string? Direction { get; init; }

    /// <summary>Total bytes transferred.</summary>
    [JsonPropertyName("totalBytes")]
    public long TotalBytes { get; init; }
}
