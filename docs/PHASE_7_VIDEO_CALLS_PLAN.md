# Phase 7: Video Calls — WebRTC Video Calling & Screen Sharing

## Overview

Extend the existing Chat module with WebRTC-based video calling and screen sharing. P2P mesh for 1-3 participants, optional LiveKit SFU for 4+. All signaling flows through the existing SignalR `CoreHub`. No new module — everything extends Chat's models, services, events, gRPC, API, and Blazor UI.

## Architecture Decisions

| Decision | Choice | Rationale |
|----------|--------|-----------|
| Module location | Built into Chat module | Calls originate from channels/DMs; avoids redundant module shell; signaling collocated with messaging |
| WebRTC utilities | SIPSorcery (.NET, BSD-3) | Server-side SDP parsing, STUN/TURN helpers |
| Browser WebRTC | JS interop (RTCPeerConnection, getUserMedia, getDisplayMedia) | Browser-native API, no extra dependencies |
| Signaling transport | SignalR (existing CoreHub) | Already handles real-time messaging, presence, group delivery |
| Small calls (1-3) | P2P mesh | No server media relay needed; lowest latency |
| Large calls (4+) | LiveKit SFU (optional, Apache 2.0) | Managed component under process supervisor |
| STUN/TURN | Built-in STUN server (UDP 3478) + optional coturn TURN | Privacy-first: no third-party STUN by default; configurable TURN for NAT traversal |
| Calls per channel | One active at a time | Simplifies state management |
| Ring timeout | 30 seconds → auto-missed | Standard UX convention |

## Scope

### Included

- 1:1 and group video/audio calls from any channel type (Public, Private, DM, Group)
- Screen sharing (browser only)
- Call history per channel
- Push notifications for incoming/missed calls (FCM + UnifiedPush)
- LiveKit integration for large group calls (optional)
- STUN/TURN configuration with ephemeral TURN credentials

### Excluded (Future Work)

- Desktop app native screen sharing (Avalonia/platform-specific)
- Call recording / transcription
- Virtual backgrounds
- Breakout rooms
- Phone/PSTN dial-in
- End-to-end encrypted calls (Phase 10 — E2EE)

---

## Steps

### Phase 7.1 — Architecture & Contracts

**Depends on:** Chat module (Phase 2, complete)

**Status:** completed ✅

**Deliverables:**
- ✓ `VideoCallState` enum: `Ringing`, `Connecting`, `Active`, `OnHold`, `Ended`, `Missed`, `Rejected`, `Failed`
- ✓ `VideoCallEndReason` enum: `Normal`, `Rejected`, `Missed`, `TimedOut`, `Failed`, `Cancelled`
- ✓ `CallParticipantRole` enum: `Initiator`, `Participant`
- ✓ `CallMediaType` enum: `Audio`, `Video`, `ScreenShare`
- ✓ DTOs in `ChatDtos.cs`: `VideoCallDto`, `CallParticipantDto`, `CallSignalDto` (SDP offer/answer/ICE), `StartCallRequest`, `JoinCallRequest`, `CallHistoryDto`
- ✓ Events: `VideoCallInitiatedEvent`, `VideoCallAnsweredEvent`, `VideoCallEndedEvent`, `VideoCallMissedEvent`, `ParticipantJoinedCallEvent`, `ParticipantLeftCallEvent`, `ScreenShareStartedEvent`, `ScreenShareEndedEvent`
- ✓ Service interface: `IVideoCallService` — `InitiateCallAsync`, `JoinCallAsync`, `LeaveCallAsync`, `EndCallAsync`, `RejectCallAsync`, `GetCallHistoryAsync`, `GetActiveCallAsync`
- ✓ Service interface: `ICallSignalingService` — `SendOfferAsync`, `SendAnswerAsync`, `SendIceCandidateAsync`, `SendMediaStateChangeAsync`
- ✓ Update `ChatModuleManifest.cs` — add new published events (8 video call events added)

**Files to modify:**
- `src/Modules/Chat/DotNetCloud.Modules.Chat/Models/` — new enum + entity files
- `src/Modules/Chat/DotNetCloud.Modules.Chat/DTOs/ChatDtos.cs` — add call DTOs
- `src/Modules/Chat/DotNetCloud.Modules.Chat/Events/` — new event files
- `src/Modules/Chat/DotNetCloud.Modules.Chat/Services/` — new interface files
- `src/Modules/Chat/DotNetCloud.Modules.Chat/ChatModuleManifest.cs` — update events

---

### Phase 7.2 — Data Model & Migration

**Depends on:** 7.1

**Status:** completed ✅

**Deliverables:**
- ✓ `VideoCall` entity: Id, ChannelId (FK → Channel), InitiatorUserId, State, MediaType, StartedAtUtc, EndedAtUtc, EndReason, MaxParticipants, IsGroupCall, LiveKitRoomId (nullable), CreatedAtUtc
- ✓ `CallParticipant` entity: Id, VideoCallId (FK → VideoCall), UserId, Role, JoinedAtUtc, LeftAtUtc, HasAudio, HasVideo, HasScreenShare
- ✓ EF configurations: `VideoCallConfiguration.cs`, `CallParticipantConfiguration.cs` — indexes on ChannelId+State, UserId+JoinedAt
- ✓ `ChatDbContext` — add `DbSet<VideoCall>` and `DbSet<CallParticipant>`
- ✓ EF migration: `AddVideoCalling`
- ✓ Soft-delete support on VideoCall (consistent with existing Chat patterns)

**Files to modify:**
- `src/Modules/Chat/DotNetCloud.Modules.Chat/Models/` — `VideoCall.cs`, `CallParticipant.cs`
- `src/Modules/Chat/DotNetCloud.Modules.Chat.Data/ChatDbContext.cs` — add DbSets
- `src/Modules/Chat/DotNetCloud.Modules.Chat.Data/Configuration/` — new config files
- `src/Modules/Chat/DotNetCloud.Modules.Chat.Data/Migrations/` — new migration

---

### Phase 7.3 — Call Management Service ✅

**Depends on:** 7.2
**Status:** Completed

**Deliverables:**
- ✓ `VideoCallService` (implements `IVideoCallService`) in Chat.Data/Services:
  - `InitiateCallAsync` — Create VideoCall record, add initiator as participant, set state=Ringing, publish `VideoCallInitiatedEvent`, send ringing notification to channel members via `IRealtimeBroadcaster`
  - `JoinCallAsync` — Add participant, transition state Ringing→Connecting→Active (on first answer), publish `VideoCallAnsweredEvent`/`ParticipantJoinedCallEvent`
  - `LeaveCallAsync` — Mark participant LeftAtUtc, publish `ParticipantLeftCallEvent`, auto-end call if last participant leaves
  - `EndCallAsync` — Set state=Ended, EndReason, EndedAtUtc, broadcast to all participants, publish `VideoCallEndedEvent`
  - `RejectCallAsync` — For 1:1 calls: set state=Rejected; for group: just don't join
  - `GetCallHistoryAsync` — Paginated call history for a channel, ordered by CreatedAtUtc desc
  - `GetActiveCallAsync` — Get active call for a channel (at most one active call per channel)
- ✓ Call timeout: `HandleRingTimeoutsAsync` — if Ringing for >30s with no answer → state=Missed, publish `VideoCallMissedEvent`
- ✓ `CallStateValidator` — State machine enforcement (valid transitions only)
- ✓ Register service in `ChatServiceRegistration.cs`
- ✓ 110 comprehensive tests (39 CallStateValidator + 71 VideoCallService)

**State Machine:**

```
                 ┌──────────┐
                 │  Ringing  │
                 └────┬──┬──┘
            answered  │  │  no answer (30s)
                      │  │
              ┌───────▼┐ └──────────┐
              │Connecting│           │
              └────┬────┘    ┌──────▼──────┐
                   │         │   Missed     │
              ┌────▼────┐    └─────────────┘
              │  Active  │
              └────┬──┬──┘
         all leave │  │ explicit end
                   │  │
              ┌────▼──▼──┐
              │   Ended   │
              └───────────┘

  Ringing ──reject──→ Rejected
  Any ──────error───→ Failed
```

**Files to modify:**
- `src/Modules/Chat/DotNetCloud.Modules.Chat.Data/Services/VideoCallService.cs` — new
- `src/Modules/Chat/DotNetCloud.Modules.Chat/Services/CallStateValidator.cs` — new
- `src/Modules/Chat/DotNetCloud.Modules.Chat.Data/ChatServiceRegistration.cs` — register

---

### Phase 7.4 — WebRTC Signaling over SignalR

**Depends on:** 7.3

**Status:** completed ✅

**Deliverables:**
- ✓ Extend `CoreHub.cs` with video call signaling methods:
  - `SendCallOfferAsync(callId, targetUserId, sdpOffer)` — Relay SDP offer to target
  - `SendCallAnswerAsync(callId, targetUserId, sdpAnswer)` — Relay SDP answer back
  - `SendIceCandidateAsync(callId, targetUserId, candidate)` — Relay ICE candidate
  - `SendMediaStateChangeAsync(callId, mediaType, enabled)` — Notify peers of mute/camera toggle
  - `JoinCallGroupAsync(callId)` / `LeaveCallGroupAsync(callId)` — Call group management
- ✓ Call-scoped SignalR groups: `"call-{callId}"` — participants auto-join on call accept, auto-leave on call end
- ✓ `CallSignalingService` (implements `ICallSignalingService`) — Server-side signaling coordinator, validates call state before relaying signals
- ✓ Input validation: sanitize SDP and ICE candidate payloads, enforce size limits (SDP max 64KB, ICE candidate max 4KB), validate call membership before relaying
- ✓ 62 CallSignalingService unit tests + 23 CoreHub signaling unit tests (85 total)

**Signaling Flow (P2P):**

```
  Caller                    CoreHub (SignalR)                  Callee
    │                            │                               │
    │── SendCallOffer ──────────►│                               │
    │                            │── ReceiveCallOffer ──────────►│
    │                            │                               │
    │                            │◄── SendCallAnswer ────────────│
    │◄── ReceiveCallAnswer ──────│                               │
    │                            │                               │
    │── SendIceCandidate ───────►│                               │
    │                            │── ReceiveIceCandidate ───────►│
    │                            │                               │
    │                            │◄── SendIceCandidate ──────────│
    │◄── ReceiveIceCandidate ────│                               │
    │                            │                               │
    │◄═══════════ P2P Media Stream (direct) ═══════════════════►│
```

**Files to modify:**
- `src/Core/DotNetCloud.Core.Server/RealTime/CoreHub.cs` — add call signaling methods
- `src/Modules/Chat/DotNetCloud.Modules.Chat/Services/CallSignalingService.cs` — new
- `src/Core/DotNetCloud.Core.Server/Extensions/SignalRServiceExtensions.cs` — if config changes needed

---

### Phase 7.5 — Client-Side WebRTC Engine (JS Interop)

**Depends on:** 7.4

**Status:** completed ✅

**Deliverables:**
- ✓ `video-call.js` — JS interop module for browser WebRTC API:
  - `initializeCall(config)` — Create RTCPeerConnection with STUN/TURN config
  - `startLocalMedia(constraints)` — `getUserMedia` for camera + mic
  - `startScreenShare()` — `getDisplayMedia` for screen capture
  - `stopScreenShare()` — Replace screen track with camera track
  - `createOffer()` / `handleOffer(sdp)` / `createAnswer()` / `handleAnswer(sdp)` — SDP negotiation
  - `addIceCandidate(candidate)` — ICE candidate handling
  - `toggleAudio(enabled)` / `toggleVideo(enabled)` — Track enable/disable
  - `hangup()` — Close peer connections, stop all tracks, cleanup
  - `onRemoteStream(callback)` — Notify Blazor when remote streams arrive
  - `onIceCandidate(callback)` — Forward ICE candidates to SignalR
  - `onConnectionStateChange(callback)` — Peer connection state events
- ✓ P2P mesh topology for 2-3 participants (one RTCPeerConnection per peer)
- ✓ STUN/TURN configuration injected from server settings (fetched via `GET /api/v1/chat/ice-servers`)
- ✓ Adaptive bitrate: adjust video quality based on connection stats (`getStats()`)

**Files created:**
- `src/UI/DotNetCloud.UI.Web/wwwroot/js/video-call.js`
- `src/Modules/Chat/DotNetCloud.Modules.Chat/Services/IWebRtcInteropService.cs`
- `src/Modules/Chat/DotNetCloud.Modules.Chat/Services/WebRtcInteropService.cs`
- `src/Modules/Chat/DotNetCloud.Modules.Chat/Services/WebRtcDtos.cs`
- `tests/DotNetCloud.Modules.Chat.Tests/WebRtcInteropServiceTests.cs` (82 tests)
- `tests/DotNetCloud.Modules.Chat.Tests/WebRtcDtoTests.cs` (29 tests)

---

### Phase 7.6 — Blazor UI Components

**Depends on:** 7.5  
**Parallel with:** 7.7

**Status:** completed ✅

**Deliverables:**
- ✓ `VideoCallDialog.razor` — Main call window (modal or inline):
  - Video grid layout (1 local + N remote video elements), auto-adapts 1×1, 2×1, 2×2
  - Participant name overlays, connection quality indicator
  - Picture-in-picture support for local video
- ✓ `CallControls.razor` — Bottom toolbar:
  - Mute/unmute mic toggle
  - Camera on/off toggle
  - Screen share start/stop toggle
  - Hang up button
  - Participant count indicator
  - Call duration timer
- ✓ `IncomingCallNotification.razor` — Toast/overlay when receiving a call:
  - Caller name + avatar
  - Accept (audio) / Accept (video) / Reject buttons
  - Ring timeout (30s) with auto-dismiss
- ✓ `CallHistoryPanel.razor` — Call log in channel sidebar:
  - List of past calls with duration, participants, outcome (answered/missed/rejected)
  - Click to call back
- ✓ Extend `ChannelHeader.razor` — Add audio call + video call buttons
- ✓ Scoped CSS for all components: `VideoCallDialog.razor.css`, `CallControls.razor.css`, `IncomingCallNotification.razor.css`, `CallHistoryPanel.razor.css` — responsive design, dark mode
- ✓ `_Imports.razor` update for new components namespace
- ✓ `App.razor` — add `video-call.js` script reference (already present)

**Notes:** All 4 components created with code-behind pattern (.razor + .razor.cs + .razor.css). ChannelHeader extended with call buttons (audio, video, join active call, call history). All components wired into ChatPageLayout. 118 unit tests passing across 5 test files.

**Files to create/modify:**
- `src/Modules/Chat/DotNetCloud.Modules.Chat/UI/VideoCallDialog.razor` + `.razor.css`
- `src/Modules/Chat/DotNetCloud.Modules.Chat/UI/CallControls.razor` + `.razor.css`
- `src/Modules/Chat/DotNetCloud.Modules.Chat/UI/IncomingCallNotification.razor` + `.razor.css`
- `src/Modules/Chat/DotNetCloud.Modules.Chat/UI/CallHistoryPanel.razor` + `.razor.css`
- `src/Modules/Chat/DotNetCloud.Modules.Chat/UI/ChannelHeader.razor` — add call buttons
- `src/UI/DotNetCloud.UI.Web/Components/App.razor` — add `video-call.js` script reference

---

### Phase 7.7 — LiveKit Integration (Optional SFU for 4+ Participants)

**Depends on:** 7.4  
**Parallel with:** 7.6

**Status:** completed ✅

**Deliverables:**
- ✓ `ILiveKitService` interface: `CreateRoomAsync`, `GenerateTokenAsync`, `DeleteRoomAsync`, `GetRoomParticipantsAsync`
- ✓ `LiveKitService` implementation — HTTP client for LiveKit Server API:
  - JWT token generation (LiveKit access tokens with room grants)
  - Room creation/deletion lifecycle tied to VideoCall entity
  - Participant token generation with publish/subscribe permissions
- ✓ `LiveKitOptions` configuration class — API key, API secret, server URL, default room settings
- ✓ `NullLiveKitService` — Graceful degradation when LiveKit not installed (calls limited to 3 participants max)
- ✓ Auto-escalation: when 4th participant joins a P2P call → migrate to LiveKit room (or reject with "LiveKit required" message)
- ✓ LiveKit room cleanup on call end (EndCallAsync and auto-end from last participant leaving)
- ✓ `appsettings.json` section for LiveKit configuration
- ✓ 78 new tests (NullLiveKitService, LiveKitOptions, LiveKitService JWT/token, auto-escalation)

**Notes:** LiveKit integration complete. Process supervisor integration (same pattern as Collabora) deferred to deployment phase. All JWT token generation uses HMAC-SHA256 with standard .NET crypto — no additional NuGet dependencies required.

**Files created:**
- `src/Modules/Chat/DotNetCloud.Modules.Chat/Services/ILiveKitService.cs`
- `src/Modules/Chat/DotNetCloud.Modules.Chat/Services/LiveKitOptions.cs`
- `src/Modules/Chat/DotNetCloud.Modules.Chat/Services/NullLiveKitService.cs`
- `src/Modules/Chat/DotNetCloud.Modules.Chat.Data/Services/LiveKitService.cs`

**Files modified:**
- `src/Modules/Chat/DotNetCloud.Modules.Chat.Data/Services/VideoCallService.cs` — added ILiveKitService dependency, auto-escalation in JoinCallAsync, LiveKit room cleanup in EndCallInternalAsync
- `src/Modules/Chat/DotNetCloud.Modules.Chat.Data/ChatServiceRegistration.cs` — registered LiveKit services (factory pattern with NullLiveKitService fallback)
- `src/Core/DotNetCloud.Core.Server/appsettings.json` — added Chat:LiveKit configuration section
- `tests/DotNetCloud.Modules.Chat.Tests/VideoCallServiceTests.cs` — added Mock<ILiveKitService> and 8 auto-escalation tests

**Test files created:**
- `tests/DotNetCloud.Modules.Chat.Tests/LiveKitServiceTests.cs` (43 tests)
- `tests/DotNetCloud.Modules.Chat.Tests/LiveKitOptionsTests.cs` (19 tests)
- `tests/DotNetCloud.Modules.Chat.Tests/NullLiveKitServiceTests.cs` (16 tests)

---

### Phase 7.8 — STUN/TURN Configuration

**Depends on:** 7.5

**Status:** completed ✅

**Deliverables:**
- ✓ `IceServerOptions` configuration class — built-in STUN, additional STUN, TURN with static/ephemeral credentials, transport policy
- ✓ Built-in STUN server (`StunServer` BackgroundService) — embedded RFC 5389 Binding Response via raw UDP sockets, dual-stack IPv4/IPv6
- ✓ Default: self-hosted STUN (privacy-first, no Google dependency out of the box)
- ✓ Optional: additional third-party STUN servers (Google, Cloudflare) can be added via `AdditionalStunUrls` config
- ✓ TURN configuration: admin-configurable (coturn self-hosted or external provider)
- ✓ `IIceServerService` interface + `IceServerService` implementation with HMAC-SHA1 ephemeral credential generation (coturn-compatible)
- ✓ API endpoint: `GET /api/v1/chat/ice-servers` — returns ICE server config with short-lived TURN credentials when configured
- ✓ Credential rotation: TURN credentials valid for configurable TTL (default 24h)
- ✓ `appsettings.json` section for ICE/TURN configuration (`Chat:IceServers`)
- ✓ Removed Google STUN fallback from `video-call.js` — server provides all ICE config
- ✓ 73 new tests (IceServerOptionsTests, IceServerServiceTests, StunServerTests)

**Notes:** Privacy-first design: the built-in STUN server runs on UDP port 3478 by default. Admins must ensure firewall allows UDP 3478 inbound. For TURN relay, configure an external coturn server. Admin settings UI deferred to Phase 7.11.

**Firewall requirements:**
- UDP port 3478 (STUN) — must be open inbound for WebRTC NAT traversal
- If using TURN: TCP/UDP 3478 and TCP 5349 (TURNS/TLS) on the coturn server

**Files created:**
- `src/Modules/Chat/DotNetCloud.Modules.Chat/Services/IceServerOptions.cs`
- `src/Modules/Chat/DotNetCloud.Modules.Chat/Services/IIceServerService.cs`
- `src/Modules/Chat/DotNetCloud.Modules.Chat.Data/Services/IceServerService.cs`
- `src/Modules/Chat/DotNetCloud.Modules.Chat.Data/Services/StunServer.cs`

**Files modified:**
- `src/Modules/Chat/DotNetCloud.Modules.Chat.Host/Controllers/ChatController.cs` — added IIceServerService + GET ice-servers endpoint
- `src/Modules/Chat/DotNetCloud.Modules.Chat.Data/ChatServiceRegistration.cs` — registered IceServerOptions, IIceServerService, StunServer
- `src/Core/DotNetCloud.Core.Server/appsettings.json` — added Chat:IceServers configuration section
- `src/UI/DotNetCloud.UI.Web/wwwroot/js/video-call.js` — removed Google STUN fallback

**Test files created:**
- `tests/DotNetCloud.Modules.Chat.Tests/IceServerOptionsTests.cs` (14 tests)
- `tests/DotNetCloud.Modules.Chat.Tests/IceServerServiceTests.cs` (33 tests)
- `tests/DotNetCloud.Modules.Chat.Tests/StunServerTests.cs` (18 tests)

---

### Phase 7.9 — REST API & gRPC Updates

**Depends on:** 7.3

**Status:** completed ✅

**Deliverables:**
- ✓ REST API endpoints added to `ChatController.cs`:

| Method | Endpoint | Description |
|--------|----------|-------------|
| `POST` | `/api/v1/chat/channels/{channelId}/calls` | Initiate call |
| `POST` | `/api/v1/chat/calls/{callId}/join` | Join active call |
| `POST` | `/api/v1/chat/calls/{callId}/leave` | Leave call |
| `POST` | `/api/v1/chat/calls/{callId}/end` | End call |
| `POST` | `/api/v1/chat/calls/{callId}/reject` | Reject incoming call |
| `GET` | `/api/v1/chat/channels/{channelId}/calls` | Call history (paginated) |
| `GET` | `/api/v1/chat/calls/{callId}` | Get call details |
| `GET` | `/api/v1/chat/channels/{channelId}/calls/active` | Get active call |
| `GET` | `/api/v1/chat/ice-servers` | ICE server configuration |

- ✓ gRPC service updates to `chat_service.proto`:
  - `InitiateVideoCall`, `JoinVideoCall`, `LeaveVideoCall`, `EndVideoCall`, `RejectVideoCall`
  - `GetCallHistory`, `GetActiveCall`
- ✓ Authorization: channel membership required for all call operations (delegated to service layer via CallerContext)
- ✓ Rate limiting: max 1 call initiation per channel per 5 seconds (`module-video-call-initiate` policy)
- ✓ `IVideoCallService.GetCallByIdAsync` added for individual call lookup
- ✓ 62 comprehensive tests (34 VideoCallControllerTests + 28 VideoCallGrpcServiceTests)

**Notes:** All 9 REST endpoints and 7 gRPC RPCs implemented. Controller follows existing error-handling pattern (ArgumentException→BadRequest, InvalidOperationException→NotFound/Conflict, UnauthorizedAccessException→Forbid). gRPC methods follow existing ID validation and CallerContext construction patterns.

**Files modified:**
- `src/Modules/Chat/DotNetCloud.Modules.Chat.Host/Controllers/ChatController.cs` — added IVideoCallService injection + 9 video call endpoints
- `src/Modules/Chat/DotNetCloud.Modules.Chat.Host/Protos/chat_service.proto` — added 7 video call RPCs + message types
- `src/Modules/Chat/DotNetCloud.Modules.Chat.Host/Services/ChatGrpcService.cs` — added IVideoCallService injection + 7 gRPC method implementations + mapper methods
- `src/Modules/Chat/DotNetCloud.Modules.Chat/Services/IVideoCallService.cs` — added `GetCallByIdAsync` method
- `src/Modules/Chat/DotNetCloud.Modules.Chat.Data/Services/VideoCallService.cs` — implemented `GetCallByIdAsync`
- `src/Core/DotNetCloud.Core.Server/appsettings.json` — added `video-call-initiate` rate limit policy

**Test files created:**
- `tests/DotNetCloud.Modules.Chat.Tests/VideoCallControllerTests.cs` (34 tests)
- `tests/DotNetCloud.Modules.Chat.Tests/VideoCallGrpcServiceTests.cs` (28 tests)

---

### Phase 7.10 — Push Notifications for Calls

**Depends on:** 7.3

**Status:** completed ✅

**Deliverables:**
- ✓ Incoming call push notification (high-priority) via existing FCM/UnifiedPush infrastructure:
  - Android: heads-up notification with Accept/Reject actions
  - Desktop (SyncTray): system notification with call info
- ✓ Missed call notification (normal priority)
- ✓ Call-ended notification for participants who were disconnected
- ✓ Extend `NotificationRouter.cs` to handle call notification types
- ✓ New notification types: `IncomingCall`, `MissedCall`, `CallEnded`

**Files modified:**
- `src/Modules/Chat/DotNetCloud.Modules.Chat/Services/IPushNotificationService.cs` — added `IncomingCall`, `MissedCall`, `CallEnded` to `NotificationCategory` enum
- `src/Modules/Chat/DotNetCloud.Modules.Chat/Services/NotificationRouter.cs` — bypass online presence suppression for `IncomingCall`
- `src/Modules/Chat/DotNetCloud.Modules.Chat/Services/ICallNotificationHandler.cs` — new interface combining 3 event handlers
- `src/Modules/Chat/DotNetCloud.Modules.Chat.Data/Services/CallNotificationEventHandler.cs` — new event handler implementation
- `src/Modules/Chat/DotNetCloud.Modules.Chat.Data/ChatServiceRegistration.cs` — DI registration
- `src/Modules/Chat/DotNetCloud.Modules.Chat/ChatModule.cs` — event bus subscription

---

### Phase 7.11 — Testing & Documentation

**Depends on:** 7.1–7.10

**Status:** completed ✅

**Deliverables:**
- ✓ **Unit Tests** (678 video-call-specific tests across 20 test files in `tests/DotNetCloud.Modules.Chat.Tests/`):

| Test File | Tests | Coverage |
|-----------|-------|----------|
| `VideoCallServiceTests.cs` | 80 | Call lifecycle (initiate, join, leave, end, reject), state machine transitions, timeout handling, concurrent call prevention |
| `CallStateValidatorTests.cs` | 29 | Valid/invalid state transitions, edge cases |
| `CallSignalingServiceTests.cs` | 62 | SDP/ICE relay validation, unauthorized relay rejection, payload size limits |
| `LiveKitServiceTests.cs` | 33 | Token generation, room lifecycle, null service degradation |
| `IceServerServiceTests.cs` | 31 | TURN credential generation, TTL expiry, configuration validation |
| `VideoCallControllerTests.cs` | 34 | REST endpoint routing, authorization, request validation |
| `CallNotificationEventHandlerTests.cs` | 28 | Push notification routing for incoming/missed calls |
| `VideoCallGrpcServiceTests.cs` | 28 | gRPC RPC routing, error handling |
| `StunServerTests.cs` | 19 | RFC 5389 binding request/response, magic cookie validation |
| `VideoCallDataModelTests.cs` | 62 | Entity configuration, FK relationships, soft delete |
| `NullLiveKitServiceTests.cs` | 15 | Fallback service behavior |
| `WebRtcDtoTests.cs` | 18 | DTO serialization, validation |
| `IceServerOptionsTests.cs` | 14 | Options binding, defaults |
| `LiveKitOptionsTests.cs` | 21 | Options validation, IsValid logic |
| `WebRtcInteropServiceTests.cs` | 93 | JS interop call verification |
| `VideoCallDialogTests.cs` | 38 | Blazor component rendering, event callbacks |
| `IncomingCallNotificationTests.cs` | 12 | Notification component rendering |
| `CallControlsTests.cs` | 23 | Control button rendering, click handlers |
| `CallHistoryPanelTests.cs` | 32 | History panel rendering, pagination |
| `ChannelHeaderCallButtonTests.cs` | 6 | Header button rendering |

- ✓ Integration tests: end-to-end call lifecycle via API (initiate → join → leave → end) in `VideoCallServiceTests.cs`
- ✓ Admin guide: `docs/admin/VIDEO_CALLING.md` — STUN/TURN configuration, coturn setup, LiveKit setup (optional)
- ✓ Update `docs/modules/chat/README.md` and `docs/modules/chat/API.md` — video calling features, endpoints, gRPC RPCs
- ✓ User documentation: `docs/user/VIDEO_CALLS.md` — how to make video calls and share screen

---

## Dependency Graph

```
Phase 7.1 (Contracts)
    │
    ▼
Phase 7.2 (Data Model)
    │
    ▼
Phase 7.3 (Call Service) ──────────────┬──────────────┐
    │                                  │              │
    ▼                                  ▼              ▼
Phase 7.4 (SignalR Signaling)    Phase 7.9 (API)  Phase 7.10 (Push)
    │
    ▼
Phase 7.5 (JS WebRTC Engine)
    │                   │
    ▼                   ▼
Phase 7.6 (Blazor UI)  Phase 7.8 (STUN/TURN)
    ║
    ║  (parallel)
    ║
Phase 7.7 (LiveKit) ← depends on 7.4
    │
    ▼
Phase 7.11 (Testing & Docs) ← depends on all above
```

---

## Files Summary

### Extend (Existing)

| File/Directory | Changes |
|----------------|---------|
| `src/Modules/Chat/DotNetCloud.Modules.Chat/Models/` | Add `VideoCall.cs`, `CallParticipant.cs`, enums |
| `src/Modules/Chat/DotNetCloud.Modules.Chat/DTOs/ChatDtos.cs` | Add call DTOs |
| `src/Modules/Chat/DotNetCloud.Modules.Chat/Events/` | Add 8 call event files |
| `src/Modules/Chat/DotNetCloud.Modules.Chat/Services/` | Add `IVideoCallService`, `ICallSignalingService`, `ILiveKitService` |
| `src/Modules/Chat/DotNetCloud.Modules.Chat/ChatModuleManifest.cs` | Update published events |
| `src/Modules/Chat/DotNetCloud.Modules.Chat/UI/ChannelHeader.razor` | Add call buttons |
| `src/Modules/Chat/DotNetCloud.Modules.Chat.Data/ChatDbContext.cs` | Add `DbSet<VideoCall>`, `DbSet<CallParticipant>` |
| `src/Modules/Chat/DotNetCloud.Modules.Chat.Data/ChatServiceRegistration.cs` | Register new services |
| `src/Modules/Chat/DotNetCloud.Modules.Chat.Host/Controllers/ChatController.cs` | Add call REST endpoints |
| `src/Modules/Chat/DotNetCloud.Modules.Chat.Host/Protos/chat_service.proto` | Add call RPCs |
| `src/Modules/Chat/DotNetCloud.Modules.Chat.Host/Services/ChatGrpcService.cs` | Implement call RPCs |
| `src/Core/DotNetCloud.Core.Server/RealTime/CoreHub.cs` | Add signaling methods |
| `src/Modules/Chat/DotNetCloud.Modules.Chat/Services/NotificationRouter.cs` | Add call notification types |

### Create (New)

| File | Purpose |
|------|---------|
| `src/Modules/Chat/DotNetCloud.Modules.Chat/Models/VideoCall.cs` | VideoCall entity |
| `src/Modules/Chat/DotNetCloud.Modules.Chat/Models/CallParticipant.cs` | CallParticipant entity |
| `src/Modules/Chat/DotNetCloud.Modules.Chat/Models/VideoCallState.cs` | State enum |
| `src/Modules/Chat/DotNetCloud.Modules.Chat/Models/VideoCallEndReason.cs` | End reason enum |
| `src/Modules/Chat/DotNetCloud.Modules.Chat/Models/CallParticipantRole.cs` | Role enum |
| `src/Modules/Chat/DotNetCloud.Modules.Chat/Models/CallMediaType.cs` | Media type enum |
| `src/Modules/Chat/DotNetCloud.Modules.Chat/Services/IVideoCallService.cs` | Call lifecycle interface |
| `src/Modules/Chat/DotNetCloud.Modules.Chat/Services/ICallSignalingService.cs` | Signaling interface |
| `src/Modules/Chat/DotNetCloud.Modules.Chat/Services/ILiveKitService.cs` | LiveKit interface |
| `src/Modules/Chat/DotNetCloud.Modules.Chat/Services/CallStateValidator.cs` | State machine |
| `src/Modules/Chat/DotNetCloud.Modules.Chat/Services/LiveKitOptions.cs` | LiveKit config |
| `src/Modules/Chat/DotNetCloud.Modules.Chat/Services/IceServerOptions.cs` | ICE config |
| `src/Modules/Chat/DotNetCloud.Modules.Chat/Services/NullLiveKitService.cs` | LiveKit fallback |
| `src/Modules/Chat/DotNetCloud.Modules.Chat/Services/CallSignalingService.cs` | Signaling impl |
| `src/Modules/Chat/DotNetCloud.Modules.Chat.Data/Services/VideoCallService.cs` | Call service impl |
| `src/Modules/Chat/DotNetCloud.Modules.Chat.Data/Services/LiveKitService.cs` | LiveKit impl |
| `src/Modules/Chat/DotNetCloud.Modules.Chat.Data/Configuration/VideoCallConfiguration.cs` | EF config |
| `src/Modules/Chat/DotNetCloud.Modules.Chat.Data/Configuration/CallParticipantConfiguration.cs` | EF config |
| `src/Modules/Chat/DotNetCloud.Modules.Chat/Events/VideoCallInitiatedEvent.cs` | Event |
| `src/Modules/Chat/DotNetCloud.Modules.Chat/Events/VideoCallAnsweredEvent.cs` | Event |
| `src/Modules/Chat/DotNetCloud.Modules.Chat/Events/VideoCallEndedEvent.cs` | Event |
| `src/Modules/Chat/DotNetCloud.Modules.Chat/Events/VideoCallMissedEvent.cs` | Event |
| `src/Modules/Chat/DotNetCloud.Modules.Chat/Events/ParticipantJoinedCallEvent.cs` | Event |
| `src/Modules/Chat/DotNetCloud.Modules.Chat/Events/ParticipantLeftCallEvent.cs` | Event |
| `src/Modules/Chat/DotNetCloud.Modules.Chat/Events/ScreenShareStartedEvent.cs` | Event |
| `src/Modules/Chat/DotNetCloud.Modules.Chat/Events/ScreenShareEndedEvent.cs` | Event |
| `src/Modules/Chat/DotNetCloud.Modules.Chat/UI/VideoCallDialog.razor` + `.razor.css` | Call window |
| `src/Modules/Chat/DotNetCloud.Modules.Chat/UI/CallControls.razor` + `.razor.css` | Call toolbar |
| `src/Modules/Chat/DotNetCloud.Modules.Chat/UI/IncomingCallNotification.razor` + `.razor.css` | Incoming call toast |
| `src/Modules/Chat/DotNetCloud.Modules.Chat/UI/CallHistoryPanel.razor` + `.razor.css` | Call log |
| `src/UI/DotNetCloud.UI.Web/wwwroot/js/video-call.js` | WebRTC JS interop |
| `tests/DotNetCloud.Modules.Chat.Tests/VideoCallServiceTests.cs` | Call lifecycle tests |
| `tests/DotNetCloud.Modules.Chat.Tests/CallStateValidatorTests.cs` | State machine tests |
| `tests/DotNetCloud.Modules.Chat.Tests/CallSignalingTests.cs` | Signaling tests |
| `tests/DotNetCloud.Modules.Chat.Tests/LiveKitServiceTests.cs` | LiveKit tests |
| `tests/DotNetCloud.Modules.Chat.Tests/IceServerConfigTests.cs` | ICE config tests |
| `tests/DotNetCloud.Modules.Chat.Tests/VideoCallApiTests.cs` | API tests |
| `tests/DotNetCloud.Modules.Chat.Tests/CallHistoryTests.cs` | History tests |
| `tests/DotNetCloud.Modules.Chat.Tests/CallNotificationTests.cs` | Notification tests |
| `docs/admin/VIDEO_CALLING.md` | Admin setup guide |

### Reference (Patterns to Follow)

| File | Pattern |
|------|---------|
| `src/Modules/Chat/DotNetCloud.Modules.Chat/Services/IChatRealtimeService.cs` | Real-time broadcast pattern |
| `src/Modules/Chat/DotNetCloud.Modules.Chat/Services/NotificationRouter.cs` | Notification routing pattern |
| `src/Core/DotNetCloud.Core/Capabilities/IRealtimeBroadcaster.cs` | Broadcast capability interface |
| `src/Core/DotNetCloud.Core/Capabilities/IPresenceTracker.cs` | Presence check before calling |
| `src/UI/DotNetCloud.UI.Web/wwwroot/js/` | Existing JS interop pattern |

---

## Verification Checklist

1. ☐ `dotnet build` — zero warnings
2. ☐ `dotnet test tests/DotNetCloud.Modules.Chat.Tests/` — all existing + 120+ new tests pass
3. ☐ Manual: 1:1 video call → SDP/ICE exchange via SignalR → video renders both sides
4. ☐ Manual: screen share toggle → remote peer sees shared screen
5. ☐ Manual: 3-person P2P mesh call works
6. ☐ Manual: reject/miss → correct state transitions + notifications
7. ☐ Manual: call history in channel sidebar
8. ☐ Verify LiveKit degradation: 4th participant gets clear error when LiveKit not configured
9. ☐ Verify TURN: call works behind symmetric NAT (TURN relay required)
10. ☐ Security: non-members cannot join calls, SDP payloads size-limited

---

## Security Considerations

- **Channel membership enforcement:** All call operations require authenticated channel membership
- **SDP/ICE payload validation:** Size limits (SDP max 64KB, ICE max 4KB), no script injection
- **TURN credential rotation:** Ephemeral HMAC credentials with configurable TTL
- **Rate limiting:** 1 call initiation per channel per 5 seconds
- **Signal relay authorization:** CoreHub validates caller is a participant before relaying SDP/ICE
- **LiveKit tokens:** Short-lived JWTs with scoped room grants (publish/subscribe permissions per participant)
