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
| **Configuration**    | `/etc/dotnetcloud/config.json`                                       | Database connection string, ports, TLS mode, module list                      |
| **TLS material**     | `/etc/dotnetcloud/certs/`                                            | `dotnetcloud-selfsigned.pfx` (server), `.pem` (Collabora), `dotnetcloud-root-ca.crt` |
| **Runtime env**      | `/etc/dotnetcloud/env`                                               | Collabora/WOPI settings (public origin, discovery/proxy URLs, signing key)    |
| **Service unit**     | `/etc/systemd/system/dotnetcloud.service`                            | Service definition — may contain operator-added `Environment=` secrets        |
| **Server settings**  | `/opt/dotnetcloud/appsettings.json`, `VERSION`                       | AllowedHosts, Kestrel, AI/email/ICE settings; deployed version                |
| **Collabora config** | `/etc/coolwsd/coolwsd.xml` (+ `cert.pem`, `ca-chain.cert.pem`)       | WOPI alias group added by the installer                                       |
| **Storage manifest** | Embedded by `dotnetcloud backup`; also `dotnetcloud backup manifest` | Index mapping every stored blob back to its file (see below)                 |

> **⚠️ Certificates and env are not optional.** `config.json` points at
> `/etc/dotnetcloud/certs/dotnetcloud-selfsigned.pfx`; if that file is missing the server throws
> `FileNotFoundException` at startup and the unit crash-loops. The root CA must also be re-installed
> into the system trust store or the server's own module gRPC clients fail TLS validation.

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

DotNetCloud includes a built-in backup command that produces a **complete, cold-restorable** archive:

```bash
sudo dotnetcloud backup --output /var/backups/dnc/dotnetcloud-2026-11-03.zip
```

By default it includes:

| Archive path                     | Source                                                      |
| -------------------------------- | ----------------------------------------------------------- |
| `database.sql`                   | `pg_dump -Fp` (PostgreSQL) or `sqlcmd BACKUP DATABASE`       |
| `data/**`                        | `{DataDirectory}` minus the backup directory                |
| `config/config.json`             | `/etc/dotnetcloud/config.json`                              |
| `config/env`                     | `/etc/dotnetcloud/env`                                      |
| `config/certs/**`                | TLS PFX/PEM + root CA                                       |
| `system/systemd/dotnetcloud.service` | Service unit (includes locally-added env vars)          |
| `system/bin/dotnetcloud`         | CLI wrapper on `PATH`                                       |
| `system/coolwsd/**`              | Collabora config + certs (if present)                       |
| `opt/appsettings.json`, `opt/VERSION` | Server settings and deployed version                   |
| `manifest/storage-manifest.tsv`  | Blob → file index                                           |
| `backup-info.json`               | Machine-readable metadata for `dotnetcloud restore`         |

A `<archive>.sha256` sidecar is written next to the archive.

Behaviour guarantees:

- **The database dump is on by default** and `pg_dump`/`psql` read the password from the
  connection string in `config.json` via `PGPASSWORD`. If the dump fails, the backup **aborts** rather
  than silently producing a file-only archive.
- **Compression** defaults to `optimal`. Use `--compression store` for large media libraries — ZIP
  cannot shrink already-compressed photos/videos, so `store` is much faster. `--compression fastest`
  is a middle ground.
- `--no-db-dump` explicitly creates a file-only backup (archive is marked as unable to restore the DB).
- `--no-system` skips the system configuration (not recommended for disaster recovery).
- `--no-manifest` skips the storage manifest (e.g. when the database is unreachable).
- The backup directory and the in-progress archive are always excluded, so backups never nest.

The manifest is generated automatically, before the archive is written, so every backup explains its own
storage tree:

```bash
sudo dotnetcloud backup --output /var/backups/dnc/dotnetcloud-2026-11-03.zip
#   → archive gains manifest/storage-manifest.tsv
#   → standalone copy at /var/lib/dotnetcloud/backups/dotnetcloud-manifest-<timestamp>.tsv

sudo dotnetcloud backup --no-manifest      # opt out
sudo dotnetcloud backup --no-db-dump       # explicit file-only backup
```

> **Take the final pre-disaster backup with the service stopped** (`sudo systemctl stop dotnetcloud`).
> With the service running, a file uploaded mid-walk may be referenced by the database but absent from
> the archive. Content-addressed blobs are immutable, so the window is small — but a cold snapshot
> removes it entirely.

If the manifest cannot be written the backup still completes, with a warning — look for
`Storage manifest could not be written` and re-run `dotnetcloud backup manifest` afterwards.

The archive deliberately **excludes the backup directory itself** (`/var/lib/dotnetcloud/backups` on a
system install) and the archive file being written. Without that, every backup would nest all of its
predecessors — and, since the archive is created before the data directory is walked, a partial copy of
itself. Nothing is lost by excluding that directory: the database dump and the storage manifest are both
added explicitly at fixed paths (`database.sql`, `manifest/storage-manifest.tsv`).

#### Operating on a non-default configuration directory

The CLI reads its configuration from `/etc/dotnetcloud` unless told otherwise. These two forms are
equivalent, and `--config-dir` is accepted anywhere on the command line (including after a subcommand):

```bash
dotnetcloud --config-dir /etc/dotnetcloud backup --output /backup/dnc.zip
DOTNETCLOUD_CONFIG_DIR=/etc/dotnetcloud dotnetcloud backup --output /backup/dnc.zip
```

Commands that need root re-run themselves under `sudo`. Because `sudo` resets the environment, the resolved
configuration directory is passed to the elevated process on its command line — so a command aimed at a
custom directory always operates on **that** directory instead of silently falling back to
`/etc/dotnetcloud`.

⚠️ **Confirm the directory before starting a backup:** a path that does not contain a configuration file is
reported (`No configuration found. Run 'dotnetcloud setup' first.`) rather than assumed, so check the resolved
configuration first — a command run without `--config-dir` operates on the system configuration:

```bash
DOTNETCLOUD_CONFIG_DIR=/tmp/dnc-test dotnetcloud status   # confirm ports/paths match the test config
```

### Method 2: Admin UI / Scheduled Backup (server-side)

Admin → **Backup & Restore** → **Run Backup Now** (or the scheduled `BackupHostedService`) writes a
restore-complete archive to the configured directory (default `/var/lib/dotnetcloud/backups`). It contains:
- `database.sql` — `pg_dump` using the password from `config.json` (`PGPASSWORD`)
- `data/**` — the data directory, **excluding** the backup directory and the archive itself
- `config/config.json`, `config/env`, `config/certs/*` (PFX, PEM, root CA)
- `system/systemd/dotnetcloud.service`, `system/bin/dotnetcloud`
- `opt/appsettings.json`, `VERSION`
- `backup-info.json` and `<archive>.sha256`

The archive layout is identical to the CLI backup and is consumed by `dotnetcloud restore` unchanged
(verified end-to-end, including a scratch-database restore of `database.sql`).

**Large datasets:** the Admin button starts a **background job** (`POST backup/start`) and the page
polls `backup/status` — the HTTP request returns immediately, so the browser's 100-second timeout can
never cancel a multi-gigabyte backup. The page shows the current phase and file/byte progress, and a
**Cancel Backup** button requests cancellation. The **Compression** setting accepts `optimal`,
`fastest` or `store` (best for media).

Two differences from the CLI backup, recorded in `backup-info.json` → `warnings`:

1. **No storage manifest.** The core server cannot query the process-isolated Files module's database, so
   `manifest/storage-manifest.tsv` is CLI-only. It is **not required** for a restore: `database.sql` contains
   every storage path and version/chunk mapping.
2. **Collabora config needs a read ACL.** `/etc/coolwsd/coolwsd.xml` and the public certificate chain are
   `root:cool 640`. The installer grants the service account read-only access with
   `setfacl -m u:dotnetcloud:r` on exactly those three files — the `cool` group is deliberately **not** used,
   because it also owns the TLS private key (`/etc/coolwsd/key.pem`). A `coolwsd` package upgrade replaces
   `coolwsd.xml` and drops the ACL; re-run `install.sh` to re-apply it. If the ACL is missing, the archive
   records an explicit warning and the CLI backup (run as root) still captures the files.

> **Limitations:** the Admin page's *Encrypt Backups* and *Retention Count* settings are stored but not yet
> enforced by the server-side backup. Use the CLI/cron plus external retention/encryption if you need those.

### Method 3: Manual Backup

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
sudo dotnetcloud restore /var/backups/dnc/dotnetcloud-2026-11-03.zip
```

`dotnetcloud restore` performs a **cold restore** in this order:

1. Reads `backup-info.json` and prints a plan (database dump / data / configuration / system files).
2. Stops `dotnetcloud` (unless `--skip-service`).
3. Restores `/etc/dotnetcloud` (`config.json`, `env`, `certs/`), the systemd unit, `/usr/local/bin/dotnetcloud`,
   Collabora config and `/opt/dotnetcloud/appsettings.json` + `VERSION`, fixing ownership and modes
   (secrets stay `640 root:dotnetcloud`, key directories stay `700`).
4. Installs the archived root CA into `/usr/local/share/ca-certificates/` and runs `update-ca-certificates`.
5. Ensures the PostgreSQL role exists, keeps its password **in sync with the restored `config.json`**, and
   verifies the target database is empty.
6. Applies `database.sql` with `psql -v ON_ERROR_STOP=1` — streamed from the archive into `psql` stdin.
7. Restores `data/**` — streamed straight to the destination, then `chown -R dotnetcloud:dotnetcloud` and
   re-applies `0600`/`0700` to secret material. **No full staging copy is made**: peak disk is the archive
   plus the restored tree (~2×, not 3×).
8. `systemctl daemon-reload` / `enable` / `start` and waits for `/health/ready`.

Options:

| Option               | Effect                                                                                     |
| -------------------- | ------------------------------------------------------------------------------------------ |
| `--yes`              | Non-interactive (skip confirmation)                                                        |
| `--replace-database` | Drop and recreate the database when it already contains tables (otherwise the restore aborts) |
| `--skip-database`    | Restore files/configuration only                                                           |
| `--skip-system`      | Do not touch `/etc/dotnetcloud`, certs, env or the unit                                    |
| `--skip-service`     | Do not stop or start the systemd service                                                   |
| `--data-dir <path>`  | Restore data somewhere other than the path recorded in `config.json`                       |
| `--no-health-wait`   | Do not poll `/health/ready` after starting                                                 |

> **Restoring onto a machine where `setup` already created a database:** `setup` generates a new random
> DB password. The restore updates the role password to the one in the archived `config.json`, so the
> service can connect. Do **not** restore `config.json` and then run `setup` again without checking it.

After the restore, validate the storage tree:

```bash
sudo dotnetcloud backup manifest --output -
# → final line reports missingWholeFileBlobs=0 truncatedWholeFileBlobs=0
```

`scripts/audit-whole-file-blobs.sh` reports the whole-file subset directly.

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
cp /opt/dotnetcloud/appsettings.json /backup/appsettings.json
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
sudo dotnetcloud backup schedule --interval daily
```

This prints the scheduler configuration. The server's `BackupHostedService` can also run scheduled
backups when `Backup:EnableAutoBackup` is enabled in Admin → Backup Settings.

### Using cron (Linux)

```cron
# Daily cold backup at 2:00 AM (stop, back up, start)
0 2 * * * /usr/bin/systemctl stop dotnetcloud && /usr/local/bin/dotnetcloud backup --output /var/backups/dnc/dotnetcloud-$(date +\%Y-\%m-\%d).zip && /usr/bin/systemctl start dotnetcloud
```

### Using Task Scheduler (Windows)

```powershell
$action = New-ScheduledTaskAction -Execute "dotnetcloud.exe" -Argument "backup --output D:\Backup\dotnetcloud-$(Get-Date -Format 'yyyy-MM-dd').zip"
$trigger = New-ScheduledTaskTrigger -Daily -At 2am
Register-ScheduledTask -TaskName "DotNetCloudBackup" -Action $action -Trigger $trigger
```

---

## Backup Retention

Manage backup retention to avoid filling disk space:

```bash
# Keep only the last 30 days of backups
find /var/backups/dnc -name "dotnetcloud-*.zip" -mtime +30 -delete
```

```powershell
# Windows: delete backups older than 30 days
Get-ChildItem "D:\Backup\dotnetcloud-*.zip" | Where-Object { $_.LastWriteTime -lt (Get-Date).AddDays(-30) } | Remove-Item
```

---

## Disaster Recovery

### Complete Data Loss

1. Copy the backup **off** the machine first (the uninstaller removes `/opt/dotnetcloud`,
   `/etc/dotnetcloud` and `/var/lib/dotnetcloud`).
2. Reinstall the platform footprint on the same host (or a replacement) with the same hostname:

   ```bash
   sudo ./tools/install.sh
   ```

   This creates the `dotnetcloud` user, directories, `/opt/dotnetcloud` binaries, the systemd unit and
   the `/usr/local/bin/dotnetcloud` wrapper. **Do not run `dotnetcloud setup` yet** — the restore
   supplies the real configuration and database credentials.

3. Restore:

   ```bash
   sudo dotnetcloud restore /var/backups/dnc/dotnetcloud-<timestamp>.zip --yes
   ```

   The restore recreates the role/database, applies `database.sql`, restores `data/**`, installs the
   root CA, then starts the service and waits for health.

4. Verify: `/health/ready`, admin login, a file download, a chat with history, and a Collabora edit.
5. Re-take a backup and confirm `missingWholeFileBlobs=0` in a fresh manifest.

> If the **whole OS** is rebuilt, also install PostgreSQL (same major version), the .NET runtime,
> `pigz`, and `coolwsd` if Collabora is used. Keep the hostname unchanged: the self-signed certificate's
> SAN is `mint22` (plus localhost/loopback). Changing the hostname requires `dotnetcloud cert-renew`.

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
