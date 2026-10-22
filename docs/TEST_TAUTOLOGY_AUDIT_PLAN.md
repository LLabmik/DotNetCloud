# Test Tautology Audit Plan

**Date:** October 21, 2026
**Status:** Planning
**Branch:** `fix/trim-tautological-tests`
**Tracking:** Update `docs/IMPLEMENTATION_CHECKLIST.md` and `docs/MASTER_PROJECT_PLAN.md` after execution of each phase

---

## 1. Purpose

Identify **tautological-only tests** across the entire test suite — tests whose result is guaranteed by the test's own
construction, the compiler, or the mocking framework rather than by production behaviour — and then decide, with
evidence, whether each one is worth **keeping, fixing, merging, or deleting**.

This is a test-quality audit, **not** a coverage push. Removing a tautological test is only acceptable when it is proven
to provide no failure signal and no contract/security value.

### Out of Scope

- Rewriting tests for style or naming (unless the test is being fixed anyway)
- Chasing line-coverage percentages
- Adding tests for untested production code (separate initiative)
- Mutation testing of the whole repository (used only as a targeted oracle, see Phase 4)
- Changing production code (except where a test is fixed to observe real behaviour)

---

## 2. Baseline Inventory (verified October 21, 2026)

| Metric                                   | Value                                                       |
| ---------------------------------------- | ----------------------------------------------------------- |
| Test project directories                 | 28 (1 empty: `DotNetCloud.Modules.Search.Tests`)            |
| Test source files (`*Tests.cs`)          | 584                                                         |
| `[TestMethod]` attributes                | 7,380                                                       |
| `Assert.*` call sites                    | 13,746                                                      |
| Moq `.Verify(...)` call sites            | 827                                                         |
| Framework                                | MSTest 4.3.3, Microsoft.NET.Test.Sdk 18.8.1, coverlet 10.0.1 |

### Files per project (largest first)

| Project                       | Files | Project                       | Files |
| ----------------------------- | ----- | ----------------------------- | ----- |
| Modules.Files                 | 82    | Modules.Calendar              | 19    |
| Core.Server                   | 78    | Core.Data                     | 17    |
| Modules.Chat                  | 70    | Core.Auth                     | 16    |
| Core                          | 44    | Modules.Contacts              | 15    |
| Client.Android                | 35    | Core.Search                   | 13    |
| Modules.Music                 | 24    | CLI                           | 13    |
| Modules.Photos                | 23    | Modules.Notes                 | 12    |
| Integration.Tests             | 23    | Client.SyncTray               | 11    |
| Modules.Video                 | 21    | UI.Shared                     | 8     |
| Modules.Tracks                | 21    | Modules.Example / Modules.AI  | 5 / 5 |
| Client.Core                   | 20    | Modules.Bookmarks / Email / About | 3 / 2 / 2 |
| Integration.Tests.PostgreSQL / .SqlServer | 1 / 1 | Modules.Search           | 0     |

---

## 3. Definition: What Counts as a "Tautological-Only" Test

A test is **tautological-only** when **every** assertion or interaction check in it is one of the patterns below, so that
no realistic production defect could make it fail.

> A test that contains **one** meaningful assertion plus several tautological ones is **not** tautological-only. It is
> out of scope for deletion (it may still be a candidate for cleanup, tracked separately).

### 3.1 Taxonomy

| ID  | Category                            | Description                                                                                               | Example shape                                                                 |
| --- | ----------------------------------- | --------------------------------------------------------------------------------------------------------- | ----------------------------------------------------------------------------- |
| T1  | No observable assertion             | Executes code but never asserts, never `Verify`s, and has no expected-exception attribute                  | `await service.DoAsync(...);` with no assertion                               |
| T2  | Self-referential assertion          | Both sides of the assertion are the same expression/variable                                              | `Assert.AreEqual(value, value)`                                               |
| T3  | Literal/constant truth              | Asserts a compile-time constant or literal against itself                                                 | `Assert.IsTrue(true)`, `Assert.AreEqual("video", "video")`                    |
| T4  | Compiler-guaranteed assertion       | Asserts non-null/type of an object the test just constructed, without exercising production behaviour      | `var x = new Foo(); Assert.IsNotNull(x);`                                     |
| T5  | Echo assertion                      | Asserted value is a test-local variable that was never produced by the code under test                    | `var expected = 5; Assert.AreEqual(expected, expected);`                      |
| T6  | Mock round-trip                     | Asserts the value the mock was configured to return, not what production code did with it                 | `mock.Setup(m => m.Get()).Returns(1); Assert.AreEqual(1, mock.Object.Get());` |
| T7  | Test-owned fixture assertion        | Asserts on data a test helper built, rather than on a production return value                             | `var dto = TestHelpers.CreateDto(); Assert.AreEqual("x", dto.Name);`          |
| T8  | Duplicate body                      | Identical (normalised) test body already covered by another test in the same fixture                      | Copy-pasted test with renamed locals                                          |
| W1  | Weak-but-valid (not tautological)   | Only `IsNotNull` on a real production return, or only absence-of-throw. Flagged separately, **never auto-deleted** | `Assert.IsNotNull(result);` after real call                                  |
| W2  | Mock-only interaction check         | Sole assertion is a `Verify` for a call with no observable consequence; may still protect wiring            | `mock.Verify(m => m.Log(...), Times.Once)`                                    |

T8 is **redundancy**, not tautology; it is collected in a separate bucket so the two are never conflated.

### 3.2 Explicit False-Positive Exclusions (never auto-delete)

These patterns can look tautological but carry value. They require manual justification before removal:

1. **Guard-clause tests** — `Assert.ThrowsExactlyAsync<ArgumentNullException>(...)` for public API validation.
2. **Contract pins** — asserts on protocol/wire constants, default values, enum numeric values, serialization field
   names, gRPC metadata headers. These intentionally pin external contracts (e.g. default chunk size 4 MB, `module-id`
   header requirement).
3. **Security/authorization invariants** — capability checks, path-traversal rejection, HTML sanitisation, auth
   interceptors, role checks.
4. **Only coverage of a code path** — if removing the test drops line/branch coverage of production code, it stays
   until replaced by a meaningful test.
5. **Platform-specific behaviour** — Android alarms, SyncTray UI/ViewModel state, MAUI lifecycle.
6. **Integration/provider projects** — `Integration.Tests*`, PostgreSQL/SQL Server variants, SignalR payload validation.
7. **Migration/entity-configuration tests** — schema and query-filter guarantees.
8. **`Verify(Times.Never)` negative tests** — these assert a side effect did **not** occur; absence is meaningful when
   the alternative would be an observable side effect (notification sent, event published).

---

## 4. Execution Phases

### Phase 0 — Baseline & Safety Net

- ✓ Confirm working tree state and branch (`fix/trim-tautological-tests`); do not commit to `main`.
- ✓ Record the full-suite result **before** any change: 7,545 passed, 0 failed, 3 skipped across 25 projects
  (`artifacts/test-tautology/baseline/`).
- ✓ Record per-project coverage baseline with coverlet into `artifacts/test-tautology/coverage/baseline/`
  (5 MTP-mode projects do not support `--collect` — documented in `TEST_TAUTOLOGY_FINDINGS.md`).
- ✓ Record per-project test counts and timings (used to prove deletions removed only dead weight).
- ✓ Create `artifacts/test-tautology/` (gitignored) for scanner output; never commit generated reports.

**Exit criteria:** baseline report committed as a generated artifact (not source), all tests green before touching
anything. — **Met 2026-10-21.**

---

### Phase 1 — Automated Static Scan

**Recommended approach: hybrid.**

1. **Fast heuristic pass (ripgrep)** — produces a first-cut candidate list and validates the scanner. Patterns:
   - `Assert\.(IsTrue|IsFalse)\((true|false)\)`
   - PCRE2 identical-argument asserts: `Assert\.\w+\(\s*(\w+)\s*,\s*\1\s*[,)]`
   - methods with no `Assert.` / `.Verify(` / `Throws` / expected-exception attribute
   - normalised duplicate method bodies
2. **Authoritative Roslyn scanner** — a small dev-only console tool at `tools/TestTautologyScanner/`
   (not added to `DotNetCloud.sln`). It parses every `tests/**/*Tests.cs` file with `Microsoft.CodeAnalysis.CSharp`
   and classifies **per test method**, not per file, because "tautological-only" is a per-method property.

#### Scanner design

| Step | Behaviour                                                                                                                     |
| ---- | ----------------------------------------------------------------------------------------------------------------------------- |
| 1    | Discover test methods: `[TestMethod]`, `[DataTestMethod]`, `[Theory]` equivalents, plus inherited fixtures                    |
| 2    | Extract the method body (including local functions and awaited calls)                                                          |
| 3    | Identify "production calls" — invocations on fields/DI types that are not mocks/helpers and are not `Assert`/`Verify`           |
| 4    | Collect assertion nodes: `Assert.*`, FluentAssertions `.Should()`, `Assert.Throws*`, `[ExpectedException]`, Moq `.Verify`       |
| 5    | Apply rules T1–T8, classify each assertion; the method is **tautological-only** iff it has ≥ 1 assertion and all are T1–T7 (or ≥1 T1) |
| 6    | Emit `artifacts/test-tautology/findings.json` and `findings.csv`; one row per test method with file, line, rule IDs, evidence   |
| 7    | Emit `docs/TEST_TAUTOLOGY_FINDINGS.md` skeleton: project → file → method → rule → proposed decision (blank)                    |

**Important:** the scanner must treat Moq `Verify` as an assertion (the earlier quick scan missed 10 files because they
only used `Verify`). It must also detect `Assert.ThrowsExactlyAsync` as a *meaningful* assertion, not as T1.

- ✓ Heuristic pass implemented and run: `scripts/scan-tautological-tests.py` (7,380 methods; 2 T1, 0 T2–T4, 0 T8,
  863 weak). See `TEST_TAUTOLOGY_FINDINGS.md`.
- ✓ Implement Roslyn scanner: `tools/TestTautologyScanner/` (Roslyn 5.9; rules T1–T8, W1–W3; 0 warnings/errors).
- ☐ Add per-rule fixture tests for the scanner (follow-up; rules were validated against real findings and a 6-case T5
  false-positive set).
- ✓ Run scanner across all test projects; produce inventory (7,378 methods).
- ✓ Validate scanner against real candidates (strict T2/T6 review; T5 mutation tracking removed 6 false positives).
- ✓ Tune rules until false-positive rate is acceptable (raw strings, expression-bodied methods, `Verify`-only, and
  mutated-counter T5 cases).
- ✓ **Decision gate:** user selected **A + C** — apply confirmed candidates and build the full scanner
  (`TEST_TAUTOLOGY_FINDINGS.md` §4–§5).

**Exit criteria:** every one of the 7,380 test methods has a classification row; sample validation recorded.

---

### Phase 2 — Triage & Decision Rubric

Candidates are reviewed in batches, largest projects first, to front-load risk:

| Batch | Projects                                                       | Rationale                        |
| ----- | -------------------------------------------------------------- | -------------------------------- |
| 1     | Core, Core.Data, Core.Auth, Core.Server, Core.Search           | Foundation; highest blast radius  |
| 2     | Modules.Files, Modules.Chat                                    | Largest suites; most candidates  |
| 3     | Modules.Music, Photos, Video, Tracks, Calendar, Contacts, Notes | Feature modules                  |
| 4     | Client.Android, Client.Core, Client.SyncTray, UI.Shared        | Platform-specific, UI state      |
| 5     | CLI, Modules.AI, Example, Bookmarks, Email, About              | Small suites                     |
| 6     | Integration.Tests*                                             | Review only — no deletions expected |

#### Decision rubric

Score each candidate on five questions (yes/no + evidence):

1. **Failure signal** — could a realistic production regression make this test fail?
2. **Contract value** — does it pin an external/wire/API contract?
3. **Coverage uniqueness** — does deleting it reduce line/branch coverage of production code?
4. **Security relevance** — does it protect an authorization/validation invariant?
5. **Redundancy** — is the same behaviour asserted by another, stronger test?

| Decision   | Rule                                                                                                     |
| ---------- | -------------------------------------------------------------------------------------------------------- |
| **DELETE** | Q1 = No, Q2 = No, Q3 = No, Q4 = No, and (Q5 = Yes or test is pure T1–T7)                                  |
| **FIX**    | The stated intent is valuable but the assertion is tautological — rewrite to assert observable behaviour |
| **MERGE**  | Duplicate (T8) or strictly weaker than another test — fold unique data rows into the stronger test       |
| **KEEP**   | Contract pin, security invariant, only coverage of a path, or negative test with meaningful side effect  |
| **ESCALATE** | Ambiguous, or deletion would remove the only test of a module/feature — requires user decision         |

- ✓ Review every strict candidate; decisions recorded in `docs/TEST_TAUTOLOGY_FINDINGS.md` §5.
- ✓ Characterise the weak buckets using `ProductionCalls` (W2 = 0 mock round-trips; W1 = 40 zero-production cases
  reviewed and split into delete/fix/keep).
- ☐ For every DELETE candidate, run a targeted coverage comparison **before** deletion (see Phase 4).
- ☐ For every KEEP/ESCALATE, write a one-line reason so future audits do not re-flag it (batch 2 recommendations are
  recorded; approval pending).
- ☐ Present a consolidated summary to the user: counts per decision, list of escalated items, and proposed commits.

**Exit criteria:** no candidate left undecided; user approval recorded for the remediation batch.

---

### Phase 3 — Remediation

Work in **isolated commits, one project per commit**, in this order: DELETE → MERGE → FIX.

- ☐ **DELETE** — remove only approved tautological-only tests. Do not touch shared helpers unless now unused;
      if a helper becomes unused, remove it in the same commit and note it.
- ☐ **MERGE** — collapse duplicates, preserving the union of their data rows / edge cases.
- ☐ **FIX** — rewrite tautological tests whose intent is worth preserving so they assert on production-produced values
      (return value, persisted state, emitted event, downstream mock interaction caused by production code).
- ☐ After each project: `dotnet test tests/<Project>/` must be green.
- ☐ Re-run coverlet for that project and attach the delta to the commit message / findings doc.
- ☐ Update `docs/TEST_TAUTOLOGY_FINDINGS.md` status column: `deleted`, `merged`, `fixed`, `kept`.

**Hard guardrails:**

- ❌ Never delete a test in the same commit as a production-code change.
- ❌ Never delete a test that is the sole coverage of a production path.
- ❌ Never delete guard-clause, security, or contract-pin tests (Section 3.2).
- ❌ Never modify `tests/DotNetCloud.Integration.Tests*` in this effort without explicit approval.
- ✅ Every deletion must cite the rule ID(s) and coverage evidence.

---

### Phase 4 — Targeted Mutation Oracle (only for disputes)

For candidates marked ESCALATE, or where coverage says "unique" but the test still looks tautological:

- ☐ Run **Stryker.NET** on the single production class under test (not the whole module).
- ☐ If the test survives all mutants that the candidate claims to protect against, it is confirmed tautological.
- ☐ If it kills a mutant, it has real failure signal → KEEP/FIX, and record the surviving/killed mutant count.

This keeps mutation testing cost bounded to a handful of classes.

---

### Phase 5 — Final Verification

- ☐ Run the full suite: `dotnet test DotNetCloud.sln` — record pass/fail/skip and duration; compare to Phase 0.
- ☐ Compare per-project coverage against baseline; any decrease must be explained in the findings doc.
- ☐ Confirm the removed-test count equals the approved DELETE count.
- ☐ Confirm no production code changed in this branch (unless a FIX required it — list those explicitly).
- ☐ Verify solution builds with warnings-as-errors (`dotnet build`).
- ☐ Update `docs/IMPLEMENTATION_CHECKLIST.md` and `docs/MASTER_PROJECT_PLAN.md` with the completed audit phase.
- ☐ Present branch, file list, and evidence to the user; **wait for explicit approval before committing/pushing.**

**Acceptance criteria:**

1. 100% of test methods classified by the scanner.
2. Every flagged candidate has a recorded decision + rationale.
3. Full suite green; meaningful coverage unchanged or improved.
4. All deletions/merges/fixes are reviewable as per-project commits.
5. No guard-clause, security, contract-pin, integration, or platform tests removed.

---

### Phase 6 — Regression Guard (optional, follow-up)

- ☐ Add a lightweight CI check for the unambiguous patterns only: `Assert.IsTrue(true)`, identical-argument asserts,
      and test methods with zero assertions/verifications. Fail the PR on new occurrences.
- ☐ Do **not** put the full Roslyn classifier in CI until its false-positive rate is proven stable.

---

## 5. Deliverables

| Artifact                                  | Purpose                                                          |
| ----------------------------------------- | ---------------------------------------------------------------- |
| `docs/TEST_TAUTOLOGY_AUDIT_PLAN.md`       | This plan                                                        |
| `tools/TestTautologyScanner/`             | Roslyn-based per-method classifier (dev-only, out of solution)   |
| `scripts/scan-tautological-tests.sh`      | Wrapper: runs scanner + heuristic pass, writes artifacts         |
| `artifacts/test-tautology/findings.*`     | Machine-readable results (gitignored)                            |
| `docs/TEST_TAUTOLOGY_FINDINGS.md`         | Curated, reviewed findings with decisions and evidence           |
| Per-project commits on `fix/trim-tautological-tests` | DELETE / MERGE / FIX changes with coverage deltas    |

---

## 6. Effort Estimate

| Phase                          | Estimate      |
| ------------------------------ | ------------- |
| 0 — Baseline                   | 0.5 day       |
| 1 — Scanner + validation       | 1.5–2 days    |
| 2 — Triage (7,380 methods)     | 2–3 days      |
| 3 — Remediation                | 1–3 days (depends on candidate count) |
| 4 — Mutation oracle (disputes) | 0.5 day       |
| 5 — Final verification         | 0.5 day       |
| **Total**                      | **6–9.5 days** |

Triage can be parallelised by project batch once the scanner is validated.

---

## 7. Risks & Mitigations

| Risk                                                     | Mitigation                                                                 |
| -------------------------------------------------------- | -------------------------------------------------------------------------- |
| Deleting a test that silently guarded a regression        | Decision rubric Q1 + targeted mutation oracle + coverage delta per project |
| Heuristic false positives (e.g. `Verify`-only tests)      | Roslyn scanner counts `Verify`, `Throws`, `Should()` as assertions         |
| "Tautological" contract pins get removed                 | Section 3.2 exclusions; KEEP requires only a one-line rationale            |
| Coverage drop hidden by aggregate numbers                 | Per-project, per-class coverage comparison before each deletion batch      |
| Scanner becomes maintenance burden                        | Keep it dev-only, out of the solution; regex guard in CI only if stable    |
| Scope creep into rewriting the whole suite                | FIX only tests whose intent is worth preserving; no style-only edits       |
| Committing without full verification                      | Repo rule #1 approval gate; Phase 5 evidence before any commit             |

---

## 8. First Action on Approval

1. Complete Phase 0 baseline (full test run + coverage snapshot).
2. Implement the heuristic pass and run it to size the candidate pool.
3. Report back with: candidate count, top 20 examples, and whether the Roslyn scanner is justified by the volume.

No tests will be modified or deleted until the findings are reviewed and the user explicitly approves each remediation
batch.
