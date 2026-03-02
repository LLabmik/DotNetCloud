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
| Phase 0.1 | 11 | 10 | 0 | 1 |
| Phase 0.2 | 12 | 10 | 0 | 2 |
| Phase 0.3 | 8 | 8 | 0 | 0 |
| Phase 0.4 | 20 | 0 | 0 | 20 |
| Phase 0.5 | 9 | 9 | 0 | 0 |
| Phase 0.6 | 13 | 0 | 0 | 13 |
| Phase 0.7 | 16 | 0 | 0 | 16 |
| Phase 0.8 | 11 | 0 | 0 | 11 |
| Phase 0.9 | 13 | 0 | 0 | 13 |
| Phase 0.10 | 11 | 0 | 0 | 11 |
| Phase 0.11 | 18 | 0 | 0 | 18 |
| Phase 0.12 | 25 | 0 | 0 | 25 |
| Phase 0.13 | 20 | 0 | 0 | 20 |
| Phase 0.14 | 12 | 0 | 0 | 12 |
| Phase 0.15 | 11 | 11 | 0 | 0 |
| Phase 0.16 | 9 | 0 | 0 | 9 |
| Phase 0.17 | 10 | 0 | 0 | 10 |
| Phase 0.18 | 8 | 0 | 0 | 8 |
| Phase 0.19 | 9 | 0 | 0 | 9 |
| Phase 1-9 | Summary | 0 | 0 | 1 |
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
**Status:** pending  
**Duration:** ~2.5 hours  
**Description:** Create comprehensive tests for data models and DbContext

**Recommended Prompt:**
```
Execute phase-0.2.12: Create comprehensive data access tests. Write entity relationship tests, 
soft-delete tests, query filter tests, integration tests for all three database engines using Docker, 
and DbInitializer tests. Use in-memory database for unit tests, Docker containers for integration. 
Target 80%+ code coverage.
Location: tests/DotNetCloud.Core.Data.Tests/
```

**Deliverables:**
- ☐ Entity relationship tests
- ☐ Soft-delete tests
- ☐ Query filter tests
- ☐ Migration integration tests (all 3 databases)
- ☐ DbInitializer tests

**File Location:** `tests/DotNetCloud.Core.Data.Tests/`  
**Dependencies:** phase-0.2.9, phase-0.2.10, phase-0.2.11  
**Testing:** 80%+ coverage, Docker multi-database testing  
**Notes:** Must pass on all three database engines

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

**Phase 0.3 Summary:**
- ✅ All 8 steps completed
- ✅ 17 classes/interfaces implemented
- ✅ Comprehensive logging with Serilog (structured, enriched, filtered)
- ✅ Health checks for system and modules (Kubernetes-ready)
- ✅ OpenTelemetry metrics and distributed tracing (OTLP export)
- ✅ Security middleware (CORS, headers, HTTPS)
- ✅ Global exception handling (consistent API errors)
- ✅ Request/response logging (PII-masked)
- ✅ One-line integration via extension methods
- ✅ Complete documentation with examples
- ✅ Production-ready with sensible defaults

**Next Phase:** Phase 0.4 - Authentication & Authorization (OpenIddict, ASP.NET Core Identity)

---

## Status Summary & Notes

- **Total Phase 0 Steps:** 228+ (across subsections 0.1-0.19)
- **Estimated Duration:** 16-20 weeks for complete Phase 0
- **Critical Path:** 0.1 → 0.2 → 0.3 → 0.4 → (0.5-0.19 can parallelize somewhat)
- **Blocking Issues:** None currently
- **Assumptions:** .NET 10, PostgreSQL/SQL Server/MariaDB support required
- **Reference:** Complete detailed task breakdowns in `/docs/IMPLEMENTATION_CHECKLIST.md`

---

**Last Updated:** 2026-03-02 (phase pre-impl-1 completed)  
**Next Review:** After Phase 0.1.1 completion  
**Maintained By:** Development Team

---

## How to Use This Plan

This plan is structured as a living document to guide the implementation of the DotNetCloud project
in phases. Each phase is broken down into steps with assigned status, duration, description, tasks,
dependencies, and testing requirements.

**Sections:**
- `Pre-Implementation Setup`: Actions required before the main implementation phases
- `Phase 0`: Foundational work for the entire project, subdivided into sections (0.1 - 0.19)

**Phase Structure:**
Each phase follows a similar structure:
- **Step ID** - Unique identifier for the step
- **Status** - Current status (pending|in-progress|completed|failed|skipped)
- **Duration** - Estimated time to complete
- **Description** - High-level overview of the step
- **Recommended Prompt** - Suggested AI prompt to execute the step
- **Tasks** - Checklist of tasks to complete
- **Dependencies** - Other steps that must be completed first
- **Testing** - How the step will be validated

**Using This Document:**
- Review the `Quick Status Summary` for a high-level overview
- Find your area of work in the detailed phases and steps
- Update the status, add notes, and check off tasks as you work
- Use the `Recommended Prompt` to guide AI assistance for your tasks
- Ensure you meet the `Testing` requirements for your steps

**Maintainers:**
This document is maintained by the Development Team. For questions or suggestions, please contact
your project lead.

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
