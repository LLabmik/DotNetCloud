# Files Module — Desktop Sync Architecture & Protocol

> **Last Updated:** 2026-03-03

---

## Overview

DotNetCloud provides a desktop sync client that keeps a local folder in bidirectional sync with the server. The client consists of two components:

| Component | Project | Purpose |
|---|---|---|
| **SyncService** | `DotNetCloud.Client.SyncService` | Background worker service (Windows Service / systemd) |
| **SyncTray** | `DotNetCloud.Client.SyncTray` | Avalonia tray icon app for status and settings |

The shared sync logic lives in `DotNetCloud.Client.Core`.

---

## Architecture

```
┌─────────────────────┐     IPC (Named Pipe / Unix Socket)     ┌──────────────────┐
│   SyncService       │◄──────────────────────────────────────►│   SyncTray       │
│   (BackgroundService)│                                        │   (Avalonia App)  │
│                     │                                        │                  │
│ ┌─────────────────┐ │                                        │ • Tray icon      │
│ │ SyncContextMgr  │ │                                        │ • Settings UI    │
│ │  ├─ Context A   │ │                                        │ • Notifications  │
│ │  │  └─ SyncEngine│ │                                        │ • Quick actions  │
│ │  ├─ Context B   │ │                                        └──────────────────┘
│ │  │  └─ SyncEngine│ │
│ │  └─ ...         │ │
│ └─────────────────┘ │
│                     │
│ ┌─────────────────┐ │     HTTPS / REST API
│ │ API Client      │◄├────────────────────────►  DotNetCloud Server
│ │ Chunked Transfer│ │
│ │ OAuth2 PKCE     │ │
│ └─────────────────┘ │
│                     │
│ ┌─────────────────┐ │
│ │ Local State DB  │ │     SQLite (per context)
│ │ (SQLite)        │ │
│ └─────────────────┘ │
└─────────────────────┘
```

---

## Sync Context

A `SyncContext` represents one sync pairing:

| Property | Description |
|---|---|
| `Id` | Unique context identifier |
| `ServerBaseUrl` | DotNetCloud server URL |
| `UserId` | Authenticated user's server-side ID |
| `LocalFolderPath` | Local directory being synced |
| `StateDatabasePath` | Path to the SQLite state database |
| `AccountKey` | Key for loading tokens from the encrypted token store |
| `FullScanInterval` | Periodic scan interval (default: 5 minutes) |
| `DisplayName` | Friendly name (e.g., "Ben @ cloud.example.com") |

### Multi-Account Support

- One OS user can connect to multiple DotNetCloud servers
- Each connection is a separate `SyncContext` with its own sync folder, state DB, and auth tokens
- The `SyncContextManager` manages lifecycle of all contexts

---

## Sync Protocol

### Change Detection

The sync engine uses two mechanisms:

1. **FileSystemWatcher** — Instant detection of local file system changes (create, modify, delete, rename)
2. **Periodic full scan** — Safety net that runs at the configured interval (default: 5 minutes) to catch any events missed by the watcher

### Sync Pass

A full sync pass consists of:

```
1. Refresh OAuth2 access token (if expired)
2. Apply remote changes
   a. GET /api/v1/files/sync/changes?since={lastSync}
   b. Download new/modified remote files
   c. Delete locally files deleted on server
3. Apply local changes
   a. Scan local state DB for pending operations
   b. Upload new/modified local files
   c. Delete on server files deleted locally
4. Update sync cursor (last sync timestamp)
```

### Server Endpoints Used

| Endpoint | Purpose |
|---|---|
| `GET /api/v1/files/sync/changes?since={ts}` | Incremental changes since last sync |
| `GET /api/v1/files/sync/tree?folderId={id}` | Full folder tree for reconciliation |
| `POST /api/v1/files/sync/reconcile` | Compare client state against server |
| `POST /api/v1/files/upload/initiate` | Start chunked upload |
| `PUT /api/v1/files/upload/{id}/chunks/{hash}` | Upload individual chunks |
| `POST /api/v1/files/upload/{id}/complete` | Complete upload |
| `GET /api/v1/files/{id}/chunks` | Get chunk manifest for delta download |
| `GET /api/v1/files/chunks/{hash}` | Download individual chunks |

---

## Chunked Transfer

### Upload

1. **Chunk:** Split file into 4 MB chunks
2. **Hash:** SHA-256 hash each chunk
3. **Initiate:** Send manifest to server; receive list of missing chunks
4. **Upload:** Send only missing chunks (deduplication)
5. **Complete:** Server assembles file from chunks

### Download (Delta Sync)

1. **Manifest:** Request server chunk manifest for the file
2. **Compare:** Identify chunks that differ from local version
3. **Download:** Fetch only changed chunks
4. **Assemble:** Reconstruct the file locally

### Resume

- Upload sessions persist on the server (24-hour TTL)
- If a transfer is interrupted, the client resumes by re-requesting the session status and uploading only remaining missing chunks

---

## Conflict Resolution

### Detection

A conflict occurs when both local and remote copies have been modified since the last sync.

### Resolution Strategy

DotNetCloud uses a **conflict copy** strategy — both versions are preserved:

1. The local file is renamed: `report (conflict - Ben - 2025-07-14).docx`
2. The remote version is downloaded to the original path
3. The user is notified via SyncTray

**No data is ever silently lost.**

### Notification

When a conflict is detected:

- SyncTray displays a desktop notification
- The notification includes the original path and conflict copy path
- The user resolves the conflict manually by choosing which version to keep

---

## Local State Database

Each sync context maintains a SQLite database tracking:

### LocalFileRecord

| Column | Description |
|---|---|
| `RelativePath` | Path relative to sync root |
| `ContentHash` | SHA-256 hash of file content |
| `LastModifiedUtc` | Last modified timestamp |
| `SyncState` | `Synced`, `PendingUpload`, `PendingDownload`, `Conflict` |
| `ServerNodeId` | Corresponding node ID on the server |

### PendingOperationRecord

| Column | Description |
|---|---|
| `OperationType` | `Upload`, `Download`, `Delete`, `Move` |
| `RelativePath` | Target file path |
| `ServerNodeId` | Server node ID (if applicable) |
| `CreatedAt` | When the operation was queued |
| `RetryCount` | Number of retry attempts |
| `LastError` | Last error message (if any) |

### Sync Cursor

The last successful sync timestamp, used as the `since` parameter for incremental change queries.

---

## Selective Sync

Users can choose which folders to sync:

- **Include:** Only sync selected folders
- **Exclude:** Sync everything except selected folders
- Configuration is stored per sync context via `ISelectiveSyncConfig`
- Excluded folders are skipped during sync passes
- Server-side changes in excluded folders are ignored

---

## Authentication

### OAuth2 PKCE Flow

1. SyncTray launches the system browser to the server's OAuth2 authorization endpoint
2. User authenticates in the browser
3. Server redirects to a localhost callback URL
4. SyncTray captures the authorization code
5. SyncTray exchanges the code for access/refresh tokens

### Token Storage

Tokens are stored in an AES-GCM encrypted file on disk (`EncryptedFileTokenStore`). The encryption key is derived from machine-specific entropy. Windows DPAPI can be layered on top for additional protection.

### Token Refresh

The sync engine automatically refreshes expired access tokens using the refresh token before each sync pass.

---

## IPC Protocol

SyncTray communicates with SyncService via Named Pipe (Windows) or Unix domain socket (Linux).

### Commands

| Command | Description |
|---|---|
| `list-contexts` | List all sync contexts with status |
| `add-account` | Add a new account (OAuth2 tokens from SyncTray) |
| `remove-account` | Remove account (stop sync, delete state) |
| `get-status` | Get sync status for a context |
| `pause` | Pause sync for a context |
| `resume` | Resume sync for a context |
| `sync-now` | Trigger immediate sync pass |

### Events

| Event | Description |
|---|---|
| `sync-progress` | Upload/download progress update |
| `sync-complete` | Sync pass completed |
| `conflict-detected` | Conflict detected and resolved |
| `error` | Sync error occurred |

---

## Error Handling

| Scenario | Behavior |
|---|---|
| Network disconnection | Queue changes locally, retry on reconnect |
| Server 5xx errors | Retry with exponential backoff |
| Server 4xx errors | Log and skip the operation |
| Token expired | Auto-refresh using refresh token |
| Rate limiting (429) | Respect `Retry-After` header |

---

## Platform Support

| Platform | Service Registration | IPC Transport |
|---|---|---|
| Windows | Windows Service (`AddWindowsService()`) | Named Pipe |
| Linux | systemd unit (`AddSystemd()`) | Unix domain socket |
