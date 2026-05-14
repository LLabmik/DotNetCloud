# Code Review Plan — Base Monolith & All Modules

**Date:** May 13, 2026
**Status:** Planning
**Security Review:** [SECURITY_REVIEW_PLAN.md](./SECURITY_REVIEW_PLAN.md) — companion security-focused review plan

---

## Overview

A phased, tool-assisted comprehensive code review of the entire DotNetCloud codebase (~1,500+ C# files across 62 projects). The review focuses on:

- **Code efficiency** — N+1 queries, unnecessary allocations, async/await misuse, hot-path optimization
- **Code completeness** — No stubs, no `NotImplementedException`, no half-finished features
- **Test coverage** — All code covered by meaningful tests; edge cases tested
- **Code readability** — Clear naming, well-structured methods, appropriate abstraction levels
- **Code standardization** — Consistent patterns across modules, adherence to project conventions
- **Raw SQL usage** — Entity Framework preferred; raw SQL only when justified and properly parameterized

### Out of Scope

- Security vulnerabilities (separate review planned)
- Authentication/authorization logic review
- Data validation for security purposes
- CORS, CSP, HSTS configuration review
- Penetration testing or threat modeling

---

## Codebase Inventory

### Source Code

| Area      | Projects | .cs Files  | Description                                                     |
| --------- | -------- | ---------- | --------------------------------------------------------------- |
| Core      | 8        | 334        | Foundation — interfaces, data layer, auth, gRPC, server host    |
| Modules   | 45       | 1,168      | 15 feature modules (3 projects each: Core/Data/Host)            |
| Clients   | 4        | ~100       | Desktop (Avalonia), Android (MAUI), Browser Extension, Core SDK |
| CLI       | 1        | ~20        | Command-line administration tool                                |
| UI        | 4        | ~50        | Blazor Web, WebAssembly, Shared Components, Android UI          |
| **Total** | **62**   | **~1,672** |                                                                 |

### Test Projects (23 projects)

| Area         | Count | Projects                                                                                  |
| ------------ | ----- | ----------------------------------------------------------------------------------------- |
| Core Tests   | 5     | Core, Core.Data, Core.Auth, Core.Server, Integration                                      |
| Module Tests | 13    | AI, Calendar, Chat, Contacts, Example, Files, Music, Notes, Photos, Search, Tracks, Video |
| Client Tests | 3     | Client.Core, SyncTray, Android                                                            |
| CLI Tests    | 1     | CLI                                                                                       |
| UI Tests     | 1     | UI.Shared                                                                                 |

### Gaps Identified

- ✓ **Bookmarks** — No test project (✅ Now has `Bookmarks.Tests` with `BookmarkFolderServiceTests.cs` and `BookmarkServiceTests.cs`)
- ✓ **Email** — No test project (✅ Now has `Email.Tests` with `EmailAccountServiceTests.cs` and `EmailRuleServiceTests.cs`)
- ✓ **About** — No test project (✅ Now has `About.Tests` with `AboutHealthCheckTests.cs` and `AboutModuleTests.cs`)
- ☐ 3 active TODO/FIXME markers: 1 in Core (MariaDB .NET 10 support), 2 in Email (OAuth credentials)

### Pre-Discovery Findings

- **Raw SQL:** 1 usage — `ExecuteSqlRaw` in `LocalStateDb.cs` (SQLite pragma — legitimate)
- **Zero `NpgsqlCommand` usage** — database provider abstraction is properly followed
- **DbConnection usage** is limited to health checks and provider detection (legitimate)
- **SQL string literals** only in migration Designer files and DatabaseSetupHelper (DB provisioning — legitimate)

---

## Review Depth Strategy

| Area                      | Depth                                                                          | Rationale                                            |
| ------------------------- | ------------------------------------------------------------------------------ | ---------------------------------------------------- |
| **Core (334 files)**      | Deep — line-by-line review                                                     | Foundation everything depends on. Must be correct.   |
| **Modules (1,168 files)** | Pattern-based — automated analysis + targeted manual review of high-risk files | Volume too high for line-by-line. Focus on hotspots. |
| **Clients (100 files)**   | Pattern-based                                                                  | Smaller surface area, well-isolated                  |
| **CLI (20 files)**        | Pattern-based                                                                  | Utility code, straightforward patterns               |
| **UI (50 files)**         | Pattern-based                                                                  | Razor components, consistent patterns                |

---

## Phase 1: Automated Analysis Foundation

All steps in this phase run in parallel. Output is a metrics spreadsheet for all 62 projects.

### 1.1 Style Compliance

```bash
dotnet format DotNetCloud.sln --verify-no-changes --report
```

- Capture all format violations by project
- Check for: file-scoped namespaces, nullable reference types, unused usings, indentation

### 1.2 Code Coverage

```bash
dotnet test --collect:"XPlat Code Coverage" --settings coverlet.runsettings
reportgenerator -reports:tests/**/coverage.cobertura.xml -targetdir:coverage-report -reporttypes:Html
```

- Per-project line coverage %
- Per-project branch coverage %
- Identify files with <60% coverage (flag for review)
- Identify files with 0% coverage (flag as untested)

### 1.3 Roslynator / .NET Analyzers

```bash
dotnet build /p:EnforceCodeStyleInBuild=true -warnaserror- 2>&1 | tee analyzer-output.txt
```

- Capture all CS*, IDE*, RS\* warnings
- Group by project and severity
- Identify recurring patterns (same warning across many files)

### 1.4 Code Smell Scan

Search entire `src/` for:

| Pattern                                            | What It Finds           |
| -------------------------------------------------- | ----------------------- |
| `TODO\|FIXME\|HACK\|XXX\|TEMP\|KLUDGE`             | Unfinished work markers |
| `NotImplementedException`                          | Stub methods            |
| `throw new NotImplementedException`                | Explicit stubs          |
| `// TODO`                                          | Commented TODOs         |
| Commente-out code blocks (`//.*\{` patterns)       | Dead code               |
| Magic numbers (integers/literals not in constants) | Hard-coded values       |
| `Console.WriteLine` (outside CLI project)          | Debug logging left in   |
| `#pragma warning disable`                          | Suppressed warnings     |

### 1.5 Raw SQL / Non-EF Data Access

Search entire `src/` for:

| Pattern                                          | Risk                                   |
| ------------------------------------------------ | -------------------------------------- |
| `ExecuteSqlRaw`                                  | Raw SQL execution                      |
| `ExecuteSqlInterpolated`                         | Interpolated SQL (safer but still raw) |
| `FromSqlRaw`                                     | Raw SQL queries                        |
| `FromSqlInterpolated`                            | Interpolated queries                   |
| `SqlCommand`                                     | ADO.NET direct (SQL Server)            |
| `SqlConnection`                                  | ADO.NET direct (SQL Server)            |
| `NpgsqlCommand`                                  | ADO.NET direct (PostgreSQL)            |
| `NpgsqlConnection`                               | ADO.NET direct (PostgreSQL)            |
| `DbCommand` (outside health checks)              | Generic ADO.NET                        |
| SQL string literals in non-migration `.cs` files | String SQL queries                     |

### 1.6 Code Duplication

```bash
jscpd src/ --min-lines 10 --min-tokens 50 --reporters html
```

- Identify copy-pasted code across modules
- Flag opportunities for shared abstractions
- Measure duplication % per project

### 1.7 Complexity Metrics

```bash
dotnet tool run ndepend-analysis  # or manual via Microsoft.CodeAnalysis.Metrics
```

- Cyclomatic complexity per method
- Identify methods with complexity >15 (flag for refactoring)
- Lines of code per file (flag files >500 lines)
- Depth of inheritance

### 1.8 XML Documentation Coverage

- % of public types with `<summary>` docs
- % of public methods with `<summary>` docs
- % of public properties with `<summary>` docs
- % of parameters with `<param>` docs
- % of return values with `<returns>` docs

### Phase 1 Output

`/docs/CODE_REVIEW_FINDINGS.md` — **Metrics Summary** section with:

- Table: Per-project style violations, coverage %, TODO count, SQL usage count, duplication %, doc coverage %, complexity hotspots
- Ranked list of projects by "review risk" (high risk = low coverage + high complexity + many TODOs)
- Flagged files requiring mandatory manual review

---

## Phase 2: Core Monolith Deep Review

Line-by-line review of all 334 Core files. Review in dependency order.

### 2.1 `DotNetCloud.Core` — SDK Interfaces & DTOs (Highest Priority)

**Files:** All `.cs` files under `src/Core/DotNetCloud.Core/`

**Review Checklist:**

- ✓ **Interface naming:** All interfaces prefixed with `I`. No `Interface` suffix.
- ✓ **Capability tier alignment:** Each capability interface correctly assigned to Public/Restricted/Privileged/Forbidden tier
- ✓ **Event naming:** All event types suffixed with `Event`. Events are immutable records.
- ✓ **DTO completeness:** All DTOs have required properties. No missing fields that should exist.
- ✓ **XML docs:** All public interfaces, methods, properties, parameters, and return values documented
- ✓ **Namespace organization:** Namespaces reflect folder structure. No orphaned types.
- ✓ **Dead code:** No interfaces without implementers. No types without references.
- ✓ **Async patterns:** All async methods suffixed with `Async`. `CancellationToken` parameters present.
- ✓ **Error types:** Error DTOs follow consistent pattern. Error codes are well-organized.
- ✓ **Constants:** Constants organized by domain. No magic strings in constants files referencing other constants.

**Files to review:**

```
src/Core/DotNetCloud.Core/
├── AI/                    # AI capability interfaces
├── Authorization/         # Authorization models
├── Capabilities/          # Capability tier definitions
├── Constants/             # Application constants
├── DTOs/                  # Data transfer objects
├── Errors/                # Error types and codes
├── Events/                # Event type definitions
├── Import/                # Import interfaces
├── Localization/          # Localization models
├── Modules/               # Module manifest, lifecycle interfaces
├── Services/              # Service interfaces
```

### 2.2 `DotNetCloud.Core.Data` — EF Core Data Layer (Second Priority)

**Files:** All `.cs` files under `src/Core/DotNetCloud.Core.Data/`

**Review Checklist:**

- ✓ **DbContext design:** `CoreDbContext` is lean. No business logic in context.
- ✓ **Entity configurations:** All entities have configuration classes in `Configuration/`. Fluent API used consistently.
- ✓ **Soft delete:** Query filter applied correctly. No hard deletes bypassing filter.
- ✓ **Timestamp interceptors:** Automatic `CreatedAt`/`UpdatedAt` handling. No manual timestamp setting.
- ✓ **Migration quality:** Migrations are clean and reversible. No data loss in down methods.
- ✓ **Table naming:** `ITableNamingStrategy` correctly implemented for PostgreSQL (schemas), SQL Server (schemas), MariaDB (prefixes)
- ✓ **Provider detection:** `DatabaseProviderDetector` correctly identifies provider from connection string
- ✓ **Relationships:** Navigation properties are correct. No missing foreign keys. Cascade delete behavior is intentional.
- ✓ **Indexes:** Appropriate indexes for query patterns. No missing indexes causing table scans.
- ✓ **Query efficiency:** No N+1 patterns. `Include`/`ThenInclude` used where needed. `AsNoTracking` for read-only queries.
- ✓ **Async everywhere:** All database operations are async. No `.Result` or `.Wait()` calls.
- ✓ **Disposal:** All disposable resources properly disposed. Context lifetime managed correctly.

**Files to review:**

```
src/Core/DotNetCloud.Core.Data/
├── Configuration/         # Entity type configurations
├── Context/               # CoreDbContext, DbContextFactory
├── Entities/              # Entity models
├── Extensions/            # Extension methods
├── Infrastructure/        # DbContextFactory, seeding
├── Initialization/        # Database initialization
├── Interceptors/          # SaveChanges interceptors
├── Migrations/            # EF Core migrations (Designer files excluded)
├── Naming/                # Table naming strategies
├── Services/              # Data services
```

### 2.3 `DotNetCloud.Core.Auth` — Authentication & Authorization

**Files:** All `.cs` files under `src/Core/DotNetCloud.Core.Auth/`

**Review Checklist:**

- ✓ **Identity extension:** ASP.NET Core Identity configured correctly
- ✓ **OpenIddict integration:** OAuth2/OIDC flows properly implemented
- ✓ **Auth middleware:** Middleware chain order is correct
- ✓ **Claims handling:** Claims transformation and mapping
- ✓ **Code clarity:** Auth logic is well-documented (auth is inherently complex)

### 2.4 `DotNetCloud.Core.Grpc` — gRPC Infrastructure

**Files:** All `.cs` files under `src/Core/DotNetCloud.Core.Grpc/`

**Review Checklist:**

- ✓ **Proto file conventions:** Consistent message naming, package naming
- ✓ **Service registration:** Services registered with correct lifetime
- ✓ **gRPC interceptors:** Error handling, logging, context propagation
- ✓ **Channel management:** Channels properly created and disposed
- ✓ **Streaming:** Server streaming, client streaming, bidirectional patterns

### 2.5 `DotNetCloud.Core.Server` — Core Server Host

**Files:** All `.cs` files under `src/Core/DotNetCloud.Core.Server/`

**Review Checklist:**

- ✓ **Program.cs:** Startup code is clear and well-organized. No god-method.
- ✓ **Module loading:** Module process management, lifecycle enforcement
- ✓ **Supervisor:** Process health monitoring, restart logic, graceful shutdown
- ✓ **Middleware pipeline:** Correct order, no redundant middleware
- ✓ **SignalR:** Real-time communication patterns
- ✓ **Health checks:** Endpoints return correct status. Database health check is efficient.
- ✓ **Configuration:** `appsettings.json` is clean. No secrets in config files.
- ✓ **Dependency injection:** Services registered with appropriate lifetimes. No captive dependencies.

**Files to review:**

```
src/Core/DotNetCloud.Core.Server/
├── Configuration/         # Server configuration
├── Controllers/           # API controllers
├── Extensions/            # IServiceCollection extensions
├── Grpc/                  # gRPC service implementations
├── HealthChecks/          # Health check endpoints
├── Initialization/        # Server startup
├── Middleware/            # HTTP middleware
├── ModuleLoading/         # Module process management
├── RealTime/              # SignalR hubs
├── Services/              # Server services
├── Supervisor/            # Module supervisor
├── Program.cs             # Entry point
```

### 2.6 `DotNetCloud.Core.ServiceDefaults` — Shared Infrastructure

**Files:** All `.cs` files under `src/Core/DotNetCloud.Core.ServiceDefaults/`

**Review Checklist:**

- ✓ **Serilog configuration:** Structured logging correctly set up. Sensitive data masking.
- ✓ **OpenTelemetry:** OTLP export configured. Traces, metrics, logs correlation.
- ✓ **Health checks:** Endpoint patterns (`/health`, `/health/ready`, `/health/live`). Database health check uses `DbConnection` (legitimate).
- ✓ **Middleware:** Security headers middleware (CSP, HSTS, X-Frame-Options) — review for headers only, not security policy.

### 2.7 `DotNetCloud.Core.Data.SqlServer` & `DotNetCloud.Core.Schema`

**Files:** All `.cs` files under both projects.

**Review Checklist:**

- ✓ **SQL Server config:** Provider-specific settings are correct
- ✓ **Schema utilities:** Schema generation/validation is functional
- ✓ **No dead code:** Both projects are actively used

### Phase 2 Output

Updated `/docs/CODE_REVIEW_FINDINGS.md` — **Core Review** section with:

- Per-project findings organized by checklist items
- Flagged issues with severity (Critical/High/Medium/Low)
- Specific file and line references for each finding
- Recommendations for each issue

---

## Phase 3: Module Reviews

Pattern-based review (automated analysis + targeted manual review of high-risk files).
Four tiers, executed sequentially. Within each tier, modules can be reviewed in parallel.

### Per-Module Review Template

For each of the 15 modules, review:

#### Core Project (`DotNetCloud.Modules.{Name}`)

- ✓ **Razor component structure:** Components are well-organized. No god-components.
- ✓ **Event handling:** Events are subscribed and unsubscribed correctly. No event leaks.
- ✓ **Capability usage:** Capability checks are present and correct.
- ✓ **UI consistency:** Uses `UI.Shared` components. Follows shared patterns.
- ✓ **Dependency clarity:** Clear dependency on Core interfaces. No hidden dependencies.

#### Data Project (`DotNetCloud.Modules.{Name}.Data`)

- ✓ **Entity models:** Entities are well-designed. No missing relationships.
- ✓ **DbContext:** Module-specific DbContext is clean. No cross-module entity references.
- ✓ **Query efficiency:** No N+1 queries. `Include`/`ThenInclude` used correctly. `AsNoTracking` for reads.
- ✓ **EF vs Raw SQL:** All data access uses EF Core. No raw SQL unless justified and documented.
- ✓ **Migrations:** Migrations are clean and up-to-date.
- ✓ **Async:** All database operations are async.

#### Host Project (`DotNetCloud.Modules.{Name}.Host`)

- ✓ **gRPC service implementations:** Services implement proto contracts correctly
- ✓ **Proto contracts:** Proto files follow conventions. Message types are well-designed.
- ✓ **Error handling:** gRPC status codes used correctly. Errors are informative.
- ✓ **Event publishing:** Events published through `IEventBus`. No direct cross-module calls.
- ✓ **Startup:** Host startup is clean. No duplicated boilerplate.

#### Cross-Cutting

- ✓ **Cross-module dependencies:** No direct database access to other modules. All communication through gRPC or events.
- ✓ **Event bus usage:** Events are the correct mechanism for the interaction. No tight coupling.
- ✓ **Capability tier enforcement:** Module capabilities are at the correct tier.

#### Test Coverage

- ✓ **Test project exists:** Every module has a test project (created for Bookmarks, Email, and About)
- ✓ **Tests are meaningful:** Not just placeholder/throwaway tests. Cover business logic.
- ✓ **Edge cases:** Tests cover error paths, boundary conditions, empty states.
- ✓ **Coverage:** Above project average. No 0% coverage files.

---

### Tier 1 — Core Feature Modules (Highest Priority)

| Module       | Core                         | Data          | Host          | Test           | Cross-Deps                |
| ------------ | ---------------------------- | ------------- | ------------- | -------------- | ------------------------- |
| **Files**    | DotNetCloud.Modules.Files    | Files.Data    | Files.Host    | Files.Tests    | Search.Client             |
| **Chat**     | DotNetCloud.Modules.Chat     | Chat.Data     | Chat.Host     | Chat.Tests     | Search.Client             |
| **Email**    | DotNetCloud.Modules.Email    | Email.Data    | Email.Host    | Email.Tests    | Search.Client             |
| **Calendar** | DotNetCloud.Modules.Calendar | Calendar.Data | Calendar.Host | Calendar.Tests | —                         |
| **Contacts** | DotNetCloud.Modules.Contacts | Contacts.Data | Contacts.Host | Contacts.Tests | Calendar.Data, Notes.Data |

### Tier 2 — Content Modules

| Module        | Core                          | Data           | Host           | Test            | Cross-Deps                         |
| ------------- | ----------------------------- | -------------- | -------------- | --------------- | ---------------------------------- |
| **Notes**     | DotNetCloud.Modules.Notes     | Notes.Data     | Notes.Host     | Notes.Tests     | Search.Client                      |
| **Tracks**    | DotNetCloud.Modules.Tracks    | Tracks.Data    | Tracks.Host    | Tracks.Tests    | Files (events), Chat (events)      |
| **Bookmarks** | DotNetCloud.Modules.Bookmarks | Bookmarks.Data | Bookmarks.Host | Bookmarks.Tests | Search.Client                      |
| **Search**    | DotNetCloud.Modules.Search    | Search.Data    | Search.Host    | Search.Tests    | (provides Search.Client to others) |

### Tier 3 — Media Modules

| Module     | Core                       | Data        | Host        | Test         | Cross-Deps     |
| ---------- | -------------------------- | ----------- | ----------- | ------------ | -------------- |
| **Photos** | DotNetCloud.Modules.Photos | Photos.Data | Photos.Host | Photos.Tests | Files (events) |
| **Music**  | DotNetCloud.Modules.Music  | Music.Data  | Music.Host  | Music.Tests  | Files (events) |
| **Video**  | DotNetCloud.Modules.Video  | Video.Data  | Video.Host  | Video.Tests  | Files (events) |

### Tier 4 — Utility Modules

| Module      | Core                        | Data         | Host         | Test          | Cross-Deps |
| ----------- | --------------------------- | ------------ | ------------ | ------------- | ---------- |
| **AI**      | DotNetCloud.Modules.AI      | AI.Data      | AI.Host      | AI.Tests      | —          |
| **About**   | DotNetCloud.Modules.About   | (none)       | About.Host   | About.Tests   | —          |
| **Example** | DotNetCloud.Modules.Example | Example.Data | Example.Host | Example.Tests | —          |

---

### Special Attention: Modules Without Tests (✅ Completed)

For **Bookmarks**, **Email**, and **About**, test projects were created during the review:

1. ✓ Created `Bookmarks.Tests` with `BookmarkFolderServiceTests.cs` and `BookmarkServiceTests.cs`
2. ✓ Created `Email.Tests` with `EmailAccountServiceTests.cs` and `EmailRuleServiceTests.cs`
3. ✓ Created `About.Tests` with `AboutHealthCheckTests.cs` and `AboutModuleTests.cs`
4. ✓ All tests follow Arrange-Act-Assert pattern and naming convention `MethodName_Condition_ExpectedResult`
5. ✓ `dotnet test` passes — 5,248+ tests across all 22 test projects

### Phase 3 Output

Updated `/docs/CODE_REVIEW_FINDINGS.md` — **Module Reviews** section with:

- Per-module findings organized by checklist items
- Flagged issues with severity and file/line references
- Test coverage gap analysis
- New test projects created for Bookmarks/Email/About

---

## Phase 4: Client, CLI & UI Reviews

All reviews in this phase can run in parallel.

### 4.1 `DotNetCloud.CLI` — Command-Line Interface

**Files:** All `.cs` files under `src/CLI/DotNetCloud.CLI/`

**Review Checklist:**

- ✓ **Command structure:** Commands follow consistent pattern. Help text is clear.
- ✓ **DatabaseSetupHelper.cs:** Raw SQL for database provisioning is justified (CREATE ROLE, CREATE DATABASE — can't be done via EF). SQL is parameterized.
- ✓ **Error handling:** CLI errors are user-friendly. Exit codes are meaningful.
- ✓ **Async:** CLI operations are properly async.

### 4.2 `DotNetCloud.Client.Core` — Client Core SDK

**Files:** All `.cs` files under `src/Clients/DotNetCloud.Client.Core/`

**Review Checklist:**

- ✓ **LocalStateDb.cs:** `ExecuteSqlRaw` for SQLite pragma is justified and minimal
- ✓ **Sync engine:** Sync logic is clear and correct
- ✓ **Conflict resolution:** Conflict handling is well-defined
- ✓ **Virtual files:** Virtual file system implementation
- ✓ **API client:** HTTP/gRPC client patterns are consistent
- ✓ **Auth:** Client auth flow (review patterns only, not security)

### 4.3 `DotNetCloud.Client.SyncTray` — Desktop Client

**Files:** All `.cs` files under `src/Clients/DotNetCloud.Client.SyncTray/`

**Review Checklist:**

- ✓ **Avalonia patterns:** MVVM is followed consistently
- ✓ **Tray icon:** System tray integration is clean
- ✓ **Notifications:** Notification system is well-structured
- ✓ **Startup:** Application startup is clear
- ✓ **Services:** Service registration and DI

### 4.4 `DotNetCloud.Client.Android` — Android Client

**Files:** All `.cs` files under `src/Clients/DotNetCloud.Client.Android/`

**Review Checklist:**

- ✓ **MAUI patterns:** MAUI conventions are followed
- ✓ **Platform code:** Platform-specific code is isolated in `Platforms/`
- ✓ **ViewModels:** ViewModels follow MVVM pattern
- ✓ **Services:** Service layer is well-structured

### 4.5 `DotNetCloud.Client.BrowserExtension` — Browser Extension

**Files:** TypeScript under `src/Clients/DotNetCloud.Client.BrowserExtension/`

**Review Checklist:**

- ✓ **TypeScript patterns:** Consistent TypeScript usage
- ✓ **Build pipeline:** `vite.config.ts` and `build-extension.ps1` are correct
- ✓ **Manifest:** Chrome and Firefox manifests are in sync
- ✓ **Tests:** Jest tests are meaningful

### 4.6 UI Projects

**Projects:** `DotNetCloud.UI.Web`, `DotNetCloud.UI.Web.Client`, `DotNetCloud.UI.Shared`, `DotNetCloud.UI.Android`

**Review Checklist:**

- ✓ **Shared components:** `UI.Shared` components are truly reusable
- ✓ **Component reuse:** Modules use shared components, not duplicates
- ✓ **Blazor patterns:** Component lifecycle is correct. No render issues.
- ✓ **WebAssembly:** Client-side code is properly separated from server-side
- ✓ **MAUI Blazor:** Android UI integration with Blazor components

### Phase 4 Output

Updated `/docs/CODE_REVIEW_FINDINGS.md` — **Client/CLI/UI Reviews** section with per-project findings.

---

## Phase 5: Consolidation & Final Report

### 5.1 Merge All Findings

Combine all findings from Phases 1-4 into the final document at `/docs/CODE_REVIEW_FINDINGS.md`.

### 5.2 Categorize by Severity

| Severity     | Criteria                                                                       | Examples                                                                     |
| ------------ | ------------------------------------------------------------------------------ | ---------------------------------------------------------------------------- |
| **Critical** | Unfinished code, broken patterns, missing essential tests                      | `NotImplementedException` in production code, 0% test coverage on core logic |
| **High**     | Poor efficiency, significant code duplication, missing XML docs on public APIs | N+1 queries in frequently-used endpoints, 40%+ duplication between modules   |
| **Medium**   | Style violations, readability issues, minor inconsistencies                    | Inconsistent naming, long methods (>50 lines), missing CancellationToken     |
| **Low**      | Cosmetic issues, naming preferences                                            | Whitespace, comment formatting, using statement order                        |

### 5.3 Prioritized Action Items

Create an ordered list of what to fix first, considering:

1. **Impact** — How many users/developers are affected?
2. **Risk** — What's the risk of not fixing it?
3. **Effort** — How much work is it to fix?

### 5.4 Per-Module Scorecards

For each module, generate a scorecard:

```
Module: Files
├── Coverage: 78% (Target: >80%) — PASS
├── Style Score: 95/100 (Target: >90) — PASS
├── TODO Count: 0 (Target: 0) — PASS
├── Raw SQL: 0 (Target: 0) — PASS
├── Duplication: 12% (Target: <15%) — PASS
├── Doc Coverage: 82% (Target: >80%) — PASS
├── Complexity Hotspots: 2 methods >15 cyclomatic complexity — FLAG
├── Overall: PASS (2 action items)
```

### 5.5 Verification

- ✓ `dotnet build` passes for entire solution — 0 errors, 0 warnings
- ✓ `dotnet test` passes for all test projects — 5,248+ tests, all passing
- ☐ Code coverage report — `coverlet.collector` not installed at review time; coverage tracking still pending setup
- ✓ All action items documented with severity, file references, and per-module scorecards

### Phase 5 Output

Final `/docs/CODE_REVIEW_FINDINGS.md` — complete consolidated review document with:

- Executive summary
- Metrics overview (all projects)
- Phase 2 findings (Core deep review)
- Phase 3 findings (Module reviews)
- Phase 4 findings (Clients/CLI/UI)
- Issue severity breakdown
- Prioritized action items
- Per-module scorecards
- Recommendations for next steps

---

## Execution Schedule

| Phase      | Duration (Est.)  | Parallel Work   | Sequential Dependency        |
| ---------- | ---------------- | --------------- | ---------------------------- |
| Phase 1    | ~1-2 hours       | All 8 steps     | None                         |
| Phase 2    | ~4-6 hours       | 2.3 + 2.4 + 2.7 | Must complete before Phase 3 |
| Phase 3 T1 | ~2-3 hours       | All 5 modules   | Must complete Phase 2        |
| Phase 3 T2 | ~2-3 hours       | All 4 modules   | After Tier 1                 |
| Phase 3 T3 | ~1-2 hours       | All 3 modules   | After Tier 2                 |
| Phase 3 T4 | ~1 hour          | All 3 modules   | After Tier 3                 |
| Phase 4    | ~2-3 hours       | All 6 reviews   | After Phase 3                |
| Phase 5    | ~1-2 hours       | None            | After Phase 4                |
| **Total**  | **~14-22 hours** |                 |                              |

---

## Verification Checklist

Before considering the code review complete:

- [ ] Phase 1: All 8 automated analyses run; metrics spreadsheet populated
- [ ] Phase 2: All 7 Core sub-reviews completed with documented findings
- [ ] Phase 3: All 15 modules reviewed (4 tiers); 3 new test projects created
- [ ] Phase 4: All 6 client/CLI/UI reviews completed with documented findings
- [ ] Phase 5: Consolidated report at `/docs/CODE_REVIEW_FINDINGS.md`
- [ ] `dotnet build` passes with no new warnings
- [ ] `dotnet test` passes with coverage report
- [ ] All action items documented with severity and file references
- [ ] Per-module scorecards generated

---

## Reference: Project Conventions

- **File-scoped namespaces** — enforced via `Directory.Build.props`
- **Nullable reference types** — enabled project-wide
- **TreatWarningsAsErrors** — enforced via `Directory.Build.props`
- **XML doc comments** — required on all public members
- **Test naming:** `MethodName_Condition_ExpectedResult`
- **Interface prefix:** `I`
- **Event suffix:** `Event`
- **Async suffix:** `Async`
- **Checkbox format:** `✓` (completed) / `☐` (pending) — never `[x]` / `[ ]`
