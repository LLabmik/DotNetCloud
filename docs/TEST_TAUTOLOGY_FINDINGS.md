# Test Tautology Findings

**Date:** October 21, 2026
**Branch:** `fix/trim-tautological-tests`
**Plan:** [TEST_TAUTOLOGY_AUDIT_PLAN.md](./TEST_TAUTOLOGY_AUDIT_PLAN.md)
**Status:** Phase 0–1 complete; remediation in progress (3 changes applied, 2nd batch pending approval)

---

## 1. Phase 0 — Baseline

### Build

| Check                       | Result                       |
| --------------------------- | ---------------------------- |
| `dotnet build DotNetCloud.CI.slnf -c Release` | ✓ succeeded, 0 warnings, 0 errors (2m 09s) |

### Test run

Every test project was run with `--filter "TestCategory!=Integration"` and `--collect:"XPlat Code Coverage"`.
The two DB-provider integration projects (`*.Integration.Tests.PostgreSQL`, `*.Integration.Tests.SqlServer`) were
excluded because they require a live database, matching the repo's review-only policy for integration projects.

| Project                                | Passed | Failed | Skipped |
| -------------------------------------- | -----: | -----: | ------: |
| Client.Android.Tests                   |    550 |      0 |       1 |
| Client.Core.Tests                      |    312 |      0 |       0 |
| Client.SyncTray.Tests                  |    149 |      0 |       0 |
| CLI.Tests                              |    180 |      0 |       0 |
| Core.Auth.Tests                        |    180 |      0 |       0 |
| Core.Data.Tests                        |    177 |      0 |       0 |
| Core.Search.Tests                      |    180 |      0 |       0 |
| Core.Server.Tests                      |    803 |      0 |       2 |
| Core.Tests                             |    532 |      0 |       0 |
| Integration.Tests                      |     48 |      0 |       0 |
| Modules.About.Tests                    |     12 |      0 |       0 |
| Modules.AI.Tests                       |     39 |      0 |       0 |
| Modules.Bookmarks.Tests                |     24 |      0 |       0 |
| Modules.Calendar.Tests                 |    208 |      0 |       0 |
| Modules.Chat.Tests                     |  1,488 |      0 |       0 |
| Modules.Contacts.Tests                 |    152 |      0 |       0 |
| Modules.Email.Tests                    |      8 |      0 |       0 |
| Modules.Example.Tests                  |     51 |      0 |       0 |
| Modules.Files.Tests                    |    968 |      0 |       0 |
| Modules.Music.Tests                    |    404 |      0 |       0 |
| Modules.Notes.Tests                    |    157 |      0 |       0 |
| Modules.Photos.Tests                   |    306 |      0 |       0 |
| Modules.Tracks.Tests                   |    205 |      0 |       0 |
| Modules.Video.Tests                    |    219 |      0 |       0 |
| UI.Shared.Tests                        |    193 |      0 |       0 |
| **Total**                              | **7,545** | **0** | **3** |

Baseline artifacts: `artifacts/test-tautology/baseline/` (TRX per project).

### Coverage baseline limitation

Coverage was produced for 21 projects. Five projects reported
`Unable to find a datacollector with friendly name 'XPlat Code Coverage'`:

- `DotNetCloud.Client.Core.Tests`
- `DotNetCloud.Core.Auth.Tests`
- `DotNetCloud.Core.Tests`
- `DotNetCloud.Integration.Tests`
- `DotNetCloud.Modules.Example.Tests`

These projects use the Microsoft.Testing.Platform runner, which does not support VSTest `--collect` data collectors.
Per-project coverage deltas for these five cannot be measured with the current command. Options (out of scope for this
audit): use `dotnet-coverage` / `coverlet.msbuild`, or force VSTest mode for the remediation comparison. **No deletions
should be approved in these five projects without an alternative coverage measurement.**

---

## 2. Phase 1 — Heuristic Scan

**Tool:** `scripts/scan-tautological-tests.py` (method-level; raw-string and expression-bodied method aware).
**Scope:** 584 test files, **7,380 test methods**.
**Output:** `artifacts/test-tautology/findings-heuristic.{csv,json}`.

| Bucket | Rule | Count | Meaning |
| ------ | ---- | ----: | ------- |
| T1 | No observable assertion / self-swallowing try-catch | **2** | Strict tautology candidates |
| T2 | Identical-argument assertion | **0** | — |
| T3 | Literal/constant truth (`Assert.IsTrue(true)`) | **0** | — |
| T4 | Compiler-guaranteed (`IsNotNull(new X())`) | **0** | — |
| T8 | Exact duplicate body in the same file | **0** | — |
| W1 | Not-null-only assertions | 270 | Weak but valid; mostly CLI option/property pins |
| W2 | Mock `Verify`-only tests | 486 | Mostly meaningful (event published, endpoint called) |
| W3 | No explicit assertion, "does not throw" | 107 | Weak smoke tests |

### 2.1 Confirmed T1 candidates

| # | Test | File | Why it is tautological | Proposed decision |
| - | ---- | ---- | ---------------------- | ----------------- |
| 1 | `CapabilityNotGrantedException_CanBeThrown` | `tests/DotNetCloud.Core.Tests/Errors/DotNetCloudExceptionsTests.cs:13` | Throws and catches its own exception; no assertion. Constructor behaviour is covered by `StoresCapabilityName` and `StoresCallerContext`. | **DELETE** |
| 2 | `GetStoragePathAsync_CancellationRequested_ThrowsOrReturns` | `tests/DotNetCloud.Modules.Files.Tests/Services/FileServiceGetStoragePathTests.cs:182` | Catches `OperationCanceledException` but does not assert it was thrown; the in-memory provider may return normally, so either outcome passes. | **FIX** (assert cancellation deterministically) or **DELETE** if cancellation is covered lower down |

### 2.2 Additional candidate found during validation (not caught by a rule)

| # | Test | File | Why it is tautological | Proposed decision |
| - | ---- | ---- | ---------------------- | ----------------- |
| 3 | `AttachFileAsync_AddsMessageToCollection` | `tests/DotNetCloud.Client.Android.Tests/ViewModels/MessageListViewModelTests.cs:347` | Name promises a collection assertion; body only performs setup and a comment saying the behaviour cannot be mocked. Nothing is asserted. | **DELETE** (or rewrite as an integration/UI test) |

### 2.3 Composition of the weak buckets

| Bucket | Composition observed | Disposition |
| ------ | -------------------- | ----------- |
| W1 (270) | CLI command structure pins (`LogsCommand_HasLevelOption`), DTO/default property pins, entity default-value tests | Mostly **KEEP** as contract pins; sample review to confirm |
| W2 (486) | `Verify` on event bus (`PublishAsync` 73, `SendAsync` 43, `BroadcastAsync` 18), HTTP endpoint calls, cache eviction | Mostly **KEEP** — production code causes the interaction |
| W3 (107) | "Does not throw" smoke tests (`Dispose_MultipleTimes_DoesNotThrow`, `RecordPlayAsync_Succeeds`, `Clear_Empty_DoesNotThrow`) | Review individually; these have a real (if weak) failure signal |

W2 and W1 are **not** tautological-only by definition (Section 3 of the plan). They are reported separately so they are
never auto-deleted.

### 2.4 Authoritative Roslyn classification

**Tool:** `tools/TestTautologyScanner/` (Roslyn 5.9, dev-only, not in `DotNetCloud.sln`).
**Command:** `dotnet run --project tools/TestTautologyScanner -c Release -- --tests-root tests --out artifacts/test-tautology`.
**Output:** `artifacts/test-tautology/findings-roslyn.{csv,json}` (all 7,378 methods classified).

| Bucket | Count |
| ------ | ----: |
| T1 — no observable assertion | 0 |
| T2 — identical-argument assertion | **1** |
| T3 — literal/constant truth | 0 |
| T4 — compiler-guaranteed assertion | 0 |
| T5 — echo assertion | 0 (6 false positives removed by mutation tracking) |
| T6 — mock round-trip | **1** |
| T7 — test-fixture assertion | 0 |
| T8 — duplicate body | 0 |
| W1 — not-null-only | 270 |
| W2 — verify-only | 486 |
| W3 — no explicit assertion | 100 |

**Decisive signal:** all 486 W2 tests have at least one production-code invocation (0 have `ProductionCalls == 0`), so
every `Verify` is checking an interaction caused by production code — none is a mock round-trip. Of the 270 W1 tests, 40
have zero production calls; those are analysed below.

---

## 3. Interpretation

1. **The suite has very few strict tautologies.** The authoritative scan found only 2 remaining (one T2, one T6) plus
   3 already removed/fixed in this branch. The heuristic's earlier "124 candidates" were false positives from raw JSON
   strings, expression-bodied methods, and `Verify`-only tests.
2. **The weak buckets are mostly legitimate.** All 486 `Verify`-only tests exercise production code; none is a mock
   round-trip. The 270 not-null-only tests are dominated by contract pins (CLI options, DTO defaults, size limits).
3. **The 40 not-null-only tests with zero production calls** are the only meaningful remaining pocket: they split into
   pure Moq checks (tautological), compiler-guaranteed interface pins (architecture contract), and constructor/default
   checks (weak but valid). Those are listed in §5.
4. **Three regex false-positive classes were fixed during the audit** — Moq `Verify`-only tests, raw JSON string
   literals breaking brace matching, and expression-bodied methods. This is why the Roslyn scanner, not the regex pass,
   is the source of truth.
5. **`ReleaseStreamSlot_FreesSlot` is not tautological** — the subsequent `AcquireStreamSlot` throws if the slot was not
   released, so the test has a real failure signal.

---

## 4. Selected Approach — A + C

The user selected **A + C**: apply the confirmed candidate changes *and* build the full Roslyn scanner to classify the
weak buckets.

Completed:

- ✓ Built `tools/TestTautologyScanner/` (Roslyn 5.9, 0 warnings/errors).
- ✓ Classified all 7,378 test methods; outputs in `artifacts/test-tautology/findings-roslyn.{csv,json}`.
- ✓ Validated T5 against 6 candidates; mutation tracking removed all 6 false positives.
- ✓ Verified W2 against `ProductionCalls`: 0 mock round-trips.

---

## 5. Decisions

### 5.1 Applied and verified (affected suites green)

| # | Test | Action | Verification |
| - | ---- | ------ | ------------ |
| 1 | `CapabilityNotGrantedException_CanBeThrown` (Core.Tests) | **DELETED** — throw/catch with no assertion | `DotNetCloudExceptionsTests`: 14/14 passed |
| 2 | `GetStoragePathAsync_CancellationRequested_ThrowsOrReturns` (Files.Tests) | **FIXED** — SQLite provider + `Assert.ThrowsAsync<OperationCanceledException>` | `FileServiceGetStoragePathTests`: 8/8 passed |
| 3 | `AttachFileAsync_AddsMessageToCollection` (Client.Android.Tests) | **DELETED** — no assertions, only setup | `MessageListViewModelTests`: 59/59 passed |

**Full-suite verification after batch 1:** 7,543 passed / 0 failed / 3 skipped across 25 projects
(`artifacts/test-tautology/verify-after-batch1/`). Net −2 tests versus baseline (two deleted, one rewritten).

### 5.2 Second batch — applied and verified

Approved by the user on 2026-10-21. Actions below were applied; the authoritative rescan reports **0 strict
tautological candidates** (7,372 methods: W1 268, W2 486, W3 96).

| # | Test | Rule | Action | Rationale |
| - | ---- | ---- | ------ | --------- |
| 4 | `RequestCode_IsIdentical_ForTheSameReminder` (Client.Android.Tests) | T2 | **DELETED** | Called the same pure function twice with identical args. Determinism is pinned by `StableHash_MatchesFnv1a_KnownValues`; variation is covered by `RequestCode_DiffersByOffsetAndOccurrence` |
| 5 | `DetectContentType_CanBeMocked` (Core.Tests) | T6 | **DELETED** | Pure Moq round-trip; tested the mocking framework, not production code |
| 6 | `PlaybackStateChanged_UpdatesState` (Client.Android.Tests) | W3 | **DELETED** | Raised the event, asserted nothing; comment admitted the main-thread dispatch may not run |
| 7 | `Constructor_SubscribesToPlayerEvents` (Client.Android.Tests) | W3 | **DELETED** | Raising an event on a mock with no handler is a no-op; proved nothing about subscription |
| 8 | `IModule_CanBeMocked` (Core.Tests) | W1 | **DELETED** | `Assert.IsNotNull(new Mock<IModule>().Object)` — pure Moq check |
| 9 | `EventBus_CanBeMocked` (Core.Tests) | W1 | **DELETED** | Same pure Moq check |
| 10 | `RecordPlayAsync_Succeeds` (Client.Android.Tests) | W3 | **FIXED** → `RecordPlayAsync_SendsPostToPlayEndpoint` | Asserts POST, exact play URL, Bearer scheme and token |
| 11 | `ToggleStarAsync_Track_StarsTrack` (Client.Android.Tests) | W3 | **FIXED** → `ToggleStarAsync_Track_SendsPostToStarEndpoint` | Asserts POST and exact star URL |
| 12 | `ExtractAsync_CorruptFile_ReturnsNull` (Core.Tests) | W3 | **FIXED** → `ExtractAsync_CorruptFile_DoesNotThrow` | Name now matches the actual contract (graceful, no throw) |
| 13 | `ReleaseStreamSlot_FreesSlot` (Modules.Music.Tests) | W3 | **KEEP** | Implicit assertion: the follow-up acquire throws if the slot was not released |
| 14 | Interface-implementation pins (`WhenCreatedThenImplementsIModuleManifest`, `...IModuleLifecycle`, `...ImplementsIEventHandler`, `ImplementsAll*EventHandlerInterfaces`, etc.) | W1 | **KEEP** | Compiler-adjacent, but they guard accidental interface removal in the module contract (rubric Q1/Q2). One-line rationale should be added if re-flagged |
| 15 | Constructor/default pins (`AllArgsConstructor_Default_DoesNotThrow`, `RoleDto_HasDefaultPermissions`, `AllowedFileTypes_*_HasSizeLimit`, `ModuleManifest_EmptyCollections_AreReadOnly`) | W1 | **KEEP** | Contract pins / real constructor behaviour |

**Full-suite verification after batch 2:** 7,537 passed / 0 failed / 3 skipped across 25 projects
(`artifacts/test-tautology/verify-after-batch2/`). Net −8 tests versus baseline (8 deleted, 3 rewritten in place).

**Rescan:** 7,372 methods classified; **0 strict tautological candidates**; W1 268, W2 486, W3 96.

No commit will be made until the user approves (repo rule #1).
