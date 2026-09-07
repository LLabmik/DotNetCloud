# Chat "is typing…" Indicator (Blazor Web + Android MAUI)

**Status:** implemented on `fix/chat-improvements` (2026-09-07) — code complete, server + Blazor + Android.
**Verification:** unit tests updated/passing; live cross-device E2E still to be run on the server + device (see below).

## Goal

Show "X is typing…" in chat when another member is typing — in the Blazor web chat
(`src/Modules/Chat/DotNetCloud.Modules.Chat/UI`) and the Android MAUI chat
(`src/Clients/DotNetCloud.Client.Android`).

## Why it was missing

Both clients already *sent* typing notifications (Blazor via the in-process module
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

## Changes

### Server / shared

| File | Change |
| --- | --- |
| `src/Modules/Chat/DotNetCloud.Modules.Chat/Services/IChatMessageNotifier.cs` | New `ChatTypingNotification(Guid ChannelId, Guid UserId, string? DisplayName)`; `event Action<ChatTypingNotification>? TypingChanged`; `NotifyTypingChanged(...)` on interface + `InProcessChatMessageNotifier`. |
| `src/Core/DotNetCloud.Core.Server/Grpc/Services/GrpcHealthServiceImpl.cs` | `TryForwardToChatMessageNotifier` now forwards `"TypingIndicator"` events to `IChatMessageNotifier` (so process-isolated module-host typing reaches Blazor circuits). |
| `src/Core/DotNetCloud.Core.Server/RealTime/ChatHub.cs` | Optional `IChatMessageNotifier` ctor arg (alias `ChatMessageNotifier`); `StartTypingAsync` also mirrors the heartbeat to the in-process notifier. |
| `src/Modules/Chat/DotNetCloud.Modules.Chat.Host/Controllers/ChatController.cs` | `POST .../typing` now also calls `_chatRealtimeService.BroadcastTypingAsync(channelId, caller.UserId, null)` after recording, so other members receive the heartbeat (module host → gRPC → Core.Server → SignalR + Blazor notifier). |

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
