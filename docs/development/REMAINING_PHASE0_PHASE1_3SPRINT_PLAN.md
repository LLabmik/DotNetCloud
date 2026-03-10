# Remaining Phase 0/1 Work - 3 Sprint Execution Plan

**Created:** 2026-03-10  
**Status:** In progress  
**Scope:** Finish top-priority open work in `phase-1.19.2`, `phase-1.15` deferred items, and `phase-1.12` deferred items.

## Important Notes

- This plan follows the mediator workflow in `docs/development/CLIENT_SERVER_MEDIATION_HANDOFF.md`.
- For cross-side work, use relay format blocks (Send to Server/Client Agent + Request Back).
- **Cleanup reminder:** Delete this file after all work in this plan is accepted and reflected in `docs/MASTER_PROJECT_PLAN.md` and `docs/IMPLEMENTATION_CHECKLIST.md`.

## Sprint Sequence

1. Sprint A: `phase-1.19.2` integration depth and matrix coverage
2. Sprint B: `phase-1.15` security/reliability hardening
3. Sprint C: `phase-1.12` deferred UX/media completion

## Global Tracking

- ☐ Sprint A complete
- ☐ Sprint B complete
- ☐ Sprint C complete
- ☐ All related docs updated (`MASTER_PROJECT_PLAN.md`, `IMPLEMENTATION_CHECKLIST.md`, mediation handoff)
- ☐ Final acceptance received
- ☐ This temporary sprint plan deleted

---

## Sprint A - Phase 1.19.2 (Files API Integration Tests)

**Priority:** Highest  
**Primary owner:** Server  
**Secondary owner:** Client (contract compatibility validation)

### Objectives

- Expand integration coverage beyond isolation tests.
- Validate core Files workflows end-to-end.
- Add provider matrix confidence (PostgreSQL required, SQL Server where available).

### Deliverables

- ✓ CRUD/tree/search/favorites REST integration tests
- ✓ Chunked upload E2E integration tests (initiate/upload/complete/dedup/quota failure)
- ✓ Version/share/trash E2E integration tests
- ✓ WOPI + sync endpoint integration smoke tests (auth + payload shape)
- ✓ Multi-database run coverage documented (PostgreSQL + SQL Server where feasible)

### Mediator Handoff Checkpoints

- ✓ Server posts detailed test-gap inventory to handoff doc
- ☐ Server posts commit hash + exact test names + raw endpoint diffs
- ☐ Client validates no response-envelope/auth contract regressions
- ☐ Cross-agent sign-off recorded in handoff doc

### Exit Criteria

- ☐ `phase-1.19.2` marked completed
- ☐ Integration suite includes both security and broad functional E2E coverage
- ☐ Documentation updated with completion notes and counts

---

## Sprint B - Phase 1.15 Deferred Hardening (SyncService)

**Priority:** High  
**Primary owner:** Client  
**Secondary owner:** Server (identity/contract review)

### Objectives

- Harden service identity boundaries.
- Improve platform-specific privilege model.
- Close operational safety gaps.

### Deliverables

- ☐ Linux privilege dropping per context (`setresuid`/`setresgid` path)
- ☐ Windows impersonation per context (`WindowsIdentity.RunImpersonated` path)
- ☐ IPC caller identity verification wired to context identity model
- ☐ Sync trigger debounce/rate limiting behavior
- ☐ Disk-full detection and tray-surfaced notification path

### Mediator Handoff Checkpoints

- ☐ Server posts expected caller identity and failure semantics
- ☐ Client posts per-OS implementation proposal and assumptions
- ☐ Server reviews and signs off on identity/security behavior
- ☐ Client posts final evidence: commit hash, logs, and test results (Windows + Linux)

### Exit Criteria

- ☐ All deferred bullets under `phase-1.15` resolved
- ☐ Regression tests added for identity mismatch, privilege boundary, disk-full, debounce
- ☐ Documentation and handoff updated

---

## Sprint C - Phase 1.12 Deferred UX/Preview Items

**Priority:** Medium  
**Primary owner:** Client/UI  
**Secondary owner:** Server (thumbnail endpoint contract)

### Objectives

- Complete deferred upload and preview capabilities.
- Improve usability of drag/drop and media preview.

### Deliverables (in order)

- ✓ Folder drag-and-drop recursive upload
- ✓ Thumbnail API controller wiring for existing `IThumbnailService`
- ☐ Video thumbnail generation integration
- ☐ PDF thumbnail generation integration
- ☐ Touch gestures for preview (swipe/pinch)

### Mediator Handoff Checkpoints

- ☐ Server locks thumbnail endpoint contract first
- ☐ Client integrates browser + preview flows against contract
- ☐ Server validates caching/auth behavior for thumbnail endpoint
- ☐ Client posts UX validation evidence and fallback behavior notes

### Exit Criteria

- ☐ All deferred bullets under `phase-1.12` resolved
- ☐ Tests added for recursive drop parsing and thumbnail endpoint behavior
- ☐ Docs and handoff updated with completion evidence

---

## Per-Sprint Execution Template

Use this template at sprint kickoff and closeout.

### Kickoff

- ☐ Scope confirmed
- ☐ Owner split confirmed
- ☐ Handoff entry created
- ☐ Test strategy listed

### Closeout

- ☐ Build passes
- ☐ Relevant tests pass
- ☐ Handoff evidence added (commit hash, raw endpoint/URL, raw errors/query params, raw logs)
- ☐ `MASTER_PROJECT_PLAN.md` updated
- ☐ `IMPLEMENTATION_CHECKLIST.md` updated

## Risks and Dependencies

- ☐ SQL Server integration environment availability (for matrix runs)
- ☐ FFmpeg/PDF renderer dependency decisions for thumbnail generation
- ☐ Platform-specific privilege APIs require careful test isolation

## Final Completion Gate

- ☐ Sprint A, B, and C all complete
- ☐ All related Phase 0/1 remaining items closed or explicitly re-scoped
- ☐ User accepts the work
- ☐ Delete `docs/development/REMAINING_PHASE0_PHASE1_3SPRINT_PLAN.md`
