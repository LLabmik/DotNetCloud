# SOC 2 Type II Compliance — Implementation Plan (All Non-Test Code)

**Status:** ☐ not started
**Owner:** engineering
**Audience:** implementation agents (this document is written to be executed by a junior/lesser LLM — follow it literally, in order)
**Last updated:** 2026-08-18

---

## 1. Objective

Make the DotNetCloud codebase SOC 2 Type II **ready** by:

1. Building a criteria-mapped `rg`-based compliance scanner that inspects **every module and every client**.
2. Implementing the missing technical controls (persisted audit trail, retention, upload validation, dependency remediations, key rotation, backup/DR, privacy tooling, client hardening).
3. Producing a control matrix, evidence bundle, and a **draft auditor report** that a CPA firm can review.

**Non-goals (out of scope):** CPA engagement/signature, actual Type II report issuance, network/physical security, penetration-test execution, third-party service security, legal advice.

---

## 2. Trust Services Criteria → code controls

SOC 2 has five categories. The code implements the technical controls; governance-only criteria are satisfied with in-repo policy/runbook documents. Map every check and every fix to a criterion ID below.

| Criterion group                                        | What the code must show                                                         | Where                          |
| ------------------------------------------------------ | ------------------------------------------------------------------------------- | ------------------------------ |
| **CC1–CC3** (control environment, communication, risk) | Policies/standards documented                                                   | `docs/security/` + this plan   |
| **CC4** (monitoring activities)                        | Persistent, attributable audit trail + log review                               | Workstream B                   |
| **CC5** (control activities)                           | SDLC gates: CI build/test/security scan                                         | `.github/workflows/`           |
| **CC6** (logical/physical access)                      | AuthN/Z, MFA, lockout, session timeout, credential protection                   | existing + Workstreams D, F, K |
| **CC7** (system operations)                            | Vuln management, change mgmt, backup/DR                                         | Workstreams E, G               |
| **CC8** (change management)                            | Authorized, tested changes (CI gates, tests)                                    | Workstream L                   |
| **CC9** (risk mitigation)                              | Vendor/third-party inventory, BCP docs                                          | Workstream L (docs)            |
| **A1–A3** (availability)                               | Capacity, backup, restore-test, monitoring/alerting                             | Workstream G                   |
| **PI1** (processing integrity)                         | Input validation, completeness, accuracy                                        | Workstream D                   |
| **C1–C2** (confidentiality)                            | Encryption at rest/in transit, key mgmt, disposal                               | Workstreams F, H, B(retention) |
| **P1–P8** (privacy)                                    | PII inventory, notice, choice, access, retention, disposal, data-subject rights | Workstream J                   |

---

## 3. Authoritative inventory (what MUST be inspected)

Run these to enumerate (do not hand-curate — the scanner repeats these):

```bash
rg --files -g 'manifest.json' src/Modules
rg --files -g '*.csproj' src/Modules
rg --files -g '*.csproj' src/Core src/Clients src/UI src/CLI
```

### 3.1 Modules — 15 total (cross-check against `RequiredModules.ModuleIds` in `src/Core/DotNetCloud.Core/Modules/RequiredModules.cs`)

Required (share `core` schema): `files`, `chat`, `search`, `contacts`, `calendar`, `notes`, `about`.
Optional (dedicated schema): `ai`, `bookmarks`, `email`, `example`, `music`, `photos`, `tracks`, `video`.

Per-module layout (verified for Files/Chat/Video/AI):

```
src/Modules/<Module>/
  DotNetCloud.Modules.<X>/            # core library (models, services, IModuleManifest)
  DotNetCloud.Modules.<X>.Host/       # gRPC host (Program.cs, Controllers, Protos)
  DotNetCloud.Modules.<X>.Data/       # EF DbContext + PostgreSQL migrations
  DotNetCloud.Modules.<X>.Client/     # gRPC client (Files, Search, etc.)
  DotNetCloud.Modules.<X>.Data.SqlServer/  # SQL Server migrations (where present)
  manifest.json                       # module identity anchor
```

### 3.2 Clients, UI, CLI, ops (all non-test code)

- `src/Clients/DotNetCloud.Client.Core` — shared client (token store encryption).
- `src/Clients/DotNetCloud.Client.SyncTray` — Avalonia desktop.
- `src/Clients/DotNetCloud.Client.Android` — MAUI Android (static scan only; not buildable on Linux).
- `src/Clients/DotNetCloud.Client.Updater` — auto-updater.
- `src/Clients/DotNetCloud.Client.BrowserExtension` — JS/TS extension.
- `src/UI/DotNetCloud.UI.Web`, `.Web.Client`, `.Shared`, `.Android` — Blazor.
- `src/CLI/DotNetCloud.CLI` — CLI.
- `tools/`, `scripts/`, `deploy/`, `Dockerfile`, `docker-compose.yml`, `.github/workflows/`.

### 3.3 Exclusions (never scan)

`tests/`, `bin/`, `obj/`, root `modules/` (deploy artifacts), `wwwroot/lib/`, `node_modules/`, `.git/`, `*.deps.json`, `*.min.js`, `*.min.css`, `*.map`, `*Designer.cs`, `soc2-compliance-report-*`, `sixlabors.lic`.

---

## 4. Baseline state (already present — do not re-implement)

Verified in this repo on 2026-08-18:

- OpenIddict + PKCE (`AuthServiceExtensions.cs`), capability tiers, `CallerContext` propagation.
- TOTP MFA + backup codes (`MfaService`, `MfaController`, `UserBackupCode`), account lockout, 12/3-of-4 password policy, session-timeout setting (`SystemSettingKeys`).
- AES-256-GCM (`EncryptedFileTokenStore`, `EmailCredentialEncryptionService`), PBKDF2 hashing, WOPI HMAC-SHA256 timing-safe.
- Serilog masking + audit sink option `SerilogOptions.AuditFilePath` (`src/Core/DotNetCloud.Core.ServiceDefaults/Logging/SerilogConfiguration.cs`).
- `BackupHostedService`, health endpoints, systemd hardening, CI dependency scanning, .NET analyzers (`TreatWarningsAsErrors`).

### Known gaps (fix these)

1. **No persisted audit trail** — `IAuditLogger` is interface-only (`src/Core/DotNetCloud.Core/Capabilities/IAuditLogger.cs`). No entity, no `DbSet`, no DI registration, no call sites. (CC4, P7) — Workstream B.
2. **Scanner noise** — `scripts/soc2-compliance-scan.sh` flags `*.deps.json` versions as IPs and minified JS as TODO; only 4 shallow checks. — Workstream A.
3. **Two suppressed NuGet advisories** — AngleSharp mXSS CVE-2026-54570; Microsoft.OpenApi GHSA-v5pm-xwqc-g5wc (`Directory.Build.props` `NuGetAuditSuppress`). (CC7) — Workstream E.
4. **Upload validation** — `IFileValidationService` (`src/Core/DotNetCloud.Core/Security/FileValidationService.cs`) is registered but only wired on `UserManagementController`; the other endpoints are not. (PI1/CC6) — Workstream D.
5. **No automated OpenIddict key rotation** (`oidc-keys/`). (CC6/C1) — Workstream F.
6. **No retention/disposal policy or purge automation**. (C2, P6) — Workstream C.
7. **No SOC 2 control matrix / evidence mapping**. — Workstream L.
8. **No privacy tooling** (PII inventory, data-subject export/delete, consent). — Workstream J.

---

## 5. Workstreams (implement in order)

### Workstream A — Upgrade the compliance scanner

**Files:**

- Modify `scripts/soc2-compliance-scan.sh` (rewrite in place; keep the filename).

**Steps:**

1. Replace the `EXCLUDE` array with the exclusion globs from §3.3. Note the root deploy-artifact directory is excluded with `-g '!modules/**'` — this does NOT exclude `src/Modules/**` (different path prefix). Add `-g '!src/Core/**/bin/**'` etc. are already covered by `!**/bin/**`.
2. Fix the IPv4 false-positive filter: after the IPv4 `rg` pass, pipe through `rg -v 'Version=|AssemblyVersion|FileVersion|InformationalVersion|PackageVersion|PublicKeyToken|Culture='` **and** additionally drop any line whose path matches `*.deps.json` by scanning with `-g '!*.deps.json'` up front (simplest, since those files are deploy artifacts).
3. Add a `-g '!*.min.js'` exclusion before the TODO/FIXME scan so minified JS is never flagged.
4. Add the criteria-tagged checks below. Each check function emits `path:line:content` + a criterion tag, and writes a per-file count.
5. Add a **module-coverage section**: `rg --files -g 'manifest.json' src/Modules` and `rg --files -g '*.csproj' src/Modules src/Clients src/UI src/CLI src/Core`, print every module ID, and assert all 15 module IDs + the client/UI/CLI projects are present with ≥1 scanned `.cs`/`.csproj` file. Exit non-zero if any is missing.
6. Add `--ci` mode: emit JSON to `soc2-compliance-report.json` and exit `1` if any untriaged finding exists; otherwise exit `0`.
7. Add `--markdown` (default) emitting `soc2-compliance-report-<timestamp>.md` instead of `.txt`.

**Checks (tag each with criterion):**

| #   | Check (rg pattern, case-insensitive)          | Criterion                                 |
| --- | --------------------------------------------- | ----------------------------------------- | --------------------------- | ------------------------------------ | ---------- | ---------------- | ------------------------------------------------------------------------------------------------------------------- | -------- | --------- | --- | --------------------------------------------- | --- |
| 1   | `Password                                     | ApiKey                                    | ApiSecret                   | ClientSecret                         | ClientId   | ConnectionString | Bearer [A-Za-z0-9]`in`appsettings*.json`, `*.env*`, `Dockerfile*`, `docker-compose\*.yml`, `.github/workflows/\*\*` | CC6/CC7  |
| 2   | `-----BEGIN (RSA                              | EC                                        | OPENSSH )?PRIVATE KEY-----` | C1                                   |
| 3   | `MD5                                          | SHA1                                      | DES                         | RC4                                  | AesManaged | RijndaelManaged  | TripleDES` (exclude test paths)                                                                                     | C1       |
| 4   | `DangerousAcceptAnyServerCertificateValidator | ServerCertificateCustomValidationCallback | AllowInsecureTls`           | C1                                   |
| 5   | `ChannelCredentials.Insecure`                 | C1                                        |
| 6   | `FromSqlRaw                                   | ExecuteSqlRaw                             | SqlQueryRaw`                | PI1                                  |
| 7   | `AllowedHosts` in `appsettings*.json`         | CC6                                       |
| 8   | `Redirect\(                                   | RedirectToAction                          | RedirectToPage              | Url.IsLocalUrl` (manual review list) | CC6        |
| 9   | `(TODO                                        | FIXME)[^\r\n]\*(security                  | auth                        | encrypt                              | password   | secret           | token)`in code globs (exclude`\*.min.js`)                                                                           | CC7      |
| 10  | PII field names: `Email                       | Phone                                     | Address                     | BirthDate                            | Ssn        | SocialSecurity   | Passport                                                                                                            | Latitude | Longitude | Gps | Geolocation`in`Entities/`, `DTOs/`, `Models/` | P6  |
| 11  | `IFormFile                                    | FromForm                                  | RequestSizeLimit`           | PI1                                  |
| 12  | `RetentionDays                                | Purge                                     | RetainedFileCountLimit      | AuditFilePath`                       | C2/P6      |

**Acceptance:** re-run `bash scripts/soc2-compliance-scan.sh --markdown`; the report contains zero hits of the known false-positive classes (`.deps.json`, `*.min.js`); the module-coverage section lists all 15 modules + all clients/UI/CLI with non-zero counts; `--ci` exit code is 0 after triage.

**Execution model (2026-08-19):** the scanner is an **offline audit tool** run by an administrator
on a machine with the source code available (SAST-style). It is **not** executed by the
production server, and the server has no access to the source repository. The initial
server-side "Run Compliance Scan" admin page/coordinator was reverted; the admin workflow is
documented in `docs/admin/SOC2_COMPLIANCE_ADMIN_GUIDE.md` §3.

---

### Workstream B — Persisted audit trail (CC4, P7) — highest priority

Architecture: Core.Server hosts a `CoreCapabilities` gRPC `LogAudit` rpc (see `src/Core/DotNetCloud.Core.Grpc/Protos/module_capabilities.proto`). Modules call it through a gRPC-backed `IAuditLogger` client (same pattern as `AddTokenIntrospection` in `src/Core/DotNetCloud.Core.Auth/Introspection/IntrospectionServiceCollectionExtensions.cs`). Core.Server persists to a new `AuditLog` table in `CoreDbContext` and mirrors to the Serilog audit sink.

#### B1 — Data layer

**New file:** `src/Core/DotNetCloud.Core.Data/Entities/Audit/AuditLog.cs`

Entity fields (mirror `SystemSetting` style — XML docs required):

| Property       | CLR type   | Constraints                                                                |
| -------------- | ---------- | -------------------------------------------------------------------------- |
| `Id`           | `Guid`     | PK; default `Guid.CreateVersion7()`                                        |
| `TimestampUtc` | `DateTime` | required; indexed                                                          |
| `CallerType`   | `string`   | required; max 20 (`User`/`System`/`Module`)                                |
| `CallerUserId` | `Guid?`    | null for system callers                                                    |
| `CallerRoles`  | `string?`  | JSON array of roles, max 2000                                              |
| `ModuleId`     | `string`   | required; max 100 (module where action occurred, e.g. `dotnetcloud.files`) |
| `Action`       | `int`      | required; `AuditAction` enum value (`Create`=0 … `Import`=8)               |
| `EntityType`   | `string`   | required; max 100                                                          |
| `EntityId`     | `Guid`     | required                                                                   |
| `Description`  | `string?`  | max 2000                                                                   |

**New file:** `src/Core/DotNetCloud.Core.Data/Configuration/Audit/AuditLogConfiguration.cs`

Mirror `SystemSettingConfiguration` exactly (`HasKey(Id)`, `IsRequired`, `HasMaxLength`, `HasColumnName` snake_case column names, `ToTable("AuditLogs")`). Add indexes:

- `IX_audit_logs_timestamp_utc` on `TimestampUtc`
- `IX_audit_logs_module_timestamp` on `(ModuleId, TimestampUtc)`
- `IX_audit_logs_entity` on `(EntityType, EntityId)`
- `IX_audit_logs_caller_user` on `CallerUserId`

**Modify:** `src/Core/DotNetCloud.Core.Data/Context/CoreDbContext.cs`

1. Add `public DbSet<AuditLog> AuditLogs => Set<AuditLog>();` (next to `SystemSettings` at ~line 152).
2. In `OnModelCreating`, add `modelBuilder.ApplyConfiguration(new AuditLogConfiguration());` next to the `SystemSettingConfiguration` call (~line 340). The existing naming-strategy pass (~lines 465–470) will map the table into the `core` schema for both providers automatically.

**Migrations (both providers — mandatory):**

```bash
# PostgreSQL (dev/local)
dotnet ef migrations add AddAuditLog \
  --project src/Core/DotNetCloud.Core.Data --context CoreDbContext --output-dir Migrations

# SQL Server (production)
dotnet ef migrations add AddAuditLog_SqlServer \
  --project src/Core/DotNetCloud.Core.Data --context CoreDbContext --output-dir Migrations/SqlServer
```

Verify both `*AddAuditLog*` migration pairs and the `CoreDbContextModelSnapshot.cs` (in both `Migrations/` and `Migrations/SqlServer/`) were created.

#### B2 — Persistence service

**New file:** `src/Core/DotNetCloud.Core.Server/Services/AuditLogService.cs` (or `Audit/` subfolder)

- Implements `IAuditLogger` (`DotNetCloud.Core.Capabilities`).
- Constructor injects `CoreDbContext` and `ILogger<AuditLogService>`.
- `LogAsync(AuditEntry entry, ...)`: map `AuditEntry` → `AuditLog`, `Add`, `SaveChangesAsync` (no-tracking perf: use a scoped context). Also `_logger.LogInformation` with a `Audit` context property so the Serilog audit sink captures it.
- Keep it fast and non-blocking for callers: fire-and-forget with a bounded channel is acceptable, but the default must be write-through (do not silently drop; log failures).

**Register in `src/Core/DotNetCloud.Core.Server/Program.cs` (and `SupervisorServiceExtensions.cs` if DI is centralized there):**

```csharp
builder.Services.AddScoped<IAuditLogger, AuditLogService>();
```

(Use `AddScoped` because it depends on `CoreDbContext`.)

#### B3 — gRPC surface

**Modify:** `src/Core/DotNetCloud.Core.Grpc/Protos/module_capabilities.proto`

Add to the `service CoreCapabilities` block:

```proto
  // Records an audit trail entry (IAuditLogger capability).
  rpc LogAudit (LogAuditRequest) returns (LogAuditResponse);
```

Add messages (reuse the existing `CallerContextMessage`):

```proto
message LogAuditRequest {
  CallerContextMessage caller = 1;   // required; attributes the action
  string module_id = 2;              // module where the action occurred
  int32 action = 3;                  // DotNetCloud.Core.Capabilities.AuditAction enum value
  string entity_type = 4;
  string entity_id = 5;              // GUID string
  string description = 6;
}

message LogAuditResponse {
  bool success = 1;
}
```

**Modify:** `src/Core/DotNetCloud.Core.Server/Grpc/Services/GrpcHealthServiceImpl.cs` (the file containing `CoreCapabilitiesServiceImpl`)

Add the `LogAudit` handler: resolve `IAuditLogger` from the scoped provider, reconstruct `AuditEntry` from the request (map `CallerContextMessage` → `CallerContext` using the same mapping the other rpcs use), and `LogAsync`.

#### B4 — Module-side client

**New file:** `src/Core/DotNetCloud.Core.Grpc/Clients/AuditLoggerGrpcClient.cs` (or under an existing client namespace)

- Implements `IAuditLogger`; wraps `CoreCapabilities.CoreCapabilitiesClient` over a channel built from `DOTNETCLOUD_GRPC_ENDPOINT` (mirror `TokenIntrospectionClient` in `src/Core/DotNetCloud.Core.Auth/Introspection/TokenIntrospectionClient.cs`).
- Add extension `AddAuditLogger(this IServiceCollection)` that registers `IAuditLogger` → `AuditLoggerGrpcClient` (follow `IntrospectionServiceCollectionExtensions`).

**Modify:** every module Host `Program.cs` (15 files) — add `builder.Services.AddAuditLogger();` (or the equivalent registration) next to `AddTokenIntrospection()`.

#### B5 — Instrumentation (call sites)

Minimum required call sites (use `IAuditLogger`; `CallerContext` comes from `ICurrentUserContext` / existing request context):

- **Core.Server auth (direct `AuditLogService`, not gRPC):**
  - `AuthController.LoginAsync` — `AuditAction.Read` on `User` for success; log `Description="login-failed"` on failure (never log the password).
  - `AuthSessionController` — password change (`Update`), logout (`Read`).
  - `MfaController` — TOTP setup (`Update`), verify (`Update`), backup-code regenerate (`Update`).
  - `UserManagementController` — admin user create/update/delete (`Create`/`Update`/`Delete`), avatar upload (`Update`).
  - `AdminController` — any admin-privileged mutation (`Update`/`Delete`).
- **Every module:** at least the create/update/delete/share/unshare/export/import operations in its controllers/services, using the module's `IAuditLogger` gRPC client (which routes to Core). Do NOT skip the `example` module — instrument it as the copy-paste template.

**Acceptance:**

- `AuditLog` table exists in both providers (migration applied).
- `rg "IAuditLogger" src` shows: interface, `AuditLogService`, `AuditLoggerGrpcClient`, 15 module registrations, and instrumentation call sites.
- `rg "LogAsync" src/Modules` returns hits in ≥14 of 15 modules (all but possibly a no-op module; `about` must at least log module lifecycle).

---

### Workstream C — Retention & disposal (C2, P6)

1. **Config:** add a `Retention` section (or `SystemSetting` keys `core.AuditLogRetentionDays`, `core.TrashRetentionDays`) with sensible defaults (e.g., audit 365 days, trash 30 days). Read via `IConfiguration`/`SystemSetting`.
2. **New file:** `src/Core/DotNetCloud.Core.Server/Services/AuditLogPurgeHostedService.cs` — `BackgroundService` that runs daily, deletes `AuditLog` rows older than retention, and logs how many it purged (so the purge itself is auditable).
3. **Extend** soft-delete purge: verify/implement a purge for soft-deleted user data (files, contacts, notes, calendar, email) after `TrashRetentionDays`.
4. **Doc:** record the disposal procedure in `docs/security/SECURITY_MODEL.md` (or the auditor guide).

**Acceptance:** `rg "RetentionDays|Purge" src` shows the config + hosted service; daily purge is registered in `Program.cs`.

---

### Workstream D — Upload validation (PI1/CC6)

1. Re-inventory upload entry points: `rg "IFormFile|FromForm|RequestSizeLimit|multipart" src` (excluding tests).
2. For every endpoint NOT already using `IFileValidationService`, wire it: inject `IFileValidationService`, define an `AllowedFileTypes.FileTypeDefinition[]` per endpoint (extension whitelist + magic bytes), call `Validate(...)` before persisting, and return 400 on failure.
3. Ensure size limits (`RequestSizeLimit`) exist on every upload endpoint.
4. Confirm `IFileValidationService` is registered wherever the endpoint's host runs (add to any module Host `Program.cs` that lacks it — mirror `UserManagementController`/Core.Server registration).

**Acceptance:** `rg "IFileValidationService" src` shows the interface, the service, and usage in every upload endpoint host.

---

### Workstream E — Dependency remediations (CC7)

1. Run `dotnet list package --vulnerable --include-transitive` and `--deprecated` for the whole solution (use `DotNetCloud.CI.slnf`).
2. Triage the two suppressions in `Directory.Build.props`:
   - AngleSharp mXSS (CVE-2026-54570): blocked on `AngleSharp.Css`/`HtmlSanitizer` releasing 1.x-compatible versions. If still blocked, keep the suppression BUT add a dated comment + a documented compensating control (input sanitization review) and a review date.
   - Microsoft.OpenApi (GHSA-v5pm-xwqc-g5wc): blocked on `AspNetCore.OpenApi` shipping a compatible OpenApi 2.x. Same treatment.
3. Upgrade any package that has an available fix.

**Acceptance:** `dotnet list package --vulnerable` clean or every remaining advisory has a dated, documented compensating control.

---

### Workstream F — OpenIddict key rotation (CC6/C1)

1. Implement **or** document a rotation procedure. Prefer documenting + scripting first (P3): add `scripts/rotate-oidc-keys.sh` that backs up `oidc-keys/`, generates a new RSA signing key via OpenIddict APIs or `dotnet` tooling, and verifies tokens still validate.
2. Document the rotation interval (e.g., 90 days) and emergency-rotation runbook in `docs/security/DEPLOYMENT_HARDENING.md`.

**Acceptance:** `rg "rotate|rotation" docs/security scripts` shows the procedure + script; evidence of the last rotation is logged.

---

### Workstream G — Availability & backup/DR (A1–A3)

1. Verify `BackupHostedService` (`src/Core/DotNetCloud.Core.Server/Services/BackupHostedService.cs`) runs and what it captures.
2. Write a restore-test runbook: `docs/admin/SOC2_COMPLIANCE_ADMIN_GUIDE.md` §Backup & Restore (backup → restore to scratch → verify → record).
3. Document RTO/RPO targets and monitoring/alerting (health endpoints `/health/live`, `/health/ready` + uptime monitor) in the admin guide.

**Acceptance:** admin guide has a complete, executable restore-test procedure; health endpoints are documented as the availability signal.

---

### Workstream H — Confidentiality (C1)

1. Audit TLS: `rg "DangerousAcceptAnyServerCertificateValidator|AllowInsecureTls|ChannelCredentials.Insecure" src` — confirm every hit is gated by environment/config (already mostly done; re-verify).
2. Verify encryption-at-rest for sensitive fields: `EmailCredentialEncryptionService`, `EncryptedFileTokenStore`, data-protection keys, client caches (SyncTray, Android). Document in the control matrix.
3. Document key-management (generation, storage, rotation) in `docs/security/SECURITY_MODEL.md`.

---

### Workstream I — Processing integrity (PI1)

1. Confirm input validation on all POST/PUT/PATCH (`ModelState.IsValid`), parameterized queries (no raw SQL with user input), and `HtmlSanitizer` on rendered user HTML.
2. Close any gaps found by the scanner checks #6, #8, #11.

---

### Workstream J — Privacy (P1–P8)

1. **PII inventory doc:** `docs/security/PII_INVENTORY.md` — one table row per PII-bearing entity/field (from scanner check #10), noting purpose, retention, encryption, and disposal.
2. **Data-subject rights:** verify/add endpoints for export and delete of a user's data across modules. If absent, add a `DataSubjectController` (or per-module endpoints) supporting export + delete, audited via `IAuditLogger`.
3. **Consent/notice:** add a privacy-notice template with placeholders (operator fills legal text) — do not write legal advice.
4. **Retention:** link to Workstream C; the PII inventory must reference retention keys.

**Acceptance:** `PII_INVENTORY.md` exists and covers every PII field the scanner flags; data-subject export/delete endpoints exist and are audited.

---

### Workstream K — Client hardening (CC6/C1)

1. **SyncTray:** verify local sync cache + token storage are encrypted at rest; log redaction of PII.
2. **Android:** verify device keystore usage + data-at-rest + backup exclusion; static `rg` scan only (no Linux build).
3. **Client.Core:** verify `EncryptedFileTokenStore` key handling + TLS validation.
4. **Updater:** verify downloaded update signature/hash verification (no arbitrary URL trust).
5. **BrowserExtension:** verify no hardcoded secrets, least-privilege manifest permissions, CSP for extension pages.
6. **UI (4 projects) + CLI:** CSP/sanitizer/cookie flags; CLI never echoes secrets, log redaction, localhost-only health probe.

---

### Workstream L — Control matrix, evidence + auditor report

1. **`docs/security/SOC2_CONTROL_MATRIX.md`** — table: criterion ID → control → implementation file(s) → how to test → evidence artifact. Fill from the workstreams above.
2. **`docs/security/SOC2_AUDITOR_GUIDE.md`** — see that file (how an auditor reproduces evidence and reads the report).
3. **`docs/security/SOC2_TYPE_II_AUDITOR_REPORT.md`** — draft auditor-facing report:
   - Management assertion + scope (system boundary, review-period placeholder).
   - System description (reuse `SECURITY_MODEL.md`).
   - Control matrix (link to #1).
   - Test results + evidence index (scanner output, CI logs, migration history, backup logs, key-rotation evidence).
   - Exceptions/deviations (Blazor CSP `unsafe-*`, video `nosniff` removal, the two suppressed advisories).
   - Complementary user-entity controls (operator runs backups, enables MFA, monitors, applies updates).
   - CPA-firm note (draft pending independent attestation).
4. **`docs/admin/SOC2_COMPLIANCE_ADMIN_GUIDE.md`** — how the administrator uses the new code to produce the audit and manages the changes.

---

### Workstream M — Tracking docs (mandatory, targeted edits only)

After all work: update both with targeted edits (preserve git history; `✓`/`☐` only, never `[x]`/`[ ]`):

1. `docs/IMPLEMENTATION_CHECKLIST.md` — add/mark the SOC 2 section items `✓`.
2. `docs/MASTER_PROJECT_PLAN.md` — update Quick Status Summary table + add/update the SOC 2 step with `**Status:**`, `**Deliverables:**`, `**Notes:**`.

---

## 6. Per-module checklist (Phase 2 check → Phase 3 implement)

Apply the **common checks** to every module: secrets, weak crypto, raw SQL, upload validation (if it uploads), dependency vulns, `IAuditLogger` registered + instrumented, PII fields, log masking. Then the module-specific items:

| Module      | Module-specific check / implement                                                                                           |
| ----------- | --------------------------------------------------------------------------------------------------------------------------- |
| `files`     | Wire `IFileValidationService` on ALL upload endpoints; chunk-integrity; audit file CRUD/share/download/restore; trash purge |
| `chat`      | Attachment upload validation; WebRTC/STUN config; audit channel/DM/host-call ops; message retention                         |
| `search`    | FTS index permission scoping; PII in index; audit search ops; index retention/rebuild                                       |
| `contacts`  | vCard/CardDAV PII; avatar + attachment upload validation; audit CRUD/share/export/import                                    |
| `calendar`  | CalDAV event PII; recurrence; audit CRUD/share/export/import                                                                |
| `notes`     | Note PII; import path; audit CRUD/share                                                                                     |
| `about`     | Minimal — declare + register + instrument `IAuditLogger` (lifecycle events)                                                 |
| `ai`        | LLM PII sent to model; prompt/response logging redaction; API key handling; audit AI ops                                    |
| `bookmarks` | SSRF (`SafeUrlFetcher` private-IP block); import upload validation; metadata fetch; audit                                   |
| `email`     | Credential encryption; TLS gating; attachment upload validation; email PII; retention; audit                                |
| `example`   | Verify it models the correct manifest + audit pattern as the copy-paste template                                            |
| `music`     | Media/metadata; library scan/playback; audit library ops; retention                                                         |
| `photos`    | EXIF/GPS geolocation PII (`ExifMetadataExtractor`); upload validation; album share; audit                                   |
| `tracks`    | CSV import upload validation; dual migrations; audit work-item CRUD                                                         |
| `video`     | Media/stream/transcode; third-party `hls.min.js` (SBOM + pin); audit                                                        |

Clients/UI/CLI (each gets common checks + client-specific items from Workstream K).

---

## 7. Verification & Definition of Done

Run and record all of the following (all pass = done):

```bash
# 1. Build + test (CI solution filter; Android excluded on Linux)
dotnet build DotNetCloud.CI.slnf -c Release
dotnet test DotNetCloud.CI.slnf -c Release --no-build

# 2. Dependency scan
dotnet list package --vulnerable --include-transitive
dotnet list package --deprecated

# 3. Compliance scanner (module + client coverage, zero untriaged findings)
bash scripts/soc2-compliance-scan.sh --markdown
bash scripts/soc2-compliance-scan.sh --ci   # exit 0 expected

# 4. Module enumeration (manual sanity)
rg --files -g 'manifest.json' src/Modules | wc -l    # must be 15
rg --files -g '*.csproj' src/Modules | wc -l

# 5. Migrations exist for both providers
rg -l "AuditLog" src/Core/DotNetCloud.Core.Data/Migrations src/Core/DotNetCloud.Core.Data/Migrations/SqlServer
```

Definition of done:

- ☐ All workstreams A–L complete with acceptance criteria met.
- ☐ Scanner lists all 15 modules + all clients/UI/CLI with non-zero counts.
- ☐ `AuditLog` persists in both providers; instrumentation call sites present.
- ☐ Control matrix + auditor report + admin guide exist and are internally consistent.
- ☐ `docs/IMPLEMENTATION_CHECKLIST.md` and `docs/MASTER_PROJECT_PLAN.md` updated with targeted edits.
- ☐ Build + tests pass; no new analyzer warnings.

---

## 8. References for the implementer

- Capability contract: `src/Core/DotNetCloud.Core/Capabilities/IAuditLogger.cs` (AuditEntry, AuditAction).
- DbContext + config pattern: `src/Core/DotNetCloud.Core.Data/Context/CoreDbContext.cs`, `Configuration/Settings/SystemSettingConfiguration.cs`, `Entities/Settings/SystemSetting.cs`.
- Naming strategy: `src/Core/DotNetCloud.Core.Data/Naming/PostgreSqlNamingStrategy.cs`, `SqlServerNamingStrategy.cs`, `src/Core/DotNetCloud.Core/Modules/RequiredModules.cs`.
- gRPC capabilities: `src/Core/DotNetCloud.Core.Grpc/Protos/module_capabilities.proto`, `src/Core/DotNetCloud.Core.Server/Grpc/Services/GrpcHealthServiceImpl.cs` (contains `CoreCapabilitiesServiceImpl`).
- Module gRPC client pattern: `src/Core/DotNetCloud.Core.Auth/Introspection/TokenIntrospectionClient.cs` + `IntrospectionServiceCollectionExtensions.cs`.
- Module Host wiring example: `src/Modules/Files/DotNetCloud.Modules.Files.Host/Program.cs`.
- Existing scanner: `scripts/soc2-compliance-scan.sh`.
- Prior security review (context, do not duplicate): `docs/SECURITY_REVIEW_FINDINGS.md`, `docs/security/SECURITY_MODEL.md`, `docs/security/DEPLOYMENT_HARDENING.md`.
