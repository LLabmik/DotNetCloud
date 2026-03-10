# Client/Server Mediation Handoff

Last updated: 2026-03-10 (Phase 2.5 SignalR group lifecycle + reconnect hardening update posted)

Purpose: Shared handoff between client-side and server-side agents, mediated by user.

> Archived context (45 resolved issues — initial sync milestone through Batch 4.5) moved to
> [CLIENT_SERVER_MEDIATION_ARCHIVE.md](CLIENT_SERVER_MEDIATION_ARCHIVE.md).
> Full git history in commits up to `1cd594a`.

## Process Rules

- All technical findings and debugging conclusions go in this document, pushed to `main`.
- Mediator role is relay-only — commit notifications and cross-agent request forwarding.
- Keep this handoff lean: when resolved/completed history causes the file to grow beyond active use,
    move completed blocks to `CLIENT_SERVER_MEDIATION_ARCHIVE.md` and leave a short reference pointer.
- Archive cadence for Sprint work: keep only current sprint kickoff + latest 1-2 update entries in this file;
    move older completed updates to archive.
- Start-of-handoff archive check (automatic): at the beginning of every new handoff/update cycle, verify this file only contains active sprint kickoff + latest 1-2 updates; immediately move older completed blocks to `CLIENT_SERVER_MEDIATION_ARCHIVE.md`.
- Moderator relay standard (default): keep relay prompts to one simple line unless extra detail is explicitly requested.
- Preferred relay text for new work handoff: `New commit on main with handoff updates. Pull and resume from the current checklist.`
- Moderator relay mode: mediator sends only short notifications between machines (example: "new handoff update available; pull and continue").
- Git push responsibility (default): assistant pushes commits to remote; moderator relays notifications.
- All complex instructions, technical details, acceptance criteria, and troubleshooting context MUST be written in this handoff document, not relayed verbally.

## Moderator Short-Ping Templates

- `New handoff update is in docs/development/CLIENT_SERVER_MEDIATION_HANDOFF.md. Pull latest main and continue.`
- `Please read the latest Sprint section in the handoff doc and post results back there.`
- `New commit on main with handoff updates. Pull and resume from the current checklist.`

## Current Status

**Issues #1–#45 fully resolved.** See [CLIENT_SERVER_MEDIATION_ARCHIVE.md](CLIENT_SERVER_MEDIATION_ARCHIVE.md) for details.

**Batch 4 — ALL ISSUES RESOLVED:**
- Issue #43 (Task 4.3): Symbolic link policy — server ✅ `d3a6422`, client ✅ `1cd594a`
- Issue #44 (Task 4.4): inotify/inode health monitoring — server ✅ `d3a6422`, client ✅ `1cd594a`
- Issue #45 (Task 4.5): Path length/filename validation — server ✅ `d3a6422`, client ✅ `1cd594a`

**Batch 5 — ALL ISSUES RESOLVED:**
- Issue #46 (Task 5.1): Bandwidth throttling — client ✅ complete
- Issue #47 (Task 5.2): Selective sync folder browser — client ✅ complete

**All sync improvement batches (1–5) are now complete.** The sync improvement plan is closed.
See [SYNC_IMPROVEMENT_PLAN.md](SYNC_IMPROVEMENT_PLAN.md) for full history.

**Next work (server):** Sprint A/B/C closeout work in this handoff is complete.
Continue from the next prioritized step in `docs/MASTER_PROJECT_PLAN.md`.

**Acceptance update (2026-03-10):** User accepted Sprint A/B/C completion. Temporary execution plan
`docs/development/REMAINING_PHASE0_PHASE1_3SPRINT_PLAN.md` has been removed per closeout note.
Next prioritized implementation target remains `phase-2.3` (Chat Business Logic & Services).

**Phase 2.3 acceptance update (2026-03-10):** User accepted Phase 2.3 completion.
Temporary file `docs/development/PHASE_2_3_EXECUTION_PLAN.md` has been removed per closeout rule.
Next prioritized implementation target is `phase-2.4` (Chat REST API Endpoints).

## Environment

| | Machine | Detail |
|---|---------|--------|
| Server | `mint22` | `https://mint22:15443/` |
| Client | `Windows11-TestDNC` | Sync dir: `C:\Users\benk\Documents\synctray` |

## Key Architecture Decisions (Carry Forward)

- **Auth:** OpenIddict bearer on all files/sync endpoints via `FilesControllerBase` `[Authorize]`. Persistent RSA keys in `{DOTNETCLOUD_DATA_DIR}/oidc-keys/`. `DisableAccessTokenEncryption()`.
- **API contract:** All endpoints use `GetAuthenticatedCaller()` (no `userId` query param). All return raw payloads — `ResponseEnvelopeMiddleware` wraps automatically. Client unwraps envelope via `ReadEnvelopeDataAsync<T>()`.
- **Sync flow:** changes → tree → reconcile → chunk manifest → chunk download → file assembly. `since` param converted to UTC kind. Client builds `nodeId→path` map from folder tree.
- **Token handling:** Client uses `DateTimeOffset` for expiry. `RefreshTokenAsync` sends `client_id`. `OAuthConstants.ClientId = "dotnetcloud-desktop"`.

## Relay Template

```markdown
### Send to [Server|Client] Agent
<message text>

### Request Back
- commit hash
- raw endpoint/URL used
- raw error/query params
- raw log lines around the event (with timestamp)
```

## Active Handoff

### Sprint Track (Phase 2.3 Closeout)

Reference tracker: Phase 2.3 accepted and closed out; continue from `docs/MASTER_PROJECT_PLAN.md` (`phase-2.4`).

- ✓ Sprint A kickoff sent
- ✓ Sprint A complete (`phase-1.19.2`)
- ✓ Sprint B kickoff sent (`phase-1.15` deferred hardening)
- ✓ Sprint B complete (`phase-1.15` deferred hardening)
- ✓ Sprint C complete (`phase-1.12` deferred UX/media)

### Phase 2.3 Update #1 - Service Hardening + Verification (Server, Linux workspace)

**Date:** 2026-03-10  
**Owner:** Server (`Linux workspace`)  
**Status:** completed ✅

**Commit hash:** `260199c`

**Files added/updated:**
- `src/Modules/Chat/DotNetCloud.Modules.Chat.Data/Services/ChannelMemberService.cs`
- `src/Modules/Chat/DotNetCloud.Modules.Chat.Data/Services/ReactionService.cs`
- `src/Modules/Chat/DotNetCloud.Modules.Chat.Data/Services/PinService.cs`
- `src/Modules/Chat/DotNetCloud.Modules.Chat.Data/Services/TypingIndicatorService.cs`
- `tests/DotNetCloud.Modules.Chat.Tests/ChannelMemberServiceTests.cs` (new)
- `tests/DotNetCloud.Modules.Chat.Tests/ReactionServiceTests.cs`
- `tests/DotNetCloud.Modules.Chat.Tests/PinServiceTests.cs`
- `tests/DotNetCloud.Modules.Chat.Tests/TypingIndicatorServiceTests.cs`
- `docs/development/PHASE_2_3_EXECUTION_PLAN.md` (temporary tracker, removed after acceptance)
- `docs/IMPLEMENTATION_CHECKLIST.md`
- `docs/MASTER_PROJECT_PLAN.md`

**Tests added/updated:**
- `tests/DotNetCloud.Modules.Chat.Tests/ChannelMemberServiceTests.cs`
    - `WhenOwnerAddsMemberThenMembershipIsCreated`
    - `WhenNonAdminAddsMemberThenUnauthorizedAccessExceptionIsThrown`
    - `WhenOutsiderListsMembersThenUnauthorizedAccessExceptionIsThrown`
    - `WhenOwnerDemotesLastOwnerThenInvalidOperationExceptionIsThrown`
    - `WhenCallerMarksReadWithInvalidMessageThenInvalidOperationExceptionIsThrown`
    - `WhenGetUnreadCountsThenMentionsIncludeAllAndChannelTypes`
    - `WhenRemovingLastOwnerThenInvalidOperationExceptionIsThrown`
- `tests/DotNetCloud.Modules.Chat.Tests/ReactionServiceTests.cs`
    - `WhenAddReactionWithWhitespaceEmojiThenEmojiIsTrimmed`
    - `WhenAddReactionAsNonMemberThenThrowsUnauthorizedAccessException`
    - `WhenRemoveReactionAsNonMemberThenThrowsUnauthorizedAccessException`
    - `WhenAddReactionThenReactionAddedEventContainsExpectedPayload`
    - `WhenRemoveReactionThenReactionRemovedEventContainsExpectedPayload`
- `tests/DotNetCloud.Modules.Chat.Tests/PinServiceTests.cs`
    - `WhenPinMessageAsNonMemberThenThrowsUnauthorizedAccessException`
    - `WhenPinMessageFromDifferentChannelThenThrowsInvalidOperationException`
    - `WhenGetPinnedMessagesThenLatestPinIsReturnedFirst`
- `tests/DotNetCloud.Modules.Chat.Tests/TypingIndicatorServiceTests.cs`
    - `WhenNotifyTypingWithEmptyChannelThenThrowsArgumentException`
    - `WhenTypingEntryExpiresThenUserIsRemoved`

**Verification commands and results:**
- `dotnet test tests/DotNetCloud.Modules.Chat.Tests/DotNetCloud.Modules.Chat.Tests.csproj`
    - Final result: total 197, succeeded 197, failed 0, skipped 0
- `dotnet build`
    - Final result: succeeded (full solution)

**Raw failing assertion/error text seen during iteration (fixed):**
- `WhenMultipleUsersReactThenCountIsCorrect`: `System.UnauthorizedAccessException: User <guid> is not a member of channel <guid>.`
    - Fix applied: test now adds the second caller as a channel member before reacting.

**Raw log snippets around authorization/event issues:**
- No runtime log-line capture in test harness (services are constructed with `NullLogger<T>` in unit tests).
- Added server-side warning/info logging statements in services for denied reaction/pin/member-management actions and reaction add/remove events.

**Intentionally deferred items:**
- Client-side compatibility validation pass (DTO/view-model/API-consumer assumptions) is deferred to the client workspace handoff.
- No Phase 2.4/2.5 work started in this update.

### Phase 2.3 Update #2 - Client Validation Block (Windows workspace)

**Date:** 2026-03-10  
**Owner:** Client (`Windows workspace`)  
**Status:** completed ✅

**Commit hash:** `9bcbcbf`

**Client paths reviewed:**
- `src/Modules/Chat/DotNetCloud.Modules.Chat/DTOs/ChatDtos.cs`
- `src/Modules/Chat/DotNetCloud.Modules.Chat/Services/ChatApiClient.cs`
- `src/Modules/Chat/DotNetCloud.Modules.Chat/UI/ViewModels.cs`
- `src/Modules/Chat/DotNetCloud.Modules.Chat/UI/ChannelList.razor.cs`
- `src/Modules/Chat/DotNetCloud.Modules.Chat/UI/MessageList.razor.cs`
- `src/UI/DotNetCloud.UI.Android/Services/ChatApiClient.cs`
- `src/UI/DotNetCloud.UI.Android/Services/SignalRChatService.cs`

**Server paths validated against client assumptions:**
- `src/Modules/Chat/DotNetCloud.Modules.Chat.Data/Services/ChannelMemberService.cs`
- `src/Modules/Chat/DotNetCloud.Modules.Chat.Data/Services/ReactionService.cs`
- `src/Modules/Chat/DotNetCloud.Modules.Chat.Data/Services/PinService.cs`
- `src/Modules/Chat/DotNetCloud.Modules.Chat.Data/Services/TypingIndicatorService.cs`
- `src/Modules/Chat/DotNetCloud.Modules.Chat.Host/Controllers/ChatController.cs`

**Payload shape examples checked:**
- `GET api/v1/chat/unread?userId=<guid>` -> `success: true`, `data: UnreadCountDto[]` with `channelId`, `unreadCount`, `mentionCount`
- `GET api/v1/chat/channels/<channelId>/pins?userId=<guid>` -> `success: true`, `data: MessageDto[]` (ordered by latest pin first)
- `GET api/v1/chat/channels/<channelId>/typing` -> `success: true`, `data: TypingIndicatorDto[]` (5-second in-memory expiry)
- `POST api/v1/chat/messages/<messageId>/reactions?userId=<guid>` -> `success: true`, `data.added: true` on success

**Validation result (client contract):**
- DTO shape/nullability for `UnreadCountDto`, `MessageDto`, `MessageReactionDto`, and `TypingIndicatorDto` remains compatible with current client/UI consumers.
- Behavior assumptions validated:
    - Unread and mention counts include `@all` and `@channel` mentions after last-read boundary.
    - Pinned message retrieval preserves latest-pin-first ordering.
    - Typing indicators expire after 5 seconds and are channel-isolated.
- No mandatory client code changes required for Phase 2.3 acceptance.

**Mismatches found / follow-up actions:**
- Follow-up (server): align Chat REST controller exception mapping for hardened authorization paths (`reactions`, `pins`, `typing`) to deterministic API responses instead of unhandled 500s when service-level `UnauthorizedAccessException` / `InvalidOperationException` bubbles.
- Follow-up (client, non-blocking): once Phase 2.4 endpoints are finalized, add client integration tests for unread/pin/typing endpoint envelopes and denial-path handling.

**Intentionally deferred items:**
- No client runtime implementation changes in this update (validation-only pass).
- No Phase 2.4/2.5 implementation work started.

### Phase 2.4 Update #1 - REST Exception Mapping Hardening (Server, Linux workspace)

**Date:** 2026-03-10  
**Owner:** Server (`Linux workspace`)  
**Status:** completed ✅ (incremental phase-2.4 scope)

**Commit hash:** `7ccc3d1`

**Files added/updated:**
- `src/Modules/Chat/DotNetCloud.Modules.Chat.Host/Controllers/ChatController.cs`
- `tests/DotNetCloud.Modules.Chat.Tests/DotNetCloud.Modules.Chat.Tests.csproj`
- `tests/DotNetCloud.Modules.Chat.Tests/ChatControllerTests.cs` (new)
- `docs/IMPLEMENTATION_CHECKLIST.md`
- `docs/MASTER_PROJECT_PLAN.md`
- `docs/development/CLIENT_SERVER_MEDIATION_HANDOFF.md`

**Implemented in this update:**
1. Added deterministic REST exception mapping for member endpoints (`AddMember`, `RemoveMember`, `GetMembers`, `UpdateMemberRole`, `UpdateNotificationPreference`, `MarkAsRead`) to return expected 403/404 instead of unhandled 500 paths.
2. Added deterministic mapping for reaction endpoints (`AddReaction`, `RemoveReaction`) including 400 for validation (`ArgumentException`) and 403/404 for auth/not-found conditions.
3. Added deterministic mapping for pin endpoints (`PinMessage`, `UnpinMessage`, `GetPinnedMessages`) to return 403/404 on service denials/not-found.
4. Added deterministic mapping for typing endpoints (`NotifyTyping`, `GetTypingUsers`) to return 400 on validation failures.
5. Added controller-level unit tests to validate status-code mapping behavior with mocked services.

**Tests added/updated:**
- `tests/DotNetCloud.Modules.Chat.Tests/ChatControllerTests.cs`
    - `AddReactionAsync_WhenUnauthorized_ThenReturnsForbidResult`
    - `PinMessageAsync_WhenUnauthorized_ThenReturnsForbidResult`
    - `RemoveMemberAsync_WhenUnauthorized_ThenReturnsForbidResult`
    - `NotifyTypingAsync_WhenInvalidArgument_ThenReturnsBadRequest`
    - `GetPinnedMessagesAsync_WhenInvalidOperation_ThenReturnsNotFound`

**Verification commands and results:**
- `dotnet test tests/DotNetCloud.Modules.Chat.Tests/DotNetCloud.Modules.Chat.Tests.csproj`
    - Result: total 202, succeeded 202, failed 0, skipped 0
- `dotnet build`
    - Result: succeeded (full solution)

**Raw failing assertion/error text:**
- None in this update.

**Raw log snippets around authorization/event issues:**
- No runtime log-line capture in controller unit tests (mocked services + status code assertions).

**Intentionally deferred items:**
- Full phase-2.4 completion criteria (controller decomposition decision and endpoint-level integration/API verification) remain open.
- No phase-2.5 implementation started in this update.

### Phase 2.4 Update #2 - Endpoint Completion + API Verification (Server, Linux workspace)

**Date:** 2026-03-10  
**Owner:** Server (`Linux workspace`)  
**Status:** completed ✅

**Commit hash:** `5a6563c`

**Files added/updated:**
- `tests/DotNetCloud.Modules.Chat.Tests/ChatControllerTests.cs`
- `docs/IMPLEMENTATION_CHECKLIST.md`
- `docs/MASTER_PROJECT_PLAN.md`
- `docs/development/CLIENT_SERVER_MEDIATION_HANDOFF.md`

**Implemented in this update:**
1. Added API verification coverage for success-envelope and denial-path status behavior in `ChatControllerTests`.
2. Verified endpoint completion criteria for phase-2.4 using consolidated `ChatController` scope (functional equivalent to split-controller task list).
3. Updated phase tracking artifacts to mark phase-2.4 completed and set next target to phase-2.5.

**Tests added/updated:**
- `tests/DotNetCloud.Modules.Chat.Tests/ChatControllerTests.cs`
    - `AddReactionAsync_WhenSuccessful_ThenReturnsEnvelopeWithAddedFlag`
    - `RemoveReactionAsync_WhenMessageMissing_ThenReturnsNotFound`
    - `MarkAsReadAsync_WhenUnauthorized_ThenReturnsForbidResult`
    - `GetUnreadCountsAsync_WhenSuccessful_ThenReturnsEnvelope`

**Verification commands and results:**
- `dotnet test tests/DotNetCloud.Modules.Chat.Tests/DotNetCloud.Modules.Chat.Tests.csproj`
    - Result: total 206, succeeded 206, failed 0, skipped 0
- `dotnet build`
    - Result: succeeded (full solution)

**Raw failing assertion/error text:**
- None in this update.

**Raw log snippets around authorization/event issues:**
- No runtime log-line capture in controller unit tests (mocked services + status code and envelope assertions).

**Intentionally deferred items:**
- No phase-2.5 implementation started in this update.

### Phase 2.5 Update #1 - SignalR Group Lifecycle + Reconnect Hardening (Server, mint22)

**Date:** 2026-03-10  
**Owner:** Server (`mint22`)  
**Status:** completed ✅ (incremental phase-2.5 scope)

**Commit hash:** `f9e5453`

**Files added/updated:**
- `src/Core/DotNetCloud.Core.Server/RealTime/UserConnectionTracker.cs`
- `src/Core/DotNetCloud.Core.Server/RealTime/RealtimeBroadcasterService.cs`
- `src/Core/DotNetCloud.Core.Server/RealTime/CoreHub.cs`
- `src/Modules/Chat/DotNetCloud.Modules.Chat.Data/Services/ChannelService.cs`
- `src/Modules/Chat/DotNetCloud.Modules.Chat.Data/Services/ChannelMemberService.cs`
- `tests/DotNetCloud.Core.Server.Tests/RealTime/CoreHubTests.cs` (new)
- `tests/DotNetCloud.Core.Server.Tests/RealTime/UserConnectionTrackerTests.cs`
- `tests/DotNetCloud.Core.Server.Tests/RealTime/RealtimeBroadcasterServiceTests.cs`
- `tests/DotNetCloud.Modules.Chat.Tests/ChannelServiceTests.cs`
- `tests/DotNetCloud.Modules.Chat.Tests/ChannelMemberServiceTests.cs`
- `docs/IMPLEMENTATION_CHECKLIST.md`
- `docs/MASTER_PROJECT_PLAN.md`
- `docs/development/CLIENT_SERVER_MEDIATION_HANDOFF.md`

**Implemented in this update:**
1. Added persistent per-user SignalR group membership tracking to `UserConnectionTracker` (`AddGroupMembership`, `RemoveGroupMembership`, `GetGroups`) so channel group intent survives disconnects.
2. Updated `RealtimeBroadcasterService` to persist/clear tracked memberships on `AddToGroupAsync` and `RemoveFromGroupAsync`.
3. Updated `CoreHub.OnConnectedAsync` to re-join all tracked groups for the connecting user/connection.
4. Wired chat data-layer lifecycle to realtime groups:
     - `ChannelService`: add all initial members to channel group on create/DM create; remove all members from group on delete.
     - `ChannelMemberService`: add/remove member group membership on join/leave.
5. Added focused coverage across core realtime + chat service tests.

**Tests added/updated:**
- `tests/DotNetCloud.Core.Server.Tests/RealTime/CoreHubTests.cs`
    - `WhenUserHasTrackedGroupsThenOnConnectedAddsConnectionToEachGroup`
- `tests/DotNetCloud.Core.Server.Tests/RealTime/UserConnectionTrackerTests.cs`
    - `WhenGroupMembershipAddedThenGetGroupsReturnsGroup`
    - `WhenGroupMembershipRemovedThenGetGroupsReturnsEmpty`
    - `WhenUserGoesOfflineThenGroupMembershipIsRetained`
    - `WhenGroupNameIsNullThenAddGroupMembershipThrows`
    - `WhenGroupNameIsNullThenRemoveGroupMembershipThrows`
- `tests/DotNetCloud.Core.Server.Tests/RealTime/RealtimeBroadcasterServiceTests.cs`
    - `WhenAddToGroupWithNoConnectionsThenDoesNothing` (extended to assert membership tracking)
    - `WhenRemoveFromGroupThenTrackedMembershipIsRemoved`
- `tests/DotNetCloud.Modules.Chat.Tests/ChannelServiceTests.cs`
    - `WhenDeleteChannelThenRealtimeGroupMembershipIsRemovedForAllMembers`
    - `WhenCreateChannelWithMembersThenMembersAreAdded` (extended to assert realtime group add calls)
- `tests/DotNetCloud.Modules.Chat.Tests/ChannelMemberServiceTests.cs`
    - `WhenAdminRemovesMemberThenRealtimeGroupMembershipIsRemoved`
    - `WhenOwnerAddsMemberThenMembershipIsCreated` (extended to assert realtime group add call)

**Verification commands and results:**
- `dotnet test tests/DotNetCloud.Core.Server.Tests/DotNetCloud.Core.Server.Tests.csproj`
    - Result: total 322, succeeded 320, failed 0, skipped 2
- `dotnet test tests/DotNetCloud.Modules.Chat.Tests/DotNetCloud.Modules.Chat.Tests.csproj`
    - Result: total 208, succeeded 208, failed 0, skipped 0
- `dotnet build`
    - Result: succeeded (full solution)

**Raw failing assertion/error text seen during iteration (fixed):**
- `CoreHubTests.cs`: `error CS0546: 'TestHubCallerContext.Items.set': cannot override because 'HubCallerContext.Items' does not have an overridable set accessor`
- `CoreHubTests.cs`: `error CS0534: 'TestHubCallerContext' does not implement inherited abstract member 'HubCallerContext.Features.get'`
    - Fix applied: test stub now exposes read-only `Items` backing store and implements `Features` with `FeatureCollection`.

**Raw log snippets around authorization/event issues:**
- No runtime service logs captured in unit tests (tests use `NullLogger<T>` or mocks); verification performed via behavior assertions.

**Intentionally deferred items:**
- Chat-specific client-to-server hub methods (`SendMessage`, `EditMessage`, `DeleteMessage`, `StartTyping`, `StopTyping`, `MarkRead`, `AddReaction`, `RemoveReaction`) remain pending.
- Presence custom status message and `PresenceChangedEvent` cross-module event integration remain pending.

### Sprint A Kickoff - Phase 1.19.2 (Files API Integration Depth)

**Sprint goal:** Complete `phase-1.19.2` by expanding Files API integration tests beyond isolation paths.

**Owner split:**
- Server: primary implementation and test expansion in `tests/DotNetCloud.Integration.Tests/`
- Client: contract compatibility validation against response envelope/auth expectations

**Kickoff checklist:**
- ✓ Scope confirmed: CRUD/tree/search/favorites, chunked upload E2E, version/share/trash flows, WOPI+sync smoke
- ✓ Mediator workflow confirmed: relay via this handoff doc
- ✓ Server kickoff message sent
- ✓ Client validation message sent

### Send to Server Agent
Execute Sprint A for `phase-1.19.2` in `tests/DotNetCloud.Integration.Tests/`.

Required coverage:
1. REST CRUD/tree/search/favorites end-to-end tests.
2. Chunked upload E2E tests (initiate, upload, complete, dedup behavior, quota rejection path).
3. Version/share/trash end-to-end tests.
4. WOPI and sync endpoint smoke tests (auth enforcement + payload shape).
5. Document provider matrix execution: PostgreSQL required; SQL Server if environment is available.

Update this handoff doc with test inventory, remaining gaps, and completion status.

### Request Back
- commit hash
- exact tests added/updated (file paths + test names)
- raw endpoint/URL used for any failing test
- raw error/query params
- raw log lines around failures (timestamped)
- list of any intentionally deferred coverage

### Send to Client Agent
Validate Sprint A output for client compatibility risk.

Checks required:
1. No response-envelope contract regressions for `DotNetCloudApiClient` paths.
2. No auth-flow regressions for Files/sync/WOPI endpoint consumption assumptions.
3. Note any required client-side follow-up tests or fixes.

### Request Back
- commit hash (if any client-side changes)
- affected client paths reviewed
- raw endpoint/URL + payload shape examples checked
- any mismatch found between integration behavior and client assumptions

### Sprint A Historical Updates (Archived)

Completed Sprint A updates `#1` through `#9` are archived in
`docs/development/CLIENT_SERVER_MEDIATION_ARCHIVE.md` under
`Sprint A Archive (Phase 1.19.2)` and
`Sprint A Archive Continuation (Phase 1.19.2 - updates #5-#9)`.

### Sprint B Kickoff - Phase 1.15 Deferred Hardening (SyncService Identity Boundaries)

**Sprint goal:** Close deferred hardening items in `phase-1.15` with priority on IPC caller identity enforcement and per-context privilege boundaries.

**Owner split:**
- Client: primary implementation in `DotNetCloud.Client.SyncService` and platform plumbing.
- Server: identity/contract review and sign-off on failure semantics.

**Kickoff checklist:**
- ✓ Scope confirmed: Linux privilege dropping, Windows impersonation, IPC identity verification, trigger debounce, disk-full surfacing.
- ✓ Expected identity semantics posted in handoff (this update).
- ✓ Expected failure semantics posted in handoff (this update).
- ✓ Client implementation kickoff message sent.

### Sprint B - Expected Caller Identity (IPC/SyncService)

These expectations define the security contract for `IpcServer`/`IpcClientHandler` once Sprint B is implemented:

1. Connection identity must come from transport-level OS credentials, not from JSON payload fields.
2. On Linux/macOS Unix socket connections, caller identity must be resolved from peer credentials (UID/GID) and mapped to a normalized OS user identity.
3. On Windows named-pipe connections, caller identity must be resolved from the authenticated pipe client token and mapped to a normalized SID/account identity.
4. Every `SyncContextRegistration` is owner-scoped by `OsUserName`; context-scoped commands must execute only when caller identity matches the context owner identity.
5. `list-contexts` must be caller-filtered (return only contexts owned by the connected caller).
6. Push events for `subscribe` must be filtered to caller-owned contexts only.
7. If identity cannot be established reliably, no mutating command may execute.

### Sprint B - Expected Failure Semantics (IPC/SyncService)

Use deterministic denial behavior for identity-boundary violations:

1. Identity unavailable/unverifiable: reject command with `success=false` and error text `Caller identity unavailable.`
2. Context ownership mismatch: reject command with `success=false` and error text `Context not found or inaccessible.`
3. Unknown context for caller: same response as mismatch (`Context not found or inaccessible.`) to avoid cross-user context enumeration.
4. Invalid or missing required fields (`contextId`, `data`, malformed JSON): reject with `success=false` and existing bad-request style error text.
5. Privilege transition failure (Linux `setresuid`/`setresgid`, Windows impersonation): reject command with `success=false`, emit sync error event, and log raw OS/platform error details server-side.
6. Debounce/rate-limit rejections for `sync-now`: return `success=true` with an explicit no-op payload (`started=false`, `reason="rate-limited"`) rather than a hard failure.
7. Identity-boundary failures must be logged with timestamp, command, normalized caller identity, target contextId, and denial reason.

### Send to Client Agent
Execute Sprint B for `phase-1.15` deferred hardening in `src/Clients/DotNetCloud.Client.SyncService/` using the identity and failure semantics above as required contract.

Required work focus:
1. Implement caller-identity extraction and context ownership enforcement at IPC boundary.
2. Implement Linux privilege dropping path per context.
3. Implement Windows impersonation path per context.
4. Add sync trigger debounce/rate limiting behavior with observable no-op response semantics.
5. Add disk-full detection and tray-facing notification path.

### Request Back
- commit hash
- exact files and tests added/updated (paths + test names)
- raw IPC command/response examples for denial paths
- raw log lines around identity mismatch and privilege-transition failures (timestamped)
- platform matrix evidence (Linux + Windows behaviors)

### Sprint B Historical Updates (Archived)

Completed Sprint B updates `#1` and `#2` are archived in
`docs/development/CLIENT_SERVER_MEDIATION_ARCHIVE.md` under
`Sprint B Archive (Phase 1.15 - updates #1-#2, archived 2026-03-10)`.

### Sprint B Update #3 - Windows Impersonation Execution Boundary (Server, Windows workspace)

**Date:** 2026-03-10  
**Owner:** Server (`Windows workspace`)  
**Status:** completed ✅

**Files added/updated:**
- `src/Clients/DotNetCloud.Client.SyncService/Ipc/IpcCallerIdentity.cs`
- `src/Clients/DotNetCloud.Client.SyncService/Ipc/IpcServer.cs`
- `src/Clients/DotNetCloud.Client.SyncService/Ipc/IpcClientHandler.cs`

**Implemented in this update:**
1. `IpcServer` now captures Windows named-pipe caller identity plus a duplicated caller access token at connection time.
2. `IpcCallerIdentity` now carries the duplicated Windows access token alongside normalized caller identity values.
3. `IpcClientHandler` now executes context-scoped operations under `WindowsIdentity.RunImpersonated` when a caller token is available.
4. Handler completion now disposes duplicated caller token handles to avoid leaking Windows token resources.
5. Failure semantics for impersonation transition errors now return deterministic IPC command errors: `Privilege transition failed.` with server-side error logs.

**Tests added/updated:**
- No new test files required.
- Existing SyncService suite run as regression validation.

**Command executed:**
- `dotnet test tests\DotNetCloud.Client.SyncService.Tests\DotNetCloud.Client.SyncService.Tests.csproj`
    - Result: total 27, succeeded 27, failed 0, skipped 0

**Remaining for Sprint B:**
- Linux per-context privilege drop (`setresuid`/`setresgid`) implementation.

### Sprint B Update #4 - Linux Privilege Drop Execution Boundary (Server, Linux workspace)

**Date:** 2026-03-10  
**Owner:** Server (`Linux workspace`)  
**Status:** completed ✅

**Files added/updated:**
- `src/Clients/DotNetCloud.Client.SyncService/Ipc/IpcCallerIdentity.cs`
- `src/Clients/DotNetCloud.Client.SyncService/Ipc/IpcServer.cs`
- `src/Clients/DotNetCloud.Client.SyncService/Ipc/IpcClientHandler.cs`
- `docs/IMPLEMENTATION_CHECKLIST.md`
- `docs/MASTER_PROJECT_PLAN.md`

**Implemented in this update:**
1. `IpcServer` now resolves Linux Unix-socket caller peer credentials (`SO_PEERCRED`) and maps UID/GID + account identity into `IpcCallerIdentity`.
2. `IpcCallerIdentity` now carries Unix UID/GID fields used for Linux privilege transitions.
3. `IpcClientHandler` now executes context-scoped operations under guarded Linux privilege transition using `setresgid`/`setresuid`, then restores original IDs after operation completion.
4. Linux privilege-transition failures now return deterministic IPC command error `Privilege transition failed.` and log raw errno with caller/context metadata.
5. Linux transition path is serialized with a transition lock to avoid overlapping process-credential mutation during context-scoped operations.

**Tests/validation executed:**
- `dotnet test tests/DotNetCloud.Client.SyncService.Tests/DotNetCloud.Client.SyncService.Tests.csproj`
    - Result: total 27, succeeded 27, failed 0, skipped 0
- `dotnet build`
    - Result: succeeded (full solution)

**Remaining for Sprint B:**
- None. Sprint B hardening scope is complete.

### Sprint C Update #1 - Folder Drag-and-Drop Recursive Upload (Server, Linux workspace)

**Date:** 2026-03-10  
**Owner:** Server (`Linux workspace`)  
**Status:** completed ✅

**Files added/updated:**
- `src/UI/DotNetCloud.UI.Web/wwwroot/js/files-drop-bridge.js`
- `src/UI/DotNetCloud.UI.Web/wwwroot/js/file-upload.js`
- `src/Modules/Files/DotNetCloud.Modules.Files/UI/FileUploadComponent.razor.cs`
- `src/Modules/Files/DotNetCloud.Modules.Files/UI/ViewModels.cs`
- `docs/IMPLEMENTATION_CHECKLIST.md`
- `docs/MASTER_PROJECT_PLAN.md`
- `docs/development/REMAINING_PHASE0_PHASE1_3SPRINT_PLAN.md`

**Implemented in this update:**
1. Browser drop bridge now traverses dropped directories recursively via `DataTransferItem.webkitGetAsEntry()` and collects file entries with relative paths.
2. Upload pipeline now preserves relative folder structure by resolving/creating nested folders through Files API (`GET /api/v1/files`, `POST /api/v1/files/folders`) before file upload.
3. Upload metadata now carries `RelativePath` from JS to Blazor queue model for dropped folder entries.
4. Existing single-file and multi-file drop/select upload flow remains intact.

**Tests/validation executed:**
- `dotnet test tests/DotNetCloud.Integration.Tests/DotNetCloud.Integration.Tests.csproj --filter "FullyQualifiedName~FilesThumbnailIntegrationTests"`
    - Result: total 2, succeeded 2, failed 0, skipped 0
- `dotnet build src/Modules/Files/DotNetCloud.Modules.Files/DotNetCloud.Modules.Files.csproj`
    - Result: succeeded
- `dotnet build src/UI/DotNetCloud.UI.Web/DotNetCloud.UI.Web.csproj`
    - Result: succeeded
- `dotnet build`
    - Result: failed due to pre-existing upstream test constructor mismatch in `tests/DotNetCloud.Modules.Files.Tests/Host/FilesControllerChunkDownloadTests.cs` (missing new `fileSystemOptions` ctor argument)

**Remaining for Sprint C:**
- Video thumbnail generation integration (FFmpeg)
- PDF thumbnail generation integration (PDF renderer)
- Touch gestures for preview (JS touch interop)

### Sprint C Update #2 - Video Thumbnail Generation (Server, Windows workspace)

**Date:** 2026-03-10  
**Owner:** Server (`Windows workspace`)  
**Status:** completed ✅

**Files added/updated:**
- `src/Modules/Files/DotNetCloud.Modules.Files/Services/IVideoFrameExtractor.cs`
- `src/Modules/Files/DotNetCloud.Modules.Files/Services/FfmpegVideoFrameExtractor.cs`
- `src/Modules/Files/DotNetCloud.Modules.Files/Services/ThumbnailService.cs`
- `src/Modules/Files/DotNetCloud.Modules.Files/Services/IThumbnailService.cs`
- `src/Modules/Files/DotNetCloud.Modules.Files.Data/FilesServiceRegistration.cs`
- `tests/DotNetCloud.Modules.Files.Tests/Services/ThumbnailServiceTests.cs`
- `tests/DotNetCloud.Modules.Files.Tests/Host/FilesControllerChunkDownloadTests.cs`
- `docs/IMPLEMENTATION_CHECKLIST.md`
- `docs/MASTER_PROJECT_PLAN.md`

**Implemented in this update:**
1. Added a video frame extraction abstraction (`IVideoFrameExtractor`) and FFmpeg implementation (`FfmpegVideoFrameExtractor`) with configurable executable path (`Files:Thumbnails:FfmpegPath`, default `ffmpeg`).
2. Extended `ThumbnailService` to process common video MIME types by extracting first-frame JPEGs and generating cached 128/256/512 thumbnails.
3. Kept image thumbnail generation flow unchanged while adding video path and temporary extraction file cleanup safeguards.
4. Wired extractor through DI (`FilesServiceRegistration`) so runtime upload completion can generate video thumbnails through the existing service pipeline.
5. Added focused unit tests for successful and failed video extraction paths; fixed upstream test constructor mismatch after `FilesController` signature expansion.

**Tests/validation executed:**
- `dotnet test tests\DotNetCloud.Modules.Files.Tests\DotNetCloud.Modules.Files.Tests.csproj --filter "FullyQualifiedName~ThumbnailServiceTests"`
    - Result: total 2, succeeded 2, failed 0, skipped 0
- `dotnet test tests\DotNetCloud.Integration.Tests\DotNetCloud.Integration.Tests.csproj --filter "FullyQualifiedName~FilesThumbnailIntegrationTests"`
    - Result: total 2, succeeded 2, failed 0, skipped 0

**Remaining for Sprint C:**
- PDF thumbnail generation integration (PDF renderer)
- Touch gestures for preview (JS touch interop)

### Sprint C Update #3 - PDF Thumbnail + Touch Gesture Completion (Server, Windows workspace)

**Date:** 2026-03-10  
**Owner:** Server (`Windows workspace`)  
**Status:** completed ✅

**Files added/updated:**
- `src/Modules/Files/DotNetCloud.Modules.Files/Services/IPdfPageRenderer.cs`
- `src/Modules/Files/DotNetCloud.Modules.Files/Services/PdftoppmPdfPageRenderer.cs`
- `src/Modules/Files/DotNetCloud.Modules.Files/Services/ThumbnailService.cs`
- `src/Modules/Files/DotNetCloud.Modules.Files/Services/IThumbnailService.cs`
- `src/Modules/Files/DotNetCloud.Modules.Files.Data/FilesServiceRegistration.cs`
- `src/Modules/Files/DotNetCloud.Modules.Files/UI/FilePreview.razor`
- `src/Modules/Files/DotNetCloud.Modules.Files/UI/FilePreview.razor.cs`
- `src/UI/DotNetCloud.UI.Web/wwwroot/js/file-preview-gestures.js`
- `src/UI/DotNetCloud.UI.Web/Components/App.razor`
- `tests/DotNetCloud.Modules.Files.Tests/Services/ThumbnailServiceTests.cs`
- `docs/IMPLEMENTATION_CHECKLIST.md`
- `docs/MASTER_PROJECT_PLAN.md`
- `docs/development/REMAINING_PHASE0_PHASE1_3SPRINT_PLAN.md`

**Implemented in this update:**
1. Added PDF first-page thumbnail rendering pipeline via `IPdfPageRenderer` + `PdftoppmPdfPageRenderer` (configurable command path: `Files:Thumbnails:PdfToPpmPath`, default `pdftoppm`).
2. Extended `ThumbnailService` to generate cached thumbnails for `application/pdf` using the same 128/256/512 cache strategy as image/video.
3. Added touch gesture support to `FilePreview`: swipe left/right to navigate and pinch zoom for image previews.
4. Added browser touch bridge script (`file-preview-gestures.js`) and wired it through `App.razor` and `FilePreview` JS interop lifecycle.
5. Expanded thumbnail unit tests to cover PDF success/failure paths in addition to existing video coverage.

**Tests/validation executed:**
- `dotnet test tests\DotNetCloud.Modules.Files.Tests\DotNetCloud.Modules.Files.Tests.csproj --filter "FullyQualifiedName~ThumbnailServiceTests"`
    - Result: total 4, succeeded 4, failed 0, skipped 0
- `dotnet test tests\DotNetCloud.Integration.Tests\DotNetCloud.Integration.Tests.csproj --filter "FullyQualifiedName~FilesThumbnailIntegrationTests"`
    - Result: total 2, succeeded 2, failed 0, skipped 0

**Remaining for Sprint C:**
- None. Sprint C deferred UX/media scope is complete.

---

**Sync Remediation — Issues #48–#61**

Verification of the sync implementation (2026-03-09) found 4 missing and 10 partial items.
Full plan: [SYNC_REMEDIATION_PLAN.md](SYNC_REMEDIATION_PLAN.md)

### Remediation Batch A — Quick Wins (next up)

| Issue | Task | Owner | Complexity | Description |
|-------|------|-------|------------|-------------|
| #49 | 2.6 | BOTH | LOW | Client ETag/If-None-Match for chunk downloads — ✅ `158ebdc` |
| #50 | 2.3 | CLIENT | LOW | Compression skip for pre-compressed MIME types — ✅ `158ebdc` |
| #52 | 1.2 | SERVER | LOW | RequestId in Serilog LogContext — ✅ `0a0ab19` |
| #54 | 1.9 | SERVER | LOW | Content-Disposition on versioned downloads — ✅ `0a0ab19` |
| #59 | 1.5 | CLIENT | LOW | TaskCanceledException retry in chunk transfers — ✅ `158ebdc` |
| #61 | 3.2 | CLIENT | LOW | Session resume window 18h → 48h — ✅ `158ebdc` |

**Server issues (#52, #54):** ✅ COMPLETE — commit `0a0ab19`  
**Client issues (#49, #50, #59, #61):** ✅ COMPLETE — commit `158ebdc`

### Status: ✅ Batch A fully resolved.

---

### Remediation Batch B — Medium Items (next up)

| Issue | Task | Owner | Complexity | Description | Status |
|-------|------|-------|------------|-------------|--------|
| #51 | 4.1 | CLIENT | MEDIUM | Case-sensitivity handling in SyncEngine | ✅ |
| #55 | 3.5b | CLIENT | MEDIUM | Conflict resolution settings in sync-settings.json | ✅ |
| #57 | 4.3/4.4 | CLIENT | LOW | FSW.Error event + symlink config | ✅ |
| #58 | 5.2 | CLIENT | MEDIUM | Selective sync cleanup + lazy load | ✅ |

**All client-side. No server work in this batch.**

#### Issue #51 (Task 4.1) — Case-Sensitivity Handling

- In `SyncEngine`, use `StringComparer.OrdinalIgnoreCase` for path comparisons on Windows/macOS (check `RuntimeInformation.IsOSPlatform` or equivalent).
- Before applying a remote file locally, check if a file with different casing already exists at the target path.
- If a case conflict exists on a case-insensitive filesystem, rename the incoming file to `filename (case conflict).ext`.
- Log a warning with both path variants.
- Add unit tests for case-conflict detection and renaming logic.
- Reference: `SYNC_IMPLEMENTATION_GUIDE.md` Task 4.1.

#### Issue #55 (Task 3.5b) — Conflict Resolution Settings

- Add a `conflictResolution` section to `sync-settings.json` with defaults: `{ "autoResolveEnabled": true, "newerWinsThresholdMinutes": 5, "enabledStrategies": ["identical", "fast-forward", "clean-merge", "newer-wins", "append-only"] }`.
- Wire `ConflictResolver` to read these settings from config instead of hardcoded values (e.g., the 5-minute newer-wins threshold).
- Add Settings UI controls: checkboxes for each strategy, a threshold input for `newerWinsThresholdMinutes`.
- Add unit tests verifying config-driven behavior.

#### Issue #57 (Tasks 4.3, 4.4) — FSW.Error Event + Symlink Config

- Subscribe to `FileSystemWatcher.Error` event — log the error, set `_pollingFallback = true`, and notify the user via the tray/notification system.
- Add a `symlinks` section to `sync-settings.json`: `{ "mode": "ignore" }` (with `"ignore"` and `"sync-as-link"` as valid values).
- Add a Settings UI dropdown for symlink mode.

#### Issue #58 (Task 5.2) — Selective Sync Cleanup + Lazy Load

- In `FolderBrowserViewModel`, implement lazy-load children on expand (there's currently a TODO comment for this).
- When a folder is unchecked in selective sync, delete local files for that folder (with a confirmation dialog before deletion).
- Add unit tests for lazy-load and cleanup behavior.

#### After completing all four

Update `docs/development/CLIENT_SERVER_MEDIATION_HANDOFF.md` — mark all four issues in the Batch B table with ✅ and commit hashes. Update `docs/development/SYNC_REMEDIATION_PLAN.md` to mark #51, #55, #57, and #58 as ✓.

### Status: ✅ Batch B fully resolved.

---

## Resolved Issues Archive (Batch 5)

Completed Batch 5 implementation details were archived to
`docs/development/CLIENT_SERVER_MEDIATION_ARCHIVE.md` under
`Resolved Issues Archive (Batch 5, archived 2026-03-10)`.
