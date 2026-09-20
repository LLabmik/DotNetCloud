# Chat Push Notifications

## Overview

The Chat module delivers push notifications for offline users through a single server-side provider: **Firebase Cloud Messaging (FCM)**. The notification pipeline includes user preference enforcement, deduplication for online users, and an automatic retry queue with exponential backoff.

Mobile clients do not depend on that pipeline for background alerts: the Android app polls `GET /api/v1/chat/alerts` itself and renders the notification locally. See [Mobile Background Chat Alerts](#mobile-background-chat-alerts).

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
                │     └── FcmPushProvider → Firebase HTTP v1 API
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

The only server-side push provider. It delivers to any client that has registered an FCM token; the Android client does not register one today (see [Mobile Background Chat Alerts](#mobile-background-chat-alerts)).

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

## Mobile Background Chat Alerts

The Android client does not receive server-initiated push. While the app is not running it learns about new messages from its own background poll of:

```
GET /api/v1/chat/alerts
```

The response is a constant-cost aggregate of counts only — `unread`, `mentions`, `unmutedUnread`, `unmutedMentions`, `topChannelId` and `changedAt` — returned with an `ETag`. The app sends `If-None-Match` on every poll, so a `304 Not Modified` means nothing changed. **No message text, sender name or channel name ever leaves the server.**

| Poll state | Next poll |
|---|---|
| Unmuted unread messages outstanding | 60 s |
| Nothing unread | 300 s |
| Device dozing | ~9 min (exact-alarm path) |

The poll runs as a `JobScheduler` job (id `3108`, persisted, any network) that re-arms itself after each run. When the device is dozing and the user has granted exact-alarm access, an allow-while-idle alarm is used instead. There is no foreground service and no persistent notification.

### Notification Text

Notification wording is chosen on the device, never by the server: an unread message renders as **"New message"** and an unread mention as **"You were mentioned"**. Muted channels never raise an alert.

## Device Registration

### Register

```
POST /api/v1/notifications/devices/register?userId={userId}
```

```json
{
  "deviceToken": "fcm-registration-token",
  "provider": "FCM"
}
```

| Field | Required | Description |
|---|---|---|
| `deviceToken` | Yes | FCM registration token |
| `provider` | Yes | `FCM` (the only supported provider) |

### Unregister

```
DELETE /api/v1/notifications/devices/{deviceToken}?userId={userId}
```

### Client-Side Registration

A client that holds a push token registers it with `provider: "FCM"` and removes it again with `DELETE /api/v1/notifications/devices/{deviceToken}` when the token is no longer valid.

**Android:** the Android client currently registers **no** push device at all — it holds no push token, so no device entry exists for it on the server. Its background chat alerts are produced locally by the poll described in [Mobile Background Chat Alerts](#mobile-background-chat-alerts).

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
5. **Provider routing** — The registered device's provider selects the push endpoint; `FCM` is the only provider.
6. **Send** — Attempt delivery via provider API.
7. **On failure** — Enqueue to `INotificationDeliveryQueue` for retry.

### Retry Queue

Failed notifications are queued in an in-memory `System.Threading.Channels`-based queue and retried by the `NotificationDeliveryBackgroundService`:

| Setting | Value | Description |
|---|---|---|
| Queue type | Single-reader, multi-writer | Lock-free async channel |
| Retry strategy | Exponential backoff | Delay doubles with each attempt, capped at 30 s |
| Max retries | 3 attempts | Fixed in `NotificationDeliveryBackgroundService` |
| Permanent failure | Token invalid | Device registration auto-cleaned |

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
| No Android background alert | Poll job not scheduled (no saved session, or battery optimisation) | Check job `3108` is registered and the app has a saved session |
