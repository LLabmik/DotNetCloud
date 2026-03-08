# Client/Server Mediation Handoff

Last updated: 2026-03-08

Purpose: Shared handoff between client-side and server-side agents, mediated by user.

> Archived context (22 resolved issues from initial sync milestone) moved to
> [CLIENT_SERVER_MEDIATION_ARCHIVE.md](CLIENT_SERVER_MEDIATION_ARCHIVE.md).
> Full git history in commits up to `8e02b52`.

## Process Rules

- All technical findings and debugging conclusions go in this document, pushed to `main`.
- Mediator role is relay-only — commit notifications and cross-agent request forwarding.

## Current Status

**Completed milestone:** End-to-end file sync with directory hierarchy (Issues #1–#22, all resolved).

Open issue: Sync Improvement Batch 1 Task 1.3 (server-side rate limiting) — Tasks 1.1 and 1.2 complete on both sides.

## Environment

| | Machine | Detail |
|---|---------|--------|
| Server | `mint22` | `https://mint22:15443/` |
| Client | `Windows11-TestDNC` | Sync dir: `C:\Users\benk\Documents\synctray` |

## Key Architecture Decisions (Carry Forward)

- **Auth:** OpenIddict bearer on all files/sync endpoints via `FilesControllerBase` `[Authorize]`. Persistent RSA keys in `{DOTNETCLOUD_DATA_DIR}/oidc-keys/`. `DisableAccessTokenEncryption()`.
- **API contract:** All endpoints use `GetAuthenticatedCaller()` (no `userId` query param). All return raw payloads — `ResponseEnvelopeMiddleware` wraps automatically. Client unwraps envelope via `ReadEnvelopeDataAsync<T>()`.
- **Sync flow:** changes → tree → reconcile → chunk manifest → chunk download → file assembly. `since` param converted to UTC kind. Client builds `nodeId→path` map from folder tree.
- **Token handling:** Client uses `DateTimeOffset` for expiry. `RefreshTokenAsync` sends `client_id`. `OAuthConstants.ClientId = "dotnetcloud-desktop"`.

## Relay Template

```markdown
### Send to [Server|Client] Agent
<message text>

### Request Back
- commit hash
- raw endpoint/URL used
- raw error/query params
- raw log lines around the event (with timestamp)
```

## Active Handoff

### Issue #23: Batch 1 Task 1.1 - Sync Service Logging (Client only)

**Server-side status:** Not applicable (client-only task).
**Client-side status:** ✅ VALIDATED on `Windows11-TestDNC` at commit `c69aeac` (2026-03-08).

**Validation results from Windows11-TestDNC:**
- Commit: `c69aeac`
- Restore/build: no errors
- Log file: `%APPDATA%\DotNetCloud\logs\sync-service20260308.log` (3258 bytes, date suffix normal for `RollingInterval.Day`)
- JSON entries confirmed:
	- `DotNetCloud Sync Service starting.`
	- `Loading 1 persisted sync context(s).`
	- `Sync engine started for context ... (C:\Users\benk\Documents\synctray)`
	- `DotNetCloud Sync Service running — 1 context(s) active.`
	- `IPC server started (Named Pipe).`
	- Full graceful shutdown sequence logged
- **Task 1.1 (client): PASS**

---

### Issue #24: Batch 1 Task 1.1b - Sync Audit Logging (Server only)

**Server-side status:** ✅ COMPLETE — commit `c585dae` (2026-03-08).
**Client-side status:** Not applicable (server-only task).

---

### Issue #25: Batch 1 Task 1.2 - Request Correlation IDs (Client side)

**Server-side status:** ✅ COMPLETE — commit `16dd7df` (2026-03-08).
**Client-side status:** ✅ COMPLETE — commit `97afdd8` (2026-03-08).

**What was implemented:**
- `src/Clients/DotNetCloud.Client.Core/Api/CorrelationIdHandler.cs` — `DelegatingHandler` that attaches `X-Request-ID: <guid>` and logs every outgoing call + failures
- Registered on typed `DotNetCloudApiClient` HttpClient (via `ClientCoreServiceExtensions`)
- Registered on named `"DotNetCloudSync"` HttpClient (via `SyncServiceExtensions`)
- Build: 0 errors
- `sync-now` IPC accepted (`"success":true`)

**Task 1.2: PASS (both sides complete)**

---

### Issue #26: Batch 1 Task 1.3 - Server-Side Rate Limiting

**Server-side status:** Pending implementation on `mint22`.
**Client-side status:** Verify 429 + `Retry-After` handling; add log line on rate-limit events (client already has `Retry-After` parsing in `SendWithRetryAsync`).

**What needs to happen on mint22:**
- Configure `AddRateLimiter()` with sliding-window policies keyed on authenticated user ID:

  | Endpoint | Limit | Window |
  |----------|-------|--------|
  | `/api/v1/sync/changes` | 60 req | 1 min |
  | `/api/v1/sync/tree` | 10 req | 1 min |
  | `/api/v1/sync/reconcile` | 30 req | 1 min |
  | `/api/v1/files/upload/initiate` | 30 req | 1 min |
  | `/api/v1/files/upload/*/chunks/*` | 300 req | 1 min |
  | `/api/v1/files/*/download` | 120 req | 1 min |
  | `/api/v1/files/chunks/*` | 300 req | 1 min |

- Return `429 Too Many Requests` with `Retry-After` header
- Configurable limits via `appsettings.json`

**Request back from server agent:**
- commit hash
- `appsettings.json` rate-limit config section sample
- confirmation `429` is returned with `Retry-After` on a rapid-fire test
