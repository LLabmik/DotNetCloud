# SOC 2 Type II — Draft Auditor Report (Template)

**Status:** ☐ draft — pending independent CPA attestation
**Review period:** PLACEHOLDER — e.g. 2026-09-01 through 2027-08-31
**System:** DotNetCloud self-hosted cloud platform
**Prepared by:** engineering (technical controls + evidence)
**Companion docs:** `docs/security/SOC2_CONTROL_MATRIX.md`, `docs/security/SOC2_AUDITOR_GUIDE.md`, `docs/admin/SOC2_COMPLIANCE_ADMIN_GUIDE.md`, `docs/security/PII_INVENTORY.md`, `docs/security/SECURITY_MODEL.md`.

> This document is a **draft** the operator hands to a CPA firm. The firm finalizes scope,
> executes the test procedures, and issues its own opinion. This template supplies the
> management assertion, system description, control matrix link, evidence index, deviations,
> and CUEC list. It does **not** constitute an attestation.

---

## 1. Management assertion

DotNetCloud engineering asserts that, for the review period indicated above, the system described
below maintained, in all material respects, effective controls over the security, availability,
processing integrity, confidentiality, and privacy of personal data, based on the control
objectives and criteria in the AICPA Trust Services Criteria, **subject to the exceptions and
complementary user-entity controls listed in this report**.

Scope boundary: the DotNetCloud self-hosted platform (core server, 15 process-isolated module
hosts, web UI, desktop/mobile clients, CLI) **as operated by the customer on their own
infrastructure**. The operator is responsible for the underlying network, physical security,
and host OS controls (CUEC).

---

## 2. System description

The system is a self-hosted cloud platform (files, chat, calendar, contacts, notes, email,
bookmarks, music, photos, tracks, video, AI) built on .NET 10. Modules run as process-isolated
processes communicating exclusively over gRPC (Unix sockets / Named Pipes). The core server acts
as supervisor and owns identity (OpenIddict + PKCE), MFA (TOTP), authorization (capability
tiers), and the persisted audit trail.

Key components:

- **Identity & access:** OpenIddict OAuth2/OIDC with PKCE, TOTP MFA + backup codes, account
  lockout, 12-char/3-of-4 password policy, session-timeout setting, capability-tier authorization.
- **Audit trail:** every security-relevant operation (login, MFA change, admin action, module
  create/update/delete/share/export/import) is written to `[core].[AuditLogs]` with caller
  attribution and retained per `core.AuditLogRetentionDays` (default 365), purged daily.
- **Encryption:** TLS in transit; AES-256-GCM for email credentials and client token stores;
  PBKDF2 password hashing; data-protection keys shared across processes.
- **Change management:** CI runs build + tests + dependency audit; `TreatWarningsAsErrors`;
  code review required.
- **Availability:** health endpoints `/health/live` and `/health/ready`, scheduled encrypted
  backups, resource monitoring.

See `docs/security/SECURITY_MODEL.md` for the full system description and
`docs/architecture/ARCHITECTURE.md` for the technical architecture.

---

## 3. Control matrix

The full criterion → control → implementation → test → evidence mapping is in
`docs/security/SOC2_CONTROL_MATRIX.md`. It covers CC1–CC9, A1–A3, PI1, C1–C2, and P1–P8.

---

## 4. Test results & evidence index

Evidence is regenerable from the repository. The auditor reproduces it per
`docs/security/SOC2_AUDITOR_GUIDE.md` §3.

| Evidence artifact      | How produced                                                             | Location                                |
| ---------------------- | ------------------------------------------------------------------------ | --------------------------------------- |
| Compliance scan report | `bash scripts/soc2-compliance-scan.sh --markdown`                        | `soc2-compliance-report-<timestamp>.md` |
| Dependency scan        | `dotnet list package --vulnerable --include-transitive` / `--deprecated` | operator log                            |
| Build + test logs      | `dotnet build DotNetCloud.CI.slnf -c Release`; `dotnet test ...`         | CI run URLs                             |
| Audit trail            | `SELECT * FROM [core].[AuditLogs] WHERE TimestampUtc >= ...`             | exported for review period              |
| Backup/restore record  | admin guide §6 restore test                                              | operator log                            |
| Key-rotation record    | `scripts/rotate-oidc-keys.sh` + automatic `OidcKeyRotationService`       | key dir + operator log                  |
| Migration history      | `src/Core/DotNetCloud.Core.Data/Migrations` (+ `SqlServer/`)             | Git history                             |

**Test results:** PLACEHOLDER — the CPA firm records the outcome of each test procedure
here, referencing the control matrix row.

---

## 5. Exceptions & deviations (accepted risks)

These are documented design decisions. The auditor reviews whether each remains acceptable.

1. **Blazor CSP:** `unsafe-inline` / `unsafe-eval` / `wasm-unsafe-eval` are required by
   Blazor WebAssembly. Deviation from a strict CSP; accepted with documented rationale.
2. **Video `nosniff`:** `X-Content-Type-Options: nosniff` is removed for the video streaming
   endpoint to permit codec probing. Accepted; only affects media streams.
3. **Suppressed NuGet advisories:** AngleSharp mXSS (`GHSA-pgww-w46g-26qg`) and
   Microsoft.OpenApi (`GHSA-v5pm-xwqc-g5wc`) are suppressed with **dated** compensating
   controls in `Directory.Build.props` (review date 2026-11-18).
4. **Rate limiting** is permissive (self-hosted trust model).
5. **Centralized DSAR controller** (aggregated export/delete) is not yet implemented;
   DSARs are fulfilled via per-module export/delete flows (documented in the PII inventory).

---

## 6. Complementary user-entity controls (CUEC)

The operator must perform these; they are outside the code:

- Enable and require MFA, especially for administrators.
- Apply OS/package/security updates promptly.
- Run backups on schedule and perform periodic restore tests.
- Rotate OpenIddict signing keys (automatic every 90 days; run
  `scripts/rotate-oidc-keys.sh` for emergency rotation).
- Monitor logs, health endpoints, and alerts.
- Configure retention windows appropriate to regulatory obligations.
- Review scanner findings and remediate on schedule.
- Maintain the privacy notice with legal counsel.

---

## 7. CPA-firm note

This report is a **draft template**. It is not an attestation and has not been reviewed by an
independent CPA firm. To issue a SOC 2 Type II report, the firm must: select applicable
criteria, confirm the system boundary and review period, execute the test procedures in
`docs/security/SOC2_AUDITOR_GUIDE.md`, collect operating-effectiveness evidence over the review
period, evaluate the deviations in §5, confirm CUEC performance, and issue its own opinion.
