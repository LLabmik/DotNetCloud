# SOC 2 — Auditor Guide (Technical Controls & Evidence)

**Audience:** independent auditors (CPA firm / third party) reviewing the DotNetCloud codebase for SOC 2 readiness.
**Companion docs:** `docs/SOC2_TYPE_II_COMPLIANCE_PLAN.md` (what was implemented), `docs/security/SOC2_CONTROL_MATRIX.md` (criterion → control mapping), `docs/security/SOC2_TYPE_II_AUDITOR_REPORT.md` (draft report), `docs/admin/SOC2_COMPLIANCE_ADMIN_GUIDE.md` (operator procedures).

---

## 1. What this codebase provides (and does not)

This repository implements and documents the **technical controls** that map to the SOC 2 Trust Services Criteria (TSC): Security (CC1–CC9), Availability (A1–A3), Processing Integrity (PI1), Confidentiality (C1–C2), and Privacy (P1–P8).

It provides:

- Source code for the controls.
- A reproducible, `rg`-based compliance scanner and its reports.
- A control matrix mapping each criterion to implementation + evidence.
- A draft Type II report template (for you to finalize).
- Admin runbooks for backup/restore, key rotation, and audit-package generation.

It does **not** provide:

- An attestation, opinion, or signature — that is yours.
- The organizational/process evidence (CC1–CC3, parts of CC9) beyond in-repo policy documents. Verify the operator's real-world processes separately.
- A populated operating-effectiveness period: Type II requires evidence over a review period (e.g., 3–12 months). The evidence (audit logs, CI runs, backup/restore records) begins accumulating once the controls ship; confirm the period with the operator.

---

## 2. How to read the control matrix

`docs/security/SOC2_CONTROL_MATRIX.md` is a table with one row per criterion. Columns:

1. **Criterion** — TSC ID (e.g., CC4, A2, P6).
2. **Control** — what the system does (designed control).
3. **Implementation** — file paths/symbols where the control lives.
4. **Test procedure** — how to verify the control operates.
5. **Evidence** — the artifact that proves it.

Use it as your index. For each criterion you select as applicable, open the referenced files and follow the test procedure.

---

## 3. Independently reproducing the evidence

All evidence is regenerable from this repository. You are not required to trust the operator's summary — reproduce it:

```bash
# 1. Compliance scan (secret detection, weak crypto, SQL, PII, module coverage)
bash scripts/soc2-compliance-scan.sh --markdown
bash scripts/soc2-compliance-scan.sh --ci

# 2. Build + tests (change management / SDLC evidence)
dotnet build DotNetCloud.CI.slnf -c Release
dotnet test DotNetCloud.CI.slnf -c Release --no-build

# 3. Dependency vulnerability/deprecation scan
dotnet list package --vulnerable --include-transitive
dotnet list package --deprecated

# 4. Enumerate the audited inventory (must be 15 modules + clients/UI/CLI)
rg --files -g 'manifest.json' src/Modules
rg --files -g '*.csproj' src/Modules src/Core src/Clients src/UI src/CLI
```

Compare your outputs to the artifacts the operator provided. Discrepancies are a finding.

---

## 4. Test procedures by criterion group

### CC4 — Monitoring activities (audit trail)

- **Control:** every security-relevant operation is written to the `AuditLog` table (and a Serilog file mirror).
- **Verify:**
  1. Query the production database: `SELECT COUNT(*) FROM [core].[AuditLogs];` (SQL Server) or `core.audit_logs` (PostgreSQL). Non-zero and growing is expected.
  2. Confirm attribution fields exist: `CallerType`, `CallerUserId`, `ModuleId`, `Action`, `EntityType`, `EntityId`, `TimestampUtc`.
  3. Confirm the write path is centralized: search the source for the gRPC `LogAudit` rpc and the `IAuditLogger` implementations (`rg "IAuditLogger|LogAudit" src`).
  4. Confirm sensitive data is masked: inspect `RequestResponseLoggingMiddleware` and the `SafeStringDestructuringPolicy`; confirm no password/token values appear in log output.
- **Evidence:** audit-log query export, scanner report, source references.

### CC6 — Logical access

- **Verify:** OpenIddict + PKCE enforcement (`RequirePkce = true`), capability-tier authorization, TOTP MFA (`MfaService`, `Otp.NET`), account lockout, 12/3-of-4 password policy, session-timeout setting, TLS validation (no unconditional `DangerousAcceptAnyServerCertificateValidator`).
- **Evidence:** source references in `src/Core/DotNetCloud.Core.Auth`, `src/Core/DotNetCloud.Core.Server/Controllers`, and the scanner's weak-crypto/TLS findings (expect only env-gated hits).

### CC7 / CC8 — System operations & change management

- **Verify:** CI workflows run build + tests (` .github/workflows/`); dependency scans are clean or have dated compensating controls (`Directory.Build.props` `NuGetAuditSuppress` comments); code review/branch protection is enforced in the VCS (confirm with the operator).
- **Evidence:** CI run URLs, `dotnet list package` output, commit history.

### A1–A3 — Availability

- **Verify:** `BackupHostedService` runs; a restore test was executed and recorded (backup file, restored row counts); health endpoints `/health/live` and `/health/ready` are monitored.
- **Evidence:** restore-test record in the admin guide, health-endpoint monitoring config.

### C1–C2 — Confidentiality

- **Verify:** encryption at rest for sensitive fields (`EmailCredentialEncryptionService`, `EncryptedFileTokenStore`, data-protection keys), TLS everywhere, key-management/rotation documented (`rotate-oidc-keys.sh`), disposal via retention purge.
- **Evidence:** source references, key-rotation log, retention settings.

### PI1 — Processing integrity

- **Verify:** input validation (`ModelState.IsValid`, `IFileValidationService` extension whitelist + magic bytes), parameterized queries (no raw SQL with user input), `HtmlSanitizer` on rendered HTML.
- **Evidence:** scanner findings (#6, #8, #11), source references.

### P1–P8 — Privacy

- **Verify:** `docs/security/PII_INVENTORY.md` lists PII fields with retention/disposal; data-subject export/delete endpoints exist and are audited; retention settings (`AuditLogRetentionDays`, `TrashRetentionDays`) are configured.
- **Evidence:** PII inventory, retention config, data-subject request logs in the audit trail.

---

## 5. Evidence index (where artifacts live)

| Artifact                   | Location / how produced                                      |
| -------------------------- | ------------------------------------------------------------ |
| Compliance scan report     | `soc2-compliance-report-<timestamp>.md` (run §3.1)           |
| Control matrix             | `docs/security/SOC2_CONTROL_MATRIX.md`                       |
| Draft Type II report       | `docs/security/SOC2_TYPE_II_AUDITOR_REPORT.md`               |
| Security model / hardening | `docs/security/SECURITY_MODEL.md`, `DEPLOYMENT_HARDENING.md` |
| PII inventory              | `docs/security/PII_INVENTORY.md`                             |
| Build/test logs            | `dotnet build` / `dotnet test` output                        |
| Dependency scans           | `dotnet list package --vulnerable/--deprecated` output       |
| Audit trail                | `[core].[AuditLogs]` table + `logs/audit-*.log`              |
| Backup/restore record      | operator-run restore test (§6 of admin guide)                |
| Key rotation record        | operator-run `rotate-oidc-keys.sh` log                       |

---

## 6. Known accepted risks / deviations

These are documented design decisions to review, not necessarily findings:

- Blazor WebAssembly requires CSP `unsafe-inline`/`unsafe-eval`/`wasm-unsafe-eval`.
- Video streaming removes `X-Content-Type-Options: nosniff` for codec probing.
- Two NuGet advisories are suppressed with dated compensating controls (AngleSharp mXSS, Microsoft.OpenApi) pending upstream releases.
- Rate limiting is permissive (self-hosted trust model).

Confirm each is still valid at the time of your review.

---

## 7. Complementary user-entity controls (operator responsibilities)

The following are performed by the operator and are outside the code. Verify them with the operator:

- Enabling and requiring MFA for all users, especially administrators.
- Applying OS/package/security updates.
- Running backups and periodic restore tests.
- Rotating OpenIddict signing keys on schedule.
- Monitoring logs, health endpoints, and alerts.
- Configuring retention windows appropriate to their regulatory obligations.
- Reviewing scanner findings and remediating on schedule.

---

## 8. Finalizing the report

Use `docs/security/SOC2_TYPE_II_AUDITOR_REPORT.md` as the template. Replace the review-period placeholder with the actual period, attach your reproduced evidence, confirm the deviations, and apply your firm's opinion. This repository supplies the technical controls and evidence; the opinion, scope selection, and CUEC assessment remain with the auditor.
