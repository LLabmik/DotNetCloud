# Files Module — Admin Configuration Guide

> **Last Updated:** 2026-03-03

---

## Overview

This guide covers all configuration options for the Files module. Configuration is managed via `appsettings.json` or environment variables, and some settings can be adjusted at runtime through the admin dashboard.

---

## Storage Configuration

### Storage Root Directory

The `IFileStorageEngine` stores file chunks on the local file system. Configure the root directory:

```json
{
  "Files": {
    "StorageRoot": "/var/lib/dotnetcloud/files"
  }
}
```

| Platform | Default |
|---|---|
| Linux | `/var/lib/dotnetcloud/files` |
| Windows | `%ProgramData%\DotNetCloud\files` |

### Directory Structure

The storage engine uses a content-addressable layout based on chunk hashes:

```
{StorageRoot}/
├── ab/
│   └── abcdef1234...    (chunk file)
├── cd/
│   └── cdef5678...      (chunk file)
└── .thumbnails/
    └── ab/
        └── {id}_256.jpg (cached thumbnail)
```

### Storage Permissions

| Platform | User/Group | Permissions |
|---|---|---|
| Linux | `dotnetcloud:dotnetcloud` | `750` (rwxr-x---) |
| Windows | `NETWORK SERVICE` or app pool identity | Full Control |

Ensure the DotNetCloud process has read/write access to the storage root.

---

## Quota Configuration

### Default Settings

```json
{
  "Files": {
    "Quota": {
      "DefaultQuotaBytes": 10737418240,
      "ExcludeTrashedFromQuota": false,
      "WarnAtPercent": 80.0,
      "CriticalAtPercent": 95.0,
      "RecalculationInterval": "24:00:00"
    }
  }
}
```

### Configuration Reference

| Setting | Default | Description |
|---|---|---|
| `DefaultQuotaBytes` | `10737418240` (10 GB) | Default storage limit for new users. Set to `0` for unlimited. |
| `ExcludeTrashedFromQuota` | `false` | When `true`, files in trash do not count against quota. |
| `WarnAtPercent` | `80.0` | Publish `QuotaWarningEvent` at this usage percentage. |
| `CriticalAtPercent` | `95.0` | Publish `QuotaCriticalEvent` at this usage percentage. |
| `RecalculationInterval` | `24:00:00` | How often the background service recalculates all user quotas. |

### Per-User Quota Management

Admins can override the default quota for individual users:

```
PUT /api/v1/files/quota/{userId}
```

```json
{
  "maxBytes": 53687091200
}
```

Set `maxBytes` to `0` for unlimited storage.

### Force Quota Recalculation

If quota values become inconsistent (e.g., after a database restore):

```
POST /api/v1/files/quota/{userId}/recalculate
```

Or wait for the next automatic recalculation cycle.

---

## Trash Retention Configuration

### Default Settings

```json
{
  "Files": {
    "TrashRetention": {
      "RetentionDays": 30,
      "CleanupInterval": "06:00:00"
    }
  }
}
```

### Configuration Reference

| Setting | Default | Description |
|---|---|---|
| `RetentionDays` | `30` | Days to keep items in trash before permanent deletion. Set to `0` to disable auto-cleanup. |
| `CleanupInterval` | `06:00:00` (6 hours) | How often the `TrashCleanupService` runs. |

### How Auto-Cleanup Works

1. The `TrashCleanupService` runs at the configured interval
2. It finds all trashed items where `DeletedAt + RetentionDays < now`
3. Each expired item is permanently deleted:
   - File versions and chunk mappings are removed
   - Chunk reference counts are decremented
   - Chunks with zero references are deleted from disk
   - Tags, comments, and shares are cascade-deleted
4. User quotas are updated to reflect freed space

---

## Version Retention Configuration

### Default Settings

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

### Configuration Reference

| Setting | Default | Description |
|---|---|---|
| `MaxVersionCount` | `50` | Maximum versions per file. Set to `0` for unlimited. |
| `RetentionDays` | `0` (disabled) | Delete unlabeled versions older than this. Set to `0` to disable. |
| `CleanupInterval` | `24:00:00` | How often the `VersionCleanupService` runs. |

### Labeled Version Protection

Versions with a label (e.g., "Final draft") are never auto-deleted. Users can label important versions via:

```
PUT /api/v1/files/{nodeId}/versions/{versionNumber}/label
```

### Storage Impact

Each version references chunks. Due to deduplication, a small edit to a large file only stores the changed 4 MB chunk as new data. Version cleanup frees disk space only when chunks become unreferenced (reference count reaches zero).

---

## Upload Limits

### Maximum File Size

The maximum upload file size is configurable via the `FileUpload` section:

```json
{
  "FileUpload": {
    "MaxFileSizeBytes": 16106127360
  }
}
```

| Setting | Default | Description |
|---|---|---|
| `MaxFileSizeBytes` | `16106127360` (15 GB) | Maximum file size allowed for upload. The web UI validates this client-side before uploading, showing a user-friendly error with the formatted size limit. The server also enforces this limit. |

The client retrieves this limit from `GET /api/v1/files/config` on page load and rejects oversized files immediately with a clear error message.

You can also configure limits at the reverse proxy level:

**nginx:**

```nginx
client_max_body_size 0;  # Unlimited (chunks are 4 MB each)
```

**Apache:**

```apache
LimitRequestBody 0
```

### Upload Session Expiration

Upload sessions expire after 24 hours by default. Stale sessions are cleaned up by the `UploadSessionCleanupService` (runs hourly).

### Chunk Size

The chunk size is fixed at 4 MB (4,194,304 bytes). This is not configurable — changing it would break deduplication across existing files.

---

## Background Services

The Files module runs several background services. All are started automatically when the module loads.

| Service | Default Interval | Purpose |
|---|---|---|
| `UploadSessionCleanupService` | 1 hour | Expire stale upload sessions, GC orphaned chunks |
| `TrashCleanupService` | 6 hours | Permanently delete expired trash items |
| `QuotaRecalculationService` | 24 hours | Recalculate per-user storage usage |
| `VersionCleanupService` | 24 hours | Prune old unlabeled versions exceeding retention limits |

### Monitoring Background Services

Background service activity is logged via Serilog. Look for log entries with the service class name:

```
[INF] TrashCleanupService: Permanently deleted 42 expired items, freed 1.2 GB
[INF] QuotaRecalculationService: Recalculated quota for 150 users in 3.2s
```

---

## Database Configuration

### Connection String

The Files module uses its own `FilesDbContext` that connects to the same database as the core platform but uses a separate schema:

| Provider | Schema | Example Table |
|---|---|---|
| PostgreSQL | `files` | `files.file_nodes` |
| SQL Server | `files` | `files.file_nodes` |
| MariaDB | _(prefix)_ | `files_file_nodes` |

The connection string is inherited from the core platform configuration.

### Migrations

Migrations are applied automatically on startup via `FilesDbInitializer`. To apply manually:

```powershell
dotnet ef database update --project src\Modules\Files\DotNetCloud.Modules.Files.Data --startup-project src\Modules\Files\DotNetCloud.Modules.Files.Host
```

---

## Admin Dashboard

The Files module admin settings page is available at `/admin/files` in the web UI. It provides:

- **Storage path** — view and update the storage root directory
- **Default quota** — set the default quota for new users
- **Trash retention** — configure auto-cleanup period
- **Version retention** — configure max versions and retention days
- **Upload limits** — set maximum file size and allowed/blocked MIME types
- **Collabora settings** — configure document editing (see [Collabora Admin Guide](COLLABORA.md))

---

## Environment Variables

All configuration settings can be overridden via environment variables using the standard ASP.NET Core pattern:

```bash
# Override default quota to 20 GB
export Files__Quota__DefaultQuotaBytes=21474836480

# Override trash retention to 60 days
export Files__TrashRetention__RetentionDays=60

# Override Collabora server URL
export Files__Collabora__ServerUrl=https://collabora.example.com
```

On Windows:

```powershell
$env:Files__Quota__DefaultQuotaBytes = "21474836480"
```

---

## Health Checks

The Files module contributes to the `/health` endpoint:

- **FilesHealthCheck** — verifies database connectivity and storage engine accessibility
- **CollaboraHealthCheck** — verifies Collabora server connectivity (if enabled)

Check health status:

```
GET /health
```

---

## Related Documentation

- [Collabora Administration](COLLABORA.md)
- [Backup & Restore](BACKUP.md)
- [Module Overview](../modules/README.md)
- [REST API Reference](../modules/API.md)
