# SOC 2 Compliance — Administrator Guide

**Audience:** DotNetCloud administrators (self-hosters, operators).
**Purpose:** explains what changed in the codebase, how to use the new code to **produce the SOC 2 audit**, and how to manage the changes day-to-day.
**Companion docs:** `docs/SOC2_TYPE_II_COMPLIANCE_PLAN.md` (implementation), `docs/security/SOC2_AUDITOR_GUIDE.md` (auditor-facing).

---

## 1. What changed (administrator's view)

The SOC 2 work adds or changes the following. Items marked **(new)** require action from you.

| Change                                         | What it does                                                                                                                        | Your action                                                               |
| ---------------------------------------------- | ----------------------------------------------------------------------------------------------------------------------------------- | ------------------------------------------------------------------------- |
| **(new)** `AuditLog` database table            | Persistent, attributable audit trail of security-relevant operations (login, MFA, admin actions, module create/update/delete/share) | Run migrations; watch disk usage                                          |
| **(new)** `AuditLogPurgeHostedService`         | Daily background job that deletes audit rows older than the retention window                                                        | Set retention values                                                      |
| **(new)** audit sink (`Serilog:AuditFilePath`) | Writes a mirror of audit events to a file                                                                                           | Configure/rotate the file; keep it out of backups if large                |
| **(new)** retention settings                   | `core.AuditLogRetentionDays`, `core.TrashRetentionDays`                                                                             | Choose values (defaults: audit 365, trash 30)                             |
| **(new)** compliance scanner                   | `scripts/soc2-compliance-scan.sh` / `.ps1` — offline source-code audit tool                                                         | Run it locally with the source available (§3); primary evidence generator |
| **(new)** control matrix + auditor report      | `docs/security/SOC2_CONTROL_MATRIX.md`, `docs/security/SOC2_TYPE_II_AUDITOR_REPORT.md`                                              | Hand these to your auditor                                                |
| Upload validation                              | All file-upload endpoints now enforce extension whitelist + magic-byte checks                                                       | Existing uploads that fail validation will be rejected — see §8           |
| OpenIddict key rotation                        | Documented + scripted rotation of signing keys                                                                                      | Run rotation on schedule (§5)                                             |
| Dependency advisories                          | Two advisories remain suppressed with compensating controls                                                                         | Review the suppression comments on each release                           |
| Privacy tooling                                | PII inventory + data-subject export/delete                                                                                          | Use §7 to satisfy data-subject requests                                   |

**Nothing was removed.** Existing auth (PKCE, MFA/TOTP, lockout, password policy) is unchanged.

---

## 2. Applying the database migration (once per upgrade)

The audit table is added to **both** providers. Apply the migration for your provider before deploying:

```bash
# Production is SQL Server
dotnet ef database update \
  --project src/Core/DotNetCloud.Core.Data \
  --context CoreDbContext

# (The SQL Server migrations live in Migrations/SqlServer and are selected
#  by the CoreDbContextSqlServerDesignTimeFactory via DOTNETCLOUD_DB_CONNECTION.)
```

Verify the table exists:

```bash
# SQL Server (production host)
/opt/mssql-tools18/bin/sqlcmd -S hyperdrive.kimball.home -d DotNetCloud -U dotnetcloud -C \
  -Q "SELECT COUNT(*) FROM [core].[AuditLogs];"

# PostgreSQL (dev)
psql -c "SELECT COUNT(*) FROM core.audit_logs;"
```

If `COUNT(*)` returns without error, the migration applied.

---

## 3. Producing the compliance report (offline scan)

The compliance scanner is an **offline, source-code (SAST-style) audit tool**. It runs on a
machine that has the **source code available**; it is **not** executed by the server, and the
production service has **no access** to the source repository. Run it as part of each audit
cycle (e.g., before handing evidence to your auditor).

**Prerequisites**

- A checkout of the source at the exact commit/version being audited — record the hash first:
  ```bash
  git -C <repo-root> log -1 --format='%H'
  ```
- `rg` (ripgrep) for the `.sh` scanner: `sudo apt install ripgrep` (Debian/Ubuntu),
  `brew install ripgrep` (macOS). No ripgrep? Use the PowerShell scanner (below).

**Step 1 — run the scan from the repo root**

```bash
cd <repo-root>                    # the directory containing scripts/ and src/
bash scripts/soc2-compliance-scan.sh --markdown
```

Writes `soc2-compliance-report-<timestamp>.md` next to the scanned directory (the repo root).
To save the report elsewhere:

```bash
SOC2_REPORT_DIR=~/soc2-evidence bash scripts/soc2-compliance-scan.sh --markdown
```

**Step 2 — what the report contains**

- Secrets/config findings (`appsettings*`, `*.env*`, Dockerfile, compose, workflows)
- Weak/hostile crypto (MD5, SHA1, DES, RC4, AesManaged, …) and TLS-validation bypass patterns
- Raw SQL with potential user input (`FromSqlRaw` / `ExecuteSqlRaw` / `SqlQueryRaw`)
- Open-redirect patterns (manual review), security TODO/FIXME markers, PII fields
- A **module coverage section** — all 15 modules and every client/UI/CLI project with file
  counts. `0 missing` = the whole tree was audited.

**Step 3 — triage every finding**

Every hit is `path:line:content` + a criterion tag. A hit is not automatically a violation —
record a disposition per finding (fixed / false positive / accepted risk) in your triage notes
(see §8).

**Step 4 — keep the evidence**

For the audit you keep: the timestamped report, your triage notes, and the remediation commits.
Store them outside the repo if you prefer (e.g. `~/soc2-evidence/`).

**Windows / no ripgrep**

`scripts/soc2-compliance-scan.ps1` uses `Select-String` and needs **no ripgrep**:

```powershell
pwsh scripts/soc2-compliance-scan.ps1 -Mode markdown -Target <repo-root>
```

**CI / scripting**

`--ci` writes `soc2-compliance-report.json` and exits `1` if any untriaged finding exists:

```bash
bash scripts/soc2-compliance-scan.sh --ci
```

> **Note:** the scanner is source-only SAST evidence. Runtime compliance (TLS, security headers,
> health, audit-log population) is covered by the deployed service's own controls and the
> checks in §6.

---

## 4. Producing the audit package (checklist)

Do this at each audit cycle (e.g., annually for Type II, or when the auditor asks):

- ☐ Run migrations and verify `AuditLog` is populated (`SELECT COUNT(*)`).
- ☐ Run the scanner (`--markdown`) and keep the timestamped report.
- ☐ Run dependency scans and keep the output:
  ```bash
  dotnet list package --vulnerable --include-transitive > deps-vulnerable.txt
  dotnet list package --deprecated > deps-deprecated.txt
  ```
- ☐ Run build + tests and keep the log:
  ```bash
  dotnet build DotNetCloud.CI.slnf -c Release > build.log
  dotnet test DotNetCloud.CI.slnf -c Release --no-build > test.log
  ```
- ☐ Confirm CI passes (`.github/workflows/`) and keep the run URLs.
- ☐ Confirm the last OpenIddict key rotation date (§5).
- ☐ Run a restore test and keep its record (§6).
- ☐ Export the audit log for the review period (query §7.1) — this is the core CC4 evidence.
- ☐ Triage every scanner finding and record dispositions in the report (§8).
- ☐ Hand the auditor: the scanner report, dependency/build/test logs, CI run URLs, audit-log export, restore-test record, `SOC2_CONTROL_MATRIX.md`, and `SOC2_TYPE_II_AUDITOR_REPORT.md`.

---

## 5. OpenIddict key rotation

Keys rotate **automatically every 90 days** (`OidcKeyRotationService`, config `Auth:KeyRotation`);
old keys are retained 120 days so existing tokens stay valid during the grace period.

To rotate **immediately** (scheduled or suspected compromise), either:

1. **Admin UI:** open **System Settings** (`/admin/settings`) → **Rotate OIDC Keys**. This backs
   up `oidc-keys/`, generates fresh signing + encryption keys, sets the
   `core.OidcKeysPendingRestart` flag, and records the rotation in the audit trail
   (`oidc-key-rotation`). A **"restart to activate keys" banner** then appears:
   - **Activate now** — gracefully restarts the server (~3 s) via `POST /api/v1/core/admin/restart`;
     systemd (`Restart=always`) brings it back up and the banner clears on startup.
   - **Dismiss** — hides the banner until the next page load; it persists until restart.
2. **Script:**
   ```bash
   bash scripts/rotate-oidc-keys.sh
   ```
   The script backs up `oidc-keys/`, generates a new signing key, and verifies tokens still validate.

**Record the date + backup path** in the audit evidence (the auditor asks for rotation history).
New keys take effect on the next server restart (`sudo systemctl restart dotnetcloud`).

> **Important for existing installs:** the **Activate now** button needs the systemd unit set to
> `Restart=always` (new installs get this automatically). If your unit still uses
> `Restart=on-failure`, a graceful stop would leave the service **down** — update it first:
> `sudo systemctl edit --full dotnetcloud` → set `Restart=always` → `sudo systemctl daemon-reload`,
> or restart manually via the script.

Emergency rotation (suspected key compromise) uses either path immediately and notes "emergency" in the record.

---

## 6. Backup & restore test

SOC 2 Availability criteria require evidence that backups **work**, not just that they run.

1. Confirm `BackupHostedService` is enabled and its schedule is correct.
2. Restore the latest backup to a scratch database:
   ```bash
   # SQL Server example — restore to a scratch DB, never over production
   /opt/mssql-tools18/bin/sqlcmd -S hyperdrive.kimball.home -U dotnetcloud -C \
     -Q "RESTORE DATABASE DotNetCloud_RestoreTest FROM DISK='...' WITH REPLACE;"
   ```
3. Verify: row counts on key tables, and `SELECT COUNT(*) FROM [core].[AuditLogs]` returns the expected number.
4. Record date, backup file, restored counts, and result in the audit evidence.

---

## 7. Audit log, retention & privacy operations

### 7.1 Reading/exporting the audit trail

The audit trail is the `AuditLog` table plus the Serilog audit file. For an auditor, export a review-period slice:

```sql
-- Example: last 365 days
SELECT Id, TimestampUtc, CallerType, CallerUserId, ModuleId, Action, EntityType, EntityId, Description
FROM [core].[AuditLogs]
WHERE TimestampUtc >= DATEADD(day, -365, SYSUTCDATETIME())
ORDER BY TimestampUtc DESC;
```

The file mirror is at the path configured in `Serilog:AuditFilePath` (default `logs/audit-sync-.log`).

### 7.2 Retention settings

Set these via config or the admin API (they are `SystemSetting` keys):

- `core.AuditLogRetentionDays` — default `365`. Lower it to reduce disk usage; raise it if your auditor requires longer retention.
- `core.TrashRetentionDays` — default `30`. How long soft-deleted user data is kept before purge.

The daily `AuditLogPurgeHostedService` enforces `AuditLogRetentionDays`. The purge deletes rows **older than** the window and logs how many were removed (the purge action itself is audited).

### 7.3 Disk usage

The audit table grows with usage. Estimate and monitor:

- `SELECT COUNT(*), MIN(TimestampUtc), MAX(TimestampUtc) FROM [core].[AuditLogs];`
- Watch `logs/audit-*.log` file size; rotate/archive per your log policy.

### 7.4 Data-subject requests (privacy)

- **Export:** use the data-subject export endpoint (per module) to produce a user's data bundle. The export action is audited.
- **Delete:** use the data-subject delete endpoint. It soft-deletes per the retention window, then the purge permanently removes it.
- See `docs/security/PII_INVENTORY.md` for what is collected and how long it is kept.

---

## 8. Triage: what to do with scanner findings

| Finding type                                            | Typical disposition                                                   |
| ------------------------------------------------------- | --------------------------------------------------------------------- |
| `Password=`/`ApiKey=` in `appsettings.Development.json` | Acceptable (dev only) — note "dev default, prod uses env vars"        |
| `ConnectionString` in `appsettings.json`                | Verify it is a dev default; prod must use `DOTNETCLOUD_DB_CONNECTION` |
| `AllowInsecureTls` / TLS bypass                         | Verify it is env-gated (off in production); note the gate             |
| `unsafe-inline`/`unsafe-eval` in CSP                    | Accepted risk (Blazor WASM requirement) — documented deviation        |
| `X-Content-Type-Options` removed for video              | Accepted risk (codec probing) — documented deviation                  |
| Suppressed NuGet advisories                             | Verify the dated compensating control + review date still hold        |
| TODO/FIXME mentioning security                          | Schedule or close it; record the disposition                          |

Record each disposition (accepted risk / fixed / scheduled) next to the finding in the scanner report. An **untriaged** finding is what fails `--ci`.

---

## 9. Quick reference (commands)

```bash
# Scanner + evidence
bash scripts/soc2-compliance-scan.sh --markdown
bash scripts/soc2-compliance-scan.sh --ci
dotnet list package --vulnerable --include-transitive
dotnet list package --deprecated
dotnet build DotNetCloud.CI.slnf -c Release
dotnet test DotNetCloud.CI.slnf -c Release --no-build

# Database
dotnet ef database update --project src/Core/DotNetCloud.Core.Data --context CoreDbContext

# Keys
bash scripts/rotate-oidc-keys.sh
```

**When in doubt, prefer `rg` over `grep` for any text search** (this project's convention).
