# Files Module — Versioning Guide

> **Last Updated:** 2026-03-03

---

## Overview

The Files module automatically creates a new version every time a file's content is updated. This provides a complete history of changes, allowing users to view, download, compare, and restore any previous version.

---

## How Versioning Works

### Version Creation

A new `FileVersion` record is created whenever:

1. A chunked upload completes for an existing file
2. A file is saved via WOPI/Collabora (PutFile)
3. A previous version is restored (creates a new version pointing to the old content)

Each version stores:

- **Version number** (1-based, ascending)
- **Content hash** (SHA-256 of the file content)
- **Size** in bytes
- **MIME type** at the time of creation
- **Creator** (user ID)
- **Timestamp** (UTC)
- **Optional label** (e.g., "Final draft", "Before review")

### Chunk Reuse

Versions reference chunks via `FileVersionChunk` records. When a file is partially modified, only the changed chunks are new — unchanged chunks are shared with previous versions. This means:

- Storage is efficient: a 100 MB file with a 1-byte change only stores the affected 4 MB chunk as new data
- Restoring a version is instant: it creates a new version that references the same chunks

---

## Version Operations

### List Versions

```
GET /api/v1/files/{nodeId}/versions?userId={guid}
```

Returns all versions of a file, newest first.

### Download a Specific Version

```
GET /api/v1/files/{nodeId}/download?userId={guid}&version={versionNumber}
```

### Restore a Version

```
POST /api/v1/files/{nodeId}/versions/{versionNumber}/restore?userId={guid}
```

Restore is non-destructive: it creates a **new** version with the same content as the specified version. The version history is preserved.

**Example:**

| Version | Action |
|---|---|
| v1 | Original upload |
| v2 | Edited by user |
| v3 | Edited again |
| v4 | *Restored from v1* (same content as v1, new version number) |

### Label a Version

```
PUT /api/v1/files/{nodeId}/versions/{versionNumber}/label?userId={guid}
```

```json
{
  "label": "Final draft"
}
```

Labeled versions are protected from automatic cleanup (see [Retention](#version-retention)).

### Delete a Version

```
DELETE /api/v1/files/{nodeId}/versions/{versionNumber}?userId={guid}
```

When a version is deleted:

1. `FileVersionChunk` records for the version are removed
2. Each referenced chunk's `ReferenceCount` is decremented
3. Chunks with `ReferenceCount = 0` become candidates for garbage collection

---

## Version Retention

### Configuration

Version retention is configured via `appsettings.json`:

```json
{
  "Files": {
    "VersionRetention": {
      "MaxVersionCount": 50,
      "RetentionDays": 0,
      "CleanupInterval": "24:00:00"
    }
  }
}
```

| Setting | Default | Description |
|---|---|---|
| `MaxVersionCount` | 50 | Maximum versions per file. Oldest unlabeled versions are pruned when exceeded. Set to 0 for unlimited. |
| `RetentionDays` | 0 (disabled) | Days to keep versions. Unlabeled versions older than this are auto-deleted. At least one version always remains. |
| `CleanupInterval` | 24 hours | How often the `VersionCleanupService` runs. |

### Retention Rules

1. **Count-based:** When a file exceeds `MaxVersionCount` versions, the oldest unlabeled versions are deleted
2. **Time-based:** Versions older than `RetentionDays` are deleted (if enabled)
3. **Labeled versions are never auto-deleted:** Add a label to protect important versions
4. **At least one version always remains:** The current version is never auto-deleted

### Background Cleanup

The `VersionCleanupService` runs at the configured interval and:

1. Scans files that exceed the version count limit
2. Identifies unlabeled versions older than the retention period
3. Deletes excess versions (oldest first)
4. Decrements chunk reference counts
5. Unreferenced chunks are garbage-collected by other background services

---

## Data Model

### FileVersion Entity

| Property | Type | Description |
|---|---|---|
| `Id` | `Guid` | Primary key |
| `FileNodeId` | `Guid` | FK to FileNode |
| `VersionNumber` | `int` | 1-based ascending |
| `Size` | `long` | Size in bytes |
| `ContentHash` | `string` | SHA-256 hash |
| `StoragePath` | `string` | Content-addressable path |
| `MimeType` | `string?` | MIME type |
| `CreatedByUserId` | `Guid` | Creator |
| `CreatedAt` | `DateTime` | Timestamp (UTC) |
| `Label` | `string?` | Optional label |

### FileVersionChunk Entity

| Property | Type | Description |
|---|---|---|
| `FileVersionId` | `Guid` | FK to FileVersion |
| `FileChunkId` | `Guid` | FK to FileChunk |
| `SequenceIndex` | `int` | Chunk order within the file |

---

## Blazor UI

The version history panel (`VersionHistoryPanel.razor`) displays:

- List of versions with date, author, size, and label
- Download button per version
- Restore button per version
- Label editing (inline)
- Delete button for old versions

---

## API Reference

See the complete endpoint documentation in [API.md](API.md#version-endpoints).
