# Tracks — Mobile Push Notifications Plan

> Status: **DEFERRED** — Excluded from Tracks Professionalization Phases D–I
> Reference: `docs/TRACKS_REMAINING_GAPS_PLAN.md`
> Date: April 30, 2026

---

## Why Deferred

Mobile push notifications require infrastructure outside the Tracks module: Firebase Cloud Messaging (FCM) for Android, Apple Push Notification Service (APNs) for iOS, device token management, and MAUI app integration. This is a separate domain from the Tracks web UI and justifies its own dedicated development phase. All other 17 gaps in the professionalization plan can be completed without mobile push.

---

## What's Needed

### Server-Side

**Entities:**
- `DeviceToken` — UserId, Platform (Android/iOS), Token string, DeviceName, CreatedAt, LastUsedAt

**Services:**
- `PushNotificationService` — register/unregister device tokens, send push payloads to FCM/APNs
- Extend `TracksNotificationService` — also send push when firing work item notifications

**Infrastructure:**
- FCM HTTP v1 API integration (service account key)
- APNs token-based authentication (p8 key + key ID + team ID)
- Queue/batch: send push asynchronously, don't block the request

**Payload design:**
```json
{
  "title": "New assignment: Fix login bug",
  "body": "Ben assigned you PROJ-142 in Mobile App",
  "data": {
    "action": "open_work_item",
    "workItemId": "abc123",
    "productId": "def456"
  }
}
```

### Notification Triggers (push in addition to in-app)

| Event | Push Recipient |
|-------|---------------|
| Work item assigned | Assignee |
| @mentioned in comment | Mentioned user(s) |
| Sprint started | All product members |
| Sprint completed | All product members |
| Due date approaching (24h) | Assignee |
| Watched item updated | All watchers |

### Client-Side (MAUI Android App)

- Register device token on app startup (Firebase SDK)
- Handle incoming push: extract action + work item ID
- Tap notification → deep link to Tracks web view showing the work item
- Settings toggle: enable/disable push per notification type
- Handle token refresh (FCM token rotation)

### Client-Side (iOS — Future)

- APNs registration via `UIApplication.RegisterForRemoteNotifications`
- Same deep-link handling as Android

---

## Effort Estimate

| Component | Est. Hours |
|-----------|------------|
| DeviceToken entity + EF + migration | 0.5 |
| PushNotificationService (FCM + APNs) | 4 |
| Extend TracksNotificationService | 1 |
| FCM project setup + service account | 1 |
| APNs certificate setup | 1 |
| MAUI Android: Firebase SDK integration | 3 |
| MAUI Android: notification handling + deep link | 2 |
| MAUI iOS: APNs registration + handling | 2 |
| Settings UI (toggle per notification type) | 1 |
| Testing: FCM, APNs, deep link, settings | 2 |
| **Total** | **~17.5 hours** |

---

## Dependencies

- Firebase project must be created (one-time setup)
- Apple Developer account with APNs key (one-time setup)
- Tracks module must be deployed and accessible via public URL (for deep links)
- MAUI app must have internet permission and Firebase config file

---

## When to Implement

Recommended **after Phase G** (Planning & Visualization) when automation rules can trigger notifications and after the roadmap/goals give users things to be notified about. Could also be done after Phase I completes the full Tracks feature set.

---

## Related Documents

- `docs/TRACKS_REMAINING_GAPS_PLAN.md` — Phases D–I implementation
- `docs/TRACKS_PROFESSIONALIZATION_PLAN.md` — Phases A–C (completed)
- `src/Clients/DotNetCloud.Client.Android/` — MAUI Android app
