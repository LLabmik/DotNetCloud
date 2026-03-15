# Sync Recovery Playbook

Purpose: Ensure DotNetCloud SyncService and SyncTray recover automatically from token expiry, rate limits, transient API failures, and conflict loops with zero technical action required from end users.

## 1) Product Requirement: Zero-Touch Recovery

End users should never need to open logs, run scripts, restart services manually, or understand OAuth internals.

Required user experience:

1. Sync recovers automatically from short-lived auth and network faults.
2. When recovery is impossible without user identity confirmation, the tray shows a single guided action (Sign in again) and handles everything else.
3. The tray always shows understandable state text (Syncing, Paused: Rate Limited, Sign-in Required, Healthy).
4. Background recovery and retries continue without requiring app restarts.

Support diagnostics are still useful internally, but they are not part of the user workflow.

## 2) Required Auto-Recovery Behaviors

These should be treated as required reliability behaviors.

### A. Token Expiry Recovery

1. Refresh proactively before expiry (for example, 5 minutes early).
2. Keep one refresh in-flight per context (single-flight lock).
3. On 401, force immediate refresh and replay the failed request once.
4. If refresh fails, mark context as ReauthRequired and notify tray clearly with one-click Sign in again.
5. Persist refresh failure reason and retry cadence so service restarts do not lose recovery state.

### B. 429 Rate Limit Recovery

1. Parse and honor Retry-After when present.
2. Use exponential backoff with jitter per context.
3. Pause aggressive sync-now loops while backoff window is active.
4. Surface a specific status: RateLimited (not generic Error).
5. Auto-resume when backoff expires; do not require user restart.

### C. Conflict and 409 Recovery

1. Treat 409 complete-upload as idempotent success only when server confirms same logical target.
2. Emit a conflict event with both local and remote hashes.
3. Keep deterministic conflict-copy naming and log correlation id.
4. When safe auto-resolution is possible, apply it silently and continue.
5. When user decision is required, show a simple guided choice in tray (keep local/keep server/review later).

### D. Service Lifetime Recovery

1. Ensure service restart policy is automatic on crash.
2. Persist pending operations before process exit.
3. Resume pending queue on start, with bounded replay and dedupe keys.
4. Publish startup self-check summary to logs (contexts loaded, token health, queue depth).
5. Add a lightweight watchdog (tray or service-side heartbeat) that restarts the sync worker when no progress is observed beyond threshold.

### E. Progress-Stall Recovery

1. Track per-context forward progress watermark (last successful upload/download timestamp).
2. If queued operations exist and no progress is made for N minutes, trigger a staged self-heal: reconnect IPC, refresh token, restart worker pipeline.
3. Keep cooldown windows to avoid restart loops.
4. Emit a single actionable tray message only if staged self-heal exhausts all attempts.

## 3) Observability and Alerting Baseline

Add these counters/gauges and alert on thresholds:

1. token_refresh_attempts_total
2. token_refresh_failures_total
3. sync_http_429_total
4. sync_http_401_total
5. sync_conflicts_total
6. sync_pending_ops_queue_depth
7. sync_context_state (Healthy, RateLimited, ReauthRequired, Error)

Suggested alerts:

1. 3+ token refresh failures in 10 minutes
2. 429 rate above threshold for 15 minutes
3. pending queue depth growing for 20+ minutes
4. context stuck in Error/RateLimited without recovery for 30+ minutes

## 4) User-Facing Recovery UX Contract

For non-technical users, recovery must be constrained to simple tray actions:

1. Default path is fully automatic and silent.
2. If identity is required, show one primary button: Sign in again.
3. Never ask users to edit files, clear state databases, or run terminal commands.
4. Provide plain-language status and expected next step, for example: "Session expired. Click Sign in again to resume syncing."
5. Auto-verify recovery after sign-in by running a lightweight connectivity and token check.

Internal/manual playbooks belong in support documentation, not user instructions.

## 5) Engineering Hardening Backlog (Priority)

P0:

1. Add per-context single-flight token refresh guard.
2. Implement Retry-After aware 429 policy with jitter and state transition.
3. Add explicit ReauthRequired state and tray banner.
4. Add stall watchdog with staged self-heal pipeline.
5. Add single-click interactive reauth flow from tray.

P1:

1. Add durable operation idempotency keys for upload/complete.
2. Improve 409 classification and conflict telemetry.
3. Add watchdog check that verifies service + IPC + at least one healthy context.
4. Add auto-resume/replay verification tests after forced service restart.

P2:

1. Add chaos test suite for refresh failure, 429 storm, and restart during upload.
2. Add automated regression scenario: token expiry during active upload.
3. Add synthetic "no-progress" test to validate watchdog recovery without user intervention.

## 6) Definition Of Done For Recovery

A recovery implementation is done when:

1. Expired token is refreshed automatically without operator action.
2. 429 episodes degrade throughput but do not deadlock sync.
3. Restart resumes pending work without duplicate corruption.
4. Conflict handling is explicit and visible in tray.
5. Metrics and alerts detect regressions before users report them.
6. A non-technical user can recover from token expiry using at most one tray click.
7. No documented user workflow depends on terminal commands.
