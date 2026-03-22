# Desktop Client — Sync Protocol Details

> **Last Updated:** 2026-03-03

---

## Overview

This document describes the internal sync protocol used by `DotNetCloud.Client.Core.Sync.SyncEngine`. For a higher-level overview, see [Desktop Sync Architecture](../../modules/SYNC.md).

---

## Sync Engine Lifecycle

```
StartAsync(context)
  ├── Initialize SQLite state DB
  ├── Start FileSystemWatcher
  ├── Start periodic scan timer
  └── Set state → Idle

SyncAsync(context)                  (triggered by watcher or timer)
  ├── Acquire sync lock (semaphore)
  ├── Set state → Syncing
  ├── Refresh access token
  ├── Apply remote changes
  ├── Apply local changes
  ├── Update sync cursor
  └── Set state → Idle

PauseAsync(context)
  └── Set _paused flag (watcher events are queued)

ResumeAsync(context)
  ├── Clear _paused flag
  └── Trigger immediate SyncAsync

StopAsync()
  ├── Cancel CancellationTokenSource
  ├── Dispose FileSystemWatcher
  └── Wait for periodic scan task
```

---

## Change Detection

### FileSystemWatcher Events

The engine monitors these notification filters:

- `FileName` — file created, deleted, renamed
- `DirectoryName` — directory created, deleted, renamed
- `LastWrite` — file content modified
- `Size` — file size changed

Events trigger an immediate `SyncAsync()` call (debounced by the semaphore lock).

### Periodic Full Scan

A background task runs at `FullScanInterval` (default: 5 minutes) and calls `SyncAsync()`. This catches:

- Events missed by the FileSystemWatcher (e.g., during high-volume changes)
- Changes made while the watcher was temporarily disabled
- External changes to the sync folder (e.g., by other processes)

---

## Remote Change Application

```
1. GET /api/v1/files/sync/changes?since={lastSyncTimestamp}&folderId={root}
2. For each change:
   a. If new/modified remote file:
      - Check selective sync (skip if excluded)
      - Check for local conflict (see Conflict Detection below)
      - Download via ChunkedTransferClient
      - Update LocalFileRecord in state DB
   b. If deleted on remote:
      - Delete local file
      - Remove LocalFileRecord from state DB
3. Update sync cursor to server timestamp
```

---

## Local Change Application

```
1. Scan local folder for changes vs. state DB:
   a. New files (not in state DB) → queue upload
   b. Modified files (hash differs from state DB) → queue upload
   c. Deleted files (in state DB but not on disk) → queue server delete
2. For each pending upload:
   - Chunk the file (4 MB chunks)
   - Hash each chunk (SHA-256)
   - Initiate upload session
   - Upload missing chunks only
   - Complete session
   - Update LocalFileRecord
3. For each pending delete:
   - DELETE /api/v1/files/{nodeId}
   - Remove LocalFileRecord
```

---

## Conflict Detection

A conflict is detected when:

1. The file has been modified locally (hash differs from state DB)
2. AND the file has been modified remotely (server hash differs from state DB hash)

### Resolution

```
1. Rename local file: "report.docx" → "report (conflict - {user} - {date}).docx"
2. Download remote version to original path
3. Update state DB with remote version
4. Create PendingUpload for the conflict copy
5. Raise ConflictDetected event → SyncTray shows notification
```

Both versions are preserved. The user resolves the conflict manually.

---

## Chunked Transfer Protocol

### Upload Flow

```
Client                              Server
  |  1. Split file into 4MB chunks    |
  |  2. SHA-256 hash each chunk       |
  |                                    |
  |  POST /upload/initiate             |
  |  { fileName, parentId, totalSize,  |
  |    mimeType, chunkHashes[] }       |
  |----------------------------------->|
  |                                    | Check quota
  |                                    | Dedup lookup
  |  { sessionId,                      |
  |    existingChunks[],               |
  |    missingChunks[] }               |
  |<-----------------------------------|
  |                                    |
  |  For each missingChunk:            |
  |  PUT /upload/{id}/chunks/{hash}    |
  |  [raw binary data]                 |
  |----------------------------------->|
  |                                    | Verify hash
  |                                    | Write to storage
  |                                    |
  |  POST /upload/{id}/complete        |
  |----------------------------------->|
  |                                    | Create FileVersion
  |                                    | Link chunks
  |  { fileNodeDto }                   |
  |<-----------------------------------|
```

### Delta Download Flow

```
Client                              Server
  |  GET /files/{id}/chunks            |
  |----------------------------------->|
  |  { chunkHashes[] }                 |
  |<-----------------------------------|
  |                                    |
  |  Compare with local chunk hashes   |
  |  Identify differing chunks         |
  |                                    |
  |  For each different chunk:         |
  |  GET /files/chunks/{hash}          |
  |----------------------------------->|
  |  [raw binary data]                 |
  |<-----------------------------------|
  |                                    |
  |  Reassemble file locally           |
```

---

## Local State Database Schema

SQLite database, one per sync context.

### `local_files` Table

| Column | Type | Description |
|---|---|---|
| `relative_path` | TEXT PK | Path relative to sync root |
| `content_hash` | TEXT | SHA-256 of file content |
| `last_modified_utc` | TEXT | ISO 8601 timestamp |
| `sync_state` | TEXT | `Synced`, `PendingUpload`, `PendingDownload`, `Conflict` |
| `server_node_id` | TEXT | Server-side node GUID |

### `pending_operations` Table

| Column | Type | Description |
|---|---|---|
| `id` | INTEGER PK | Auto-increment ID |
| `operation_type` | TEXT | `Upload`, `Download`, `Delete`, `Move` |
| `relative_path` | TEXT | Target path |
| `server_node_id` | TEXT | Server node ID |
| `created_at` | TEXT | When queued |
| `retry_count` | INTEGER | Retry attempts |
| `last_error` | TEXT | Last error message |

### `sync_cursor` Table

| Column | Type | Description |
|---|---|---|
| `key` | TEXT PK | Cursor key (e.g., "last_sync") |
| `value` | TEXT | ISO 8601 timestamp |

---

## Error Recovery

| Error | Recovery Strategy |
|---|---|
| Network timeout | Retry with exponential backoff |
| HTTP 429 (rate limit) | Wait for `Retry-After` header duration |
| HTTP 401 (unauthorized) | Refresh OAuth2 token, retry once |
| HTTP 409 (conflict) | Create conflict copy |
| HTTP 5xx (server error) | Retry with exponential backoff (max 5 attempts) |
| Upload interrupted | Resume: re-request session, upload remaining chunks |
| Hash mismatch | Re-read file, re-hash, re-upload |
| SQLite locked | Retry with short delay |

---

## Concurrency

- The `SyncEngine` uses a `SemaphoreSlim(1, 1)` to serialize sync passes
- FileSystemWatcher events and periodic scans both compete for the semaphore
- While a sync pass is running, incoming events are queued
- Chunk uploads/downloads within a pass can run concurrently (configurable count)
