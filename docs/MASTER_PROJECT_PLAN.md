# DotNetCloud Master Project Plan

> **Version:** 1.0

> **Created:** 2026-03-02

> **Purpose:** Comprehensive, persistent plan for all DotNetCloud implementation phases

> **Status Tracking:** Each step includes status (pending|in-progress|completed|failed|skipped)

> **Reference in Conversations:** Use step IDs like "phase-0.1" to reference specific work

---

## Quick Status Summary

| Phase | Steps | Completed | In Progress | Pending |
|-------|-------|-----------|-------------|---------|
| Pre-Implementation | 2 | 2 | 0 | 0 |
| Phase 0.1 | 11 | 11 | 0 | 0 |
| Phase 0.2 | 12 | 12 | 0 | 0 |
| Phase 0.3 | 8 | 8 | 0 | 0 |
| Phase 0.4 | 20 | 20 | 0 | 0 |
| Phase 0.5 | 9 | 9 | 0 | 0 |
| Phase 0.6 | 14 | 14 | 0 | 0 |
| Phase 0.7 | 16 | 16 | 0 | 0 |
| Phase 0.8 | 11 | 11 | 0 | 0 |
| Phase 0.9 | 13 | 13 | 0 | 0 |
| Phase 0.10 | 11 | 11 | 0 | 0 |
| Phase 0.11 | 18 | 18 | 0 | 0 |
| Phase 0.12 | 25 | 25 | 0 | 0 |
| Phase 0.13 | 20 | 20 | 0 | 0 |
| Phase 0.14 | 18 | 18 | 0 | 0 |
| Phase 0.15 | 12 | 12 | 0 | 0 |
| Phase 0.16 | 12 | 12 | 0 | 0 |
| Phase 0.17 | 10 | 10 | 0 | 0 |
| Phase 0.18 | 8 | 8 | 0 | 0 |
| Phase 0.19 | 11 | 11 | 0 | 0 |
| Phase 1.1 | 6 | 6 | 0 | 0 |
| Phase 1.2 | 5 | 5 | 0 | 0 |
| Phase 1.3 | 15 | 15 | 0 | 0 |
| Phase 1.4 | 15 | 15 | 0 | 0 |
| Phase 1.5 | 10 | 10 | 0 | 0 |
| Phase 1.6 | 9 | 9 | 0 | 0 |
| Phase 1.7 | 11 | 11 | 0 | 0 |
| Phase 1.8 | 8 | 8 | 0 | 0 |
| Phase 1.9 | 14 | 14 | 0 | 0 |
| Phase 1.10 | 24 | 24 | 0 | 0 |
| Phase 1.11 | 8 | 8 | 0 | 0 |
| Phase 1.12 | 17 | 17 | 0 | 0 |
| Phase 1.13 | 4 | 4 | 0 | 0 |
| Phase 1.14 | 32 | 32 | 0 | 0 |
| Phase 1.15 | 25 | 25 | 0 | 0 |
| Phase 1.16 | 20 | 20 | 0 | 0 |
| Phase 1.17 | 25 | 25 | 0 | 0 |
| Phase 1.18 | 6 | 6 | 0 | 0 |
| Phase 1.19 | 20 | 20 | 0 | 0 |
| Phase 1.20 | 20 | 20 | 0 | 0 |
| Phase 2.1 | 6 | 6 | 0 | 0 |
| Phase 2.2 | 4 | 4 | 0 | 0 |
| Phase 2.3 | 7 | 7 | 0 | 0 |
| Phase 2.4 | 5 | 5 | 0 | 0 |
| Phase 2.5 | 4 | 4 | 0 | 0 |
| Phase 2.6 | 4 | 4 | 0 | 0 |
| Phase 2.7 | 4 | 4 | 0 | 0 |
| Phase 2.8 | 11 | 11 | 0 | 0 |
| Phase 2.9 | 3 | 3 | 0 | 0 |
| Phase 2.10 | 10 | 10 | 0 | 0 |
| Phase 2.11 | 3 | 3 | 0 | 0 |
| Phase 2.12 | 2 | 2 | 0 | 0 |
| Phase 2.13 | 3 | 3 | 0 | 0 |
| Integration Testing Sprint | 3 | 3 | 0 | 0 |
| Sync Batch 1 | 10 | 10 | 0 | 0 |
| Sync Batch 2 | 6 | 6 | 0 | 0 |
| Sync Batch 3 | 6 | 6 | 0 | 0 |
| Sync Batch 4 | 5 | 5 | 0 | 0 |
| Sync Batch 5 | 2 | 2 | 0 | 0 |
| Sync Verification | 1 | 1 | 0 | 0 |
| Sync Hardening P0 | 3 | 3 | 0 | 0 |
| Sync Hardening P1–P2 | 6 | 6 | 0 | 0 |
| Client Security Remediation | 1 | 1 | 0 | 0 |
| Phase 3.1 | 4 | 4 | 0 | 0 |
| Phase 3.2 | 6 | 6 | 0 | 0 |
| Phase 3.3 | 6 | 6 | 0 | 0 |
| Phase 3.4 | 6 | 6 | 0 | 0 |
| Phase 3.5 | 4 | 4 | 0 | 0 |
| Phase 3.6 | 4 | 4 | 0 | 0 |
| Phase 3.7 | 5 | 5 | 0 | 0 |
| Phase 3.8 | 4 | 4 | 0 | 0 |
| Phase 4.1 | 11 | 11 | 0 | 0 |
| Phase 4.2 | 7 | 7 | 0 | 0 |
| Phase 4.3 | 21 | 21 | 0 | 0 |
| Phase 4.4 | 17 | 17 | 0 | 0 |
| Phase 4.5 | 9 | 9 | 0 | 0 |
| Phase 4.6 | 4 | 4 | 0 | 0 |
| Phase 4.7 | 6 | 6 | 0 | 0 |
| Phase 4.8 | 8 | 8 | 0 | 0 |
| Phase 4.9 | 42 | 24 | 0 | 18 |
| Phase 5-9 | Summary | 0 | 0 | 1 |
| Infrastructure | Summary | 0 | 0 | 1 |
| Documentation | Summary | 0 | 0 | 1 |

Maintenance note: local install/setup health verification now follows configured Kestrel ports and accepts self-signed local HTTPS during startup checks. Fresh Linux installs now invoke `dotnetcloud setup --beginner` by default, which auto-selects the recommended local PostgreSQL path and then branches cleanly between the three real deployment shapes: private/local test, public behind a reverse proxy, and public served directly by DotNetCloud itself. The local branch uses self-signed HTTPS on DotNetCloud directly. The reverse-proxy public branch keeps DotNetCloud on local HTTP and ends with explicit reverse-proxy/TLS guidance instead of pretending automatic public-certificate setup exists; it now also points beginners to a dedicated Apache-first reverse-proxy guide with a Caddy alternative. The public-direct branch lets the user point DotNetCloud at an existing public certificate file and explains the extra tradeoffs, while still explicitly recommending a reverse proxy for most public installs because it simplifies ports 80/443, TLS renewal, and future services on the same machine. All branches print explicit direct local access URLs and health probe URLs and end with a plain-language summary of the selected defaults plus the beginner user's next steps. Upgrade runs now also end with a plain-language summary that confirms existing data/configuration were preserved, states clearly whether a one-time setup review is still required, and re-shows the access URLs plus the user's next step. This also clarifies the internal app defaults HTTP `5080` / HTTPS `5443` versus reverse-proxy/public HTTPS ports such as `15443`. Windows now has a separate IIS-first installation path via `tools/install-windows.ps1`, with IIS reverse proxying to `http://localhost:5080`, a beginner-focused IIS guide, a dedicated architecture rationale note, native Windows Service hosting support in the core server, and machine-level config/data environment propagation during setup and service runtime so Windows self-hosters do not need to follow the Linux installer path.

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
- Git Flow branching strategy (main, develop, feature/*, bugfix/*, release/*)
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

**Notes:** All Files endpoints functional, upload/download/sync verified across 3 machines (mint22, Windows11-TestDNC, mint-dnc-client). Collabora/WOPI integration operational. Desktop sync clients working on Windows (service + SyncTray) and Linux. Share notifications (public link access, expiry warnings) and sync debounce all implemented. 644 Files module tests + 182 Client.Core tests + 27 SyncService tests + 77 SyncTray tests = 930 tests covering Files/Sync.

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
- ✓ Distribution signing: Release PropertyGroup with AndroidKeyStore/KEYSTORE_* env vars, AndroidUseAapt2=true for F-Droid reproducibility
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

**Notes:** All Phase 3.1 contracts added to DotNetCloud.Core. DTOs: ContactDtos.cs, CalendarDtos.cs, NoteDtos.cs. Events: ContactEvents.cs, CalendarEvents.cs, NoteEvents.cs. Capabilities: IContactDirectory, ICalendarDirectory, INoteDirectory (all Public tier). Error codes added for CONTACT_, CALENDAR_, NOTE_ domains. 197/197 Core tests pass. Ready for phase-3.2 (Contacts Module).

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
- ✓ Shared notification patterns (ResourceShared, UserMentioned, ReminderTriggered events + handlers + push integration)
- ✓ Cross-module link resolution (ICrossModuleLinkResolver with Contact/CalendarEvent/Note/File support, batch resolve)
- ✓ Consistent authorization, audit, and soft-delete behavior (IAuditLogger capability, CallerContext verification, all manifests updated)
- ✓ 30 new tests (CrossModuleLinkResolver 13, NotificationHandlers 4, ManifestConsistency 13)
- ✓ Core DTOs: NotificationDtos, CrossModuleLinkDtos
- ✓ Module Razor SDK upgrades (Contacts, Calendar, Notes)
- ✓ Module manifest updates with cross-module capabilities and event subscriptions

**Notes:** Cross-module integration complete. All PIM modules now declare IAuditLogger + ICrossModuleLinkResolver capabilities, publish ResourceSharedEvent, and subscribe to each other's domain events. Notification handlers wire into existing IPushNotificationService. ExampleModule NoteCreatedEvent naming conflict resolved with using aliases. Deferred items were completed in follow-up implementation: audit columns were added across PIM entities, notification persistence + bell UI were wired, contact reverse related-entity queries were exposed via API, and link chips now render in Contacts/Calendar/Notes views. All D1-D7 deferred items are now complete: API client methods added for sharing/RSVP/import-export/folder-CRUD/version-history/search across all three modules; ContactsPage has avatar display and sharing dialog; CalendarPage has RSVP buttons, sharing dialog, and iCal import/export; NotesPage has folder CRUD (create/rename/delete), version history panel with restore, and sharing dialog.

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
**STATUS:** in-progress 🔄
**DELIVERABLES:**
- ✓ Phase A: Data Model & Mode System — `BoardMode` enum, `ReviewSession`/`ReviewSessionParticipant` entities, sprint planning fields, EF configs
- ✓ Phase B: Service Layer — Mode-aware `BoardService`, `SprintPlanningService`, `ReviewSessionService`, backlog/sprint filter, poker vote status
- ✓ Phase C: API Layer — Board mode parameter, sprint wizard endpoints, backlog endpoints, `ReviewSessionController`, poker vote status endpoint, gRPC updates
- ✓ Phase D: Real-Time / SignalR — Review session broadcasts, client-side SignalR events
- ✓ Phase E: UI — Personal Mode Simplification — Mode selector in create dialog, mode badge in board list, conditional sidebar for Personal/Team, sprint controls hidden for Personal boards, 35 Phase E tests
- ✓ Phase F: UI — Sprint Planning Wizard — 4-step wizard (Plan Basics → Swimlanes → Schedule → Review), TracksPage integration with Year Plan nav, 61 Phase F tests
- ☐ Phase G: UI — Backlog & Sprint Views
- ☐ Phase H: UI — Year Timeline / Gantt View
- ☐ Phase I: UI — Live Review Mode

**Notes:** Phases A–F complete. Phase F added SprintPlanningWizard component: 4-step wizard for creating year sprint plans on Team boards (plan basics, swimlane definition, sprint schedule with cascading duration adjustments, review & create). Integrated into TracksPage with Year Plan sidebar nav item. 61 new Phase F tests, 594 total Tracks tests passing. Next: Phase G (Backlog & Sprint Views).

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
