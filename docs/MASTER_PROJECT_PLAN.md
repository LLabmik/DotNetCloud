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
| Phase 0.11 | 18 | 17 | 0 | 1 |
| Phase 0.12 | 25 | 25 | 0 | 0 |
| Phase 0.13 | 20 | 20 | 0 | 0 |
| Phase 0.14 | 18 | 18 | 0 | 0 |
| Phase 0.15 | 12 | 12 | 0 | 0 |
| Phase 0.16 | 12 | 11 | 0 | 1 |
| Phase 0.17 | 10 | 10 | 0 | 0 |
| Phase 0.18 | 8 | 8 | 0 | 0 |
| Phase 0.19 | 9 | 9 | 0 | 0 |
| Phase 1.1 | 6 | 6 | 0 | 0 |
| Phase 1.2 | 5 | 5 | 0 | 0 |
| Phase 1.3 | 15 | 15 | 0 | 0 |
| Phase 1.4 | 15 | 15 | 0 | 0 |
| Phase 1.5 | 10 | 10 | 0 | 0 |
| Phase 1.6 | 9 | 8 | 0 | 1 |
| Phase 1.7 | 11 | 11 | 0 | 0 |
| Phase 1.8 | 8 | 8 | 0 | 0 |
| Phase 1.9 | 14 | 14 | 0 | 0 |
| Phase 1.10 | 24 | 24 | 0 | 0 |
| Phase 1.11 | 8 | 8 | 0 | 0 |
| Phase 1.12 | 17 | 17 | 0 | 0 |
| Phase 1.13 | 4 | 4 | 0 | 0 |
| Phase 1.14 | 32 | 32 | 0 | 0 |
| Phase 1.15 | 25 | 23 | 0 | 2 |
| Phase 1.16 | 20 | 20 | 0 | 0 |
| Phase 1.17 | 25 | 25 | 0 | 0 |
| Phase 1.18 | 6 | 6 | 0 | 0 |
| Phase 1.19 | 20 | 20 | 0 | 0 |
| Phase 1.20 | 20 | 20 | 0 | 0 |
| Phase 2.1 | 6 | 6 | 0 | 0 |
| Phase 2.2 | 4 | 4 | 0 | 0 |
| Phase 2.3 | 7 | 3 | 0 | 4 |
| Phase 2.4 | 5 | 5 | 0 | 0 |
| Phase 2.5 | 4 | 4 | 0 | 0 |
| Phase 2.6 | 4 | 4 | 0 | 0 |
| Phase 2.7 | 4 | 4 | 0 | 0 |
| Phase 2.8 | 11 | 11 | 0 | 0 |
| Phase 2.9 | 3 | 0 | 1 | 2 |
| Phase 2.10 | 8 | 0 | 0 | 8 |
| Phase 2.11 | 3 | 3 | 0 | 0 |
| Phase 2.12 | 2 | 1 | 1 | 0 |
| Phase 2.13 | 3 | 0 | 0 | 3 |
| Sync Batch 1 | 10 | 10 | 0 | 0 |
| Sync Batch 2 | 6 | 6 | 0 | 0 |
| Sync Batch 3 | 6 | 6 | 0 | 0 |
| Sync Batch 4 | 5 | 5 | 0 | 0 |
| Sync Batch 5 | 2 | 2 | 0 | 0 |
| Sync Verification | 1 | 1 | 0 | 0 |
| Phase 3-9 | Summary | 0 | 0 | 1 |
| Infrastructure | Summary | 0 | 0 | 1 |
| Documentation | Summary | 0 | 0 | 1 |

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
**Notes:** CoreDbContext fully configured and tested. Successfully generated InitialCreate migration. TimestampInterceptor automatically manages CreatedAt/UpdatedAt for all entities. Ready for phase-0.2.8 (DbInitializer).

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

**Test Statistics:**
- ✅ **Total Test Methods:** 80+ tests
- ✅ **All Tests Passing:** 100% success rate
- ✅ **Build Status:** Successful with no warnings or errors
- ✅ **Code Coverage:** 80%+ coverage across all data entities and relationships

**Test Project:** `tests/DotNetCloud.Core.Data.Tests/`

**File Locations:**
- `tests/DotNetCloud.Core.Data.Tests/Entities/Organizations/SoftDeleteTests.cs` (7 tests)
- `tests/DotNetCloud.Core.Data.Tests/Entities/Organizations/RelationshipTests.cs` (12 tests)
- `tests/DotNetCloud.Core.Data.Tests/Entities/Permissions/RolePermissionTests.cs` (13 tests)
- `tests/DotNetCloud.Core.Data.Tests/Entities/Settings/SettingsHierarchyTests.cs` (11 tests)
- `tests/DotNetCloud.Core.Data.Tests/Entities/Modules/DeviceModuleRegistryTests.cs` (13 tests)
- `tests/DotNetCloud.Core.Data.Tests/Integration/MultiDatabaseTests.cs` (11 tests)
- `tests/DotNetCloud.Core.Data.Tests/Integration/DbContextConfigurationTests.cs` (13 tests)

**Dependencies:** phase-0.2.9, phase-0.2.10, phase-0.2.11 ✅  
**Testing:** ✅ 80+ tests all passing  
**Build Status:** ✅ Solution builds successfully with no warnings
**Coverage:** ✅ 80%+ code coverage for all entities and relationships
**Notes:** Phase 0.2 (Database & Data Access Layer) is now complete. All 12 steps finished with comprehensive test coverage validating entity relationships, soft-deletes, multi-database support, and DbContext configuration. Ready for Phase 0.3 (Service Defaults & Cross-Cutting Concerns).

---

### Section: Phase 0.3 - Service Defaults & Cross-Cutting Concerns

#### Step: phase-0.3.1 - Serilog Logging Configuration
**Status:** completed ✅
**Duration:** ~1.5 hours  
**Description:** Set up Serilog with console and file sinks

**Deliverables:**
- ✓ Console sink configuration (development) with structured output template
- ✓ File sink configuration (production with daily rolling, 31-day retention, 100MB file limit)
- ✓ Structured logging format with JSON properties
- ✓ Log level configuration per module via `ModuleLogLevels` dictionary
- ✓ Log context enrichment classes:
  - ✓ `LogEnricher.WithUserId()`
  - ✓ `LogEnricher.WithRequestId()`
  - ✓ `LogEnricher.WithModuleName()`
  - ✓ `LogEnricher.WithOperationName()`
  - ✓ `LogEnricher.WithCallerContext()`
- ✓ `ModuleLogFilter` for per-module log filtering
- ✓ `SerilogConfiguration` with `UseDotNetCloudSerilog()` extension method
- ✓ `SerilogOptions` class for configuration
- ✓ Machine name, environment, process ID, thread ID enrichment

**File Location:** `src/Core/DotNetCloud.Core.ServiceDefaults/Logging/`  
**Dependencies:** None  
**Testing:** ✅ Builds successfully with no warnings  
**Notes:** Complete Serilog infrastructure with structured logging, enrichment, and module-specific filtering. Configuration via appsettings.json supported.

---

#### Step: phase-0.3.2 - Health Checks Infrastructure
**Status:** completed ✅
**Duration:** ~1.5 hours  
**Description:** Create health check framework for system components

**Deliverables:**
- ✓ `IModuleHealthCheck` interface for module-specific health checks
- ✓ `ModuleHealthCheckResult` class (Healthy, Degraded, Unhealthy statuses)
- ✓ `ModuleHealthStatus` enum
- ✓ `ModuleHealthCheckAdapter` wrapping module checks as ASP.NET Core health checks
- ✓ `DatabaseHealthCheck` implementation with `IDbConnectionFactory` interface
- ✓ Health check endpoints configuration:
  - ✓ `/health` - overall health
  - ✓ `/health/ready` - readiness probe
  - ✓ `/health/live` - liveness probe
- ✓ `AddModuleHealthCheck()` extension method
- ✓ `AddDatabaseHealthCheck()` extension method
- ✓ `MapDotNetCloudHealthChecks()` extension method

**File Location:** `src/Core/DotNetCloud.Core.ServiceDefaults/HealthChecks/`  
**Dependencies:** None  
**Testing:** ✅ Builds successfully  
**Notes:** Kubernetes-ready health checks with support for custom module health monitoring. Liveness/readiness probe support included.

---

#### Step: phase-0.3.3 - OpenTelemetry Setup
**Status:** completed ✅
**Duration:** ~2 hours  
**Description:** Configure metrics collection and distributed tracing

**Deliverables:**
- ✓ **Metrics collection:**
  - ✓ HTTP request metrics (ASP.NET Core instrumentation)
  - ✓ HttpClient metrics
  - ✓ Runtime instrumentation (.NET runtime metrics)
  - ✓ gRPC call metrics (GrpcNetClient instrumentation)
  - ✓ Built-in meters: Kestrel, Hosting, Routing, System.Net.Http, System.Net.NameResolution
- ✓ **Distributed tracing:**
  - ✓ W3C Trace Context propagation
  - ✓ ASP.NET Core instrumentation with exception recording
  - ✓ HttpClient instrumentation with exception recording
  - ✓ gRPC client interceptor for tracing
  - ✓ Custom activity sources: Core, Modules, Authentication, Authorization
- ✓ **Exporters:**
  - ✓ Console exporter for development
  - ✓ OTLP exporter for production (Prometheus, Jaeger, etc.)
- ✓ `TelemetryOptions` configuration class
- ✓ `AddDotNetCloudTelemetry()` extension method
- ✓ `TelemetryActivitySources` static class with pre-configured sources
- ✓ Resource builder with service name, version, environment, hostname
- ✓ Sampling configuration (AlwaysOn for dev, TraceIdRatioBased for production)

**File Location:** `src/Core/DotNetCloud.Core.ServiceDefaults/Telemetry/`  
**Dependencies:** Serilog (phase-0.3.1)  
**Testing:** ✅ Builds successfully  
**Notes:** Complete OpenTelemetry setup with metrics and distributed tracing. Production-ready with OTLP export support. Health check endpoints excluded from tracing.

---

#### Step: phase-0.3.4 - Security Middleware
**Status:** completed ✅
**Duration:** ~1.5 hours  
**Description:** Create CORS and security headers middleware

**Deliverables:**
- ✓ **CORS configuration:**
  - ✓ Origin whitelist via configuration (`Cors:AllowedOrigins`)
  - ✓ AllowAnyMethod, AllowAnyHeader, AllowCredentials support
  - ✓ Fallback to AllowAnyOrigin for development
- ✓ **Security headers middleware:**
  - ✓ Content-Security-Policy (customizable policy)
  - ✓ X-Frame-Options (DENY, SAMEORIGIN, ALLOW-FROM)
  - ✓ X-Content-Type-Options (nosniff)
  - ✓ Strict-Transport-Security (HSTS with configurable max-age)
  - ✓ Referrer-Policy (strict-origin-when-cross-origin)
  - ✓ Permissions-Policy (geolocation, microphone, camera restrictions)
  - ✓ Server header removal
  - ✓ X-Powered-By header removal
- ✓ `SecurityHeadersMiddleware` class
- ✓ `SecurityHeadersOptions` configuration class
- ✓ HTTPS-only enforcement for HSTS
- ✓ Integration in `UseDotNetCloudMiddleware()` extension method

**File Location:** `src/Core/DotNetCloud.Core.ServiceDefaults/Middleware/`  
**Dependencies:** None  
**Testing:** ✅ Builds successfully  
**Notes:** Production-grade security headers with sensible defaults. All headers configurable via SecurityHeadersOptions. CORS configured per environment.

---

#### Step: phase-0.3.5 - Global Exception Handler Middleware
**Status:** completed ✅
**Duration:** ~1 hour  
**Description:** Create centralized exception handling middleware

**Deliverables:**
- ✓ `GlobalExceptionHandlerMiddleware` class
- ✓ **Exception-to-HTTP mapping:**
  - ✓ `UnauthorizedException` → 401 Unauthorized
  - ✓ `CapabilityNotGrantedException` → 403 Forbidden
  - ✓ `ValidationException` → 400 Bad Request
  - ✓ `ModuleNotFoundException` → 404 Not Found
  - ✓ `ArgumentException` → 400 Bad Request
  - ✓ `InvalidOperationException` → 409 Conflict
  - ✓ `NotImplementedException` → 501 Not Implemented
  - ✓ All others → 500 Internal Server Error
- ✓ Consistent error response format:
  - ✓ `code` - error code string
  - ✓ `message` - human-readable message
  - ✓ `requestId` - request correlation ID
  - ✓ `timestamp` - error timestamp
  - ✓ `details` - stack trace (dev only)
- ✓ Request ID tracking via `HttpContext.TraceIdentifier`
- ✓ Environment-based stack trace inclusion (dev only)
- ✓ Error logging with exception details
- ✓ JSON response formatting

**File Location:** `src/Core/DotNetCloud.Core.ServiceDefaults/Middleware/`  
**Dependencies:** Logging (phase-0.3.1), Core exceptions  
**Testing:** ✅ Builds successfully  
**Notes:** Catches all unhandled exceptions globally. Provides consistent API error responses. Stack traces hidden in production for security.

---

#### Step: phase-0.3.6 - Request/Response Logging Middleware
**Status:** completed ✅
**Duration:** ~1 hour  
**Description:** Create request/response logging middleware with PII masking

**Deliverables:**
- ✓ `RequestResponseLoggingMiddleware` class
- ✓ **Sensitive data masking:**
  - ✓ Authorization header → `***REDACTED***`
  - ✓ Cookie header → `***REDACTED***`
  - ✓ Set-Cookie header → `***REDACTED***`
  - ✓ X-API-Key header → `***REDACTED***`
  - ✓ X-Auth-Token header → `***REDACTED***`
- ✓ **Excluded paths:**
  - ✓ `/health` - health check endpoints
  - ✓ `/metrics` - metrics endpoints
- ✓ Request logging:
  - ✓ HTTP method, path, remote IP
  - ✓ Scheme, host, query string (debug level)
  - ✓ Masked headers (debug level)
- ✓ Response logging:
  - ✓ Status code, elapsed milliseconds
  - ✓ Log level based on status (Error for 5xx, Warning for 4xx, Info for 2xx/3xx)
- ✓ Request ID enrichment via `LogEnricher.WithRequestId()`
- ✓ Elapsed time tracking with Stopwatch
- ✓ Development-only activation

**File Location:** `src/Core/DotNetCloud.Core.ServiceDefaults/Middleware/`  
**Dependencies:** Logging (phase-0.3.1)  
**Testing:** ✅ Builds successfully  
**Notes:** Automatic request/response logging with sensitive data protection. Only enabled in development. Skips health check and metrics endpoints to reduce noise.

---

#### Step: phase-0.3.7 - ServiceDefaults Integration Extensions
**Status:** completed ✅
**Duration:** ~1 hour  
**Description:** Create extension methods for easy ServiceDefaults registration

**Deliverables:**
- ✓ **`ServiceDefaultsExtensions` class with extension methods:**
  - ✓ `AddDotNetCloudServiceDefaults(IHostApplicationBuilder)` - for generic hosts
  - ✓ `AddDotNetCloudServiceDefaults(WebApplicationBuilder)` - for web applications
  - ✓ `UseDotNetCloudMiddleware(WebApplication)` - middleware pipeline setup
  - ✓ `MapDotNetCloudHealthChecks(WebApplication)` - health check endpoint mapping
  - ✓ `AddModuleHealthCheck(IServiceCollection, IModuleHealthCheck)` - module health registration
  - ✓ `AddDatabaseHealthCheck(IServiceCollection)` - database health registration
- ✓ **Integrated services:**
  - ✓ Serilog logging configuration
  - ✓ OpenTelemetry metrics and tracing
  - ✓ Health checks
  - ✓ CORS with configurable origins
- ✓ **Integrated middleware:**
  - ✓ Security headers
  - ✓ Global exception handler
  - ✓ Request/response logging (dev only)
  - ✓ CORS
  - ✓ HTTPS redirection (production only)
- ✓ Configuration support via `Action<T>` delegates
- ✓ Environment-aware defaults (development vs. production)

**File Location:** `src/Core/DotNetCloud.Core.ServiceDefaults/Extensions/`  
**Dependencies:** All previous phase-0.3 steps  
**Testing:** ✅ Builds successfully  
**Notes:** One-line integration: `builder.AddDotNetCloudServiceDefaults()` and `app.UseDotNetCloudMiddleware()`. All cross-cutting concerns configured automatically.

---

#### Step: phase-0.3.8 - ServiceDefaults Documentation & Project Setup
**Status:** completed ✅
**Duration:** ~1 hour  
**Description:** Create comprehensive README and finalize project setup

**Deliverables:**
- ✓ **Project file (`DotNetCloud.Core.ServiceDefaults.csproj`):**
  - ✓ .NET 10 target framework
  - ✓ NuGet packages: Serilog (4.3.0), OpenTelemetry (1.10.0), AspNetCore.HealthChecks
  - ✓ Project reference to DotNetCloud.Core
  - ✓ XML documentation generation enabled
- ✓ **Comprehensive README.md:**
  - ✓ Features overview (logging, telemetry, health checks, security, error handling)
  - ✓ Installation instructions
  - ✓ Basic usage examples
  - ✓ Custom configuration examples
  - ✓ appsettings.json configuration reference
  - ✓ Log enrichment usage
  - ✓ Custom health check implementation
  - ✓ Custom activity source usage
  - ✓ Security headers configuration
  - ✓ Architecture diagrams (logging flow, telemetry flow, middleware pipeline)
  - ✓ Best practices for each component
  - ✓ Dependencies list
- ✓ All classes have comprehensive XML documentation
- ✓ Project added to solution file
- ✓ Solution builds successfully with no warnings

**File Location:** `src/Core/DotNetCloud.Core.ServiceDefaults/`  
**Dependencies:** All previous phase-0.3 steps  
**Testing:** ✅ Solution builds successfully  
**Notes:** Phase 0.3 complete! Service defaults ready for use in all DotNetCloud projects. Developer documentation provides examples for all features. Zero-config defaults with full customization support.

---

### Section: Phase 0.4 - Authentication & Authorization
**STATUS:** ✅ COMPLETED (20/20 steps)
**DURATION:** ~10 hours (across multiple sessions)
**DELIVERABLES:**
- ✓ OpenIddict database models (Application, Authorization, Token, Scope entities)
- ✓ Auth infrastructure library (DotNetCloud.Core.Auth) with AuthService, MfaService, ClaimsTransformation
- ✓ ASP.NET Core Identity integration with OpenIddict 5.x
- ✓ DotNetCloud.Core.Server web application with HTTP endpoints
- ✓ AuthController (9 endpoints), MfaController (5 endpoints), OpenIddict protocol endpoints (6 endpoints)
- ✓ DataServiceExtensions for multi-database DbContext registration
- ✓ Integration tests (18 tests passing)

---

#### Step: phase-0.4.1 - OpenIddict Database Models & Configuration
**Status:** completed ✅
**Duration:** ~2 hours
**Description:** Create OpenIddict entity models and EF Core configurations for OAuth2/OIDC

**Completed Deliverables:**
- ✓ `OpenIddictApplication` entity with comprehensive XML documentation
  - ✓ Represents OAuth2/OIDC client applications
  - ✓ Properties: ClientId, ClientSecret, RedirectUris, Permissions, Type, ConsentType
  - ✓ Navigation properties to Authorizations and Tokens
  - ✓ Supports confidential, public, and hybrid client types
- ✓ `OpenIddictAuthorization` entity with comprehensive XML documentation
  - ✓ Represents user consent/authorization records
  - ✓ Properties: ApplicationId, Subject, Status, Type, Scopes, CreationDate
  - ✓ Navigation properties to Application and Tokens
  - ✓ Supports permanent and ad-hoc authorization types
- ✓ `OpenIddictToken` entity with comprehensive XML documentation
  - ✓ Represents OAuth2/OIDC tokens (access, refresh, ID tokens, authorization codes)
  - ✓ Properties: ApplicationId, AuthorizationId, Type, Status, Payload, ReferenceId, ExpirationDate
  - ✓ Navigation properties to Application and Authorization
  - ✓ Supports token revocation and redemption tracking
- ✓ `OpenIddictScope` entity with comprehensive XML documentation
  - ✓ Represents OAuth2/OIDC scope definitions
  - ✓ Properties: Name, DisplayName, Description, Resources
  - ✓ Supports localized names and descriptions
  - ✓ Includes standard OIDC scopes and custom scope examples
- ✓ `OpenIddictApplicationConfiguration` (IEntityTypeConfiguration)
  - ✓ Table naming via ITableNamingStrategy (multi-database support)
  - ✓ Primary key, unique constraint on ClientId
  - ✓ Relationships to Authorizations and Tokens with cascade delete
  - ✓ Concurrency token configuration
- ✓ `OpenIddictAuthorizationConfiguration` (IEntityTypeConfiguration)
  - ✓ Table naming via ITableNamingStrategy
  - ✓ Indexes on ApplicationId, Subject, Status
  - ✓ Composite index on (ApplicationId, Subject, Status)
  - ✓ Relationships with cascade delete
- ✓ `OpenIddictTokenConfiguration` (IEntityTypeConfiguration)
  - ✓ Table naming via ITableNamingStrategy
  - ✓ Unique constraint on ReferenceId
  - ✓ Indexes on ApplicationId, AuthorizationId, Subject, Status, Type, ExpirationDate
  - ✓ Composite index on (ApplicationId, Status, Subject, Type)
  - ✓ Relationships with cascade delete
- ✓ `OpenIddictScopeConfiguration` (IEntityTypeConfiguration)
  - ✓ Table naming via ITableNamingStrategy
  - ✓ Unique constraint on Name
  - ✓ Concurrency token configuration
- ✓ CoreDbContext updated with 4 new DbSets:
  - ✓ OpenIddictApplications
  - ✓ OpenIddictAuthorizations
  - ✓ OpenIddictTokens
  - ✓ OpenIddictScopes
- ✓ CoreDbContext updated with ConfigureAuthenticationModels() method
- ✓ All entity configurations integrated into OnModelCreating

**Quality Metrics:**
- ✓ All entities have comprehensive XML documentation (2,500+ lines total)
- ✓ All configurations follow established EF Core patterns
- ✓ Build successful with no compiler errors or warnings
- ✓ Multi-database naming strategy support (PostgreSQL, SQL Server, MariaDB)
- ✓ Proper cascade delete configuration for data integrity
- ✓ Comprehensive indexing for performance
- ✓ Follows OpenIddict entity model best practices

**File Locations:**
- `src/Core/DotNetCloud.Core.Data/Entities/Auth/OpenIddictApplication.cs`
- `src/Core/DotNetCloud.Core.Data/Entities/Auth/OpenIddictAuthorization.cs`
- `src/Core/Core.DotNetCloud.Core.Data/Entities/Auth/OpenIddictToken.cs`
- `src/Core/DotNetCloud.Core.Data/Entities/Auth/OpenIddictScope.cs`
- `src/Core/DotNetCloud.Core.Data/Configuration/Auth/OpenIddictApplicationConfiguration.cs`
- `src/Core/DotNetCloud.Core.Data/Configuration/Auth/OpenIddictAuthorizationConfiguration.cs`
- `src/Core/Core.DotNetCloud.Core.Data/Configuration/Auth/OpenIddictTokenConfiguration.cs`
- `src/Core/DotNetCloud.Core.Data/Configuration/Auth/OpenIddictScopeConfiguration.cs`
- `src/Core/DotNetCloud.Core.Data/Context/CoreDbContext.cs` (updated)

**Dependencies:** phase-0.4.1 ✓
**Testing:** Ready for migration generation in phase-0.4.19
**Build Status:** ✅ Solution builds successfully
**Notes:** OpenIddict entity models complete with comprehensive documentation. Database models ready for OpenIddict server configuration. All entities follow established patterns with proper relationships, indexing, and multi-database support. Ready for phase-0.4.2 (OpenIddict NuGet packages and service configuration).

---

#### Step: phase-0.4.2 through phase-0.4.12 - Auth Infrastructure Library
**Status:** completed ✅
**Duration:** ~6 hours (across 2 sessions)
**Description:** Full authentication & authorization infrastructure layer (no HTTP endpoints — those are Phase 0.7)

**Completed Deliverables:**
- ✓ Fixed OpenIddict entity inheritance: 4 entities now inherit from `OpenIddictEntityFrameworkCore*<Guid>` base classes
- ✓ Replaced 4 broken POCO `IEntityTypeConfiguration` files with `modelBuilder.UseOpenIddict<>()` + naming overrides
- ✓ Added `OpenIddict.EntityFrameworkCore` 5.x to `DotNetCloud.Core.Data.csproj`
- ✓ Created `UserBackupCode` entity (SHA-256 hashed TOTP backup codes with FK to ApplicationUser)
- ✓ Created `FidoCredential` entity skeleton (WebAuthn/passkey data model)
- ✓ Created `UserBackupCodeConfiguration` and `FidoCredentialConfiguration` (EF Core)
- ✓ Updated `CoreDbContext` with `UserBackupCodes`/`FidoCredentials` DbSets
- ✓ Created Auth DTOs: `LoginRequest/Response`, `RegisterRequest/Response`, `RefreshTokenRequest`, `TokenResponse`, `AuthError`, etc.
- ✓ Created MFA DTOs: `TotpSetupResponse`, `TotpVerifyRequest`, `BackupCodesResponse`
- ✓ Created `IAuthService`, `IMfaService`, `IFidoService` interfaces in `DotNetCloud.Core`
- ✓ Created `DotNetCloud.Core.Auth` class library project (net10.0, FrameworkReference ASP.NET Core)
- ✓ Created `AuthOptions` strongly-typed configuration (access/refresh token lifetimes, external auth stubs)
- ✓ Created `AuthServiceExtensions.AddDotNetCloudAuth()`: Identity, OpenIddict 5.x, claims transformation, policies, capabilities
- ✓ Configured OpenIddict 5.x server (JWT default, ephemeral keys, all 6 endpoints, PKCE required, 4 scopes)
- ✓ Implemented `AuthService`: register, login (with lockout + MFA check), logout (token revocation), password reset, email confirmation
- ✓ Implemented `MfaService`: TOTP setup/verify (via ASP.NET Identity), backup codes (10x SHA-256 hashed)
- ✓ Implemented `DotNetCloudClaimsTransformation`: role + locale + timezone claims, 5-min `IMemoryCache`
- ✓ Created `PermissionRequirement` + `PermissionAuthorizationHandler` (`dnc:perm` claims)
- ✓ Created `AuthorizationPolicies` constants + policies registered in DI
- ✓ Created `UserDirectoryService`, `UserManagerService`, `CurrentUserContextService` capability implementations
- ✓ Added `DotNetCloud.Core.Auth`, `DotNetCloud.Core.Data`, and test projects to `DotNetCloud.sln`
- ✓ Generated EF Core migrations: `Phase0_4_Auth` (PostgreSQL) + `Phase0_4_Auth_SqlServer`
- ✓ Created `DotNetCloud.Core.Auth.Tests` project with 31 passing tests covering MfaService, AuthService, ClaimsTransformation, PermissionAuthorizationHandler

**Key Fix:** `UseJsonWebTokens()` removed — JWT is the default token format in OpenIddict 5.8.x (removed from builder API; `UseReferenceAccessTokens()` is the opt-in alternative)

**Dependencies:** phase-0.4.1 ✓
**Build Status:** ✅ All projects build successfully; 0 errors
**Testing:** ✅ 31/31 tests pass (`dotnet test tests/DotNetCloud.Core.Auth.Tests/`)
**Notes:** HTTP endpoint handlers (`/connect/token`, `/connect/authorize`, etc.) are deferred to Phase 0.7. The DI configuration (`AddDotNetCloudAuth`) is fully wired and ready for a web host.
- ☐ Configure PKCE requirements for public clients
- ☐ Create OpenIddictServerConfiguration extension class
- ☐ Integrate with CoreDbContext for data persistence

**Dependencies:** phase-0.4.1 ✓
**Testing:** Service configuration validation
**Notes:** In progress. Will configure OpenIddict server with proper security defaults.

---

### Section: Phase 0.6 - Process Supervisor & gRPC Host

**Status:** completed ✅
**Description:** Process management, module loading, gRPC infrastructure, and inter-process communication

**Deliverables:**
- ✓ ProcessSupervisor (BackgroundService + IProcessSupervisor): spawning, health monitoring, restart policies, graceful shutdown
- ✓ ResourceLimiter: cgroups v2 (Linux) and Job Objects (Windows) for CPU/memory limits
- ✓ ModuleProcessHandle: per-module process state management
- ✓ GrpcChannelManager: channel pooling, Unix socket/named pipe/TCP support
- ✓ ModuleDiscoveryService: filesystem scanning for module binaries
- ✓ ModuleManifestLoader: manifest.json loading and validation
- ✓ ModuleConfigurationLoader: multi-source config (file + DB + core)
- ✓ CapabilityValidator: tier-based capability grant enforcement
- ✓ gRPC interceptors: Auth, CallerContext, Tracing, ErrorHandling, Logging
- ✓ GrpcHealthServiceImpl: gRPC health checking protocol
- ✓ GrpcServerConfiguration: Kestrel listener setup (UDS/pipes/TCP)
- ✓ AuthController & MfaController: REST API controllers for auth flows
- ✓ OpenIddict endpoint mapping extensions
- ✓ Unit tests: ModuleProcessHandleTests, ModuleManifestLoaderTests, GrpcChannelManagerTests, ModuleDiscoveryServiceTests, FilesControllerTests (comprehensive REST endpoint coverage for file controller paths)

**Build Status:** ✅ Full solution builds with zero errors
**Testing:** ✅ 305/305 Server.Tests pass
**Notes:** All Phase 0.6 implementation and unit tests complete. InternalsVisibleTo added to Server project for test access to internal types. NullLogger used in tests to avoid Moq proxy issues with strong-named assemblies. Added comprehensive FilesController unit coverage (29 tests) to lock down endpoint behaviors for CRUD, upload/download, chunk retrieval, share/public-link paths, and caller identity enforcement.

---

### Section: Phase 0.7 - Web Server & API Foundation

**Status:** completed ✅
**Description:** Full ASP.NET Core web server infrastructure including Kestrel configuration, reverse proxy support, API versioning, response envelope, error handling, rate limiting, OpenAPI/Swagger, and CORS.

**Deliverables:**
- ✓ KestrelConfiguration: configurable HTTPS/TLS, HTTP/2, listener addresses, request limits, connection limits
- ✓ ReverseProxyTemplates: nginx, Apache mod_proxy, and IIS ANCM (web.config) template generators with configuration validation
- ✓ Reverse proxy documentation (docs/development/REVERSE_PROXY.md)
- ✓ ApiVersionMiddleware: URL-based versioning (/api/v1/, /api/v2/), version negotiation, deprecation warnings (X-Api-Deprecated, Sunset headers)
- ✓ ApiVersion class: parsing, comparison, equality for semantic API versions
- ✓ ResponseEnvelopeMiddleware: wraps API responses in ApiSuccessResponse/ApiErrorResponse envelope, path-based include/exclude, already-enveloped detection
- ✓ Error handling: GlobalExceptionHandlerMiddleware (pre-existing Phase 0.4), 50+ standard ErrorCodes, stack trace handling (dev vs prod)
- ✓ RateLimitingConfiguration: per-IP global limits, per-user authenticated limits, per-module limits, configurable windows, rejection response with Retry-After headers
- ✓ OpenApiConfiguration: Microsoft.AspNetCore.OpenApi document generation with document transformer, Swagger UI with deep linking/filtering
- ✓ CorsConfiguration: configurable origin whitelist, allowed methods/headers, exposed headers (rate limit + versioning headers), credentials, preflight caching
- ✓ ForwardedHeaders support for reverse proxy X-Forwarded-For/Proto/Host
- ✓ Updated Program.cs pipeline: Kestrel → ForwardedHeaders → Middleware → HealthChecks → OpenAPI → Versioning → Envelope → CORS → RateLimiting → Auth → Controllers
- ✓ Updated appsettings.json and appsettings.Development.json with all new configuration sections
- ✓ Unit tests: ApiVersionTests, ApiVersionMiddlewareTests, ReverseProxyTemplatesTests, KestrelOptionsTests, ResponseEnvelopeMiddlewareTests, RateLimitingOptionsTests, CorsOptionsTests (64 new tests, all passing)

**Build Status:** ✅ Full solution builds with zero errors
**Testing:** ✅ 130/130 Server.Tests pass (66 existing + 64 new)
**Notes:** All Phase 0.7 implementation complete. Uses built-in .NET 10 Microsoft.AspNetCore.OpenApi for schema generation (not Swashbuckle SwaggerGen) due to Microsoft.OpenApi v2.0.0 breaking changes. Swashbuckle UI retained for developer experience.

---

### Section: Phase 0.8 - Real-Time Communication (SignalR)

**Status:** completed ✅
**Description:** SignalR real-time communication infrastructure including hub, connection tracking, presence, broadcasting, and WebSocket configuration.

**Deliverables:**
- ✓ IRealtimeBroadcaster capability interface (Public tier): BroadcastAsync, SendToUserAsync, SendToRoleAsync, AddToGroupAsync, RemoveFromGroupAsync
- ✓ IPresenceTracker capability interface (Public tier): IsOnlineAsync, GetOnlineStatusAsync, GetLastSeenAsync, GetOnlineUsersAsync, GetActiveConnectionCountAsync
- ✓ RealtimeDtos: UserPresenceDto, RealtimeMessageDto
- ✓ PresenceEvents: UserConnectedEvent, UserDisconnectedEvent
- ✓ SignalROptions: configurable keep-alive, client timeout, handshake timeout, message sizes, transport toggles, hub path, connection limits, presence cleanup interval
- ✓ UserConnectionTracker: thread-safe user-to-connectionId mapping with multi-device support, first/last connection detection
- ✓ CoreHub: [Authorize] SignalR hub with OnConnectedAsync/OnDisconnectedAsync lifecycle, JoinGroupAsync/LeaveGroupAsync, PingAsync heartbeat, UserOnline/UserOffline broadcasts
- ✓ PresenceService: IPresenceTracker implementation with ConcurrentDictionary last-seen tracking, delegates online status to UserConnectionTracker
- ✓ RealtimeBroadcasterService: IRealtimeBroadcaster implementation using IHubContext<CoreHub>, role-based groups via "role:{roleName}" convention
- ✓ SignalRServiceExtensions: AddDotNetCloudSignalR (DI registration), MapDotNetCloudHubs (hub endpoint + transport config)
- ✓ Program.cs integration: SignalR services registered, hub mapped after controllers
- ✓ appsettings.json/Development.json updated with SignalR configuration section
- ✓ Unit tests: UserConnectionTrackerTests (20), PresenceServiceTests (11), SignalROptionsTests (13), RealtimeBroadcasterServiceTests (18) — 62 new tests

**File Locations:**
- `src/Core/DotNetCloud.Core/Capabilities/IRealtimeBroadcaster.cs`
- `src/Core/DotNetCloud.Core/Capabilities/IPresenceTracker.cs`
- `src/Core/DotNetCloud.Core/DTOs/RealtimeDtos.cs`
- `src/Core/DotNetCloud.Core/Events/PresenceEvents.cs`
- `src/Core/DotNetCloud.Core.Server/Configuration/SignalRConfiguration.cs`
- `src/Core/DotNetCloud.Core.Server/RealTime/UserConnectionTracker.cs`
- `src/Core/DotNetCloud.Core.Server/RealTime/CoreHub.cs`
- `src/Core/DotNetCloud.Core.Server/RealTime/PresenceService.cs`
- `src/Core/DotNetCloud.Core.Server/RealTime/RealtimeBroadcasterService.cs`
- `src/Core/DotNetCloud.Core.Server/Extensions/SignalRServiceExtensions.cs`
- `tests/DotNetCloud.Core.Server.Tests/RealTime/*.cs` (4 test files)

**Build Status:** ✅ Full solution builds with zero errors
**Testing:** ✅ 192/192 Server.Tests pass (130 existing + 62 new)
**Notes:** All Phase 0.8 implementation complete. SignalR hub lives in the core process; modules use IRealtimeBroadcaster capability interface to push real-time messages without depending on SignalR directly. Presence tracking is in-memory (suitable for single-server deployments; Redis backplane can be added later for scale-out).

---

### Section: Phase 0.9 - Authentication API Endpoints

**Status:** completed ✅
**Description:** REST endpoints for all authentication flows — user auth, OAuth2/OIDC, MFA (TOTP + passkey skeleton), password management, and device management. Routes restructured to `/api/v1/core/auth/` namespace.

**Deliverables:**
- ✓ `POST /api/v1/core/auth/register` — User registration
- ✓ `POST /api/v1/core/auth/login` — User login (credential validation, MFA detection)
- ✓ `POST /api/v1/core/auth/logout` — Revoke all tokens for user
- ✓ `POST /api/v1/core/auth/refresh` — Refresh access token via refresh token
- ✓ `GET /api/v1/core/auth/user` — Get current user profile (new: queries Identity + roles + MFA status)
- ✓ `GET /api/v1/core/auth/external-login/{provider}` — External provider challenge redirect
- ✓ `GET /api/v1/core/auth/external-callback` — External provider callback handler
- ✓ `GET /.well-known/openid-configuration` — OIDC discovery (via OpenIddict)
- ✓ `POST /api/v1/core/auth/mfa/totp/setup` — TOTP authenticator setup
- ✓ `POST /api/v1/core/auth/mfa/totp/verify` — Verify TOTP code
- ✓ `POST /api/v1/core/auth/mfa/totp/disable` — Disable TOTP
- ✓ `POST /api/v1/core/auth/mfa/passkey/setup` — Passkey registration skeleton (FidoCredential entity ready)
- ✓ `POST /api/v1/core/auth/mfa/passkey/verify` — Passkey assertion skeleton
- ✓ `GET /api/v1/core/auth/mfa/backup-codes` — Generate backup codes
- ✓ `GET /api/v1/core/auth/mfa/status` — MFA status for current user
- ✓ `POST /api/v1/core/auth/password/change` — Change password (verifies current password via Identity)
- ✓ `POST /api/v1/core/auth/password/forgot` — Request password reset (anti-enumeration)
- ✓ `POST /api/v1/core/auth/password/reset` — Reset password with token
- ✓ `GET /api/v1/core/auth/devices` — List user's registered devices
- ✓ `DELETE /api/v1/core/auth/devices/{deviceId}` — Remove device (ownership validated)
- ✓ `IAuthService.ChangePasswordAsync` — New method using Identity's ChangePasswordAsync
- ✓ `IAuthService.GetUserProfileAsync` — New method returning full profile + roles + MFA status
- ✓ `IDeviceService` interface + `DeviceService` implementation (EF Core, CoreDbContext)
- ✓ `UserProfileResponse` DTO added to AuthDtos.cs
- ✓ `DeviceController` — New controller for device management endpoints
- ✓ DI registration in `AuthServiceExtensions.AddDotNetCloudAuth`
- ✓ Unit tests: 10 DeviceServiceTests + 6 AuthServiceTests (ChangePasswordAsync, GetUserProfileAsync)

**File Locations:**
- `src/Core/DotNetCloud.Core/Services/IAuthService.cs` (modified — ChangePasswordAsync, GetUserProfileAsync)
- `src/Core/DotNetCloud.Core/Services/IDeviceService.cs` (new)
- `src/Core/DotNetCloud.Core/DTOs/AuthDtos.cs` (modified — UserProfileResponse)
- `src/Core/DotNetCloud.Core.Auth/Services/AuthService.cs` (modified — 2 new methods)
- `src/Core/DotNetCloud.Core.Auth/Services/DeviceService.cs` (new)
- `src/Core/DotNetCloud.Core.Auth/Extensions/AuthServiceExtensions.cs` (modified — IDeviceService DI)
- `src/Core/DotNetCloud.Core.Server/Controllers/AuthController.cs` (restructured — route, new endpoints)
- `src/Core/DotNetCloud.Core.Server/Controllers/MfaController.cs` (restructured — route, passkey, backup-codes)
- `src/Core/DotNetCloud.Core.Server/Controllers/DeviceController.cs` (new)
- `tests/DotNetCloud.Core.Auth.Tests/Services/DeviceServiceTests.cs` (new — 10 tests)
- `tests/DotNetCloud.Core.Auth.Tests/Services/AuthServiceTests.cs` (modified — 6 new tests)

**Build Status:** ✅ Full solution builds with zero errors
**Testing:** ✅ 186/186 tests pass across solution (16 new tests added)
**Notes:** All Phase 0.9 endpoints implemented. Routes moved from `/api/v1/auth/` to `/api/v1/core/auth/` to match the planned URL structure. Passkey endpoints are skeleton implementations — full FIDO2/WebAuthn requires a dedicated library (e.g., FIDO2.NET) which will be integrated when Phase 0.x addresses passkey hardware support. External login endpoints redirect to ASP.NET Core's Challenge flow; actual provider configuration (Google, GitHub, etc.) is a deployment-time concern.

---

### Section: Phase 0.10 - User & Admin Management

**Status:** completed ✅
**Description:** Administrative REST endpoints for user management (list, get, update, delete, disable/enable, password reset), system settings CRUD, module lifecycle management (list, start/stop/restart, capability grant/revoke), and system health checks. All endpoints are admin-only (RequireAdmin policy) except user profile self-view and self-update.

**Deliverables:**
- ✓ `IUserManagementService` interface — list, get, update, delete, disable, enable, admin password reset
- ✓ `IAdminSettingsService` interface — list, get, upsert, delete system settings
- ✓ `IAdminModuleService` interface — list, get, start/stop/restart modules, grant/revoke capabilities
- ✓ `UserListQuery` DTO — pagination, search, sort, active-status filter
- ✓ `PaginatedResult<T>` DTO — generic paginated response with page/totalCount/totalPages
- ✓ `AdminResetPasswordRequest` DTO — admin-initiated password reset (no current password)
- ✓ Error codes added: `SETTING_NOT_FOUND`, `SETTING_INVALID_VALUE`, `ADMIN_PASSWORD_RESET_FAILED`, `USER_ALREADY_DISABLED`, `USER_ALREADY_ENABLED`
- ✓ `UserManagementService` implementation (ASP.NET Core Identity, UserManager)
- ✓ `AdminSettingsService` implementation (EF Core, CoreDbContext)
- ✓ `AdminModuleService` implementation (EF Core + IProcessSupervisor for lifecycle)
- ✓ `UserManagementController` — 7 endpoints at `/api/v1/core/users/`
  - ✓ `GET /api/v1/core/users` — List users with pagination (admin only)
  - ✓ `GET /api/v1/core/users/{userId}` — Get user details (self or admin)
  - ✓ `PUT /api/v1/core/users/{userId}` — Update user profile (self or admin)
  - ✓ `DELETE /api/v1/core/users/{userId}` — Delete user (admin only, self-delete blocked)
  - ✓ `POST /api/v1/core/users/{userId}/disable` — Disable user (admin only, self-disable blocked)
  - ✓ `POST /api/v1/core/users/{userId}/enable` — Enable user (admin only)
  - ✓ `POST /api/v1/core/users/{userId}/reset-password` — Admin password reset
- ✓ `AdminController` — 12 endpoints at `/api/v1/core/admin/`
  - ✓ `GET /api/v1/core/admin/settings` — List settings (optional module filter)
  - ✓ `GET /api/v1/core/admin/settings/{module}/{key}` — Get specific setting
  - ✓ `PUT /api/v1/core/admin/settings/{module}/{key}` — Create/update setting
  - ✓ `DELETE /api/v1/core/admin/settings/{module}/{key}` — Delete setting
  - ✓ `GET /api/v1/core/admin/modules` — List installed modules
  - ✓ `GET /api/v1/core/admin/modules/{moduleId}` — Get module details
  - ✓ `POST /api/v1/core/admin/modules/{moduleId}/start` — Start module
  - ✓ `POST /api/v1/core/admin/modules/{moduleId}/stop` — Stop module
  - ✓ `POST /api/v1/core/admin/modules/{moduleId}/restart` — Restart module
  - ✓ `POST /api/v1/core/admin/modules/{moduleId}/capabilities/{capability}/grant` — Grant capability
  - ✓ `DELETE /api/v1/core/admin/modules/{moduleId}/capabilities/{capability}` — Revoke capability
  - ✓ `GET /api/v1/core/admin/health` — Detailed system health report
- ✓ DI registration in `AuthServiceExtensions` (UserManagementService, AdminSettingsService)
- ✓ DI registration in `SupervisorServiceExtensions` (AdminModuleService)
- ✓ Unit tests: 14 UserManagementServiceTests + 9 AdminSettingsServiceTests (23 total)

**File Locations:**
- `src/Core/DotNetCloud.Core/Services/IUserManagementService.cs` (new)
- `src/Core/DotNetCloud.Core/Services/IAdminSettingsService.cs` (new)
- `src/Core/DotNetCloud.Core/Services/IAdminModuleService.cs` (new)
- `src/Core/DotNetCloud.Core/DTOs/AdminDtos.cs` (new — UserListQuery, PaginatedResult<T>, AdminResetPasswordRequest)
- `src/Core/DotNetCloud.Core/Errors/ErrorCodes.cs` (modified — 5 new error codes)
- `src/Core/DotNetCloud.Core.Auth/Services/UserManagementService.cs` (new)
- `src/Core/DotNetCloud.Core.Auth/Services/AdminSettingsService.cs` (new)
- `src/Core/DotNetCloud.Core.Auth/Extensions/AuthServiceExtensions.cs` (modified — 2 new service registrations)
- `src/Core/DotNetCloud.Core.Server/Services/AdminModuleService.cs` (new)
- `src/Core/DotNetCloud.Core.Server/Extensions/SupervisorServiceExtensions.cs` (modified — AdminModuleService DI)
- `src/Core/DotNetCloud.Core.Server/Controllers/UserManagementController.cs` (new)
- `src/Core/DotNetCloud.Core.Server/Controllers/AdminController.cs` (new)
- `tests/DotNetCloud.Core.Auth.Tests/Services/UserManagementServiceTests.cs` (new — 14 tests)
- `tests/DotNetCloud.Core.Auth.Tests/Services/AdminSettingsServiceTests.cs` (new — 9 tests)

**Build Status:** ✅ Full solution builds with zero errors
**Testing:** ✅ 69/69 tests pass across solution (23 new tests added)
**Notes:** All Phase 0.10 endpoints implemented. User management includes self-action guards (cannot delete/disable own account). Settings use composite key (module, key) to match the SystemSetting entity model. Module management delegates to IProcessSupervisor for start/stop/restart and uses EF Core for capability grant persistence. Health endpoint uses ASP.NET Core's built-in HealthCheckService for comprehensive reporting.

---

### Section: Phase 0.11 - Web UI Shell (Blazor)

**Status:** completed ✅
**Description:** Blazor InteractiveAuto web UI shell with two projects: `DotNetCloud.UI.Web` (server-side RCL with SSR auth pages, layouts, and App.razor) and `DotNetCloud.UI.Web.Client` (WebAssembly project with interactive admin pages). Uses InteractiveAuto render mode so components pre-render on the server then switch to WebAssembly. Includes complete admin dashboard, user management, module management, settings management, health monitoring, authentication pages (login, register, forgot password, reset password, MFA verification, logout), module plugin system for dynamic UI extension, light/dark theme toggle, toast notifications, confirmation dialogs, and responsive sidebar navigation.

**Deliverables:**
- ✓ `DotNetCloud.UI.Web` Razor Class Library (server-side root, SSR auth pages, layouts)
  - ✓ `Components/App.razor` — root document with InteractiveAuto HeadOutlet and Routes
  - ✓ `Components/Routes.razor` — router scanning both UI.Web and UI.Web.Client assemblies
  - ✓ `Components/Layout/MainLayout.razor` — app shell with sidebar, topbar, dark mode, error boundary
  - ✓ `Components/Layout/NavMenu.razor` — sidebar navigation with dynamic module items
  - ✓ `Components/Pages/Home.razor` — role-aware home dashboard for non-admin users with module quick links
  - ✓ `Components/Layout/AuthLayout.razor` — minimal centered layout for auth pages
  - ✓ `Components/Pages/Auth/Login.razor` — SSR login with SignInManager cookie auth
  - ✓ `Components/Pages/Auth/Register.razor` — SSR registration with UserManager
  - ✓ `Components/Pages/Auth/ForgotPassword.razor` — SSR forgot password flow
  - ✓ `Components/Pages/Auth/ResetPassword.razor` — SSR password reset with token
  - ✓ `Components/Pages/Auth/MfaVerify.razor` — SSR TOTP verification
  - ✓ `Components/Pages/Auth/Logout.razor` — SSR sign-out and redirect
  - ✓ `Components/Shared/RedirectToLogin.razor` — unauthorized redirect helper
  - ✓ `Components/Shared/ErrorDisplay.razor` — error boundary content
  - ✓ `Components/Shared/ModulePageHost.razor` — dynamic component loader for modules
  - ✓ `Services/ModuleUiRegistry.cs` — module nav item and page registration
  - ✓ `Initialization/ModuleUiRegistrationHostedService.cs` — registers installed/enabled Files+Chat UI pages into nav and refreshes registrations automatically when module status changes
  - ✓ `Components/Pages/Modules/Files.razor` — `/apps/files` module route host
  - ✓ `Components/Pages/Modules/Chat.razor` — `/apps/chat` module route host
  - ✓ Module host routes use `InteractiveServer` render mode so module UI event handlers (New Folder, Upload, Create Channel) are active
  - ✓ Files and Chat module UI actions are service-backed (folder create/upload/list refresh and channel list/create)
  - ✓ Files upload queue now keeps selection lightweight and reads/caches bytes at upload-time to avoid reader-lifecycle failures without blocking file picker UX
  - ✓ Files upload start now pre-buffers all pending selected files before per-file network upload to prevent later-file reader expiry in a batch
  - ✓ Files upload dialog maps low-level reader lifecycle exceptions to a clear reselect-and-retry user message
  - ✓ Core server registers in-process Files/Chat module data services so module UI actions execute against real module logic
  - ✓ Files folder entries support direct click-to-open navigation and use real file/folder icons instead of placeholder text
  - ✓ `wwwroot/css/app.css` — complete CSS theme (500+ lines, light/dark, responsive)
- ✓ `DotNetCloud.UI.Web.Client` WebAssembly project (interactive admin pages)
  - ✓ `Program.cs` — WASM host builder with auth, HttpClient, API client, ToastService
  - ✓ `Services/DotNetCloudApiClient.cs` — typed HTTP client for all REST API endpoints
  - ✓ `Services/ToastService.cs` — toast notification state management
  - ✓ `Shared/ToastContainer.razor` — toast notification display
  - ✓ `Shared/LoadingIndicator.razor` — spinner with optional message
  - ✓ `Shared/ConfirmDialog.razor` — async confirmation dialog
  - ✓ `Pages/Admin/Dashboard.razor` — summary cards (users, modules, settings, health)
  - ✓ `Pages/Admin/ModuleList.razor` — module table with start/stop/restart actions
  - ✓ `Pages/Admin/ModuleDetail.razor` — module info, capabilities, events, actions
  - ✓ `Pages/Admin/UserList.razor` — paginated user table with search
  - ✓ `Pages/Admin/UserDetail.razor` — user profile, roles, disable/enable/delete/reset
  - ✓ `Pages/Admin/UserCreate.razor` — create user form via RegisterRequest
  - ✓ `Pages/Admin/UserEdit.razor` — edit user profile form
  - ✓ `Pages/Admin/Settings.razor` — settings table with inline edit dialog
  - ✓ `Pages/Admin/Health.razor` — system health report with per-component status
- ✓ Server integration in `DotNetCloud.Core.Server/Program.cs`
  - ✓ `AddRazorComponents().AddInteractiveServerComponents().AddInteractiveWebAssemblyComponents()`
  - ✓ `MapRazorComponents<App>().AddInteractiveServerRenderMode().AddInteractiveWebAssemblyRenderMode()`
  - ✓ Server-side DI for ModuleUiRegistry, ToastService, HttpClient, DotNetCloudApiClient
  - ✓ `Microsoft.AspNetCore.Components.WebAssembly.Server` package added
  - ✓ Static files, antiforgery middleware configured
  - ✓ Persist ASP.NET Core DataProtection keys to `DOTNETCLOUD_DATA_DIR/data-protection-keys`
- ☐ Backup/restore settings page (deferred to Phase 0.13 CLI)
- ☐ Brand assets/logos (deferred — placeholder emoji icons used)

**File Locations:**
- `src/UI/DotNetCloud.UI.Web/` — Server-side RCL (17 files)
- `src/UI/DotNetCloud.UI.Web.Client/` — WebAssembly project (15 files)
- `src/Core/DotNetCloud.Core.Server/Program.cs` (modified — Blazor integration)
- `src/Core/DotNetCloud.Core.Server/DotNetCloud.Core.Server.csproj` (modified — UI project refs + WASM server pkg)

**Build Status:** ✅ Full solution builds with zero errors, zero warnings
**Testing:** ✅ 539/539 tests pass (108 Core + 186 Server + 69 Auth + 176 Data)
**Notes:** InteractiveAuto chosen per project requirements. Auth pages use SSR (need HttpContext for cookie sign-in via SignInManager). Admin pages use InteractiveAuto via HttpClient-based API calls so they work in both server prerendering and WebAssembly contexts. Module plugin system allows future modules to register nav items and page components dynamically via ModuleUiRegistry. Home route (`/`) now serves as a role-aware user dashboard so non-admin users land on a non-admin experience by default. Files and Chat are wired into module UI registration and exposed via `/apps/files` and `/apps/chat` route hosts when installed and enabled. Module nav/page registration now refreshes automatically in-process when module status changes, so admin enable/disable actions are reflected without a server restart. Module host routes are explicitly interactive (`InteractiveServer`) to ensure module component button actions are wired at runtime. Files and Chat module actions now execute against registered module services in Core.Server and refresh correctly in the UI, and module contexts are now aligned to the configured core database provider (PostgreSQL/SQL Server; MariaDB remains deferred pending provider support). Files folder items now support direct click-to-open navigation, and file-type glyphs are rendered as icons instead of placeholder text tokens. Startup now explicitly checks module sentinel tables and creates module tables in the shared relational database when missing, preventing `42P01 relation \"FileNodes\" does not exist` errors. Provider-incompatible SQL Server bracket syntax in Files filtered index definitions was corrected for PostgreSQL-compatible table creation. Files upload UI was polished with dedicated styling for dialog/dropzone/progress states and iconized controls, replacing scaffold-style raw tokens. Files initial view now includes structured layout styling for breadcrumbs/actions/list rows, and stylesheet cache-busting query parameters were added so clients reliably receive current CSS after deploys. Upload initiation now bootstraps missing user quota rows before quota checks and surfaces upload exceptions directly in the dialog, preventing silent no-op upload behavior. Upload dialog close behavior now gates on full success, so failed uploads remain visible with an actionable error instead of auto-closing like a success path. Upload start flow now includes top-level exception handling so claim-resolution or other pre-upload failures surface visibly in the dialog instead of appearing as inert button clicks. Files upload selection now remains lightweight while bytes are read and cached at upload-time, preventing file-picker stalls while still avoiding reader-completed failures from expired browser handles. Upload error display now maps the low-level reader-completed exception to a clear reselect-and-retry message for end users. Files storage path default now falls back to `DOTNETCLOUD_DATA_DIR/storage` when `Files:StoragePath` is not configured, preventing read-only write failures under hardened systemd deployments. DataProtection key persistence is configured under `DOTNETCLOUD_DATA_DIR/data-protection-keys` to avoid ephemeral key warnings and improve restart stability for auth and antiforgery tokens.

---

### Phase 0.12: Shared UI Components

#### Step: phase-0.12.1 - DotNetCloud.UI.Shared Project
**Status:** completed ✅
**Duration:** ~3 hours
**Description:** Create reusable Blazor component library for the entire DotNetCloud UI

**Completed Deliverables:**

**Project Setup:**
- ✓ Created `DotNetCloud.UI.Shared` Razor Class Library (RCL) project
- ✓ Configured for WASM compatibility (`Microsoft.AspNetCore.Components.Web` package reference)
- ✓ Added to solution, referenced from `DotNetCloud.UI.Web` and `DotNetCloud.UI.Web.Client`
- ✓ Updated `_Imports.razor` in all three UI projects with shared component namespaces
- ✓ CSS stylesheet linked in `App.razor` (`DotNetCloud.UI.Shared.styles.css`)

**Form Components (7 components + 1 record):**
- ✓ `DncInput` — text/password/email input with label, validation, and disabled state (inherits InputBase<string>)
- ✓ `DncSelect` — dropdown select with default option text (inherits InputSelect<string>)
- ✓ `DncCheckbox` — checkbox with label (inherits InputBase<bool>)
- ✓ `DncRadioGroup` — radio button group with inline option, uses `RadioOption` record
- ✓ `DncTextArea` — multiline text input with rows parameter (inherits InputTextArea)
- ✓ `DncDatePicker` — date/datetime-local/month/time picker (inherits InputDate<DateTime?>)
- ✓ `DncFormValidation` — DataAnnotationsValidator + ValidationSummary wrapper

**Data Display Components (5 components + 3 supporting types):**
- ✓ `DncDataTable<TItem>` — generic data table with sorting, pagination, custom templates, row click
- ✓ `DataTableColumn<TItem>` — column definition with SortKey, Template, CssClass
- ✓ `DncPaginator` — previous/next pagination with page info display
- ✓ `DncBreadcrumb` — breadcrumb navigation trail using `BreadcrumbItem` record
- ✓ `DncTabs` — tab header with two-way `ActiveTabId` binding, uses `TabItem` record
- ✓ `DncAccordion` — collapsible sections with AllowMultiple option, uses `AccordionSection` class

**Dialog Components (3 components + 1 enum):**
- ✓ `DncModal` — generic modal with title, body, footer, overlay click dismiss
- ✓ `DncConfirmDialog` — async ShowAsync returning bool, configurable button text/class
- ✓ `DncAlertDialog` — alert with severity level, dismiss callback
- ✓ `AlertLevel` enum (Success, Warning, Danger)

**Navigation Components (4 components + 3 supporting types):**
- ✓ `DncSidebar` — collapsible sidebar with brand icon/text, nav items, extra content slot
- ✓ `DncTopBar` — header bar with title, left/right content slots
- ✓ `DncMenu` — NavLink-based menu list using `NavItem` record
- ✓ `DncButton` — button with Variant (Primary/Danger/Warning/Success/Outline), Size (Default/Small), Loading spinner
- ✓ `ButtonVariant`, `ButtonSize` enums, `NavItem` record

**Notification Components (5 components + 1 service):**
- ✓ `DncToast` — toast container driven by `DncToastService` parameter
- ✓ `DncToastService` — singleton service with ShowSuccess/Error/Warning/Info, auto-dismiss
- ✓ `ToastMessage` record, `ToastLevel` enum
- ✓ `DncAlert` — inline dismissible alert with AlertLevel
- ✓ `DncBadge` — inline badge with variant (success/warning/danger/muted)
- ✓ `DncLoadingIndicator` — spinner with optional message
- ✓ `DncErrorDisplay` — error display with exception message and retry button

**Layout Components (4 components):**
- ✓ `DncCard` — card with optional title/header, body, footer
- ✓ `DncPanel` — surface panel with title (maps to existing detail-section style)
- ✓ `DncSection` — page section with title and action buttons slot
- ✓ `DncGrid` — responsive grid with 1-4 columns, mobile collapse

**Styling:**
- ✓ `DotNetCloud.UI.Shared.styles.css` — component-specific styles (checkbox/radio, breadcrumb, tabs, accordion, sortable headers, card, grid, validation summary, responsive breakpoints)
- ✓ Theme variables inherited from existing `app.css` custom properties
- ✓ Responsive breakpoints at 768px and 1024px

**File Locations:**
- `src/UI/DotNetCloud.UI.Shared/` — 40 files (1 csproj, 1 _Imports, 1 CSS, 24 .razor, 7 .cs, 6 supporting types)
- `src/UI/DotNetCloud.UI.Web/Components/App.razor` (modified — CSS link added)
- `src/UI/DotNetCloud.UI.Web/_Imports.razor` (modified — shared namespaces)
- `src/UI/DotNetCloud.UI.Web.Client/_Imports.razor` (modified — shared namespaces)

**Build Status:** ✅ Full solution builds with zero errors, zero warnings (14 projects)
**Testing:** ✅ 539/539 tests pass (no regressions)
**Notes:** Existing Page components (ConfirmDialog, ToastContainer, LoadingIndicator) left intact in DotNetCloud.UI.Web.Client.Shared — shared library provides standardized replacements available for all new development. Components designed to work in both SSR and InteractiveAuto render modes.

---

### Phase 0.13: CLI Management Tool

#### Step: phase-0.13.1 - DotNetCloud.CLI Project
**Status:** completed ✅
**Duration:** ~3 hours
**Description:** Create CLI management tool with System.CommandLine for all administration tasks

**Deliverables:**
- ✓ Console application project (`DotNetCloud.CLI.csproj`) with System.CommandLine 2.0.3
- ✓ Project references to Core, Core.Data, Core.ServiceDefaults
- ✓ Assembly name `dotnetcloud` for ergonomic CLI usage
- ✓ CLI infrastructure (CliConfiguration, ConsoleOutput, ServiceProviderFactory)
- ✓ Setup command — interactive first-run wizard:
  - ✓ Database selection (PostgreSQL/SQL Server/MariaDB)
  - ✓ Connection string configuration with verification
  - ✓ Admin user creation (email + password)
  - ✓ MFA setup prompt
  - ✓ Organization setup
  - ✓ TLS/HTTPS configuration with Let's Encrypt option
  - ✓ Module selection (files, chat, contacts, calendar, notes, deck)
  - ✓ Data/log/backup directory configuration
  - ✓ Configuration summary and save to JSON
- ✓ Service commands:
  - ✓ `dotnetcloud serve` — start server (foreground/background modes, PID file tracking)
  - ✓ `dotnetcloud stop` — graceful shutdown via PID
  - ✓ `dotnetcloud status` — show server process, config, memory, uptime
  - ✓ `dotnetcloud status` — probes HTTP listener and `/health/live`; warns when process is running but no port listener is active
  - ✓ `dotnetcloud restart` — stop then start
- ✓ Module commands:
  - ✓ `dotnetcloud module list` — list installed modules from DB with table output
  - ✓ `dotnetcloud module start {module}` — enable module in DB
  - ✓ `dotnetcloud module stop {module}` — disable module in DB
  - ✓ `dotnetcloud module restart {module}` — request restart via supervisor
  - ✓ `dotnetcloud module install {module}` — register module in DB
  - ✓ `dotnetcloud module uninstall {module}` — remove module and capability grants
- ✓ Component commands:
  - ✓ `dotnetcloud component status {component}` — check database, server, modules, signalr, grpc
  - ✓ `dotnetcloud component restart {component}` — restart guidance
- ✓ Log commands:
  - ✓ `dotnetcloud logs` — view system logs with colored output
  - ✓ `dotnetcloud logs {module}` — module-specific log filtering
  - ✓ `dotnetcloud logs --level {level}` — Serilog level filtering (DBG/INF/WRN/ERR/FTL)
  - ✓ `dotnetcloud logs --tail N` — show last N lines
  - ✓ `dotnetcloud logs --follow` — real-time log tailing
  - ✓ Read-only commands (`status`, `version`, `logs`) use safe config loading and show actionable permission errors instead of throwing
- ✓ Backup commands:
  - ✓ `dotnetcloud backup` — create ZIP backup of config + data
  - ✓ `dotnetcloud backup --output {path}` — custom output path
  - ✓ `dotnetcloud backup restore {file}` — restore from ZIP backup
  - ✓ `dotnetcloud backup schedule {interval}` — cron/schtasks guidance (daily/weekly/monthly)
- ✓ Miscellaneous commands:
  - ✓ `dotnetcloud update` — update check (placeholder for future remote check)
  - ✓ `dotnetcloud version` — version, runtime, OS, architecture info
  - ✓ `dotnetcloud help` — built-in via System.CommandLine
  - ✓ `dotnetcloud help {command}` — built-in per-command help
- ✓ Unit tests (118 tests, all passing):
  - ✓ `CliConfigTests` — 16 tests (defaults, JSON serialization roundtrip, save/load to disk)
  - ✓ `ConsoleOutputTests` — 16 tests (FormatStatus color mappings, case insensitivity)
  - ✓ `SetupCommandTests` — 9 tests (MaskConnectionString, command name/description)
  - ✓ `CommandStructureTests` — 25 tests (all commands, subcommands, options, arguments validated)
  - ✓ `SystemdServiceHelperTests` — 15 tests (Type=forking, PIDFile, no ExecStop, RuntimeDirectory, hardening, systemd format validation)

**File Locations:**
- `src/CLI/DotNetCloud.CLI/DotNetCloud.CLI.csproj` — project file
- `src/CLI/DotNetCloud.CLI/Program.cs` — entry point, root command registration
- `src/CLI/DotNetCloud.CLI/Infrastructure/CliConfiguration.cs` — config file management
- `src/CLI/DotNetCloud.CLI/Infrastructure/ConsoleOutput.cs` — formatted console output
- `src/CLI/DotNetCloud.CLI/Infrastructure/ServiceProviderFactory.cs` — DI for DB access
- `src/CLI/DotNetCloud.CLI/Commands/SetupCommand.cs` — setup wizard
- `src/CLI/DotNetCloud.CLI/Commands/ServiceCommands.cs` — serve/stop/status/restart
- `src/CLI/DotNetCloud.CLI/Commands/ModuleCommands.cs` — module lifecycle
- `src/CLI/DotNetCloud.CLI/Commands/ComponentCommands.cs` — component status/restart
- `src/CLI/DotNetCloud.CLI/Commands/LogCommands.cs` — log viewing
- `src/CLI/DotNetCloud.CLI/Commands/BackupCommands.cs` — backup/restore/schedule
- `src/CLI/DotNetCloud.CLI/Commands/MiscCommands.cs` — update/version
- `tests/DotNetCloud.CLI.Tests/DotNetCloud.CLI.Tests.csproj` — test project
- `tests/DotNetCloud.CLI.Tests/Infrastructure/CliConfigTests.cs` — config tests
- `tests/DotNetCloud.CLI.Tests/Infrastructure/ConsoleOutputTests.cs` — console output tests
- `tests/DotNetCloud.CLI.Tests/Commands/SetupCommandTests.cs` — setup command tests
- `tests/DotNetCloud.CLI.Tests/Commands/CommandStructureTests.cs` — command structure tests
- `tests/DotNetCloud.CLI.Tests/Infrastructure/SystemdServiceHelperTests.cs` — systemd unit file generation tests

**Build Status:** ✅ Full solution builds with zero errors, zero warnings
**Testing:** ✅ 118 CLI tests pass (16 Config + 16 Console + 9 Setup + 25 Structure + 15 Systemd + 37 other)
**Notes:** CLI uses System.CommandLine 2.0.3 (stable). Argument/Option constructors use name-only with Description via object initializer (2.0.3 API). Commands that need DB access use ServiceProviderFactory which builds a minimal DI container with AddDotNetCloudDbContext. Configuration persisted as JSON in AppData/dotnetcloud. Server management uses PID file for process tracking and now cross-checks process state against listener/health probes for clearer diagnostics. Read-only commands now handle permission-denied config reads (common on `/etc/dotnetcloud/config.json`) without crashing and provide remediation guidance. Help is automatically generated by System.CommandLine for all commands and subcommands.

---

### Phase 0.14: Example Module Reference

#### Step: phase-0.14.1 - Example Module Reference Implementation
**Status:** completed ✅
**Duration:** ~2 hours
**Description:** Create a complete reference implementation of a DotNetCloud module demonstrating lifecycle, capabilities, events, gRPC, data access, and Blazor UI.

**Deliverables:**
- ✓ `DotNetCloud.Modules.Example` project (core logic, Razor SDK):
  - ✓ `ExampleModuleManifest` implementing `IModuleManifest` (id: dotnetcloud.example, capabilities: INotificationService + IStorageProvider)
  - ✓ `ExampleModule` implementing `IModuleLifecycle` (full lifecycle: Initialize, Start, Stop, Dispose)
  - ✓ `ExampleNote` domain model (Id, Title, Content, CreatedByUserId, timestamps)
  - ✓ `NoteCreatedEvent` and `NoteDeletedEvent` domain events implementing `IEvent`
  - ✓ `NoteCreatedEventHandler` implementing `IEventHandler<NoteCreatedEvent>`
  - ✓ `CreateNoteAsync` method demonstrating event publishing via `IEventBus`
  - ✓ Blazor UI components: `ExampleNotesPage.razor`, `ExampleNoteForm.razor`, `ExampleNoteDisplay.razor`
- ✓ `DotNetCloud.Modules.Example.Data` project (EF Core):
  - ✓ `ExampleDbContext` with `DbSet<ExampleNote>`
  - ✓ `ExampleNoteConfiguration` entity type configuration (fluent API, indexes, constraints)
- ✓ `DotNetCloud.Modules.Example.Host` project (gRPC host):
  - ✓ `example_service.proto` defining CreateNote, GetNote, ListNotes, DeleteNote RPCs
  - ✓ `ExampleGrpcService` implementing module-specific gRPC CRUD operations
  - ✓ `ExampleLifecycleService` implementing `ModuleLifecycle.ModuleLifecycleBase` (Initialize, Start, Stop, HealthCheck, GetManifest)
  - ✓ `ExampleHealthCheck` implementing `IHealthCheck`
  - ✓ `Program.cs` entry point with gRPC, health check, and DI configuration
- ✓ `manifest.json` for filesystem module discovery
- ✓ Module-specific `README.md` with project structure, key concepts, and creation guide
- ✓ All 3 projects added to `DotNetCloud.sln`
- ✓ `DotNetCloud.Modules.Example.Tests` project (MSTest, Moq):
  - ✓ `ExampleModuleManifestTests` — 10 tests (Id, Name, Version, capabilities, events, IModuleManifest)
  - ✓ `ExampleModuleTests` — 22 tests (lifecycle, notes CRUD, event pub/sub, error states)
  - ✓ `ExampleNoteTests` — 10 tests (Id generation, defaults, record semantics)
  - ✓ `EventTests` — 5 tests (NoteCreatedEvent, NoteDeletedEvent, IEvent, record semantics)
  - ✓ `NoteCreatedEventHandlerTests` — 4 tests (IEventHandler interface, logging, cancellation)

**File Locations:**
- `src/Modules/Example/DotNetCloud.Modules.Example/DotNetCloud.Modules.Example.csproj` — core logic project
- `src/Modules/Example/DotNetCloud.Modules.Example/ExampleModuleManifest.cs` — module manifest
- `src/Modules/Example/DotNetCloud.Modules.Example/ExampleModule.cs` — IModuleLifecycle implementation
- `src/Modules/Example/DotNetCloud.Modules.Example/Models/ExampleNote.cs` — domain model
- `src/Modules/Example/DotNetCloud.Modules.Example/Events/NoteCreatedEvent.cs` — domain event
- `src/Modules/Example/DotNetCloud.Modules.Example/Events/NoteDeletedEvent.cs` — domain event
- `src/Modules/Example/DotNetCloud.Modules.Example/Events/NoteCreatedEventHandler.cs` — event handler
- `src/Modules/Example/DotNetCloud.Modules.Example/UI/ExampleNotesPage.razor` — notes page component
- `src/Modules/Example/DotNetCloud.Modules.Example/UI/ExampleNoteForm.razor` — form component
- `src/Modules/Example/DotNetCloud.Modules.Example/UI/ExampleNoteDisplay.razor` — display component
- `src/Modules/Example/DotNetCloud.Modules.Example.Data/DotNetCloud.Modules.Example.Data.csproj` — data project
- `src/Modules/Example/DotNetCloud.Modules.Example.Data/ExampleDbContext.cs` — module DbContext
- `src/Modules/Example/DotNetCloud.Modules.Example.Data/Configuration/ExampleNoteConfiguration.cs` — EF config
- `src/Modules/Example/DotNetCloud.Modules.Example.Host/DotNetCloud.Modules.Example.Host.csproj` — host project
- `src/Modules/Example/DotNetCloud.Modules.Example.Host/Protos/example_service.proto` — gRPC contract
- `src/Modules/Example/DotNetCloud.Modules.Example.Host/Services/ExampleGrpcService.cs` — gRPC service
- `src/Modules/Example/DotNetCloud.Modules.Example.Host/Services/ExampleLifecycleService.cs` — lifecycle gRPC
- `src/Modules/Example/DotNetCloud.Modules.Example.Host/Services/ExampleHealthCheck.cs` — health check
- `src/Modules/Example/DotNetCloud.Modules.Example.Host/Program.cs` — host entry point
- `src/Modules/Example/manifest.json` — filesystem manifest
- `src/Modules/Example/README.md` — module documentation
- `tests/DotNetCloud.Modules.Example.Tests/DotNetCloud.Modules.Example.Tests.csproj` — test project
- `tests/DotNetCloud.Modules.Example.Tests/ExampleModuleManifestTests.cs` — manifest tests
- `tests/DotNetCloud.Modules.Example.Tests/ExampleModuleTests.cs` — module lifecycle tests
- `tests/DotNetCloud.Modules.Example.Tests/ExampleNoteTests.cs` — model tests
- `tests/DotNetCloud.Modules.Example.Tests/EventTests.cs` — event tests
- `tests/DotNetCloud.Modules.Example.Tests/NoteCreatedEventHandlerTests.cs` — handler tests

**Build Status:** ✅ Full solution builds with zero errors, zero warnings (20 projects)
**Testing:** ✅ 656/656 tests pass (605 existing + 51 new Example module tests)
**Notes:** Module demonstrates all key integration points: IModuleLifecycle, IModuleManifest, IEvent/IEventHandler, IEventBus pub/sub, gRPC ModuleLifecycle service, module-owned DbContext (separate from CoreDbContext), and Blazor Razor components loaded via module plugin system. Host uses in-memory database for standalone development. The manifest.json enables filesystem-based module discovery by the core supervisor. Fixed ExampleLifecycleService to use CallerContext.CreateSystemContext() instead of direct constructor with Guid.Empty.

---

### Phase 0.15: Testing Infrastructure

#### Step: phase-0.15.1 - Unit Test Infrastructure
**Status:** completed ✅
**Description:** Core unit test projects and helpers (already delivered during Phases 0.1–0.14).

**Deliverables:**
- ✓ `DotNetCloud.Core.Tests` project (MSTest, Moq)
- ✓ 108 test cases across 6 test classes (CapabilityTier, EventBus, CallerContext, Module system)
- ✓ Fake implementations & Moq-based helpers

**Notes:** Pre-existing — each phase delivered its own unit tests alongside the production code.

#### Step: phase-0.15.2 - Integration Test Project & Test Data Builders
**Status:** completed ✅
**Duration:** ~30 minutes
**Description:** Create the `DotNetCloud.Integration.Tests` project skeleton, MSTest configuration, and fluent test-data builders.

**Deliverables:**
- ✓ `DotNetCloud.Integration.Tests.csproj` with MSTest, Moq, Microsoft.AspNetCore.Mvc.Testing, Grpc.Net.Client, EF Core InMemory
- ✓ `MSTestSettings.cs` (parallelism configuration)
- ✓ `Builders/ApplicationUserBuilder.cs` — fluent builder for `ApplicationUser`
- ✓ `Builders/OrganizationBuilder.cs` — fluent builder for `Organization`
- ✓ `Builders/TeamBuilder.cs` — fluent builder for `Team`
- ✓ `Builders/RegisterRequestBuilder.cs` — fluent builder for `RegisterRequest` DTO
- ✓ `Builders/CallerContextBuilder.cs` — fluent builder for `CallerContext`

#### Step: phase-0.15.3 - Database Test Infrastructure
**Status:** completed ✅
**Duration:** ~20 minutes
**Description:** Docker-based database container fixture, in-memory seeder, and container configuration.

**Deliverables:**
- ✓ `Infrastructure/DatabaseContainerConfig.cs` — Docker container configuration model
- ✓ `Infrastructure/DatabaseContainerFixture.cs` — Docker lifecycle management (start, health-wait, stop)
- ✓ `Infrastructure/DatabaseSeeder.cs` — in-memory CoreDbContext factory + default seed data (Identity roles, permissions, settings, organization)

#### Step: phase-0.15.4 - Program.cs Class-Based Conversion
**Status:** completed ✅
**Duration:** ~10 minutes
**Description:** Convert `Program.cs` from top-level statements to a class with `Main`, `ConfigureServices`, and `ConfigurePipeline` methods for `WebApplicationFactory<Program>` compatibility.

**Deliverables:**
- ✓ `DotNetCloud.Core.Server.Program` class with `Main(string[] args)` entry point
- ✓ `ConfigureServices(WebApplicationBuilder)` — separated service registration
- ✓ `ConfigurePipeline(WebApplication)` — separated middleware pipeline
- ✓ No `InternalsVisibleTo` hack needed

#### Step: phase-0.15.5 - WebApplicationFactory & API Assertion Helpers
**Status:** completed ✅
**Duration:** ~30 minutes
**Description:** Custom `WebApplicationFactory<Program>` with InMemory database, stubbed `IProcessSupervisor`, Swashbuckle application-part removal, and API response assertion utilities.

**Deliverables:**
- ✓ `Infrastructure/DotNetCloudWebApplicationFactory.cs`:
  - ✓ Replaces `DbContextOptions<CoreDbContext>` with InMemory provider (avoids dual-provider conflict)
  - ✓ Removes Swashbuckle `ApplicationParts` to prevent `ReflectionTypeLoadException` (OpenApi v2 mismatch)
  - ✓ Stubs `IProcessSupervisor` via Moq
  - ✓ Provides dummy connection string via in-memory configuration
  - ✓ Inner `InMemoryDbContextFactory` for `IDbContextFactory` consumers
- ✓ `Infrastructure/ApiAssert.cs` — `SuccessAsync`, `ErrorAsync`, `StatusCode`, `ReadAsAsync<T>`, `DataAsync<T>`

#### Step: phase-0.15.6 - gRPC Client Test Helpers
**Status:** completed ✅
**Duration:** ~10 minutes
**Description:** Factory methods for creating typed gRPC clients connected to the test server.

**Deliverables:**
- ✓ `Infrastructure/GrpcTestClientFactory.cs`:
  - ✓ `CreateLifecycleClient` — `ModuleLifecycle.ModuleLifecycleClient`
  - ✓ `CreateCapabilitiesClient` — `CoreCapabilities.CoreCapabilitiesClient`
  - ✓ `CreateModuleCaller` / `CreateSystemCaller` — `CallerContextMessage` helpers

#### Step: phase-0.15.7 - Multi-Database Matrix Tests
**Status:** completed ✅
**Duration:** ~20 minutes
**Description:** Integration tests verifying consistent behavior across PostgreSQL, SQL Server, and MariaDB naming strategies using InMemory database.

**Deliverables:**
- ✓ `Database/MultiDatabaseMatrixTests.cs` — 21 tests:
  - ✓ `Context_CreatesSuccessfully_ForEachProvider` (3 providers)
  - ✓ `Schema_EntityTypeCount_IsConsistentAcrossProviders`
  - ✓ `Schema_EntityNames_AreConsistentAcrossProviders`
  - ✓ `Crud_Organization_WorksForEachProvider` (3 providers, including soft-delete)
  - ✓ `Crud_User_WorksForEachProvider` (3 providers)
  - ✓ `Crud_SystemSetting_WorksForEachProvider` (3 providers)
  - ✓ `Crud_Permission_WorksForEachProvider` (3 providers)
  - ✓ `ProviderDetection_PostgreSQL/SqlServer/MariaDB_IsDetected`
  - ✓ `NamingStrategy_GetNamingStrategy_ReturnsCorrectType`

#### Step: phase-0.15.8 - API Integration Tests
**Status:** completed ✅
**Duration:** ~20 minutes
**Description:** Full-stack API tests via `WebApplicationFactory` covering health endpoints and authentication flows.

**Deliverables:**
- ✓ `Api/HealthEndpointTests.cs` — 3 tests:
  - ✓ `Health_ReturnsOk` (`/health`)
  - ✓ `HealthReady_ReturnsOk` (`/health/ready`)
  - ✓ `HealthLive_ReturnsOk` (`/health/live`)
- ✓ `Api/AuthEndpointTests.cs` — 8 tests:
  - ✓ `Register_ValidRequest_ReturnsOk`
  - ✓ `Register_DuplicateEmail_ReturnsBadRequest`
  - ✓ `Register_WeakPassword_ReturnsBadRequest`
  - ✓ `Login_ValidCredentials_ReturnsOk`
  - ✓ `Login_InvalidCredentials_ReturnsUnauthorized`
  - ✓ `Logout_WithoutAuth_ReturnsUnauthorizedOrRedirect`
  - ✓ `GetCurrentUser_WithoutAuth_ReturnsUnauthorizedOrRedirect`
  - ✓ `ForgotPassword_ValidEmail_ReturnsOk`

#### Step: phase-0.15.9 - CallerContext.CreateSystemContext Bug Fix
**Status:** completed ✅
**Duration:** ~10 minutes
**Description:** Fixed pre-existing bug where `CallerContext.Validate()` rejected `Guid.Empty` unconditionally, preventing `CreateSystemContext()` from working. Updated `Validate` to accept `CallerType` and allow `Guid.Empty` for System callers.

**Deliverables:**
- ✓ `CallerContext.Validate(Guid, IReadOnlyList<string>?, CallerType)` — now allows `Guid.Empty` for `CallerType.System`
- ✓ `CallerContextTests.CreateSystemContext_CreatesContextWithEmptyUserId` — replaced throw-expecting test
- ✓ `ModuleInterfaceTests` — 3 workaround sites replaced with `CallerContext.CreateSystemContext()`
- ✓ `AuthController.BuildCallerContext()` — returns `CallerContext.CreateSystemContext()` for anonymous callers

**Build Status:** ✅ Full solution builds with zero errors, zero warnings (20 projects including Integration.Tests)
**Testing:** ✅ 688/688 tests pass across 7 test projects (32 new integration tests)
**Notes:** Integration testing required multiple infrastructure fixes: Swashbuckle OpenApi v2 `ReflectionTypeLoadException` (removed application parts), Npgsql/InMemory dual-provider conflict (replaced `DbContextOptions` only, not `AddDbContext`), `CallerContext.CreateSystemContext()` bug (Validate now type-aware). Program.cs converted to class-based at user request for cleaner WebApplicationFactory usage.

#### Step: phase-0.15.10 - Docker-Based Database Integration Tests
**Status:** completed ✅
**Duration:** ~4 hours (includes Docker/WSL setup and debugging)
**Description:** Real database integration tests that start PostgreSQL containers via `DatabaseContainerFixture` with WSL 2 Docker support, and connect to local SQL Server Express via Windows Authentication. SQL Server tests prefer a local instance (shared memory) and fall back to Docker containers. MariaDB skipped (Pomelo lacks .NET 10 support).

**Deliverables:**
- ✓ `tools/setup-docker-wsl.sh` — Docker Engine installer for WSL (Linux Mint 22 / Ubuntu 24.04)
- ✓ `.gitattributes` — LF line ending enforcement for shell scripts
- ✓ `DatabaseContainerFixture` rewritten with WSL auto-detection:
  - ✓ Tries native `docker` first, falls back to `wsl docker` automatically
  - ✓ Container crash detection via `docker ps -q --filter id=`
  - ✓ Host-side TCP port verification (`VerifyHostPortAsync`)
  - ✓ Explicit `docker stop` + `docker rm` cleanup (no `--rm` flag — causes crashes on WSL2)
- ✓ `LocalSqlServerDetector` — probes local SQL Server Express via shared memory (`Data Source=.`):
  - ✓ Windows-only detection (Windows Authentication)
  - ✓ Isolated test database creation per session (`dotnetcloud_test_YYYYMMDD_HHmmss`)
  - ✓ Automatic cleanup on test teardown (DROP DATABASE)
  - ✓ Result cached for process lifetime
- ✓ `ApplicationUserConfiguration` fix: `GETUTCDATE()` → `CURRENT_TIMESTAMP` (cross-database)
- ✓ `DatabaseContainerConfig.SqlServer()` fix: double quotes instead of single quotes in health check
- ✓ Cross-database fixes:
  - ✓ `OrganizationMemberConfiguration` / `TeamMemberConfiguration`: removed hard-coded `HasColumnType("jsonb")` (PostgreSQL-specific)
  - ✓ `CoreDbContext.ApplyJsonColumnTypes()`: provider-aware JSON column types (`jsonb` → PostgreSQL, `nvarchar(max)` → SQL Server, `longtext` → MariaDB)
  - ✓ Membership FK cascade → `Restrict` for `OrganizationMember`, `TeamMember`, `GroupMember` User FKs (SQL Server rejects multiple cascade paths)
- ✓ `Database/DockerDatabaseIntegrationTests.cs` — 12 tests:
  - ✓ `PostgreSql_EnsureCreated_Succeeds` — **passes against real PostgreSQL 16**
  - ✓ `PostgreSql_Crud_Organization` — **passes** (create, read, update, soft-delete)
  - ✓ `PostgreSql_Crud_User` — **passes**
  - ✓ `PostgreSql_Crud_SystemSetting` — **passes**
  - ✓ `PostgreSql_Crud_Permission` — **passes**
  - ✓ `PostgreSql_Seed_DefaultData` — **passes**
  - ✓ `SqlServer_EnsureCreated_Succeeds` — **passes against local SQL Server Express**
  - ✓ `SqlServer_Crud_Organization` — **passes**
  - ✓ `SqlServer_Crud_User` — **passes**
  - ✓ `SqlServer_Crud_SystemSetting` — **passes**
  - ✓ `SqlServer_Crud_Permission` — **passes**
  - ✓ `SqlServer_Seed_DefaultData` — **passes**
- ✓ `EnsureCreatedOrSkipAsync` helper — catches container crashes as `Assert.Inconclusive`
- ✓ Concurrent fixture startup (`Task.WhenAll`) — prevents WSL idle timeout
- ✓ Seed test assertions updated for test-order independence

**Notes:** Docker Engine 29.2.1 installed in WSL 2 (Linux Mint 22). PostgreSQL 16 containers work perfectly. SQL Server Docker containers crash on WSL2 kernel 6.6.87.2; resolved by using local SQL Server Express (Windows Auth, shared memory protocol). All 12 database integration tests now pass: 6 PostgreSQL (Docker) + 6 SQL Server (local). Total: 803 tests pass across 7 test projects.

---

### Phase 0.16: Internationalization (i18n) Infrastructure

#### Step: phase-0.16.1 - i18n Infrastructure Setup
**Status:** completed ✅
**Duration:** ~2 hours
**Description:** Full internationalization infrastructure for Blazor Web App with InteractiveAuto render mode. Supports both server-side (cookie-based) and client-side (localStorage-based) culture persistence.

**Deliverables:**
- ✓ `SupportedCultures.cs` — centralized culture registry with 7 cultures (en-US, es-ES, de-DE, fr-FR, pt-BR, ja-JP, zh-CN)
- ✓ `TranslationKeys.cs` — constant classes for Common, Auth, Errors, Validation, Admin string keys
- ✓ `SharedResources.cs` — marker class for `IStringLocalizer<SharedResources>`
- ✓ `SharedResources.resx` — default English strings (50+ entries: UI, auth, admin, errors, validation)
- ✓ `SharedResources.es.resx` — Spanish translations (all entries)
- ✓ `CultureSelector.razor` — Blazor component with dual persistence (localStorage + cookie redirect)
- ✓ `CultureController.cs` — ASP.NET Core controller for localization cookie via redirect
- ✓ Server-side: `AddLocalization()`, `UseRequestLocalization` with `SupportedCultures` config
- ✓ Client-side (WASM): `AddLocalization()`, JS interop culture read from localStorage, `BlazorWebAssemblyLoadAllGlobalizationData`
- ✓ `App.razor` — dynamic `html lang` attribute, `blazorCulture` JS interop, cookie persistence via `CookieRequestCultureProvider`
- ✓ `MainLayout.razor` — CultureSelector integrated in topbar with `InteractiveAuto` render mode
- ✓ All `_Imports.razor` files updated with `Microsoft.Extensions.Localization`, `DotNetCloud.Core.Localization`, `DotNetCloud.UI.Shared.Resources`
- ✓ `Microsoft.Extensions.Localization` package added to `DotNetCloud.UI.Shared` and `DotNetCloud.UI.Web.Client`
- ✓ `DotNetCloud.UI.Shared` → `DotNetCloud.Core` project reference added
- ✓ `docs/architecture/internationalization.md` — comprehensive i18n guide
- ✓ `SupportedCulturesTests` — 11 tests (DefaultCulture, All array, DisplayNames, GetCultureInfos, BCP-47 validation)
- ✓ `TranslationKeysTests` — 13 tests (nested class structure, non-empty constants, global uniqueness, expected key values)
- ✓ `CultureControllerTests` — 15 tests (cookie setting, redirect behavior, empty/null guards, all supported cultures)
- ☐ Weblate translation workflow (deferred to later phase)

**Notes:** Full i18n infrastructure in place with 45 unit tests. Culture selection works for both SSR and CSR via dual persistence (cookie + localStorage). Spanish translation included as reference. Additional languages can be added by creating `.resx` files and registering in `SupportedCultures`. Weblate integration deferred. All 739 tests pass (0 failures, 6 skipped SQL Server Docker tests).

---

### Phase 0.17: Logging & Observability

#### Step: phase-0.17.1 - Logging & Observability Implementation
**Status:** completed ✅
**Duration:** ~2 hours
**Description:** Comprehensive observability infrastructure ensuring all logging, health checks, metrics, and tracing components are properly configured, tested, and documented across the entire platform.

**Deliverables:**

**Health Check Enhancements:**
- ✓ `StartupHealthCheck` — readiness probe that reports Unhealthy until `MarkReady()` is called after initialization
- ✓ Tag-based health check endpoint filtering:
  - ✓ `/health` — full report (all registered checks)
  - ✓ `/health/live` — liveness probe (only `live`-tagged checks, no external deps)
  - ✓ `/health/ready` — readiness probe (`ready` + `database` + `module`-tagged checks)
- ✓ JSON response writer for all health endpoints (status, duration, description, exception, data per entry)
- ✓ `self` check (always healthy) registered with `live` tag
- ✓ `startup` check registered with `ready` tag

**Prometheus Metrics Exporter:**
- ✓ `OpenTelemetry.Exporter.Prometheus.AspNetCore` 1.15.0-beta.1 package added
- ✓ `EnablePrometheusExporter` option added to `TelemetryOptions` (default: false, opt-in)
- ✓ `MapDotNetCloudPrometheus()` extension method — maps `/metrics` endpoint when enabled
- ✓ Prometheus exporter wired into metrics pipeline in `ConfigureMetricsExporters`
- ✓ `/metrics` endpoint mapped in `Program.cs` pipeline

**Serilog Configuration (validated existing infrastructure):**
- ✓ Serilog configured in `ServiceDefaultsExtensions.AddDotNetCloudServiceDefaults(WebApplicationBuilder)` via `UseDotNetCloudSerilog()`
- ✓ Console sink (development: colored structured output; production: plain structured)
- ✓ File sink (daily rolling, 31-day retention, 100MB per file, shared mode)
- ✓ Log levels: Debug, Information, Warning, Error, Fatal (all supported)
- ✓ Context enrichment: UserId, RequestId, ModuleName, OperationName, CallerContext via `LogEnricher`
- ✓ Module-level filtering via `ModuleLogFilter` (exclusion + per-module levels)
- ✓ Machine name, environment, process ID, thread ID auto-enrichment

**appsettings Configuration:**
- ✓ `appsettings.json` — Serilog section (file path, rotation, retention, structured format, module log levels)
- ✓ `appsettings.json` — Telemetry section expanded (ServiceName, ServiceVersion, Prometheus, OTLP, additional sources/meters)
- ✓ `appsettings.Development.json` — Serilog section (Debug level, 7-day retention, dev file path)
- ✓ `appsettings.Development.json` — Telemetry section (console exporter off by default, Prometheus off)

**Unit Tests (58 tests, all passing):**
- ✓ `SerilogConfigurationTests` — 11 tests (defaults, log levels, file rotation, retention, modules)
- ✓ `ModuleLogFilterTests` — 9 tests (exclusion, module levels, precedence, null params)
- ✓ `LogEnricherTests` — 10 tests (property push/pop via CollectorSink, CallerContext, dispose cleanup)
- ✓ `TelemetryConfigurationTests` — 14 tests (options defaults, activity sources, Prometheus, OTLP)
- ✓ `HealthCheckTests` — 14 tests (StartupHealthCheck lifecycle, ModuleHealthCheckResult factories, adapter mapping, exception handling, enum values)

**Documentation:**
- ✓ `docs/architecture/observability.md` — comprehensive observability guide (logging, metrics, tracing, health checks, Kubernetes probes, configuration reference, architecture diagram)

**Notes:** All observability infrastructure was already implemented in Phase 0.3 (Serilog, OpenTelemetry, health checks). Phase 0.17 enhanced the health check endpoints with proper tag-based liveness/readiness filtering, added the Prometheus metrics exporter (opt-in), added comprehensive appsettings configuration, created 58 unit tests covering all observability components, and documented the full observability architecture. All 797 tests pass (6 SQL Server Docker tests skipped as expected).

---

### Phase 0.18: CI/CD Pipeline Setup

#### Step: phase-0.18.1 - CI/CD Pipeline Setup
**Status:** completed ✅
**Duration:** ~2 hours
**Description:** Complete CI/CD pipeline infrastructure with build, test, multi-database integration, code coverage, Docker containerization, and packaging script skeletons for all target platforms.

**Deliverables:**

**CI/CD Workflows (GitHub Actions + Gitea Actions):**
- ✓ `.github/workflows/build-test.yml` — GitHub Actions CI workflow
- ✓ `.gitea/workflows/build-test.yml` — Gitea Actions CI workflow (mirrored)
- ✓ **Build job:** restore, compile (Release), publish Core Server + CLI, upload artifacts (7-day retention)
- ✓ **Unit test job:** MSTest with TRX logging, coverlet XPlat Code Coverage (Cobertura), exclude test projects + migrations
- ✓ **Integration test job:** multi-database matrix (PostgreSQL 16, SQL Server 2022) via service containers
- ✓ NuGet package caching (keyed by `.csproj` + `Directory.Build.props` hash)
- ✓ Concurrency groups with cancel-in-progress for PR builds

**Docker Containerization:**
- ✓ `Dockerfile` — multi-stage build (restore → build → publish → runtime)
  - ✓ .NET 10 SDK/ASP.NET base images
  - ✓ Layer-cached NuGet restore (copy `.csproj` files first)
  - ✓ Non-root user (`dotnetcloud:1000`) for security
  - ✓ Health check via `curl` on `/health/live`
  - ✓ Data/logs/modules volume directories
- ✓ `docker-compose.yml` — local development & deployment
  - ✓ Core Server service with PostgreSQL dependency
  - ✓ PostgreSQL 16 Alpine with health check
  - ✓ SQL Server 2022 optional profile (`--profile sqlserver`)
  - ✓ Named volumes for data, logs, modules, database storage
- ✓ `.dockerignore` — exclude Git, IDE, build output, docs, CI/CD, test results

**Packaging Scripts (Skeletons):**
- ✓ `tools/packaging/build-deb.ps1` — Debian package skeleton (publish, DEBIAN/control, directory structure)
- ✓ `tools/packaging/build-rpm.ps1` — RPM package skeleton (publish, .spec file, rpmbuild structure)
- ✓ `tools/packaging/build-msi.ps1` — Windows MSI skeleton (publish win-x64, WiX v4 placeholder)
- ✓ `tools/packaging/build-docker.ps1` — Docker image build script (functional: build, tag, optional push)
- ✓ `tools/packaging/build-desktop-client-bundles.ps1` — desktop client bundle builder with CMD-first Windows ZIP installer/uninstaller (`install.cmd`, `uninstall.cmd`) for script-policy-restricted machines
- ✓ `tools/packaging/build-desktop-client-msix.ps1` — Windows SyncTray MSIX builder (win-x64 publish, manifest + assets generation, makeappx pack, optional signing)

**Notes:** Full CI/CD pipeline in place. Both GitHub Actions and Gitea Actions workflows are functionally identical, covering build, unit tests with coverage, and multi-database integration tests. Docker multi-stage build produces a minimal runtime image with non-root security. Packaging scripts provide the skeleton for `.deb`, `.rpm`, and MSI builds to be fleshed out in later infrastructure phases, now include a dedicated desktop `SyncTray` MSIX packaging script for Windows distribution, and include a CMD-first Windows ZIP installer path that avoids PowerShell execution-policy issues on development/testing machines. Status badge documentation deferred. All existing tests continue to pass. Build verified successful.

---

## Phase 0.19: Documentation

#### Step: phase-0.19 - Documentation
**Status:** completed ✅
**Duration:** ~3 hours
**Description:** Comprehensive documentation for Phase 0 covering architecture, development setup, API reference, authentication flows, response formats, error handling, and module development guide.

**Deliverables:**

**Core Documentation (6 items — all previously existing):**
- ✓ Architecture overview documentation (`docs/architecture/ARCHITECTURE.md`)
- ✓ Development environment setup guide (`docs/development/README.md`, `IDE_SETUP.md`, `DATABASE_SETUP.md`, `DOCKER_SETUP.md`)
- ✓ Bare-metal server installation and fast redeploy runbook (`docs/admin/server/INSTALLATION.md`)
- ✓ One-command bare-metal redeploy helper script (`tools/redeploy-baremetal.sh`) with publish + restart + health verification
- ✓ Local-server workflow guidance: use source redeploy helper during local development and maintain `tools/install.sh` parity for first-install/upgrade on other machines
- ✓ Redeploy helper endpoint parity: auto-probe both local HTTPS `:15443` and installer-default HTTP `:5080` liveness endpoints
- ✓ Repository commit template (`.gitmessage`) and updated `CONTRIBUTING.md` commit workflow guidance for detailed AI-assisted commit messages
- ✓ README developer quick setup note for commit-template activation (`git config commit.template .gitmessage`)
- ✓ Running tests documentation (`docs/development/RUNNING_TESTS.md` — **new**)
- ✓ Contributing guidelines (`CONTRIBUTING.md`)
- ✓ License documentation (`LICENSE` — AGPL-3.0)

**API Documentation (4 items — all new):**
- ✓ API endpoint reference (`docs/api/README.md`) — complete endpoint table with request/response examples for auth, MFA, devices, users, admin, health, OIDC, SignalR
- ✓ Authentication flow documentation (`docs/api/AUTHENTICATION.md`) — architecture, flows by client type, registration, login, MFA, tokens, external providers, password management, authorization
- ✓ Response format documentation (`docs/api/RESPONSE_FORMAT.md`) — standard envelope, pagination, error responses, middleware configuration, special cases
- ✓ Error handling documentation (`docs/api/ERROR_HANDLING.md`) — complete error code reference, exception mapping, global exception handler, validation, dev vs prod

**Module Development Guide Skeleton (4 items — all new):**
- ✓ Module architecture overview (`docs/guides/MODULE_DEVELOPMENT.md`)
- ✓ Creating a module (`docs/guides/MODULE_DEVELOPMENT.md`)
- ✓ Module manifest documentation (`docs/guides/MODULE_DEVELOPMENT.md`)
- ✓ Capability interfaces documentation (`docs/architecture/core-abstractions.md`, `docs/guides/MODULE_DEVELOPMENT.md`)

**Notes:** All Phase 0.19 documentation complete. Core documentation now includes an explicit fast bare-metal redeploy runbook in `docs/admin/server/INSTALLATION.md` and a one-command helper script `tools/redeploy-baremetal.sh` (publish, restart, service-unit verification, and health checks). Local-development guidance now explicitly prefers source-based redeploys over re-running GitHub installer fetches, while requiring `tools/install.sh` parity whenever bare-metal process steps change so fresh-machine installs/upgrades remain consistent. Health probing parity is now explicit: the helper auto-tries both local HTTPS (`https://localhost:15443/health/live`) and installer-default HTTP (`http://localhost:5080/health/live`) endpoints. Commit quality guidance is also standardized via repository `.gitmessage`, expanded `CONTRIBUTING.md` instructions (template setup + AI workflow for detailed subject/body output), and a README quick-setup note so contributors can enable the template immediately. Script validation succeeded locally against `dotnetcloud.service` and returned healthy liveness output. 6 new files created: `RUNNING_TESTS.md`, `docs/api/README.md`, `AUTHENTICATION.md`, `RESPONSE_FORMAT.md`, `ERROR_HANDLING.md`, `docs/guides/MODULE_DEVELOPMENT.md`. Phase 0 documentation is now comprehensive. Ready for Phase 0 completion verification.

---

## Status Summary & Notes

### Phase 0 Completion Verification (2026-03-04)

**Build:** ✓ All 20 projects compile — 0 errors, 0 warnings
**Tests:** ✓ 797 passed, 0 failed, 6 skipped (SQL Server Docker on WSL2)
- Core.Tests: 138 | CLI.Tests: 66 | Example.Tests: 51 | Core.Data.Tests: 176
- Core.Auth.Tests: 69 | Integration.Tests: 38+6 skipped | Core.Server.Tests: 259

**Remaining ☐ items (3 total):**
1. ☐ MariaDB integration tests — Pomelo EF Core provider lacks .NET 10 support
2. ☐ Docker runtime health checks — requires Docker daemon (files are present)
3. ☐ Kubernetes deployment — Helm chart not yet created

**All other Phase 0 checklist items verified ✓** — see `docs/IMPLEMENTATION_CHECKLIST.md` Phase 0 Completion Checklist for full evidence annotations.

- **Total Phase 0 Steps:** 229+ (across subsections 0.1-0.19)
- **Estimated Duration:** 16-20 weeks for complete Phase 0
- **Critical Path:** 0.1 → 0.2 → 0.3 → 0.4 → (0.5-0.19 can parallelize somewhat)
- **Blocking Issues:** MariaDB (Pomelo .NET 10 support pending)
- **Assumptions:** .NET 10, PostgreSQL/SQL Server/MariaDB support required
- **Reference:** Complete detailed task breakdowns in `/docs/IMPLEMENTATION_CHECKLIST.md`

---

## Phase 1: Files (Public Launch)

**Goal:** File upload/download/browse/share + working desktop sync client.
**Expected Duration:** 8-12 weeks
**Milestone:** Full file management across web, desktop, with sync, sharing, and Collabora integration.

---

### Step: phase-1.1 - Files Core Abstractions & Data Models
**Status:** completed ✅
**Duration:** ~1 week (actual)
**Description:** Create Files module projects, domain models (FileNode, FileVersion, FileChunk, FileShare, FileTag, FileComment, FileQuota, ChunkedUploadSession, FileVersionChunk), DTOs, events, and FilesModuleManifest.

**Deliverables:**
- ✓ Create project structure (Files, Files.Data, Files.Host, Files.Tests) — 4 projects added to solution
- ✓ Create FilesModuleManifest implementing IModuleManifest
- ✓ Create domain models (FileNode, FileVersion, FileChunk, FileShare, FileTag, FileComment, FileQuota, ChunkedUploadSession, FileVersionChunk) — 9 entities
- ✓ Create enums (FileNodeType, ShareType, SharePermission, UploadSessionStatus) — 4 enums
- ✓ Create DTOs for all entities (FileNodeDto, FileVersionDto, FileShareDto, etc.)
- ✓ Create events (FileUploadedEvent, FileMovedEvent, FileDeletedEvent, FileSharedEvent, FileRestoredEvent) — 5 events

**Dependencies:** Phase 0 (complete)
**Blocking Issues:** None
**Notes:** Phase 1.1 complete. All models, DTOs, events, and manifest follow core module patterns.

---

### Step: phase-1.2 - Files Database & Data Access Layer
**Status:** completed ✅
**Duration:** ~1 week (actual)
**Description:** Create FilesDbContext, entity configurations, IFileStorageEngine/LocalFileStorageEngine, ContentHasher, and database initialization.

**Deliverables:**
- ✓ Create entity configurations for all 9 entities with indexes, FKs, query filters
- ✓ Create FilesDbContext with all DbSets and naming strategy
- ✓ Create IFileStorageEngine interface and LocalFileStorageEngine implementation
- ✓ Create ContentHasher (SHA-256)
- ✓ Create FilesDbInitializer

**Dependencies:** phase-1.1
**Blocking Issues:** None
**Notes:** Phase 1.2 complete. Soft-delete query filters on FileNode and FileComment. Materialized path indexing for tree queries. Content-addressable chunk storage with SHA-256 hashing.

---

### Step: phase-1.3 - Files Business Logic & Services
**Status:** completed ✅
**Duration:** ~2 weeks (actual)
**Description:** Implement 9 service interfaces with implementations, 3 background services, and DI registration for the Files module business logic layer.

**Deliverables:**
- ✓ Create PagedResult<T> generic DTO and FilesErrorCodes constants
- ✓ Implement IFileService and FileService (tree ops, authorization, materialized path updates, soft-delete, copy, search, favorites)
- ✓ Implement IChunkedUploadService and ChunkedUploadService (dedup via chunk hash lookup, quota pre-check, hash verification, version creation)
- ✓ Implement IDownloadService and DownloadService (ConcatenatedStream for lazy chunk reassembly)
- ✓ Implement IVersionService and VersionService (version history, restore creates new version, refcount management)
- ✓ Implement IShareService and ShareService (user/team/public-link sharing, crypto tokens, password hashing, expiry/download limits)
- ✓ Implement ITrashService and TrashService (restore to original parent or root, cascading permanent delete, chunk GC)
- ✓ Implement IQuotaService and QuotaService (storage quota CRUD, recalculation)
- ✓ Implement ITagService and TagService (tag CRUD, duplicate prevention)
- ✓ Implement ICommentService and CommentService (threaded comments, soft-delete, reply counts)
- ✓ Create UploadSessionCleanupService (1h interval, expire stale sessions)
- ✓ Create TrashCleanupService (6h interval, purge >30d trash, GC unreferenced chunks)
- ✓ Create QuotaRecalculationService (24h interval, per-user recalculation)
- ✓ Create FilesServiceRegistration (DI wiring: 9 scoped services + 3 hosted background services)
- ✓ 298 unit tests passing (9 test files covering all services)

**Dependencies:** phase-1.2
**Blocking Issues:** None
**Notes:** Phase 1.3 complete. All 9 services are `internal sealed class` with `InternalsVisibleTo` for test access. Services follow CallerContext authorization pattern (owner-or-system checks). FileService enforces MaxDepth=50 and name uniqueness within parent. ShareService uses RandomNumberGenerator for link tokens and ASP.NET Identity PasswordHasher for link passwords. TrashService cascades permanent delete through shares→tags→comments→versions→chunks→node with refcount management. 850 total solution tests pass (no regressions). Some items deferred: range request downloads, version retention limits, notification integration.

---

### Step: phase-1.4 - Files REST API Endpoints
**Status:** completed ✅
**Duration:** ~1-2 weeks
**Description:** Create REST controllers for file/folder CRUD, upload/download, sharing, versioning, trash, tags, comments, and search.

**Deliverables:**
- ✓ Create FilesController (CRUD, tree navigation, search, favorites, recent, upload, download, chunk manifest, shared-with-me, public links)
- ✓ Core-server Files route exposure hardening — added `src/Core/DotNetCloud.Core.Server/Controllers/FilesController.cs` and `FilesControllerBase.cs` so `/api/v1/files/*` resolves in bare-metal single-process installs
- ✓ Create VersionController (list, get by number, restore, delete, label)
- ✓ Create ShareController (list, create, update, delete)
- ✓ Create TrashController (list, restore, permanent delete, empty, size)
- ✓ Create QuotaController (get, set, recalculate)
- ✓ Create TagController (add, remove by name, list all, list by tag)
- ✓ Create CommentController (add, list, edit, delete)
- ✓ Create BulkController (move, copy, delete, permanent-delete)
- ✓ Create SyncController (changes, tree, reconcile)
- ✓ Create FilesControllerBase (envelope pattern, exception-to-HTTP mapping)
- ✓ Create InProcessEventBus for standalone module operation
- ✓ Create ISyncService + SyncService (change detection, tree snapshots, reconciliation)
- ✓ Add new service methods: ListRecentAsync, GetVersionByNumberAsync, GetChunkManifestAsync, GetTrashSizeAsync, RemoveTagByNameAsync, GetAllUserTagsAsync
- ✓ Update Program.cs with AddFilesServices(), IFileStorageEngine, IEventBus registrations
- ✓ Add DTOs: BulkOperationDto, BulkResultDto, BulkItemResultDto, AddTagDto, AddCommentDto, EditCommentDto, SetQuotaDto, LabelVersionDto, SyncDtos
- ✓ Harden `FilesControllerBase` caller construction to bind `userId` to authenticated claim (`NameIdentifier`/`sub`) and reject spoofing

**Dependencies:** phase-1.3
**Blocking Issues:** None
**Notes:** All 47 endpoints implemented under /api/v1/files/ namespace. Controllers refactored from direct DbContext to service layer via FilesControllerBase. PATCH methods changed to PUT per spec. Caller identity is now claim-bound in `FilesControllerBase` (query `userId` must match authenticated principal) to prevent cross-user impersonation. Runtime hardening now exposes `/api/v1/files/*` directly from the core server for bare-metal installs (endpoint now returns auth-gated responses instead of 404 when unauthenticated). Download response hardening added in `FilesController.DownloadAsync` so null/empty/whitespace MIME values fall back to `application/octet-stream` (prevents ASP.NET Core `FormatException` and HTTP 500 on download). Files module test suite passes (476 tests).

---

### Step: phase-1.5 - Chunked Upload & Download Infrastructure
**Status:** completed ✅
**Duration:** ~1 week
**Description:** Complete the chunked transfer infrastructure: seekable ConcatenatedStream for HTTP range requests, per-chunk download by hash for sync clients, storage deduplication metrics, and orphaned chunk GC in upload session cleanup.

**Deliverables:**
- ✓ Make `ConcatenatedStream` seekable (implements `CanSeek`, `Position`, `Seek()`) — enables ASP.NET Core range processing
- ✓ Enable HTTP range requests in `FilesController.DownloadAsync` (`enableRangeProcessing: true`)
- ✓ Add `DownloadChunkByHashAsync` to `IDownloadService` + `DownloadService`
- ✓ Add `GET /api/v1/files/chunks/{chunkHash}` endpoint for sync client per-chunk downloads
- ✓ Create `IStorageMetricsService` + `StorageMetricsService` (physical vs. logical bytes, deduplication savings, chunk/version counts)
- ✓ Create `StorageMetricsController` with `GET /api/v1/files/storage/metrics`
- ✓ Add `StorageMetricsDto` with `PhysicalStorageBytes`, `LogicalStorageBytes`, `DeduplicationSavingsBytes`, `TotalUniqueChunks`, `TotalVersions`, `TotalFiles`
- ✓ Enhance `UploadSessionCleanupService` to GC orphaned chunks (ReferenceCount = 0) alongside session expiry
- ✓ Register `IStorageMetricsService` in `FilesServiceRegistration`
- ✓ 25 new unit tests: seekable stream seeking/position, chunk-by-hash download, storage metrics (dedup savings, orphaned exclusion), session cleanup GC — 347 total Files tests

**Dependencies:** phase-1.4
**Blocking Issues:** None
**Notes:** Phase 1.5 complete. All 20 Phase 1.5 checklist items marked ✓. Many were already implemented in Phases 1.2–1.4 (chunking, hashing, dedup, progress tracking, session management). This phase added the remaining pieces: seekable stream for HTTP range requests, per-chunk endpoint for sync clients, deduplication metrics API, and explicit orphaned-chunk GC in the upload cleanup service. 830 total solution tests pass (no regressions).

---

### Step: phase-1.6 - File Sharing & Permissions
**Status:** completed ✅
**Duration:** ~1 week (actual)
**Description:** Implement the sharing system, permission service with cascading, and anonymous public-link endpoint.

**Deliverables:**
- ✓ `FileShare`, `ShareType`, `SharePermission` domain models — already in place from Phase 1.1
- ✓ `IShareService` / `ShareService` — CRUD + public link token generation, expiry, download limits, password hashing
- ✓ `IPermissionService` / `PermissionService` — effective-permission resolution with owner fast-path and cascading ancestor shares
- ✓ Permission enforcement in `FileService` — Read on GetNode/ListChildren/Copy-source; ReadWrite on CreateFolder/Rename/Move/Copy-target; Full on Delete
- ✓ Permission enforcement in `DownloadService` — Read required for DownloadCurrent, DownloadVersion, GetChunkManifest
- ✓ Permission enforcement in `DownloadService.DownloadChunkByHashAsync` — returns chunk only when caller can read at least one referencing file
- ✓ Permission enforcement in `VersionService` — Read for listing/get, ReadWrite/Full for restore/label/delete
- ✓ Permission enforcement in `TagService` and `CommentService` — node read/read-write checks before list/create/edit/delete operations
- ✓ Permission enforcement in `ShareService.GetSharesAsync` — Full permission required to enumerate node shares
- ✓ `ShareController` (CRUD) + `PublicShareController` (anonymous link resolve) + `MySharesController` (shares-with-me listing)
- ✓ `FileSharedEvent` published on share creation
- ✓ 14 new `PermissionServiceTests` covering ownership, direct shares, expiry, multi-share, and cascading; 361 Files tests total, 1085 solution tests (no regressions)
- ☐ Notifications on public-link first access and share expiry (deferred)

**Dependencies:** phase-1.5
**Blocking Issues:** None
**Notes:** Phase 1.6 complete. `IPermissionService.GetEffectivePermissionAsync` walks the materialized path to check ancestor shares, giving O(depth) cascading without recursive queries. Service-layer permission enforcement has been expanded to comments, tags, version operations, share enumeration, and chunk-hash download paths to strengthen user-to-user file isolation. Team/group share enforcement deferred until `CallerContext` is enriched with membership IDs. Files module test suite passes (476 tests).

---

### Step: phase-1.7 - File Versioning System
**Status:** completed ✅
**Duration:** ~1 week (actual)
**Description:** Complete the file versioning system: version creation on upload, version history, restore to previous version with chunk reuse, and configurable retention policies enforced by a background cleanup service.

**Deliverables:**
- ✓ `FileVersion` + `FileVersionChunk` domain models — already in place from Phase 1.1
- ✓ `IVersionService` / `VersionService` — list, get, get-by-number, restore, label, delete
- ✓ Version created on every upload via `ChunkedUploadService.CompleteUploadAsync`
- ✓ Chunks linked via `FileVersionChunk` with sequence index; reference counts maintained
- ✓ `VersionController` REST API — 5 endpoints (list, get, restore, delete, label)
- ✓ Download specific version via `GET /api/v1/files/{nodeId}/download?version=N`
- ✓ `FileVersionRestoredEvent` published on version restore (new event, distinct from trash `FileRestoredEvent`)
- ✓ `VersionRetentionOptions` — configurable `MaxVersionCount` (default 50) and `RetentionDays` (default 0 = disabled), bound from `Files:VersionRetention` config section
- ✓ `VersionCleanupService` (IHostedService) — runs every 24 h, prunes oldest unlabeled versions exceeding max count, deletes unlabeled versions older than retention period, always keeps at least one version, never deletes labeled versions, decrements chunk reference counts
- ✓ Register `VersionCleanupService` in `FilesServiceRegistration`
- ✓ 11 new tests: 2 `FileVersionRestoredEvent` event tests, 1 event-publishing assertion in `VersionServiceTests`, 8 `VersionCleanupServiceTests` — 372 total Files tests, 1355 solution tests (no regressions)

**Dependencies:** phase-1.6
**Blocking Issues:** None
**Notes:** Phase 1.7 complete. All 15 Phase 1.7 checklist items marked ✓. Most of the core versioning infrastructure (models, DB config, service interface/impl, REST controllers, download endpoint) was already in place from Phases 1.1–1.4. This phase added the missing pieces: `FileVersionRestoredEvent` with IEventBus publishing in `VersionService.RestoreVersionAsync`, `VersionRetentionOptions` (IOptions pattern), and `VersionCleanupService` background service enforcing max-count and time-based retention policies with labeled-version protection. 1355 total solution tests pass.

---

### Step: phase-1.8 - Trash & Recovery
**Status:** completed ✅
**Duration:** ~1 day (actual)
**Description:** Complete the trash/recovery system: remove shares on soft-delete, auto-rename on name conflict during restore, update user quota on permanent delete, and make trash retention configurable via `TrashRetentionOptions`.

**Deliverables:**
- ✓ `TrashRetentionOptions` — configurable `RetentionDays` (default 30) and `CleanupInterval` (default 6 h), bound from `Files:TrashRetention` config section
- ✓ `TrashCleanupService` updated to use `IOptions<TrashRetentionOptions>` (replaces hardcoded statics); retention=0 disables auto-cleanup
- ✓ `FileService.DeleteAsync` removes shares for soft-deleted node and all its descendants (trashed items should not remain shared)
- ✓ `TrashService.RestoreAsync` auto-renames restored nodes if a name conflict exists in the target folder (mirrors `GetCopyNameAsync` pattern)
- ✓ `TrashService.PermanentDeleteAsync` decrements `FileQuota.UsedBytes` after deletion (direct DB update, clamped to 0)
- ✓ `TrashService.EmptyTrashAsync` decrements `FileQuota.UsedBytes` after bulk deletion
- ✓ `FilesServiceRegistration` binds `TrashRetentionOptions` from configuration
- ✓ 9 new tests: `DeleteAsync_RemovesSharesWhenTrashing`, `DeleteAsync_FolderWithSharedDescendants_RemovesAllShares`, `RestoreAsync_NameConflict_RenamesNode`, `RestoreAsync_NoNameConflict_KeepsOriginalName`, `PermanentDeleteAsync_UpdatesUserQuota`, `PermanentDeleteAsync_QuotaNotDecremented_BelowZero`, `PermanentDeleteAsync_NoQuotaRecord_Succeeds`, `EmptyTrashAsync_UpdatesUserQuota`, `RestoreAllAsync_RestoresAllTopLevelItems` — 381 total Files tests, all passing

**Dependencies:** phase-1.7
**Blocking Issues:** None
**Notes:** Phase 1.8 complete. Admin-configurable retention per organization deferred to admin UI phase. 381 total Files tests pass.

---

### Step: phase-1.9 - Storage Quotas & Limits
**Status:** completed ✅
**Duration:** ~1 day (actual)
**Description:** Full quota enforcement, administration, notifications, and UI display. Real-time quota tracking on upload and copy, configurable trashed-items exclusion, event-based threshold notifications, admin listing endpoint, and FileBrowser progress bar.

**Deliverables:**
- ✓ `QuotaOptions` — configurable `DefaultQuotaBytes` (10 GB), `ExcludeTrashedFromQuota` (default false), `WarnAtPercent` (80%), `CriticalAtPercent` (95%), `RecalculationInterval` (24 h); bound from `Files:Quota`
- ✓ `QuotaWarningEvent`, `QuotaCriticalEvent`, `QuotaExceededEvent` — published when thresholds crossed or quota exceeded
- ✓ `FilesErrorCodes.QuotaExceeded` = `"FILES_QUOTA_EXCEEDED"` added
- ✓ `IQuotaService` extended: `GetOrCreateQuotaAsync`, `GetAllQuotasAsync`, `AdjustUsedBytesAsync`
- ✓ `QuotaService` updated: auto-creates quota with configurable default, publishes threshold events, respects `ExcludeTrashedFromQuota` via `IgnoreQueryFilters()`
- ✓ `ChunkedUploadService.CompleteUploadAsync` calls `AdjustUsedBytesAsync` post-upload (increments by delta for new/updated files)
- ✓ `FileService.CopyAsync` checks quota before copying (file or folder subtree size), increments quota on success
- ✓ `QuotaController` new `GET /api/v1/files/quota/all` endpoint for admin quota listing; `GetCurrentAsync` uses `GetOrCreateQuotaAsync`
- ✓ `QuotaRecalculationService` updated to delegate to `IQuotaService.RecalculateAsync` (picks up trashed exclusion and notifications); uses configurable `RecalculationInterval`
- ✓ `FilesServiceRegistration` registers `QuotaOptions`
- ✓ `QuotaViewModel` and `QuotaProgressBar` Blazor component added; `FileBrowser` exposes quota state
- ✓ 15 new tests (QuotaServiceTests): `GetOrCreateQuotaAsync_*`, `GetAllQuotasAsync_*`, `AdjustUsedBytesAsync_*`, `RecalculateAsync_ExcludeTrash*`, event notification tests — 396 total Files tests, all passing
- ✓ 1 new FileServiceTests: `CopyAsync_InsufficientQuota_ThrowsValidationException`

**Dependencies:** phase-1.8
**Blocking Issues:** None
**Notes:** Phase 1.9 complete. `ExcludeTrashedFromQuota` works via `IgnoreQueryFilters()` since `FileNode` has a global soft-delete query filter. Quota increment on copy handles both file and folder subtrees via `CalculateSubtreeSizeAsync`. 396 total Files tests pass.

---

### Step: phase-1.10 - WOPI Host & Collabora Integration
**Status:** completed ✅
**Duration:** ~8 hours (actual, across sessions)
**Description:** Implement WOPI protocol endpoints (CheckFileInfo, GetFile, PutFile) with HMAC-signed access tokens, Collabora discovery service, health check, and Blazor document editor component.

**Deliverables:**
- ✓ `CollaboraOptions` — configuration model (ServerUrl, WopiBaseUrl, TokenSigningKey, TokenLifetimeMinutes, Enabled, etc.)
- ✓ `FilesErrorCodes` — WOPI-specific error codes (WopiTokenInvalid, WopiTokenExpired, WopiFileNotSupported, CollaboraUnavailable)
- ✓ WOPI DTOs — `WopiCheckFileInfoResponse`, `WopiAccessTokenDto`, `WopiTokenPayload`, `WopiTokenContext`, `WopiFileContentResult`, `CollaboraDiscoveryResult`, `CollaboraAction`
- ✓ `IWopiTokenService` — interface for HMAC-SHA256 token generation and validation
- ✓ `IWopiService` — interface for CheckFileInfo, GetFile, PutFile operations
- ✓ `ICollaboraDiscoveryService` — interface for WOPI discovery XML parsing, editor URL resolution, extension support
- ✓ `WopiTokenService` — HMAC-SHA256 signed tokens with userId, fileId, permissions, expiry; Base64URL-safe encoding
- ✓ `WopiService` — CheckFileInfo (permission-aware UserCanWrite), GetFile (via DownloadService), PutFile (chunk + version creation with deduplication)
- ✓ `CollaboraDiscoveryService` — XML discovery parsing, cached results, editor URL construction, extension support queries
- ✓ `CollaboraHealthCheck` — IHealthCheck implementation checking Collabora availability
- ✓ `WopiController` — rewritten with token-validated WOPI endpoints, token generation endpoint, discovery endpoints
- ✓ Core-server WOPI route exposure hardening — added `src/Core/DotNetCloud.Core.Server/Controllers/WopiController.cs` so `/api/v1/wopi/*` resolves in bare-metal single-process installs
- ✓ Service registration in `FilesServiceRegistration` — all WOPI/Collabora services, HttpClient("Collabora"), health check
- ✓ `DocumentEditor.razor` + `.razor.cs` — Blazor component with iframe embedding, loading/error states, co-editing indicators, supported format detection
- ✓ 34 unit tests — `WopiTokenServiceTests` (11), `WopiServiceTests` (14), `CollaboraDiscoveryServiceTests` (10) — all passing
- ✓ WOPI proof key validation — `IWopiProofKeyValidator` / `WopiProofKeyValidator` with RSA-SHA256 using `X-WOPI-Proof` headers; supports current-key, old-key, and rotation; 10 tests
- ✓ Max concurrent sessions — `IWopiSessionTracker` / `WopiSessionTracker` singleton; `TryBeginSession` / `HeartbeatSession` / `EndSession`; 9 tests; `DELETE /api/v1/wopi/token/{fileId}` session-close endpoint
- ✓ Supported file format configuration — `CollaboraOptions.SupportedMimeTypes` filters `IsSupportedExtensionAsync`
- ✓ Open supported documents from file browser — `FileBrowser` single-click actions invoke `DocumentEditor`/open handlers for supported extensions; `ApiBaseUrl`/`UserId` parameters added
- ✓ Collabora-availability-aware editor opening — `FileBrowser` now reads discovery actions and only opens `DocumentEditor` when Collabora is reachable and the extension is supported
- ✓ Create new Collabora-supported files from file browser — "New Document" flow creates zero-byte file nodes via `IChunkedUploadService` and opens the editor
- ✓ New Document fallback visibility — when Collabora is configured but discovery is temporarily unavailable, FileBrowser keeps New Document visible using a safe default extension list
- ✓ `DocumentEditor.razor.cs` real HTTP integration — `LoadEditorAsync` calls `POST /api/v1/wopi/token/{fileId}` via injected `HttpClient`; `CloseEditorAsync` calls `DELETE` to release session
- ✓ DocumentEditor API path hardening — token/session calls now normalize to root `/api/v1/wopi/*` even when `ApiBaseUrl` is route-based (prevents false HTTP 404 from `/apps/files/api/...`)
- ✓ WOPI token user identity hardening — `DocumentEditor` now resolves `UserId` from auth claims when parameter binding is empty, and core `WopiController` returns 401 for unresolved identity instead of throwing 500
- ✓ WOPI token transport hardening — `WopiTokenService` now uses Base64Url encoding for token payload/signature and accepts legacy Base64 on validation to avoid query-string decoding breakage (`+`/`/`) in Collabora callbacks
- ✓ WOPI fallback-signing hardening — when `TokenSigningKey` is not configured, `WopiTokenService` now uses a process-stable ephemeral key (instead of per-instance random keys), preventing signature mismatches between token generation and callback validation
- ✓ WOPI proof timestamp compatibility — `WopiProofKeyValidator` now accepts FILETIME, DateTime ticks, and Unix timestamp formats for age validation before signature verification
- ✓ WOPI proof-key format compatibility — when `proof-key value` is not importable as SubjectPublicKeyInfo, validator now falls back to discovery `modulus` + `exponent` RSA parameters for signature verification
- ✓ Collabora discovery URL normalization — `CollaboraDiscoveryService` rewrites `urlsrc` host/scheme to configured `Files:Collabora:ServerUrl` so returned `editorUrl` is client-reachable
- ✓ Editor parameter-binding hardening — `FileBrowser.razor` now passes `FileName`/`ApiBaseUrl` as bound expressions (not string literals), fixing literal `EditorNode.Name` rendering and incorrect API base propagation
- ✓ Collabora iframe CSP allowance — core pipeline now augments `Content-Security-Policy` with configured `Files:Collabora:ServerUrl` origin in `frame-src`/`child-src` so embedded editor loads
- ✓ SSR login cookie-write hardening — `/auth/login` now posts to server endpoint `/auth/session/login` (controller-based `SignInManager.PasswordSignInAsync`) to avoid "Headers are read-only, response has already started" failures on `/_blazor` circuit requests
- ✓ Server-side same-origin TLS hardening for LAN hostnames — scoped Blazor UI `HttpClient` now honors `Files:Collabora:AllowInsecureTls` for HTTPS base URIs so self-signed local certs on non-loopback hosts (for example `https://mint22:15443`) do not break editor bootstrap calls
- ✓ Proxied Collabora frame-header normalization — proxy pipeline now strips `X-Frame-Options` and rewrites CSP `frame-ancestors` to `'self'` on `/hosting/*`, `/browser/*`, `/cool/*`, and `/lool/*` responses so Firefox/Chromium can embed editor pages in the Files iframe
- ✓ Collabora CODE download/auto-installation — `CollaboraInstallCommand` (`dotnetcloud install collabora`); cross-platform download+extract with progress; setup wizard step 9
- ✓ Linux installer alignment — `tools/install.sh` now reads persisted `collaboraMode` (camelCase) and auto-runs Collabora CODE package installation when `BuiltIn` is selected during setup
- ✓ Linux installer runtime hardening for single-origin Collabora — `tools/install.sh` now derives DotNetCloud public origin from runtime config (`/etc/dotnetcloud/dotnetcloud.env` `Files__Collabora__ServerUrl`, with setup-config fallback), idempotently manages `/etc/coolwsd/coolwsd.xml` WOPI `alias_groups` entries, enforces `root:cool` + `640` permissions, then restarts and probes `coolwsd` (`/hosting/discovery`)
- ✓ Collabora process management — `ICollaboraProcessManager` / `CollaboraProcessManager` BackgroundService; start/stop/health monitor; exponential backoff restart; `UseBuiltInCollabora`/`CollaboraInstallDirectory`/`CollaboraExecutablePath` added to `CollaboraOptions` and `CliConfig`
- ✓ TLS/URL routing for Collabora — `GenerateNginxConfigWithCollabora` and `GenerateApacheConfigWithCollabora` in `ReverseProxyTemplates`; full location blocks for `/browser`, `/hosting/discovery`, `/cool/` WebSocket paths
- ✓ Core single-origin proxy routing for Collabora — `DotNetCloud.Core.Server` now forwards `/hosting/*`, `/browser/*`, `/cool/*`, and `/lool/*` via YARP (`AddHttpForwarder`) with optional `Files:Collabora:ProxyUpstreamUrl` so public `ServerUrl` can stay on the main HTTPS origin without self-proxy loops
- ✓ Collabora proxy startup diagnostics — startup warning logs now flag invalid `Files:Collabora:ServerUrl` when enabled and probable self-proxy loop configuration when `ServerUrl`/`WopiBaseUrl` share origin but `ProxyUpstreamUrl` is missing
- ✓ Collabora proxy origin-metadata hardening — YARP forwarding now preserves public `Host` and `X-Forwarded-*` values and response CSP normalization emits a single effective policy, preventing `cool.html` bootstrap leakage to `wss://localhost:9980` in single-origin deployments
- ✓ Admin UI for Collabora configuration — `/admin/collabora` Blazor page; Collabora health badge; all `CollaboraOptions` fields editable; proxy config snippet generator (nginx / Apache)

**Dependencies:** phase-1.3 (services), phase-1.5 (chunking)
**Blocking Issues:** None
**Notes:** Phase 1.10 fully complete. WOPI protocol with RSA-SHA256 proof key validation, session tracking, MIME filtering, and full Blazor integration. Added stricter token generation behavior so editor tokens are only issued when Collabora is enabled, reachable, and the file extension is supported. Added FileBrowser "New Document" creation for Collabora-supported formats, dynamic discovery-driven editor availability checks, and fallback New Document visibility when Collabora is configured but discovery is temporarily unavailable. Installer flow now correctly auto-installs built-in Collabora when setup saves `collaboraMode: BuiltIn` (camelCase config), and now also auto-configures `coolwsd` WOPI alias groups for the derived DotNetCloud public origin while enforcing `coolwsd.xml` service-readable permissions to prevent post-install host-authorization and startup regressions. Runtime hardening now exposes `/api/v1/wopi/*` directly from the core server for bare-metal installs and excludes `/api/v1/wopi/files/*` from response-envelope wrapping to preserve WOPI protocol payload shape for Collabora. Additional runtime fixes prevent false editor-open 404s by normalizing DocumentEditor API calls to root paths and normalizing discovery `urlsrc` to the configured Collabora server host/scheme. Latest hotfix hardens user identity propagation for token/session calls by resolving `UserId` from authenticated claims when UI parameter wiring is empty and by returning a clean 401 from `WopiController` when identity cannot be resolved, eliminating the prior `UserId cannot be empty` HTTP 500 path. Editor usability hardening now fixes Razor string parameter binding for `DocumentEditor` launch values and applies a full-screen modal layout for the editor panel. Security-header hardening now allows the configured Collabora origin in CSP `frame-src`/`child-src`, unblocking iframe rendering in `/apps/files`. Final token hardening now uses Base64Url-safe transport plus a process-stable fallback signing key when `TokenSigningKey` is unset, eliminating callback-time token parsing/signature mismatch failure modes during Collabora `CheckFileInfo`. Proof-key hardening now accepts multiple valid timestamp encodings (FILETIME, DateTime ticks, Unix) and falls back to modulus/exponent verification when SPKI `proof-key value` import fails, preventing false signature failures while retaining strict RSA verification. Login-flow hardening now performs cookie sign-in on a regular HTTP form post endpoint (`/auth/session/login`) instead of a Blazor circuit callback, preventing "headers are read-only" failures on `/auth/login` submit. Server-side Blazor API bootstrapping now also tolerates self-signed certs on non-loopback local hostnames when `Files:Collabora:AllowInsecureTls=true`, preventing TLS failures when opening the document editor via `https://mint22:15443`. Proxy response hardening now normalizes frame-related headers for Collabora paths by removing `X-Frame-Options` and setting CSP `frame-ancestors 'self'`, eliminating Firefox embedded-page blocking on same-origin editor loads. Final single-origin proxy hardening now preserves public forwarding origin metadata (`Host`, `X-Forwarded-Host`, `X-Forwarded-Proto`, `X-Forwarded-Port`) and emits one effective CSP for proxied Collabora responses, which makes `cool.html` advertise `wss://mint22:15443` instead of `wss://localhost:9980` and unblocks remote-browser editor bootstrap through the `/apps/files` iframe. Collabora process management (BackgroundService), CLI install command (`dotnetcloud install collabora`), setup wizard step 9, reverse proxy templates (nginx/Apache with Collabora location blocks), core in-app YARP forwarding for `/hosting`/`/browser`/`/cool`/`/lool` on single-origin deployments with optional `Files:Collabora:ProxyUpstreamUrl`, startup diagnostics to warn on likely self-proxy loop misconfiguration, and `/admin/collabora` admin UI remain complete.

---

### Step: phase-1.11 - File Browser Web UI (Blazor)
**Status:** completed ✅
**Duration:** ~2 hours
**Description:** Complete Blazor file management interface for the Files module — sidebar navigation, version history panel, admin settings page, column sorting, trash sort/bulk/size, loading skeletons, and improved empty states.

**Deliverables:**
- ✓ `QuotaProgressBar.razor` — markup for the quota bar component (code-behind existed from Phase 1.9); colour-coded fill (normal / warning / critical / exceeded), ARIA progressbar role
- ✓ `FileSidebar.razor` + `FileSidebar.razor.cs` — sidebar navigation component
  - ✓ "All Files" navigation item
  - ✓ "Favorites" navigation item
  - ✓ "Recent" navigation item
  - ✓ "Shared with me" navigation item
  - ✓ "Shared by me" navigation item
  - ✓ "Tags" navigation item with expandable tag list (colour dots, file counts)
  - ✓ "Trash" navigation item with item count badge
  - ✓ Storage quota display (`QuotaProgressBar` embedded at bottom)
  - ✓ `FileSidebarSection` enum + `FileTagViewModel` view model added to `ViewModels.cs`
- ✓ `VersionHistoryPanel.razor` + `VersionHistoryPanel.razor.cs` — version history side panel
  - ✓ Lists versions with version number, date, author, size
  - ✓ Download specific version (event callback)
  - ✓ Restore to specific version (event callback)
  - ✓ Add/edit version labels (inline edit, Enter/Escape keyboard shortcuts)
  - ✓ Delete old versions (removed from list, event callback)
  - ✓ `FileVersionViewModel` view model added to `ViewModels.cs`
- ✓ `FilesAdminSettings.razor` + `FilesAdminSettings.razor.cs` — Files module settings page
  - ✓ Default quota for new users (GB, 0 = unlimited)
  - ✓ Trash retention period (days)
  - ✓ Version retention settings (max versions + retention days)
  - ✓ Maximum upload size (MB)
  - ✓ Allowed/blocked file types (comma-separated extension lists)
  - ✓ Storage root path configuration
  - ✓ Client-side validation with inline error messages; Reset to Defaults button
  - ✓ `AdminSettingsViewModel` view model added to `ViewModels.cs`
- ✓ `FileBrowser.razor` / `.razor.cs` — column sort headers in list view
  - ✓ Sort by Name, Type, Size, Date (column header click)
  - ✓ `SortedNodes` computed property (folders always first); sort direction toggle
  - ✓ One-click-only interaction model: open actions moved to explicit single-click controls; no double-click dependency
  - ✓ Loading skeleton (8 skeleton rows while `IsLoading`)
  - ✓ Empty state: "No files yet — upload or create a folder" with inline action buttons
- ✓ `TrashBin.razor` / `.razor.cs` — enhanced trash bin
  - ✓ Trash size display (total across all items)
  - ✓ Sort by Name, Date deleted, Size (column header click + direction toggle)
  - ✓ Per-row checkboxes and "Select all" toggle
  - ✓ Bulk restore and bulk delete actions

**File Locations:**
- `src/Modules/Files/DotNetCloud.Modules.Files/UI/QuotaProgressBar.razor` (new)
- `src/Modules/Files/DotNetCloud.Modules.Files/UI/FileSidebar.razor` (new)
- `src/Modules/Files/DotNetCloud.Modules.Files/UI/FileSidebar.razor.cs` (new)
- `src/Modules/Files/DotNetCloud.Modules.Files/UI/VersionHistoryPanel.razor` (new)
- `src/Modules/Files/DotNetCloud.Modules.Files/UI/VersionHistoryPanel.razor.cs` (new)
- `src/Modules/Files/DotNetCloud.Modules.Files/UI/FilesAdminSettings.razor` (new)
- `src/Modules/Files/DotNetCloud.Modules.Files/UI/FilesAdminSettings.razor.cs` (new)
- `src/Modules/Files/DotNetCloud.Modules.Files/UI/ViewModels.cs` (extended — FileVersionViewModel, FileSidebarSection, FileTagViewModel, AdminSettingsViewModel)
- `src/Modules/Files/DotNetCloud.Modules.Files/UI/FileBrowser.razor` (enhanced — skeleton, empty state, list sort headers)
- `src/Modules/Files/DotNetCloud.Modules.Files/UI/FileBrowser.razor.cs` (enhanced — SortedNodes, SetSort, SortHeaderClass, SortIndicator)
- `src/Modules/Files/DotNetCloud.Modules.Files/UI/TrashBin.razor` (enhanced — sort headers, checkboxes, bulk actions, size)
- `src/Modules/Files/DotNetCloud.Modules.Files/UI/TrashBin.razor.cs` (enhanced — SortedItems, bulk ops, TrashTotalSizeLabel)

**Dependencies:** phase-1.9 (QuotaProgressBar code-behind), phase-1.10 (DocumentEditor)
**Blocking Issues:** None
**Notes:** All 8 component groups complete. Build: zero errors, zero warnings. No new tests required (UI-only components, no business logic). Components use the established pattern: `#pragma warning disable CS0649` for fields populated by future API integration, EventCallback parameters for host-page wiring, and `protected` property accessors following the existing FileBrowser/TrashBin pattern. File interactions now follow a one-click-only model (double-click handlers removed from Files UI).

### Step: phase-1.12 - File Upload & Preview UI
**Status:** completed ✅ (17/17 tasks)
**Duration:** ~1 session
**Description:** Enhanced upload experience with drag-and-drop on the browser, floating upload progress panel with speed/ETA/pause/cancel, ImageSharp-based thumbnail generation and caching, and full-screen file preview supporting all media types with keyboard navigation.

**Deliverables:**
- ✓ `IThumbnailService.cs` + `ThumbnailService.cs` — thumbnail generation/caching/cleanup service
  - ✓ ImageSharp 3.1.12 for raster image resizing (JPEG, PNG, GIF, WebP, BMP, TIFF)
  - ✓ FFmpeg-backed first-frame extraction for video thumbnails (`IVideoFrameExtractor`, `FfmpegVideoFrameExtractor`)
  - ✓ PDF first-page rendering via `IPdfPageRenderer` + `PdftoppmPdfPageRenderer`
  - ✓ All three sizes (128 / 256 / 512 px) cached to `{storageRoot}/.thumbnails/{prefix}/{id}_{size}.jpg`
  - ✓ `GenerateThumbnailAsync` (called on upload complete), `GetThumbnailAsync`, `DeleteThumbnailsAsync`
  - ✓ Thumbnail API endpoint wired: `GET /api/v1/files/{nodeId}/thumbnail?size=small|medium|large`
- ✓ `UploadProgressPanel.razor` + `UploadProgressPanel.razor.cs` — floating upload progress panel
  - ✓ Per-file progress bar, speed (B/KB/MB per second), ETA (s/m/h remaining)
  - ✓ Pause / resume / cancel per file (IsPaused / IsCancelled flags)
  - ✓ Overall aggregate progress bar
  - ✓ Minimize / expand toggle
- ✓ `FilePreview.razor` + `FilePreview.razor.cs` — full-screen preview modal (replaces placeholder)
  - ✓ Image: inline `<img>` tag
  - ✓ Video: HTML5 `<video>` with controls
  - ✓ Audio: HTML5 `<audio>` with controls + artwork area
  - ✓ PDF: `<iframe>` embed
  - ✓ Text/Code: `<iframe>` embed with language label from extension
  - ✓ Markdown: `<iframe>` embed
  - ✓ Unsupported: fallback Download File button
  - ✓ Keyboard: Escape = close, ← = prev, → = next file
  - ✓ Prev/next navigation across all files in current folder
  - ✓ Metadata footer (MIME type, size, modified date, position in folder)
  - ✓ Share button (raises OnShare → FileBrowser opens ShareDialog)
  - ✓ Download button (raises OnDownload event)
  - ✓ Auto-focus overlay on render for keyboard capture
  - ✓ Touch gestures (swipe left/right navigation; pinch zoom for image previews) via JS interop
- ✓ `FileUploadComponent.razor` + `.razor.cs` — enhanced upload dialog
  - ✓ Uses `UploadProgressPanel` while uploading (replaces inline file list)
  - ✓ Per-file speed/ETA tracked via `Stopwatch` + chunk bytes
  - ✓ Pause / resume / cancel wired to UploadProgressPanel callbacks
  - ✓ Remove individual pending files before upload
- ✓ `FileBrowser.razor` + `.razor.cs` — browser-level drag zone + thumbnail display
  - ✓ Drag-and-drop zone over entire browser (counter-based to avoid child-element flicker)
  - ✓ `browser-drop-overlay` shown when dragging files over browser
  - ✓ Drop captures files and pre-populates upload queue (browser overlay `InputFile` + `InitialFiles`)
  - ✓ Browser default drop navigation blocked (`@ondrop:preventDefault`) so dropped files are not opened in-tab
  - ✓ Grid view shows `<img src="@node.ThumbnailUrl">` when thumbnail URL is set
  - ✓ Passes `SortedNodes` as `AllNodes` to `FilePreview` for in-folder navigation
  - ✓ `OnShare` + `OnDownload` callbacks wired from `FilePreview`
  - ✓ Folder drag-and-drop (recursive upload) via JS DataTransfer directory traversal + recursive folder creation API calls
- ✓ `ViewModels.cs` extended: `ThumbnailUrl` on `FileNodeViewModel`; `SpeedBytesPerSecond`, `EtaSeconds`, `IsPaused`, `IsCancelled` on `UploadFileItem`; `Paused` added to `UploadStatus`

**File Locations:**
- `src/Modules/Files/DotNetCloud.Modules.Files/Services/IThumbnailService.cs` (new)
- `src/Modules/Files/DotNetCloud.Modules.Files/Services/ThumbnailService.cs` (new)
- `src/Modules/Files/DotNetCloud.Modules.Files/Services/IPdfPageRenderer.cs` (new)
- `src/Modules/Files/DotNetCloud.Modules.Files/Services/PdftoppmPdfPageRenderer.cs` (new)
- `src/Modules/Files/DotNetCloud.Modules.Files/UI/UploadProgressPanel.razor` (new)
- `src/Modules/Files/DotNetCloud.Modules.Files/UI/UploadProgressPanel.razor.cs` (new)
- `src/Modules/Files/DotNetCloud.Modules.Files/UI/FilePreview.razor` (rewritten — full implementation)
- `src/Modules/Files/DotNetCloud.Modules.Files/UI/FilePreview.razor.cs` (rewritten — navigation, keyboard, media)
- `src/Modules/Files/DotNetCloud.Modules.Files/UI/FileUploadComponent.razor` (enhanced — UploadProgressPanel, pause/cancel)
- `src/Modules/Files/DotNetCloud.Modules.Files/UI/FileUploadComponent.razor.cs` (enhanced — speed/ETA, pause/cancel logic)
- `src/Modules/Files/DotNetCloud.Modules.Files/UI/FileBrowser.razor` (enhanced — drag zone, thumbnails, AllNodes, callbacks)
- `src/Modules/Files/DotNetCloud.Modules.Files/UI/FileBrowser.razor.cs` (enhanced — drag counter, preview share/download)
- `src/Modules/Files/DotNetCloud.Modules.Files/UI/ViewModels.cs` (extended — ThumbnailUrl, upload speed/ETA/pause/cancel fields)
- `src/Modules/Files/DotNetCloud.Modules.Files/DotNetCloud.Modules.Files.csproj` (SixLabors.ImageSharp 3.1.12 added)
- `src/UI/DotNetCloud.UI.Web/wwwroot/js/files-drop-bridge.js` (enhanced — recursive directory traversal from DataTransfer items)
- `src/UI/DotNetCloud.UI.Web/wwwroot/js/file-upload.js` (enhanced — relative path handling + recursive folder auto-creation during upload)
- `src/UI/DotNetCloud.UI.Web/wwwroot/js/file-preview-gestures.js` (new — swipe/pinch gesture bridge)
- `src/UI/DotNetCloud.UI.Web/Components/App.razor` (updated script includes)
- `tests/DotNetCloud.Modules.Files.Tests/UI/FileUploadComponentTests.cs` (new — multi-file upload regression coverage)
- `tests/DotNetCloud.Modules.Files.Tests/Services/ThumbnailServiceTests.cs` (expanded — video + PDF thumbnail tests)

**Dependencies:** phase-1.11 (UI components), phase-1.3 (IChunkedUploadService interface)
**Blocking Issues:** None
**Notes:** Build succeeds for changed runtime projects (`DotNetCloud.Modules.Files`, `DotNetCloud.UI.Web`). Drag-and-drop supports recursive folder uploads by traversing `DataTransferItem` directory entries (`webkitGetAsEntry`), capturing per-file relative paths, and resolving/creating nested folders through `/api/v1/files` and `/api/v1/files/folders` before upload. Thumbnail generation now covers image, video first frame (FFmpeg), and PDF first page (pdftoppm bridge), all cached through the existing thumbnail store path. Preview UX now includes mobile touch gestures via JS interop: swipe left/right for file navigation and pinch zoom for image previews. Validation: `ThumbnailServiceTests` 4/4 passing and `FilesThumbnailIntegrationTests` 2/2 passing.

---

### Step: phase-1.13 - File Sharing & Settings UI
**Status:** completed ✅
**Duration:** ~1 session
**Description:** Comprehensive share dialog rewrite with user/team/group search, existing share management, and public link settings. "Shared with me" and "Shared by me" views integrated into FileBrowser via ActiveSection routing. FilesAdminSettings expanded with storage backend selection (Local/S3), Collabora integration settings, and full validation.

**Deliverables:**
- ✓ `ShareDialog.razor` + `.razor.cs` — rewritten with comprehensive sharing UI
  - ✓ Search users by name/email for sharing
  - ✓ Search teams and groups for sharing
  - ✓ Show all existing shares for a node with inline permission change and removal
  - ✓ Public link section with toggle, copy button, password, max downloads, and expiry
- ✓ `SharedWithMeView.razor` + `.razor.cs` — new "Shared with me" view
  - ✓ Group items by share source (who shared)
  - ✓ Permission badges (View only / Can edit / Full access)
  - ✓ Accept/decline actions per item
  - ✓ Relative date formatting for expiry
  - ✓ Grouping selector (by sharer / by type / none)
- ✓ `SharedByMeView.razor` + `.razor.cs` — new "Shared by me" view
  - ✓ Sortable columns (Name, Date) with direction toggle
  - ✓ Inline permission editing via dropdown
  - ✓ Share revocation per item
  - ✓ Copy public link button
  - ✓ Download count / max downloads display
  - ✓ `SharePermissionChangedEventArgs` event args class
- ✓ `FilesAdminSettings.razor` + `.razor.cs` — expanded admin settings
  - ✓ Storage backend selection (Local filesystem / S3-compatible)
  - ✓ S3 configuration fields (endpoint, bucket, region, access key, secret key)
  - ✓ Default quota management with hints
  - ✓ Trash auto-cleanup with interval note
  - ✓ Version retention with labeled-version protection note
  - ✓ Upload limits (allowed/blocked extensions)
  - ✓ Collabora Online integration (enable/disable, built-in vs external, auto-save interval, max sessions)
  - ✓ Comprehensive validation for all conditional sections
- ✓ `FileBrowser.razor` + `.razor.cs` — integrated shared views
  - ✓ `ActiveSection` parameter controlling SharedWithMe/SharedByMe/AllFiles routing
  - ✓ `SharedWithMeItems` and `SharedByMeItems` parameters
  - ✓ Event callbacks for open, decline, manage, revoke, permission change, copy link
- ✓ `ViewModels.cs` — `SharedItemViewModel`, `AdminSettingsViewModel` (already had all needed fields from Phase 1.11)

**File Locations:**
- `src/Modules/Files/DotNetCloud.Modules.Files/UI/ShareDialog.razor` (rewritten)
- `src/Modules/Files/DotNetCloud.Modules.Files/UI/ShareDialog.razor.cs` (rewritten)
- `src/Modules/Files/DotNetCloud.Modules.Files/UI/SharedWithMeView.razor` (new)
- `src/Modules/Files/DotNetCloud.Modules.Files/UI/SharedWithMeView.razor.cs` (new)
- `src/Modules/Files/DotNetCloud.Modules.Files/UI/SharedByMeView.razor` (new)
- `src/Modules/Files/DotNetCloud.Modules.Files/UI/SharedByMeView.razor.cs` (new)
- `src/Modules/Files/DotNetCloud.Modules.Files/UI/FilesAdminSettings.razor` (expanded)
- `src/Modules/Files/DotNetCloud.Modules.Files/UI/FilesAdminSettings.razor.cs` (expanded)
- `src/Modules/Files/DotNetCloud.Modules.Files/UI/FileBrowser.razor` (enhanced — shared view routing)
- `src/Modules/Files/DotNetCloud.Modules.Files/UI/FileBrowser.razor.cs` (enhanced — shared item parameters and handlers)

**Dependencies:** phase-1.12 (UI components), phase-1.6 (sharing services)
**Blocking Issues:** None
**Notes:** Phase 1.13 complete. All 4 step groups implemented: comprehensive share dialog, shared-with-me view, shared-by-me view, and expanded admin settings. Build: zero errors, zero warnings. No new tests required (UI-only components). Components follow established patterns: EventCallback parameters, protected property accessors, code-behind partial classes.

---

### Step: phase-1.14 - Client.Core — Shared Sync Engine
**Status:** completed ✅
**Duration:** ~1 session
**Description:** Created `DotNetCloud.Client.Core` shared client library with full sync engine, API client, OAuth2 PKCE auth, chunked transfer, conflict resolution, SQLite local state database, and selective sync configuration.

**Deliverables:**
- ✓ `DotNetCloud.Client.Core` class library project created; added to `DotNetCloud.sln`
- ✓ `DotNetCloud.Client.Core.Tests` test project created; added to `DotNetCloud.sln`
- ✓ **API Client** — `IDotNetCloudApiClient` interface + `DotNetCloudApiClient` implementation
  - ✓ Authentication (exchange code, refresh token, revoke token)
  - ✓ File operations (list, get, create folder, rename, move, copy, delete)
  - ✓ Upload operations (initiate, upload chunk, complete)
  - ✓ Download operations (file, version, chunk by hash, chunk manifest)
  - ✓ Sync operations (changes since, folder tree, reconcile)
  - ✓ Quota operations (get quota)
  - ✓ Retry with exponential backoff (3 retries, 500ms base delay)
  - ✓ Rate limiting — 429 handling with Retry-After header respect
- ✓ **OAuth2 PKCE Authentication** — `IOAuth2Service` + `OAuth2Service`
  - ✓ Authorization Code + PKCE flow (code verifier/challenge generation)
  - ✓ System browser launch for authorization
  - ✓ Localhost callback listener (HttpListener on port 52701, 5-minute timeout)
  - ✓ Authorization code exchange for tokens
  - ✓ Automatic token refresh
  - ✓ Token revocation (access + refresh)
- ✓ **Secure Token Storage** — `ITokenStore` + `EncryptedFileTokenStore`
  - ✓ AES-GCM encryption with machine-derived key (cross-platform)
  - ✓ Per-account key files (SHA-256 keyed filename)
  - ✓ Save, load, delete operations
- ✓ **Sync Engine** — `ISyncEngine` + `SyncEngine`
  - ✓ `FileSystemWatcher` for instant change detection
  - ✓ Periodic full scan (configurable interval, default 5 minutes)
  - ✓ Remote change application (download, handle deletions)
  - ✓ Local change application (upload pending operations)
  - ✓ Conflict detection (local and remote both modified since last sync)
  - ✓ Pause/resume support
  - ✓ `StatusChanged` event for UI/tray notification
  - ✓ `SyncContext`, `SyncStatus`, `SyncState` model classes
- ✓ **Chunked Transfer Client** — `IChunkedTransferClient` + `ChunkedTransferClient`
  - ✓ 4 MB chunk size
  - ✓ SHA-256 hash per chunk
  - ✓ Chunk manifest generation (upload deduplication — skip chunks server already has)
  - ✓ Delta sync on download (per-chunk download by hash)
  - ✓ Configurable concurrent chunk count (default 4)
  - ✓ `TransferProgress` model for upload/download progress reporting
- ✓ **Conflict Resolution** — `IConflictResolver` + `ConflictResolver`
  - ✓ Conflict copy creation: `report (conflict - Ben - 2025-07-14).docx` pattern
  - ✓ Auto-increment on duplicate conflict copies
  - ✓ `ConflictDetected` event for user notification
  - ✓ No silent data loss (both versions preserved)
- ✓ **Local State Database** — `ILocalStateDb` + `LocalStateDb` (EF Core SQLite)
  - ✓ `LocalFileRecord` table (path, node ID, hash, sync timestamps)
  - ✓ `PendingOperationDbRow` table (upload/download queue)
  - ✓ `SyncCheckpointRow` table (last sync timestamp)
  - ✓ Upsert, remove, get-by-path, get-by-node-id operations
  - ✓ Pending operation queue with upload/download counts
- ✓ **Selective Sync** — `ISelectiveSyncConfig` + `SelectiveSyncConfig`
  - ✓ Folder include/exclude rules per context
  - ✓ Longest-match wins (most specific rule takes precedence)
  - ✓ Per-context isolation
  - ✓ JSON persistence (save/load)
- ✓ `ClientCoreServiceExtensions.AddDotNetCloudClientCore()` DI registration
- ✓ 53 unit tests: API client (6), token info (4), token store (4), sync engine (8), chunked transfer (3), conflict resolver (6), local state DB (11), selective sync (11) — all passing

**File Locations:**
- `src/Clients/DotNetCloud.Client.Core/DotNetCloud.Client.Core.csproj`
- `src/Clients/DotNetCloud.Client.Core/ClientCoreServiceExtensions.cs`
- `src/Clients/DotNetCloud.Client.Core/Api/IDotNetCloudApiClient.cs`
- `src/Clients/DotNetCloud.Client.Core/Api/DotNetCloudApiClient.cs`
- `src/Clients/DotNetCloud.Client.Core/Api/ApiModels.cs`
- `src/Clients/DotNetCloud.Client.Core/Auth/IOAuth2Service.cs`
- `src/Clients/DotNetCloud.Client.Core/Auth/OAuth2Service.cs`
- `src/Clients/DotNetCloud.Client.Core/Auth/ITokenStore.cs`
- `src/Clients/DotNetCloud.Client.Core/Auth/EncryptedFileTokenStore.cs`
- `src/Clients/DotNetCloud.Client.Core/Auth/TokenInfo.cs`
- `src/Clients/DotNetCloud.Client.Core/Sync/ISyncEngine.cs`
- `src/Clients/DotNetCloud.Client.Core/Sync/SyncEngine.cs`
- `src/Clients/DotNetCloud.Client.Core/Sync/SyncContext.cs`
- `src/Clients/DotNetCloud.Client.Core/Sync/SyncStatus.cs`
- `src/Clients/DotNetCloud.Client.Core/Sync/SyncState.cs`
- `src/Clients/DotNetCloud.Client.Core/Transfer/IChunkedTransferClient.cs`
- `src/Clients/DotNetCloud.Client.Core/Transfer/ChunkedTransferClient.cs`
- `src/Clients/DotNetCloud.Client.Core/Transfer/TransferProgress.cs`
- `src/Clients/DotNetCloud.Client.Core/Conflict/IConflictResolver.cs`
- `src/Clients/DotNetCloud.Client.Core/Conflict/ConflictResolver.cs`
- `src/Clients/DotNetCloud.Client.Core/Conflict/ConflictInfo.cs`
- `src/Clients/DotNetCloud.Client.Core/LocalState/ILocalStateDb.cs`
- `src/Clients/DotNetCloud.Client.Core/LocalState/LocalStateDb.cs`
- `src/Clients/DotNetCloud.Client.Core/LocalState/LocalStateDbContext.cs`
- `src/Clients/DotNetCloud.Client.Core/LocalState/Entities/LocalFileRecord.cs`
- `src/Clients/DotNetCloud.Client.Core/LocalState/Entities/PendingOperationRecord.cs`
- `src/Clients/DotNetCloud.Client.Core/SelectiveSync/ISelectiveSyncConfig.cs`
- `src/Clients/DotNetCloud.Client.Core/SelectiveSync/SelectiveSyncConfig.cs`
- `tests/DotNetCloud.Client.Core.Tests/` (7 test files, 53 tests)

**Dependencies:** phase-1.4 (chunked upload API), phase-1.5 (chunk download API), phase-1.9 (sync endpoints)
**Blocking Issues:** None
**Notes:** Phase 1.14 complete. All 32 checklist items implemented. `DotNetCloud.Client.Core` is a pure .NET library (no ASP.NET Core dependency) suitable for use in both the Avalonia desktop client and the MAUI mobile client. Token storage uses AES-GCM cross-platform encryption (Windows DPAPI can be layered on top by callers if desired). 53 tests pass; full solution builds 0 errors.

---

### Step: phase-1.15 - Client.SyncService — Background Sync Worker
**Status:** completed ✅
**Duration:** ~1 week (actual)
**Description:** Background sync worker service (Windows Service / systemd unit) managing multiple sync contexts (one per OS-user + account pair). Provides an IPC server over Named Pipe (Windows) or Unix socket (Linux) for communication with SyncTray and other clients.

**Deliverables:**
- ✓ `src/Clients/DotNetCloud.Client.SyncService/DotNetCloud.Client.SyncService.csproj` (.NET Worker Service, Windows Service + systemd)
- ✓ `src/Clients/DotNetCloud.Client.SyncService/Program.cs`
- ✓ `src/Clients/DotNetCloud.Client.SyncService/SyncWorker.cs` (BackgroundService orchestrator)
- ✓ `src/Clients/DotNetCloud.Client.SyncService/SyncServiceExtensions.cs` (DI registration)
- ✓ `src/Clients/DotNetCloud.Client.SyncService/ContextManager/SyncContextRegistration.cs`
- ✓ `src/Clients/DotNetCloud.Client.SyncService/ContextManager/AddAccountRequest.cs`
- ✓ `src/Clients/DotNetCloud.Client.SyncService/ContextManager/SyncEventArgs.cs`
- ✓ `src/Clients/DotNetCloud.Client.SyncService/ContextManager/ISyncContextManager.cs`
- ✓ `src/Clients/DotNetCloud.Client.SyncService/ContextManager/SyncContextManager.cs`
- ✓ `src/Clients/DotNetCloud.Client.SyncService/Ipc/IpcProtocol.cs` (newline-delimited JSON protocol)
- ✓ `src/Clients/DotNetCloud.Client.SyncService/Ipc/IIpcServer.cs`
- ✓ `src/Clients/DotNetCloud.Client.SyncService/Ipc/IpcServer.cs` (Named Pipe + Unix socket)
- ✓ `src/Clients/DotNetCloud.Client.SyncService/Ipc/IpcClientHandler.cs` (per-connection handler with event subscription)
- ✓ `src/Clients/DotNetCloud.Client.SyncService/Ipc/IpcCallerIdentity.cs` (transport-resolved caller identity model)
- ✓ `tests/DotNetCloud.Client.SyncService.Tests/` (3 test files, 27 tests)
- ✓ Linux privilege dropping (UID/GID per context) — Unix socket peer credentials are resolved from transport (`SO_PEERCRED`) and context-scoped operations execute via Linux `setresuid`/`setresgid` transition with deterministic failure semantics
- ✓ Windows user impersonation — named-pipe caller token is captured/duplicated and context-scoped operations execute via `WindowsIdentity.RunImpersonated`
- ✓ Caller identity verification in IPC — named-pipe callers are identified via transport impersonation identity; unavailable identities are denied
- ✓ Rate-limit / debounce sync triggers — `sync-now` now returns `started=false, reason=rate-limited` during cooldown
- ☐ FileSystemWatcher burst coalescing debounce — pending short-delay batching for rapid-save event storms
- ✓ Disk full detection — `SyncEngine` now detects disk-full IO failures (`0x80070070` + OS-specific ENOSPC text), pauses further sync attempts, and surfaces `SyncState.Error`/`LastError` through existing SyncTray error notifications

**Dependencies:** Phase 1.14 (Client.Core — Shared Sync Engine)
**Blocking Issues:** None
**Notes:** Phase 1.15 hardening advanced to 24/25 checklist items implemented. IPC commands enforce caller identity, restrict context-scoped operations to owner-matching contexts, and filter push events to caller-owned contexts. `sync-now` applies deterministic cooldown no-op semantics (`started=false`, `reason=rate-limited`) when throttled. Windows named-pipe caller identity captures/duplicates the transport token and runs context-scoped work under `WindowsIdentity.RunImpersonated`; Linux Unix socket callers now resolve peer UID/GID from transport credentials and execute context-scoped operations under guarded `setresuid`/`setresgid` privilege transitions. Disk-full failures are explicitly detected and surfaced as sync errors while pausing further sync attempts until manual resume. Remaining deferred/pending item: FileSystemWatcher burst-coalescing debounce. SyncService tests pass (27/27).

---

### Step: phase-1.16 - Client.SyncTray — Avalonia Tray App
**Status:** completed ✅
**Duration:** ~1 week (actual)
**Description:** Avalonia system-tray application displaying sync status, context menu, settings window, and platform notifications. Connects to the background SyncService via Named Pipe (Windows) or Unix socket (Linux) using the existing IPC protocol.

**Deliverables:**
- ✓ `src/Clients/DotNetCloud.Client.SyncTray/DotNetCloud.Client.SyncTray.csproj` (Avalonia 11.3.12, net10.0, references SyncService + Client.Core)
- ✓ `src/Clients/DotNetCloud.Client.SyncTray/Program.cs` (single-instance mutex, Avalonia app builder)
- ✓ `src/Clients/DotNetCloud.Client.SyncTray/App.axaml` + `App.axaml.cs` (DI setup, tray icon lifecycle)
- ✓ `src/Clients/DotNetCloud.Client.SyncTray/TrayIconManager.cs` (programmatic tray icon, 5-state colour-circle icons, context menu)
- ✓ `src/Clients/DotNetCloud.Client.SyncTray/Ipc/IIpcClient.cs` (interface with event data types)
- ✓ `src/Clients/DotNetCloud.Client.SyncTray/Ipc/IpcClient.cs` (Named Pipe / Unix socket client, subscribe+read loop, reconnect, transport-factory ctor for testing)
- ✓ `src/Clients/DotNetCloud.Client.SyncTray/Notifications/INotificationService.cs`
- ✓ `src/Clients/DotNetCloud.Client.SyncTray/Notifications/WindowsNotificationService.cs` (Shell_NotifyIcon balloon tips via P/Invoke)
- ✓ `src/Clients/DotNetCloud.Client.SyncTray/Notifications/LinuxNotificationService.cs` (notify-send subprocess)
- ✓ `src/Clients/DotNetCloud.Client.SyncTray/Notifications/NotificationServiceFactory.cs` (runtime OS dispatch)
- ✓ `src/Clients/DotNetCloud.Client.SyncTray/Notifications/NoOpNotificationService.cs`
- ✓ `src/Clients/DotNetCloud.Client.SyncTray/ViewModels/ViewModelBase.cs`
- ✓ `src/Clients/DotNetCloud.Client.SyncTray/ViewModels/TrayViewModel.cs` (aggregate state, 5 TrayState values, IPC event handlers)
- ✓ `src/Clients/DotNetCloud.Client.SyncTray/ViewModels/AccountViewModel.cs`
- ✓ `src/Clients/DotNetCloud.Client.SyncTray/ViewModels/SettingsViewModel.cs` (OAuth2 add-account, remove account, relay commands)
- ✓ `src/Clients/DotNetCloud.Client.SyncTray/Views/SettingsWindow.axaml` + `SettingsWindow.axaml.cs`
- ✓ `src/Clients/DotNetCloud.Client.SyncTray/Views/AddAccountDialog.axaml` + `AddAccountDialog.axaml.cs`
- ✓ `tests/DotNetCloud.Client.SyncTray.Tests/` (3 test files, 24 tests)

**Dependencies:** Phase 1.15 (Client.SyncService — IPC server + protocol)
**Blocking Issues:** None
**Notes:** Phase 1.16 complete. 24 tests pass; full solution builds 0 errors, 0 warnings (Avalonia AVLN diagnostics suppressed). Tray icons are programmatic coloured circles (placeholder — production icons should be added to `Assets/`). Windows auto-start (`HKCU\Run`) and Linux autostart desktop file deferred to the packaging phase. Bandwidth-limit enforcement deferred (UI is present, enforcement in the sync engine is a future enhancement).

---

### Step: phase-1.17 - Bulk Operations & Tags
**Status:** completed ✅
**Duration:** ~1 day (actual)
**Description:** Bulk file operations (move, copy, delete, permanent delete) and a full tag system (create/remove/list tags with color, filter by tag, tag autocomplete, bulk tag operations) for the Files module.

**Deliverables:**
- ✓ `BulkController` — POST /bulk/move, /bulk/copy, /bulk/delete, /bulk/permanent-delete with per-node success/failure tracking
- ✓ `ITagService` / `TagService` — add, remove, getByNode, getByTagName, getAllUserTags, `GetUserTagSummariesAsync`, `BulkAddTagAsync`, `BulkRemoveTagByNameAsync`
- ✓ `TagController` — add/remove tag, list all, list by name, GET /tags/summary, POST /tags/bulk-add, POST /tags/bulk-remove
- ✓ `FileTagDto` moved to `FileDtos.cs`; `FileNodeDto.Tags` upgraded from `IReadOnlyList<string>` to `IReadOnlyList<FileTagDto>` (name + color)
- ✓ `UserTagSummaryDto` — tag name, representative color, file count for sidebar display
- ✓ `BulkTagDto` — NodeIds + TagName + Color for bulk tag requests
- ✓ `TagBadge.razor` / `.razor.cs` — reusable colored tag badge component with optional remove button
- ✓ `TagInput.razor` / `.razor.cs` — tag autocomplete input with color picker
- ✓ `FileBrowser.razor` — tag badges on file items, bulk-tag panel on selection toolbar, Tags section view
- ✓ `FileBrowser.razor.cs` — ActiveTag, TaggedNodes, UserTags state; FilterByTag, BulkTagAdd handlers; OnBulkTagAdd event callback
- ✓ `FileNodeViewModel.Tags` — rich tag view model list for badge rendering
- ✓ 60 new tests (17 tag service + existing 396 + new bulk tag tests); 456 total

**Dependencies:** Phase 1.9 (Tags model + TagService skeleton)
**Blocking Issues:** None
**Notes:** Phase 1.17 complete. `FileService.ToDto` now maps `FileTag` entities to `FileTagDto` including color. `GetUserTagSummariesAsync` groups by tag name and uses the most-recently-added color as the representative. Bulk ops follow the existing per-item-catch pattern (partial success). All 456 Files module tests pass.

---

### Step: phase-1.18 - Files gRPC Host
**Status:** completed ✅
**Duration:** ~1 day (actual)
**Description:** gRPC service contracts and implementation for the Files module, enabling the core supervisor to interact with the Files module over gRPC for all file operations (CRUD, tree, upload/download, sharing, trash, versioning, quotas).

**Deliverables:**
- ✓ `files_service.proto` — 22 RPCs: CreateFolder, ListNodes, GetNode, RenameNode, MoveNode, CopyNode, DeleteNode, ListTrash, RestoreNode, PurgeNode, EmptyTrash, InitiateUpload, UploadChunk, CompleteUpload, DownloadFile (server streaming), ListVersions, RestoreVersion, CreateShare, ListShares, RevokeShare, GetQuota, ToggleFavorite
- ✓ `FileNodeMessage`, `FileVersionMessage`, `FileShareMessage` shared proto messages
- ✓ `FilesGrpcService` — full gRPC implementation (22 RPCs, EF Core direct queries, content-hash deduplication, materialized path management)
- ✓ Harden `FilesGrpcService` for cross-user isolation: owner-scoped node/share lookups, user-scoped restore/purge/toggle paths, and owner checks on move/copy/create-folder parents
- ✓ Enforce authenticated caller identity in user-scoped gRPC RPCs (authenticated claim `NameIdentifier`/`sub` must match `request.user_id`)
- ✓ Harden gRPC upload path: `UploadChunk` now requires valid active session and verifies SHA-256 chunk hash against uploaded bytes and session manifest
- ✓ `FilesLifecycleService` — extends shared `ModuleLifecycle.ModuleLifecycleBase` (Initialize, Start, Stop, HealthCheck, GetManifest)
- ✓ `FilesHealthCheck` — ASP.NET Core `IHealthCheck` reporting module status
- ✓ `Program.cs` — registers FilesModule, FilesDbContext, IFileStorageEngine, InProcessEventBus, gRPC services, REST controllers, health checks

**Dependencies:** Phase 1.1 (models), Phase 1.2 (FilesDbContext), Phase 1.3 (services), Phase 0.6 (Core.Grpc lifecycle proto)
**Blocking Issues:** None
**Notes:** Phase 1.18 complete. Proto file uses module-specific response types (e.g., `CreateFolderResponse`) rather than generic `NodeResponse` for clarity. Lifecycle proto is shared via `DotNetCloud.Core.Grpc` — no separate `files_lifecycle.proto` needed. `FilesGrpcService` now applies explicit ownership constraints on sensitive node/share operations, requires authenticated caller identity to match request user scope, and blocks chunk ingestion unless the upload session is active and chunk data matches its declared hash. Files host project builds cleanly and Files module tests pass (483/483).

---

### Phase 1.19: Testing Infrastructure

#### Step: phase-1.19.1 - Unit Tests (DotNetCloud.Modules.Files.Tests)
**Status:** completed ✅
**Deliverables:**
- ✓ `FilesModuleManifestTests` — 10 tests
- ✓ `FilesModuleTests` — 18 tests
- ✓ `FileNodeTests` — 15 tests
- ✓ `FileQuotaTests` — 11 tests
- ✓ `EventTests` — 10 tests
- ✓ `FileUploadedEventHandlerTests` — 4 tests
- ✓ `ContentHasherTests` — 15 tests
- ✓ `LocalFileStorageEngineTests` — 17 tests
- ✓ `WopiTokenServiceTests` — 11 tests
- ✓ `WopiServiceTests` — 14 tests
- ✓ `CollaboraDiscoveryServiceTests` — 10 tests
- ✓ `FileServiceTests` — 20 tests (CRUD, authorization, name validation, materialized paths, favorites, search, copy, recent)
- ✓ `ChunkedUploadServiceTests` — 7 tests (initiate, upload chunk, complete, cancel, dedup, quota)
- ✓ `DownloadServiceTests` — 17 tests (file download, version download, chunk download, permissions)
- ✓ `VersionServiceTests` — 12 tests (list, get, restore, delete, label, retention)
- ✓ `ShareServiceTests` — 11 tests (create, list, delete, update, public link, password, expiry)
- ✓ `TrashServiceTests` — 17 tests (list, restore, permanent delete, empty, cascade, quota update)
- ✓ `QuotaServiceTests` — 22 tests (get, set, recalculate, enforcement, notifications)
- ✓ `TagServiceTests` — 17 tests (add, remove, list by tag, list user tags)
- ✓ `CommentServiceTests` — 9 tests (add, edit, delete, list, threaded replies)
- ✓ `BulkOperationTests` — 20 tests (bulk move, copy, delete, permanent delete, partial failure, DTOs, edge cases)
- ✓ `FilesGrpcServiceSecurityTests` — 7 tests (cross-user node isolation, request/claim identity mismatch rejection, upload session owner mismatch, invalid session, and chunk hash tampering)
- ✓ Additional tests: PermissionServiceTests (14), SyncServiceTests (14), StorageMetricsServiceTests, WopiSessionTrackerTests, WopiProofKeyValidatorTests, VersionCleanupServiceTests, UploadSessionCleanupServiceTests, model/DTO/enum tests

**Notes:** 483 total Files module tests pass. Coverage includes dedicated gRPC hardening regression tests for cross-user isolation and upload abuse scenarios in addition to service-layer authorization checks.

#### Step: phase-1.19.2 - Integration Tests (Files API)
**Status:** completed ✅
**Deliverables:**
- ✓ Files gRPC isolation integration tests in `DotNetCloud.Integration.Tests` (`FilesHostWebApplicationFactory` + `FilesGrpcIsolationIntegrationTests`: cross-user node access denial, request/claim mismatch rejection, upload session-owner mismatch rejection)
- ✓ Files REST isolation integration tests in `DotNetCloud.Integration.Tests` (`FilesRestIsolationIntegrationTests`: cross-user read/rename denial, upload session ownership enforcement, owner-scoped share/trash behavior, quota-exceeded upload rejection)
- ✓ Expanded REST workflow integration coverage (`FilesRestIsolationIntegrationTests`: list/search/favorites/recent, sync tree/changes/reconcile, WOPI discovery payload shape)
- ✓ Expanded feature-flow coverage (`FilesRestIsolationIntegrationTests`: dedup upload initiation, share create/update/revoke + public-link password validation, version list/get/label/restore, trash list/size/restore/purge, bulk move/copy/delete/permanent-delete)
- ✓ WOPI token/file endpoint smoke coverage (`FilesRestIsolationIntegrationTests`: graceful disabled-provider behavior check for token generation, plus CheckFileInfo/GetFile/PutFile path when provider is enabled)
- ✓ Local verification evidence refreshed after latest `main` pull (`FullyQualifiedName~FilesRestIsolationIntegrationTests` = 16 passing, `~Files` = 19 passing, `~MultiDatabaseMatrixTests` = 21 passing)
- ✓ Real-provider matrix runtime evidence in Docker-backed environment (PostgreSQL required; SQL Server lane where available)

**Notes:** Integration harness now covers both isolation and broader REST endpoint workflows (16 REST + 3 gRPC files-focused integration tests passing in current suite). Docker-backed provider matrix execution is now validated (`DockerDatabaseIntegrationTests`: 12/12 passed, PostgreSQL + SQL Server). Sprint A client-side compatibility validation remains tracked in mediation handoff and does not block this step.

#### Step: phase-1.19.3 - Client Tests (DotNetCloud.Client.Core.Tests)
**Status:** completed ✅
**Deliverables:**
- ✓ `SyncEngineTests` — change detection, reconciliation, conflict detection
- ✓ `ChunkedTransferClientTests` — split, hash, upload, resume
- ✓ `DotNetCloudApiClientTests` — mock HTTP responses, retry logic, rate limiting
- ✓ `LocalStateDbTests` — SQLite operations
- ✓ `EncryptedFileTokenStoreTests` + `TokenInfoTests` — OAuth2 PKCE token storage
- ✓ `SelectiveSyncConfigTests` — include/exclude logic
- ✓ `ConflictResolverTests` — conflict detection and resolution

**Dependencies:** Phase 1.14 (Client.Core)
**Notes:** 53 client tests pass. Tests cover sync engine, chunked transfer, API client, local state DB, auth token store, selective sync, and conflict resolution.

---

### Step: phase-1.20 - Documentation
**Status:** completed ✅
**Duration:** ~1 day
**Description:** Comprehensive documentation for the Files module, desktop sync client, admin guides, and user guides. Created 20 documentation files covering module architecture, REST API reference, sharing, versioning, WOPI/Collabora, sync protocol, admin configuration, backup/restore, and user getting-started guides.

**Files Module Documentation (8 items):**
- ✓ `docs/modules/files/README.md` — module overview, architecture, project structure, configuration
- ✓ `docs/modules/files/API.md` — complete REST API reference with 14 endpoint categories and examples
- ✓ `docs/modules/files/ARCHITECTURE.md` — data model, chunking strategy, dedup, storage engine, pipelines
- ✓ `docs/modules/files/SHARING.md` — 4 share types, permission levels, public link features, lifecycle
- ✓ `docs/modules/files/VERSIONING.md` — version creation, operations, retention config, background cleanup
- ✓ `docs/modules/files/WOPI.md` — WOPI protocol, endpoints, tokens, proof keys, session tracking, Collabora
- ✓ `docs/modules/files/SYNC.md` — sync architecture, protocol, chunked transfer, conflict resolution, IPC
- ✓ `src/Modules/Files/DotNetCloud.Modules.Files/README.md` — developer README with full project tree

**Desktop Client Documentation (4 items):**
- ✓ `docs/clients/desktop/README.md` — SyncService + SyncTray overview, architecture diagram
- ✓ `docs/clients/desktop/SETUP.md` — installation (Windows/Linux), account setup, selective sync
- ✓ `docs/clients/desktop/SYNC_PROTOCOL.md` — sync engine lifecycle, change detection, chunked transfer
- ✓ `docs/clients/desktop/TROUBLESHOOTING.md` — common issues, logs, diagnostics, FAQ

**Admin Documentation (3 items):**
- ✓ `docs/admin/files/CONFIGURATION.md` — storage, quotas, retention, upload limits, background services
- ✓ `docs/admin/files/COLLABORA.md` — Collabora CODE setup, deployment options, configuration, proxy
- ✓ `docs/admin/files/BACKUP.md` — backup/restore procedures, scheduled backups, disaster recovery

**Server Admin Documentation (3 items — cross-cutting):**
- ✓ `docs/admin/server/INSTALLATION.md` — Linux (Ubuntu/Debian, RHEL), Windows (MSI, manual), Docker, reverse proxy (nginx, Apache, IIS), TLS/Let's Encrypt
- ✓ `docs/admin/server/CONFIGURATION.md` — complete appsettings.json reference (Kestrel, auth, CORS, rate limiting, SignalR, Serilog, OpenTelemetry, security headers, env vars)
- ✓ `docs/admin/server/UPGRADING.md` — Linux/Windows/Docker upgrade procedures, database migrations, rollback, version compatibility

**User Documentation (3 items):**
- ✓ `docs/user/files/GETTING_STARTED.md` — upload, browse, share, organize, preview, tags, trash
- ✓ `docs/user/files/SYNC_CLIENT.md` — install sync client, connect, selective sync, conflicts
- ✓ `docs/user/files/DOCUMENT_EDITING.md` — online editing with Collabora, auto-save, collaboration

**Inline Documentation (2 items):**
- ✓ XML documentation (`///`) on all public types and methods (already present from implementation phases)
- ✓ README added to each Files project root (`Files`, `Files.Data`, `Files.Host`)

**Notes:** Phase 1.20 complete. All 20 documentation deliverables created, plus 3 additional cross-cutting server admin docs (INSTALLATION.md, CONFIGURATION.md, UPGRADING.md). Documentation covers developer, admin, and user audiences with accurate references to actual code, APIs, and configuration options.

---

## Phase 2: Chat & Notifications

**Goal:** Real-time messaging + Android app.
**Expected Duration:** 10-14 weeks
**Milestone:** Real-time chat across web, desktop, and Android.

---

### Step: phase-2.1 - Chat Core Abstractions & Data Models
**Status:** completed ✅
**Duration:** ~1 week (actual)
**Description:** Create chat module projects, domain models (Channel, Message, Reaction, Mention, Attachment, PinnedMessage), DTOs, events, event handlers, and ChatModuleManifest.

**Deliverables:**
- ✓ Create project structure (Chat, Chat.Data, Chat.Host, Chat.Tests) — 4 projects added to solution
- ✓ Create ChatModuleManifest implementing IModuleManifest (Id: dotnetcloud.chat, 4 capabilities, 5 published events, 1 subscribed event)
- ✓ Create domain models (Channel, ChannelMember, Message, MessageAttachment, MessageReaction, MessageMention, PinnedMessage) — 7 entities
- ✓ Create enums (ChannelType, ChannelMemberRole, MessageType, MentionType, NotificationPreference) — 5 enums
- ✓ Create DTOs for all entities (ChannelDto, MessageDto, ChannelMemberDto, MessageAttachmentDto, and more)
- ✓ Create events and event handlers (10 events: MessageSent/Edited/Deleted, ChannelCreated/Deleted/Archived, UserJoined/Left, ReactionAdded/Removed + 2 handlers)

**Dependencies:** Phase 0 (complete), Phase 1 (FileNode reference for attachments)
**Blocking Issues:** None
**Notes:** Phase 2.1 complete. All models, DTOs, events, and manifest follow Files module patterns. 78 unit tests passing.

---

### Step: phase-2.2 - Chat Database & Data Access Layer
**Status:** completed ✅
**Duration:** ~1 week
**Description:** Create ChatDbContext, entity configurations, migrations, and database initialization.

**Deliverables:**
- ✓ Create entity configurations (Channel, ChannelMember, Message, MessageAttachment, MessageReaction, MessageMention, PinnedMessage, Announcement, AnnouncementAcknowledgement) — 9 configurations with indexes, FKs, query filters
- ✓ Create ChatDbContext with all DbSets and naming strategy — 9 DbSets
- ✓ Create migrations (PostgreSQL `InitialCreate` + SQL Server `InitialCreate_SqlServer`) with `ChatDbContextDesignTimeFactory`
- ✓ Create ChatDbInitializer — seeds `#general`, `#announcements`, `#random` channels per organization

**Dependencies:** phase-2.1
**Blocking Issues:** None
**Notes:** Phase 2.2 complete. Design-time factory supports both PostgreSQL (default) and SQL Server (via `CHAT_DB_PROVIDER=SqlServer` env var). PostgreSQL migration uses `uuid`, `timestamp with time zone`, `boolean` types. SQL Server migration uses `uniqueidentifier`, `datetime2`, `nvarchar`, `bit` types. ChatDbInitializer seeds 3 default public channels with idempotent check. MariaDB migration deferred (Pomelo lacks .NET 10 support).

---

### Step: phase-2.3 - Chat Business Logic & Services
**Status:** completed ✅
**Duration:** ~2 weeks
**Description:** Implement core chat services: ChannelService, MessageService, ReactionService, PinService, TypingIndicatorService, and ChatModule lifecycle.

**Deliverables:**
- ✓ Implement IChannelService and ChannelService (CRUD, DM creation, authorization, channel name uniqueness validation)
- ✓ Implement IChannelMemberService and ChannelMemberService (add/remove, roles, unread counts)
- ✓ Implement IMessageService and MessageService (send, edit, delete, search, mention parsing, mention notification dispatching)
- ✓ Implement IReactionService and ReactionService
- ✓ Implement IPinService and PinService
- ✓ Implement ITypingIndicatorService (in-memory, time-expiring)
- ✓ Create ChatModule implementing IModule (lifecycle management) — initialize/start/stop/dispose with event bus integration

**Dependencies:** phase-2.2
**Blocking Issues:** None
**Notes:** Phase 2.3 completed with full hardening of `ChannelMemberService`, `ReactionService`, `PinService`, and `TypingIndicatorService`. Authorization boundaries are now enforced for member-management, reaction, and pin operations; deterministic validation paths were added for channel/message mismatch and last-owner protection; unread mention calculations now include `@all` and `@channel`; reaction payloads were normalized and validated for event consistency; pinned-message retrieval now preserves `PinnedAt` ordering and projects mentions consistently. Added/expanded tests in `ChannelMemberServiceTests`, `ReactionServiceTests`, `PinServiceTests`, and `TypingIndicatorServiceTests`. Verification: `dotnet test tests/DotNetCloud.Modules.Chat.Tests/DotNetCloud.Modules.Chat.Tests.csproj` passed (197/197), and full `dotnet build` succeeded. Next: proceed to `phase-2.4` then `phase-2.5`.

---

### Step: phase-2.4 - Chat REST API Endpoints
**Status:** completed ✅
**Duration:** ~1 week
**Description:** Create REST controllers for channels, messages, reactions, pins, and file sharing.

**Tasks:**
- ✓ Create ChannelController (CRUD, archive, DM) implementation scope delivered under consolidated `ChatController`
- ✓ Create MemberController (add/remove, role, notifications, read marker, unread counts) implementation scope delivered under consolidated `ChatController`
- ✓ Create MessageController (send, edit, delete, paginate, search) implementation scope delivered under consolidated `ChatController`
- ✓ Create ReactionController and PinController implementation scope delivered under consolidated `ChatController`
- ✓ Create file attachment endpoints

**Dependencies:** phase-2.3
**Blocking Issues:** None
**Notes:** Phase 2.4 completed with all required REST endpoint scopes implemented under `/api/v1/chat/` via a consolidated `ChatController` (functional equivalent to split-controller scope). Deterministic exception mapping is now enforced for member/reaction/pin/typing denial and validation paths (403/404/400 as appropriate), and controller-level API verification tests were expanded to validate success envelope shape and denial/not-found mapping. Verification: `dotnet test tests/DotNetCloud.Modules.Chat.Tests/DotNetCloud.Modules.Chat.Tests.csproj` passed (206/206), and full `dotnet build` succeeded. Next: proceed to `phase-2.5` (SignalR real-time chat integration).

---

### Step: phase-2.5 - SignalR Real-Time Chat Integration
**Status:** completed ✅
**Duration:** ~1 week
**Description:** Integrate chat with CoreHub for real-time message delivery, typing indicators, presence, and reactions.

**Tasks:**
- ✓ Register chat SignalR methods (SendMessage, EditMessage, DeleteMessage, StartTyping, StopTyping, MarkRead, AddReaction, RemoveReaction)
- ✓ Implement server-to-client broadcasts (NewMessage, MessageEdited, MessageDeleted, TypingIndicator, ReactionUpdated, etc.)
- ✓ Implement SignalR group management per channel membership
- ✓ Extend presence tracking (Online, Away, DND, custom status)

**Dependencies:** phase-2.3, Phase 0.8 (SignalR infrastructure)
**Blocking Issues:** None
**Notes:** Phase 2.5 completed. Final increment added chat presence custom status support and cross-module presence events: `PresenceService` now tracks `StatusMessage` and explicit status transitions (`Online`, `Away`, `DoNotDisturb`, `Offline`) via `SetPresenceAsync`; `CoreHub` now exposes `SetPresenceAsync` and publishes realtime presence updates through `IChatRealtimeService` plus `PresenceChangedEvent` through `IEventBus`; and `ChatModuleManifest` now declares `PresenceChangedEvent` in published events. This completes previously delivered hub method registration and reconnect-safe group lifecycle management. Added/expanded tests in `CoreHubTests` and `PresenceServiceTests`. Verification: `dotnet test tests/DotNetCloud.Core.Server.Tests/DotNetCloud.Core.Server.Tests.csproj` passed (327/329 with 2 skipped), and full `dotnet build` succeeded. Next: proceed to `phase-2.6` (Announcements Module).

---

### Step: phase-2.6 - Announcements Module
**Status:** completed ✅
**Duration:** ~1 week
**Description:** Create announcements module for organization-wide broadcasts with acknowledgement tracking.

**Tasks:**
- ✓ Create Announcement and AnnouncementAcknowledgement models
- ✓ Create IAnnouncementService and implementation (CRUD, acknowledge, list acknowledgements)
- ✓ Create REST endpoints (POST/GET/PUT/DELETE /api/v1/announcements, acknowledge, acknowledgements)
- ✓ Create real-time broadcast via SignalR (new/urgent announcements)

**Dependencies:** phase-2.5
**Blocking Issues:** None
**Notes:** Phase 2.6 completed. Existing announcements domain and service implementation in chat module were fully wired into host APIs with deterministic envelope/error behavior via `ChatController` routes under `/api/v1/announcements` (create/list/get/update/delete/acknowledge/acknowledgements). Real-time announcement delivery now broadcasts `AnnouncementCreated` for all announcements, `UrgentAnnouncement` for urgent priority, and `AnnouncementBadgeUpdated` for live badge count updates through `IRealtimeBroadcaster`. Added controller coverage in `ChatControllerTests` for creation + realtime broadcast behavior, urgent-broadcast behavior, missing-announcement mapping, and acknowledge envelope behavior; updated manifest tests for published presence events. Verification: `dotnet test tests/DotNetCloud.Modules.Chat.Tests/DotNetCloud.Modules.Chat.Tests.csproj` passed (212/212), and full `dotnet build` succeeded. Next: proceed to `phase-2.7` (Push Notifications Infrastructure).

---

### Step: phase-2.7 - Push Notifications Infrastructure
**Status:** completed ✅
**Duration:** ~1-2 weeks
**Description:** Implement push notification service with FCM and UnifiedPush providers, notification routing, and device management.

**Tasks:**
- ✓ Create IPushNotificationService interface and models (PushNotification, DeviceRegistration, PushProvider enum)
- ✓ Implement FcmPushProvider (Firebase Admin SDK, HTTP v1 API, batch sending)
- ✓ Implement UnifiedPushProvider (HTTP POST to distributor endpoint)
- ✓ Create NotificationRouter (provider selection, user preferences, deduplication, queuing)

**Dependencies:** phase-2.3
**Blocking Issues:** None
**Notes:** Phase 2.7 is now complete with post-closeout server hardening. Added queue/reliability background processing with `INotificationDeliveryQueue` and `NotificationDeliveryBackgroundService`, plus router fallback enqueue-on-failure and queued dispatch via `IQueuedNotificationDispatcher`. `NotificationRouter` now attempts direct delivery, queues failed all-provider sends, and background worker retries with bounded exponential backoff. Completed deferred provider configuration models with `FcmPushOptions` and `UnifiedPushOptions`, bound from configuration (`Chat:Push:Fcm`, `Chat:Push:UnifiedPush`) and consumed by providers for enable/attempt behavior. Added provider option tests for disabled FCM and configurable UnifiedPush max attempts. Client SDK binding follow-up is also complete: `ChatApiClient` now exposes push-device register/unregister and notification preference get/update methods with shared push DTOs for UI/desktop callers, and the chat UI now includes `NotificationPreferencesPanel` for push enable/disable, DND, and per-channel mutes wired to GET/PUT preferences endpoints. Verification: `dotnet test tests/DotNetCloud.Modules.Chat.Tests/DotNetCloud.Modules.Chat.Tests.csproj` passed (228/228), and full `dotnet build` succeeded. Next: proceed to phase-2.8 remaining chat web UI work.

---

### Step: phase-2.8 - Chat Web UI (Blazor)
**Status:** in-progress 🔄
**Duration:** ~2-3 weeks
**Description:** Create Blazor chat UI components: channel list, message list, composer, typing indicators, member panel, settings, DM view, and announcement components.

**Deliverables:**
- ✓ Create ChannelList.razor (sidebar, unread counts, search/filter, create channel dialog, active highlight)
- ✓ Create ChannelHeader.razor (name, topic, member count, member list toggle, search)
- ✓ Create MessageList.razor (avatars, timestamps, reactions, attachments, typing indicator, infinite scroll, system messages, edited indicator)
- ✓ Create MessageComposer.razor (emoji picker, file attach, reply-to preview, send/Enter, typing broadcast, Markdown toolbar, @mention autocomplete, paste image support)
- ☐ Create TypingIndicator.razor (animated dots, auto-expire)
- ☐ Create MemberListPanel.razor (grouped by role, status, actions)
- ☐ Create ChannelSettingsDialog.razor (edit, members, notifications, archive/delete)
- ✓ Create DirectMessageView.razor (user search, DM list, group DM)
- ✓ Add DM user search/start flow in DirectMessageView (filter `UserSuggestions`, call DM create, raise `OnDmChannelReady`)
- ☐ Create ChatNotificationBadge.razor (total unread, real-time update)
- ☐ Create AnnouncementBanner.razor, AnnouncementList.razor, AnnouncementEditor.razor
- ☐ Register chat UI components with ModuleUiRegistry

**Dependencies:** phase-2.5, Phase 0.11 (Blazor shell), Phase 0.12 (shared UI components)
**Blocking Issues:** None
**Notes:** Phase 2.8 now includes complete direct-message composition and escalation flow in `DirectMessageView`, plus pinned-channel drag-to-reorder, paste image support, Markdown toolbar, and `@mention` autocomplete. `DirectMessageView` now supports New DM user search (`GetOrCreateDmAsync` + `OnDmChannelReady`) and group-DM escalation with an Add people picker that calls `AddMemberAsync` on the active DM channel and updates header state to show a Group indicator once member count exceeds two. Added targeted component-state coverage in `DirectMessageViewTests` for user filtering, DM-ready callback flow, and group-member add behavior/member-count transition. `ChatNotificationBadge` mention tracking complete: `ISignalRChatService` now exposes `MentionCountUpdated` (separate from `UnreadCountUpdated`); badge tracks `_mentionsByChannel` independently and `HasMentions` is now `TotalMentions > 0` (not `TotalUnread > 0`), correctly distinguishing mentions from regular unreads. `AnnouncementEditor` preview test coverage added (`AnnouncementEditorTests.cs`, 7 tests covering toggle, save-disabled guard, field population from announcement, and field reset). Latest verification: `dotnet test tests/DotNetCloud.Modules.Chat.Tests/DotNetCloud.Modules.Chat.Tests.csproj` passed (263/263), `dotnet build` succeeded. Remaining phase-2.8 work: typing indicator/member/settings components.

---

### Step: phase-2.9 - Desktop Client Chat Integration
**Status:** in-progress
**Duration:** ~1 week
**Description:** Add chat notifications, tray icon badges, and quick reply to the existing SyncTray desktop application.

**Tasks:**
- ✓ Add chat notification popups with message preview and channel-aware titles (current implementation: Windows toast + Linux libnotify)
- ✓ Add DND/mute suppression (Settings-backed mute toggle + tray popup guard)
- ✓ Add click-to-open chat browser action (`/apps/chat`) from activated notifications
- ✓ Migrate Windows notifications from balloon tips to Windows toast notifications
- ✓ Add notification grouping behavior (toast tag/group on Windows, grouped/replaced notifications on Linux)
- ✓ Implement tray unread aggregation path (`IChatSignalRClient`, `ChatUnreadCount`, `ChatHasMentions`, tooltip unread summary, SignalR-driven clear)
- ☐ Implement tray icon mention-vs-message visual badge state
- ☐ Add quick reply popup from notification
- ☐ Send quick reply via REST API/client-core chat transport
- ☐ Show typing indicator while composing quick reply

**Dependencies:** phase-2.5, Phase 1 (SyncTray exists)
**Blocking Issues:** Phase 1 must be complete (desktop client exists)
**Notes:** Step 5a through Step 5d complete. Added `IChatSignalRClient` contract in `DotNetCloud.Client.Core`; wired SyncTray `TrayViewModel` to subscribe to unread-count updates (aggregate totals + mention flag + tooltip summary) and to `OnNewChatMessage` for popups with channel-aware titles and message preview body. Added `NotificationType.Chat`/`NotificationType.Mention` and mapped platform behavior: Linux uses libnotify urgency/icon mapping and Windows now uses a toast-backed path (PowerShell-hosted WinRT toast payload) instead of `Shell_NotifyIcon` balloon tips; mention/warning/error retain higher-priority attribution text. Added DND/mute handling with `SettingsViewModel.IsMuteChatNotifications` persisted to local `sync-tray-settings.json`, propagated into `TrayViewModel.IsMuteChatNotifications`, and enforced in chat popup path. Click-to-open remains available via notification action URL on Linux and via toast protocol action on Windows. Notification grouping is now wired per channel: `INotificationService.ShowNotification` includes optional `groupKey`/`replaceKey`, `TrayViewModel` now passes deterministic `chat-channel-<channelId>` keys, Windows sets toast `Group` + `Tag`, and Linux uses `notify-send` grouping hints plus stable `--replace-id` mapping keyed by `replaceKey`. Phase 2.9 regression checklist pass completed on 2026-03-10 via full-suite run: `dotnet test` reported `2013 total, 0 failed, 2000 passed, 13 skipped`, with explicit coverage across ChannelList/unread/presence paths, reaction + typing flows, announcement lifecycle and preferences, DM/group-DM flows, SyncTray chat popup + mute/DND + action URL behavior, and existing sync/conflict/transfer paths. Release-hardening pass is complete for current chat UX surfaces. Planning pivot on 2026-03-10: the user wants full quick-reply support and does not want Windows grouping limited to a balloon-tip approximation, so remaining Phase 2.9 work now explicitly includes tray mention-vs-message badge state and quick-reply plumbing (send path + typing indicator). Latest validation after grouping implementation: `dotnet build` succeeded and `dotnet test tests/DotNetCloud.Client.SyncTray.Tests/DotNetCloud.Client.SyncTray.Tests.csproj` passed (`54 total, 54 succeeded, 0 failed`).

---

### Step: phase-2.10 - Android MAUI App
**Status:** pending
**Duration:** ~3-4 weeks
**Description:** Create Android MAUI app with authentication, chat UI, SignalR real-time, push notifications, offline support, and photo auto-upload.

**Tasks:**
- ☐ Create DotNetCloud.Clients.Android MAUI project (build flavors: googleplay/fdroid)
- ☐ Implement authentication (OAuth2/OIDC, token storage, refresh)
- ☐ Create chat UI views (channel list, message list, composer, channel details)
- ☐ Implement SignalR client with background connection (foreground service, Doze mode)
- ☐ Integrate push notifications (FCM for googleplay, UnifiedPush for fdroid)
- ☐ Implement offline support (local cache, message queue, sync on reconnect)
- ☐ Create photo auto-upload (MediaStore observer, chunked upload, WiFi/battery config)
- ☐ Configure distribution (Google Play, F-Droid, direct APK)

**Dependencies:** phase-2.5, phase-2.7
**Blocking Issues:** None
**Notes:** Largest step in Phase 2. Can parallelize UI work and push notification work.

---

### Step: phase-2.11 - Chat Module gRPC Host
**Status:** completed ✅
**Duration:** ~0.5 weeks (actual)
**Description:** Create gRPC service definitions and implementation for chat module inter-process communication.

**Deliverables:**
- ✓ Create chat_service.proto (10 RPCs: CreateChannel, GetChannel, ListChannels, SendMessage, GetMessages, EditMessage, DeleteMessage, AddReaction, RemoveReaction, NotifyTyping)
- ✓ Implement ChatGrpcService (full CRUD), ChatLifecycleService (init/start/stop/health/manifest), ChatHealthCheck (ASP.NET Core IHealthCheck)
- ✓ Configure Program.cs (InMemory ChatDbContext, gRPC services, REST controllers, health checks)

**Dependencies:** phase-2.3, Phase 0.6 (gRPC infrastructure)
**Blocking Issues:** None
**Notes:** Complete. ChatController REST API also created with channels, messages, and members endpoints.

---

### Step: phase-2.12 - Testing Infrastructure
**Status:** in-progress 🔄
**Duration:** ~1-2 weeks
**Description:** Create comprehensive unit tests and integration tests for all chat functionality.

**Deliverables:**
- ✓ Create unit tests — 180 tests passing across 10 test classes:
  - ✓ ChatModuleManifestTests (10 tests: Id, Name, Version, capabilities, events, IModuleManifest)
  - ✓ ChatModuleTests (15 tests: lifecycle, event bus subscribe/unsubscribe, null check, manifest)
  - ✓ ModelTests (35 tests: Channel 10, Message 10, ChannelMember 7, MessageReaction 3, MessageMention 5)
  - ✓ EventTests (18 tests: 10 event records IEvent compliance + 8 event handler tests)
  - ✓ ChannelServiceTests (CRUD, authorization, name uniqueness)
  - ✓ MessageServiceTests (29 tests: send, edit, delete, pagination, search, mentions, attachments)
  - ✓ ReactionServiceTests (7 tests: add, remove, duplicate, multi-user, grouping, validation)
  - ✓ PinServiceTests (5 tests: pin, unpin, duplicate, non-pinned, empty list)
  - ✓ TypingIndicatorServiceTests (5 tests: notify, empty, multi-user, channel isolation, cleanup)
  - ✓ AnnouncementServiceTests (18 tests: CRUD, priority, acknowledgement tracking)
  - ✓ MentionNotificationServiceTests
- ☐ Create integration tests (REST API CRUD, SignalR real-time delivery, typing, presence, file attachment, announcements, push registration, multi-database)

**Dependencies:** phase-2.1 through phase-2.11
**Blocking Issues:** None
**Notes:** Unit tests complete with 180/180 passing across all service, model, event, and module tests. Integration tests will be added as SignalR integration is implemented.

---

### Step: phase-2.13 - Documentation
**Status:** pending
**Duration:** ~1 week
**Description:** Create comprehensive documentation for chat module, Android app, and push notifications.

**Tasks:**
- ☐ Create chat module docs (README, API reference, architecture, real-time events, push notifications)
- ☐ Create Android app docs (README, setup, distribution)
- ☐ Add XML documentation to all public types

**Dependencies:** phase-2.1 through phase-2.12
**Blocking Issues:** None
**Notes:** Complete before Phase 2 sign-off.

---

**Last Updated:** 2026-03-05 (Phase 2.1 complete, 2.2/2.3/2.8/2.11/2.12 in progress)
**Next Review:** Phase 2.3 service implementations
**Maintained By:** Development Team

---

## How to Use This Plan

## Sync Improvement Plan Execution

### Step: sync-batch-1.1 - Sync Service Logging (Client)
**Status:** completed ✅
**Duration:** ~1 hour
**Description:** Add structured Serilog JSON logging to DotNetCloud client sync service and core sync lifecycle components.

**Deliverables:**
- ✓ Added `Serilog.AspNetCore`, `Serilog.Sinks.File`, and `Serilog.Formatting.Compact` to `src/Clients/DotNetCloud.Client.SyncService/DotNetCloud.Client.SyncService.csproj`
- ✓ Configured `Log.Logger` in `src/Clients/DotNetCloud.Client.SyncService/Program.cs` for rolling JSON file output at `{DataRoot}/logs/sync-service.log`
- ✓ Added configurable logging settings support from `sync-settings.json` (`retentionDays`, `maxFileSizeMB`, `minimumLevel`)
- ✓ Added Linux owner-only (`600`) log-file permissions via `File.SetUnixFileMode()` on service startup
- ✓ Added structured sync pass start/complete/error and watcher-trigger logs in `src/Clients/DotNetCloud.Client.Core/Sync/SyncEngine.cs`
- ✓ Added structured file upload/download start/complete/error logs (size + duration) in `src/Clients/DotNetCloud.Client.Core/Transfer/ChunkedTransferClient.cs`
- ✓ Added conflict detection reason logging in `src/Clients/DotNetCloud.Client.Core/Conflict/ConflictResolver.cs`
- ✓ Added token refresh success/failure logging in `src/Clients/DotNetCloud.Client.Core/Auth/OAuth2Service.cs` (no token value logging)
- ✓ Added IPC command received logging in `src/Clients/DotNetCloud.Client.SyncService/Ipc/IpcClientHandler.cs`
- ✓ Added `src/Clients/DotNetCloud.Client.SyncService/sync-settings.json` with default logging configuration

**Notes:**
- VALIDATED on `Windows11-TestDNC` at commit `c69aeac` (2026-03-08).
- `dotnet restore` and `dotnet build` passed with no errors.
- Service started, loaded 1 persisted context, IPC server started on Named Pipe — all logged as structured JSON to `%APPDATA%\DotNetCloud\logs\sync-service20260308.log`.
- Graceful shutdown sequence also fully logged.
- Server-side audit logging (Task 1.1b) completed on `mint22`.

### Step: sync-batch-1.1b - Sync Audit Logging (Server)
**Status:** completed ✅
**Duration:** ~30 minutes
**Description:** Add structured Serilog audit logging to server sync/file operations with a dedicated audit log file sink.

**Deliverables:**
- ✓ Added `ILogger<FilesController>` injection with structured audit logs for `file.uploaded`, `file.downloaded`, `file.deleted`, `file.moved`, `file.renamed` events
- ✓ Added `ILogger<SyncController>` injection with structured audit log for `sync.reconcile.completed` (includes UserId, ChangeCount, DurationMs)
- ✓ Added `AuditFilePath` option to `SerilogOptions` (default: `logs/audit-sync-.log`)
- ✓ Added dedicated audit log file sink in `SerilogConfiguration.cs` filtering on `file.*` and `sync.*` message templates
- ✓ Audit sink uses same rolling-file settings (daily, 31-day retention) as main log sink

**Notes:**
- Build succeeded with 0 warnings, 0 errors.
- All 513 Files module tests pass.
- Rename handler fetches old name before rename to capture `OldName` → `NewName` in audit log.
- Ready for Task 1.2 client side (Windows — `DotNetCloudApiClient` `DelegatingHandler`).

### Step: sync-batch-1.2-client - Request Correlation IDs (Client)
**Status:** completed ✅
**Duration:** ~20 minutes
**Description:** Add `CorrelationIdHandler` DelegatingHandler to attach `X-Request-ID` to every outgoing API call and log it.

**Deliverables:**
- ✓ Created `src/Clients/DotNetCloud.Client.Core/Api/CorrelationIdHandler.cs`
  - Generates `Guid.NewGuid()` per request, attaches as `X-Request-ID` header
  - Logs outgoing call: Method, URL, RequestId (Information level)
  - Logs failure: RequestId, StatusCode (Error level) on non-2xx responses
- ✓ Registered as `AddTransient<CorrelationIdHandler>()` in `ClientCoreServiceExtensions`
- ✓ Added `.AddHttpMessageHandler<CorrelationIdHandler>()` to typed `DotNetCloudApiClient` HttpClient
- ✓ Registered in `SyncServiceExtensions` on named `"DotNetCloudSync"` HttpClient (used by `SyncContextManager`)

**Notes:**
- Commit `97afdd8` on `Windows11-TestDNC` (2026-03-08).
- Build succeeded with 0 errors on `DotNetCloud.Client.SyncService`.
- `sync-now` IPC triggered successfully (`"success":true,"data":{"started":true}`).
- RequestId log entries will appear on server-reachable sync passes.

### Step: sync-batch-1.2-server - Request Correlation IDs (Server)
**Status:** completed ✅
**Duration:** ~20 minutes
**Description:** Add `RequestCorrelationMiddleware` to propagate `X-Request-ID` headers, linking client and server logs.

**Deliverables:**
- ✓ Created `src/Core/DotNetCloud.Core.ServiceDefaults/Middleware/RequestCorrelationMiddleware.cs`
- ✓ Reads `X-Request-ID` from incoming request; generates compact GUID if absent
- ✓ Sets `HttpContext.TraceIdentifier` so `RequestResponseLoggingMiddleware` and Serilog enricher pick it up automatically
- ✓ Echoes ID back on response via `X-Request-ID` header
- ✓ Registered as first middleware in `UseDotNetCloudMiddleware()` (before security headers)

**Notes:**
- Build succeeded with 0 warnings, 0 errors (ServiceDefaults + Server).
- Client side (Task 1.2) next: add `X-Request-ID` in `DotNetCloudApiClient` / `DelegatingHandler` on Windows.

### Step: sync-batch-1.3 - Server-Side Rate Limiting on Sync Endpoints
**Status:** completed ✅
**Duration:** ~30 minutes
**Description:** Apply `[EnableRateLimiting]` attributes to sync and file controller endpoints using existing rate limiting infrastructure.

**Deliverables:**
- ✓ `src/Core/DotNetCloud.Core.Server/appsettings.json` — `RateLimiting.ModuleLimits` populated: `sync-changes` (60/min), `sync-tree` (10/min), `sync-reconcile` (30/min), `upload-initiate` (30/min), `upload-chunks` (300/min), `download` (120/min), `chunks` (300/min)
- ✓ `src/Modules/Files/DotNetCloud.Modules.Files.Host/Controllers/SyncController.cs` — `[EnableRateLimiting]` on `GetChanges`, `GetTree`, `Reconcile`
- ✓ `src/Modules/Files/DotNetCloud.Modules.Files.Host/Controllers/FilesController.cs` — `[EnableRateLimiting]` on `InitiateUpload`, `UploadChunk`, `Download`, `GetChunkManifest`, `DownloadChunkByHash`

**Notes:**
- Commit `4570c16` on `mint22` (2026-03-08). Build: 0 errors; 304 server tests passed.
- Client required no changes — `SendWithRetryAsync()` already handles 429 + `Retry-After`.

### Step: sync-batch-1.4 - Chunk Integrity Verification on Download
**Status:** completed ✅
**Duration:** ~30 minutes
**Description:** Add SHA-256 post-download verification for every chunk in `ChunkedTransferClient`, with 3 retries on mismatch and `ChunkIntegrityException` on persistent failure.

**Deliverables:**
- ✓ Created `src/Clients/DotNetCloud.Client.Core/Transfer/ChunkIntegrityException.cs`
- ✓ `src/Clients/DotNetCloud.Client.Core/Transfer/ChunkedTransferClient.cs` — `DownloadChunksAsync()` now verifies `SHA256.HashData(bytes)` against manifest hash after each chunk download
- ✓ Retry loop (max 3 attempts) with `LogWarning` on mismatch
- ✓ `LogError` + `ChunkIntegrityException` thrown if all 3 attempts fail
- ✓ Existing test updated: `DownloadAsync_WithManifest_DownloadsChunks` uses real SHA-256 hash
- ✓ New tests: `DownloadAsync_ChunkHashMismatch_RetriesAndSucceeds`, `DownloadAsync_ChunkHashAlwaysMismatch_ThrowsChunkIntegrityException`

**Notes:**
- Build: 0 errors. All 55 `DotNetCloud.Client.Core.Tests` pass (including 3 new/updated transfer tests).

---

### Step: sync-batch-1.5 - Per-Chunk Retry with Exponential Backoff (Client)
**Status:** completed ✅
**Deliverables:**
- ✓ Per-chunk retry loop (max 3 attempts) with exponential backoff + jitter in `ChunkedTransferClient`
- ✓ `ShouldRetryChunk()` — retry on network/5xx/hash-mismatch, NOT on 4xx/429/cancellation
- ✓ Detailed per-chunk logging (hash, attempt, delay, error)

**Notes:** Commit `1aa6b18` on `Windows11-TestDNC` (2026-03-08). 64 tests pass.

---

### Step: sync-batch-1.6 - SQLite WAL Mode + Corruption Recovery (Client)
**Status:** completed ✅
**Deliverables:**
- ✓ WAL journal mode via `PRAGMA journal_mode=WAL` in `RunSchemaEvolutionAsync`
- ✓ Startup integrity check (`PRAGMA integrity_check`) with automatic recovery
- ✓ Corrupt DB preserves (renamed with timestamp), fresh DB recreated
- ✓ Post-sync WAL checkpoint via `CheckpointWalAsync()` in `SyncEngine`
- ✓ `WasRecentlyReset()` flag for tray notification

**Notes:** Commit `1aa6b18` on `Windows11-TestDNC` (2026-03-08). 64 tests pass.

---

### Step: sync-batch-1.7 - Operation Retry Queue with Backoff (Client)
**Status:** completed ✅
**Deliverables:**
- ✓ `NextRetryAt` + `LastError` columns on `PendingOperationDbRow`
- ✓ `FailedOperationDbRow` entity + DbSet in `LocalStateDbContext`
- ✓ Exponential backoff schedule (1m → 5m → 15m → 1h → 6h) in `SyncEngine.ApplyLocalChangesAsync()`
- ✓ Pending operations filtered by `NextRetryAt` eligibility
- ✓ Logging of retry attempts and permanent failures

**Notes:** Commit `1aa6b18` on `Windows11-TestDNC` (2026-03-08). 64 tests pass.

---

### Step: sync-batch-1.8 - Secure Temp File Handling (Server)
**Status:** completed ✅
**Deliverables:**
- ✓ Dedicated temp directory under `DOTNETCLOUD_DATA_DIR/tmp/` with `chmod 700` on Linux
- ✓ `DownloadService` uses app-specific temp dir instead of `Path.GetTempPath()`
- ✓ `TempFileCleanupService` (`IHostedService`) deletes files older than 1 hour on startup

**Notes:** Commit `82ca53b` on `mint22` (2026-03-08).

---

### Step: sync-batch-1.9 - Server-Side File Scanning Interface (Server)
**Status:** completed ✅
**Deliverables:**
- ✓ Chunk storage files have `600` permissions (no execute bits) after write
- ✓ `X-Content-Type-Options: nosniff` + `Content-Disposition: attachment` on download endpoints
- ✓ `IFileScanner` interface + `NoOpFileScanner` default implementation registered in DI
- ✓ `ScanStatus` nullable enum field on `FileVersion` model
- ✓ Configurable max file size (`FileUpload:MaxFileSizeBytes = 15GB`) enforced in `InitiateUploadAsync()`

**Notes:** Commit `82ca53b` on `mint22` (2026-03-08).

---

### Step: sync-batch-2 - Efficiency: Bandwidth Savings (All)
**Status:** completed ✅
**Duration:** ~2 days
**Description:** Six efficiency improvements reducing bandwidth and memory use: FastCDC chunking, streaming pipeline, Brotli/Gzip compression, monotonic sync cursor, paginated changes, and ETag chunk caching.

**Deliverables:**
- ✓ Task 2.1: FastCDC content-defined chunking in `ContentHasher` + `ChunkedTransferClient`; `Offset`+`ChunkSize` on `FileVersionChunk`; `ChunkSizes` in upload DTOs. Server `3a7e0ae`, Client `bc9e08a`.
- ✓ Task 2.2: Bounded `Channel<ChunkData>` producer-consumer pipeline in `ChunkedTransferClient`; disk-based download assembly. Client `2e0788c`.
- ✓ Task 2.3: Response compression (Brotli + Gzip) on server; `AutomaticDecompression = All` + gzip upload wrapping on client. Server `032f6a2`, Client (2026-03-08).
- ✓ Task 2.4: `UserSyncCounter` table + `SyncSequence` on `FileNode`; cursor-based `GetChangesSinceAsync()`; `SyncCursor` replaces `LastSyncedAt` in client. Server `c81495d`, Client `1a9c4c6`.
- ✓ Task 2.5: `limit` param + `hasMore`/`nextCursor` on changes endpoint; pagination loop in client `ApplyRemoteChangesAsync()` with per-page cursor persistence. Server `c81495d`, Client `1a9c4c6`.
- ✓ Task 2.6: `ETag` + `If-None-Match` → `304` on chunk download endpoint; client sends `If-None-Match` and handles `304`. Server `c81495d`, Client `1a9c4c6`.

**Notes:** All Batch 2 tasks complete. Bandwidth savings most visible on repeated syncs of large modified files (CDC) and compressible content (text/code/documents).

---

### Step: sync-batch-3 - User Experience (Client)
**Status:** completed ✅
**Duration:** ~2 days
**Description:** Six UX improvements: .syncignore pattern parsing, crash-resilient upload resumption, locked file handling, per-file transfer progress, conflict resolution UI with auto-resolution engine, and idempotent uploads.

**Deliverables:**
- ✓ Task 3.1: `SyncIgnoreParser` with `.gitignore`-compatible glob matching; built-in OS/temp/VCS defaults; "Ignored Files" settings panel. Commit `a9c6812`.
- ✓ Task 3.2: `ActiveUploadSessionRecord` for crash-resilient upload resumption; startup resume logic with server session validation. Commit `4243328`.
- ✓ Task 3.3: 4-tier locked file handling (shared-read, retry, VSS/`ILockedFileReader`, defer); `SyncStateTag.Deferred`; tray notification. Commit `b971551`.
- ✓ Task 3.4: `transfer-progress` + `transfer-complete` IPC events; `ActiveTransfersViewModel`; throttled progress events (max 2/sec). Commit `7f93226`.
- ✓ Task 3.5: `ConflictRecord` entity; auto-resolution pipeline (5 strategies); three-pane merge editor (line-based `DiffPlex` + XML-aware `Microsoft.XmlDiffPatch`); conflict tray icon/badge/notifications. Commit `8508afc`.
- ✓ Task 3.6: Pre-upload content hash comparison; skip upload when server hash matches; idempotent operations. Commit `3504932`. 119 tests pass.

**Notes:** All Batch 3 tasks complete.

---

### Step: sync-batch-4 - Cross-Platform Hardening (Both)
**Status:** completed ✅
**Duration:** ~1 day
**Description:** Five platform-hardening tasks ensuring correct sync between Linux and Windows clients sharing the same server account.

**Deliverables:**
- ✓ Task 4.1: Case-insensitive uniqueness check on server file create/rename; `409 Conflict` response; client renames incoming files with `(case conflict)` suffix. Server pre-existing + Client `3504932`.
- ✓ Task 4.2: `PosixMode`+`PosixOwnerHint` on `FileNode`/`FileVersion`; included in all DTOs/gRPC; Linux client reads/applies permissions; setuid/setgid safety policy. Server `fa097bf`, Client `c70bd47`.
- ✓ Task 4.3: Symlink detection (ignore by default); opt-in `sync-as-link` mode; symlink metadata-only upload; secure relative-only validation; settings UI. Server `d3a6422`, Client `1cd594a`.
- ✓ Task 4.4: inotify limit + instance tracking + dynamic auto-fix with polkit; graceful fallback to fast periodic scan; inode usage check via `statvfs()`; server health check `degraded` on low inotify/inode. Server `d3a6422`, Client `1cd594a`.
- ✓ Task 4.5: `longPathAware` app manifest; `\\?\` prefix fallback on Windows; `SyncStateTag.PathTooLong`; filename byte-length check on Linux (255-byte limit); server rejects Windows-illegal chars + reserved names. Server `d3a6422`, Client `1cd594a`.

**Notes:** All Batch 4 tasks complete. 119 tests pass.

---

### Step: sync-batch-5 - Polish (Client)
**Status:** completed ✅
**Duration:** ~1 day
**Description:** Two quality-of-life improvements: bandwidth throttling and a selective sync folder browser.

**Deliverables:**
- ✓ Task 5.1: `ThrottledStream` (token bucket); `ThrottledHttpHandler` (DelegatingHandler); `SyncContext.UploadLimitKbps`/`DownloadLimitKbps`; `sync-settings.json` bandwidth section; IPC persist path via `SyncContextManager`; 6 unit tests.
- ✓ Task 5.2: `FolderBrowserItemViewModel` (three-state check + bubble-up); `FolderBrowserViewModel` (full tree load + save to `SelectiveSyncConfig`); `FolderBrowserView.axaml` + `FolderBrowserDialog.axaml`; add-account flow integration; Settings → Accounts → "Choose folders" button; 4 unit tests.

**Notes:** All Batch 5 tasks complete. All 5 sync improvement batches now closed. Commit range `bbf8c6e` on main (2026-03-09). 15 new tests (4 stream + 11 browser/settings). Build: 0 errors, 3 pre-existing warnings.

---

### Step: sync-verification - Sync Verification & Remediation
**Status:** completed ✅
**Duration:** ~2 hours
**Description:** Systematic verification of all 28 sync improvement tasks across Batches 1-5 using SYNC_VERIFICATION_PLAN.md. Identified gaps (4 Critical, 10 Partial) and resolved all of them — most were already implemented but the plan was outdated.

**Deliverables:**
- ✓ Verified 3/4 "Critical Gaps" already implemented (Task 2.3 compression skip, Task 2.6 ETag/If-None-Match, Task 4.1 case-sensitivity)
- ✓ Verified 7/9 "Partial" items already implemented in code
- ✓ Implemented Three-Pane Merge Editor (Task 3.5e): `MergeEditorViewModel`, `MergeEditorWindow.axaml`, `DiffLineViewModel`, `DiffLineConverters`, DiffPlex integration
- ✓ Implemented 24-hour conflict re-notification timer (Task 3.5c) in `TrayViewModel`
- ✓ Integrated merge editor into `ConflictViewModel` (MergeCommand + CanMerge property) and `SettingsWindow.axaml` (Merge button)
- ✓ 16 unit tests for `MergeEditorViewModel` (all pass)
- ✓ Updated SYNC_VERIFICATION_PLAN.md: 28/28 tasks now ✓ (was 20✓/10⚠/4✗)

**Notes:** Verification complete. All 28 sync improvement tasks confirmed implemented. Build: 0 errors. Tests: 1063/1065 passed (2 expected Linux-only failures). Merge editor uses DiffPlex for line-level diffs with three-way auto-merge and conflict markers for overlapping changes.

---

## Ongoing Management

This plan is a living document and will evolve as the project progresses. Regularly review and
update the plan to reflect the current state of the project, adjust estimates, and add new tasks
or phases as needed. Use this plan to communicate progress, roadblocks, and changes to all
stakeholders.

---

## Appendix

### A. References
- [Git Flow](https://nvie.com/posts/a-successful-git-branching-model/)
- [Semantic Versioning](https://semver.org/)
- [API Versioning in ASP.NET Core](https://docs.microsoft.com/en-us/aspnet/core/web-api/overview)
- [OpenID Connect & OAuth 2.0 Protocol](https://oauth.net/2/)
- [SAML 2.0 Specification](https://docs.oasis-open.org/security/saml/v2.0/saml-core-2.0-os.pdf)

### B. Tools & Technologies
- **Programming Languages:** C# 10
- **Framework:** .NET 6
- **Database:** PostgreSQL 14, SQL Server 2019, MariaDB 10.5
- **ORM:** EF Core 6
- **API:** ASP.NET Core 6
- **Authentication:** OpenIddict, ASP.NET Core Identity
- **Logging:** Serilog
- **Monitoring:** OpenTelemetry
- **Containerization:** Docker, Docker Compose
- **IDEs:** Visual Studio 2022, JetBrains Rider, VS Code
- **Operating Systems:** Windows 10/11, Ubuntu 20.04+, macOS Monterey+
