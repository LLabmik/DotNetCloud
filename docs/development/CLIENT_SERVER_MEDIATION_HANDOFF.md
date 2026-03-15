# Client/Server Mediation Handoff

Last updated: 2026-03-15 (Linux re-test run on fresh client binaries: echo suppression split by duplicate local contexts; client context cleanup now required)

Purpose: shared handoff between client-side and server-side agents, mediated by user.

Archived context:
- Historical completed updates are in `CLIENT_SERVER_MEDIATION_ARCHIVE.md`.
- Additional history remains available in git.

## Process Rules

**Agent autonomy (CRITICAL):**
- Both client and server agents work autonomously — they do NOT ask the moderator for context or permission.
- Agents pull the latest `main`, read the **Active Handoff** section, and execute the work described there independently.
- All actionable items, blockers, and technical details go directly in this document (committed to `main`).
- No moderator involvement in technical decisions, code reviews, or work coordination.

**Handoff management:**
- Put all technical findings, debugging conclusions, and next-step details in this document.
- Assistant (current agent) commits their findings/work and updates the **Active Handoff** section with actionable next steps for the other client.
- Assistant pushes commits to `main`.
- Unexpected untracked content rule (MANDATORY): remove unexpected untracked files/directories before commit; only keep intentional tracked changes for the handoff update.
- Handoff readiness gate (MANDATORY): all executable tests must pass before marking a handoff as ready.
- Environment-gated tests are allowed to be skipped, but must be explicitly identified as gated with the required environment/runtime prerequisites documented in the handoff.
- Runtime verification gate (MANDATORY): before declaring a server-side blocker fixed, verify the running service is on current binaries (not stale publish output) and document the verification command/output in handoff notes.
- OAuth contract check (MANDATORY when auth is involved): verify `client_id`, `redirect_uri`, and requested scopes exactly match server-registered OpenIddict client permissions before requesting cross-machine retries.
- Secret handling rule (MANDATORY): never commit raw bearer tokens/refresh tokens; share token acquisition steps and sanitized outputs only.
- Moderator relays a short "check for updates" message to the other machine.
- Moderator handoff prompt rule (MANDATORY): every ready-to-relay message must explicitly state the target machine name (for example: `mint22`, `mint-dnc-client`, `Windows11-TestDNC`).
- Other agent pulls latest, reads the handoff, and takes action without asking questions.

**Document maintenance:**
- Pre-commit archive rule (MANDATORY): before committing this file, move all completed/older handoff tasks to `CLIENT_SERVER_MEDIATION_ARCHIVE.md`.
- Keep only the single current task in **Active Handoff** (one active block only).
- If a task is completed, archive it first, then replace **Active Handoff** with the next task.

## Moderator Communication (Minimal)

**Moderator relays ONLY ONE OF THESE messages — nothing more:**

- `New handoff update for <target-machine>. Pull main and resume from 'Active Handoff' section.`
- `<Commit hash> — New handoff update for <target-machine>. Pull and check docs/development/CLIENT_SERVER_MEDIATION_HANDOFF.md Active Handoff.`

**No moderator task:** Moderator provides zero context, zero explanation. The handoff document has everything the receiving agent needs.

## Current Status

- All prior Phase 2, chat, and pre-Linux sync remediation work is complete and archived.
- P0 server-side sync hardening deployed and verified on `mint22`.
- Client-side upload dedup + echo suppression (commit `4c575cc`) verified on all machines:
  - **Linux (`mint-dnc-client`):** runtime verified — single initiate per event, no conflict copies.
  - **Windows (`Windows11-TestDNC`):** runtime verified on MSIX `0.23.3.0` — single initiate per event, no conflict copies.
  - **Server (`mint22`):** zero 5xx errors since deployment; only normal token-refresh 401s observed.
- **Upload hardening story: CLOSED.** Full chain verification complete across all three machines.
- Server-side P1 echo suppression/device-identity fix and `SyncDeviceIdentity` DB migration are now applied on `mint22`.
- **Windows (`Windows11-TestDNC`) re-verification PASSED** on 2026-03-15: uploaded file completed, immediate follow-up pass showed `RemoteChanges=1, LocalApplied=0`, no download path was entered for the uploaded node, and the next scheduled pass was clean.
- **Linux (`mint-dnc-client`) parity re-verification FAILED** on 2026-03-15: uploaded verification node was downloaded on follow-up pass (`RemoteChanges=1, LocalApplied=1`), so parity with Windows behavior is not achieved.
- **Next active cycle:** client-side duplicate-context cleanup on `mint-dnc-client`, then a single-context Linux re-test.

## Environment

| Role | Machine | Detail |
|---|---|---|
| Server | `mint22` | `https://mint22:15443/` |
| Client | `Windows11-TestDNC` | Sync dir: `C:\Users\benk\Documents\synctray` |
| Client | `mint-dnc-client` | Linux Mint 22 validation host for desktop sync client implementation + E2E testing |

## Key Carry-Forward Contracts

- Auth: OpenIddict bearer on files/sync endpoints via `FilesControllerBase` `[Authorize]`.
- API envelope: middleware wraps responses; clients should unwrap via envelope helpers.
- Sync flow: changes -> tree -> reconcile -> chunk manifest -> chunk download -> file assembly.
- Desktop OAuth constant: `OAuthConstants.ClientId = "dotnetcloud-desktop"`.

## Active Handoff

### Duplicate Sync Context Cleanup — Execute on `mint-dnc-client`

**Date:** 2026-03-15
**Target machine:** `mint-dnc-client` ONLY (server work is done — `mint22` has nothing to do)
**Status:** EXECUTE NOW

#### Background (Do Not Re-Investigate — Just Execute Steps Below)

Echo suppression works per-context, but the Linux client has **two sync contexts** pointing at the same folder/server/user. Context A suppresses the echo correctly, but Context B doesn't know about the upload and re-downloads the file. Fix: delete one context, keep one, re-test.

#### Step-by-Step Instructions (Execute ALL)

**Step 1: Stop any running sync service**

```bash
# Kill any running sync service process
pkill -f dotnetcloud-sync-service 2>/dev/null || true
pkill -f DotNetCloud.Client.SyncService 2>/dev/null || true
```

**Step 2: Find and inspect the context registry file**

The file is at one of these paths (check in order):
```bash
cat /var/lib/dotnetcloud/sync/contexts.json 2>/dev/null || \
cat ~/.local/share/DotNetCloud/Sync/contexts.json
```

You will see TWO context entries with the same `ServerBaseUrl`, `UserId`, and `LocalFolderPath`:
- Context `e7ba5002-dc72-4c97-a511-17f194ca79c5`
- Context `cb22726a-cdef-4cc8-a29c-755b22f1c899`

**Step 3: Edit the contexts.json file — remove one duplicate**

Keep context `cb22726a-cdef-4cc8-a29c-755b22f1c899` (the one that successfully suppressed echo).
Delete context `e7ba5002-dc72-4c97-a511-17f194ca79c5` from the JSON array.

Make sure the resulting JSON is valid (no trailing commas, proper brackets).

**Step 4: Also delete the data directory for the removed context**

```bash
# Whichever data root holds contexts.json, the context data dir is a subdirectory named by the context ID (no dashes):
rm -rf ~/.local/share/DotNetCloud/Sync/e7ba5002dc724697a51117f194ca79c5 2>/dev/null
rm -rf /var/lib/dotnetcloud/sync/e7ba5002dc724697a51117f194ca79c5 2>/dev/null
```

**Step 5: Start the sync service**

```bash
cd ~/Repos/dotnetcloud
dotnet run --project src/Clients/DotNetCloud.Client.SyncService/DotNetCloud.Client.SyncService.csproj
```

Wait for it to log `Loading 1 persisted sync context(s)` (not 2).

**Step 6: Create a verification upload file**

```bash
echo "echo-verify-single-context-$(date +%Y%m%d-%H%M%S)" > ~/synctray/echo-verify-single-context-$(date +%Y%m%d-%H%M%S).txt
```

**Step 7: Watch the sync service logs for follow-up pass results**

After the file uploads, wait for the next sync pass (30-60 seconds). Look for:
- `RemoteChanges=1, LocalApplied=0` → **PASS** (echo was suppressed)
- `RemoteChanges=1, LocalApplied=1` or `File download starting` → **FAIL**

**Step 8: Update this handoff and push**

Replace this Active Handoff section with results:
- Remaining context ID
- Whether `Loading 1 persisted sync context(s)` appeared
- Follow-up pass log line showing `RemoteChanges` / `LocalApplied` values
- Whether `File download starting` appeared for the uploaded node
- PASS or FAIL verdict

Then commit, push, and provide the relay message for `mint22`.

## Relay Template

```markdown
### Send to [Server|Client] Agent on <target-machine>
<message text including target machine>

### Request Back
- commit hash
- raw endpoint/URL used
- raw error/query params
- raw log lines around the event (with timestamp)
```
