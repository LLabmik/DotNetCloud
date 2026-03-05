# Files Module — Architecture

> **Last Updated:** 2026-03-03

---

## Table of Contents

1. [Data Model](#data-model)
2. [File Tree Structure](#file-tree-structure)
3. [Chunking Strategy](#chunking-strategy)
4. [Content-Hash Deduplication](#content-hash-deduplication)
5. [Storage Engine](#storage-engine)
6. [Upload Pipeline](#upload-pipeline)
7. [Download Pipeline](#download-pipeline)
8. [Background Services](#background-services)
9. [Permission Model](#permission-model)
10. [Database Schema](#database-schema)

---

## Data Model

### Entity Relationship Overview

```
FileNode (file or folder)
  ├── FileVersion[]           (version history)
  │     └── FileVersionChunk[]  (ordered chunk references)
  │           └── FileChunk     (deduplicated chunk storage)
  ├── FileShare[]             (user/team/group/public link shares)
  ├── FileTag[]               (colored labels)
  ├── FileComment[]           (threaded discussions)
  └── FileNode[] (children)   (self-referencing tree)

ChunkedUploadSession          (tracks in-progress uploads)
FileQuota                     (per-user storage limits)
```

### Core Entities

#### FileNode

The unified tree node representing both files and folders. Distinguished by `NodeType` (File or Folder).

| Property | Type | Description |
|---|---|---|
| `Id` | `Guid` | Primary key |
| `Name` | `string` | Display name |
| `NodeType` | `FileNodeType` | `File` or `Folder` |
| `MimeType` | `string?` | MIME type (null for folders) |
| `Size` | `long` | Size in bytes (0 for folders) |
| `ParentId` | `Guid?` | FK to parent folder (null = root) |
| `OwnerId` | `Guid` | Owner user ID |
| `MaterializedPath` | `string` | `/root-id/parent-id/this-id` for tree queries |
| `Depth` | `int` | Tree depth (0 = root level) |
| `ContentHash` | `string?` | SHA-256 of current content |
| `CurrentVersion` | `int` | Latest version number |
| `StoragePath` | `string?` | Content-addressable storage path |
| `IsDeleted` | `bool` | Soft-delete flag (trash) |
| `DeletedAt` | `DateTime?` | When trashed |
| `DeletedByUserId` | `Guid?` | Who trashed it |
| `OriginalParentId` | `Guid?` | Restore target |
| `IsFavorite` | `bool` | Favorite flag |
| `CreatedAt` | `DateTime` | Creation timestamp (UTC) |
| `UpdatedAt` | `DateTime` | Last modified timestamp (UTC) |

#### FileVersion

Every content update creates a new version. Versions reference their content through `FileVersionChunk` records.

| Property | Type | Description |
|---|---|---|
| `Id` | `Guid` | Primary key |
| `FileNodeId` | `Guid` | FK to FileNode |
| `VersionNumber` | `int` | 1-based ascending version number |
| `Size` | `long` | Size in bytes |
| `ContentHash` | `string` | SHA-256 hash |
| `StoragePath` | `string` | Content-addressable path |
| `MimeType` | `string?` | MIME type at creation |
| `CreatedByUserId` | `Guid` | Creator user ID |
| `CreatedAt` | `DateTime` | Version creation time |
| `Label` | `string?` | Optional label (e.g., "Final draft") |

#### FileChunk

Content-addressed chunk storage. Identical chunks are stored once across all users and files.

| Property | Type | Description |
|---|---|---|
| `Id` | `Guid` | Primary key |
| `ChunkHash` | `string` | SHA-256 hash (unique deduplication key) |
| `Size` | `int` | Chunk size in bytes (max 4 MB) |
| `StoragePath` | `string` | Disk storage path |
| `ReferenceCount` | `int` | Number of file versions referencing this chunk |
| `CreatedAt` | `DateTime` | First stored |
| `LastReferencedAt` | `DateTime` | Last reference count update |

#### FileVersionChunk

Junction table linking versions to their constituent chunks in order.

| Property | Type | Description |
|---|---|---|
| `FileVersionId` | `Guid` | FK to FileVersion |
| `FileChunkId` | `Guid` | FK to FileChunk |
| `SequenceIndex` | `int` | Chunk order within the file |

---

## File Tree Structure

Files and folders are stored in a single `FileNode` table with a self-referencing parent relationship. The tree supports:

### Materialized Path

Each node stores its full ancestor path: `/root-id/parent-id/this-id`. This enables:

- **Fast descendant queries:** `WHERE MaterializedPath LIKE '/parent-id/%'`
- **Efficient depth calculation:** Count path segments
- **Path resolution:** Single query from any node to root

### Move Operations

When a node is moved to a new parent:

1. Update `ParentId` to the new parent
2. Recalculate `MaterializedPath` for the node and all descendants
3. Update `Depth` for the node and all descendants

### Soft-Delete Cascade

When a folder is trashed:

1. Set `IsDeleted`, `DeletedAt`, `DeletedByUserId` on the folder
2. Cascade soft-delete to all children (recursive)
3. Store `OriginalParentId` for restore
4. Remove active shares on trashed items

---

## Chunking Strategy

Files are split into fixed-size chunks for efficient transfer and storage:

### Chunk Parameters

| Parameter | Value |
|---|---|
| **Chunk size** | 4 MB (4,194,304 bytes) |
| **Hash algorithm** | SHA-256 |
| **Last chunk** | May be smaller than 4 MB |

### Chunking Process

1. **Split:** File is divided into sequential 4 MB chunks
2. **Hash:** Each chunk is hashed with SHA-256
3. **Manifest:** Ordered list of chunk hashes represents the file
4. **Dedup check:** Server reports which chunks it already has
5. **Upload:** Client uploads only missing chunks
6. **Assemble:** Server links chunks to a file version via `FileVersionChunk` records

### Content Integrity

- Chunk hash is verified on receipt: the server computes SHA-256 of the received data and compares it to the expected hash
- Manifest hash (SHA-256 of the concatenated chunk hashes) represents the entire file content
- The file's `ContentHash` is the manifest hash, enabling fast change detection

---

## Content-Hash Deduplication

### How Deduplication Works

Chunks are stored in a shared pool keyed by their SHA-256 hash. When a user uploads a file:

1. Client sends the chunk hash manifest with `InitiateUpload`
2. Server checks each hash against the `FileChunks` table
3. Existing chunks are reported as `existingChunks` (skip upload)
4. Only `missingChunks` need to be uploaded
5. On upload completion, `FileVersionChunk` records link the version to its chunks
6. `ReferenceCount` on each chunk is incremented

### Cross-User Deduplication

Deduplication works across all users. If User A and User B upload the same file, the chunks are stored once. Both file versions reference the same chunk records.

### Garbage Collection

When a file version is deleted:

1. Each chunk's `ReferenceCount` is decremented
2. Chunks with `ReferenceCount = 0` are candidates for garbage collection
3. The `TrashCleanupService` and `UploadSessionCleanupService` periodically delete unreferenced chunks from both the database and disk storage

### Storage Savings

The `IStorageMetricsService` tracks deduplication savings and exposes them via the `GET /api/v1/files/storage/metrics` endpoint.

---

## Storage Engine

### IFileStorageEngine Interface

The storage engine abstraction supports pluggable backends:

```csharp
public interface IFileStorageEngine
{
    Task WriteChunkAsync(string storagePath, ReadOnlyMemory<byte> data, CancellationToken ct);
    Task<byte[]?> ReadChunkAsync(string storagePath, CancellationToken ct);
    Task<Stream?> OpenReadStreamAsync(string storagePath, CancellationToken ct);
    Task<bool> ExistsAsync(string storagePath, CancellationToken ct);
    Task DeleteAsync(string storagePath, CancellationToken ct);
    Task<long> GetTotalSizeAsync(CancellationToken ct);
}
```

### LocalFileStorageEngine

The default implementation stores chunks on the local file system using a content-addressable directory structure based on the first characters of the chunk hash:

```
{storageRoot}/
  ab/
    abcdef1234...  (chunk file)
  cd/
    cdef5678...    (chunk file)
```

### Thumbnail Storage

Thumbnails are cached under `{storageRoot}/.thumbnails/{prefix}/{id}_{size}.jpg` using ImageSharp for image processing.

---

## Upload Pipeline

### Sequence Diagram

```
Client                          Server
  |                               |
  |  POST /upload/initiate        |
  |  { fileName, chunkHashes }    |
  |------------------------------>|
  |                               |  Check quota
  |                               |  Check existing chunks
  |  { existingChunks,            |
  |    missingChunks, sessionId } |
  |<------------------------------|
  |                               |
  |  PUT /upload/{id}/chunks/{h}  |  (for each missing chunk)
  |  [binary data]                |
  |------------------------------>|
  |                               |  Verify hash
  |                               |  Write to storage
  |  { uploaded: true }           |
  |<------------------------------|
  |                               |
  |  POST /upload/{id}/complete   |
  |------------------------------>|
  |                               |  Create FileVersion
  |                               |  Link FileVersionChunks
  |                               |  Update FileNode
  |                               |  Publish FileUploadedEvent
  |  { fileNodeDto }              |
  |<------------------------------|
```

### Upload Session Lifecycle

| Status | Description |
|---|---|
| `InProgress` | Session active, accepting chunks |
| `Completed` | All chunks received, file assembled |
| `Failed` | Error during assembly |
| `Expired` | Session timed out (default: 24 hours) |

Stale sessions are cleaned up by `UploadSessionCleanupService` (runs every hour).

---

## Download Pipeline

### Standard Download

Files are served as seekable streams reconstructed from chunks. The `ConcatenatedStream` class reads chunks in sequence order, supporting HTTP range requests for partial downloads.

### Chunk-Level Delta Download (Sync Clients)

1. Client requests chunk manifest: `GET /api/v1/files/{nodeId}/chunks`
2. Client compares local chunk hashes with server manifest
3. Client downloads only changed chunks: `GET /api/v1/files/chunks/{chunkHash}`
4. Client reassembles the file locally

---

## Background Services

| Service | Interval | Purpose |
|---|---|---|
| `UploadSessionCleanupService` | 1 hour | Expire stale upload sessions, GC orphaned chunks |
| `TrashCleanupService` | 6 hours | Permanently delete items past retention period, GC chunks |
| `QuotaRecalculationService` | 24 hours | Recalculate per-user storage usage |
| `VersionCleanupService` | 24 hours | Prune old unlabeled versions exceeding retention limits |
| `CollaboraProcessManager` | Continuous | Supervise built-in Collabora CODE process |

---

## Permission Model

### Ownership

- File/folder owner has full access to all operations
- Owner can share with others and configure permissions

### Share Permissions

| Level | Capabilities |
|---|---|
| `Read` | View, download |
| `ReadWrite` | View, download, upload, rename, move within shared folder |
| `Full` | All operations including re-share and delete |

### Permission Cascade

Folder share permissions cascade to all children. A share on `/Documents/` grants access to all files and subfolders within.

### Permission Enforcement

The `IPermissionService` validates every file operation against:

1. Ownership (caller is the file owner)
2. Share permissions (caller has a share with sufficient permission)
3. Admin override (system callers bypass permission checks)

---

## Database Schema

### Table Naming

| Provider | Strategy | Example |
|---|---|---|
| PostgreSQL | Schema-based | `files.file_nodes` |
| SQL Server | Schema-based | `files.file_nodes` |
| MariaDB | Prefix-based | `files_file_nodes` |

### Indexes

| Table | Index | Purpose |
|---|---|---|
| `FileNode` | `ParentId` | Fast child listing |
| `FileNode` | `OwnerId` | Fast user file listing |
| `FileNode` | `MaterializedPath` | Fast descendant queries |
| `FileVersion` | `(FileNodeId, VersionNumber)` | Fast version lookup |
| `FileChunk` | `ChunkHash` (unique) | Deduplication lookup |
| `FileShare` | `SharedWithUserId` | Fast "shared with me" queries |
| `FileShare` | `LinkToken` (unique) | Public link resolution |
| `FileShare` | `ExpiresAt` | Expired share cleanup |
| `FileTag` | `(FileNodeId, Name, CreatedByUserId)` (unique) | Prevent duplicate tags |
| `FileComment` | `FileNodeId` | Fast comment listing |
| `ChunkedUploadSession` | `UserId`, `Status`, `ExpiresAt` | Session management |

### Soft-Delete Query Filters

EF Core global query filters automatically exclude soft-deleted records from all queries. Use `IgnoreQueryFilters()` when querying the trash bin.
