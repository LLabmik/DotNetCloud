# Direct Messaging, Direct Calls & Host-Based Call Management

## Overview

Currently all chats and calls are channel-based — users must navigate to a channel before messaging or calling. This plan adds:

1. **Direct user-to-user chat initiation** via a global user search/picker
2. **Direct user-to-user call initiation** without navigating to a channel first
3. **Mid-call participant addition** with full incoming-call ring
4. **"Host" role** for calls with transferable ownership and permission control
5. **DM → Group in-place conversion** when a 3rd person is added to a 1:1 conversation

The existing infrastructure (`ChannelType.DirectMessage`, `DirectMessageView` component, `IUserDirectory.SearchUsersAsync`) provides strong foundations. The work is primarily wiring, new service methods, UI integration, and the Host role system.

---

## Design Decisions

| Decision | Choice | Rationale |
|----------|--------|-----------|
| DM → Group escalation | Convert in-place | 3rd person sees prior messages. Simpler than forking a new channel. Channel type changes from `DirectMessage` to `Group`. |
| Mid-call invite UX | Full ring (30s timeout) | Same experience as a normal incoming call. Consistent and hard to miss. |
| Add-people permissions | Host only | Prevents chaotic call growth. Host is transferable so control can be delegated. |
| Call control role name | **Host** (replaces "Initiator") | More intuitive. "Initiator" implies a historical fact; "Host" implies current authority. |
| Host auto-transfer | Longest-active participant | If Host leaves without explicit transfer, the participant with the earliest `JoinedAtUtc` inherits. Deterministic and fair. |
| Direct call mechanics | Auto-creates DM channel | A direct call to a user creates (or reuses) the DM channel, then initiates a call on it. No orphan calls without a channel. |

---

## Phase A — Database & Model Changes

### A1. Rename `CallParticipantRole.Initiator` → `Host`

Rename the enum value across the codebase. If the value is stored as a string in the DB, add an EF migration to update existing rows.

**Files:**
- `src/Modules/Chat/DotNetCloud.Modules.Chat/Models/CallParticipantRole.cs` — Rename enum value
- `src/Modules/Chat/DotNetCloud.Modules.Chat.Data/Services/VideoCallService.cs` — All references
- `src/Modules/Chat/DotNetCloud.Modules.Chat/DTOs/ChatDtos.cs` — `CallParticipantDto.Role` serialization
- All test files referencing `CallParticipantRole.Initiator`
- New EF migration if needed for stored string values

### A2. Add `HostUserId` to `VideoCall` Entity

Track the current Host of a call. Initially set to `InitiatorUserId` when a call is created. Updated on explicit transfer or auto-transfer.

**Files:**
- `src/Modules/Chat/DotNetCloud.Modules.Chat/Models/VideoCall.cs` — Add `Guid HostUserId` property
- `src/Modules/Chat/DotNetCloud.Modules.Chat.Data/Configuration/VideoCallConfiguration.cs` — Configure FK relationship
- New EF migration: `AddCallHostUserId`

### A3. DM → Group Auto-Conversion

When `ChannelMemberService.AddMemberAsync` adds a 3rd member to a `DirectMessage` channel, automatically change the channel type to `ChannelType.Group`.

**Files:**
- `src/Modules/Chat/DotNetCloud.Modules.Chat.Data/Services/ChannelMemberService.cs` — Add type conversion logic in `AddMemberAsync`
- No schema change needed — `Channel.Type` column already supports `Group`

---

## Phase B — Service Layer: Direct DM & Call Initiation

### B1. Wire Global User Search for DM Creation

Connect `IUserDirectory.SearchUsersAsync()` to the chat UI so users can find and message anyone, not just current channel members.

**Files:**
- `src/Modules/Chat/DotNetCloud.Modules.Chat/UI/ChatPageLayout.razor.cs` — Add `SearchUsersForDmAsync(string searchTerm)` method
- `src/Modules/Chat/DotNetCloud.Modules.Chat/UI/ChatPageLayout.razor` — Integrate user picker dialog

### B2. Direct Call Initiation by User ID

New service method that creates-or-gets the DM channel between two users, then initiates a call on it. Single atomic operation from the caller's perspective.

**Interface addition:**
```csharp
Task<VideoCallDto> InitiateDirectCallAsync(
    Guid targetUserId,
    StartCallRequest request,
    CallerContext caller,
    CancellationToken cancellationToken = default);
```

**Implementation flow:**
1. Call `IChannelService.GetOrCreateDirectMessageAsync(targetUserId, caller)`
2. Call `InitiateCallAsync(channelId, request, caller)`
3. Return the `VideoCallDto`

**Files:**
- `src/Modules/Chat/DotNetCloud.Modules.Chat/Services/IVideoCallService.cs` — Add interface method
- `src/Modules/Chat/DotNetCloud.Modules.Chat.Data/Services/VideoCallService.cs` — Implement
- `src/Modules/Chat/DotNetCloud.Modules.Chat.Host/Controllers/ChatController.cs` — New endpoint: `POST /api/v1/chat/calls/direct/{targetUserId}`

---

## Phase C — Mid-Call Participant Addition *(depends on A)*

### C1. `InviteToCallAsync` Service Method

**Interface addition:**
```csharp
Task InviteToCallAsync(
    Guid callId,
    Guid targetUserId,
    CallerContext caller,
    CancellationToken cancellationToken = default);
```

**Implementation flow:**
1. Validate caller is Host (`call.HostUserId == caller.UserId`)
2. Validate target is not already an active participant
3. If target is not a channel member, auto-add them via `IChannelMemberService.AddMemberAsync` (which may trigger DM→Group conversion from A3)
4. Add `CallParticipant` record with `InvitedAtUtc` set
5. Send incoming-call notification to target user via `IRealtimeBroadcaster.SendToUserAsync`
6. Start 30s ring timeout

**Files:**
- `src/Modules/Chat/DotNetCloud.Modules.Chat/Services/IVideoCallService.cs` — Add method
- `src/Modules/Chat/DotNetCloud.Modules.Chat.Data/Services/VideoCallService.cs` — Implement with Host validation
- `src/Modules/Chat/DotNetCloud.Modules.Chat/Models/CallParticipant.cs` — Add `DateTime? InvitedAtUtc` field and/or `ParticipantState` enum (`Invited`, `Joined`, `Left`, `Rejected`)
- `src/Modules/Chat/DotNetCloud.Modules.Chat.Host/Controllers/ChatController.cs` — New endpoint: `POST /api/v1/chat/calls/{callId}/invite` with body `{ "userId": "<guid>" }`

### C2. SignalR Notification for Mid-Call Invite

The invited user receives an incoming-call event with additional context indicating it's a mid-call invite.

**Payload:**
```json
{
  "callId": "...",
  "channelId": "...",
  "invitedByUserId": "...",
  "invitedByDisplayName": "...",
  "mediaType": "Video",
  "isMidCallInvite": true,
  "participantCount": 3
}
```

**Files:**
- `src/Modules/Chat/DotNetCloud.Modules.Chat/Services/ChatRealtimeService.cs` — Add/reuse broadcast method for call invites
- `src/Modules/Chat/DotNetCloud.Modules.Chat/UI/IncomingCallNotification.razor.cs` — Handle `isMidCallInvite` flag

---

## Phase D — Host Transfer *(depends on A)*

### D1. `TransferHostAsync` Service Method

**Interface addition:**
```csharp
Task TransferHostAsync(
    Guid callId,
    Guid newHostUserId,
    CallerContext caller,
    CancellationToken cancellationToken = default);
```

**Implementation flow:**
1. Validate caller is current Host
2. Validate target is an active participant (joined, not left)
3. Update `VideoCall.HostUserId` to new user
4. Update participant roles: old host → `Participant`, new host → `Host`
5. Broadcast `HostTransferred` event to all call participants

**Files:**
- `src/Modules/Chat/DotNetCloud.Modules.Chat/Services/IVideoCallService.cs` — Add method
- `src/Modules/Chat/DotNetCloud.Modules.Chat.Data/Services/VideoCallService.cs` — Implement
- `src/Modules/Chat/DotNetCloud.Modules.Chat.Host/Controllers/ChatController.cs` — New endpoint: `POST /api/v1/chat/calls/{callId}/transfer-host` with body `{ "userId": "<guid>" }`

### D2. Auto-Transfer Host on Leave

If the Host leaves the call without explicitly transferring, auto-transfer to the remaining participant with the earliest `JoinedAtUtc`.

**Files:**
- `src/Modules/Chat/DotNetCloud.Modules.Chat.Data/Services/VideoCallService.cs` — In `LeaveCallAsync`, if leaving user is Host and other participants remain, auto-transfer and broadcast

### D3. End-Call Permission Enforcement

Only the Host can end a call for all participants. Non-hosts can only leave (which removes themselves).

**Files:**
- `src/Modules/Chat/DotNetCloud.Modules.Chat.Data/Services/VideoCallService.cs` — In `EndCallAsync`, validate `caller.UserId == call.HostUserId`

### D4. New Event

**Files:**
- `src/Modules/Chat/DotNetCloud.Modules.Chat/Events/` — New `CallHostTransferredEvent` with `CallId`, `PreviousHostUserId`, `NewHostUserId`
- `src/Modules/Chat/DotNetCloud.Modules.Chat/Services/ChatRealtimeService.cs` — Broadcast `"HostTransferred"` to call group

---

## Phase E — UI Integration *(depends on B, C, D)*

### E1. "New DM" User Picker in Sidebar

Add a "+" button in the Direct Messages section of the sidebar. Opens a user search dialog using `IUserDirectory.SearchUsersAsync`. Selecting a user calls `GetOrCreateDmAsync` and navigates to the DM.

**Files:**
- `src/Modules/Chat/DotNetCloud.Modules.Chat/UI/ChannelList.razor` — "+" button in DM section header
- `src/Modules/Chat/DotNetCloud.Modules.Chat/UI/ChatPageLayout.razor.cs` — `SearchUsersForDmAsync`, `StartDmWithUserAsync` methods
- `src/Modules/Chat/DotNetCloud.Modules.Chat/UI/ChatPageLayout.razor` — Wire user picker dialog
- CSS for user search/picker dialog

### E2. "Call User" Buttons

From user profiles, member lists, or the DM header — a "Call" button that calls `InitiateDirectCallAsync`.

**Files:**
- `src/Modules/Chat/DotNetCloud.Modules.Chat/UI/ChannelHeader.razor` — Ensure call buttons work for DM channels
- `src/Modules/Chat/DotNetCloud.Modules.Chat/UI/MemberListPanel.razor` — Add call icon per user
- `src/Modules/Chat/DotNetCloud.Modules.Chat/UI/ChatPageLayout.razor.cs` — Add `CallUserDirectlyAsync(Guid userId)` method

### E3. "Add People" Button in Active Call (Host Only)

During an active call, the Host sees an "Add people" button that opens a user search picker. Selecting a user calls `InviteToCallAsync`.

**Files:**
- `src/Modules/Chat/DotNetCloud.Modules.Chat/UI/CallControls.razor` — "Add people" button (visible only to Host)
- `src/Modules/Chat/DotNetCloud.Modules.Chat/UI/VideoCallDialog.razor` — User picker overlay for adding participants
- `src/Modules/Chat/DotNetCloud.Modules.Chat/UI/VideoCallDialog.razor.cs` — `InviteUserToCallAsync` method

### E4. "Transfer Host" in Call Participant List

Host can click a participant and select "Make Host". Non-hosts see a "Host" badge on the host participant.

**Files:**
- `src/Modules/Chat/DotNetCloud.Modules.Chat/UI/VideoCallDialog.razor` — Participant list with Host badge and transfer context menu
- `src/Modules/Chat/DotNetCloud.Modules.Chat/UI/VideoCallDialog.razor.cs` — `TransferHostAsync` method
- Handle `"HostTransferred"` SignalR event to update UI for all participants in real-time

### E5. Updated Incoming Call Notification

Show "X invited you to join an ongoing call" when `isMidCallInvite` is true, vs "X is calling you" for a fresh call.

**Files:**
- `src/Modules/Chat/DotNetCloud.Modules.Chat/UI/IncomingCallNotification.razor` — Conditional text
- `src/Modules/Chat/DotNetCloud.Modules.Chat/UI/IncomingCallNotification.razor.cs` — Handle `isMidCallInvite` flag

### E6. "Add People" to Group Chat

From the channel header or member panel, a button to add more people to the current conversation. For DMs this triggers the DM → Group conversion.

**Files:**
- `src/Modules/Chat/DotNetCloud.Modules.Chat/UI/ChannelHeader.razor` — "Add people" button for DM/Group channels
- `src/Modules/Chat/DotNetCloud.Modules.Chat/UI/MemberListPanel.razor` — "Invite" section with user search

---

## Phase F — SignalR Hub Updates *(parallel with E)*

### F1. New Hub Methods

**Files:**
- `src/Core/DotNetCloud.Core.Server/RealTime/CoreHub.cs` — Add:
  - `InviteToCallAsync(Guid callId, Guid targetUserId)` — relays to `IVideoCallService.InviteToCallAsync`
  - `TransferHostAsync(Guid callId, Guid newHostUserId)` — relays to `IVideoCallService.TransferHostAsync`

### F2. New Client-Side Event Handlers

New SignalR events for clients to handle:

| Event | Payload | Purpose |
|-------|---------|---------|
| `"HostTransferred"` | `{ callId, newHostUserId, previousHostUserId }` | All call participants update Host badge |
| `"CallInviteReceived"` | `{ callId, channelId, invitedByUserId, mediaType, isMidCallInvite }` | Target user sees incoming call notification |

**Files:**
- `src/Modules/Chat/DotNetCloud.Modules.Chat/UI/ChatPageLayout.razor.cs` — Register handlers for new events

---

## Phase G — Tests *(after all above)*

### G1. Unit Tests

| Test Area | Scenarios |
|-----------|-----------|
| Host transfer | Valid transfer, non-host attempts transfer (rejected), auto-transfer on host leave |
| Mid-call invite | Host invites user, non-host rejected, invite already-in-call user (rejected), invite triggers channel membership add |
| DM → Group | Add 3rd member to DM changes type to Group, existing members unaffected |
| Direct call | Creates DM channel + call in one operation, reuses existing DM |
| End-call permission | Only Host can end, non-host gets error |
| Host leave | Auto-transfers to longest-active, broadcasts to all |

**Files:**
- `tests/DotNetCloud.Modules.Chat.Tests/` — New test classes for Host and invite features
- Existing `VideoCallService` test files — extend with Host scenarios

### G2. Integration / E2E Tests

| Flow | Steps |
|------|-------|
| Mid-call invite | SignalR: invite → target receives ring → accepts → joins call → sees other participants |
| Host transfer | SignalR: transfer → all participants notified → new Host can add people |
| Full lifecycle | Create DM → escalate to group → start call → invite 4th user → transfer host → original host leaves → call continues |

---

## Relevant Files Summary

### Models
- `src/Modules/Chat/DotNetCloud.Modules.Chat/Models/VideoCall.cs` — Add `HostUserId`
- `src/Modules/Chat/DotNetCloud.Modules.Chat/Models/CallParticipant.cs` — Add `InvitedAtUtc`, consider `ParticipantState` enum
- `src/Modules/Chat/DotNetCloud.Modules.Chat/Models/CallParticipantRole.cs` — Rename `Initiator` → `Host`

### Services
- `src/Modules/Chat/DotNetCloud.Modules.Chat/Services/IVideoCallService.cs` — 3 new methods (`InitiateDirectCallAsync`, `InviteToCallAsync`, `TransferHostAsync`)
- `src/Modules/Chat/DotNetCloud.Modules.Chat.Data/Services/VideoCallService.cs` — Implement all + Host enforcement in `EndCallAsync` and `LeaveCallAsync`
- `src/Modules/Chat/DotNetCloud.Modules.Chat.Data/Services/ChannelMemberService.cs` — DM→Group auto-conversion
- `src/Modules/Chat/DotNetCloud.Modules.Chat/Services/ChatRealtimeService.cs` — New broadcast methods

### API
- `src/Modules/Chat/DotNetCloud.Modules.Chat.Host/Controllers/ChatController.cs` — 3 new endpoints

### SignalR
- `src/Core/DotNetCloud.Core.Server/RealTime/CoreHub.cs` — 2 new hub methods

### UI
- `src/Modules/Chat/DotNetCloud.Modules.Chat/UI/ChatPageLayout.razor(.cs)` — DM search, direct call, event handlers
- `src/Modules/Chat/DotNetCloud.Modules.Chat/UI/ChannelList.razor` — "New DM" button
- `src/Modules/Chat/DotNetCloud.Modules.Chat/UI/VideoCallDialog.razor(.cs)` — Add people, Host transfer, Host badge
- `src/Modules/Chat/DotNetCloud.Modules.Chat/UI/CallControls.razor` — "Add people" button (Host only)
- `src/Modules/Chat/DotNetCloud.Modules.Chat/UI/IncomingCallNotification.razor(.cs)` — Mid-call invite text
- `src/Modules/Chat/DotNetCloud.Modules.Chat/UI/ChannelHeader.razor` — "Add people" to chat, call buttons
- `src/Modules/Chat/DotNetCloud.Modules.Chat/UI/MemberListPanel.razor` — Per-user call icon, invite

### Events
- `src/Modules/Chat/DotNetCloud.Modules.Chat/Events/` — New `CallHostTransferredEvent`

### Migrations
- New EF migration for `HostUserId` column + `CallParticipantRole` rename (if string-stored) + any `CallParticipant` state fields

---

## Verification Checklist

- ☐ `dotnet build` — full solution compiles
- ☐ `dotnet test` — all existing + new tests pass
- ☐ Manual: "+" in DM section → search user → start DM → send messages
- ☐ Manual: In DM, click call → other user gets ring → accept → call works
- ☐ Manual: Host clicks "Add people" → search → invite → target gets ring → joins
- ☐ Manual: Host clicks participant → "Make Host" → badge moves → old host leaves → call continues
- ☐ Manual: Add 3rd person to DM → converts to Group → new member sees history
- ☐ Manual: Non-host tries add people or end call → action blocked
- ☐ Manual: Host leaves without transferring → auto-transfers to longest-active participant

---

## Scope Boundaries

**Included:**
- Direct user-to-user DM initiation with global user search
- Direct user-to-user call initiation (auto-creates DM channel)
- Mid-call participant invitation (Host only, full ring)
- Host role with transfer capability
- DM → Group in-place conversion
- Auto-host-transfer on Host leave
- End-call restricted to Host only

**Excluded (future work):**
- Multi-select user picker for creating group chats from scratch
- Call recording
- Scheduled/planned calls
- "Ring all channel members" feature for group channels
