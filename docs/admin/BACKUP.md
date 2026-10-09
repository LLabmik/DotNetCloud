# Files Module — Backup & Restore Procedures

> **Last Updated:** 2026-10-09

---

## Overview

Backing up the Files module requires two components:

1. **Database** — file metadata, versions, shares, tags, comments, quotas
2. **File storage** — actual file chunk data on disk

Both must be backed up together and restored together for a consistent state.

---

## What to Back Up

| Component            | Location                                                             | Contains                                                                     |
| -------------------- | -------------------------------------------------------------------- | ---------------------------------------------------------------------------- |
| **Database**         | PostgreSQL / SQL Server                                              | `files.*` schema (file_nodes, file_versions, file_chunks, file_shares, etc.) |
| **File storage**     | `{StorageRoot}` directory                                            | Binary chunk files, thumbnails                                               |
| **Configuration**    | `appsettings.json`                                                   | Module settings (quota, retention, Collabora, storage path)                  |
| **Storage manifest** | Embedded by `dotnetcloud backup`; also `dotnetcloud backup manifest` | Index mapping every stored blob back to its file (see below)                 |

> **⚠️ The storage tree is not self-describing.** Content is content-addressed and stored _without_ a
> file extension — whole-file media blobs at `files/<xx>/<yy>/<sha256>`, chunked files at
> `chunks/<xx>/<yy>/<sha256>`. Nothing on disk records which blob belongs to which file, or in what
> order a chunked file's chunks concatenate, so a copy of the storage tree cannot be interpreted (or
> rebuilt) without the database. **Always generate a manifest alongside the storage backup.**

### Storage Manifest

Generates a tab-separated index of every content blob, keyed by the database rows that reference it.
It works with either database provider (it reads the database through the CLI, not through provider tools).

```bash
# Default: <backup directory>/dotnetcloud-manifest-<timestamp>.tsv
dotnetcloud backup manifest

# Explicit destination
dotnetcloud backup manifest --output /backup/dotnetcloud-manifest-2026-10-09.tsv

# File versions only (much smaller; omits chunk rows)
dotnetcloud backup manifest --no-chunks

# Stream to stdout (for piping into the backup archive)
dotnetcloud backup manifest --output -
```

Each data row is one `file` record (per file version) or one `blob` record (per chunk):

| record                    | meaning                                                                                     |
| ------------------------- | ------------------------------------------------------------------------------------------- |
| `file`, `isChunked=false` | the file's bytes are the single whole-file blob at `storagePath` — copy it as-is            |
| `file`, `isChunked=true`  | the bytes are the concatenation of that version's `blob` rows, in ascending `sequenceIndex` |

The trailing `#totals` line also reports `missingWholeFileBlobs` and `truncatedWholeFileBlobs`. Those
are the dangerous ones: a whole-file media version whose blob is gone still lists normally and only
fails when it is played or downloaded.

> **`dotnetcloud backup` does this for you.** Every backup run generates the manifest _before_ writing the
> archive and embeds it as `manifest/storage-manifest.tsv` (a standalone copy is left next to the archive).
> Use `dotnetcloud backup --no-manifest` to skip it, or run `dotnetcloud backup manifest` on its own for an
> ad-hoc index — e.g. to check for missing content without taking a backup.

---

## Backup Procedures

### Method 1: CLI Backup (Recommended)

DotNetCloud includes a built-in backup command that handles both database and file storage:

```bash
dotnetcloud backup --output /backup/dotnetcloud-2026-03-03.tar.gz
```

This creates a compressed archive containing:

- Database dump (pg_dump / SQL Server bacpac)
- File storage directory
- Configuration files
- **Storage manifest** — `manifest/storage-manifest.tsv`

The manifest is generated automatically, before the archive is written, so every backup explains its own
storage tree:

```bash
dotnetcloud backup --output /backup/dotnetcloud-2026-10-09.zip
#   → archive gains manifest/storage-manifest.tsv
#   → standalone copy at /var/lib/dotnetcloud/backups/dotnetcloud-manifest-<timestamp>.tsv

dotnetcloud backup --no-manifest      # opt out (e.g. when the database is unreachable)
```

If the manifest cannot be written the backup still completes, with a warning — look for
`Storage manifest could not be written` and re-run `dotnetcloud backup manifest` afterwards.

### Method 2: Manual Backup

#### Step 1: Database Backup

**PostgreSQL:**

```bash
pg_dump -U dotnetcloud -d dotnetcloud --schema=files -F c -f /backup/files-db.dump
```

**SQL Server:**

```powershell
SqlPackage /Action:Export /SourceConnectionString:"..." /TargetFile:"D:\Backup\files-db.bacpac"
```

#### Step 2: File Storage Backup

Copy the entire storage root directory:

```bash
# Linux
rsync -a /var/lib/dotnetcloud/files/ /backup/files-storage/
```

```powershell
# Windows
robocopy "C:\ProgramData\DotNetCloud\files" "D:\Backup\files-storage" /MIR /MT:8
```

#### Step 3: Configuration Backup

```bash
cp /etc/dotnetcloud/appsettings.json /backup/appsettings.json
```

### Consistency

For a consistent backup:

1. **Pause uploads** — stop accepting new uploads during backup (or accept minor inconsistency)
2. **Back up database first** — the database references chunks; a missing chunk is recoverable, but a missing database record means orphaned data
3. **Back up storage second** — extra chunks on disk waste space but don't cause errors

In practice, running the backup during low-activity periods is sufficient. The chunked architecture means partially-uploaded files are tracked as sessions and will be cleaned up automatically.

---

## Restore Procedures

### Method 1: CLI Restore

```bash
dotnetcloud restore /backup/dotnetcloud-2026-03-03.tar.gz
```

This restores both the database and file storage.

If you kept a storage manifest, compare it against the restored tree before starting the server: every
`file` row should have its `storagePath` (whole-file) or its ordered `blob` rows (chunked) present under
the storage root. `dotnetcloud backup manifest` run after the restore will re-flag anything missing,
and `scripts/audit-whole-file-blobs.sh` reports the whole-file subset directly.

### Method 2: Manual Restore

#### Step 1: Stop DotNetCloud

```bash
sudo systemctl stop dotnetcloud
```

#### Step 2: Restore Database

**PostgreSQL:**

```bash
pg_restore -U dotnetcloud -d dotnetcloud --clean --schema=files /backup/files-db.dump
```

**SQL Server:**

```powershell
SqlPackage /Action:Import /TargetConnectionString:"..." /SourceFile:"D:\Backup\files-db.bacpac"
```

#### Step 3: Restore File Storage

```bash
# Linux
rsync -a /backup/files-storage/ /var/lib/dotnetcloud/files/
chown -R dotnetcloud:dotnetcloud /var/lib/dotnetcloud/files/
```

```powershell
# Windows
robocopy "D:\Backup\files-storage" "C:\ProgramData\DotNetCloud\files" /MIR /MT:8
```

#### Step 4: Restore Configuration

```bash
cp /backup/appsettings.json /etc/dotnetcloud/appsettings.json
```

#### Step 5: Start DotNetCloud

```bash
sudo systemctl start dotnetcloud
```

#### Step 6: Verify

1. Check health endpoint: `GET /health`
2. Browse files in the web UI
3. Verify quota values: `POST /api/v1/files/quota/{userId}/recalculate` for each user
4. Check that Collabora connects (if enabled)

---

## Scheduled Backups

### Using the CLI

```bash
dotnetcloud backup --schedule daily --output /backup/
```

This creates a cron job / Windows Task Scheduler entry that runs daily backups.

### Using cron (Linux)

```cron
# Daily backup at 2:00 AM
0 2 * * * /usr/local/bin/dotnetcloud backup --output /backup/dotnetcloud-$(date +\%Y-\%m-\%d).tar.gz
```

### Using Task Scheduler (Windows)

```powershell
$action = New-ScheduledTaskAction -Execute "dotnetcloud.exe" -Argument "backup --output D:\Backup\dotnetcloud-$(Get-Date -Format 'yyyy-MM-dd').tar.gz"
$trigger = New-ScheduledTaskTrigger -Daily -At 2am
Register-ScheduledTask -TaskName "DotNetCloudBackup" -Action $action -Trigger $trigger
```

---

## Backup Retention

Manage backup retention to avoid filling disk space:

```bash
# Keep only the last 30 days of backups
find /backup/ -name "dotnetcloud-*.tar.gz" -mtime +30 -delete
```

```powershell
# Windows: delete backups older than 30 days
Get-ChildItem "D:\Backup\dotnetcloud-*.tar.gz" | Where-Object { $_.LastWriteTime -lt (Get-Date).AddDays(-30) } | Remove-Item
```

---

## Disaster Recovery

### Complete Data Loss

1. Install DotNetCloud on a new server: `dotnetcloud setup`
2. Restore from the latest backup: `dotnetcloud restore /backup/latest.tar.gz`
3. Verify all services are running: `dotnetcloud status`
4. Force quota recalculation for all users
5. Notify users to reconnect their sync clients

### Partial Data Loss (Storage Only)

If the database is intact but file storage is lost:

1. Restore file storage from backup
2. Start DotNetCloud — it will detect missing chunks
3. Users can re-upload affected files
4. The `QuotaRecalculationService` will correct quota values

### Partial Data Loss (Database Only)

If file storage is intact but the database is lost:

1. Restore the database from backup
2. Orphaned chunks on disk (not referenced by the restored database) waste space but cause no errors
3. Run a manual garbage collection to clean up orphaned chunks (future feature)

---

## Verification

After any restore operation, verify data integrity:

1. **Health check:** `GET /health` — should return healthy
2. **File listing:** Browse files in the web UI — verify files appear
3. **Download test:** Download a file and verify content integrity
4. **Quota check:** Recalculate quotas for a sample of users
5. **Share test:** Access a public link to verify shares work
6. **Collabora test:** Open a document for editing (if Collabora is enabled)

---

## Related Documentation

- [Admin Configuration](CONFIGURATION.md)
- [Collabora Administration](COLLABORA.md)
- [Architecture](../../modules/files/ARCHITECTURE.md)
