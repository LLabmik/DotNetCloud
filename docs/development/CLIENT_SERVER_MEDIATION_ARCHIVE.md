# Client/Server Mediation — Archived Context

Archived: 2026-03-08. Full git history preserved in commits up to `8e02b52`.

This file contains historical reference from the client/server mediation sessions.
Only consult this if you encounter a regression or need to understand a past fix.

## Resolved Issues (Issues #1–#22, 2026-03-07 to 2026-03-08)

| # | Issue | Root Cause | Fix | Side |
|---|-------|-----------|-----|------|
| 1 | `invalid_client` on authorize | `dotnetcloud-desktop` not registered | Added OIDC client seeder with upsert | Server |
| 2 | `invalid_scope` on authorize | `files:read`/`files:write` not registered | Added scope registration + client permissions | Server |
| 3 | `404` on `GET /connect/authorize` | Only `POST` mapped | Changed to `GET`+`POST` mapping | Server |
| 4 | Login redirect to wrong path | `/login` instead of `/auth/login` | Corrected redirect path | Server |
| 5 | Placeholder JSON on authenticated authorize | Not calling OpenIddict `SignIn` | Reworked passthrough to issue `SignIn` | Server |
| 6 | TLS errors on token exchange | Self-signed cert not trusted by client | Client-side bypass for local/LAN hosts | Client |
| 7 | Token JSON field mapping | Snake_case `access_token` etc. not mapped | Client DTO mapping + typed HttpClient | Client |
| 8 | `UserId = Guid.Empty` | Access tokens encrypted (JWE); no OIDC claims; no userinfo endpoint | `DisableAccessTokenEncryption()`, DB claim lookup, userinfo registration | Server |
| 9 | Sync endpoints `404` | `SyncController` in `Files.Host` (not loaded) | Added `SyncController` to `Core.Server` | Server |
| 10 | TLS errors on sync API calls | `DotNetCloudSync` named HttpClient had no cert bypass | Added cert bypass to named client registration | Client |
| 11 | Sync calls required `userId` query param | Server controller bound `userId` from query string | Derived `CallerContext` from bearer claims; removed `userId` param | Server |
| 12 | Sync response deserialization mismatch | Server returned envelope-wrapped sync payloads | Changed sync responses from `Ok(Envelope(...))` to `Ok(...)` | Server |
| 13 | Token refresh was a stub | `RefreshAccessTokenAsync` did nothing | Implemented actual refresh: API call → save tokens → update accessor | Client |
| 14 | Missing `client_id` in refresh request | OpenIddict requires `client_id` for public clients | Added `clientId` parameter to `RefreshTokenAsync` | Client |
| 15 | `DateTime` serialization bug — tokens appear unexpired | `DateTimeKind` lost after JSON roundtrip | Changed `ExpiresAt` from `DateTime` to `DateTimeOffset` across client chain | Client |
| 16 | Refresh token `invalid_grant` | Ephemeral RSA keys regenerated on every restart | Created `OidcKeyManager` for persistent PEM key files; fixed config key names | Server |
| 17 | Sync API returns 403 with valid bearer token | No `[Authorize]` attribute; default auth scheme was cookies | Added OpenIddict bearer `[Authorize]` to `FilesControllerBase` | Server |
| 18 | Files API returns 403 "Caller user ID does not match" | 20 `FilesController` endpoints used `[FromQuery] Guid userId` | Changed all to `GetAuthenticatedCaller()`; removed `userId` param | Server |
| 19 | Files API responses double-envelope wrapped | `Ok(Envelope(data))` + `ResponseEnvelopeMiddleware` | Removed `Envelope()` calls; middleware handles wrapping | Server |
| 20 | Sync changes endpoint returns 500 | `since` parsed as `DateTime Kind=Unspecified`; Npgsql rejects for `timestamptz` | `DateTime.SpecifyKind(since, DateTimeKind.Utc)`; added general exception handler | Server |
| 21 | Chunk manifest deserialization failure | Server returns `string[]`; client expected object with `Chunks`+`TotalSize` | Client deserializes `List<string>` and maps to `ChunkManifestResponse` | Client |
| 22 | Sync flattens directory structure | `ResolveLocalPathAsync` used filename only, ignoring `ParentId` | Client fetches folder tree, builds `nodeId→path` map, creates dirs before files | Client |

## Verified State at Milestone Completion

### Server (mint22, commit `69dd5eb`)
- Build: 0 errors, 0 warnings
- Tests: 304 server + 85 auth + 513 files = 902 passed
- Health: `https://localhost:15443/health/live` → Healthy

### Client (Windows11-TestDNC, commit `6a9ccb0`)
- Build: 0 errors, 0 warnings
- Tests: 53 Core + 24 SyncService + 24 SyncTray = 101 passed
- Sync: 7 files into correct subdirectories with full directory hierarchy

### End-to-End Flow Verified
OAuth login → token exchange → sync changes → tree → reconcile → chunk manifest → chunk download → file assembly. 7 files synced into `clients/`, `Finance/`, `Pictures/`, `Test/`, and root.

---

## Resolved Issues (Issues #23–#42, 2026-03-08 to 2026-03-09)

Archived: 2026-03-09. Full git history preserved in commits `c69aeac` through `c70bd47`.

| # | Issue | Commit(s) | Side |
|---|-------|-----------|------|
| 23 | Batch 1 Task 1.1 — Sync Service Logging | `c69aeac` | Client |
| 24 | Batch 1 Task 1.1b — Audit Logging | `c585dae` | Server |
| 25 | Batch 1 Task 1.2 — Request Correlation IDs | `97afdd8` | Client |
| 26 | Batch 1 Task 1.3 — Rate Limiting | `4570c16` | Server |
| 27 | Batch 1 Task 1.4 — Chunk Integrity (SHA-256) | Windows 2026-03-08 | Client |
| 28 | Batch 1 Tasks 1.5/1.6/1.7 — Retry / WAL / Upload Queue | `1aa6b18` | Client |
| 29 | Batch 1 Tasks 1.8/1.9 — Temp-file atomicity / Malware scanning stub | `82ca53b` | Client |
| 30 | Batch 2 Task 2.1 — CDC chunking (FastCDC) | `3a7e0ae` / `bc9e08a` | Both |
| 31 | Batch 2 Task 2.2 — Streaming upload/download pipeline | `7cbc12e` | Both |
| 32 | Batch 2 Task 2.3 — Brotli compression for chunk transfers | `032f6a2` | Both |
| 33 | Batch 3 Task 3.1 — `.syncignore` pattern matching | `a9c6812` | Client |
| 34 | Batch 3 Task 3.2 — Persistent upload sessions (crash recovery) | `4243328` | Client |
| 35 | Batch 3 Task 3.3 — Locked file handling (VSS on Windows) | `b971551` | Client |
| 36 | Batch 3 Task 3.4 — Per-file transfer progress in Tray UI | `7f93226` | Client |
| 37 | Batch 3 Task 3.5 — Conflict resolution UI (DiffPlex 5-strategy) | `8508afc` | Client |
| 38 | Batch 2 Tasks 2.4+2.5 — Server-issued sync cursor + paginated changes | `c81495d` / `1a9c4c6` | Both |
| 39 | Batch 2 Task 2.6 — ETag / chunk-download file-system cache | `c81495d` / `1a9c4c6` | Both |
| 40 | Batch 3 Task 3.6 — Idempotent upload operations (hash pre-check) | `3504932` | Client |
| 41 | Batch 4 Task 4.1 — Case-sensitivity conflict detection (NAME_CONFLICT) | `3504932` | Client |
| 42 | Batch 4 Task 4.2 — POSIX permission metadata sync | `c70bd47` | Both |
| 43 | Batch 4 Task 4.3 — Symbolic link policy | Server `d3a6422`, Client `1cd594a` | Both |
| 44 | Batch 4 Task 4.4 — inotify/inode health monitoring | Server `d3a6422`, Client `1cd594a` | Both |
| 45 | Batch 4 Task 4.5 — Path length/filename compatibility validation | Server `d3a6422`, Client `1cd594a` | Both |

### Verified State at Batch 1–4.5 Completion

**Server (mint22, commit `d3a6422`):** Build 0 errors, all tests pass.  
**Client (Windows11-TestDNC, commit `1cd594a`):** Build 0 errors, 123/123 tests pass.
