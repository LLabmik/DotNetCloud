# Files Module — Admin Configuration Guide

> **Last Updated:** 2026-10-21

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

| Platform | Default                           |
| -------- | --------------------------------- |
| Linux    | `/var/lib/dotnetcloud/files`      |
| Windows  | `%ProgramData%\DotNetCloud\files` |

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

| Platform | User/Group                             | Permissions       |
| -------- | -------------------------------------- | ----------------- |
| Linux    | `dotnetcloud:dotnetcloud`              | `750` (rwxr-x---) |
| Windows  | `NETWORK SERVICE` or app pool identity | Full Control      |

Ensure the DotNetCloud process has read/write access to the storage root.

---

## Default User Content (New-Account Template)

Every newly created account can be seeded with a starter folder tree that is copied from a
directory on the server. The administrator maintains that directory **directly on the filesystem** —
there is no upload UI, and the service never deletes or rewrites anything in it.

### Template Location

| Deployment                                     | Path                                                                |
| ---------------------------------------------- | ------------------------------------------------------------------- |
| Linux (systemd, `tools/install.sh`)            | `/var/lib/dotnetcloud/default-user-content`                         |
| Docker Compose                                 | `/app/data/default-user-content` (in the `dotnetcloud-data` volume) |
| Single container (`-v dotnetcloud-data:/data`) | `/data/default-user-content`                                        |
| Windows service / IIS                          | `C:\ProgramData\DotNetCloud\default-user-content`                   |
| Development (`dotnet run`)                     | `{ContentRoot}/default-user-content`                                |

Resolution order in the Files module:

1. `Files:DefaultUserContent:RootPath` (explicit setting — **required on Windows**; `DOTNETCLOUD_DATA_DIR`
   is a Linux-only environment variable).
2. `{DOTNETCLOUD_DATA_DIR}/default-user-content`.
3. `{ContentRoot}/default-user-content` (development only).

The path actually in use is shown on **Admin → Files → Default User Content**.

### Default Layout

A fresh install guarantees these five folders (and copies any starter files the release ships in
`deploy/seed/default-user-content/`):

```
default-user-content/
├── Documents/
├── Photos/
├── Music/
├── Templates/
└── Videos/
```

### Managing the Template

```bash
TEMPLATE=/var/lib/dotnetcloud/default-user-content

# Create/refresh the stock layout (additive only)
sudo -u dotnetcloud mkdir -p "$TEMPLATE"/{Documents,Photos,Music,Templates,Videos}

# Copy starter content in (any SFTP/SSH copy works)
sudo cp -r ./starter/. "$TEMPLATE"/

# The service user must be able to READ every entry
sudo chown -R dotnetcloud:dotnetcloud "$TEMPLATE"
sudo find "$TEMPLATE" -type d -exec chmod 755 {} +
sudo find "$TEMPLATE" -type f -exec chmod 644 {} +

# Confirm the service can read the files
sudo -u dotnetcloud find "$TEMPLATE" -type f -readable | wc -l
```

- **No restart is required.** The template is read when an account is created, so changes apply to the
  next new account (or the next backfill run).
- **Read permission matters.** Files copied as `root` with a restrictive umask (mode `600`) are
  unreadable by the service and are skipped with `Permission denied`. Run the `chown`/`chmod` lines
  above after every copy.
- **Symlinks are skipped**, never followed.
- **Windows-illegal names** (`\ / : * ? " < > |` and reserved names such as `CON`) are skipped so
  that user folders stay syncable to Windows clients.
- **Hidden files** (dotfiles, `.DS_Store`, `Thumbs.db`, `*.tmp-*`) are skipped by default.
- Copy first, then `chown`/`chmod`; then run **Validate** (below).

### Validating the Template

After changing the directory, run either:

- **Admin → Files → Default User Content → Validate**, or
- `dotnetcloud files default-content validate`

Validation lists unreadable files, symlinks, Windows-illegal names, files larger than the per-file
cap, totals over the total cap, and hidden entries that will be skipped.

### Existing Users and Start-Up Convergence

Convergence is **automatic**: every time DotNetCloud starts, a rate-limited background pass gives
any user the template items they do not already have. A daily pass repeats the check, so a user
created while the Files module was down is caught up without intervention.

Convergence adds what is missing. Removals are detected automatically but are **never applied by an
automatic pass** — they wait for an administrator to confirm:

| Situation                                           | Result                                                                                                                                                                                                                                                         |
| --------------------------------------------------- | -------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| New template item the user never received           | Copied to the user                                                                                                                                                                                                                                             |
| User deleted a template item                        | Soft-deleted into the user's trash and remembered; **not** recreated on restart, and never auto-restored. Automatic trash cleanup **skips starter content**, so it stays recoverable until the user restores or explicitly deletes it permanently.             |
| Admin removes an item from the template             | Marked as **pending deletion** and shown on the admin page — automatic passes delete nothing. A confirmed run permanently deletes matching unmodified copies (user-modified copies are kept; folders only when they contain no user content).                  |
| User modified a template item                       | Left untouched; its content is no longer quota-exempt                                                                                                                                                                                                          |
| Admin edits a template file that users already have | Marked as a pending **update**. Automatic passes change nothing; a _publish updates_ run replaces unmodified copies **in place** (default content keeps no version history). User-modified copies are only replaced by an explicit _overwrite modified_ action. |

To apply a change immediately, or to also replace copies users have modified, use:

- **Admin → Files → Default User Content → Run convergence now**, or
- `dotnetcloud files default-content reconcile --all` (add `--publish-updates` to push template changes to copies users never modified, `--dry-run` to preview).

Both only queue work for the Files module; they never write user files directly.

**Removals require confirmation.** If a run would permanently delete any user content (because items
were removed from the template), the admin page shows a banner with the pending count and a dialog
listing the number of items to add, the number to delete, and a sample of affected paths; the run only
proceeds after an acknowledgement checkbox and **Confirm**. The API enforces the same rule (`409` with
`requiresConfirmation` until `confirmDeletions=true`), and the CLI refuses to queue anything and exits
with code `2` unless `--confirm-deletions` is passed. Start-up and daily passes never delete, so a
template edit can never destroy user content unattended.

### Configuration Reference

| Setting                                    | Default   | Description                                        |
| ------------------------------------------ | --------- | -------------------------------------------------- |
| `Files:DefaultUserContent:Enabled`         | `true`    | Seed new accounts from the template.               |
| `Files:DefaultUserContent:RootPath`        | _(empty)_ | Explicit template directory (required on Windows). |
| `Files:DefaultUserContent:MaxFiles`        | `500`     | Maximum files copied per user.                     |
| `Files:DefaultUserContent:MaxTotalBytes`   | `5 GiB`   | Maximum total size of the template.                |
| `Files:DefaultUserContent:MaxFileBytes`    | `500 MiB` | Maximum size of a single template file.            |
| `Files:DefaultUserContent:MaxDepth`        | `8`       | Maximum directory nesting.                         |
| `Files:DefaultUserContent:SkipHiddenFiles` | `true`    | Skip dotfiles and OS metadata files.               |

Starter content **does not count against the user's storage quota**. Each user's allowance is
`quota + exempt starter content`, where the exemption is recomputed from the template items they
still have unmodified. The Files quota bar shows this as a secondary line —
**"N MB default content (free)"** — and the admin page shows the same figure in its breakdown.
Deleting a template file frees its bytes and its exemption together, so the
usable allowance stays the same; replacing a template file with your own content charges it normally.
The exemption is recomputed at start-up, daily, and after each convergence pass.

### Developer-Supplied Starter Content

A checked-in tree at `deploy/seed/default-user-content/` ships inside the release tarball and the
container image. It is copied into the template directory **only on a fresh install** (when the
directory is missing or empty) and is never applied on upgrade, so an existing deployment's template
is never modified.

After an upgrade that ships new starter content, copy it in explicitly:

```bash
dotnetcloud files default-content sync-seed
```

`sync-seed` copies only paths that do not already exist in the template; it never overwrites or
deletes. Any administrator-customised file with the same path is kept.

### Backups and Upgrades

- The template lives under the data directory and is included in normal backups
  (`tar -I pigz ... /var/lib/dotnetcloud`, `dotnetcloud backup`). Restoring a backup restores the
  template too; the installer keeps it across upgrades.
- `dotnetcloud files default-content export <path.zip>` (or the **Export ZIP** button) produces a
  template-only archive. It never contains any user's copy.
- `dotnetcloud files default-content import <path.zip>` restores such an archive. It validates the ZIP
  first (no absolute paths, symlinks, zip bombs, illegal names, or entries over the caps), stages it,
  then merges (default) or replaces with `--replace --force`, keeping the previous template as
  `default-user-content.bak-<timestamp>`. Use `--dry-run` to preview. The admin UI does not upload
  archives — run the command on the server.
- **In-place upgrades preserve the directory.** The installer replaces binaries under
  `/opt/dotnetcloud` and leaves `/var/lib/dotnetcloud` untouched; only a full uninstall (which
  removes all data) deletes it. In Docker, it lives in the `dotnetcloud-data` named volume — do not
  run `docker compose down -v` unless you intend to delete all data.

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

| Setting                   | Default               | Description                                                    |
| ------------------------- | --------------------- | -------------------------------------------------------------- |
| `DefaultQuotaBytes`       | `10737418240` (10 GB) | Default storage limit for new users. Set to `0` for unlimited. |
| `ExcludeTrashedFromQuota` | `false`               | When `true`, files in trash do not count against quota.        |
| `WarnAtPercent`           | `80.0`                | Publish `QuotaWarningEvent` at this usage percentage.          |
| `CriticalAtPercent`       | `95.0`                | Publish `QuotaCriticalEvent` at this usage percentage.         |
| `RecalculationInterval`   | `24:00:00`            | How often the background service recalculates all user quotas. |

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

| Setting           | Default              | Description                                                                                |
| ----------------- | -------------------- | ------------------------------------------------------------------------------------------ |
| `RetentionDays`   | `30`                 | Days to keep items in trash before permanent deletion. Set to `0` to disable auto-cleanup. |
| `CleanupInterval` | `06:00:00` (6 hours) | How often the `TrashCleanupService` runs.                                                  |

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

### Admin page (`/admin/files`)

The primary way to control versioning is **Admin → File Settings** (`/admin/files`), which exposes:

| Field                             | Stored key                   | Description                          |
| --------------------------------- | ---------------------------- | ------------------------------------ |
| Keep file versions                | `VersionRetention:Enabled`   | Turn version history on or off       |
| Maximum versions per file         | `VersionRetention:MaxNumber` | `0` = no count limit (up to 1000)    |
| Delete versions older than (days) | `VersionRetention:MaxDays`   | `0` = keep indefinitely (up to 3650) |

The page shows the resulting policy in a "Currently Enforced" readout and can run the retention pass
immediately (**Run retention now**) instead of waiting for the daily maintenance pass. Limits are
also applied as files change — an upload or document save that creates a version prunes the older
ones in the same operation.

**Precedence:** the configuration below is the baseline; the values saved on `/admin/files` are
layered on top of it, so the page is the effective policy. Clearing a row (or leaving the
configuration sections out entirely) falls back to the baseline. Changes apply within about 30
seconds and need no restart.

When **Keep file versions** is off, only the current version of each file is retained: the content
users open is never touched, and each new upload or editor save releases the older versions.
Labeled versions are always kept.

### Default Settings

```json
{
  "Files": {
    "VersionRetention": {
      "Enabled": true,
      "MaxVersionCount": 50,
      "RetentionDays": 0,
      "CleanupInterval": "24:00:00"
    }
  }
}
```

### Configuration Reference

| Setting           | Default        | Description                                                          |
| ----------------- | -------------- | -------------------------------------------------------------------- |
| `Enabled`         | `true`         | Record new versions. When `false`, only the current version is kept. |
| `MaxVersionCount` | `50`           | Maximum versions per file. Set to `0` for unlimited.                 |
| `RetentionDays`   | `0` (disabled) | Delete unlabeled versions older than this. Set to `0` to disable.    |
| `CleanupInterval` | `24:00:00`     | How often the `VersionCleanupService` runs.                          |

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
    "MaxFileSizeBytes": 21474836480,
    "MaxZipSizeBytes": 4294967296
  }
}
```

| Setting            | Default                | Description                                                                                                                                                                                             |
| ------------------ | ---------------------- | ------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| `MaxFileSizeBytes` | `21474836480` (20 GiB) | Maximum file size allowed for upload. The web UI validates this client-side before uploading, showing a user-friendly error with the formatted size limit. The server also enforces this limit.         |
| `MaxZipSizeBytes`  | `4294967296` (4 GiB)   | Maximum size of a generated multi-item ZIP download (multiple files/folders downloaded at once). Downloads that would exceed this limit fail with HTTP 413 and the web UI shows an informational modal. |

The client retrieves these limits from `GET /api/v1/files/config` on page load and rejects oversized files immediately with a clear error message.

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

| Service                       | Default Interval | Purpose                                                 |
| ----------------------------- | ---------------- | ------------------------------------------------------- |
| `UploadSessionCleanupService` | 1 hour           | Expire stale upload sessions, GC orphaned chunks        |
| `TrashCleanupService`         | 6 hours          | Permanently delete expired trash items                  |
| `QuotaRecalculationService`   | 24 hours         | Recalculate per-user storage usage                      |
| `VersionCleanupService`       | 24 hours         | Prune old unlabeled versions exceeding retention limits |

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

| Provider   | Schema  | Example Table      |
| ---------- | ------- | ------------------ |
| PostgreSQL | `files` | `files.file_nodes` |
| SQL Server | `files` | `files.file_nodes` |

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
