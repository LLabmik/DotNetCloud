# Chat "is typing…" Indicator (Blazor Web + Android MAUI)

**Status:** implemented on `fix/chat-improvements` (2026-09-07) — code complete, server + Blazor + Android.
**Verification:** unit tests updated/passing; live cross-device E2E still to be run on the server + device (see below).
**2026-09-09 fix (`fix/android-improvements`):** the typist was seeing their own "X is typing…" on Android.
Root cause: the OIDC **access token is JWE-encrypted** and can't be decoded client-side, so
`MessageListViewModel` failed to resolve `_currentUserId` from it (`sub` extraction threw → `Guid.Empty`),
which defeated the self-echo filter (`e.UserId == _currentUserId`). Blazor was unaffected because it derives
the user id from the server caller context. Fixes:

- Android client: `_currentUserId` is now resolved from the signed **id_token** `sub` (access token as fallback).
- Server (defense-in-depth): `Core.Server` no longer echoes a `TypingIndicator` broadcast back to the
  connections of the typing user (`RealtimeBroadcasterService.BroadcastToGroupExceptUserAsync`, used by the
  module→Core gRPC path in `GrpcHealthServiceImpl`). The typist's own devices never receive their heartbeat;
  other members still do.
- Verification: Android 269 tests + Core.Server broadcaster/hub tests green. Fixed APK installed on device
  (`R5CWC356B2K`); live re-test (typer must NOT see their own indicator, other users must) pending.

## Goal

Show "X is typing…" in chat when another member is typing — in the Blazor web chat
(`src/Modules/Chat/DotNetCloud.Modules.Chat/UI`) and the Android MAUI chat
(`src/Clients/DotNetCloud.Client.Android`).

## Why it was missing

Both clients already _sent_ typing notifications (Blazor via the in-process module
`ITypingIndicatorService`, Android via REST `POST /api/v1/chat/channels/{id}/typing`), and the
server had `ChatHub.StartTypingAsync/StopTypingAsync` + an in-memory `TypingIndicatorService`.
But **nothing broadcast the typing state to other members**, and neither client displayed it:

- Module-host REST typing only recorded state locally (never broadcast).
- `Core.Server` gRPC event forwarding (`TryForwardToChatMessageNotifier`) only forwarded
  `NewMessage`/`MessageEdited`/`MessageDeleted` — no `TypingIndicator` case.
- `IChatMessageNotifier` had no typing event, so Blazor circuits never learned about typing.
- Android `SignalRChatClient` registered no `"TypingIndicator"` hub handler, and
  `MessageListViewModel` had no indicator state/UI.

## Design (heartbeat model)

- The server broadcasts `TypingIndicator` `{ channelId, userId, displayName? }` to the
  `chat-channel-{id}` SignalR group.
- Receivers treat **every** `TypingIndicator` event as a heartbeat → add/refresh the user.
  The indicator hides when:
  1. no heartbeat has arrived for ~5 s (receiver-side expiry pruner), or
  2. a `NewMessage` from that user arrives.
- `displayName` is optional; when null receivers resolve the display name from their own
  channel member list (no per-heartbeat name lookup on the server).
- Senders throttle to ~1 heartbeat / 2.5 s while composing.
- **The typist never sees their own indicator.** Two layers enforce this:
  1. Server: a `TypingIndicator` broadcast excludes the typing user's own connections
     (`GroupExcept` via the connection tracker), so the typist's devices are never told they're typing.
  2. Clients: each client additionally ignores any heartbeat whose `userId` equals its own
     `_currentUserId` (defense for the in-process Blazor notifier path, which can't be per-user targeted).

## Changes

### Server / shared

| File                                                                           | Change                                                                                                                                                                                                                                    |
| ------------------------------------------------------------------------------ | ----------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| `src/Modules/Chat/DotNetCloud.Modules.Chat/Services/IChatMessageNotifier.cs`   | New `ChatTypingNotification(Guid ChannelId, Guid UserId, string? DisplayName)`; `event Action<ChatTypingNotification>? TypingChanged`; `NotifyTypingChanged(...)` on interface + `InProcessChatMessageNotifier`.                          |
| `src/Core/DotNetCloud.Core.Server/Grpc/Services/GrpcHealthServiceImpl.cs`      | `TryForwardToChatMessageNotifier` now forwards `"TypingIndicator"` events to `IChatMessageNotifier` (so process-isolated module-host typing reaches Blazor circuits).                                                                     |
| `src/Core/DotNetCloud.Core.Server/RealTime/ChatHub.cs`                         | Optional `IChatMessageNotifier` ctor arg (alias `ChatMessageNotifier`); `StartTypingAsync` also mirrors the heartbeat to the in-process notifier.                                                                                         |
| `src/Modules/Chat/DotNetCloud.Modules.Chat.Host/Controllers/ChatController.cs` | `POST .../typing` now also calls `_chatRealtimeService.BroadcastTypingAsync(channelId, caller.UserId, null)` after recording, so other members receive the heartbeat (module host → gRPC → Core.Server → SignalR + Blazor notifier).      |
| `src/Core/DotNetCloud.Core.Server/RealTime/RealtimeBroadcasterService.cs`      | New `BroadcastToGroupExceptUserAsync(group, excludedUserId, eventName, message, ct)` — group send minus all of one user's connections (via `UserConnectionTracker` + `Clients.GroupExcept`).                                              |
| `src/Core/DotNetCloud.Core.Server/Grpc/Services/GrpcHealthServiceImpl.cs`      | `BroadcastRealtimeEvent` group broadcasts of `"TypingIndicator"` now exclude the typing user's own connections (parses `userId` from the payload), so the typist is never told they're typing; other members still receive the heartbeat. |

### Blazor chat UI

- `ChatPageLayout.razor.cs`: typing state (`_typingUsers`, `_typingState`, prune timer);
  subscribes to `TypingChanged`; handler updates/resolves names; expiry pruner (~5 s);
  clears typing on channel switch + when a message from the typist arrives.
  `HandleTyping` now records via `TypingService`, broadcasts via `ChatRealtimeService.BroadcastTypingAsync`
  and mirrors to the in-process notifier, throttled to ~1/2.5 s.
- `MessageList.razor` / `MessageList.razor.css`: animated-dot + text indicator pinned to the
  bottom of the message list.

### Android MAUI chat

- `src/Clients/DotNetCloud.Client.Core/IChatSignalRClient.cs`: new `ChatTypingEventArgs` record.
- `src/Clients/DotNetCloud.Client.Android/Services/ICoreHubClient.cs`: new `OnChatTyping` event.
- `src/Clients/DotNetCloud.Client.Android/Chat/SignalRChatClient.cs`: `TypingIndicatorPayload`;
  registers `_hub.On<TypingIndicatorPayload>("TypingIndicator", …)` and raises `OnChatTyping`.
- `src/Clients/DotNetCloud.Client.Android/ViewModels/MessageListViewModel.cs`: depends on
  `ICoreHubClient`; exposes `TypingIndicatorText`; sends typing heartbeats while composing
  (immediate + every 2.5 s; stops when the composer clears); shows/resolves remote typers,
  prunes after 5 s, clears when their message arrives.
- `src/Clients/DotNetCloud.Client.Android/Views/MessageListPage.xaml`: typing label above the composer.

## Tests

- `ChatHubTests`: `WhenStartTypingCalledThenForwardsToInProcessChatNotifier`.
- `ChatControllerTests`: `NotifyTypingAsync_WhenSuccessful_ThenBroadcastsTypingHeartbeat`.
- `MessageListViewModelTests`: 8 new typing tests (resolve name, provided name, two users,
  self ignored, wrong channel, message clears typing, heartbeat start/stop).

## Verification steps (live)

Server (`dotnetcloud` on cloud host, deployed by the server agent):

1. Build + `dotnet test` the affected suites; deploy.
2. Browser: two users in one channel. User A types → User B sees "A is typing…"; A sends →
   indicator hides; indicator disappears ≤5 s after A stops typing without sending.

Android (physical phone, client agent):

1. Build arm64 APK (`-r android-arm64`), install on device.
2. Two devices/users in the same channel: typing from the other shows "… is typing…" above
   the composer; hides on their message or after ~5 s idle.

Known limits: typing is best-effort (network/perf); no presence/away semantics involved.
