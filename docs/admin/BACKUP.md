# Files Module — Backup & Restore Procedures

> **Last Updated:** 2026-03-03

---

## Overview

Backing up the Files module requires two components:

1. **Database** — file metadata, versions, shares, tags, comments, quotas
2. **File storage** — actual file chunk data on disk

Both must be backed up together and restored together for a consistent state.

---

## What to Back Up

| Component | Location | Contains |
|---|---|---|
| **Database** | PostgreSQL / SQL Server | `files.*` schema (file_nodes, file_versions, file_chunks, file_shares, etc.) |
| **File storage** | `{StorageRoot}` directory | Binary chunk files, thumbnails |
| **Configuration** | `appsettings.json` | Module settings (quota, retention, Collabora, storage path) |

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
