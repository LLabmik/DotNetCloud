# DotNetCloud Master Project Plan

> **Version:** 1.0

> **Created:** 2026-03-02

> **Purpose:** Comprehensive, persistent plan for all DotNetCloud implementation phases

> **Status Tracking:** Each step includes status (pending|in-progress|completed|failed|skipped)

> **Reference in Conversations:** Use step IDs like "phase-0.1" to reference specific work

---

## Quick Status Summary

| Phase                       | Steps   | Completed | In Progress | Pending |
| --------------------------- | ------- | --------- | ----------- | ------- |
| Pre-Implementation          | 2       | 2         | 0           | 0       |
| Phase 0.1                   | 11      | 11        | 0           | 0       |
| Phase 0.2                   | 12      | 12        | 0           | 0       |
| Phase 0.3                   | 8       | 8         | 0           | 0       |
| Phase 0.4                   | 20      | 20        | 0           | 0       |
| Phase 0.5                   | 9       | 9         | 0           | 0       |
| Phase 0.6                   | 14      | 14        | 0           | 0       |
| Phase 0.7                   | 16      | 16        | 0           | 0       |
| Phase 0.8                   | 11      | 11        | 0           | 0       |
| Phase 0.9                   | 13      | 13        | 0           | 0       |
| Phase 0.10                  | 11      | 11        | 0           | 0       |
| Phase 0.11                  | 18      | 18        | 0           | 0       |
| Phase 0.12                  | 25      | 25        | 0           | 0       |
| Phase 0.13                  | 20      | 20        | 0           | 0       |
| Phase 0.14                  | 18      | 18        | 0           | 0       |
| Phase 0.15                  | 12      | 12        | 0           | 0       |
| Phase 0.16                  | 12      | 12        | 0           | 0       |
| Phase 0.17                  | 10      | 10        | 0           | 0       |
| Phase 0.18                  | 8       | 8         | 0           | 0       |
| Phase 0.19                  | 11      | 11        | 0           | 0       |
| Phase 1.1                   | 6       | 6         | 0           | 0       |
| Phase 1.2                   | 5       | 5         | 0           | 0       |
| Phase 1.3                   | 15      | 15        | 0           | 0       |
| Phase 1.4                   | 15      | 15        | 0           | 0       |
| Phase 1.5                   | 10      | 10        | 0           | 0       |
| Phase 1.6                   | 9       | 9         | 0           | 0       |
| Phase 1.7                   | 11      | 11        | 0           | 0       |
| Phase 1.8                   | 8       | 8         | 0           | 0       |
| Phase 1.9                   | 14      | 14        | 0           | 0       |
| Phase 1.10                  | 24      | 24        | 0           | 0       |
| Phase 1.11                  | 8       | 8         | 0           | 0       |
| Phase 1.12                  | 17      | 17        | 0           | 0       |
| Phase 1.13                  | 4       | 4         | 0           | 0       |
| Phase 1.14                  | 32      | 32        | 0           | 0       |
| Phase 1.15                  | 25      | 25        | 0           | 0       |
| Phase 1.16                  | 20      | 20        | 0           | 0       |
| Phase 1.17                  | 25      | 25        | 0           | 0       |
| Phase 1.18                  | 6       | 6         | 0           | 0       |
| Phase 1.19                  | 20      | 20        | 0           | 0       |
| Phase 1.20                  | 20      | 20        | 0           | 0       |
| Phase 2.1                   | 6       | 6         | 0           | 0       |
| Phase 2.2                   | 4       | 4         | 0           | 0       |
| Phase 2.3                   | 7       | 7         | 0           | 0       |
| Phase 2.4                   | 5       | 5         | 0           | 0       |
| Phase 2.5                   | 4       | 4         | 0           | 0       |
| Phase 2.6                   | 4       | 4         | 0           | 0       |
| Phase 2.7                   | 4       | 4         | 0           | 0       |
| Phase 2.8                   | 11      | 11        | 0           | 0       |
| Phase 2.9                   | 3       | 3         | 0           | 0       |
| Phase 2.10                  | 10      | 10        | 0           | 0       |
| Phase 2.11                  | 3       | 3         | 0           | 0       |
| Phase 2.12                  | 2       | 2         | 0           | 0       |
| Phase 2.13                  | 3       | 3         | 0           | 0       |
| Integration Testing Sprint  | 3       | 3         | 0           | 0       |
| Sync Batch 1                | 10      | 10        | 0           | 0       |
| Sync Batch 2                | 6       | 6         | 0           | 0       |
| Sync Batch 3                | 6       | 6         | 0           | 0       |
| Sync Batch 4                | 5       | 5         | 0           | 0       |
| Sync Batch 5                | 2       | 2         | 0           | 0       |
| Sync Verification           | 1       | 1         | 0           | 0       |
| Sync Hardening P0           | 3       | 3         | 0           | 0       |
| Sync Hardening P1–P2        | 6       | 6         | 0           | 0       |
| Client Security Remediation | 1       | 1         | 0           | 0       |
| Phase 3.1                   | 4       | 4         | 0           | 0       |
| Phase 3.2                   | 6       | 6         | 0           | 0       |
| Phase 3.3                   | 6       | 6         | 0           | 0       |
| Phase 3.4                   | 6       | 6         | 0           | 0       |
| Phase 3.5                   | 4       | 4         | 0           | 0       |
| Phase 3.6                   | 4       | 4         | 0           | 0       |
| Phase 3.7                   | 5       | 5         | 0           | 0       |
| Phase 3.8                   | 4       | 4         | 0           | 0       |
| Phase 4.1                   | 11      | 11        | 0           | 0       |
| Phase 4.2                   | 7       | 7         | 0           | 0       |
| Phase 4.3                   | 21      | 21        | 0           | 0       |
| Phase 4.4                   | 17      | 17        | 0           | 0       |
| Phase 4.5                   | 9       | 9         | 0           | 0       |
| Phase 4.6                   | 4       | 4         | 0           | 0       |
| Phase 4.7                   | 6       | 6         | 0           | 0       |
| Phase 4.8                   | 8       | 8         | 0           | 0       |
| Phase 4.9                   | 42      | 42        | 0           | 0       |
| Phase 5-8                   | Summary | 10        | 0           | 0       |
| Phase 8 (Full-Text Search)  | 18      | 18        | 0           | 0       |
| Phase 7 (Video Calling)     | 11      | 11        | 0           | 0       |
| Phase 9                     | 7       | 5         | 0           | 2       |
| Phase 11 (Auto-Updates)     | 16      | 7         | 0           | 9       |
| DM & Host Calls — Phase A   | 3       | 3         | 0           | 0       |
| DM & Host Calls — Phase B   | 2       | 0         | 0           | 2       |
| DM & Host Calls — Phase C–G | 10      | 1         | 1           | 8       |
| Shared File Folders         | 6       | 6         | 0           | 0       |
| Infrastructure              | Summary | 0         | 0           | 1       |
| Documentation               | Summary | 0         | 0           | 1       |

Maintenance note: local install/setup health verification now follows configured Kestrel ports and accepts self-signed local HTTPS during startup checks. Fresh Linux installs now invoke `dotnetcloud setup --beginner` by default, which auto-selects the recommended local PostgreSQL path and then branches cleanly between the three real deployment shapes: private/local test, public behind a reverse proxy, and public served directly by DotNetCloud itself. The local branch uses self-signed HTTPS on DotNetCloud directly. The reverse-proxy public branch keeps DotNetCloud on local HTTP and ends with explicit reverse-proxy/TLS guidance instead of pretending automatic public-certificate setup exists; it now also points beginners to a dedicated Apache-first reverse-proxy guide with a Caddy alternative. The public-direct branch lets the user point DotNetCloud at an existing public certificate file and explains the extra tradeoffs, while still explicitly recommending a reverse proxy for most public installs because it simplifies ports 80/443, TLS renewal, and future services on the same machine. All branches print explicit direct local access URLs and health probe URLs and end with a plain-language summary of the selected defaults plus the beginner user's next steps. Upgrade runs now also end with a plain-language summary that confirms existing data/configuration were preserved, states clearly whether a one-time setup review is still required, and re-shows the access URLs plus the user's next step. This also clarifies the internal app defaults HTTP `5080` / HTTPS `5443` versus reverse-proxy/public HTTPS ports such as `15443`. Windows now has a separate IIS-first installation path via `tools/install-windows.ps1`, with IIS reverse proxying to `http://localhost:5080`, a beginner-focused IIS guide, a dedicated architecture rationale note, native Windows Service hosting support in the core server, and machine-level config/data environment propagation during setup and service runtime so Windows self-hosters do not need to follow the Linux installer path. The bare-metal redeploy helper now also repairs build-output ownership and purges stale normal and malformed Debug output trees before Release build/publish runs so local Linux redeploys do not inherit broken artifacts from prior attempts.

---

## Pre-Implementation Setup

### Step: pre-impl-1 - Repository & Project Structure Setup

**Status:** completed  
**Duration:** ~1-2 hours  
**Description:** Establish the foundational monorepo structure and configuration files

**Recommended Prompt:**

```
Execute phase pre-impl-1: Set up the repository structure and foundational configuration files.
Create the solution file, directory structure (src/Core/, src/Modules/, src/UI/, src/Clients/,
tests/, tools/, docs/), and configuration files (.gitignore, global.json, .editorconfig,
Directory.Build.props, Directory.Build.targets, NuGet.config). Also create LICENSE (AGPL-3.0),
README.md, CONTRIBUTING.md, and copilot instructions file.
```

**Tasks:**

- ✓ Initialize Git repository (if not already done)
- ✓ Create `.gitignore` for .NET projects
- ✓ Create solution file: `DotNetCloud.sln`
- ✓ Create directory structure: `src/Core/`, `src/Modules/`, `src/UI/`, `src/Clients/`, `tests/`, `tools/`, `docs/`
- ✓ Add LICENSE file (AGPL-3.0)
- ✓ Create comprehensive README.md with project vision
- ✓ Create CONTRIBUTING.md
- ✓ Add .github/copilot-instructions.md for AI contribution guidelines

**Dependencies:** None (starting point)  
**Blocking Issues:** None  
**Notes:** Foundation established. Ready for Phase 0.1.1

---

### Step: pre-impl-2 - Development Environment Setup

**Status:** completed  
**Duration:** ~1-2 hours  
**Description:** Set up local development environment and tools

**Recommended Prompt:**

```
Execute phase pre-impl-2: Set up the development environment. Install required tools (Visual Studio, .NET SDK, PostgreSQL, Docker), clone the repository, and build the solution.
Ensure all development dependencies are installed and configured (EF Core tools, Docker support, etc.).
Create a sample appsettings.Development.json for local configuration.
```

**Tasks:**

- ✓ Install Visual Studio 2022 (or later)
- ✓ Install .NET 10 SDK
- ✓ Install PostgreSQL 14 (or later)
- ✓ Install Docker Desktop
- ✓ Clone the repository
- ✓ Build the solution
- ✓ Install EF Core tools
- ✓ Configure Docker support in Visual Studio
- ✓ Create sample `appsettings.Development.json`

**Dependencies:** None  
**Blocking Issues:** None  
**Notes:** Development environment ready. Can now proceed with implementation Phases.

---

### Step: pre-impl-2 - Development Environment Documentation & Setup

**Status:** completed  
**Duration:** ~3-4 hours  
**Description:** Create comprehensive development environment guides and documentation

**Completed Deliverables:**
✅ **docs/development/IDE_SETUP.md** (1,800+ lines)

- Visual Studio 2022 installation, configuration, debugging, testing
- VS Code setup with C# Dev Kit and extensions
- JetBrains Rider setup and features
- EditorConfig enforcement across all IDEs
- Troubleshooting for IntelliSense, breakpoints, debugging

✅ **docs/development/DATABASE_SETUP.md** (1,600+ lines)

- PostgreSQL setup (Windows, Linux, macOS)
- SQL Server setup and configuration
- MariaDB setup and configuration
- Connection string formats for all three databases
- EF Core migrations and seeding
- Multi-database testing strategies
- Comprehensive troubleshooting guide

✅ **docs/development/DOCKER_SETUP.md** (1,400+ lines)

- Docker Desktop installation for all platforms
- docker-compose.yml configuration for all three databases
- Running databases in containers
- Application containerization with Dockerfile
- Local development workflows (databases in Docker, app local)
- Multi-database testing matrix for CI/CD
- Container debugging and troubleshooting

✅ **docs/development/DEVELOPMENT_WORKFLOW.md** (1,200+ lines)

- Git Flow branching strategy (main, develop, feature/_, bugfix/_, release/\*)
- Conventional Commits format with examples
- Pull request process and templates
- Code review standards and comment guidelines
- Testing requirements (80%+ coverage)
- Local development best practices
- Conflict resolution strategies
- Release process with semantic versioning

✅ **docs/development/README.md** (Index & Quick Start)

- Navigation guide linking all development docs
- Quick decision tree for getting started
- Common workflows and scripts
- Troubleshooting matrix
- Technology stack reference
- Key configuration files

**Tasks Completed:**

- ✓ Create comprehensive IDE setup guide (Visual Studio, VS Code, Rider)
- ✓ Create local development database setup guide (PostgreSQL, SQL Server, MariaDB)
- ✓ Document Docker setup for local testing and multi-database CI/CD
- ✓ Create development workflow guidelines (branching, commits, PRs, code review)
- ✓ Updated IMPLEMENTATION_CHECKLIST.md to mark all Development Environment Setup tasks as completed
- ✓ Updated MASTER_PROJECT_PLAN.md with completion status

**Documentation Location:** `/docs/development/`

**Dependencies:** pre-impl-1  
**Blocking Issues:** None  
**Notes:** All four critical development setup guides are complete and comprehensive. Developers can now get started with IDE setup, databases, Docker, and workflow guidelines. Total documentation: 5,000+ lines covering all platforms (Windows, Linux, macOS) and all supported databases (PostgreSQL, SQL Server, MariaDB). Ready for Phase 0.1 core implementation work.

---

## Phase 0: Foundation

### Section: Phase 0.1 - Core Abstractions & Interfaces

**STATUS:** ✅ COMPLETED (11/11 steps)
**DURATION:** ~11 hours
**DELIVERABLES:**

- ✓ Capability system with tier enforcement (ICapabilityInterface, CapabilityTier enum, public/restricted/privileged tier interfaces, forbidden interfaces list)
- ✓ Authorization context and models (CallerContext, CallerType, CapabilityRequest)
- ✓ Module system interfaces (IModuleManifest, IModule, IModuleLifecycle, ModuleInitializationContext)
- ✓ Event system interfaces (IEvent, IEventHandler<T>, IEventBus, EventSubscription model)
- ✓ Complete DTO layer (User, Organization, Team, Permission, Role, Module, Device, Settings DTOs)
- ✓ Standardized error handling (ErrorCodes constants, exception hierarchy, API error response models)
- ✓ Foundation for all subsequent phases established

---

#### Step: phase-0.1.1 - Capability System Interfaces

**Status:** completed
**Duration:** ~2-3 hours  
**Description:** Create the capability tier system and public/restricted/privileged interfaces

**Recommended Prompt:**

```
Execute phase-0.1.1: Create the DotNetCloud.Core project with the capability system.
Implement ICapabilityInterface marker interface, CapabilityTier enum (Public, Restricted, Privileged, Forbidden),
and these interfaces: IUserDirectory, ICurrentUserContext, INotificationService, IEventBus (public tier);
IStorageProvider, IModuleSettings, ITeamDirectory (restricted tier); IUserManager, IBackupProvider (privileged tier).
Include XML documentation for all types. Location: src/Core/DotNetCloud.Core/Capabilities/
```

**Deliverables:**

- ✓ `ICapabilityInterface` marker interface
- ✓ `CapabilityTier` enum (Public, Restricted, Privileged, Forbidden)
- ✓ Public tier interfaces:
  - ✓ `IUserDirectory`
  - ✓ `ICurrentUserContext`
  - ✓ `INotificationService`
  - ✓ `IEventBus`
- ✓ Restricted tier interfaces:
  - ✓ `IStorageProvider`
  - ✓ `IModuleSettings`
  - ✓ `ITeamDirectory`
- ✓ Privileged tier interfaces:
  - ✓ `IUserManager`
  - ✓ `IBackupProvider`

**File Location:** `src/Core/DotNetCloud.Core/Capabilities/`  
**Dependencies:** None  
**Testing:** Unit tests for tier enforcement  
**Notes:** This is a critical foundation - other systems depend on it

---

#### Step: phase-0.1.2 - Context & Authorization Models

**Status:** completed
**Duration:** ~1.5 hours  
**Description:** Create CallerContext, CallerType, and CapabilityRequest models

**Recommended Prompt:**

```
Execute phase-0.1.2: Create authorization context and models. Implement CallerContext record
(UserId, Roles, Type properties) with validation logic, CallerType enum (User, System, Module),
and CapabilityRequest model (capability name, required tier, optional description).
Location: src/Core/DotNetCloud.Core/Authorization/
```

**Deliverables:**

- ✓ `CallerContext` record with:
  - ✓ `Guid UserId` property
  - ✓ `IReadOnlyList<string> Roles` property
  - ✓ `CallerType Type` property
  - ✓ Validation logic
- ✓ `CallerType` enum (User, System, Module)
- ✓ `CapabilityRequest` model with capability name, required tier, optional description

**File Location:** `src/Core/DotNetCloud.Core/Authorization/`  
**Dependencies:** phase-0.1.1  
**Testing:** Unit tests for validation  
**Notes:** Used throughout the codebase for authorization checks

---

#### Step: phase-0.1.3 - Module System Interfaces

**Status:** completed
**Duration:** ~1.5 hours  
**Description:** Create IModuleManifest and IModule interfaces

**Deliverables:**

- ✓ `IModuleManifest` interface with properties: Id, Name, Version, RequiredCapabilities, PublishedEvents, SubscribedEvents
- ✓ `IModule` base interface with: Manifest property, InitializeAsync(), StartAsync(), StopAsync()
- ✓ `IModuleLifecycle` interface with: InitializeAsync(), StartAsync(), StopAsync(), DisposeAsync()
- ✓ Module initialization context (ModuleInitializationContext record)

**File Location:** `src/Core/DotNetCloud.Core/Modules/`  
**Dependencies:** phase-0.1.1 (capability system)  
**Testing:** Unit tests for manifest validation  
**Notes:** Foundational for module loading system. Interfaces enable dynamic module discovery, validation of capabilities at load time, and event subscription management. ModuleInitializationContext provides modules with service provider, configuration, and system caller context.

---

#### Step: phase-0.1.4 - Event System Interfaces

**Status:** completed
**Duration:** ~1.5 hours  
**Description:** Create IEvent, IEventHandler, and IEventBus interfaces

**Recommended Prompt:**

```
Execute phase-0.1.4: Create event system interfaces. Implement IEvent base interface,
IEventHandler<TEvent> generic interface with Task HandleAsync(TEvent @event) method,
and IEventBus interface with methods: Task PublishAsync<TEvent>, Task SubscribeAsync<TEvent>,
Task UnsubscribeAsync<TEvent>. Also create event subscription model.
Location: src/Core/DotNetCloud.Core/Events/
```

**Deliverables:**

- ✓ `IEvent` base interface
- ✓ `IEventHandler<TEvent>` interface with `Task HandleAsync(TEvent @event)` method
- ✓ `IEventBus` interface with: PublishAsync, SubscribeAsync, UnsubscribeAsync
- ✓ Event subscription model

**File Location:** `src/Core/DotNetCloud.Core/Events/`  
**Dependencies:** phase-0.1.1 (for capability-aware event filtering)  
**Testing:** Unit tests for event subscription/publishing  
**Notes:** Critical for inter-module communication

---

#### Step: phase-0.1.5 - Data Transfer Objects (DTOs)

**Status:** completed
**Duration:** ~2 hours  
**Description:** Create DTO classes for all core domain entities

**Recommended Prompt:**

```
Execute phase-0.1.5: Create data transfer object classes. Implement User DTOs (UserDto, CreateUserDto,
UpdateUserDto), Organization DTOs, Team DTOs, Permission DTOs (PermissionDto, RoleDto), Module DTOs
(ModuleDto, InstalledModuleDto), Device DTOs, and Settings DTOs (SystemSettingDto, OrganizationSettingDto,
UserSettingDto). All should have proper properties and JSON serialization attributes.
Location: src/Core/DotNetCloud.Core/DTOs/
```

**Deliverables:**

- ✓ User DTOs: UserDto, CreateUserDto, UpdateUserDto
- ✓ Organization DTOs: OrganizationDto, CreateOrganizationDto, UpdateOrganizationDto
- ✓ Team DTOs: TeamDto, CreateTeamDto, UpdateTeamDto, TeamMemberDto, AddTeamMemberDto
- ✓ Permission DTOs: PermissionDto, CreatePermissionDto, RoleDto, CreateRoleDto, UpdateRoleDto
- ✓ Module DTOs: ModuleDto, CreateModuleDto, ModuleCapabilityGrantDto, GrantModuleCapabilityDto
- ✓ Device DTOs: UserDeviceDto, RegisterUserDeviceDto, UpdateUserDeviceDto
- ✓ Settings DTOs: SystemSettingDto, OrganizationSettingDto, UserSettingDto, UpsertSystemSettingDto, UpsertOrganizationSettingDto, UpsertUserSettingDto, SettingsBulkDto

**File Location:** `src/Core/DotNetCloud.Core/DTOs/`  
**Dependencies:** None  
**Testing:** Basic structure validation tests  
**Notes:** Used throughout API layer for serialization. Comprehensive DTOs cover Create, Read, Update operations.

---

#### Step: phase-0.1.6 - Error Handling & Exceptions

**Status:** completed
**Duration:** ~1 hour  
**Description:** Create standardized exception types and error response models

**Recommended Prompt:**

```
Execute phase-0.1.6: Create exception hierarchy and error models. Define error code constants class,
implement exception types (CapabilityNotGrantedException, ModuleNotFoundException, UnauthorizedException,
ValidationException, ForbiddenException, NotFoundException, ConcurrencyException), and create API error response models
with code, message, and details properties. Include XML documentation.
Location: src/Core/DotNetCloud.Core/Errors/
```

**Deliverables:**

- ✓ Error code constants class (70+ error codes)
- ✓ Exception types:
  - ✓ `CapabilityNotGrantedException`
  - ✓ `ModuleNotFoundException`
  - ✓ `UnauthorizedException`
  - ✓ `ValidationException`
  - ✓ `ForbiddenException`
  - ✓ `NotFoundException`
  - ✓ `ConcurrencyException`
  - ✓ `InvalidOperationException`
- ✓ `ApiErrorResponse` model with code, message, details, path, timestamp, traceId
- ✓ `ApiSuccessResponse<T>` generic model with data and pagination support
- ✓ `PaginationInfo` model for paginated responses

**File Location:** `src/Core/DotNetCloud.Core/Errors/`  
**Dependencies:** None  
**Testing:** Unit tests for exception properties and response creation  
**Notes:** Used globally for consistent error handling. All exception types inherit from DotNetCloudException base class.

---

#### Step: phase-0.1.7 - Core Abstractions Unit Tests

**Status:** completed
**Duration:** ~2 hours  
**Description:** Create comprehensive unit test suite for all Phase 0.1 interfaces

**Recommended Prompt:**

```
Execute phase-0.1.7: Create comprehensive unit tests for Phase 0.1. Write tests for capability
tier enforcement, CallerContext validation, module manifest validation, event bus interface contracts,
and exception creation. Aim for 80%+ code coverage. Use MSTEST and Moq.
Location: tests/DotNetCloud.Core.Tests/
```

**Deliverables:**

- ✓ Capability system tests
- ✓ CallerContext validation tests
- ✓ Module manifest validation tests
- ✓ Event bus interface contract tests
- ✓ Exception creation tests

**File Location:** `tests/DotNetCloud.Core.Tests/`  
**Dependencies:** phase-0.1.1 through phase-0.1.6  
**Testing:** Min 80% code coverage for abstractions  
**Notes:** Should run clean before moving to Phase 0.2

---

#### Step: phase-0.1.8 - Document Core Abstractions

**Status:** completed ✅
**Duration:** ~2 hours
**Deliverables:**

- ✓ `docs/architecture/core-abstractions.md` created with comprehensive documentation
  - ✓ Capability system design with all four tiers (Public, Restricted, Privileged, Forbidden)
  - ✓ Real-world capability examples and usage patterns
  - ✓ Capability tier approval workflows
  - ✓ Module system design with complete lifecycle documentation
  - ✓ Module lifecycle state transitions and guarantees
  - ✓ Example module implementations
  - ✓ Event system design with pub/sub patterns
  - ✓ Event choreography and event sourcing patterns
  - ✓ Authorization and caller context patterns
  - ✓ Cross-module integration example (Chat module)
  - ✓ Best practices for each abstraction
- ✓ XML documentation comments added to all public types in Core project
  - ✓ `ICapabilityInterface` — marker interface with design patterns
  - ✓ `CapabilityTier` — comprehensive enum documentation with approval flows
  - ✓ `IModuleManifest` — detailed interface with validation rules and examples
  - ✓ `IModule` — complete lifecycle documentation with code samples
  - ✓ `IModuleLifecycle` — disposal interface documentation
  - ✓ `IEvent` — event contract with design principles
  - ✓ `IEventHandler<T>` — handler implementation patterns and best practices
  - ✓ `IEventBus` — pub/sub semantics and usage patterns
  - ✓ `CallerContext` — authorization context with role patterns
  - ✓ `CallerType` — caller type enum with decision trees
  - ✓ `ModuleInitializationContext` — initialization patterns and configuration access
- ✓ `src/Core/DotNetCloud.Core/README.md` created with
  - ✓ Quick start guide for module developers
  - ✓ 5-step example implementation
  - ✓ Reference for all capability interfaces
  - ✓ Project file structure documentation
  - ✓ Development guidelines and best practices
  - ✓ Contribution guidelines specific to Core
  - ✓ Links to related documentation

**Quality Metrics:**

- All public types have comprehensive XML documentation (300+ lines added)
- Build passes with no compiler warnings
- Documentation includes 15+ code examples
- All tier levels documented with real examples
- Best practices documented for each abstraction

**Notes:** Phase 0.1 abstractions fully documented. Core developers and module implementers have complete reference for all foundational types. XML comments enable IntelliSense support in IDEs.

---

### Section: Phase 0.2 - Database & Data Access Layer

#### Step: phase-0.2.1 - Multi-Database Provider Strategy

**Status:** completed ✅
**Duration:** ~1.5 hours  
**Description:** Design and implement multi-database support abstraction

**Deliverables:**

- ✓ `IDbContextFactory<CoreDbContext>` abstraction
- ✓ `ITableNamingStrategy` interface
- ✓ `DatabaseProvider` enum (PostgreSQL, SqlServer, MariaDB)
- ✓ `PostgreSqlNamingStrategy` (schemas: `core.*`, `files.*`, etc.)
  - ✓ Schema-based organization using lowercase module names
  - ✓ Snake_case naming for tables and columns
  - ✓ Provider-specific index, FK, and constraint naming
- ✓ `SqlServerNamingStrategy` (schemas: `[core]`, `[files]`, etc.)
  - ✓ Schema-based organization using lowercase module names in brackets
  - ✓ PascalCase naming for tables and columns
  - ✓ Provider-specific index, FK, and constraint naming
- ✓ `MariaDbNamingStrategy` (table prefixes: `core_*`, `files_*`, etc.)
  - ✓ Table prefix-based organization for databases without schema support
  - ✓ Snake_case naming for tables and columns
  - ✓ Identifier truncation support for MySQL 64-character limit
- ✓ `DatabaseProviderDetector` with provider detection from connection string
- ✓ `DefaultDbContextFactory` implementation
- ✓ `CoreDbContext` skeleton with naming strategy integration
- ✓ Comprehensive README with usage examples

**Quality Metrics:**

- All classes have XML documentation
- Provider detection supports all three database types
- Factory pattern enables easy context creation
- Build passes with no errors
- Ready for entity model configuration (phase-0.2.2)

**File Location:** `src/Core/DotNetCloud.Core.Data/`  
**Dependencies:** None  
**Blocking Issues:** None  
**Notes:** Multi-database support foundation complete. Enables identical codebase across PostgreSQL, SQL Server, and MariaDB. Factory and naming strategies automatically handle provider-specific requirements.

---

#### Step: phase-0.2.2 - Identity Models (ASP.NET Core Identity)

**Status:** completed ✅  
**Duration:** ~2 hours  
**Description:** Create ApplicationUser and ApplicationRole entities

**Recommended Prompt:**

```
Execute phase-0.2.2: Create ASP.NET Core Identity models. Implement ApplicationUser entity extending
IdentityUser<Guid> with properties: DisplayName, AvatarUrl, Locale, Timezone, CreatedAt, LastLoginAt,
IsActive. Implement ApplicationRole extending IdentityRole<Guid> with properties: Description,
IsSystemRole. Configure Identity relationships. Use fluent API configuration.
Location: src/Core/DotNetCloud.Core.Data/Entities/Identity/
```

**Deliverables:**

- ✓ `ApplicationUser` entity extending `IdentityUser<Guid>`:
  - ✓ DisplayName (required, max 200 chars)
  - ✓ AvatarUrl (optional, max 500 chars)
  - ✓ Locale (required, default "en-US", max 10 chars)
  - ✓ Timezone (required, default "UTC", max 50 chars)
  - ✓ CreatedAt (required, auto-set)
  - ✓ LastLoginAt (optional)
  - ✓ IsActive (required, default true)
- ✓ `ApplicationRole` entity extending `IdentityRole<Guid>`:
  - ✓ Description (optional, max 500 chars)
  - ✓ IsSystemRole (required, default false)
- ✓ `ApplicationUserConfiguration` with fluent API:
  - ✓ Property configurations with max lengths
  - ✓ Default values
  - ✓ Indexes on DisplayName, Email, IsActive, LastLoginAt
- ✓ `ApplicationRoleConfiguration` with fluent API:
  - ✓ Property configurations
  - ✓ Indexes on IsSystemRole and Name
- ✓ `CoreDbContext` updated to extend `IdentityDbContext<ApplicationUser, ApplicationRole, Guid>`
- ✓ Identity model configuration applied in ConfigureIdentityModels()
- ✓ Microsoft.AspNetCore.Identity.EntityFrameworkCore package added
- ✓ Comprehensive unit tests created:
  - ✓ ApplicationUserTests (12 test methods)
  - ✓ ApplicationRoleTests (10 test methods)
  - ✓ All 22 tests passing
  - ✓ Test project created: DotNetCloud.Core.Data.Tests

**File Locations:**

- `src/Core/DotNetCloud.Core.Data/Entities/Identity/ApplicationUser.cs`
- `src/Core/DotNetCloud.Core.Data/Entities/Identity/ApplicationRole.cs`
- `src/Core/DotNetCloud.Core.Data/Configuration/Identity/ApplicationUserConfiguration.cs`
- `src/Core/DotNetCloud.Core.Data/Configuration/Identity/ApplicationRoleConfiguration.cs`
- `src/Core/DotNetCloud.Core.Data/Context/CoreDbContext.cs` (updated)
- `tests/DotNetCloud.Core.Data.Tests/Entities/Identity/ApplicationUserTests.cs`
- `tests/DotNetCloud.Core.Data.Tests/Entities/Identity/ApplicationRoleTests.cs`

**Dependencies:** phase-0.2.1 ✅  
**Testing:** ✅ All unit tests passing (22/22)  
**Build Status:** ✅ Solution builds successfully  
**Notes:** Identity models complete with proper Guid primary keys, comprehensive XML documentation, and full test coverage. CoreDbContext now properly extends IdentityDbContext with multi-database naming strategy support. MariaDB support temporarily disabled (Pomelo package awaiting .NET 10 update). Ready for phase-0.2.3 (Organization Hierarchy Models).

---

#### Step: phase-0.2.3 - Organization Hierarchy Models

**Status:** completed ✅
**Duration:** ~2.5 hours  
**Description:** Create Organization, Team, and related hierarchy entities

**Recommended Prompt:**

```
Execute phase-0.2.3: Create organization hierarchy entities. Implement Organization entity (Name,
Description, CreatedAt, soft-delete with IsDeleted/DeletedAt), Team entity (OrganizationId FK,
Name, soft-delete), TeamMember entity (TeamId, UserId, RoleIds), Group entity (OrganizationId,
Name), GroupMember entity (GroupId, UserId), and OrganizationMember entity (OrganizationId, UserId,
RoleIds). Include all relationships and foreign keys. Add unit tests for relationships.
Location: src/Core/DotNetCloud.Core.Data/Entities/Organizations/
```

**Deliverables:**

- ✓ `Organization` entity with:
  - ✓ Name, Description, CreatedAt properties
  - ✓ Soft-delete support (IsDeleted, DeletedAt)
  - ✓ Navigation properties for Teams, Groups, Members, Settings
  - ✓ Comprehensive XML documentation
- ✓ `Team` entity with:
  - ✓ OrganizationId FK
  - ✓ Name, Description, CreatedAt properties
  - ✓ Soft-delete support
  - ✓ Navigation properties for Organization and Members
- ✓ `TeamMember` entity with:
  - ✓ Composite key (TeamId, UserId)
  - ✓ RoleIds collection for team-scoped roles (JSON serialized)
  - ✓ JoinedAt timestamp
  - ✓ Navigation properties for Team and User
- ✓ `Group` entity with:
  - ✓ OrganizationId FK
  - ✓ Name, Description, CreatedAt properties
  - ✓ Soft-delete support
  - ✓ Navigation properties for Organization and Members
- ✓ `GroupMember` entity with:
  - ✓ Composite key (GroupId, UserId)
  - ✓ AddedAt timestamp
  - ✓ AddedByUserId for audit tracking
  - ✓ Navigation properties for Group, User, and AddedByUser
- ✓ `OrganizationMember` entity with:
  - ✓ Composite key (OrganizationId, UserId)
  - ✓ RoleIds collection for org-scoped roles (JSON serialized)
  - ✓ JoinedAt timestamp
  - ✓ InvitedByUserId for audit tracking
  - ✓ IsActive flag
  - ✓ Navigation properties for Organization, User, and InvitedByUser
- ✓ EF Core fluent API configurations for all entities:
  - ✓ OrganizationConfiguration with soft-delete query filter
  - ✓ TeamConfiguration with soft-delete query filter
  - ✓ TeamMemberConfiguration with JSON serialization for RoleIds
  - ✓ GroupConfiguration with soft-delete query filter
  - ✓ GroupMemberConfiguration
  - ✓ OrganizationMemberConfiguration with JSON serialization for RoleIds
  - ✓ All indexes, constraints, and relationships properly configured
- ✓ CoreDbContext updated with 6 new DbSets
- ✓ Comprehensive unit tests (67 tests passing):
  - ✓ OrganizationTests (10 tests)
  - ✓ TeamTests (10 tests)
  - ✓ TeamMemberTests (11 tests)
  - ✓ GroupTests (12 tests)
  - ✓ GroupMemberTests (12 tests)
  - ✓ OrganizationMemberTests (12 tests)

**Quality Metrics:**

- All entities have comprehensive XML documentation
- All navigation properties properly configured
- Composite keys correctly defined
- Soft-delete query filters applied
- JSON serialization for RoleIds collections
- Build passes with no errors
- All 67 unit tests passing
- Follows established naming conventions

**File Locations:**

- `src/Core/DotNetCloud.Core.Data/Entities/Organizations/*.cs` (6 entity files)
- `src/Core/DotNetCloud.Core.Data/Configuration/Organizations/*.cs` (6 configuration files)
- `tests/DotNetCloud.Core.Data.Tests/Entities/Organizations/*.cs` (6 test files)

**Dependencies:** phase-0.2.2 (ApplicationUser) ✅  
**Testing:** ✅ All entity relationship tests passing (67/67)  
**Build Status:** ✅ Solution builds successfully  
**Notes:** Organization hierarchy complete with comprehensive three-tier role system (organization-scoped, team-scoped, and group-based permissions). Supports multi-tenancy, soft-deletion, and full audit tracking. Ready for phase-0.2.4 (Permissions System Models).

---

#### Step: phase-0.2.4 - Permissions System Models

**Status:** completed ✅
**Duration:** ~1.5 hours  
**Description:** Create Permission, Role, and RolePermission junction entities

**Completed Deliverables:**

- ✓ `Permission` entity with Code, DisplayName, Description properties
  - Unique constraint on Code (hierarchical naming convention like "files.upload")
  - Navigation property to RolePermission collection
  - Maximum length constraints and comprehensive documentation
- ✓ `Role` entity with Name, Description, IsSystemRole properties
  - Unique constraint on Name
  - Navigation property to RolePermission collection
  - Supports system roles (immutable) and custom roles (mutable)
  - Index on IsSystemRole for filtering system vs. custom roles
- ✓ `RolePermission` junction table with composite primary key (RoleId, PermissionId)
  - Proper foreign key relationships with cascade delete
  - Indexes for efficient querying
  - Fluent API configuration with constraint naming

**Configurations Implemented:**

- ✓ `PermissionConfiguration` class (IEntityTypeConfiguration<Permission>)
- ✓ `RoleConfiguration` class (IEntityTypeConfiguration<Role>)
- ✓ `RolePermissionConfiguration` class (IEntityTypeConfiguration<RolePermission>)
- ✓ CoreDbContext updated with DbSet properties and ConfigurePermissionModels implementation

**File Location:** `src/Core/DotNetCloud.Core.Data/Entities/Permissions/`  
**Dependencies:** phase-0.2.3 (Organization hierarchy)  
**Testing:** Junction table relationship tests  
**Build Status:** ✅ Solution builds successfully  
**Notes:** Enables flexible RBAC system. Permission, Role, and RolePermission entities complete with all configurations. Ready for phase-0.2.5 (Settings Models).

---

#### Step: phase-0.2.5 - Settings Models (Three Scopes)

**Status:** completed ✅
**Duration:** ~1.5 hours  
**Description:** Create SystemSetting, OrganizationSetting, UserSetting entities for three-level configuration hierarchy

**Completed Deliverables:**

- ✓ `SystemSetting` entity with:
  - ✓ `string Module` property (composite key part 1, max 100 chars)
  - ✓ `string Key` property (composite key part 2, max 200 chars)
  - ✓ `string Value` property (JSON serializable, max 10,000 chars)
  - ✓ `DateTime UpdatedAt` property (auto-updated timestamp)
  - ✓ `string? Description` property (optional, max 500 chars)
  - ✓ Composite primary key: (Module, Key)
  - ✓ Comprehensive XML documentation with usage examples
- ✓ `OrganizationSetting` entity with:
  - ✓ `Guid Id` primary key
  - ✓ `Guid OrganizationId` FK
  - ✓ `string Key` property (max 200 chars)
  - ✓ `string Value` property (JSON serializable, max 10,000 chars)
  - ✓ `string Module` property (max 100 chars)
  - ✓ `DateTime UpdatedAt` property (auto-updated timestamp)
  - ✓ `string? Description` property (optional, max 500 chars)
  - ✓ Unique constraint: (OrganizationId, Module, Key)
  - ✓ Cascade delete on Organization
  - ✓ Comprehensive XML documentation
- ✓ `UserSetting` entity with:
  - ✓ `Guid Id` primary key
  - ✓ `Guid UserId` FK
  - ✓ `string Key` property (max 200 chars)
  - ✓ `string Value` property (JSON serializable, max 10,000 chars)
  - ✓ `string Module` property (max 100 chars)
  - ✓ `DateTime UpdatedAt` property (auto-updated timestamp)
  - ✓ `string? Description` property (optional, max 500 chars)
  - ✓ `bool IsEncrypted` property (flag for sensitive data)
  - ✓ Unique constraint: (UserId, Module, Key)
  - ✓ Cascade delete on ApplicationUser
  - ✓ Comprehensive XML documentation

**EF Core Configurations:**

- ✓ `SystemSettingConfiguration` (IEntityTypeConfiguration<SystemSetting>)
  - ✓ Composite primary key configuration
  - ✓ Column naming (snake_case)
  - ✓ Indexes on Module and UpdatedAt
  - ✓ Database timestamp defaults
- ✓ `OrganizationSettingConfiguration` (IEntityTypeConfiguration<OrganizationSetting>)
  - ✓ Primary key and foreign key configuration
  - ✓ Unique constraint on (OrganizationId, Module, Key)
  - ✓ Indexes for efficient querying
  - ✓ Cascade delete behavior
  - ✓ Column naming and defaults
- ✓ `UserSettingConfiguration` (IEntityTypeConfiguration<UserSetting>)
  - ✓ Primary key and foreign key configuration
  - ✓ Unique constraint on (UserId, Module, Key)
  - ✓ Indexes for efficient querying
  - ✓ IsEncrypted flag support
  - ✓ Cascade delete behavior
  - ✓ Column naming and defaults

**CoreDbContext Updates:**

- ✓ Added DbSet<SystemSetting> with XML documentation
- ✓ Added DbSet<OrganizationSetting> with XML documentation
- ✓ Added DbSet<UserSetting> with XML documentation
- ✓ Updated ConfigureSettingModels() method to apply all three configurations
- ✓ Added using statements for Settings entities and configurations

**Quality Metrics:**

- ✓ All entities have comprehensive XML documentation (900+ lines total)
- ✓ All configurations follow established EF Core patterns
- ✓ Build successful with no compiler errors or warnings
- ✓ Three-level settings hierarchy properly designed:
  - System-wide settings with module namespace
  - Organization-scoped settings (override system)
  - User-scoped settings (override organization/system)
- ✓ Proper cascade delete configuration
- ✓ Unique constraints prevent duplicate settings
- ✓ Encryption support flagged for UserSetting sensitive data

**File Locations:**

- `src/Core/DotNetCloud.Core.Data/Entities/Settings/SystemSetting.cs`
- `src/Core/DotNetCloud.Core.Data/Entities/Settings/OrganizationSetting.cs`
- `src/Core/DotNetCloud.Core.Data/Entities/Settings/UserSetting.cs`
- `src/Core/DotNetCloud.Core.Data/Configuration/Settings/SystemSettingConfiguration.cs`
- `src/Core/DotNetCloud.Core.Data/Configuration/Settings/OrganizationSettingConfiguration.cs`
- `src/Core/DotNetCloud.Core.Data/Configuration/Settings/UserSettingConfiguration.cs`
- `src/Core/DotNetCloud.Core.Data/Context/CoreDbContext.cs` (updated)

**Dependencies:** phase-0.2.2 (ApplicationUser), phase-0.2.3 (Organization) ✅  
**Testing:** Ready for integration tests in phase-0.2.12  
**Build Status:** ✅ Solution builds successfully  
**Notes:** Three-level settings system complete enabling flexible configuration at system, organization, and user scopes. Composite keys for SystemSetting provide efficient namespace organization. UserSetting includes encryption support for sensitive preferences. All relationships properly configured with cascade delete. Ready for phase-0.2.6 (Device & Module Registry Models).

---

#### Step: phase-0.2.6 - Device & Module Registry Models

**Status:** completed ✅
**Duration:** ~1.5 hours  
**Description:** Create UserDevice, InstalledModule, and ModuleCapabilityGrant entities

**Recommended Prompt:**

```
Execute phase-0.2.6: Create device and module registry entities. Implement UserDevice entity
(UserId, Name, DeviceType, PushToken, LastSeenAt), InstalledModule entity (ModuleId PK, Version,
Status, InstalledAt), and ModuleCapabilityGrant entity (ModuleId FK, CapabilityName, GrantedAt,
GrantedByUserId). Include all relationships and indexes for efficient querying.
Location: src/Core/DotNetCloud.Core.Data/Entities/Modules/
```

**Completed Deliverables:**

- ✓ `UserDevice` entity with:
  - ✓ `Guid Id` primary key (auto-generated)
  - ✓ `Guid UserId` FK to ApplicationUser
  - ✓ `string Name` property (max 200 chars, e.g., "Windows Laptop")
  - ✓ `string DeviceType` property (max 50 chars: Desktop, Mobile, Tablet, Web, CLI)
  - ✓ `string? PushToken` property (max 500 chars, nullable for FCM/APNs/UnifiedPush)
  - ✓ `DateTime LastSeenAt` property (presence tracking, stale device cleanup)
  - ✓ `DateTime CreatedAt` property (auto-set)
  - ✓ Navigation property to ApplicationUser
  - ✓ Comprehensive XML documentation with usage patterns and examples
- ✓ `InstalledModule` entity with:
  - ✓ `string ModuleId` primary key (max 200 chars, natural key, e.g., "dotnetcloud.files")
  - ✓ `string Version` property (max 50 chars, semantic versioning support)
  - ✓ `string Status` property (max 50 chars: Enabled, Disabled, UpdateAvailable, Failed, Installing, Uninstalling, Updating)
  - ✓ `DateTime InstalledAt` property (immutable, preserved across updates)
  - ✓ `DateTime UpdatedAt` property (auto-updated on version/status changes)
  - ✓ Navigation property to CapabilityGrants collection
  - ✓ Comprehensive XML documentation with lifecycle state transitions
- ✓ `ModuleCapabilityGrant` entity with:
  - ✓ `Guid Id` primary key (auto-generated)
  - ✓ `string ModuleId` FK to InstalledModule (max 200 chars)
  - ✓ `string CapabilityName` property (max 200 chars, e.g., "IStorageProvider")
  - ✓ `DateTime GrantedAt` property (immutable audit timestamp)
  - ✓ `Guid? GrantedByUserId` FK to ApplicationUser (nullable for system-granted)
  - ✓ Navigation properties to InstalledModule and ApplicationUser
  - ✓ Comprehensive XML documentation with capability tier explanations
- ✓ `UserDeviceConfiguration` (IEntityTypeConfiguration<UserDevice>):
  - ✓ Primary key and property configurations
  - ✓ Indexes on UserId, LastSeenAt, and (UserId, DeviceType)
  - ✓ Foreign key to ApplicationUser with cascade delete
  - ✓ Column naming via ITableNamingStrategy
- ✓ `InstalledModuleConfiguration` (IEntityTypeConfiguration<InstalledModule>):
  - ✓ Natural key (ModuleId) configuration
  - ✓ Property configurations with max lengths
  - ✓ Indexes on Status and InstalledAt
  - ✓ One-to-many relationship to CapabilityGrants with cascade delete
  - ✓ Column naming via ITableNamingStrategy
- ✓ `ModuleCapabilityGrantConfiguration` (IEntityTypeConfiguration<ModuleCapabilityGrant>):
  - ✓ Primary key and property configurations
  - ✓ Unique constraint on (ModuleId, CapabilityName)
  - ✓ Indexes on ModuleId, CapabilityName, and GrantedByUserId
  - ✓ Foreign key to InstalledModule with cascade delete
  - ✓ Foreign key to ApplicationUser with restrict delete (preserve audit trail)
  - ✓ Column naming via ITableNamingStrategy
- ✓ `CoreDbContext` updated with:
  - ✓ DbSet<UserDevice> with XML documentation
  - ✓ DbSet<InstalledModule> with XML documentation
  - ✓ DbSet<ModuleCapabilityGrant> with XML documentation
  - ✓ ConfigureDeviceModels() implementation applying UserDeviceConfiguration
  - ✓ ConfigureModuleModels() implementation applying InstalledModule and ModuleCapabilityGrant configurations
  - ✓ Using statements for Modules entities and configurations

**Quality Metrics:**

- ✓ All entities have comprehensive XML documentation (2,000+ lines total)
- ✓ All configurations follow established EF Core patterns
- ✓ Build successful with no compiler errors or warnings
- ✓ Device tracking system properly designed with presence monitoring
- ✓ Module lifecycle states documented with transition flows
- ✓ Capability-based security model enforced at database level
- ✓ Proper cascade delete configuration (UserDevice, InstalledModule → CapabilityGrants)
- ✓ Audit trail preservation (ModuleCapabilityGrant.GrantedByUserId with restrict delete)
- ✓ Unique constraint prevents duplicate capability grants per module

**File Locations:**

- `src/Core/DotNetCloud.Core.Data/Entities/Modules/UserDevice.cs`
- `src/Core/DotNetCloud.Core.Data/Entities/Modules/InstalledModule.cs`
- `src/Core/DotNetCloud.Core.Data/Entities/Modules/ModuleCapabilityGrant.cs`
- `src/Core/DotNetCloud.Core.Data/Configuration/Modules/UserDeviceConfiguration.cs`
- `src/Core/DotNetCloud.Core.Data/Configuration/Modules/InstalledModuleConfiguration.cs`
- `src/Core/DotNetCloud.Core.Data/Configuration/Modules/ModuleCapabilityGrantConfiguration.cs`
- `src/Core/DotNetCloud.Core.Data/Context/CoreDbContext.cs` (updated)

**Dependencies:** phase-0.2.2 (ApplicationUser), phase-0.2.4 (Permission system for capability model) ✅  
**Testing:** Ready for integration tests in phase-0.2.12  
**Build Status:** ✅ Solution builds successfully  
**Notes:** Device and module registry complete. UserDevice enables device management, push notifications, and presence tracking. InstalledModule tracks module lifecycle with semantic versioning. ModuleCapabilityGrant enforces capability-based security with comprehensive tier documentation (Public, Restricted, Privileged, Forbidden). All relationships properly configured with appropriate cascade/restrict delete behavior. Ready for phase-0.2.7 (CoreDbContext configuration - though most already complete).

---

#### Step: phase-0.2.7 - CoreDbContext Configuration

**Status:** completed ✅  
**Duration:** ~3 hours  
**Description:** Create CoreDbContext class and configure all relationships

**Deliverables:**

- ✓ `CoreDbContext` class extending `IdentityDbContext<ApplicationUser, ApplicationRole, Guid>`
- ✓ DbSet properties for all entities (17 entity types)
- ✓ Fluent API configuration for all relationships
- ✓ Automatic timestamps (CreatedAt, UpdatedAt) via `TimestampInterceptor`
- ✓ Soft-delete query filters configured in entity configurations
- ✓ Design-time factory for EF Core tooling

**File Location:** `src/Core/DotNetCloud.Core.Data/CoreDbContext.cs`  
**Implementation Details:**

- Created `TimestampInterceptor` class that automatically sets CreatedAt/UpdatedAt timestamps
- Configured `OnConfiguring` to register the timestamp interceptor
- All 17 entity configurations properly integrated into `OnModelCreating`
- Soft-delete query filters applied to Organization, Team, Group entities via `HasQueryFilter`
- Design-time factory created for migration generation
- Initial migration successfully generated for PostgreSQL

**Dependencies:** phase-0.2.7 (CoreDbContext)  
**Testing:** ✓ Migration generation test passed  
**Notes:** CoreDbContext fully configured and tested. Successfully generated Initial migration. TimestampInterceptor automatically manages CreatedAt/UpdatedAt for all entities. Ready for phase-0.2.8 (DbInitializer).

---

#### Step: phase-0.2.8 - Database Initialization (DbInitializer)

**Status:** completed ✅
**Duration:** ~2 hours  
**Description:** Create DbInitializer for seeding default data

**Completed Deliverables:**

- ✓ `DbInitializer` class created with comprehensive functionality:
  - ✓ Database creation and migration logic with `EnsureDatabaseAsync()` method
  - ✓ Supports both relational databases (PostgreSQL, SQL Server) and in-memory databases
  - ✓ Automatic migration application with pending migration detection
  - ✓ Transaction support for relational databases (atomic seeding operations)
- ✓ Seed default system roles (4 roles):
  - ✓ Administrator - Full system access
  - ✓ User - Standard user permissions
  - ✓ Guest - Read-only access
  - ✓ Moderator - Content moderation capabilities
  - ✓ All roles marked as system roles (IsSystemRole = true)
- ✓ Seed default permissions (48 permissions across 6 modules):
  - ✓ Core module permissions (13 permissions): admin, user management, role management, settings, modules
  - ✓ Files module permissions (7 permissions): view, upload, download, edit, delete, share, versions
  - ✓ Chat module permissions (6 permissions): send, read, channels management, moderation
  - ✓ Calendar module permissions (5 permissions): view, create, edit, delete, share
  - ✓ Contacts module permissions (5 permissions): view, create, edit, delete, share
  - ✓ Notes module permissions (5 permissions): view, create, edit, delete, share
  - ✓ Hierarchical naming convention (module.action format)
- ✓ Seed system settings (23 default settings across 5 modules):
  - ✓ Core settings (9): SessionTimeout, EnableRegistration, password policies, login limits
  - ✓ Files settings (5): MaxUploadSize, EnableVersioning, MaxVersions, Deduplication, DefaultQuota
  - ✓ Notifications settings (3): EmailEnabled, PushEnabled, EmailProvider
  - ✓ Backup settings (3): EnableAutoBackup, BackupSchedule, BackupRetention
  - ✓ Security settings (3): EnableTwoFactor, RequireTwoFactorForAdmins, EnableWebAuthn
- ✓ Idempotency checks - all seeding operations check for existing data before insertion
- ✓ Comprehensive XML documentation (1,000+ lines)
- ✓ Comprehensive integration tests (14 test cases, all passing):
  - ✓ Constructor validation tests (null checks)
  - ✓ Full initialization test (seeds all data)
  - ✓ Idempotency test (safe to run multiple times)
  - ✓ Individual seeding tests for roles, permissions, settings
  - ✓ Hierarchical permission naming validation
  - ✓ Multi-module settings validation
  - ✓ Specific setting value tests (password policy, file storage, security)
  - ✓ Logging verification test
  - ✓ Existing data skip tests (3 tests)

**Quality Metrics:**

- ✓ All 14 integration tests passing (100% pass rate)
- ✓ Comprehensive XML documentation on all public methods
- ✓ Build successful with no compiler errors or warnings
- ✓ Proper error handling and transaction management
- ✓ Idempotent operations (safe for repeated execution)
- ✓ Support for both relational and in-memory databases
- ✓ Extensive logging for initialization steps

**File Locations:**

- `src/Core/DotNetCloud.Core.Data/Initialization/DbInitializer.cs`
- `tests/DotNetCloud.Core.Data.Tests/Initialization/DbInitializerTests.cs`

**Dependencies:** phase-0.2.7 (CoreDbContext) ✓  
**Testing:** ✅ All 14 integration tests passing  
**Build Status:** ✅ Solution builds successfully  
**Notes:** DbInitializer complete with comprehensive seeding logic for roles, permissions, and settings. Includes transaction support for relational databases and in-memory database compatibility for testing. All operations are idempotent and include extensive logging. Ready for phase-0.2.9 (PostgreSQL migrations).

---

#### Step: phase-0.2.9 - EF Core Migrations (PostgreSQL)

**Status:** completed ✅
**Duration:** ~1.5 hours  
**Description:** Create initial EF Core migrations for PostgreSQL

**Deliverables:**

- ✓ Initial migration file (`20260302195528_InitialCreate.cs`)
- ✓ Schema creation (all 22 core tables)
- ✓ Index creation (strategic indexes for performance)
- ✓ Constraint definitions (foreign keys, unique constraints)
- ✓ Idempotent SQL script generation
- ✓ Migration verification documentation

**File Location:** `src/Core/DotNetCloud.Core.Data/Migrations/`  
**Dependencies:** phase-0.2.7 (CoreDbContext) ✓, phase-0.2.8 (DbInitializer) ✓  
**Testing:** ✅ Migration script generated and validated  
**Build Status:** ✅ Solution builds successfully  
**Notes:** PostgreSQL migration complete with all 22 tables: AspNetUsers, AspNetRoles, Organizations, Teams, TeamMembers, Groups, GroupMembers, OrganizationMembers, Permissions, Roles, RolePermissions, SystemSettings, OrganizationSettings, UserSettings, UserDevices, InstalledModules, ModuleCapabilityGrants, and all Identity-related tables. Comprehensive verification document created at `docs/development/migration-verification-postgresql.md`. Idempotent SQL script available at `docs/development/migration-initial-postgresql.sql`. Ready for phase-0.2.10 (SQL Server migrations).

---

#### Step: phase-0.2.10 - EF Core Migrations (SQL Server)

**Status:** completed ✅
**Duration:** ~1.5 hours
**Description:** Create initial EF Core migrations for SQL Server

**Deliverables:**

- ✓ Initial migration file (`20260302203100_InitialCreate_SqlServer.cs`)
- ✓ Designer file for snapshot tracking
- ✓ Schema creation (all 22 core tables with SQL Server-specific data types)
- ✓ Index creation (strategic indexes for performance with SQL Server syntax)
- ✓ Constraint definitions (foreign keys, unique constraints, filtered indexes)
- ✓ SQL Server-specific data types (uniqueidentifier, nvarchar, bit, datetime2, IDENTITY columns)
- ✓ Migration verification and validation

**File Location:** `src/Core/DotNetCloud.Core.Data/Migrations/SqlServer/`
**Dependencies:** phase-0.2.7 (CoreDbContext) ✓, phase-0.2.8 (DbInitializer) ✓
**Build Status:** ✓ Solution builds successfully
**Notes:** SQL Server migration complete with proper data type mappings (UUID→uniqueidentifier, VARCHAR→nvarchar, BOOLEAN→bit, TIMESTAMP→datetime2, DEFAULT CURRENT_TIMESTAMP→GETUTCDATE()). Includes IDENTITY column support for auto-incrementing integers. Ready for phase-0.2.11 (MariaDB migrations).

---

#### Step: phase-0.2.11 - EF Core Migrations (MariaDB)

**Status:** completed ✅
**Duration:** ~1.5 hours
**Description:** Create initial EF Core migrations for MariaDB

**Deliverables:**

- ✓ Initial migration file (`20260302203200_InitialCreate_MariaDb.cs`)
- ✓ Designer file for snapshot tracking
- ✓ Schema creation (all 22 core tables with MariaDB-specific data types)
- ✓ Index creation (strategic indexes for performance with MariaDB syntax)
- ✓ Constraint definitions (foreign keys, unique constraints)
- ✓ MariaDB-specific data types (CHAR(36) for UUID, VARCHAR for strings, TINYINT(1) for booleans, DATETIME(6) for timestamps)
- ✓ Collation support (UTF8MB4 default, ASCII for UUID columns)
- ✓ Migration verification and validation

**File Location:** `src/Core/DotNetCloud.Core.Data/Migrations/MariaDb/`
**Dependencies:** phase-0.2.7 (CoreDbContext) ✓, phase-0.2.8 (DbInitializer) ✓
**Build Status:** ✓ Solution builds successfully
**Notes:** MariaDB migration complete with proper data type mappings (UUID→CHAR(36), VARCHAR→VARCHAR, BOOLEAN→TINYINT(1), TIMESTAMP→DATETIME(6), AUTO_INCREMENT support via MySql:ValueGenerationStrategy). Includes table prefixing strategy through naming convention. All three database engines now supported. Ready for phase-0.2.12 (Data access tests).

---

#### Step: phase-0.2.12 - Data Access Layer Unit & Integration Tests

**Status:** completed ✅
**Duration:** ~2.5 hours  
**Description:** Create comprehensive tests for data models and DbContext

**Completed Deliverables:**

- ✓ **Soft-Delete Query Filter Tests (`SoftDeleteTests.cs`)** - 7 test methods
  - ✓ Organization soft-delete filtering (excluded from queries)
  - ✓ Team soft-delete filtering
  - ✓ Group soft-delete filtering
  - ✓ Mixed deleted/active entities (returns only active)
  - ✓ Soft-delete filter with includes (applies to related entities)
  - ✓ Delete timestamp verification
  - ✓ Cascade delete behavior with soft-deletes

- ✓ **Entity Relationship Tests (`RelationshipTests.cs`)** - 12 test methods
  - ✓ Organization-to-Teams one-to-many relationship
  - ✓ Team-to-Organization many-to-one relationship
  - ✓ TeamMember composite key and role collection preservation
  - ✓ GroupMember with audit trail (AddedByUser tracking)
  - ✓ OrganizationMember with audit trail (InvitedByUser tracking)
  - ✓ Organization-to-Groups one-to-many relationship
  - ✓ Multi-user in multiple organizations
  - ✓ Cascade delete Organization → Teams and Groups
  - ✓ Cascade delete Team → TeamMembers
  - ✓ Navigation property loading
  - ✓ Composite key functionality
  - ✓ Foreign key relationships

- ✓ **Role-Permission Junction Tests (`RolePermissionTests.cs`)** - 13 test methods
  - ✓ Role-to-Permissions many-to-many relationship
  - ✓ Permission-to-Roles many-to-many relationship
  - ✓ RolePermission composite key identification
  - ✓ Permission code unique constraint
  - ✓ Role name unique constraint
  - ✓ Role with multiple permissions
  - ✓ Permission assigned to multiple roles
  - ✓ Cascade delete Permission → RolePermissions
  - ✓ Cascade delete Role → RolePermissions
  - ✓ System role vs custom role distinction
  - ✓ Relationship includes and querying
  - ✓ Exception handling for unique constraint violations
  - ✓ Many-to-many traversal

- ✓ **Settings Hierarchy Tests (`SettingsHierarchyTests.cs`)** - 11 test methods
  - ✓ SystemSetting composite key (Module, Key)
  - ✓ OrganizationSetting overrides SystemSetting
  - ✓ UserSetting overrides Organization/SystemSettings
  - ✓ OrganizationSetting unique constraint enforcement
  - ✓ UserSetting encryption flag
  - ✓ SystemSetting UpdatedAt timestamp
  - ✓ Cascade delete Organization → OrganizationSettings
  - ✓ Cascade delete User → UserSettings
  - ✓ Multi-module settings separation
  - ✓ Three-level settings hierarchy validation
  - ✓ Exception handling for unique constraint violations

- ✓ **Device & Module Registry Tests (`DeviceModuleRegistryTests.cs`)** - 13 test methods
  - ✓ UserDevice-to-User many-to-one relationship
  - ✓ User-to-UserDevices one-to-many relationship
  - ✓ UserDevice LastSeenAt presence tracking
  - ✓ InstalledModule valid status values
  - ✓ InstalledModule semantic versioning
  - ✓ ModuleCapabilityGrant-to-InstalledModule many-to-one
  - ✓ InstalledModule-to-CapabilityGrants one-to-many
  - ✓ ModuleCapabilityGrant GrantedByUser audit tracking
  - ✓ ModuleCapabilityGrant unique constraint (one per module)
  - ✓ InstalledModule installation date immutability
  - ✓ Cascade delete InstalledModule → CapabilityGrants
  - ✓ Restrict delete User (audit trail preservation)
  - ✓ Relationship traversal and navigation

- ✓ **Multi-Database Support Tests (`MultiDatabaseTests.cs`)** - 11 test methods
  - ✓ PostgreSQL provider detection
  - ✓ SQL Server provider detection
  - ✓ MariaDB provider detection
  - ✓ PostgreSQL naming strategy (lowercase, snake_case, schemas)
  - ✓ SQL Server naming strategy (PascalCase, bracketed schemas)
  - ✓ MariaDB naming strategy (table prefixes, snake_case)
  - ✓ PostgreSQL context creation
  - ✓ Multi-database consistent schema
  - ✓ In-memory database identical data handling
  - ✓ Index naming consistency
  - ✓ Foreign key naming consistency
  - ✓ Unknown provider detection

- ✓ **DbContext Configuration Tests (`DbContextConfigurationTests.cs`)** - 13 test methods
  - ✓ CoreDbContext initialization success
  - ✓ All required DbSets present
  - ✓ All entity types configured (25+ entities)
  - ✓ Relationship configuration validation
  - ✓ Index configuration validation
  - ✓ Unique constraint configuration
  - ✓ Foreign key configuration
  - ✓ Multiple naming strategies consistency
  - ✓ IdentityDbContext inheritance
  - ✓ Query filters applied (soft-delete)
  - ✓ Property configurations applied
  - ✓ Concurrency tokens configured
  - ✓ Default values configured

- ✓ **Chat API Integration Tests** — 47 tests via ChatHostWebApplicationFactory:
  - ✓ Channel CRUD (create, duplicate-name conflict, list, get, get-404, update, delete, archive, DM)
  - ✓ Member management (add, list, update role, remove, notification preference, unread counts)
  - ✓ Message CRUD (send, paginated list, get, edit, delete, delete-404, search, search-empty-400)
  - ✓ Reactions (add, get, remove)
  - ✓ Pins (pin, unpin)
  - ✓ Typing indicators (notify, get)
  - ✓ Announcements (create, list, get-404, update, delete, acknowledge, get acknowledgements)
  - ✓ File attachments (add, list channel files)
  - ✓ Push device registration (register, empty-token-400, invalid-provider-400)
  - ✓ Mark read, health endpoint, module info endpoint
  - ✓ Full end-to-end flow (create→message→react→pin→read)

**File Locations:**

- `tests/DotNetCloud.Core.Tests/` (dotnet test -v diag --filter "FullyQualifiedName~Tests")
- `tests/DotNetCloud.Client.Core.Tests/` (dotnet test -v diag --filter "FullyQualifiedName~Tests")
- `tests/DotNetCloud.Client.SyncService.Tests/` (dotnet test -v diag --filter "FullyQualifiedName~Tests")
- `tests/DotNetCloud.Client.SyncTray.Tests/` (dotnet test -v diag --filter "FullyQualifiedName~Tests")
- `tests/DotNetCloud.Integration.Tests/` (dotnet test -v diag --filter "FullyQualifiedName~Tests")
- `tests/DotNetCloud.Modules.Chat.Tests/` (dotnet test -v diag --filter "FullyQualifiedName~Tests")
- `tests/DotNetCloud.Modules.Files.Tests/` (dotnet test -v diag --filter "FullyQualifiedName~Tests")

**Notes:** 2,242 tests pass across 12 test projects (2 skipped — SQL Server Docker). All critical functionality (sync, transfer, auth, notifications, chat, files) is covered by automated tests.

---

## Phase 1: Files (Public Launch)

**STATUS:** ✅ COMPLETED (277/277 steps)

**Goal:** File upload/download/browse/share + working desktop sync client.
**Expected Duration:** 8-12 weeks (actual)
**Milestone:** Full file management across web, desktop, with sync, sharing, and Collabora integration.

**Sub-phases:** Phase 1.1-1.20 (see Quick Status Summary table above)

**Detailed tracking:**

- Task-level checklist: `docs/IMPLEMENTATION_CHECKLIST.md` (Phase 1.1-1.16 sections)
- Completion verification plan: `docs/PHASE_1_COMPLETION_PLAN.md`

**Notes:** All Files endpoints functional, upload/download/sync verified across 3 machines (mint22, Windows11-TestDNC, mint-dnc-client). Collabora/WOPI integration operational. Desktop sync clients now ship as SyncTray-owned single-process installs on Linux and Windows, with bundle installers cleaning up stale SyncService artifacts during upgrades and avoiding Linux self-copy failures when fixing executable permissions. Share notifications (public link access, expiry warnings) and sync debounce all implemented. The Files page sidebar collapse behavior was also polished to follow the Tracks module pattern so collapsed navigation stays icon-first without clipped title/quota text and retains the correct active-state highlight. 644 Files module tests + 182 Client.Core tests + 27 SyncService tests + 77 SyncTray tests = 930 tests covering Files/Sync.

### Step: client-security-remediation-2026-03-22 - Client Security Audit Follow-up

**Status:** completed ✅
**Duration:** ~1 hour
**Description:** Implemented and validated client-side fixes from the cross-machine security audit handoff.

**Deliverables:**

- ✓ SyncTray default add-account server URL changed from hardcoded development host to empty value
- ✓ SyncService Unix socket listener now forces socket file permissions to owner-only read/write (`0600`) after bind
- ✓ SyncEngine now blocks symlink materialization when resolved link targets escape the sync root
- ✓ SyncEngine now validates all resolved local paths stay within sync root and throws on traversal attempts
- ✓ Regression tests added for all fixes in SyncTray, SyncService, and SyncEngine test suites

**Dependencies:** Prior security audit handoff (`e5b5988`)
**Blocking Issues:** None
**Notes:** All remediation tests pass in targeted runs, including explicit traversal/symlink guard coverage and socket mode verification.

---

## Phase 2: Chat & Notifications & Android

**STATUS:** ✅ COMPLETED (13/13 sub-phases)

**Goal:** Real-time chat, push notifications, announcements, and Android MAUI app.
**Expected Duration:** 6-10 weeks (actual)
**Milestone:** Full chat functionality with web UI, real-time messaging, push notifications, and mobile Android app.

---

### Step: phase-2.1 - Chat Core Abstractions & Data Models

**Status:** completed ✅
**Duration:** ~1 week (actual)
**Description:** Create Chat module projects, domain models (Channel, ChannelMember, Message, MessageAttachment, Reaction, Mention, PinnedMessage), DTOs, events, and ChatModuleManifest.

**Deliverables:**

- ✓ Create project structure (Chat, Chat.Data, Chat.Host, Chat.Tests) — 4 projects added to solution
- ✓ Create ChatModuleManifest implementing IModuleManifest
- ✓ Create domain models (Channel, ChannelMember, Message, MessageAttachment, Reaction, Mention, PinnedMessage) — 7 entities
- ✓ Create DTOs for all entities (ChannelDto, MessageDto, ReactionDto, etc.)
- ✓ Create events and event handlers (10 events: MessageSent/Edited/Deleted, ChannelCreated/Deleted/Archived, UserJoined/Left, ReactionAdded/Removed + 2 handlers)

**Dependencies:** Phase 0 (complete), Phase 1 (FileNode reference for attachments)
**Blocking Issues:** None
**Notes:** Phase 2.1 complete. All models, DTOs, events, and manifest follow core module patterns. 78 unit tests passing.

---

### Step: phase-2.2 - Chat Database & Data Access Layer

**Status:** completed ✅
**Duration:** ~1 week
**Description:** Create ChatDbContext, entity configurations, migrations, and database initialization.

**Deliverables:**

- ✓ Create entity configurations for all 9 entities with indexes, FKs, query filters
- ✓ Create ChatDbContext with all DbSets and naming strategy
- ✓ Create migrations (PostgreSQL `InitialCreate` + SQL Server `InitialCreate_SqlServer`) with `ChatDbContextDesignTimeFactory`
- ✓ Create ChatDbInitializer — seeds `#general`, `#announcements`, `#random` channels per organization

**Dependencies:** phase-2.1
**Blocking Issues:** None
**Notes:** Phase 2.2 complete. Design-time factory supports both PostgreSQL (default) and SQL Server (via `CHAT_DB_PROVIDER=SqlServer` env var). PostgreSQL migration uses `uuid`, `timestamp with time zone`, `boolean` types. SQL Server migration uses `uniqueidentifier`, `datetime2`, `nvarchar`, `bit` types. ChatDbInitializer seeds 3 default public channels with idempotent check. MariaDB migration deferred (Pomelo lacks .NET 10 support).

---

### Step: phase-2.10 - Android MAUI App

**Status:** completed ✅
**Duration:** ~3-4 weeks (actual)
**Description:** Create Android MAUI app with authentication, chat UI, SignalR real-time, push notifications, offline support, and photo auto-upload.

**Deliverables:**

- ✓ Create DotNetCloud.Clients.Android MAUI project (build flavors: googleplay/fdroid)
- ✓ Authentication: OAuth2/OIDC with PKCE, Android Keystore token storage, token refresh, multi-server support
- ✓ Android OAuth callback chooser hardening: removed duplicate `oauth2redirect` intent registration and set explicit `DotNetCloud` activity labels for browser return flow
- ✓ Android local HTTPS hardening: allow self-signed certificates for private LAN FQDNs such as `mint22.kimball.home` across OAuth token exchange, REST API clients, photo upload, and SignalR
- ✓ Android login-shell stabilization: route successful login to `//Main/ChannelList` and keep Shell navigation plus first-screen collection updates on the UI thread to prevent post-connect white screens
- ✓ Chat UI: ChannelListPage, MessageListPage (pull-to-refresh), ChannelDetailsPage (members + leave), enhanced composer (emoji picker, file attach, @mention autocomplete), dark/light theme
- ✓ Real-time: SignalRChatClient with exponential backoff reconnect [0s, 2s, 5s, 15s], ChatConnectionService foreground service + WakeLock
- ✓ Push: FcmMessagingService (googleplay flavor), UnifiedPushReceiver (fdroid flavor), 5 notification channels (connection, messages, mentions, announcements, photo_upload), AndroidManifest declarations
- ✓ Offline: SqliteMessageCache (read), IPendingMessageQueue + SqlitePendingMessageQueue (write), flush queue on SignalR reconnect
- ✓ Photo auto-upload: IPhotoAutoUploadService + PhotoAutoUploadService; MediaStore query, 4 MB chunked upload, WiFi-only + enabled preference, progress notification
- ✓ File browser: IFileRestClient + HttpFileRestClient (chunked upload, envelope unwrapping, download streaming), FileBrowserViewModel (folder navigation, file picker upload, camera photo/video capture, download-and-open, delete, quota), FileBrowserPage.xaml + code-behind, Files tab in AppShell
- ✓ Media auto-upload (photos + videos): IMediaAutoUploadService + MediaAutoUploadService; scans MediaStore for both photos and videos, uploads into InstantUpload/YYYY/MM folder hierarchy (auto-created, Nextcloud-style), configurable folder name, uses IFileRestClient for chunked upload with parentId, ChannelIdMediaUpload notification channel
- ✓ Distribution signing: Release PropertyGroup with AndroidKeyStore/KEYSTORE\_\* env vars, AndroidUseAapt2=true for F-Droid reproducibility
- ✓ Direct APK download option documented
- ✓ App store listing description written

**Dependencies:** phase-2.7, phase-2.8
**Blocking Issues:** None
**Notes:** Phase 2.10 fully complete. All deliverables shipped: auth (PKCE+Keystore), real-time chat (SignalR + FCM/UP push), offline queue (SQLite), photo upload (MediaStore → chunked API), file browser (IFileRestClient with chunked upload/download, FileBrowserViewModel with folder navigation and camera capture, Files tab in Shell), media auto-upload (photos + videos into InstantUpload/YYYY/MM folders via IFileRestClient), distribution signing, notification badges (AppBadgeManager → SetNumber on notification builders), direct APK download docs, and app store listing. Android callback handling was hardened by de-duplicating the `oauth2redirect` intent registration and applying explicit `DotNetCloud` labels so browser return prompts no longer present duplicate generic `.NET` targets. The local HTTPS path was also hardened so private LAN FQDNs that resolve inside the home network, including `mint22.kimball.home`, are treated like other local/self-hosted targets for self-signed certificate acceptance during OAuth token exchange and subsequent app traffic. Post-login navigation was further stabilized by aligning the authenticated Shell route with `//Main/ChannelList` and keeping Shell transitions plus bound collection updates on the UI thread across login, channel list, message list, channel details, and settings flows. All services registered in MauiProgram.cs via `AddSingleton`/`AddTransient`/`AddHttpClient`.

---

## Phase 3: Contacts, Calendar & Notes

> **Goal:** Personal information management — Contacts (CardDAV), Calendar (CalDAV), Notes (Markdown). Full PIM suite with standards compliance.
> **Detailed plan:** `docs/PHASE_3_IMPLEMENTATION_PLAN.md`

### Section: Phase 3.1 - Architecture And Contracts

**STATUS:** completed ✅
**DELIVERABLES:**

- ✓ Core DTOs and contracts for Contacts, Calendar, Notes
- ✓ Event contracts (Created/Updated/Deleted events for each domain)
- ✓ Capability interfaces and tier mapping
- ✓ Validation and error code extensions

**Notes:** All Phase 3.1 contracts added to DotNetCloud.Core. DTOs: ContactDtos.cs, CalendarDtos.cs, NoteDtos.cs. Events: ContactEvents.cs, CalendarEvents.cs, NoteEvents.cs. Capabilities: IContactDirectory, ICalendarDirectory, INoteDirectory (all Public tier). Error codes added for CONTACT*, CALENDAR*, NOTE\_ domains. 197/197 Core tests pass. Ready for phase-3.2 (Contacts Module).

---

### Section: Phase 3.2 - Contacts Module

**STATUS:** completed ✅
**DELIVERABLES:**

- ✓ Module projects (Contacts, Contacts.Data, Contacts.Host)
- ✓ Data model + EF configurations (8 entities, 8 configs)
- ✓ REST API endpoints (CRUD, bulk import/export, search)
- ✓ CardDAV endpoints (principal discovery, vCard get/put/delete, sync token)
- ✓ Contact avatar and attachment support
- ✓ Contact sharing model

**Notes:** Full 3-tier module with 9 entity models (Contact, ContactEmail, ContactPhone, ContactAddress, ContactCustomField, ContactGroup, ContactGroupMember, ContactShare, ContactAttachment), 5 service implementations (ContactService, ContactGroupService, ContactShareService, VCardService, ContactAvatarService), REST API controller with avatar/attachment endpoints, CardDAV controller with PROPFIND/REPORT WebDAV methods, gRPC service + lifecycle service, health check, InProcessEventBus, proto definition. 105 tests pass. Avatar upload/download/delete, attachment CRUD, VCard PHOTO serialization/parsing all complete.

---

### Section: Phase 3.3 - Calendar Module

**STATUS:** completed ✅
**DELIVERABLES:**

- ✓ Module projects (Calendar, Calendar.Data, Calendar.Host)
- ✓ Data model (calendars, events, attendees, recurrence, reminders, shares)
- ✓ REST API endpoints (CRUD, RSVP, sharing, search/filter)
- ✓ CalDAV endpoints (calendar discovery, iCal get/put/delete, sync token)
- ✓ Recurrence engine and occurrence expansion
- ✓ Reminder/notification pipeline (in-app + push)
- ✓ gRPC service (11 RPCs) for core ↔ module communication
- ✓ iCalendar RFC 5545 import/export service
- ✓ 82 passing tests (39 existing + 43 new: recurrence, expansion, reminders)

**Notes:** Calendar module fully complete. RecurrenceEngine parses RFC 5545 RRULE (DAILY/WEEKLY/MONTHLY/YEARLY, INTERVAL, COUNT, UNTIL, BYDAY with ordinals, BYMONTHDAY, BYMONTH, BYSETPOS). OccurrenceExpansionService merges expanded/concrete/exception events for time-range queries. ReminderDispatchService (BackgroundService) scans every 30s, publishes CalendarReminderTriggeredEvent + ReminderTriggeredEvent, logs dispatches in ReminderLog table to prevent duplicates. Handles recurring event reminders via recurrence expansion.

---

### Section: Phase 3.4 - Notes Module

**STATUS:** completed ✅
**DELIVERABLES:**

- ✓ Module projects (Notes, Notes.Data, Notes.Host)
- ✓ Data model (notes, versions, folders, tags, links, sharing — 6 entities, 6 EF configurations)
- ✓ REST API endpoints (~25 endpoints: CRUD, tagging, search, version history, folders, sharing)
- ✓ gRPC service (10 RPCs) + lifecycle service
- ✓ Markdown rendering pipeline with sanitization (Markdig + HtmlSanitizer)
- ✓ Rich-editor integration (MarkdownEditor Blazor component with toolbar + live preview)
- ✓ Cross-entity link references (Files, Calendar, Contact, Note)
- ✓ Note sharing (ReadOnly/ReadWrite per-user permissions)
- ✓ Version history with restore + optimistic concurrency
- ✓ 50 passing tests (module lifecycle, CRUD, search, versioning, folders, sharing)

**Notes:** Notes module fully complete. Markdown rendering pipeline implemented using Markdig (advanced extensions, task lists, emoji) + Ganss.Xss HtmlSanitizer for XSS prevention. MarkdownEditor Blazor component provides toolbar (bold, italic, headings, lists, code, tables, etc.) with split-pane live preview. API endpoints added: GET /api/v1/notes/{id}/preview (rendered note) and POST /api/v1/notes/render (live preview). 40 new MarkdownRenderer tests cover rendering, sanitization, and 15 XSS attack vectors. Total: 121 Notes tests passing.

---

### Section: Phase 3.5 - Cross-Module Integration

**STATUS:** completed ✅
**DELIVERABLES:**

- ✓ Unified navigation + module registration in Blazor shell (Contacts 👤, Calendar 📅, Notes 📝 with stub pages)
- ✓ App-shell collapsed sidebar accessibility polish (`title`/`aria-label` hover labels on icon-only navigation links)
- ✓ Shared notification patterns (ResourceShared, UserMentioned, ReminderTriggered events + handlers + push integration)
- ✓ Cross-module link resolution (ICrossModuleLinkResolver with Contact/CalendarEvent/Note/File support, batch resolve)
- ✓ Consistent authorization, audit, and soft-delete behavior (IAuditLogger capability, CallerContext verification, all manifests updated)
- ✓ 30 new tests (CrossModuleLinkResolver 13, NotificationHandlers 4, ManifestConsistency 13)
- ✓ Core DTOs: NotificationDtos, CrossModuleLinkDtos
- ✓ Module Razor SDK upgrades (Contacts, Calendar, Notes)
- ✓ Module manifest updates with cross-module capabilities and event subscriptions
- ✓ Tracks-style collapsed sidebar polish across Contacts, Calendar, and Notes (icon-first collapsed nav, hidden expanded-only panes/actions, corrected toggle glyph rendering)

**Notes:** Cross-module integration complete. All PIM modules now declare IAuditLogger + ICrossModuleLinkResolver capabilities, publish ResourceSharedEvent, and subscribe to each other's domain events. Notification handlers wire into existing IPushNotificationService. ExampleModule NoteCreatedEvent naming conflict resolved with using aliases. Deferred items were completed in follow-up implementation: audit columns were added across PIM entities, notification persistence + bell UI were wired, contact reverse related-entity queries were exposed via API, and link chips now render in Contacts/Calendar/Notes views. All D1-D7 deferred items are now complete: API client methods added for sharing/RSVP/import-export/folder-CRUD/version-history/search across all three modules; ContactsPage has avatar display and sharing dialog; CalendarPage has RSVP buttons, sharing dialog, and iCal import/export; NotesPage has folder CRUD (create/rename/delete), version history panel with restore, and sharing dialog. The Blazor sidebars for all three PIM modules now also match the Tracks/Files collapsed navigation behavior so collapsed mode stays icon-first and removes clipped expanded-only content. The app-level shell navigation now follows the same collapsed accessibility pattern as Files by exposing hover text and screen-reader labels on icon-only links.

---

### Section: Phase 3.6 - Migration Foundation

**STATUS:** completed ✅
**DELIVERABLES:**

- ✓ Import contract interfaces and pipeline architecture
- ✓ vCard and iCalendar migration parsers/transformers
- ✓ Notes import adapter (markdown/plain exports)
- ✓ Dry-run mode with import report and conflict summary

**Notes:** Import infrastructure complete in `DotNetCloud.Core.Import` namespace. Core contracts: `ImportDtos.cs` (ImportRequest/ImportReport/ImportItemResult records), `IImportProvider` (module adapter interface), `IImportPipeline` (orchestrator). `ImportPipelineService` routes by DataType via DI-injected providers. Three providers: `ContactsImportProvider` (vCard 3.0 — FN/N/ORG/TITLE/EMAIL/TEL/ADR/BDAY/URL/NOTE), `CalendarImportProvider` (iCalendar RFC 5545 — SUMMARY/DTSTART/DTEND/DESCRIPTION/LOCATION/URL/RRULE), `NotesImportProvider` (JSON manifest array or raw Markdown with heading extraction). Dry-run: `DryRun=true` parses and validates without persisting, returns deterministic `ImportReport`. 51 tests (8 pipeline + 12 contacts + 13 calendar + 18 notes), all passing. 2,476 total CI tests pass. Ready for Phase 3.7 (Testing And Quality Gates).

---

### Section: Phase 3.7 - Testing And Quality Gates

**STATUS:** completed ✅
**DELIVERABLES:**

- ✓ Unit test suites for all three modules (ContactShareServiceTests, CalendarShareServiceTests, NoteSecurityTests)
- ✓ Integration tests for REST and DAV endpoints (CardDavInteropTests, CalDavInteropTests)
- ✓ CardDAV and CalDAV compatibility test matrix (vCard 3.0 round-trip, iCal RFC 5545 round-trip, timezone/RRULE/VALARM/all-day handling)
- ✓ Security tests (authz bypass, tenant isolation, XSS) — ContactSecurityTests, CalendarSecurityTests, NoteSecurityTests (XSS content storage validation)
- ✓ Performance baselines (500-contact creation, 200-event creation, large list/search/export benchmarks)

**Notes:** Phase 3.7 complete. 224 new tests added across 8 new test files. Total PIM module tests: 245 (77 Contacts + 87 Calendar + 81 Notes). Total CI tests: 2,700 — all passing, 0 failures. XSS tests document that content is stored as-is; sanitization is a presentation-layer concern and is handled by the markdown rendering pipeline. Previously deferred Phase 3.5 follow-ups are now implemented (audit columns, PIM notification persistence/UI, and cross-module related-link rendering).

---

### Section: Phase 3.8 - Documentation And Release Readiness

**STATUS:** completed ✅
**DELIVERABLES:**

- ✓ Admin docs for Contacts, Calendar, Notes — `docs/admin/PIM_MODULES.md`
- ✓ User guides for import, sharing, sync, troubleshooting — `docs/user/CONTACTS.md`, `docs/user/CALENDAR.md`, `docs/user/NOTES.md`
- ✓ API docs for REST and DAV endpoints — `docs/api/CONTACTS.md`, `docs/api/CALENDAR.md`, `docs/api/NOTES.md`
- ✓ Upgrade/release notes with migration caveats — `docs/admin/PHASE_3_RELEASE_NOTES.md`

**Notes:** Phase 3.8 complete. All four documentation deliverables created: admin operations guide covering all three PIM modules, three user guides (one per module) with workflows for contact/calendar/note management plus DAV sync setup and import/export, three API reference docs with full REST + DAV endpoint specifications including schemas and error codes, and release notes with upgrade instructions and known limitations. Doc indexes updated: `docs/api/README.md` links module API references; `README.md` links admin guide, user guides, and release notes. Phase 3 documentation milestone (Milestone D) is now fully complete.

---

## Phase 4: Project Management (Tracks)

> **Goal:** Kanban boards + Jira-like project tracking as a process-isolated module.
> **Module ID:** `dotnetcloud.tracks`
> **Detailed plan:** `docs/PHASE_4_IMPLEMENTATION_PLAN.md`

### Section: Phase 4.1 - Architecture And Contracts

**STATUS:** completed ✅
**DELIVERABLES:**

- ✓ `TracksDtos.cs` — 21 DTO records: BoardDto, BoardMemberDto, BoardListDto, CardDto, CardAssignmentDto, LabelDto, CardCommentDto, CardAttachmentDto, CardChecklistDto, ChecklistItemDto, CardDependencyDto, SprintDto, TimeEntryDto, BoardActivityDto + 7 request DTOs (Create/Update Board/Card/List/Label/Sprint/TimeEntry, MoveCard) + 4 enums (BoardMemberRole, CardPriority, CardDependencyType, SprintStatus)
- ✓ `TracksEvents.cs` — 10 domain events: BoardCreatedEvent, BoardDeletedEvent, CardCreatedEvent, CardMovedEvent, CardUpdatedEvent, CardDeletedEvent, CardAssignedEvent, CardCommentAddedEvent, SprintStartedEvent, SprintCompletedEvent
- ✓ `ITracksDirectory` capability interface (Public tier) with board/card lookup + CardSummary record
- ✓ 15 `TRACKS_` error codes in `ErrorCodes.cs` (board/list/card/label/sprint/comment/checklist/time entry not found, role checks, WIP limit, dependency cycle, sprint transitions)
- ✓ 49 unit tests: 34 DTO tests, 10 event tests, 5 capability tests — all passing (246 total Core tests, 0 failures)
- ✓ `ITeamDirectory` capability interface (Restricted tier) — cross-module read-only team/membership access with `TeamInfo` and `TeamMemberInfo` records
- ✓ `ITeamManager` capability interface (Restricted tier) — cross-module team CRUD and member management (CreateTeam, UpdateTeam, DeleteTeam, AddMember, RemoveMember)
- ✓ Tracks team DTOs: `TracksTeamDto`, `TracksTeamMemberDto`, `CreateTracksTeamDto`, `UpdateTracksTeamDto`, `TransferBoardDto`, `TracksTeamMemberRole` enum
- ✓ Tracks team events: `TeamCreatedEvent`, `TeamDeletedEvent`
- ✓ Tracks team error codes: `TracksTeamNotFound`, `TracksNotTeamMember`, `TracksInsufficientTeamRole`, `TracksTeamHasBoards`, `TracksAlreadyTeamMember`

**Notes:** Phase 4.1 complete. All Tracks contracts added to DotNetCloud.Core. Added Option C team architecture: Core owns team identity/membership (ITeamDirectory + ITeamManager), Tracks extends with module-specific role overlay. Ready for Phase 4.2.

---

### Section: Phase 4.2 - Data Model And Module Scaffold

**STATUS:** completed ✅
**DELIVERABLES:**

- ✓ `DotNetCloud.Modules.Tracks/` — Module library (TracksModule.cs, TracksModuleManifest.cs, manifest.json, 16 entity models + PokerSession + PokerVote)
- ✓ `DotNetCloud.Modules.Tracks.Data/` — TracksDbContext (18 DbSets), 18 EF configurations, design-time factory, db initializer, service registration
- ✓ `DotNetCloud.Modules.Tracks.Host/` — gRPC host scaffold (Program.cs, TracksGrpcService with 11 RPCs incl. 4 poker RPCs, TracksLifecycleService, TracksHealthCheck, InProcessEventBus, TracksControllerBase, tracks_service.proto)
- ✓ Solution integration (all 3 projects in DotNetCloud.sln + DotNetCloud.CI.slnf)
- ✓ Integrated planning poker: PokerSession/PokerVote entities, PokerSessionStatus/PokerScale enums, 6 DTOs, 3 events, 4 error codes, 14 new unit tests
- ✓ `TeamRole` entity — Option C design: `CoreTeamId` + `UserId` → `TracksTeamMemberRole` (Member/Manager/Owner). Unique index on (CoreTeamId, UserId).
- ✓ `Board.TeamId` (nullable Guid) references Core team ID — cross-DB reference, app-level validation only (no FK)

**Notes:** Full 3-tier module scaffold. 18 entities + TeamRole. Includes integrated planning poker and Option C team model (Core teams = identity, Tracks extends with roles). Board.TeamId is a cross-DB reference to Core teams. Builds with 0 errors. Ready for Phase 4.3.

---

### Section: Phase 4.3 - Core Services And Business Logic

**STATUS:** completed ✅
**DELIVERABLES:**

- ✓ `BoardService` — CRUD boards, manage members/roles, archive/unarchive
- ✓ `ListService` — CRUD lists, reorder (gap-based positioning), WIP limit enforcement
- ✓ `CardService` — CRUD cards, move between lists, assign/unassign users, priority, due dates, archive
- ✓ `LabelService` — CRUD labels per board, assign/remove from cards
- ✓ `CommentService` — CRUD comments with Markdown content (stored as-is, sanitization at presentation layer)
- ✓ `ChecklistService` — CRUD checklists and items, toggle completion
- ✓ `AttachmentService` — Link files (Files module or URL), remove
- ✓ `DependencyService` — Add/remove card dependencies, BFS cycle detection for BlockedBy
- ✓ `SprintService` — CRUD sprints, start/complete lifecycle, move cards in/out
- ✓ `TimeTrackingService` — Start/stop timer, manual entry, duration rollup
- ✓ `ActivityService` — Log mutations, query activity feed per board/card
- ✓ Authorization logic — Board role checks (Owner/Admin/Member/Viewer) via EnsureBoardRoleAsync/EnsureBoardMemberAsync
- ✓ Unit tests (112 tests covering all 11 services)
- ✓ `TeamService` — Option C implementation: Core teams via ITeamDirectory (read) + ITeamManager (write), Tracks TeamRoles overlay
  - ✓ Team CRUD (create → Core team + Tracks Owner role, update, delete with block/cascade)
  - ✓ Member management (add/remove/update role, Owner cannot be removed/is last-owner protected)
  - ✓ Board transfer (personal ↔ team, requires board Owner + team Manager)
  - ✓ `GetEffectiveBoardRoleAsync` — merges direct board membership + team-derived role (higher wins)
  - ✓ Graceful degradation when ITeamDirectory/ITeamManager not injected (nullable capabilities)
- ✓ `TeamDirectoryService` — ITeamDirectory implementation in Core.Auth (reads from CoreDbContext)
- ✓ `TeamManagerService` — ITeamManager implementation in Core.Auth (writes to CoreDbContext)
- ✓ DI registration for ITeamDirectory + ITeamManager as scoped services in AuthServiceExtensions

**Notes:** All 11 services + TeamService (12 total). Option C team architecture: Core owns team identity/membership, Tracks stores module-specific role assignments (TeamRole entity). Team role mapping: Owner→BoardOwner, Manager→BoardAdmin, Member→BoardMember. 29 new TeamServiceTests. Ready for Phase 4.4.

---

### Section: Phase 4.4 - REST API And gRPC Service

**STATUS:** completed ✅
**DELIVERABLES:**

- ✓ `BoardsController` — 15 endpoints: CRUD boards, activity, members (CRUD + role), labels (CRUD), export/import
- ✓ `ListsController` — 5 endpoints: CRUD lists, reorder
- ✓ `CardsController` — 10 endpoints: CRUD cards, move, assign/unassign, labels add/remove, activity
- ✓ `CommentsController` — 4 endpoints: CRUD comments
- ✓ `ChecklistsController` — 6 endpoints: CRUD checklists + items, toggle, delete items
- ✓ `AttachmentsController` — 3 endpoints: list, add, remove
- ✓ `DependenciesController` — 3 endpoints: list, add, remove (cycle → 409)
- ✓ `SprintsController` — 9 endpoints: CRUD sprints, start/complete, add/remove cards
- ✓ `TimeEntriesController` — 5 endpoints: list, create, delete, timer start/stop
- ✓ `TeamsController` — 10 endpoints: CRUD teams, add/remove/update members, transfer board, list team boards
- ✓ `TracksGrpcService` — 7 RPCs fully implemented (4 poker stubs → Phase 4.7)
- ✓ `TracksControllerBase` — IsBoardNotFound() helper, auth, envelope methods
- ✓ 58 new tests (10 board + 7 card + 5 list + 7 sprint + 19 subresource + 10 gRPC) + 29 TeamServiceTests
- ✓ Cross-module integration (file attachments via `FileDeletedEventHandler` + `ICardAttachmentCleanupService`) — completed in Phase 4.6

**Notes:** 50+ REST endpoints across 10 controllers. All 238 Tracks tests pass. Teams support Option C architecture with full CRUD, member management, board ownership transfer, and effective role resolution.

---

### Section: Phase 4.5 - Web UI (Blazor)

**STATUS:** completed ✅
**DELIVERABLES:**

- ✓ Board list page — Grid/list of boards, create board dialog
- ✓ Board view — Full kanban with drag-and-drop cards between lists
- ✓ Card detail panel — Slide-out with description, assignments, labels, checklists, comments, attachments, time, dependencies, activity
- ✓ Sprint management — Planning view, backlog → sprint, progress indicators
- ✓ Sprint Planning Workflow UX — Full sprint planning experience:
  - ✓ Sprint selector in card detail sidebar (assign/remove cards from sprints)
  - ✓ Sprint backlog view (expandable card list per sprint in SprintPanel)
  - ✓ Quick-add cards to sprint (card picker dialog with multi-select, search, batch add)
  - ✓ Sprint filter on kanban board (filter cards by sprint)
  - ✓ Sprint badge on kanban cards (🏃 sprint title)
  - ✓ Sprint Planning View (side-by-side product backlog/sprint backlog, capacity bar, member workload, priority groups)
  - ✓ Burndown chart (SVG-based SprintBurndownChart.razor, ideal vs actual remaining SP)
  - ✓ Velocity chart (SVG-based VelocityChart.razor, committed vs completed across sprints)
  - ✓ Sprint completion dialog (summary stats, incomplete card handling — move to next sprint or backlog)
  - ✓ Sprint report API client methods (GetSprintReportAsync, GetBoardVelocityAsync)
- ✓ Board settings — Members, labels, archive management
- ✓ Team management — Create/edit teams, member roles, board transfer
- ✓ Filters and search — Filter by label, assignee, due date, priority
- ✓ Real-time updates — Blazor event subscriptions via `ITracksSignalRService`, auto-refresh on board/card/sprint signals (completed in Phase 4.6)
- ✓ Responsive layout — Desktop, tablet, mobile-friendly
- ✓ CSS consistent with DotNetCloud UI theme
- ✓ ITracksApiClient / TracksApiClient HTTP service
- ✓ Module UI registration in ModuleUiRegistrationHostedService
- ✓ tracks-kanban.js drag-drop JS interop

**Notes:** Full Blazor UI: TracksPage (sidebar layout), BoardListView, KanbanBoard (HTML5 drag-drop), CardDetailPanel (slide-out), SprintPanel, BoardSettingsDialog, TeamManagement. Comprehensive CSS with ::deep scoping and responsive breakpoints. Real-time event subscriptions integrated in Phase 4.6. Sprint Planning Workflow UX added: SprintPlanningView (side-by-side planning), SprintBurndownChart + VelocityChart (SVG-based, code-behind rendering to avoid Razor <text> conflicts), SprintCompletionDialog (incomplete card handling), sprint filter/badge on kanban, card picker dialog. ~470 lines of new CSS. TargetStoryPoints added to Sprint model/DTOs. BatchAddSprintCardsDto for multi-card sprint assignment. 5 new API client methods.

---

### Section: Phase 4.6 - Real-time And Notifications

**STATUS:** completed ✅
**DELIVERABLES:**

- ✓ `TracksRealtimeService` + `ITracksSignalRService` — Real-time board state sync via `IRealtimeBroadcaster`
- ✓ Notification integration — Card assignment, @mention, sprint start/complete, team membership via `TracksNotificationService`
- ✓ Activity feed — Per-board real-time stream via `BroadcastActivityAsync`, Blazor auto-refresh
- ✓ @mention support — `MentionParser` (GeneratedRegex), `IUserDirectory` resolution, `Mention` notifications

**Notes:** Follows Chat module's nullable-capability pattern. Each board is a `tracks-board-{boardId}` group, teams use `tracks-team-{teamId}`. Also completed deferred Phase 4.4 (FileDeletedEvent cross-module handler via `ICardAttachmentCleanupService`) and deferred Phase 4.5 (Blazor real-time UI subscriptions via `ITracksSignalRService`). 39 new unit tests (238 total).

---

### Section: Phase 4.7 - Advanced Features

**STATUS:** completed ✅
**DELIVERABLES:**

- ✓ Board templates — `BoardTemplateService` with 4 built-in templates (Kanban, Scrum, Bug Tracking, Personal TODO); `BoardTemplatesController` (5 endpoints); seeded on startup
- ✓ Card templates — `CardTemplateService`: save/list/get/delete/create from template; `CardTemplatesController` (4 endpoints)
- ✓ Due date reminders — `DueDateReminderService` (IHostedService): hourly background scan, notifies cards due within 24h
- ✓ Board analytics — `AnalyticsService.GetBoardAnalyticsAsync`: completions over time, cycle time, list dwell time, workload; GET /boards/{id}/analytics
- ✓ Team analytics — `AnalyticsService.GetTeamAnalyticsAsync`: board count, cards by member; GET /teams/{id}/analytics
- ✓ Sprint reports — `SprintReportService.GetSprintReportAsync`: velocity, burndown by date, cards by status; GET /sprints/{id}/report
- ✓ Bulk operations — `BulkOperationService`: BulkMoveCards, BulkAssignCards, BulkLabelCards, BulkArchiveCards (max 100); `BulkOperationsController` (4 endpoints)
- ✓ Poker gRPC RPCs — `TracksGrpcService` StartPokerSession, SubmitPokerVote, RevealPokerSession, AcceptPokerEstimate fully implemented; previously deferred from Phase 4.4
- ✓ Unit tests — 92 new tests covering all new services; 291 total Tracks tests passing

**Notes:** All 291 Tracks tests pass. BulkOperationService uses `_db.CardAssignments.Add()`/`_db.CardLabels.Add()` directly (not via navigation collection) to avoid EF InMemory `HasDefaultValueSql` concurrency issue. Built-in template names: "Kanban", "Scrum", "Bug Tracking", "Personal TODO".

---

### Section: Phase 4.8 - Testing Documentation And Release

**STATUS:** completed ✅
**DELIVERABLES:**

- ✓ Unit tests — Full service coverage, authorization, cycle detection (existing 291 tests from phases 4.1–4.7)
- ✓ Integration tests — REST API endpoint tests (12 tests: BoardsController, CardsController, SprintsController, CommentsController, ChecklistsController, TeamsController, TimeEntriesController, end-to-end workflows, gRPC, multi-user concurrent ops)
- ✓ Security tests — Board role authorization (5 role levels × 6 operations), team role escalation prevention, tenant isolation, Markdown XSS prevention (script, img onerror, iframe, javascript URL)
- ✓ Performance tests — Large board (100+ cards per list, 500 cards across 10 lists), reorder operations (20 lists), 50-card move, 50 team members, 30 board members, 50 labels, 20-deep dependency chain
- ✓ Admin documentation — `docs/modules/tracks/README.md`: module config, architecture, 15 controllers, 88 endpoints, authorization model, gRPC RPCs, enums reference
- ✓ User guide — `docs/modules/tracks/USER_GUIDE.md`: board management, card workflows, sprints, time tracking, planning poker, teams, bulk operations, templates, analytics
- ✓ API documentation — `docs/modules/tracks/API.md`: all 88 REST endpoints with request/response examples, DTOs reference, error handling
- ✓ README roadmap status update — Tracks marked ✅ Phase 4 in feature table and ✅ Complete in roadmap table

**Notes:** 344 total Tracks tests pass (291 existing + 53 new: 30 security, 12 integration, 11 performance). Phase 4 complete.

---

### Section: Phase 4.9 - Dual-Mode Rework (Personal + Team)

**STATUS:** completed ✅
**DELIVERABLES:**

- ✓ Phase A: Data Model & Mode System — `BoardMode` enum, `ReviewSession`/`ReviewSessionParticipant` entities, sprint planning fields, EF configs
- ✓ Phase B: Service Layer — Mode-aware `BoardService`, `SprintPlanningService`, `ReviewSessionService`, backlog/sprint filter, poker vote status
- ✓ Phase C: API Layer — Board mode parameter, sprint wizard endpoints, backlog endpoints, `ReviewSessionController`, poker vote status endpoint, gRPC updates
- ✓ Phase D: Real-Time / SignalR — Review session broadcasts, client-side SignalR events
- ✓ Phase E: UI — Personal Mode Simplification — Mode selector in create dialog, mode badge in board list, conditional sidebar for Personal/Team, sprint controls hidden for Personal boards, 35 Phase E tests
- ✓ Phase F: UI — Sprint Planning Wizard — 4-step wizard (Plan Basics → Swimlanes → Schedule → Review), TracksPage integration with Year Plan nav, 61 Phase F tests
- ✓ Phase G: UI — Backlog & Sprint Views — BacklogView component (filter/multi-select/bulk assign), sprint tabs in KanbanBoard, Backlog TracksView in sidebar, 47 Phase G tests
- ✓ Phase H: UI — Year Timeline / Gantt View — TimelineView.razor with sprint Gantt bars, drag-resize, today marker, responsive zoom, 54 Phase H tests
- ✓ Phase I: UI — Live Review Mode — ReviewSessionPanel.razor, participant management, poker voting integration, host controls, 42 Phase I tests
- ✓ Phase J: Comprehensive Tests — 62 new tests covering data model validation, mode-aware services, sprint planning edge cases, review session edge cases, poker vote status, controller integration, security, and performance

**Notes:** All 10 phases (A–J) complete. 801 total Tracks tests passing. Dual-mode rework fully implemented: personal boards (simplified kanban) and team boards (sprints, backlog, planning wizard, review sessions, poker voting, timeline view). No remaining work.

---

## Phase 5: Photos Module (Sub-Phase B)

### Section: Phase 5.3 - Photos Architecture & Contracts

#### Step: phase-5.3 - Photos Architecture & Contracts

**Status:** completed ✅
**Deliverables:**

- ✓ `IPhotoDirectory` capability interface
- ✓ `PhotosDtos.cs` — PhotoDto, AlbumDto, PhotoMetadataDto, PhotoEditOperationDto, GeoClusterDto, PhotoShareDto, SlideshowDto + enums
- ✓ `PhotoEvents.cs` — PhotoUploadedEvent, PhotoDeletedEvent, AlbumCreatedEvent, AlbumSharedEvent, PhotoEditedEvent
- ✓ `PhotosModuleManifest.cs` — module manifest with capabilities, published/subscribed events
- ✓ `PhotosModule.cs` — IModuleLifecycle implementation with initialize/start/stop/dispose
- ✓ `FileUploadedPhotoHandler.cs` — event handler for image uploads

**Notes:** Foundation contracts and module scaffolding complete. Ready for data model.

---

### Section: Phase 5.4 - Photos Data Model

#### Step: phase-5.4 - Photos Data Model & Migrations

**Status:** completed ✅
**Deliverables:**

- ✓ 7 entity models: Photo, Album, AlbumPhoto, PhotoMetadata, PhotoTag, PhotoShare, PhotoEditRecord
- ✓ 7 EF Core configurations with indexes and soft-delete query filters
- ✓ `PhotosDbContext` with all DbSets
- ✓ `PhotosDbContextDesignTimeFactory` for EF Core tooling
- ✓ InitialCreate migration (7 tables)

**Notes:** Full data model with soft delete, geo-indexing, edit history tracking, and initial migration.

---

### Section: Phase 5.5 - Photos Core Services

#### Step: phase-5.5 - Photos Core Services

**Status:** completed ✅
**Deliverables:**

- ✓ `PhotoService` — CRUD, timeline, favorites, search, soft delete
- ✓ `AlbumService` — album CRUD, photo management, sorting
- ✓ `PhotoMetadataService` — EXIF extraction and storage
- ✓ `PhotoGeoService` — geo-tagged photo queries and clustering
- ✓ `PhotoShareService` — photo/album sharing with permission levels
- ✓ `PhotoThumbnailService` — photo-specific sizes (grid 300px, detail 1200px, full), ImageSharp-based, two-level cache
- ✓ `PhotoIndexingBackgroundService` — periodic scan for unindexed image files

**Notes:** All core services implemented with CallerContext authorization. Thumbnail and background indexing services added.

---

### Section: Phase 5.6 - Photo Editing & Slideshow

#### Step: phase-5.6 - Photo Editing & Slideshow

**Status:** completed ✅
**Deliverables:**

- ✓ `PhotoEditService` — non-destructive editing with validation (rotate, crop, flip, brightness, contrast, saturation, sharpen, blur)
- ✓ `SlideshowService` — slideshow creation from albums or photo selections
- ✓ `PhotosServiceRegistration` — DI registration for all Photos services

**Notes:** Edit stack with undo/revert-all. Validation rules per edit type.

---

### Section: Phase 5.7 - Photos API & Web UI

#### Step: phase-5.7 - Photos API, gRPC & Host

**Status:** completed ✅
**Deliverables:**

- ✓ `photos_service.proto` — full gRPC contract
- ✓ `PhotosController` — REST API for photos, timeline, metadata, geo, editing, sharing, slideshow
- ✓ `AlbumsController` — REST API for album CRUD, photo management, sharing
- ✓ `PhotosGrpcServiceImpl` — gRPC service implementation
- ✓ `PhotosHealthCheck` — module health check
- ✓ `Program.cs` — host application builder
- ✓ 95 comprehensive tests (all passing) — PhotoService, AlbumService, PhotoEditService, PhotoGeoService, PhotoShareService, SlideshowService, PhotoMetadataService, PhotosModule

**Notes:** Sub-Phase B (Photos Module) fully complete. All 3 projects compile, solution/CI updated, 119 tests passing.

---

## Phase 5: Music Module (Sub-Phase C)

### Section: Phase 5.8 - Music Architecture & Contracts

#### Step: phase-5.8 - Music Architecture & Contracts

**Status:** completed ✅
**Deliverables:**

- ✓ `IMusicDirectory` capability interface (Public tier)
- ✓ Music DTOs: ArtistDto, MusicAlbumDto, TrackDto, PlaylistDto, NowPlayingDto, EqPresetDto, LibraryScanResultDto
- ✓ Music events: TrackPlayedEvent, PlaylistCreatedEvent, LibraryScanCompletedEvent, TrackScrobbledEvent
- ✓ `MusicModuleManifest` and `MusicModule` lifecycle
- ✓ Module project scaffolding (5 projects + test project)

---

### Section: Phase 5.9 - Music Data Model

#### Step: phase-5.9 - Music Data Model & Migrations

**Status:** completed ✅
**Deliverables:**

- ✓ 13 entity models: Artist, MusicAlbum, Track, TrackArtist, Genre, TrackGenre, Playlist, PlaylistTrack, PlaybackHistory, EqPreset, UserMusicPreference, ScrobbleRecord, StarredItem
- ✓ 13 EF Core configurations with indexes
- ✓ `MusicDbContext` with 13 DbSets
- ✓ `MusicDbContextDesignTimeFactory` for EF Core tooling
- ✓ InitialCreate migration (13 tables)

---

### Section: Phase 5.10 - Music Library Scanning

#### Step: phase-5.10 - Music Library Scanning & Metadata

**Status:** completed ✅
**Deliverables:**

- ✓ `LibraryScanService` — scans user files for audio MIME types, builds Artist→Album→Track hierarchy
- ✓ `MusicMetadataService` — tag reading/writing via TagLibSharp
- ✓ `AlbumArtService` — embedded art extraction, folder art fallback, thumbnail caching
- ✓ Supported formats: MP3, FLAC, OGG, AAC/M4A, OPUS, WAV, WMA

---

### Section: Phase 5.11 - Music Core Services

#### Step: phase-5.11 - Music Core Services

**Status:** completed ✅
**Deliverables:**

- ✓ `ArtistService` — browse, search, discography
- ✓ `MusicAlbumService` — browse, search, album tracks, album art
- ✓ `TrackService` — search, starred/favorites, recently added
- ✓ `PlaylistService` — CRUD, reorder, sharing
- ✓ `PlaybackService` — play history, scrobble recording, queue management
- ✓ `RecommendationService` — recently played, most played, similar, new additions
- ✓ `EqPresetService` — CRUD for equalizer presets

---

### Section: Phase 5.12 - Music Streaming

#### Step: phase-5.12 - Music Streaming Service

**Status:** completed ✅
**Deliverables:**

- ✓ `MusicStreamingService` — HTTP Range streaming, auth token generation/validation, concurrent stream limiting
- ✓ Gapless playback metadata, stream URL generation with time-limited tokens
- ✓ 15 streaming tests passing

---

### Section: Phase 5.13 - Subsonic API

#### Step: phase-5.13 - Subsonic API Compatibility

**Status:** completed ✅
**Deliverables:**

- ✓ `SubsonicController` — ~25 Subsonic REST API v1.16 endpoints
- ✓ `SubsonicAuth` — MD5 token+salt authentication
- ✓ System, browsing, search, media retrieval, playlist, user interaction endpoints
- ✓ XML + JSON response format support

---

### Section: Phase 5.14 - Music API & gRPC

#### Step: phase-5.14 - Music REST API, gRPC & Host

**Status:** completed ✅
**Deliverables:**

- ✓ `MusicController` — ~30 REST endpoints
- ✓ `MusicGrpcServiceImpl` + `music_service.proto`
- ✓ Music Host project (Kestrel, gRPC, health checks)
- ✓ 156 tests passing across all music services
- ✓ Blazor UI — full music player (library, albums, artists, genres, playlists, favorites, recently played, now-playing bar, equalizer, search)

**Notes:** Sub-Phase C (Music Module) fully complete including Blazor UI. Starring/favorites with optimistic UI updates, album art, playback controls, play bar with album navigation.

---

## Phase 5: MusicBrainz Metadata Enrichment (Sub-Phase C.1)

### Section: Phase A - Data Model Changes (Migration)

#### Step: phase-5-mb-A - MusicBrainz Data Model Changes

**Status:** completed ✅
**Deliverables:**

- ✓ Artist model: `MusicBrainzId`, `Biography`, `ImageUrl`, `WikipediaUrl`, `DiscogsUrl`, `OfficialUrl`, `LastEnrichedAt`
- ✓ MusicAlbum model: `MusicBrainzReleaseGroupId`, `MusicBrainzReleaseId`, `LastEnrichedAt`
- ✓ Track model: `MusicBrainzRecordingId`, `LastEnrichedAt`
- ✓ EF Core configurations updated with max lengths and indexes (ix_artists_musicbrainz_id, ix_music_albums_musicbrainz_release_group_id, ix_tracks_musicbrainz_recording_id)
- ✓ `AddMusicBrainzEnrichment` migration created
- ✓ 250 existing Music tests still passing

**Notes:** Phase A complete. All enrichment fields added as nullable columns — no breaking changes. Ready for Phase B (MusicBrainz + Cover Art Archive service clients).

---

### Section: Phase B - MusicBrainz + Cover Art Archive Services

#### Step: phase-5-mb-B - MusicBrainz + Cover Art Archive Services

**Status:** completed ✅
**Deliverables:**

- ✓ `IMusicBrainzClient` interface — search artists, release groups, recordings; get artist/release group/recording details
- ✓ `MusicBrainzClient` implementation — typed HttpClient, rate-limited (shared `MusicBrainzRateLimiter`), JSON deserialization of MB API v2 responses
- ✓ `ICoverArtArchiveClient` interface — get front cover, get cover list, fallback through releases
- ✓ `CoverArtArchiveClient` implementation — typed HttpClient, rate-limited, release fallback logic, MIME type detection
- ✓ `MusicBrainzRateLimiter` — shared `SemaphoreSlim`-based rate limiter (1 req/sec) for both MB and CAA clients
- ✓ `IMetadataEnrichmentService` interface — enrich album/artist/track, batch enrich missing art, batch enrich all
- ✓ `MetadataEnrichmentService` implementation — orchestrates MB lookups + CAA art fetching, score threshold ≥90, 30-day cooldown, force flag override, progress reporting
- ✓ `EnrichmentProgress` DTO added to `MusicDtos.cs`
- ✓ Full solution build: 0 errors
- ✓ 250 existing Music tests still passing

**Notes:** Phase B complete. All MusicBrainz and Cover Art Archive service interfaces and implementations created. Rate limiting shared between clients via singleton `MusicBrainzRateLimiter`. Service registration (DI + HttpClient configuration) deferred to Phase F. Ready for Phase C (scan progress infrastructure).

---

### Section: Phase C - Scan Progress Infrastructure

#### Step: phase-5-mb-C - Scan Progress Infrastructure

**Status:** completed ✅
**Deliverables:**
- ✓ `LibraryScanProgress` DTO — real-time progress record with phase, file counts, track stats, percentage, elapsed time
- ✓ `LibraryScanService` updated — accepts `IProgress<LibraryScanProgress>?`, reports per-file progress, runs enrichment phase (auto-fetch art + auto-enrich artists) controlled by configuration
- ✓ `ScanProgressState` — scoped Blazor state service bridging `IProgress<T>` callbacks to `StateHasChanged()` via `OnProgressChanged` event
- ✓ `ScanProgressState` registered as scoped in `MusicServiceRegistration`
- ✓ `IMetadataEnrichmentService?` injected into `LibraryScanService` as optional dependency
- ✓ Configuration-driven enrichment: `Music:Enrichment:Enabled`, `AutoFetchArt`, `AutoEnrichArtists`
- ✓ Full solution build: 0 errors, 250 existing tests passing

**Notes:** Phase C complete. Scan progress infrastructure in place. `ScanLibraryAsync` now reports real-time progress through all phases (metadata extraction → enrichment → complete) and supports cancellation. Enrichment runs automatically after scan if configured. The `ScanProgressState` service enables Blazor components to subscribe to progress updates. Ready for Phase D (API endpoints).

---

### Section: Phase D - API Endpoints

#### Step: phase-5-mb-D - Enrichment & Scan Progress API Endpoints

**Status:** completed ✅
**Deliverables:**

- ✓ `POST /api/v1/music/enrich/album/{albumId}` — enrich single album (MusicBrainz metadata + Cover Art Archive cover art)
- ✓ `POST /api/v1/music/enrich/artist/{artistId}` — enrich single artist (biography, external links)
- ✓ `POST /api/v1/music/enrich/all` — batch enrich all unenriched items for user
- ✓ `POST /api/v1/music/enrich/missing-art` — batch enrich only albums missing cover art
- ✓ `GET /api/v1/music/artists/{artistId}/bio` — get artist biography and external links
- ✓ `GET /api/v1/music/scan/progress` — current scan progress for authenticated user
- ✓ `ArtistBioDto` — new DTO with biography, image URL, Wikipedia/Discogs/official links, MusicBrainz ID
- ✓ `IArtistService.GetArtistBioAsync()` — new interface method + implementation
- ✓ `IMetadataEnrichmentService` and `ScanProgressState` injected into `MusicController`
- ✓ Full solution build: 0 errors, 250 tests passing

**Notes:** Phase D complete. All enrichment and scan progress REST API endpoints added to `MusicController`. Single-item enrichment endpoints support `?force=true` query parameter to bypass 30-day cooldown. Artist bio endpoint returns enriched data including biography, external links, and enrichment timestamp. Scan progress endpoint returns current `ScanProgressState` for non-Blazor clients. Ready for Phase E (Blazor UI updates).

---

### Section: Phase E - Blazor UI Updates

#### Step: phase-5-mb-E - Blazor UI Updates

**Status:** completed ✅
**Deliverables:**

- ✓ E1: Scan progress UI overhaul — progress bar, phase indicator, file counts (added/updated/skipped/failed/art), elapsed time, cancel button hooked to CancellationTokenSource
- ✓ E2: Album enrichment UI — "Fetch Cover Art" button on albums without art, spinner during enrichment, toast notification on success
- ✓ E3: Artist enrichment UI — biography section, external links (Wikipedia/Discogs/Website), artist image, "Fetch Info" button with spinner
- ✓ E4: Settings enrichment toggles — auto-fetch metadata and auto-fetch album art checkboxes, persisted via UserSettingsService
- ✓ ~300 lines of scoped CSS for all new UI elements (progress bar, artist bio, toggles, toast animations)
- ✓ Full solution build: 0 errors, 250 tests passing

**Notes:** Phase E complete. All Blazor UI components for MusicBrainz enrichment are in place. Scan progress panel shows real-time progress with cancel support. Album and artist detail views have contextual enrichment buttons. Settings section includes enrichment toggle controls. Ready for Phase G (comprehensive unit tests).

---

### Section: Phase F - Service Registration + Configuration

#### Step: phase-5-mb-F - Service Registration + Configuration

**Status:** completed ✅
**Deliverables:**

- ✓ `MusicBrainzRateLimiter` registered as singleton in `MusicServiceRegistration.cs`
- ✓ `AddHttpClient<IMusicBrainzClient, MusicBrainzClient>` — base URL `https://musicbrainz.org/ws/2/`, Accept + User-Agent headers
- ✓ `AddHttpClient<ICoverArtArchiveClient, CoverArtArchiveClient>` — base URL `https://coverartarchive.org/`
- ✓ `MetadataEnrichmentService` registered as scoped with interface forward-registration
- ✓ `Microsoft.Extensions.Http` package reference added to `DotNetCloud.Modules.Music.Data.csproj`
- ✓ Rate limit configurable via `Music:Enrichment:RateLimitMs` (default 1100ms)
- ✓ Full solution build: 0 errors, 250 tests passing

**Notes:** Phase F complete. All MusicBrainz enrichment services registered in DI. HTTP clients configured with proper base URLs, headers, and shared rate limiting. Done as a dependency of Phase E (UI needs injected services). Ready for Phase G (comprehensive unit tests).

---

### Section: Phase G - Comprehensive Unit Tests

#### Step: phase-5-mb-G - Comprehensive Unit Tests

**Status:** completed ✅
**Deliverables:**

- ✓ `MockHttpMessageHandler.cs` — shared reusable HTTP mock infrastructure (ForJson, ForBytes, ForStatus, ForSequence, ForNetworkError, ForTimeout, ForRoutes, ForException)
- ✓ `MusicBrainzClientTests.cs` — 23 tests: URL construction, JSON deserialization, rate limiting, error handling
- ✓ `CoverArtArchiveClientTests.cs` — 15 tests: image fetching, release fallback, MIME types, error handling
- ✓ `MetadataEnrichmentServiceTests.cs` — 30 tests: album/artist/track enrichment, batch operations, progress, caching, cooldown
- ✓ `LibraryScanProgressTests.cs` — 12 tests: progress reporting, enrichment integration, cancellation
- ✓ `ScanProgressStateTests.cs` — 8 tests: Blazor scoped state, event notifications, multiple subscribers
- ✓ `TestHelpers.cs` updated — `SeedAlbumWithoutArtAsync`, `SeedEnrichedArtistAsync`, `CreateMockMusicBrainzArtistJson`
- ✓ `DotNetCloud.Modules.Music.Tests.csproj` — added `Microsoft.Extensions.Configuration` package
- ✓ Full test suite: 338 tests passing (88 new + 250 existing)

**Notes:** Phase G complete. All MusicBrainz enrichment plan phases (A–G) now fully implemented. 88 new unit tests covering all service clients, enrichment orchestration, scan progress, and Blazor state. No existing tests broken.

---

## Phase 5: Video Module (Sub-Phase D)

### Section: Phase 5.15 - Video Contracts & Data

#### Step: phase-5.15 - Video Architecture, Contracts & Data Model

**Status:** completed ✅
**Deliverables:**

- ✓ `IVideoDirectory` capability interface
- ✓ Video DTOs and events
- ✓ `VideoModuleManifest` and `VideoModule` lifecycle
- ✓ 8 entity models with EF configurations
- ✓ `VideoDbContext` with 8 DbSets
- ✓ `VideoDbContextDesignTimeFactory` for EF Core tooling
- ✓ InitialCreate migration (8 tables)

---

### Section: Phase 5.16 - Video Core Services

#### Step: phase-5.16 - Video Core Services

**Status:** completed ✅
**Deliverables:**

- ✓ `VideoService` — CRUD, search, recently watched, favorites
- ✓ `VideoMetadataService` — metadata persistence
- ✓ `VideoCollectionService` — collections/series management
- ✓ `SubtitleService` — SRT/VTT upload, parsing, association
- ✓ `WatchProgressService` — watch position tracking, resume playback
- ✓ `FileUploadedVideoHandler` — event handler for 12 video MIME types
- ✓ 74 tests passing

---

### Section: Phase 5.17 - Video Streaming & API

#### Step: phase-5.17 - Video Streaming & API

**Status:** completed ✅
**Deliverables:**

- ✓ `VideoStreamingService` — token-based streaming URL generation/validation
- ✓ `VideoController` — ~20 REST endpoints
- ✓ `VideoGrpcServiceImpl` + `video_service.proto` (12 RPCs)
- ✓ Video Host project (Kestrel, gRPC, health checks)

---

### Section: Phase 5.18 - Video Web UI

#### Step: phase-5.18 - Video Web UI

**Status:** completed ✅
**Notes:** Sub-Phase D (Video Module) fully complete. All projects compile, 105 tests passing. Video is a separate process-isolated module.

---

### Section: Phase 5.19 - Cross-Module Integration

#### Step: phase-5.19 - Cross-Module Integration (Photos ↔ Music ↔ Video ↔ Files)

**Status:** completed ✅
**Deliverables:**

- ✓ `FileUploadedPhotoHandler` with `IPhotoIndexingCallback` — 9 image MIME types, callback pattern
- ✓ `FileUploadedMusicHandler` with `IMusicIndexingCallback` — 15 audio MIME types
- ✓ `FileUploadedVideoHandler` with `IVideoIndexingCallback` — 12 video MIME types
- ✓ `IMediaSearchService` + `MediaSearchResultDto` — cross-module search aggregation
- ✓ `AlbumSharedNotificationHandler`, `PlaylistSharedNotificationHandler`, `VideoSharedNotificationHandler`
- ✓ `MediaDashboardDto`, `VideoContinueWatchingDto`, `RecentMediaItemDto` — dashboard widgets
- ✓ 8 new `CrossModuleLinkType` enum values for navigation integration
- ✓ `PhotoIndexingCallback`, `MusicIndexingCallback`, `VideoIndexingCallback` — Data layer bridges
- ✓ `VideoService.CreateVideoAsync` — duplicate detection + VideoAddedEvent publishing
- ✓ All service registrations updated (3 handlers, 3 callbacks, 3 notification handlers)

**Notes:** Used callback interface pattern to avoid Module→Data circular dependency. All projects build clean (0 errors).

---

### Section: Phase 5.20 - Testing & Documentation

#### Step: phase-5.20 - Comprehensive Test Suites

**Status:** completed ✅ (test suites — security/perf/docs deferred)
**Deliverables:**

- ✓ Photos: 119 tests total (24 new: 12 handler + 6 notification + 6 callback)
- ✓ Music: 156 tests total (25 new: 12 handler + 9 notification + 4 callback)
- ✓ Video: 105 tests total (31 new: 12 handler + 9 notification + 10 service + 5 callback)
- ✓ Core: 410 tests total (16 new cross-module DTO tests)
- ✓ All 790 tests passing across 4 projects
- ✓ Tracks-style collapsed sidebar polish for Photos, Music, and Video, including layout shrink behavior and persisted Video sidebar state
- ☐ Security tests, performance tests, admin/user/API docs — deferred

**Notes:** Sub-Phase E complete. All test targets exceeded (Photos 119≥80, Music 156≥100, Video 105≥60). Phase 5 integration code done. Media module sidebars now follow the same icon-first collapsed pattern as Tracks/Files, with Photos albums and Music playlists hidden in collapsed mode, Music/Video layouts shrinking correctly with the sidebar, and Video persisting the collapse preference. The updated modules were validated through `dotnet build DotNetCloud.CI.slnf` and a successful healthy bare-metal redeploy.

---

## Future: Multi-Root Sync (Scoped for Future Phase)

> **Priority:** Medium — enhances sync client usability significantly  
> **Prerequisite:** Phase 1 sync client stable and shipping  
> **Effort estimate:** Medium (client changes are straightforward; server already supports `folderId` scoping)

### Overview

Allow users to sync multiple local folders (e.g. Documents, Pictures, Desktop) to separate server-side virtual roots, rather than requiring everything under a single sync folder. This is the approach used by Nextcloud, Syncthing, and Dropbox.

### Current State (already supports multi-context)

- `SyncContextManager._contexts` is a `Dictionary<Guid, RunningContext>` — **multiple contexts already work**
- `AddContextAsync` has **no single-context limit** — each call creates a new context with its own engine, state DB, and token store
- Server-side `GET /api/v1/files/sync/changes?folderId={id}` and `GET /api/v1/files/sync/tree?folderId={id}` already accept an optional `folderId` for scoping to a sub-tree
- The single-account limit is only enforced in the **UI** (`CanAddAccount => !HasAccount`), not the engine

### What's Needed

#### Server-Side

- ☐ API for managing per-device sync root mappings (`POST /api/v1/sync/roots`, `GET /api/v1/sync/roots`)
- ☐ Each root maps a server folder ID to a client-chosen local path label
- ☐ SSE stream scoped per root (or multiplexed with root ID in event payload)

#### Client-Side

- ☐ `SyncContextRegistration` gains a `ServerFolderId` (nullable `Guid?`) — when set, the engine passes it to `sync/changes` and `sync/tree`
- ☐ `SyncEngine` passes `folderId` query param to API calls when `ServerFolderId` is set
- ☐ Settings UI: "Add Sync Folder" button under the account — opens a server folder picker + local folder chooser
- ☐ Each sync root gets its own card in the Accounts tab showing local path, server path, status, and remove button
- ☐ Each root has independent selective sync, state DB, and chunk cache
- ☐ Tray menu shows per-root "Open Folder" entries

#### UX Flow

1. User connects account (as today — creates a default "all files" root)
2. User clicks "Add Sync Folder" in Settings
3. Server folder picker shows top-level server folders (Documents, Photos, etc.)
4. User picks server folder + chooses local path (e.g. `C:\Users\benk\Documents`)
5. New sync context starts for that root pair

### Workaround (Available Today)

Power users can create directory junctions inside the sync folder:

```powershell
New-Item -ItemType Junction -Path "C:\Users\benk\synctray\Documents" -Target "C:\Users\benk\Documents"
```

The sync engine follows junction contents transparently. Caveat: deleting the junction server-side could affect real local files.

---

## Phase 9: AI Assistant

### Section: Phase 9.1 — Core AI Interfaces & Module Scaffold
**Status:** completed ✅
**Deliverables:**
- ✓ `ILlmProvider` capability interface (Restricted tier) in `DotNetCloud.Core/Capabilities/`
- ✓ Core DTOs: `LlmRequest`, `LlmResponse`, `LlmResponseChunk`, `LlmModelInfo`, `LlmMessage` in `DotNetCloud.Core/AI/`
- ✓ `AiModule` (IModuleLifecycle) and `AiModuleManifest` (IModuleManifest)
- ✓ Domain models: `Conversation`, `ConversationMessage`
- ✓ Events: `ConversationCreatedEvent`, `ConversationMessageEvent`
- ✓ Service interfaces: `IAiChatService`, `IOllamaClient`
- ✓ Module manifest (`manifest.json`)

**Notes:** Foundation layer complete. ILlmProvider follows the existing capability tier model.

### Section: Phase 9.2 — Data Layer & Ollama Provider
**Status:** completed ✅
**Deliverables:**
- ✓ `AiDbContext` with EF Core entity configurations
- ✓ `ConversationConfiguration` / `ConversationMessageConfiguration` with soft-delete, indexes
- ✓ `OllamaClient` — Full Ollama REST API client (chat, streaming NDJSON, model listing, health check)
- ✓ `AiChatService` — Conversation CRUD, history-aware LLM requests, message persistence
- ✓ `AiServiceRegistration` — DI with `HttpClientFactory`, configurable base URL
- ✓ `IAiSettingsProvider` / `AiSettingsProvider` — DB-backed settings with IConfiguration fallback

**Notes:** Default Ollama URL `http://localhost:11434/` for fresh installs, configurable via admin settings. Default model `gpt-oss:20b`. InMemory DB for dev.

### Section: Phase 9.3 — Module Host & REST API
**Status:** completed ✅
**Deliverables:**
- ✓ `DotNetCloud.Modules.AI.Host` — Standalone web host (`Program.cs`)
- ✓ `AiChatController` — REST API endpoints:
  - POST `/api/ai/conversations` — Create conversation
  - GET `/api/ai/conversations` — List conversations
  - GET `/api/ai/conversations/{id}` — Get conversation with messages
  - DELETE `/api/ai/conversations/{id}` — Soft-delete conversation
  - POST `/api/ai/conversations/{id}/messages` — Send message (full response)
  - POST `/api/ai/conversations/{id}/messages/stream` — Send message (SSE streaming)
  - GET `/api/ai/models` — List available models
  - GET `/api/ai/health/ollama` — Ollama health check
- ✓ `AiHealthCheck` — Ollama connectivity health check
- ✓ `InProcessEventBus` — Standalone operation event bus

**Notes:** All projects registered in DotNetCloud.sln. Build succeeds with 0 warnings.

### Section: Phase 9.4 — Unit Tests
**Status:** completed ✅
**Deliverables:**
- ✓ `AiModuleTests` — 7 lifecycle tests (init, start, stop, event sub/unsub)
- ✓ `AiChatServiceTests` — 11 tests (CRUD, ownership, message sending, model listing)
- ✓ `OllamaClientTests` — 10 tests (health, chat, models, system prompt, error handling)
- ✓ All 28 tests passing

**Notes:** Tests use InMemory EF Core and mocked HttpMessageHandler — no Ollama instance required.

### Section: Phase 9.5 — Blazor UI Chat Panel
**Status:** pending ☐
**Deliverables:**
- ☐ Chat-style AI assistant panel component
- ☐ Streaming response rendering via SSE
- ☐ Model selector dropdown
- ☐ Conversation history sidebar

### Section: Phase 9.6 — Admin Settings & Multi-Provider Support
**Status:** in-progress 🔄
**Deliverables:**
- ✓ `AiAdminSettingsViewModel` — Settings model (Provider, ApiBaseUrl, ApiKey, OrgId, DefaultModel, MaxTokens, Timeout)
- ✓ `AiAdminSettings.razor` / `.razor.cs` — Blazor admin UI with provider-aware sections
- ✓ `IAiSettingsProvider` / `AiSettingsProvider` — DB-first settings with IConfiguration fallback
- ✓ `OllamaClient` dynamic base URL from settings (live reconfiguration, no restart)
- ✓ DB seed: 7 AI settings via `DbInitializer` with backfill for existing databases
- ✓ Provider selection: Ollama (local), OpenAI, Anthropic — auth fields shown/hidden per provider
- ☐ Full OpenAI-compatible request routing (Authorization header, API paths)
- ☐ Full Anthropic-compatible request routing (x-api-key header, Messages API)
- ☐ Per-user API key storage (encrypted)
- ☐ Rate limiting per user

**Notes:** Admin settings infrastructure complete. Ollama fully working via DB settings. OpenAI/Anthropic provider routing pending.

### Section: Phase 9.7 — Module Integration
**Status:** pending ☐
**Deliverables:**
- ☐ Notes module integration (summarize, expand, translate)
- ☐ Chat module integration (message summarization, smart replies)
- ☐ Files module integration (content summarization, document Q&A)

---

## Phase 8: Full-Text Search Module

**Reference:** `docs/FULL_TEXT_SEARCH_IMPLEMENTATION_PLAN.md`

### Section: Phase 8.2 — Search Module Scaffold
**Status:** completed ✅
**Duration:** ~3 hours
**Deliverables:**
- ✓ `DotNetCloud.Modules.Search/` — Business logic project (services, extractors, event handler, module lifecycle)
- ✓ `DotNetCloud.Modules.Search.Data/` — EF Core data project (SearchDbContext, SearchIndexEntry, IndexingJob, configurations)
- ✓ `DotNetCloud.Modules.Search.Host/` — gRPC host + REST controllers (search_service.proto, SearchGrpcService, SearchController, Program.cs)
- ✓ 3 provider-specific ISearchProvider implementations (PostgreSQL, SQL Server, MariaDB)
- ✓ 5 content extractors (PlainText, Markdown, PDF via PdfPig, DOCX, XLSX via OpenXml)
- ✓ SearchModule + SearchModuleManifest (IModuleLifecycle, event subscription)
- ✓ SearchIndexingService (Channel-based background queue), SearchQueryService, ContentExtractionService, SearchReindexBackgroundService
- ✓ InProcessEventBus for standalone module operation
- ✓ REST endpoints: GET /search, GET /suggest, GET /stats, POST /admin/reindex, POST /admin/reindex/{moduleId}
- ✓ `DotNetCloud.Modules.Search.Tests` — 116 tests, all passing (12 test files covering providers, services, extractors, module lifecycle, DbContext)

**Notes:** Phase 2 complete. Search module scaffold fully operational with EF Core InMemory. Phase 1 interfaces/DTOs (in DotNetCloud.Core) were already in place.

### Section: Phase 8.3 — Module Search API Integration
**Status:** completed ✅
**Duration:** ~4 hours
**Deliverables:**
- ✓ Search RPCs added to all 9 module protos (Files, Chat, Notes, Contacts, Calendar, Photos, Music, Video, Tracks)
- ✓ `GetSearchableDocuments` (server streaming) + `GetSearchableDocument` (unary) + `SearchableDocument` message per module
- ✓ gRPC service implementations mapping domain entities to SearchableDocument in all 9 modules
- ✓ `SearchIndexRequestEvent` publishing on CRUD operations in 10 service files across 9 modules
- ✓ 8 new test files with 23 SearchIndex tests — all passing
- ✓ Zero regressions across full test suite

**Notes:** Phase 3 complete. All modules now expose searchable data via gRPC and publish SearchIndexRequestEvent on CRUD operations. AI module excluded (REST only, no proto/gRPC). Next: Phase 4 — Indexing Engine (event-driven + scheduled reindex).

### Section: Phase 8.4 — Indexing Engine
**Status:** completed ✅
**Deliverables:**
- ✓ `SearchIndexingService` upgraded — Channel-based queue with Start/Stop lifecycle, module lookup, content extraction pipeline
- ✓ `SearchReindexBackgroundService` — Full reindex, per-module reindex, batch processing (200 default), IndexingJob tracking
- ✓ `SearchIndexRequestEventHandler` — Routes Index → indexing service, Remove → provider directly
- ✓ Orphaned entry cleanup for unregistered modules
- ✓ 43 Phase 4 tests in 5 test files (IndexingService, EventHandler, ReindexService, ContentExtraction, IntegrationPipeline)
- ✓ 212 total search tests passing

**Notes:** Phase 4 complete. Background indexing pipeline processes events asynchronously via channel queue. Full and per-module reindex with batch processing and job tracking.

### Section: Phase 8.5 — Search Query Engine
**Status:** completed ✅
**Deliverables:**
- ✓ `SearchQueryParser` — Parses user input into structured `ParsedSearchQuery` (keywords, phrases, in:module, type:value, -exclusion)
- ✓ `ParsedSearchQuery` with provider-specific query string builders (PostgreSQL tsquery, SQL Server CONTAINS, MariaDB BOOLEAN MODE)
- ✓ `SnippetGenerator` — HTML-safe snippet generation with `<mark>` highlighting and XSS prevention
- ✓ `SearchQueryService` upgraded — Parser integration, filter extraction from query syntax, empty/filter-only short-circuit
- ✓ All 3 providers (PostgreSQL, SQL Server, MariaDB) upgraded — parsed query support, exclusion WHERE clauses, relevance scoring, title/snippet highlighting, facet queries
- ✓ 6 new test files with ~125 Phase 5 tests (Parser, ParsedQuery, Snippet, Integration, Aggregation, ServicePhase5)
- ✓ 343 total search tests passing

**Notes:** Phase 5 complete. Full query engine with advanced syntax parsing, provider-specific query translation, relevance scoring, and highlighted snippets. Next: Phase 6 — REST + gRPC API integration.

### Section: Phase 8.6 — REST + gRPC API
**Status:** completed ✅
**Deliverables:**
- ✓ `DotNetCloud.Modules.Search.Client` project — shared gRPC client library
- ✓ `ISearchFtsClient` interface with IsAvailable + SearchAsync
- ✓ `SearchFtsClient` — lazy GrpcChannel, Unix socket support, timeout config, graceful degradation
- ✓ `SearchFtsClientOptions` — SearchModuleAddress + Timeout configuration
- ✓ `SearchClientServiceExtensions` — AddSearchFtsClient DI registration
- ✓ Files, Chat, Notes controllers updated — FTS first, fallback to LIKE
- ✓ 7 new test files with 89 Phase 6 tests (Controller, gRPC, FtsClient, Options, Extensions, EnhancedModule, Integration)
- ✓ 432 total search tests passing

**Notes:** Phase 6 complete. Search module client library with graceful degradation. Module controllers upgraded with FTS-first search. Next: Phase 7 — Blazor UI.

### Section: Phase 8.7 — Blazor UI
**Status:** completed ✅
**Deliverables:**
- ✓ `GlobalSearchBar.razor` — Modal search overlay with Ctrl+K/Cmd+K shortcut, debounced suggestions, keyboard navigation, recent searches (localStorage)
- ✓ `SearchResults.razor` — Full results page at `/search?q=...` with faceted sidebar, pagination, sort (relevance/date)
- ✓ `SearchResultCard.razor` — Per-module result card with XSS-safe highlight, module-specific metadata rendering (10 modules)
- ✓ `global-search.js` — JS interop for keyboard shortcut registration and localStorage management
- ✓ Scoped CSS for all 3 components (responsive, dark mode support)
- ✓ `DotNetCloudApiClient` — SearchAsync + SearchSuggestAsync methods added
- ✓ MainLayout integration (topbar-center), _Imports.razor, App.razor script tag
- ✓ 6 new test files with 159 Phase 7 tests (URLs, Sanitizer, Display, QueryBuilder, Metadata, Sort/EdgeCases)
- ✓ 591 total search tests passing

**Notes:** Phase 7 complete. Full Blazor search UI with global search bar (Ctrl+K), results page with facets and pagination, and per-module rich result cards. All 8 FTS implementation phases (2-7 + testing) are now complete.

### Section: Phase 8.8 — Testing & Documentation
**Status:** completed ✅
**Deliverables:**
- ✓ `PermissionScopingTests` — 10 tests (user isolation across providers, facet/filter/pagination scoping)
- ✓ `EndToEndIndexingTests` — 12 tests (full pipeline: event → handler → indexing → provider → query)
- ✓ `MultiDatabaseProviderTests` — 10 tests (SqlServer/MariaDb behavioral consistency)
- ✓ `PerformanceBenchmarkTests` — 8 tests (indexing throughput, query latency p50/p95, concurrent searches)
- ✓ `docs/modules/SEARCH.md` — Module documentation (architecture, features, services, extractors, providers, schema, configuration, admin operations, test matrix)
- ✓ `docs/api/search.md` — API reference (REST endpoints, gRPC RPCs, advanced query syntax, client library, permission model)
- ✓ `docs/architecture/ARCHITECTURE.md` — Section 25: Full-Text Search Architecture (indexing pipeline, query engine, API surface, content extraction)
- ✓ 631 total search tests passing (40 Phase 8 + 591 previous)

**Notes:** Phase 8 complete. Testing & documentation finalize the full-text search module. All 8 implementation phases delivered: module scaffold, module API integration, indexing engine, query engine, REST/gRPC API, Blazor UI, testing & documentation. 631 tests across all phases.

---

## Phase 7: Video Calling & Screen Sharing

### Step: phase-7.1 — Architecture & Contracts

**Status:** completed ✅
**Depends on:** Chat module (Phase 2, complete)
**Deliverables:**
- ✓ `VideoCallState` enum (`Ringing`, `Connecting`, `Active`, `OnHold`, `Ended`, `Missed`, `Rejected`, `Failed`)
- ✓ `VideoCallEndReason` enum (`Normal`, `Rejected`, `Missed`, `TimedOut`, `Failed`, `Cancelled`)
- ✓ `CallParticipantRole` enum (`Initiator`, `Participant`)
- ✓ `CallMediaType` enum (`Audio`, `Video`, `ScreenShare`)
- ✓ DTOs: `VideoCallDto`, `CallParticipantDto`, `CallSignalDto`, `StartCallRequest`, `JoinCallRequest`, `CallHistoryDto`
- ✓ Events: `VideoCallInitiatedEvent`, `VideoCallAnsweredEvent`, `VideoCallEndedEvent`, `VideoCallMissedEvent`, `ParticipantJoinedCallEvent`, `ParticipantLeftCallEvent`, `ScreenShareStartedEvent`, `ScreenShareEndedEvent`
- ✓ Service interface: `IVideoCallService` (7 methods)
- ✓ Service interface: `ICallSignalingService` (4 methods)
- ✓ `ChatModuleManifest.cs` updated with 8 new published events

**Notes:** Phase 7.1 complete. All contracts, enums, DTOs, events, and service interfaces defined. Chat module builds cleanly (0 warnings, 0 errors). All 323 existing Chat tests pass. Ready for phase-7.2 (Data Model & Migration).

### Step: phase-7.2 — Data Model & Migration

**Status:** completed ✅
**Depends on:** 7.1
**Deliverables:**
- ✓ `VideoCall` entity — Id, ChannelId (FK → Channel), InitiatorUserId, State, MediaType, StartedAtUtc, EndedAtUtc, EndReason, MaxParticipants, IsGroupCall, LiveKitRoomId, CreatedAtUtc, soft-delete
- ✓ `CallParticipant` entity — Id, VideoCallId (FK → VideoCall), UserId, Role, JoinedAtUtc, LeftAtUtc, HasAudio, HasVideo, HasScreenShare
- ✓ `VideoCallConfiguration.cs` — Enum-to-string conversions, soft-delete query filter, indexes (ChannelId+State, InitiatorUserId, CreatedAtUtc, State, IsDeleted)
- ✓ `CallParticipantConfiguration.cs` — Unique composite index (VideoCallId+UserId), indexes (UserId+JoinedAtUtc, UserId), cascade delete
- ✓ `ChatDbContext` — Added `DbSet<VideoCall>` and `DbSet<CallParticipant>`
- ✓ EF migration `AddVideoCalling` (creates VideoCalls + CallParticipants tables with all indexes and FKs)
- ✓ 65 comprehensive tests (20 VideoCall model, 14 CallParticipant model, 31 EF/DB integration)

**Notes:** Phase 7.2 complete. Data model follows existing Chat patterns (soft-delete, enum-to-string, cascade FKs). All 65 new tests pass. Ready for phase-7.3 (Call Management Service).

### Step: phase-7.3 — Call Management Service

**Status:** completed ✅
**Depends on:** 7.2
**Deliverables:**
- ✓ `VideoCallService` — full `IVideoCallService` implementation (InitiateCallAsync, JoinCallAsync, LeaveCallAsync, EndCallAsync, RejectCallAsync, GetCallHistoryAsync, GetActiveCallAsync)
- ✓ `CallStateValidator` — static state machine enforcement with valid transitions, terminal state detection
- ✓ Call timeout — `HandleRingTimeoutsAsync` transitions Ringing calls to Missed after 30s
- ✓ DI registration in `ChatServiceRegistration.cs` (scoped)
- ✓ 110 comprehensive tests (39 CallStateValidator + 71 VideoCallService)

**Notes:** Call management service complete. State machine: Ringing → Connecting/Active/Ended/Missed/Rejected/Failed. Auto-end on last participant leave. Group calls allow rejection without ending. Ready for phase-7.4 (WebRTC Signaling).

### Step: phase-7.4 — WebRTC Signaling over SignalR

**Status:** completed ✅
**Depends on:** 7.3
**Deliverables:**
- ✓ `CallSignalingService` — server-side signaling coordinator with SDP/ICE relay, call state validation, participant membership enforcement
- ✓ `CoreHub` signaling methods — `SendCallOfferAsync`, `SendCallAnswerAsync`, `SendIceCandidateAsync`, `SendMediaStateChangeAsync`, `JoinCallGroupAsync`, `LeaveCallGroupAsync`
- ✓ Call-scoped SignalR groups (`call-{callId}`)
- ✓ Input validation (SDP max 64KB, ICE max 4KB, UTF-8 byte counting)
- ✓ 85 unit tests (62 CallSignalingService + 23 CoreHub signaling)

**Notes:** WebRTC signaling complete. Media state changes update DB (HasAudio/HasVideo/HasScreenShare). Screen share toggles publish events via IEventBus. Ready for phase-7.5 (Client-Side WebRTC Engine).

### Step: phase-7.5 — Client-Side WebRTC Engine (JS Interop)

**Status:** completed ✅
**Depends on:** 7.4
**Deliverables:**
- ✓ `video-call.js` — Full WebRTC engine (P2P mesh, SDP negotiation, ICE handling, adaptive bitrate)
- ✓ `IWebRtcInteropService` + `WebRtcInteropService` — C# Blazor ↔ JS interop with input validation
- ✓ `WebRtcDtos.cs` — `IceServerDto`, `WebRtcCallConfig`, `WebRtcCallState`, `WebRtcPeerState`, `WebRtcMediaState`
- ✓ P2P mesh topology for 2-3 participants (one RTCPeerConnection per peer, max 3)
- ✓ STUN/TURN configuration injection from server ICE config
- ✓ Adaptive bitrate: connection stats monitoring + automatic video quality adjustment (good/fair/poor)
- ✓ Screen share with browser-native stop detection and track replacement
- ✓ DI registration in `ChatServiceRegistration.cs`
- ✓ Script reference in `App.razor`
- ✓ 111 comprehensive tests (82 WebRtcInteropService + 29 WebRtcDto)

**Notes:** Client-side WebRTC engine complete. JS follows existing IIFE namespace pattern (`window.dotnetcloudVideoCall`). C# interop service validates SDP (64KB max), ICE candidates (4KB max), peer IDs, element IDs, stream types, and ICE config before delegating to JS. Ready for phase-7.6 (Blazor UI) and phase-7.8 (STUN/TURN config).

### Step: phase-7.6 — Blazor UI Components

**Status:** completed ✅
**Depends on:** 7.5
**Deliverables:**
- ✓ `VideoCallDialog.razor` — main call window with adaptive grid layout (solo/pair/trio/grid)
- ✓ `CallControls.razor` — bottom toolbar with mute, camera, screen share, hang up, timer, participant count
- ✓ `IncomingCallNotification.razor` — incoming call toast with accept (audio/video) and reject
- ✓ `CallHistoryPanel.razor` — call history sidebar with outcome formatting, duration, callback
- ✓ Extended `ChannelHeader.razor` with audio/video call buttons, join active call, call history toggle
- ✓ Scoped CSS for all 4 new components + ChannelHeader extensions
- ✓ All components wired into `ChatPageLayout.razor` with state fields and handlers
- ✓ 118 unit tests passing (5 test files)

**Notes:** All Blazor UI components complete. Fields for SignalR-driven state (call state, participants, etc.) are declared with CS0649 pragma — will be assigned when Phase 7.9 (SignalR wiring) is implemented.

### Step: phase-7.7 — LiveKit Integration (Optional SFU)

**Status:** completed ✅
**Depends on:** 7.4
**Deliverables:**
- ✓ `ILiveKitService` interface (CreateRoomAsync, GenerateToken, DeleteRoomAsync, GetRoomParticipantsAsync)
- ✓ `LiveKitService` implementation with JWT token generation (HMAC-SHA256) and LiveKit Twirp API
- ✓ `LiveKitOptions` configuration class (Enabled, ServerUrl, ApiKey, ApiSecret, MaxP2PParticipants)
- ✓ `NullLiveKitService` — graceful degradation when LiveKit not configured
- ✓ Auto-escalation in VideoCallService.JoinCallAsync (P2P ≤3 → LiveKit SFU 4+)
- ✓ LiveKit room cleanup on call end
- ✓ DI registration with conditional factory (LiveKitService vs NullLiveKitService)
- ✓ `appsettings.json` configuration section
- ✓ 86 new tests (LiveKitServiceTests, LiveKitOptionsTests, NullLiveKitServiceTests, auto-escalation tests)

**Notes:** LiveKit integration complete. Zero additional NuGet dependencies — JWT generation uses System.Security.Cryptography HMAC-SHA256. Process supervisor integration (managed component pattern like Collabora) deferred to deployment phase. All 864 Chat module tests pass.

### Step: phase-7.8 — STUN/TURN Configuration

**Status:** completed ✅
**Depends on:** 7.5
**Deliverables:**
- ✓ `IceServerOptions` configuration class (built-in STUN, additional STUN, TURN with static/ephemeral credentials)
- ✓ Built-in STUN server (`StunServer` BackgroundService) — RFC 5389 Binding Response, dual-stack IPv4/IPv6, UDP 3478
- ✓ `IIceServerService` interface + `IceServerService` with HMAC-SHA1 coturn-compatible ephemeral credentials
- ✓ API endpoint: `GET /api/v1/chat/ice-servers`
- ✓ `appsettings.json` Chat:IceServers configuration section
- ✓ Removed Google STUN fallback from video-call.js
- ✓ 73 new tests (IceServerOptionsTests, IceServerServiceTests, StunServerTests)

**Notes:** Privacy-first: self-hosted STUN by default, no Google dependency. Firewall must allow UDP 3478 inbound. Admin settings UI deferred to Phase 7.11.

### Step: phase-7.9 — REST API & gRPC Updates

**Status:** completed ✅
**Depends on:** 7.3
**Deliverables:**
- ✓ 9 REST API endpoints in ChatController (initiate, join, leave, end, reject, history, get call, active call, ICE servers)
- ✓ 7 gRPC RPCs + 12 message types in chat_service.proto
- ✓ ChatGrpcService implementation with IVideoCallService injection
- ✓ Rate limiting: 1 call initiation per 5 seconds per user
- ✓ Authorization via CallerContext + channel membership checks
- ✓ GetCallByIdAsync added to IVideoCallService interface + implementation
- ✓ 62 comprehensive tests (34 controller + 28 gRPC)

**Notes:** All call lifecycle operations available via both REST and gRPC. Error handling follows existing patterns (ArgumentException→BadRequest, InvalidOperationException→NotFound/Conflict, UnauthorizedAccessException→Forbid). Ready for phase-7.10 (Push Notifications).

### Step: phase-7.10 — Push Notifications for Calls

**Status:** completed ✅
**Depends on:** 7.3
**Deliverables:**
- ✓ `NotificationCategory.IncomingCall` — high-priority push for incoming calls (bypasses online presence suppression)
- ✓ `NotificationCategory.MissedCall` — normal-priority push for missed calls
- ✓ `NotificationCategory.CallEnded` — push for disconnected participants when call ends
- ✓ `CallNotificationEventHandler` — handles `VideoCallInitiatedEvent`, `VideoCallMissedEvent`, `VideoCallEndedEvent`
- ✓ `ICallNotificationHandler` interface in Chat project for cross-project DI resolution
- ✓ `NotificationRouter.CanSendPushAsync` — IncomingCall bypasses online presence suppression
- ✓ Event bus subscription/unsubscription in `ChatModule` lifecycle
- ✓ DI registration in `ChatServiceRegistration`
- ✓ 37 comprehensive tests (`CallNotificationEventHandlerTests.cs`)

**Notes:** Push notifications for video calls complete. Incoming calls always ring on all devices (bypass presence). DND still respected. Channel muting does not affect call notifications. Ready for phase-7.11 (Testing & Documentation).

### Step: phase-7.11 — Testing & Documentation

**Status:** completed ✅
**Depends on:** 7.1–7.10
**Deliverables:**
- ✓ Unit tests: 678 video-call-specific tests across 20 test files (target was 120+)
- ✓ Integration tests: full call lifecycle tests (initiate → join → leave → end) in `VideoCallServiceTests.cs`
- ✓ All 1027 Chat module tests pass
- ✓ Admin guide: `docs/admin/VIDEO_CALLING.md` — STUN/TURN configuration, coturn setup, LiveKit setup
- ✓ Updated `docs/modules/chat/README.md` — video calling features, enums, events, test count
- ✓ Updated `docs/modules/chat/API.md` — 9 REST endpoints + 7 gRPC RPCs documented
- ✓ User documentation: `docs/user/VIDEO_CALLS.md` — how to make calls, screen share, call history

**Notes:** Phase 7 (Video Calling & Screen Sharing) is now fully complete. All 11 steps delivered: contracts, data model, call service, SignalR signaling, JS WebRTC engine, Blazor UI, LiveKit SFU, STUN/TURN, REST/gRPC API, push notifications, testing & documentation.

---

## Phase 11: Auto-Updates

### Section: Phase 11 — Phase A: Core Update Infrastructure (Server-Side)

#### Step: phase-11.1 — IUpdateService Interface & DTOs
**Status:** completed ✅
**Deliverables:**
- ✓ `IUpdateService` interface with `CheckForUpdateAsync`, `GetLatestReleaseAsync`, `GetRecentReleasesAsync`
- ✓ `UpdateCheckResult` record (IsUpdateAvailable, CurrentVersion, LatestVersion, ReleaseUrl, ReleaseNotes, PublishedAt, Assets)
- ✓ `ReleaseInfo` record (Version, TagName, ReleaseNotes, PublishedAt, IsPreRelease, Assets)
- ✓ `ReleaseAsset` record (Name, DownloadUrl, Size, ContentType, Platform)

**Notes:** DTOs shared by both server and client via DotNetCloud.Core package.

#### Step: phase-11.2 — GitHubUpdateService Implementation
**Status:** completed ✅
**Deliverables:**
- ✓ `GitHubUpdateService` — queries GitHub Releases API with MemoryCache (1-hour TTL)
- ✓ Semantic version comparison with pre-release support
- ✓ Platform asset matching from release filenames
- ✓ DI registration in `SupervisorServiceExtensions`

**Notes:** Public GitHub API (60 req/hr); caching prevents rate limit issues.

#### Step: phase-11.3 — Update Check API Endpoint
**Status:** completed ✅
**Deliverables:**
- ✓ `UpdateController` with `GET /api/v1/core/updates/check`, `/releases`, `/releases/latest`
- ✓ Public endpoints (no auth required for client update checks)

**Notes:** Response wraps in standard `{ success: true, data: {...} }` format.

#### Step: phase-11.4 — CLI `dotnetcloud update` Implementation
**Status:** completed ✅
**Deliverables:**
- ✓ `dotnetcloud update --check` (display update status, exit code 0/1)
- ✓ `dotnetcloud update` (check + download tarball)

**Notes:** Server self-apply deferred for safety; download-only for now.

#### Step: phase-11.5 — Admin UI Updates Page
**Status:** completed ✅
**Deliverables:**
- ✓ `Updates.razor` at `/admin/updates` with version card, latest release, history, settings

**Notes:** Integrated into admin sidebar navigation.

#### Step: phase-11.6 — Unit Tests (Server-Side)
**Status:** completed ✅
**Deliverables:**
- ✓ `GitHubUpdateServiceTests` (mock HTTP, version comparison, caching, asset matching)
- ✓ `UpdateControllerTests` (response format, edge cases)

**Notes:** Phase A complete. All server-side update infrastructure in place.

### Section: Phase 11 — Phase B: Desktop Client Auto-Update (SyncTray)

#### Step: phase-11.7 — IClientUpdateService Interface
**Status:** completed ✅
**Deliverables:**
- ✓ `IClientUpdateService` interface (`CheckForUpdateAsync`, `DownloadUpdateAsync`, `ApplyUpdateAsync`, `UpdateAvailable` event)
- ✓ Reuses `UpdateCheckResult` and `ReleaseAsset` from `DotNetCloud.Core.DTOs`

**Notes:** Client.Core now references DotNetCloud.Core for shared DTOs.

#### Step: phase-11.8 — ClientUpdateService Implementation
**Status:** completed ✅
**Deliverables:**
- ✓ `ClientUpdateService` — server endpoint check with GitHub Releases API fallback
- ✓ Streaming download with `IProgress<double>` progress reporting
- ✓ Version comparison logic (semver + pre-release split)
- ✓ DI registration via `ClientCoreServiceExtensions.AddHttpClient<IClientUpdateService, ClientUpdateService>()`

**Notes:** Falls back to direct GitHub API if server unreachable or no base address configured.

#### Step: phase-11.9 — Background Update Checker (SyncTray)
**Status:** completed ✅
**Deliverables:**
- ✓ `UpdateCheckBackgroundService` — periodic timer (30s initial delay, 24h interval, configurable)
- ✓ `UpdateAvailable` event wired to `TrayViewModel.OnUpdateAvailable`
- ✓ Tray context menu "Check for Updates…" item (updates text when update available)
- ✓ System notification on update found

**Notes:** Integrated into App.axaml.cs lifecycle (start after tray init, dispose on shutdown).

#### Step: phase-11.10 — SyncTray Update UI
**Status:** completed ✅
**Deliverables:**
- ✓ `UpdateDialog.axaml` — dark themed 480×420 Avalonia window with version cards, status badges (green/amber), release notes, download progress bar, action buttons
- ✓ `UpdateViewModel` — check/download commands, platform asset matching, `ShouldClose` property
- ✓ Settings "Updates" tab — current version display, update available/up-to-date banners, auto-check toggle
- ✓ `SettingsViewModel` — `CurrentClientVersion`, `AutoCheckForUpdates` (persisted to local settings)

**Notes:** Follows existing dark theme and AddAccountDialog patterns.

#### Step: phase-11.11 — Desktop Client Update Tests
**Status:** completed ✅
**Deliverables:**
- ✓ `ClientUpdateServiceTests` — 10 tests (server check, no-update, GitHub fallback, no base address skip, event firing, download with progress, null/missing/empty path errors)
- ✓ `UpdateCheckBackgroundServiceTests` — 8 tests (update event, no-update silence, error resilience, result storage, start/stop lifecycle, double dispose, enabled/interval defaults)
- ✓ All 18 Phase B tests passing

**Notes:** Phase B complete. Desktop client auto-update fully implemented with background checking, tray integration, update dialog, settings tab. Ready for Phase C (Android) or Phase D (documentation).

### Section: Phase 11 — Phase C: Android Client Update Notification

#### Step: phase-11.12 — Android Update Check Service
**Status:** pending
**Deliverables:**
- ☐ Android-specific update service checking server endpoint
- ☐ Play Store / APK link handling

#### Step: phase-11.13 — Android Update UI
**Status:** pending
**Deliverables:**
- ☐ Update notification in Android app
- ☐ Settings page update preferences

#### Step: phase-11.14 — Android Update Tests
**Status:** pending
**Deliverables:**
- ☐ Android update service unit tests

### Section: Phase 11 — Phase D: Documentation & Integration

#### Step: phase-11.15 — Auto-Update Documentation
**Status:** completed ✅
**Deliverables:**
- ✓ `docs/modules/AUTO_UPDATES.md` — feature documentation (architecture, API reference, configuration)
- ✓ `docs/user/AUTO_UPDATES.md` — user-facing update configuration guide
- ✓ Architecture doc updated — Phase 8 split into Phase 8 (Search) + Phase 11 (Auto-Updates)
- ✓ README.md roadmap table updated with Phase 11 row

**Notes:** All documentation covering server, CLI, desktop, and Android update flows. User guide covers configuration for all surfaces.

#### Step: phase-11.16 — Integration Testing
**Status:** completed ✅
**Deliverables:**
- ✓ `UpdateEndpointTests.cs` — 6 integration tests covering check, releases, latest, version param, count clamping, graceful degradation
- ✓ Uses `DotNetCloudWebApplicationFactory` in-memory test infrastructure
- ✓ Verifies standard API envelope format (`{ success: true, data: {...} }`)

**Notes:** Phase D complete. All documentation and integration tests in place. Remaining Phase 11 work: Phase C (Android).

---

## Direct Messaging, Direct Calls & Host-Based Call Management

Reference plan: `docs/DIRECT_MESSAGING_AND_HOST_CALLS_PLAN.md`

### Section: Phase A — Database & Model Changes

#### Step: dm-host-A1 — Rename CallParticipantRole.Initiator → Host
**Status:** completed ✅
**Deliverables:**
- ✓ `CallParticipantRole.cs` — enum value renamed from `Initiator` to `Host`
- ✓ `VideoCallService.cs` — all references updated
- ✓ `ChatDtos.cs` — `CallParticipantDto.Role` comment updated
- ✓ All test files updated (VideoCallServiceTests, CallSignalingServiceTests, VideoCallDataModelTests, VideoCallGrpcServiceTests)
- ✓ EF migration data update: stored `"Initiator"` → `"Host"` in CallParticipants table

**Notes:** Clean rename across 7 files. All 375+ chat tests pass.

#### Step: dm-host-A2 — Add HostUserId to VideoCall Entity
**Status:** completed ✅
**Deliverables:**
- ✓ `VideoCall.cs` — `Guid HostUserId` property added
- ✓ `VideoCallConfiguration.cs` — index `ix_chat_video_calls_host_user_id` added
- ✓ `VideoCallDto` — `HostUserId` field added to DTO
- ✓ `VideoCallService.cs` — `ToVideoCallDto` mapper includes `HostUserId`; `InitiateCallAsync` sets `HostUserId = caller.UserId`
- ✓ EF migration `AddCallHostUserId` — adds column, backfills from `InitiatorUserId`

**Notes:** HostUserId enables transferable call authority separate from the historical initiator.

#### Step: dm-host-A3 — DM → Group Auto-Conversion
**Status:** completed ✅
**Deliverables:**
- ✓ `ChannelMemberService.AddMemberAsync` — detects 3rd member added to DirectMessage channel
- ✓ Auto-converts `Channel.Type` from `DirectMessage` to `Group`
- ✓ No schema change needed (existing `Type` column supports `Group`)

**Notes:** Phase A complete. Foundation for Host role system and mid-call participant management. Next: Phase B (Direct DM & Call Initiation).

### Section: Phase B — Service Layer: Direct DM & Call Initiation

#### Step: dm-host-B1 — Wire Global User Search for DM Creation
**Status:** pending
**Deliverables:**
- ☐ `ChatPageLayout.razor.cs` — `SearchUsersForDmAsync` method
- ☐ `ChatPageLayout.razor` — integrate user picker dialog

#### Step: dm-host-B2 — Direct Call Initiation by User ID
**Status:** pending
**Deliverables:**
- ☐ `IVideoCallService.InitiateDirectCallAsync` interface method
- ☐ `VideoCallService` implementation
- ☐ `ChatController` — `POST /api/v1/chat/calls/direct/{targetUserId}` endpoint

### Section: Phase C — Mid-Call Participant Addition

#### Step: dm-host-C1 — InviteToCallAsync Service Method
**Status:** pending

#### Step: dm-host-C2 — SignalR Notification for Mid-Call Invite
**Status:** pending

### Section: Phase D — Host Transfer

#### Step: dm-host-D1 — TransferHostAsync Service Method
**Status:** pending

#### Step: dm-host-D2 — Auto-Transfer Host on Leave
**Status:** pending

#### Step: dm-host-D3 — End-Call Permission Enforcement
**Status:** pending

#### Step: dm-host-D4 — CallHostTransferredEvent
**Status:** pending

### Section: Phase E — UI Integration

#### Step: dm-host-E — UI Integration (6 sub-items)
**Status:** completed ✅
**Deliverables:**
- ✓ `ChannelList.razor(.cs/.css)` — Direct Messages header now has a dedicated "+" action wired to `OnNewDm` and the DM user picker flow
- ✓ `ChatPageLayout.razor(.cs/.css)` — wired New DM, channel add-people picker, call add-people picker, direct member call actions, and host/invite state tracking
- ✓ `MemberListPanel.razor(.cs/.css)` — per-member audio/video call actions plus channel-level add-people action in panel header
- ✓ `ChannelHeader.razor(.cs)` — added "Add People" action for DM/Group channels
- ✓ `CallControls.razor(.cs)` — host-only "Add People" call control
- ✓ `VideoCallDialog.razor(.cs/.css)` — participant panel with Host badge, transfer-host actions, and add-people invite picker overlay
- ✓ `IncomingCallNotification` integration in `ChatPageLayout.razor` now passes mid-call invite fields (`IsMidCallInvite`, `ParticipantCount`)

**Notes:** Phase E UI behavior is now fully wired for DM creation, direct calls, call host controls, and DM/group member expansion workflows.

### Section: Phase F — SignalR Hub Updates

#### Step: dm-host-F — SignalR Hub Updates
**Status:** pending

### Section: Phase G — Tests

#### Step: dm-host-G — Unit & Integration Tests
**Status:** in-progress
**Deliverables:**
- ✓ Added/expanded unit coverage for new Phase E behavior:
- ✓ `ChannelListTests.cs` — New DM callback path
- ✓ `ChannelHeaderCallButtonTests.cs` — Add People callback path
- ✓ `MemberListPanelTests.cs` — add-people/member action callbacks and profile behavior
- ✓ `CallControlsTests.cs` — host add-people control callback
- ✓ `VideoCallDialogTests.cs` — host-state detection and add-people/transfer-host/invite callbacks
- ✓ `IncomingCallNotificationTests.cs` — mid-call invite parameter coverage
- ✓ Full Chat test suite executed: `dotnet test tests/DotNetCloud.Modules.Chat.Tests/DotNetCloud.Modules.Chat.Tests.csproj --no-restore` (1176 passed, 0 failed)
- ☐ Integration / E2E scenarios for invite + host transfer lifecycle remain pending

**Notes:** Unit-level validation is comprehensive for the Phase E UI additions. Next step is dedicated end-to-end flow verification for multi-user call lifecycles.

---

## Shared File Folder Workstream

Reference plan: `docs/SHARED_FILE_FOLDER_IMPLEMENTATION_PLAN.md`

### Section: Shared File Folders — Group Foundation

#### Step: shared-file-folders-1 — Group Capability Foundation
**Status:** completed ✅
**Deliverables:**
- ✓ `IGroupDirectory` capability contract added to `DotNetCloud.Core`
- ✓ `IGroupManager` capability contract added to `DotNetCloud.Core`
- ✓ `GroupDirectoryService` implemented in `DotNetCloud.Core.Auth`
- ✓ `GroupManagerService` implemented in `DotNetCloud.Core.Auth`
- ✓ DI registration added in `AuthServiceExtensions`
- ✓ Focused Core.Auth capability tests added for group queries, CRUD, and membership flows
- ✓ Validation run completed: filtered `DotNetCloud.Core.Auth.Tests` execution passed (15 tests)

**Notes:** This completes the first implementation slice for the shared file folder plan by wiring the missing group capability layer on top of the existing Core.Data group entities. Remaining work now moves to protected `All Users` semantics, admin APIs/UI, and Files-side integration.

#### Step: shared-file-folders-2 — Built-In All Users Group Semantics
**Status:** completed ✅
**Deliverables:**
- ✓ Extend the group model with protected built-in semantics
- ✓ Add creation/backfill logic for one `All Users` group per organization
- ✓ Route built-in membership through active organization membership resolution
- ✓ Prevent rename, delete, and manual membership mutation for the built-in group
- ✓ Add EF migration and focused auth/data test coverage for the built-in group flow

**Notes:** The shared-folder workstream now has a protected built-in `All Users` group with initializer backfill, migration support, and implicit membership based on active organization membership rather than explicit `GroupMembers` rows. Focused validation passed again after the schema update via `dotnet test DotNetCloud.CI.slnf --filter "FullyQualifiedName~GroupDirectoryServiceTests|FullyQualifiedName~GroupManagerServiceTests|FullyQualifiedName~DbInitializerTests" --no-restore` (59 tests).

#### Step: shared-file-folders-3 — Admin Group Management Surfaces
**Status:** completed ✅
**Deliverables:**
- ✓ Add group DTOs aligned with existing team patterns
- ✓ Add admin REST endpoints for group CRUD and membership management
- ✓ Add dedicated admin UI for group management

**Notes:** Admin-facing group management is now available end-to-end. The core server exposes admin CRUD and membership endpoints through `GroupsController`, the web client has a dedicated `/admin/groups` page plus navigation entry, and the UI keeps the built-in `All Users` group read-only so it matches the implicit-membership server rules. Focused validation passed again via `dotnet test DotNetCloud.CI.slnf --filter "FullyQualifiedName~GroupsControllerTests" --no-restore` (8 tests), and the stricter 4.7 host-level verification bar now also includes `dotnet test tests/DotNetCloud.Integration.Tests/DotNetCloud.Integration.Tests.csproj --no-restore --filter "FullyQualifiedName~GroupsEndpointIntegrationTests"` for real admin group CRUD and membership flow coverage over the core host.

#### Step: shared-file-folders-4 — Files Share Model Hardening
**Status:** completed ✅
**Deliverables:**
- ✓ Honor direct user, team, group, and inherited parent-folder shares in Files permissions
- ✓ Add membership resolution abstraction inside Files
- ✓ Keep `Shared With Me` scoped to explicit user shares only
- ✓ Add separate listing path for mounted team/group-accessible content

**Notes:** Files share-model hardening is now complete. Files permission evaluation resolves direct user shares plus team/group shares across inherited parent-folder paths, Core membership lookups stay behind `IShareAccessMembershipResolver`, `GetSharedWithMeAsync` is explicitly `ShareType.User` only, and Files now exposes a separate `ListMountedAccessAsync` path plus `GET /api/v1/files/mounted-access` for non-owned team/group-accessible nodes that will feed the future `_DotNetCloud` experience. `FileShareDto` now carries `SharedWithGroupId` so group share API responses match the supported share model. Focused validation passed via `dotnet test DotNetCloud.CI.slnf --filter "FullyQualifiedName~FileServiceTests|FullyQualifiedName~ShareServiceTests|FullyQualifiedName~ControllerSecurityAuditTests|FullyQualifiedName~FileDtoTests" --no-restore` (121 tests).

#### Step: shared-file-folders-5 — Admin Shared Folder Virtualization
**Status:** completed ✅
**Deliverables:**
- ✓ Add admin shared-folder definitions and path validation model
- ✓ Add admin CRUD API, group-assignment endpoints, and reindex/rescan controls
- ✓ Add admin UI for shared-folder CRUD, group assignment, scan actions, platform-root source browsing, and seeded scheduled scans
- ✓ Back manual Rescan Now and Reindex actions with a Files maintenance worker plus Search reindex dispatch
- ✓ Add `_DotNetCloud` virtual root composition with mounted folder browsing
- ✓ Enforce read-only behavior for mounted paths

**Notes:** The 4.4 virtualization slice remains complete end-to-end. Files root listings inject a synthetic `_DotNetCloud` folder for every user, `_DotNetCloud` contains a synthetic `Shared With Me` folder plus the caller's accessible admin shared folders, admin shared folders enumerate the real on-disk hierarchy as nested virtual nodes, mounted files can be opened through the existing download path, and mounted-path mutations are blocked across FileService writes plus share, tag, comment, and upload entry points. The admin shared-folder page now adds a server-constrained folder picker backed by a dedicated browse endpoint, suggests a display name from the selected path's final segment when the name is still unset or auto-suggested, and seeds Scheduled crawl mode with a next scan 24 hours ahead while clearing that field for Manual mode. The browse flow now defaults to the platform filesystem root for interactive picking while still resolving relative source paths against an optional configured base when present, which removed the live 409 on mint22 where no Files admin root was configured. Manual Rescan Now and Reindex actions are now executed by a Files maintenance worker that probes due shared folders, updates scan state, and dispatches Files-module reindex requests through the Search pipeline, so the admin controls no longer stall in a requested-only state. Focused validation passed via `dotnet test DotNetCloud.CI.slnf --filter "FullyQualifiedName~AdminSharedFolder" --no-restore` (22 tests) and `dotnet test tests/DotNetCloud.Modules.Files.Tests/DotNetCloud.Modules.Files.Tests.csproj --filter "FullyQualifiedName~AdminSharedFolderServiceTests|FullyQualifiedName~AdminSharedFolderMaintenanceServiceTests" --no-restore` (10 tests), then live verification passed on mint22: `ben.kimball@llabmik.net` created a shared folder from the admin UI and `testdude@llabmik.net` could see and access the mounted share in a separate browser session. Remaining work still moves to 4.5 search indexing/navigation and 4.6 media scan-source selection.

#### Step: shared-file-folders-6 — Search And Media Integration
**Status:** completed
**Deliverables:**
- ✓ Add group-aware search indexing and navigation for mounted shared folders
- ✓ Add per-user shared-folder scan-source selection for Music, Photos, and Video
- ✓ Keep sync clients ignoring `_DotNetCloud` admin shares in v1

**Notes:** The search and media integration step is now complete. Mounted search indexing/navigation remains in place, 4.6 media integration persists per-user per-module source lists for owned folders plus `_DotNetCloud` admin shared mounts, and the client selective-sync layer now canonicalizes root-relative paths, hard-excludes `_DotNetCloud` from sync decisions, and locks that virtual root as unchecked in the SyncTray folder browser so users cannot opt it back in. Focused validation passed via `dotnet test tests/DotNetCloud.Client.Core.Tests/DotNetCloud.Client.Core.Tests.csproj --no-restore --filter "FullyQualifiedName~SelectiveSyncConfigTests"`, `dotnet test tests/DotNetCloud.Client.SyncTray.Tests/DotNetCloud.Client.SyncTray.Tests.csproj --no-restore --filter "FullyQualifiedName~FolderBrowserViewModelTests|FullyQualifiedName~FolderBrowserItemViewModelTests"`, `dotnet test tests/DotNetCloud.Core.Server.Tests/DotNetCloud.Core.Server.Tests.csproj --no-restore --filter "FullyQualifiedName~MediaFolderImportServiceTests"`, `dotnet test tests/DotNetCloud.Core.Server.Tests/DotNetCloud.Core.Server.Tests.csproj --no-restore --filter "FullyQualifiedName~MediaLibraryControllerTests"`, `dotnet test tests/DotNetCloud.Integration.Tests/DotNetCloud.Integration.Tests.csproj --no-restore --filter "FullyQualifiedName~MediaLibraryEndpointIntegrationTests"`, and `dotnet test tests/DotNetCloud.Integration.Tests/DotNetCloud.Integration.Tests.csproj --no-restore --filter "FullyQualifiedName~AdminSharedFoldersEndpointTests"`. 4.7 verification coverage now includes explicit Core.Server tests for shared-mount media scan enumeration, stale-index cleanup, and media-library shared-source API responses, plus core-host integration coverage for media-library paths/scan routing and Files-host integration coverage for shared-folder admin endpoints, `_DotNetCloud` listing, nested virtual browsing, and mounted write rejection.
