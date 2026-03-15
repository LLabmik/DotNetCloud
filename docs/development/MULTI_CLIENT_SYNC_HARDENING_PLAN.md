# Multi-Client Sync Hardening Plan

**Created:** 2026-03-14
**Context:** Running Linux + Windows sync clients simultaneously for the same user caused duplicate file entries in the database. Root cause analysis identified multiple server-side race conditions that are unsafe under concurrent multi-client access for a single user account.

**Goal:** Make the sync system safe and performant for a single user running 20+ simultaneous sync clients across different machines.

---

## Priority Tiers

### P0 — Critical (Must Fix Before Multi-Client Is Safe)

These are active race conditions that cause data corruption (duplicate files, lost sequence numbers, corrupted reference counts) when two or more clients sync concurrently for the same user.

### P1 — Important (Required for Good Multi-Client Experience)

These are design gaps that cause unnecessary work (echo re-downloads, shared rate limit starvation) but don't corrupt data.

### P2 — Enhancement (Needed for 20+ Client Scale)

Efficiency and architectural improvements that reduce server load and improve responsiveness at high client counts.

---

## P0 — Critical Fixes

### P0.1 — Atomic SyncSequence Assignment

**File:** `src/Modules/Files/DotNetCloud.Modules.Files.Data/SyncCursorHelper.cs`

**Problem:** `AssignNextSequenceAsync` does a read-modify-write at the EF application level. Two concurrent requests for the same user can read the same `CurrentSequence` value, both increment to the same number, and both files get the same `SyncSequence`. This breaks cursor-based sync — clients can miss changes entirely or re-download files they already have.

**Root Cause of Duplicate Files:** Two clients upload simultaneously → both get `SyncSequence = N` → other clients' cursor logic can't correctly distinguish them → re-materialization creates duplicates.

**Fix:**
- Replace the EF read-modify-write with a single raw SQL statement:
  ```sql
  UPDATE files.user_sync_counters
  SET current_sequence = current_sequence + 1, updated_at = NOW()
  WHERE user_id = @userId
  RETURNING current_sequence;
  ```
- If no row exists (first mutation for user), use `INSERT ... ON CONFLICT DO UPDATE ... RETURNING`.
- Assign the returned value to `node.SyncSequence`.
- This guarantees atomicity — PostgreSQL row-level locking ensures sequential increments even under concurrent access.

**Scope:**
- ✓ Modify `SyncCursorHelper.AssignNextSequenceAsync` to use raw SQL with `RETURNING`
- ✓ Handle the insert-or-update (upsert) case atomically
- ✓ Pass `FilesDbContext` connection/transaction so it participates in the same DB transaction
- ✓ Update unit tests to verify sequential assignment under simulated concurrency
- ✓ Add integration test: two concurrent `SaveChangesAsync` calls produce distinct sequences

**Validation:** After fix, run two simultaneous upload completions for the same user and verify `SyncSequence` values are strictly monotonic with no gaps or duplicates.

---

### P0.2 — Unique Constraint on File Names Per Parent Folder

**File:** `src/Modules/Files/DotNetCloud.Modules.Files.Data/Configuration/FileNodeConfiguration.cs`

**Problem:** The index on `(ParentId, Name)` is NOT declared as unique. The name-existence check in `CompleteUploadAsync` and `CreateFolderAsync` is check-then-act with a gap between the query and the insert. Two concurrent uploads of the same filename to the same folder both pass the check and both create a row.

**Fix:**
- Add a unique filtered index on `(ParentId, Name)` excluding soft-deleted rows:
  ```csharp
  builder.HasIndex(n => new { n.ParentId, n.Name })
      .IsUnique()
      .HasFilter("\"IsDeleted\" = false")
      .HasDatabaseName("uq_file_nodes_parent_name_active");
  ```
- In `CompleteUploadAsync` and `CreateFolderAsync`, wrap the insert in a try/catch for `DbUpdateException` with a unique-violation inner exception. On catch, either:
  - Return the existing node (idempotent upload), or
  - Throw a user-friendly `ValidationException`
- Generate an EF migration for the new constraint.
- Handle the `ParentId IS NULL` case (root-level files) — PostgreSQL unique indexes treat NULLs as distinct by default, so root-level uniqueness needs a separate partial index or a sentinel root folder approach.

**Scope:**
- ✓ Add unique filtered index to `FileNodeConfiguration`
- ✓ Add handling for `ParentId IS NULL` root-level uniqueness (coalesce or sentinel)
- ✓ Generate EF migration
- ✓ Update `ChunkedUploadService.CompleteUploadAsync` — catch unique violation, return existing node
- ✓ Update `FileService.CreateFolderAsync` — catch unique violation
- ✓ Remove or keep the application-level pre-check as a fast-path (not a correctness guarantee)
- ✓ Add tests: concurrent create with same name produces exactly one row
- ☐ Apply migration to dev/staging DB and verify

**Validation:** Two simultaneous `CompleteUploadAsync` calls for `report.pdf` in the same folder → exactly one `FileNode` row, no exception for the second caller (or a clean 409).

---

### P0.3 — Atomic Chunk Reference Counting

**File:** `src/Modules/Files/DotNetCloud.Modules.Files.Data/Services/ChunkedUploadService.cs`

**Problem:** `chunk.ReferenceCount++` is an EF in-memory increment. Two concurrent uploads sharing the same chunk (common with CDC dedup) both read `RefCount=3`, both write `4` instead of one writing `4` and the other `5`. If refcount drifts downward, garbage collection could delete live chunks that other files still reference — data loss.

**Fix:**
- Replace EF in-memory increment with raw SQL per chunk:
  ```sql
  UPDATE files.file_chunks
  SET reference_count = reference_count + 1, last_referenced_at = NOW()
  WHERE id = @chunkId;
  ```
- Batch all chunk updates into a single statement or use `ExecuteSqlRawAsync` in a loop within the same transaction.
- For the decrement path (file deletion / version cleanup), apply the same atomic pattern.
- Consider adding a `CHECK (reference_count >= 0)` constraint to prevent negative refcounts.

**Scope:**
- ✓ Replace `chunk.ReferenceCount++` in `CompleteUploadAsync` with raw SQL atomic increment
- ✓ Find and fix all decrement paths (file deletion, version pruning, session cleanup)
- ✓ Add `CHECK (reference_count >= 0)` constraint via migration
- ✓ Add tests: concurrent uploads sharing a chunk produce correct final refcount
- ✓ Audit: search entire codebase for any other `ReferenceCount` mutations

**Validation:** Two uploads with overlapping chunks → `ReferenceCount` equals exactly the number of unique references. Delete one → refcount decrements correctly.

---

## P1 — Important Improvements

### P1.1 — Device Identity Registration

**Problem:** The server has no concept of which device performed an operation. All file mutations are attributed only to a user. This makes echo suppression, audit trails, and per-device conflict resolution impossible.

**Design:**
- Client generates a stable `DeviceId` (GUID) on first run, persisted in the sync service's local config.
- Client sends `X-Device-Id` header on every API request.
- Server maintains a `SyncDevice` table:
  ```
  SyncDevice
  ├─ Id (PK, Guid)
  ├─ UserId (FK → Users)
  ├─ DeviceName (e.g., "benk-desktop-linux", from hostname)
  ├─ Platform (e.g., "Linux", "Windows", "Android")
  ├─ ClientVersion (e.g., "0.1.0-alpha")
  ├─ FirstSeenAt
  ├─ LastSeenAt
  └─ IsActive (soft disable)
  ```
- `FileNode` gets an `OriginatingDeviceId` nullable FK column — set on creation/update.
- `ChunkedUploadSession` gets a `DeviceId` column.
- Device registration is auto-upsert (first request with unknown `DeviceId` creates the row).

**Scope:**
- ✓ Define `SyncDevice` model and EF configuration
- ✓ Add `OriginatingDeviceId` to `FileNode`
- ✓ Add `DeviceId` to `ChunkedUploadSession`
- ✓ Create middleware or action filter to extract `X-Device-Id` and resolve/create `SyncDevice`
- ✓ Generate EF migration
- ✓ Client: generate stable `DeviceId` on first run (store in sync service config)
- ✓ Client: send `X-Device-Id` header via `DelegatingHandler`
- ✓ Add admin API endpoint to list/manage devices per user
- ✓ Tests for device auto-registration and attribution

---

### P1.2 — Echo Suppression

**Problem:** When client A uploads a file, the `sync/changes` feed includes that file as a new change. Client A's next sync pass sees it and re-downloads its own upload. With 20 clients, every upload triggers 19 re-downloads (correct) plus 1 unnecessary re-download to the originator.

**Design (requires P1.1):**
- `SyncChangeDto` includes `OriginatingDeviceId`.
- Client stores `(SyncSequence, DeviceId)` for every upload it performs.
- During `ApplyRemoteChangesAsync`, client skips changes where:
  - `OriginatingDeviceId == myDeviceId`, AND
  - local file already exists with matching `ContentHash`
- This is a client-side optimization — server just provides the data.

**Alternative (simpler):** On `CompleteUploadAsync` response, server returns the assigned `SyncSequence`. Client records it. On next `sync/changes`, client recognizes "I already have this sequence" and skips.

**Scope:**
- ✓ Add `OriginatingDeviceId` to `SyncChangeDto` response
- ✓ Client: track uploaded sequences locally
- ✓ Client: skip self-originated changes in `ApplyRemoteChangesAsync`
- ✓ Tests: upload from device A → device A's sync pass doesn't re-download

---

### P1.3 — Per-Device Rate Limiting

**Problem:** Rate limit buckets are keyed by user ID only. With 20 clients, all 20 share one `30 req/min` bucket for `upload/initiate`. Clients permanently starve each other — some get 429 on every request while others consume the entire budget.

**Design:**
- Change rate limit partition key from `userId` to `{userId}:{deviceId}` (requires P1.1).
- Adjust limits: per-device limits can be tighter (e.g., 15/min per device) while the aggregate per-user ceiling is higher (e.g., 200/min total).
- Alternatively, keep per-user limits but increase them proportionally, and use `X-Device-Id` only for diagnostics.

**Scope:**
- ✓ Update rate limiting configuration to include device ID in partition key
- ✓ Review and adjust rate limit values for multi-client scenarios
- ✓ Add `X-RateLimit-Remaining` response header so clients can self-throttle
- ✓ Tests: two devices don't starve each other

---

## P2 — Scale Enhancements (20+ Clients)

### P2.1 — Push Change Notifications (WebSocket/SSE)

**Problem:** With 20 clients polling `sync/changes` every 30 seconds, the server handles ~40 change-check requests per minute per user — most returning empty results. This is pure waste at scale.

**Design:**
- Server pushes a lightweight notification when a user's file tree changes:
  ```json
  { "type": "sync-changed", "userId": "...", "latestSequence": 157 }
  ```
- Transport: Server-Sent Events (SSE) on `/api/v1/files/sync/stream` — simpler than WebSocket, works through proxies, auto-reconnects.
- Client keeps an SSE connection open. On receiving a notification, triggers a `sync/changes` poll.
- Fallback: clients still poll at a longer interval (e.g., 5 minutes) as a safety net if SSE disconnects.

**Scope:**
- ✓ Implement SSE endpoint for sync change notifications
- ✓ Server: publish notification on any `SyncSequence` increment for a user
- ✓ Client: add SSE listener, trigger sync on notification
- ✓ Client: increase poll interval to 5 minutes when SSE is connected
- ✓ Handle SSE reconnection and missed-notification recovery
- ✓ Tests and load benchmarking

---

### P2.2 — Server-Side Per-Device Cursor Tracking

**Problem:** Each client stores its cursor locally. The server has no idea what each device has seen. This means:
- If a client reinstalls, it must do a full re-sync (no server-side cursor recovery).
- The server can't compute minimal deltas per device.
- No admin visibility into which devices are behind.

**Design:**
- `SyncDeviceCursor` table:
  ```
  SyncDeviceCursor
  ├─ DeviceId (PK, FK → SyncDevice)
  ├─ UserId (FK → Users)
  ├─ LastAcknowledgedSequence (long)
  ├─ UpdatedAt
  ```
- Client sends `X-Sync-Cursor-Ack: {sequence}` header after successfully processing changes.
- Server updates `SyncDeviceCursor` on each ack.
- New client or reinstalled client can query `/api/v1/files/sync/device-cursor` to recover last-known position.
- Admin dashboard shows per-device sync lag.

**Scope:**
- ✓ Define `SyncDeviceCursor` model and EF configuration
- ✓ Server: update cursor on ack header
- ✓ Server: expose cursor recovery endpoint
- ✓ Client: send ack header after processing changes
- ✓ Client: query cursor on first connect (skip full re-sync if cursor exists)
- ✓ Admin: surface per-device sync status
- ✓ Migration

---

## P3 — Resume Handoff Work

### P3.1 — Linux Sync Client Final Runtime Verification

**Context:** The active handoff in `docs/development/CLIENT_SERVER_MEDIATION_HANDOFF.md` has a pending runtime verification task for `mint-dnc-client`. The Linux sync client E2E test was blocked by server-side API behavior under concurrent load — specifically mixed `upload/initiate` 429 throttling and `GET /api/v1/files/{id}/chunks` 404 responses for nodes still present in the change/tree feeds.

**Dependency:** P0 fixes (especially P0.1 and P0.2) will resolve the root causes of the API inconsistencies observed during the Linux verification run. The 404 chunk responses were likely caused by duplicate/ghost `FileNode` rows from the name-uniqueness race condition.

**Scope:**
- ☐ After P0 fixes are deployed to `mint22`, resume the handoff task
- ☐ Re-run Linux E2E sync verification on `mint-dnc-client` with both Linux + Windows clients active simultaneously
- ☐ Verify no duplicate files appear, no 404 on chunk downloads for valid nodes, no upload 429 storms
- ☐ Capture timestamped runtime evidence per handoff exit criteria
- ☐ Update handoff document with results and close out the active task

---

## Implementation Order

```
Phase 1: P0 fixes (data correctness)
  P0.1 → P0.2 → P0.3
  ↓ Deploy & validate with dual-client test

Phase 2: P1 improvements (multi-client UX)
  P1.1 → P1.2 → P1.3
  ↓ Deploy & validate with 3+ clients

Phase 3: P2 enhancements (scale)
  P2.1 → P2.2
  ↓ Load test with simulated 20 clients

Phase 4: Resume handoff
  P3.1 (after P0 deployed)
```

**P0 items are independent of each other** and can be implemented in any order, but P0.1 (sequence atomicity) is the highest-impact fix for the duplicate file bug.

**P1 items are sequential** — P1.2 and P1.3 depend on P1.1 (device identity).

**P2 items are independent** of each other but both benefit from P1.1.

---

## Testing Strategy

| Fix | Unit Test | Integration Test | Manual Validation |
|-----|-----------|------------------|-------------------|
| P0.1 | Mock DB, verify SQL call | Two concurrent SaveChanges → distinct sequences | Dual-client upload, check DB sequences |
| P0.2 | Verify unique constraint config | Two concurrent creates → one succeeds, one handled | Dual-client same-name upload |
| P0.3 | Verify raw SQL increment | Two concurrent uploads sharing chunk → correct refcount | After delete, verify chunk not orphaned |
| P1.1 | Device model validation | Auto-registration on first request | Check `SyncDevice` table after client connect |
| P1.2 | Skip logic in mock | Upload + immediate sync → no re-download | Watch client logs for echo |
| P1.3 | Rate limit partition key | Two devices hit limits independently | 429 only affects one device |
| P2.1 | SSE endpoint test | Notification received after upload | Client reacts to push, not poll |
| P2.2 | Cursor model validation | Ack updates cursor | Reinstall client, verify cursor recovery |

---

## Risk Assessment

| Risk | Mitigation |
|------|-----------|
| P0.2 migration on existing data with duplicates | Clean duplicates BEFORE applying unique constraint; migration must handle existing violations |
| Raw SQL bypasses EF change tracking | Reload entities after raw SQL if needed in same transaction; document clearly |
| Device ID spoofing | Device ID is convenience, not security — all auth is still via OAuth bearer token |
| SSE connection limits at scale | Limit SSE connections per user (e.g., 25 max); older connections auto-close |
| PostgreSQL-specific SQL (`RETURNING`) | Abstract behind provider interface if MariaDB/SQL Server support is needed |
