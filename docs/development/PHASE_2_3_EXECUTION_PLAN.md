# Phase 2.3 Execution Plan - Chat Business Logic & Services

**Created:** 2026-03-10  
**Status:** Completed (server-side + client validation, pending final acceptance)  
**Scope:** Complete `phase-2.3` in `docs/MASTER_PROJECT_PLAN.md` and prepare clean handoff into `phase-2.4` and `phase-2.5`.

## Temporary Plan Notice

- ☐ **Delete this document upon user acceptance of Phase 2.3 completion.**
- ☐ Remove plan reference from `docs/development/CLIENT_SERVER_MEDIATION_HANDOFF.md` after acceptance.

## Process Contract (Mediator Workflow)

- Continue using `docs/development/CLIENT_SERVER_MEDIATION_HANDOFF.md` as the source of truth for cross-machine relay.
- Keep server/client work separated and relay-only through mediator.
- Post technical details, evidence, and acceptance criteria in handoff updates, not short pings.

## Owner Split (Server vs Client)

- **Server owner:** Chat module service implementations, authorization semantics, event consistency, test expansion in `tests/DotNetCloud.Modules.Chat.Tests`.
- **Client owner:** Compatibility validation for DTO/contract assumptions consumed by chat UI and client API paths; identify any follow-up needed in client-side adapters/view models.

## Execution Sequence

### Step 1 - Baseline and Gap Audit (0.5 day)

- ✓ Confirm existing services already present in code:
  - `ChannelMemberService`
  - `ReactionService`
  - `PinService`
  - `TypingIndicatorService`
- ✓ Produce explicit gap checklist against `phase-2.3` deliverables.
- ✓ Identify missing authorization checks, deterministic error semantics, and test gaps.

### Step 2 - ChannelMemberService Hardening (1 day)

- ✓ Verify add/remove/role-change authorization boundaries.
- ✓ Validate unread count and mention count edge cases (`LastReadAt` null/set, membership changes).
- ✓ Ensure deterministic errors for not-found/forbidden/invalid transitions.
- ✓ Add/adjust tests for authorization and unread/mention math.

### Step 3 - ReactionService Hardening (0.5 day)

- ✓ Enforce membership/visibility checks before add/remove reaction.
- ✓ Validate emoji normalization and idempotent duplicate handling.
- ✓ Verify `ReactionAddedEvent` and `ReactionRemovedEvent` payload consistency.
- ✓ Add edge-case tests for duplicate add, missing remove, and unauthorized access.

### Step 4 - PinService Hardening (0.5 day)

- ✓ Enforce channel membership/permission checks for pin/unpin/list.
- ✓ Validate cross-channel pin prevention and not-found behavior.
- ✓ Confirm pinned-list ordering and payload completeness.
- ✓ Add tests for duplicate pin idempotency and invalid pin targets.

### Step 5 - TypingIndicatorService Finalization (0.5 day)

- ✓ Validate time-expiry semantics at boundary conditions.
- ✓ Validate channel isolation and concurrency behavior.
- ✓ Confirm singleton in-memory lifecycle expectations are documented.
- ✓ Add/adjust tests for cleanup and expiration behavior.

### Step 6 - Cross-Service Consistency Pass (0.5 day)

- ✓ Align exception style and logging fields across all Phase 2.3 services.
- ✓ Ensure cancellation token flow and non-blocking async behavior.
- ✓ Ensure DTO projections are consistent with existing client/UI assumptions.

### Step 7 - Verification Gate (0.5 day)

- ✓ Run targeted tests for `tests/DotNetCloud.Modules.Chat.Tests`.
- ✓ Run broader solution build and any affected module tests.
- ✓ Record pass/fail evidence and intentionally deferred items in handoff.

### Step 8 - Documentation and Tracking Closeout (mandatory, 0.5 day)

- ✓ Update `docs/IMPLEMENTATION_CHECKLIST.md` with `✓` and `☐` states for completed/remaining items.
- ✓ Update `docs/MASTER_PROJECT_PLAN.md` Quick Status Summary and `phase-2.3` section:
  - ✓ `**Status:** completed`
  - ✓ Deliverables marked with `✓`
  - ✓ Notes updated with outcome and next target (`phase-2.4`, `phase-2.5`)
- ✓ Add closeout evidence entry in handoff doc.

## Handoff Relay Blocks

### Send to Server Agent
Execute `phase-2.3` completion work in Chat services and tests.

Required focus:
1. Hardening and verification of `ChannelMemberService`, `ReactionService`, `PinService`, and `TypingIndicatorService`.
2. Authorization and deterministic error semantics.
3. Event payload consistency.
4. Test expansion for edge and permission cases.
5. Build/test evidence capture.

### Request Back
- commit hash
- exact files and tests changed (paths + test names)
- raw failing assertion/error text for any unresolved test
- raw log snippets for authorization denials or event emission failures
- list of intentionally deferred items

### Send to Client Agent
Validate Phase 2.3 output for consumer compatibility risk.

Checks required:
1. DTO shape and nullability expectations used by chat UI/view models.
2. Behavior assumptions for unread counts, mention counts, pinned message ordering, and typing indicators.
3. Any required follow-up in client API adapters or Blazor view models.

### Request Back
- commit hash (if client-side changes)
- client paths reviewed
- payload shape examples checked
- mismatches found and required follow-up actions

### Client Validation Result (Completed 2026-03-10)

- ✓ Client validation block executed in Windows workspace.
- ✓ DTO shape and nullability assumptions verified against current client/UI consumers.
- ✓ Behavior assumptions verified for unread/mention counts, pinned ordering, and typing indicator expiry.
- ✓ No blocking client-side code changes required for Phase 2.3.
- ☐ Server follow-up remains: deterministic REST exception mapping for authorization/not-found in hardened reaction/pin/typing paths.

## Definition of Done

- ✓ All `phase-2.3` deliverables implemented and verified.
- ✓ Chat module tests pass for updated service coverage.
- ✓ No unresolved authorization or consistency gaps remain.
- ✓ `IMPLEMENTATION_CHECKLIST.md` and `MASTER_PROJECT_PLAN.md` updated.
- ✓ Handoff contains completion evidence and explicit next step.

## Next Step After Phase 2.3

- Proceed to `phase-2.4` (Chat REST API Endpoints), then `phase-2.5` (SignalR real-time integration).
