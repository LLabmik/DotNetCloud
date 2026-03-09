# Client/Server Mediation Handoff

Last updated: 2026-03-09

Purpose: Shared handoff between client-side and server-side agents, mediated by user.

> Archived context (45 resolved issues — initial sync milestone through Batch 4.5) moved to
> [CLIENT_SERVER_MEDIATION_ARCHIVE.md](CLIENT_SERVER_MEDIATION_ARCHIVE.md).
> Full git history in commits up to `e871128`.

## Process Rules

- All technical findings and debugging conclusions go in this document, pushed to `main`.
- Mediator role is relay-only — commit notifications and cross-agent request forwarding.

## Current Status

**Issues #1–#42 fully resolved.** See [CLIENT_SERVER_MEDIATION_ARCHIVE.md](CLIENT_SERVER_MEDIATION_ARCHIVE.md) for details.

**Batch 4 — ALL ISSUES RESOLVED:**
- Issue #43 (Task 4.3): Symbolic link policy — server ✅ complete, client ✅ complete (commit `1cd594a`)
- Issue #44 (Task 4.4): inotify/inode health monitoring — server ✅ complete, client ✅ complete (commit `1cd594a`)
- Issue #45 (Task 4.5): Path length/filename validation — server ✅ complete, client ✅ complete (commit `1cd594a`)

**No pending issues. All Batch 4 items resolved.**

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

No pending issues. See [CLIENT_SERVER_MEDIATION_ARCHIVE.md](CLIENT_SERVER_MEDIATION_ARCHIVE.md) for all resolved issues (Batch 1 through Batch 4.5).
