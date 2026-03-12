# Chat Push Notifications

## Overview

The Chat module supports push notifications for offline users via two providers: **Firebase Cloud Messaging (FCM)** for Google Play builds and **UnifiedPush** for F-Droid/self-hosted deployments. The notification pipeline includes user preference enforcement, deduplication for online users, and an automatic retry queue with exponential backoff.

## Architecture

```
Message Sent / @Mention
       │
       ▼
MentionNotificationService
       │
       ├─── SignalR (online users get real-time delivery)
       │
       └─── NotificationRouter (offline users)
                │
                ├── Check user preferences
                │     ├── Push enabled?
                │     ├── DND mode active?
                │     └── Channel muted?
                │
                ├── Skip if user received via SignalR
                │
                ├── Route by device provider
                │     ├── FcmPushProvider → Firebase HTTP v1 API
                │     └── UnifiedPushProvider → HTTP POST to distributor
                │
                └── On failure → NotificationDeliveryQueue
                                    └── Background retry worker
```

## Notification Categories

| Category | Trigger | Priority |
|---|---|---|
| `ChatMessage` | New message in channel | Normal |
| `ChatMention` | User was @mentioned | High |
| `Announcement` | New announcement published | Normal (Important/Urgent raises priority) |
| `FileShared` | File shared with user | Normal |
| `System` | System notifications | Low |

## Providers

### Firebase Cloud Messaging (FCM)

Used by the Google Play build flavor of the Android app.

**Configuration:**

```json
{
  "Chat": {
    "Push": {
      "Fcm": {
        "Enabled": true,
        "ProjectId": "your-firebase-project-id",
        "CredentialsPath": "/path/to/firebase-admin-sdk.json"
      }
    }
  }
}
```

| Setting | Type | Description |
|---|---|---|
| `Enabled` | `bool` | Enable/disable FCM provider |
| `ProjectId` | `string` | Firebase project identifier |
| `CredentialsPath` | `string` | Path to Firebase Admin SDK service account JSON |

**Behavior:**
- Sends via Firebase HTTP v1 API.
- Maintains per-user device token registry.
- Auto-detects invalid/expired tokens and removes them.
- Supports notification + data payloads.

### UnifiedPush

Used by the F-Droid build flavor and self-hosted deployments. No Google dependency.

**Configuration:**

```json
{
  "Chat": {
    "Push": {
      "UnifiedPush": {
        "Enabled": true,
        "MaxRetryAttempts": 3,
        "RetryDelaySeconds": 30
      }
    }
  }
}
```

| Setting | Type | Default | Description |
|---|---|---|---|
| `Enabled` | `bool` | `true` | Enable/disable UnifiedPush provider |
| `MaxRetryAttempts` | `int` | `3` | Maximum delivery retry attempts |
| `RetryDelaySeconds` | `int` | `30` | Base delay between retries |

**Behavior:**
- Sends HTTP POST requests to the distributor endpoint registered by each device.
- Compatible with ntfy, Gotify UP, and other UnifiedPush distributors.
- Distinguishes transient failures (retry) from permanent failures (discard).
- Per-device endpoint URLs stored in device registration.

## Device Registration

### Register

```
POST /api/v1/notifications/devices/register?userId={userId}
```

```json
{
  "deviceToken": "fcm-token-or-unified-push-id",
  "provider": "FCM",
  "endpoint": null
}
```

| Field | Required | Description |
|---|---|---|
| `deviceToken` | Yes | FCM token or UnifiedPush identifier |
| `provider` | Yes | `FCM` or `UnifiedPush` |
| `endpoint` | For UP | UnifiedPush distributor endpoint URL |

### Unregister

```
DELETE /api/v1/notifications/devices/{deviceToken}?userId={userId}
```

### Client-Side Registration

**Android (GooglePlay):**

```csharp
// FcmPushService.RegisterAsync()
var token = await FirebaseMessaging.Instance.GetTokenAsync();
// POST to /api/v1/notifications/devices/register with provider="FCM"
```

**Android (F-Droid):**

```csharp
// UnifiedPushService.RegisterAsync()
// Receives endpoint URL from UnifiedPushReceiver broadcast
// POST to /api/v1/notifications/devices/register with provider="UnifiedPush"
```

## User Preferences

### Get Preferences

```
GET /api/v1/notifications/preferences?userId={userId}
```

### Update Preferences

```
PUT /api/v1/notifications/preferences?userId={userId}
```

```json
{
  "pushEnabled": true,
  "doNotDisturb": false,
  "mutedChannelIds": ["channel-guid-1", "channel-guid-2"]
}
```

| Setting | Type | Default | Description |
|---|---|---|---|
| `pushEnabled` | `bool` | `true` | Master push toggle |
| `doNotDisturb` | `bool` | `false` | Suppress all push notifications when active |
| `mutedChannelIds` | `Guid[]` | `[]` | Channels with suppressed notifications |

### Per-Channel Notification Preferences

Individual channel notification preferences are separate from push preferences:

```
PUT /api/v1/chat/channels/{channelId}/notifications?userId={userId}
```

```json
{
  "preference": "Mentions"
}
```

| Value | Behavior |
|---|---|
| `All` | Receive notifications for all messages |
| `Mentions` | Only receive notifications for @mentions |
| `None` | No notifications from this channel |

## Delivery Pipeline

### Notification Payload

```csharp
public sealed record PushNotification
{
    public required string Title { get; init; }
    public required string Body { get; init; }
    public string? ImageUrl { get; init; }
    public Dictionary<string, string> Data { get; init; } = [];
    public NotificationCategory Category { get; init; }
}
```

### Delivery Flow

1. **Event occurs** — Message sent, @mention detected, or announcement published.
2. **Mention dispatch** — `MentionNotificationService` identifies recipients (excluding sender).
3. **Online check** — If user is connected via SignalR, skip push (real-time delivery).
4. **Preference check** — Verify push enabled, not in DND, channel not muted.
5. **Provider routing** — Route to FCM or UnifiedPush based on registered device provider.
6. **Send** — Attempt delivery via provider API.
7. **On failure** — Enqueue to `INotificationDeliveryQueue` for retry.

### Retry Queue

Failed notifications are queued in an in-memory `System.Threading.Channels`-based queue and retried by the `NotificationDeliveryBackgroundService`:

| Setting | Value | Description |
|---|---|---|
| Queue type | Single-reader, multi-writer | Lock-free async channel |
| Retry strategy | Exponential backoff | Delay doubles with each attempt |
| Max retries | Provider-configurable | Default: 3 for UnifiedPush |
| Permanent failure | Token invalid, endpoint gone | Device registration auto-cleaned |

### Deduplication

The `NotificationRouter` skips push delivery when a user is currently connected via SignalR, avoiding duplicate notifications for online users who already received real-time events.

## Desktop Client Integration

The SyncTray desktop client also receives chat notifications via SignalR (not push). The tray icon shows:

| State | Badge |
|---|---|
| No unreads | No overlay |
| Unread messages | Amber overlay badge |
| Unread @mentions | Red overlay badge |

Notifications respect the `IsMuteChatNotifications` setting in `sync-tray-settings.json`.

## Troubleshooting

| Issue | Cause | Fix |
|---|---|---|
| No push notifications | Push disabled in preferences | Check `GET /api/v1/notifications/preferences` |
| No push despite enabled | DND mode active | Check `doNotDisturb` preference |
| Channel notifications silent | Channel muted | Check `mutedChannelIds` or per-channel pref |
| FCM token errors | Expired or invalid token | Re-register device; provider auto-cleans |
| UnifiedPush not working | Distributor endpoint changed | Re-register with new endpoint |
