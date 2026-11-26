# Database Maintenance — Growth, Logs and Retention

> **Last Updated:** 2026-11-25

This guide is for operators whose `DotNetCloud` database keeps growing. It explains where the
space actually goes, how to reclaim it safely, and which tables the application prunes on its own.

Commands below are written for SQL Server (the production default); PostgreSQL equivalents are noted
where they differ. Replace `<sql-host>` with your database host.

---

## 1. Diagnose before you delete anything

The single most common cause of a "huge" database is **not** data. Measure first:

```sql
-- File-level split: data vs. transaction log
SELECT name, type_desc, CAST(size * 8.0 / 1024 AS decimal(12,1)) AS size_mb
FROM sys.database_files;

-- Recovery model and why the log cannot truncate
SELECT recovery_model_desc, log_reuse_wait_desc
FROM sys.databases WHERE name = DB_NAME();

-- Per-table split (schema, rows, data MB, index MB)
SELECT s.name AS schema_name, t.name AS table_name,
       SUM(p.rows) AS row_count,
       CAST(SUM(a.data_pages) * 8.0 / 1024 AS decimal(12,1)) AS data_mb,
       CAST((SUM(a.total_pages) - SUM(a.data_pages)) * 8.0 / 1024 AS decimal(12,1)) AS index_mb
FROM sys.tables t
JOIN sys.schemas s ON s.schema_id = t.schema_id
JOIN sys.indexes i ON i.object_id = t.object_id
JOIN sys.partitions p ON p.object_id = i.object_id AND p.index_id = i.index_id
JOIN sys.allocation_units a ON a.container_id = p.partition_id
WHERE t.is_ms_shipped = 0 AND i.index_id > 0
GROUP BY s.name, t.name
ORDER BY CAST(SUM(a.total_pages) * 8.0 / 1024 AS decimal(12,1)) DESC;
```

> **⚠️ `SUM(sys.partitions.rows)` is multiplied by the number of indexes on the table.** A 1,000-row
> table with nine indexes reports 9,000. Use `COUNT(*)` when the actual row count matters, or group by
> `index_id = 1`.

### Interpreting the result

| Finding                                    | Meaning                                                    |
| ------------------------------------------ | ---------------------------------------------------------- |
| `.ldf` dwarfs `.mdf`                       | Log growth, not data. See §2.                              |
| `log_reuse_wait_desc = LOG_BACKUP`         | Truncation is blocked because no log backup is ever taken. |
| One table holding `varbinary(max)` columns | Stored binaries (for example photo thumbnails), not bloat. |
| Many zero-row tables in a legacy schema    | Dead schema left behind by a past rework. See §5.          |

---

## 2. The transaction log

A database in `FULL` recovery keeps every log record until a **log backup** copies it away. Full or
differential backups do **not** truncate the log. An instance that only ever takes full backups will
therefore grow its log indefinitely — a database with under 1 GB of data can easily reach tens of GB.

Choose one strategy:

**A. Simple recovery (recommended when point-in-time recovery is not needed).**

```sql
ALTER DATABASE DotNetCloud SET RECOVERY SIMPLE;
CHECKPOINT;
DBCC SHRINKFILE (DotNetCloud_log, 1024);   -- re-evaluate size_mb from §1 first
```

The log now truncates automatically at every checkpoint and stays small (a few hundred MB is normal).

**B. Keep FULL and schedule log backups** (required if you want point-in-time restore):

```sql
BACKUP LOG DotNetCloud TO DISK = N'<backup-path>\DotNetCloud_log.trn' WITH COMPRESSION;
```

Run it every 15–30 minutes. Once a log backup completes the log truncates itself and stops growing;
then shrink once:

```sql
DBCC SHRINKFILE (DotNetCloud_log, 1024);
```

> **⚠️ Do not shrink on a schedule.** Shrinking is a one-off repair for a log that was never truncated.
> Shrinking repeatedly fragments the file and the file grows straight back.

> **⚠️ PostgreSQL** does not have this failure mode (WAL is rotated automatically). Space that is
> never reused there usually comes from dead tuples — check with `pg_stat_all_tables.n_dead_tup` and
> rely on autovacuum.

---

## 3. Shrinking the data file

If §1 shows a large gap between the file size and the sum of the tables, the space is free-but-allocated:

```sql
DBCC SHRINKFILE (DotNetCloud, <target-mb>);   -- leave 10–20 % headroom above used space
```

Only do this after deleting data (for example the legacy tables in §5); otherwise the file simply
grows again. Index rebuilds can also leave allocated space behind — `ALTER INDEX … REBUILD` on the
largest indexes reclaims it without shrinking the file.

---

## 4. What the application already prunes

| Data                              | Retention                                  | Where it is enforced                     |
| --------------------------------- | ------------------------------------------ | ---------------------------------------- |
| `dbo.AuditLogs`                   | `core.AuditLogRetentionDays` (default 365) | `AuditLogPurgeHostedService` — daily     |
| `core.OpenIddictTokens` (expired) | deleted once expired                       | `ExpiredTokenPurgeHostedService` — daily |
| `core.UploadSessions` (finished)  | row deleted 7 days after `ExpiresAt`       | `UploadSessionCleanupService` — hourly   |
| Orphaned file chunks              | `ReferenceCount = 0` and unreferenced      | `UploadSessionCleanupService` — hourly   |
| Trash (`FileNodes.IsDeleted`)     | `core.TrashRetentionDays` (default 30)     | Trash cleanup                            |
| Old file versions                 | operator-triggered                         | Admin → Files → _Prune versions_         |

Everything else is retained indefinitely by design. In particular:

- **`core.SearchIndexEntries`** — one or more rows per indexed item; scales with the content library.
- **`core.FileNodes` / `FileVersions` / `MountedNodeEntries`** — scale with the number of files.
- **Module data** (`music`, `video`, `dnc_dotnetcloud_<module>_<hash>`) — user content; only shrinks
  when the user data is removed.
- **Photo thumbnails** — stored as `varbinary(max)` in the Photos module's `Photos` table, so that
  table is sized by thumbnail bytes rather than row count.

If a deployment is short on space, the items to review are the ones above, not the framework tables.

---

## 5. Legacy schema left behind by past reworks

Some tables are no longer part of any EF Core model — they were superseded but never dropped. They are
unreadable by the current code, so they can be removed once verified empty.

**Music (handled by a migration).** Migration `DropLegacyMusicTables` drops the pre-canonical-library
tables (`Tracks`, `Albums`, `Artists`, `Genres`, `TrackArtists`, `TrackGenres`). They are guarded with
`IF EXISTS`, so fresh databases are unaffected.

**Contacts (manual — contains data).** Contacts became a required module and now store in `core.*`;
installations upgraded from an older release may still have a `contacts` schema holding the pre-move
rows. **Check the row counts before touching it:**

```sql
SELECT (SELECT COUNT(*) FROM core.Contacts)       AS live_contacts,
       (SELECT COUNT(*) FROM contacts.Contacts)   AS legacy_contacts;
```

If `legacy_contacts` is `0`, drop the schema. If it is not, decide whether those rows still matter; they
predate the move and may already be covered by `core.Contacts`. Do not drop the schema without a
backup.

**SQL Server diagrams.** `dbo.sysdiagrams` is created by SSMS, not by DotNetCloud. It is safe to drop
once no operator needs the saved diagrams.

---

## 6. Verify after any change

```sql
SELECT name, type_desc, CAST(size * 8.0 / 1024 AS decimal(12,1)) AS size_mb FROM sys.database_files;
SELECT recovery_model_desc, log_reuse_wait_desc FROM sys.databases WHERE name = DB_NAME();
```

Expected after a correct log fix: the log sits at a stable few hundred MB and `log_reuse_wait_desc` is
`NOTHING` (SIMPLE) or `LOG_BACKUP` only between two scheduled log backups.

Then confirm the application is healthy: `/health/ready` reports every component healthy and the
background services listed on **Admin → System** show recent successful runs.

---

## 7. Taking a backup before cleanup

Always take a full backup first — the steps in §5 delete data permanently:

```sql
BACKUP DATABASE DotNetCloud TO DISK = N'<backup-path>\DotNetCloud_before_cleanup.bak' WITH COMPRESSION, CHECKSUM;
RESTORE VERIFYONLY FROM DISK = N'<backup-path>\DotNetCloud_before_cleanup.bak';
```

Restore _to a scratch database_ for a rehearsal, never over production.
