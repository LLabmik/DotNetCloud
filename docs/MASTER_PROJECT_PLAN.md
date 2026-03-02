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
| Phase 0.2 | 12 | 3 | 0 | 9 |
| Phase 0.3 | 8 | 0 | 0 | 8 |
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
**Status:** pending  
**Duration:** ~1.5 hours  
**Description:** Create SystemSetting, OrganizationSetting, UserSetting entities

**Recommended Prompt:**
```
Execute phase-0.2.5: Create three-level settings hierarchy. Implement SystemSetting entity (Key, 
Value JSON-serializable, Module, composite key on Module+Key), OrganizationSetting entity (OrganizationId, 
Key, Value, Module), and UserSetting entity (UserId, Key, Value encrypted, Module). Include encryption 
service integration for UserSetting. Add tests for encryption/decryption.
Location: src/Core/DotNetCloud.Core.Data/Entities/Settings/
```

**Deliverables:**
- ☐ `SystemSetting` entity (Key, Value, Module, composite key)
- ☐ `OrganizationSetting` entity (OrganizationId, Key, Value, Module)
- ☐ `UserSetting` entity (UserId, Key, Value encrypted, Module)

**File Location:** `src/Core/DotNetCloud.Core.Data/Entities/Settings/`  
**Dependencies:** phase-0.2.2, phase-0.2.3  
**Testing:** Encryption/decryption tests for UserSetting  
**Notes:** Settings scoped to system, org, and user levels

---

#### Step: phase-0.2.6 - Device & Module Registry Models
**Status:** pending  
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

**Deliverables:**
- ☐ `UserDevice` entity (UserId, Name, DeviceType, PushToken, LastSeenAt)
- ☐ `InstalledModule` entity (ModuleId PK, Version, Status, InstalledAt)
- ☐ `ModuleCapabilityGrant` entity (ModuleId, CapabilityName, GrantedAt, GrantedByUserId)

**File Location:** `src/Core/DotNetCloud.Core.Data/Entities/Modules/`  
**Dependencies:** phase-0.2.2, phase-0.2.4  
**Testing:** Module registry tests  
**Notes:** Tracks installed modules and their capability grants

---

#### Step: phase-0.2.7 - CoreDbContext Configuration
**Status:** pending  
**Duration:** ~3 hours  
**Description:** Create CoreDbContext class and configure all relationships

**Recommended Prompt:**
```
Execute phase-0.2.7: Create CoreDbContext. Implement CoreDbContext class extending 
IdentityDbContext<ApplicationUser, ApplicationRole, Guid> with DbSet properties for all entities. 
Configure all relationships using fluent API, set up automatic timestamps (CreatedAt, UpdatedAt 
via interceptor or value generators), configure soft-delete query filters, and apply table naming 
strategy. Test migration generation.
Location: src/Core/DotNetCloud.Core.Data/CoreDbContext.cs
```

**Deliverables:**
- ☐ `CoreDbContext` class extending `IdentityDbContext<ApplicationUser, ApplicationRole, Guid>`
- ☐ DbSet properties for all entities
- ☐ Fluent API configuration for all relationships
- ☐ Automatic timestamps (CreatedAt, UpdatedAt)
- ☐ Soft-delete query filters
- ☐ Table naming strategy application

**File Location:** `src/Core/DotNetCloud.Core.Data/CoreDbContext.cs`  
**Dependencies:** phase-0.2.2 through phase-0.2.6  
**Testing:** DbContext design tests, migration generation tests  
**Notes:** Critical for all database operations

---

#### Step: phase-0.2.8 - Database Initialization (DbInitializer)
**Status:** pending  
**Duration:** ~2 hours  
**Description:** Create DbInitializer for seeding default data

**Recommended Prompt:**
```
Execute phase-0.2.8: Create DbInitializer service. Implement DbInitializer class with methods for 
database creation, seeding default system roles (Admin, User, Guest, Moderator), seeding default 
permissions (for all modules), and seeding system settings with default config values. Create 
seed data in separate methods for maintainability. Add integration tests.
Location: src/Core/DotNetCloud.Core.Data/DbInitializer.cs
```

**Deliverables:**
- ☐ Database creation logic
- ☐ Seed default system roles (Admin, User, Guest, etc.)
- ☐ Seed default permissions (for all modules)
- ☐ Seed system settings (default config values)

**File Location:** `src/Core/DotNetCloud.Core.Data/DbInitializer.cs`  
**Dependencies:** phase-0.2.7 (CoreDbContext)  
**Testing:** Integration tests with test database  
**Notes:** Runs on first application startup

---

#### Step: phase-0.2.9 - EF Core Migrations (PostgreSQL)
**Status:** pending  
**Duration:** ~1.5 hours  
**Description:** Create initial EF Core migrations for PostgreSQL

**Recommended Prompt:**
```
Execute phase-0.2.9: Create PostgreSQL migrations. Run "dotnet ef migrations add Initial" targeting 
PostgreSQL provider, creating schema structure with core.*, files.*, etc. schemas. Verify indexes, 
constraints, and foreign keys are correctly generated. Ensure idempotency and versioning.
Location: src/Core/DotNetCloud.Core.Data/Migrations/PostgreSQL/
```

**Deliverables:**
- ☐ Initial migration file
- ☐ Schema creation (core.*, files.*, etc.)
- ☐ Index creation
- ☐ Constraint definitions

**File Location:** `src/Core/DotNetCloud.Core.Data/Migrations/PostgreSQL/`  
**Dependencies:** phase-0.2.7, phase-0.2.8  
**Testing:** Migration application test on PostgreSQL database  
**Notes:** Idempotent, version-tracked

---

#### Step: phase-0.2.10 - EF Core Migrations (SQL Server)
**Status:** pending  
**Duration:** ~1.5 hours  
**Description:** Create initial EF Core migrations for SQL Server

**Recommended Prompt:**
```
Execute phase-0.2.10: Create SQL Server migrations. Run migrations targeting SQL Server provider 
with schema structure. Ensure identical schema to PostgreSQL version (same tables, relationships, 
constraints). Verify indexes and foreign keys match PostgreSQL migration.
Location: src/Core/DotNetCloud.Core.Data/Migrations/SqlServer/
```

**Deliverables:**
- ☐ Initial migration file
- ☐ Schema creation
- ☐ Index creation
- ☐ Constraint definitions

**File Location:** `src/Core/DotNetCloud.Core.Data/Migrations/SqlServer/`  
**Dependencies:** phase-0.2.7, phase-0.2.8  
**Testing:** Migration application test on SQL Server database  
**Notes:** Ensure identical schema to PostgreSQL

---

#### Step: phase-0.2.11 - EF Core Migrations (MariaDB)
**Status:** pending  
**Duration:** ~1.5 hours  
**Description:** Create initial EF Core migrations for MariaDB

**Recommended Prompt:**
```
Execute phase-0.2.11: Create MariaDB migrations. Run migrations targeting MariaDB provider using 
table prefix naming strategy. Ensure schema is functionally equivalent to PostgreSQL (same relationships, 
data types, but using table prefixes instead of schemas). Test prefix application.
Location: src/Core/DotNetCloud.Core.Data/Migrations/MariaDB/
```

**Deliverables:**
- ☐ Initial migration file
- ☐ Table prefix naming applied
- ☐ Index creation
- ☐ Constraint definitions

**File Location:** `src/Core/DotNetCloud.Core.Data/Migrations/MariaDB/`  
**Dependencies:** phase-0.2.7, phase-0.2.8  
**Testing:** Migration application test on MariaDB database  
**Notes:** Uses table prefixes instead of schemas

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
**Status:** pending  
**Duration:** ~1.5 hours  
**Description:** Set up Serilog with console and file sinks

**Recommended Prompt:**
```
Execute phase-0.3.1: Configure Serilog logging. Create console sink for development (colorized output), 
file sink for production with daily rolling file strategy and 30-day retention. Implement structured 
logging format with timestamps. Configure log level hierarchy per module. Add context enrichment 
(user ID, request ID, module name). Create extension methods for easy registration.
Location: src/Core/DotNetCloud.Core.ServiceDefaults/Logging/
```

**Deliverables:**
- ☐ Console sink configuration (development)
- ☐ File sink configuration (production with rotation)
- ☐ Structured logging format
- ☐ Log level configuration per module
- ☐ Log context enrichment (user ID, request ID, module name)

**File Location:** `src/Core/DotNetCloud.Core.ServiceDefaults/Logging/`  
**Dependencies:** None  
**Testing:** Logging output validation tests  
**Notes:** Used in all projects via ServiceDefaults

---

#### Step: phase-0.3.2 - Health Checks Infrastructure
**Status:** pending  
**Duration:** ~1.5 hours  
**Description:** Create health check framework for system components

**Recommended Prompt:**
```
Execute phase-0.3.2: Create health checks infrastructure. Implement health check base classes, 
database health check (tests connection), custom health check interface for modules, health check 
endpoints setup (/health, /health/live, /health/ready). Create health status aggregation logic 
to combine statuses from multiple checks.
Location: src/Core/DotNetCloud.Core.ServiceDefaults/HealthChecks/
```

**Deliverables:**
- ☐ Health check infrastructure base classes
- ☐ Database health check implementation
- ☐ Custom health check interface for modules
- ☐ Health check endpoints setup
- ☐ Health status aggregation

**File Location:** `src/Core/DotNetCloud.Core.ServiceDefaults/HealthChecks/`  
**Dependencies:** None  
**Testing:** Health check response format tests  
**Notes:** Supports Kubernetes liveness/readiness probes

---

#### Step: phase-0.3.3 - OpenTelemetry Setup
**Status:** pending  
**Duration:** ~2 hours  
**Description:** Configure metrics collection and distributed tracing

**Recommended Prompt:**
```
Execute phase-0.3.3: Configure OpenTelemetry. Set up metrics collection for HTTP requests, gRPC calls, 
database queries. Implement W3C Trace Context propagation, gRPC interceptor for tracing, HTTP middleware 
for tracing. Configure trace exporters (console for dev, OTLP for production). Create extension methods 
for telemetry registration.
Location: src/Core/DotNetCloud.Core.ServiceDefaults/Telemetry/
```

**Deliverables:**
- ☐ Metrics configuration (HTTP, gRPC, database)
- ☐ W3C Trace Context propagation
- ☐ gRPC interceptor for tracing
- ☐ HTTP middleware for tracing
- ☐ Trace exporter configuration

**File Location:** `src/Core/DotNetCloud.Core.ServiceDefaults/Telemetry/`  
**Dependencies:** Serilog (phase-0.3.1)  
**Testing:** Telemetry output validation  
**Notes:** Foundation for observability

---

#### Step: phase-0.3.4 - Security Middleware
**Status:** pending  
**Duration:** ~1.5 hours  
**Description:** Create CORS and security headers middleware

**Recommended Prompt:**
```
Execute phase-0.3.4: Create security middleware. Implement CORS configuration with origin whitelist 
(configurable), security headers middleware with Content-Security-Policy, X-Frame-Options, 
X-Content-Type-Options, Strict-Transport-Security. Add authorization/authentication middleware 
validation. Create extension methods for easy registration.
Location: src/Core/DotNetCloud.Core.ServiceDefaults/Security/
```

**Deliverables:**
- ☐ CORS configuration with origin whitelist
- ☐ Security headers middleware:
  - ☐ Content-Security-Policy
  - ☐ X-Frame-Options
  - ☐ X-Content-Type-Options
  - ☐ Strict-Transport-Security
- ☐ Authorization/authentication middleware

**File Location:** `src/Core/DotNetCloud.Core.ServiceDefaults/Security/`  
**Dependencies:** None  
**Testing:** Security header presence tests  
**Notes:** Applied to all API endpoints

---

#### Step: phase-0.3.5 - Global Exception Handler Middleware
**Status:** pending  
**Duration:** ~1 hour  
**Description:** Create centralized exception handling middleware

**Recommended Prompt:**
```
Execute phase-0.3.5: Create global exception handler middleware. Implement middleware that catches 
unhandled exceptions, formats them consistently (code, message, details), handles stack traces 
differently in dev vs production, logs errors with context. Return standardized ApiError response.
Location: src/Core/DotNetCloud.Core.ServiceDefaults/Middleware/
```

**Deliverables:**
- ☐ Global exception handler middleware
- ☐ Consistent error response formatting
- ☐ Request validation error handling
- ☐ Stack trace handling (dev vs. production)
- ☐ Error logging integration

**File Location:** `src/Core/DotNetCloud.Core.ServiceDefaults/Middleware/`  
**Dependencies:** Logging (phase-0.3.1)  
**Testing:** Error response format tests  
**Notes:** Catches unhandled exceptions globally

---

#### Step: phase-0.3.6 - Request/Response Logging Middleware
**Status:** pending  
**Duration:** ~1 hour  
**Description:** Create request/response logging middleware with PII masking

**Recommended Prompt:**
```
Execute phase-0.3.6: Create request/response logging middleware. Log request bodies and response bodies 
with configurable verbosity. Implement PII/sensitive data masking (passwords, tokens, SSNs). 
Measure and log request/response timing. Create configuration to enable/disable per route.
Location: src/Core/DotNetCloud.Core.ServiceDefaults/Middleware/
```

**Deliverables:**
- ☐ Request body logging
- ☐ Response body logging
- ☐ PII/sensitive data masking
- ☐ Request/response timing

**File Location:** `src/Core/DotNetCloud.Core.ServiceDefaults/Middleware/`  
**Dependencies:** Logging (phase-0.3.1)  
**Testing:** PII masking validation tests  
**Notes:** Helps with debugging and audit trails

---

#### Step: phase-0.3.7 - ServiceDefaults Integration
**Status:** pending  
**Duration:** ~1.5 hours  
**Description:** Create extension methods for easy middleware registration

**Recommended Prompt:**
```
Execute phase-0.3.7: Create ServiceDefaults integration layer. Implement AddServiceDefaults() extension 
for IServiceCollection to register all logging, telemetry, and health checks. Implement UseServiceDefaults() 
extension for IApplicationBuilder to add all middleware. Create feature flags to enable/disable 
individual components.
Location: src/Core/DotNetCloud.Core.ServiceDefaults/ServiceCollectionExtensions.cs
```

**Deliverables:**
- ☐ `AddServiceDefaults()` extension method
- ☐ `UseServiceDefaults()` extension method
- ☐ Feature flag for middleware enablement

**File Location:** `src/Core/DotNetCloud.Core.ServiceDefaults/ServiceCollectionExtensions.cs`  
**Dependencies:** phase-0.3.1 through phase-0.3.6  
**Testing:** Service registration tests  
**Notes:** Simplifies Program.cs setup

---

#### Step: phase-0.3.8 - Service Defaults Unit Tests
**Status:** pending  
**Duration:** ~1.5 hours  
**Description:** Create tests for all cross-cutting concerns

**Recommended Prompt:**
```
Execute phase-0.3.8: Create ServiceDefaults test suite. Write logging configuration tests, health check 
response format tests, telemetry emission tests, security header presence tests, exception handling tests. 
Aim for 80%+ coverage. Test both individual components and integration.
Location: tests/DotNetCloud.Core.ServiceDefaults.Tests/
```

**Deliverables:**
- ☐ Logging configuration tests
- ☐ Health check format tests
- ☐ Telemetry emission tests
- ☐ Security header tests
- ☐ Exception handling tests

**File Location:** `tests/DotNetCloud.Core.ServiceDefaults.Tests/`  
**Dependencies:** phase-0.3.1 through phase-0.3.7  
**Testing:** 80%+ code coverage  
**Notes:** Ensures consistent behavior across all projects

---

### Section: Phase 0.4 - Authentication & Authorization

#### Step: phase-0.4.1 - OpenIddict NuGet Packages & Configuration
**Status:** pending  
**Duration:** ~1.5 hours  
**Description:** Add OpenIddict packages and configure dependency injection

**Recommended Prompt:**
```
Execute phase-0.4.1: Set up OpenIddict foundation. Add OpenIddict.AspNetCore, OpenIddict.EntityFrameworkCore 
NuGet packages. Configure OpenIddict in DI with server features, token formats (JWT), supported flows 
(authorization code, refresh token, client credentials), and scopes (openid, profile, email). 
Create OpenIddictApplication entity model for registered clients.
Location: src/Core/DotNetCloud.Core.Data/Entities/Identity/
```

**Deliverables:**
- ☐ OpenIddict NuGet packages added
- ☐ OpenIddict server configuration in DI
- ☐ Token format configuration (JWT)
- ☐ Scopes configuration (openid, profile, email, offline_access)
- ☐ `OpenIddictApplication` entity model

**File Location:** `src/Core/DotNetCloud.Core.Data/Entities/Identity/`  
**Dependencies:** phase-0.2.7 (CoreDbContext)  
**Testing:** Configuration validation tests  
**Notes:** Foundation for OAuth2/OIDC server

---

#### Step: phase-0.4.2 - Token Endpoint Implementation
**Status:** pending  
**Duration:** ~3 hours  
**Description:** Implement /connect/token endpoint with multiple flows

**Recommended Prompt:**
```
Execute phase-0.4.2: Create token endpoint. Implement POST /connect/token with Authorization Code flow, 
Refresh Token flow, and Client Credentials flow. Handle token requests, validate client credentials, 
issue access/refresh tokens. Add proper error responses per OAuth2 spec.
Location: src/Core/DotNetCloud.Core.Server/Controllers/
```

**Deliverables:**
- ☐ `/connect/token` endpoint
- ☐ Authorization Code flow handler
- ☐ Refresh Token flow handler
- ☐ Client Credentials flow handler
- ☐ Token validation logic

**File Location:** `src/Core/DotNetCloud.Core.Server/Controllers/`  
**Dependencies:** phase-0.4.1  
**Testing:** Token flow integration tests  
**Notes:** Core OAuth2 endpoint

---

#### Step: phase-0.4.3 - Authorization Endpoint with PKCE
**Status:** pending  
**Duration:** ~3 hours  
**Description:** Implement /connect/authorize endpoint with login and consent

**Recommended Prompt:**
```
Execute phase-0.4.3: Create authorization endpoint. Implement GET /connect/authorize with login page 
redirect, consent page, PKCE validation (code_challenge, code_verifier). Handle authorization code 
generation and storage. Implement redirect_uri validation.
Location: src/Core/DotNetCloud.Core.Server/Controllers/
```

**Deliverables:**
- ☐ `/connect/authorize` endpoint
- ☐ Login page integration
- ☐ Consent page UI
- ☐ PKCE support (code_challenge/code_verifier)
- ☐ Authorization code generation

**File Location:** `src/Core/DotNetCloud.Core.Server/Controllers/`  
**Dependencies:** phase-0.4.2  
**Testing:** Authorization flow tests, PKCE validation tests  
**Notes:** Enables secure public client flows

---

#### Step: phase-0.4.4 - Logout & Userinfo Endpoints
**Status:** pending  
**Duration:** ~1.5 hours  
**Description:** Implement logout and userinfo endpoints

**Recommended Prompt:**
```
Execute phase-0.4.4: Create logout and userinfo endpoints. Implement POST /connect/logout to revoke 
tokens and end session, and GET /connect/userinfo to return user claims. Validate bearer tokens, 
return standard OIDC claims (sub, name, email, etc.).
Location: src/Core/DotNetCloud.Core.Server/Controllers/
```

**Deliverables:**
- ☐ `/connect/logout` endpoint
- ☐ `/connect/userinfo` endpoint
- ☐ Token revocation logic
- ☐ Claims mapping

**File Location:** `src/Core/DotNetCloud.Core.Server/Controllers/`  
**Dependencies:** phase-0.4.3  
**Testing:** Logout flow tests, userinfo claim tests  
**Notes:** Completes core OIDC endpoints

---

#### Step: phase-0.4.5 - Token Revocation Endpoint
**Status:** pending  
**Duration:** ~1 hour  
**Description:** Implement token revocation endpoint

**Recommended Prompt:**
```
Execute phase-0.4.5: Create token revocation. Implement POST /connect/revoke endpoint to revoke 
access and refresh tokens. Validate client credentials, mark tokens as revoked in database.
Location: src/Core/DotNetCloud.Core.Server/Controllers/
```

**Deliverables:**
- ☐ `/connect/revoke` endpoint
- ☐ Token revocation logic
- ☐ Database persistence of revoked tokens

**File Location:** `src/Core/DotNetCloud.Core.Server/Controllers/`  
**Dependencies:** phase-0.4.4  
**Testing:** Revocation flow tests  
**Notes:** Security best practice for token lifecycle

---

#### Step: phase-0.4.6 - Token Lifetime & Rotation Configuration
**Status:** pending  
**Duration:** ~1 hour  
**Description:** Configure token lifetimes and refresh token rotation

**Recommended Prompt:**
```
Execute phase-0.4.6: Configure token lifetimes. Set access token lifetime (15 min default), 
refresh token lifetime (14 days default). Implement refresh token rotation (one-time use). 
Create configuration options in appsettings.json.
Location: src/Core/DotNetCloud.Core.Server/Configuration/
```

**Deliverables:**
- ☐ Access token lifetime configuration
- ☐ Refresh token lifetime configuration
- ☐ Refresh token rotation implementation
- ☐ Configuration validation

**File Location:** `src/Core/DotNetCloud.Core.Server/Configuration/`  
**Dependencies:** phase-0.4.2  
**Testing:** Token lifetime validation tests  
**Notes:** Balances security and usability

---

#### Step: phase-0.4.7 - Token Validation Middleware
**Status:** pending  
**Duration:** ~1.5 hours  
**Description:** Create middleware for validating bearer tokens

**Recommended Prompt:**
```
Execute phase-0.4.7: Create token validation middleware. Implement JWT bearer authentication, 
validate token signature, expiration, issuer, audience. Inject CallerContext from token claims. 
Handle revoked tokens.
Location: src/Core/DotNetCloud.Core.ServiceDefaults/Authentication/
```

**Deliverables:**
- ☐ JWT bearer authentication configuration
- ☐ Token validation logic
- ☐ CallerContext injection
- ☐ Revoked token check

**File Location:** `src/Core/DotNetCloud.Core.ServiceDefaults/Authentication/`  
**Dependencies:** phase-0.4.6  
**Testing:** Token validation tests  
**Notes:** Applied to all authenticated endpoints

---

#### Step: phase-0.4.8 - ASP.NET Core Identity Integration
**Status:** pending  
**Duration:** ~2 hours  
**Description:** Integrate ASP.NET Core Identity for user management

**Recommended Prompt:**
```
Execute phase-0.4.8: Integrate ASP.NET Core Identity. Configure UserManager<ApplicationUser>, 
SignInManager<ApplicationUser>, RoleManager<ApplicationRole>. Set up password validation rules 
(length, complexity). Configure account lockout (5 failed attempts, 15 min lockout).
Location: src/Core/DotNetCloud.Core.Server/
```

**Deliverables:**
- ☐ UserManager configuration
- ☐ SignInManager configuration
- ☐ RoleManager configuration
- ☐ Password validation rules
- ☐ Account lockout configuration

**File Location:** `src/Core/DotNetCloud.Core.Server/`  
**Dependencies:** phase-0.4.1 (OpenIddict)  
**Testing:** User management tests  
**Notes:** Foundation for user operations

---

#### Step: phase-0.4.9 - User Registration Endpoint
**Status:** pending  
**Duration:** ~2 hours  
**Description:** Create user registration with email confirmation

**Recommended Prompt:**
```
Execute phase-0.4.9: Create user registration. Implement POST /api/v1/auth/register endpoint 
with email, password, displayName. Validate email uniqueness, generate email confirmation token, 
send confirmation email. Return registration success response.
Location: src/Core/DotNetCloud.Core.Server/Controllers/
```

**Deliverables:**
- ☐ User registration endpoint
- ☐ Email validation logic
- ☐ Email confirmation token generation
- ☐ Confirmation email sending

**File Location:** `src/Core/DotNetCloud.Core.Server/Controllers/`  
**Dependencies:** phase-0.4.8  
**Testing:** Registration flow tests  
**Notes:** Requires email service configuration

---

#### Step: phase-0.4.10 - Password Management Endpoints
**Status:** pending  
**Duration:** ~2 hours  
**Description:** Implement password change and reset flows

**Recommended Prompt:**
```
Execute phase-0.4.10: Create password management. Implement POST /api/v1/auth/password/change 
(requires authentication), POST /api/v1/auth/password/forgot (generates reset token), 
POST /api/v1/auth/password/reset (validates token, sets new password). Add password history 
to prevent reuse of last 5 passwords.
Location: src/Core/DotNetCloud.Core.Server/Controllers/
```

**Deliverables:**
- ☐ Password change endpoint
- ☐ Password forgot endpoint
- ☐ Password reset endpoint
- ☐ Password reset token generation/validation
- ☐ Password history tracking

**File Location:** `src/Core/DotNetCloud.Core.Server/Controllers/`  
**Dependencies:** phase-0.4.9  
**Testing:** Password flow tests  
**Notes:** Includes password strength validation

---

#### Step: phase-0.4.11 - TOTP MFA Setup
**Status:** pending  
**Duration:** ~2.5 hours  
**Description:** Implement Time-based One-Time Password MFA

**Recommended Prompt:**
```
Execute phase-0.4.11: Create TOTP MFA. Implement POST /api/v1/auth/mfa/totp/setup to generate 
TOTP secret and QR code, POST /api/v1/auth/mfa/totp/verify to validate TOTP code. Store TOTP 
secrets encrypted. Generate 10 backup codes for recovery. Use GoogleAuthenticator library.
Location: src/Core/DotNetCloud.Core.Server/Controllers/
```

**Deliverables:**
- ☐ TOTP setup endpoint with QR code generation
- ☐ TOTP verification endpoint
- ☐ Encrypted TOTP secret storage
- ☐ Backup code generation
- ☐ MFA enforcement policies

**File Location:** `src/Core/DotNetCloud.Core.Server/Controllers/`  
**Dependencies:** phase-0.4.10  
**Testing:** TOTP flow tests, encryption tests  
**Notes:** Adds second factor authentication

---

#### Step: phase-0.4.12 - WebAuthn/Passkey Support
**Status:** pending  
**Duration:** ~3 hours  
**Description:** Implement WebAuthn for passwordless authentication

**Recommended Prompt:**
```
Execute phase-0.4.12: Create WebAuthn/Passkey support. Integrate Fido2NetLib package. 
Implement POST /api/v1/auth/passkey/register (credential creation), 
POST /api/v1/auth/passkey/verify (assertion verification). Store WebAuthn credentials 
with counter tracking. Support platform and cross-platform authenticators.
Location: src/Core/DotNetCloud.Core.Server/Controllers/
```

**Deliverables:**
- ☐ Fido2NetLib package integration
- ☐ Passkey registration flow
- ☐ Passkey verification flow
- ☐ WebAuthn credential storage
- ☐ Challenge generation/validation

**File Location:** `src/Core/DotNetCloud.Core.Server/Controllers/`  
**Dependencies:** phase-0.4.11  
**Testing:** WebAuthn flow tests  
**Notes:** Passwordless authentication

---

#### Step: phase-0.4.13 - Session Management
**Status:** pending  
**Duration:** ~1.5 hours  
**Description:** Implement cookie-based sessions with timeout and limits

**Recommended Prompt:**
```
Execute phase-0.4.13: Create session management. Configure cookie authentication with 
sliding expiration (30 min default). Implement concurrent session limits (max 5 per user). 
Track active sessions per user. Add session termination endpoint.
Location: src/Core/DotNetCloud.Core.Server/Services/
```

**Deliverables:**
- ☐ Cookie session configuration
- ☐ Session timeout (sliding expiration)
- ☐ Concurrent session limits
- ☐ Session tracking per user

**File Location:** `src/Core/DotNetCloud.Core.Server/Services/`  
**Dependencies:** phase-0.4.8  
**Testing:** Session lifecycle tests  
**Notes:** Balances security and UX

---

#### Step: phase-0.4.14 - Device Tracking
**Status:** pending  
**Duration:** ~1.5 hours  
**Description:** Track user devices and provide device management UI

**Recommended Prompt:**
```
Execute phase-0.4.14: Create device tracking. Implement device registration on login, 
track LastSeenAt, DeviceType, PushToken. Create GET /api/v1/auth/devices endpoint 
(list user devices), DELETE /api/v1/auth/devices/{deviceId} (remove device). 
Update UserDevice entity on each login.
Location: src/Core/DotNetCloud.Core.Server/Controllers/
```

**Deliverables:**
- ☐ Device registration on login
- ☐ Device list endpoint
- ☐ Device removal endpoint
- ☐ LastSeenAt tracking

**File Location:** `src/Core/DotNetCloud.Core.Server/Controllers/`  
**Dependencies:** phase-0.2.6 (UserDevice entity)  
**Testing:** Device tracking tests  
**Notes:** Security audit trail

---

#### Step: phase-0.4.15 - Google OAuth Integration
**Status:** pending  
**Duration:** ~2 hours  
**Description:** Configure Google as external authentication provider

**Recommended Prompt:**
```
Execute phase-0.4.15: Add Google OAuth. Install Microsoft.AspNetCore.Authentication.Google package. 
Configure Google OAuth options (ClientId, ClientSecret from Google Cloud Console). 
Implement sign-in handler, map Google claims (sub, name, email, picture) to ApplicationUser. 
Create account linking if email already exists.
Location: src/Core/DotNetCloud.Core.Server/Configuration/
```

**Deliverables:**
- ☐ Google OAuth package
- ☐ Google OAuth configuration
- ☐ Google sign-in handler
- ☐ Claims mapping to ApplicationUser

**File Location:** `src/Core/DotNetCloud.Core.Server/Configuration/`  
**Dependencies:** phase-0.4.8  
**Testing:** Google OAuth flow tests  
**Notes:** Requires Google Cloud project

---

#### Step: phase-0.4.16 - Microsoft/Azure AD Integration
**Status:** pending  
**Duration:** ~2 hours  
**Description:** Configure Microsoft/Azure AD as external authentication provider

**Recommended Prompt:**
```
Execute phase-0.4.16: Add Microsoft OAuth. Install Microsoft.AspNetCore.Authentication.MicrosoftAccount 
package. Configure Microsoft OAuth options (ClientId, ClientSecret from Azure portal). 
Implement sign-in handler, map Microsoft claims to ApplicationUser. Support personal and 
organizational accounts.
Location: src/Core/DotNetCloud.Core.Server/Configuration/
```

**Deliverables:**
- ☐ Microsoft OAuth package
- ☐ Microsoft OAuth configuration
- ☐ Microsoft sign-in handler
- ☐ Claims mapping to ApplicationUser

**File Location:** `src/Core/DotNetCloud.Core.Server/Configuration/`  
**Dependencies:** phase-0.4.15  
**Testing:** Microsoft OAuth flow tests  
**Notes:** Requires Azure AD app registration

---

#### Step: phase-0.4.17 - GitHub OAuth Skeleton
**Status:** pending  
**Duration:** ~1 hour  
**Description:** Create GitHub OAuth configuration structure (future phase)

**Recommended Prompt:**
```
Execute phase-0.4.17: Create GitHub OAuth skeleton. Install AspNet.Security.OAuth.GitHub package. 
Add configuration structure for GitHub OAuth (ClientId, ClientSecret). Document setup process. 
Mark as future phase implementation.
Location: src/Core/DotNetCloud.Core.Server/Configuration/
```

**Deliverables:**
- ☐ GitHub OAuth package
- ☐ Configuration structure
- ☐ Setup documentation

**File Location:** `src/Core/DotNetCloud.Core.Server/Configuration/`  
**Dependencies:** phase-0.4.16  
**Testing:** None (skeleton only)  
**Notes:** Implementation in future phase

---

#### Step: phase-0.4.18 - SAML 2.0 Skeleton
**Status:** pending  
**Duration:** ~1.5 hours  
**Description:** Create SAML configuration structure (enterprise feature)

**Recommended Prompt:**
```
Execute phase-0.4.18: Create SAML 2.0 skeleton. Create SAML configuration models, 
document SAML metadata endpoint structure, create assertion consumer service (ACS) endpoint stub. 
Add setup documentation for SAML IdP integration. Mark as future enterprise feature.
Location: src/Core/DotNetCloud.Core.Server/Controllers/
```

**Deliverables:**
- ☐ SAML configuration models
- ☐ Metadata endpoint documentation
- ☐ ACS endpoint stub
- ☐ SAML setup documentation

**File Location:** `src/Core/DotNetCloud.Core.Server/Controllers/`  
**Dependencies:** phase-0.4.17  
**Testing:** None (skeleton only)  
**Notes:** Enterprise SSO feature

---

#### Step: phase-0.4.19 - OIDC Federation Skeleton
**Status:** pending  
**Duration:** ~1 hour  
**Description:** Create OIDC discovery endpoint and federation docs

**Recommended Prompt:**
```
Execute phase-0.4.19: Create OIDC federation skeleton. Implement GET /.well-known/openid-configuration 
discovery endpoint returning issuer, endpoints, supported scopes, token signing keys. Document federation 
setup for external OIDC providers.
Location: src/Core/DotNetCloud.Core.Server/Controllers/
```

**Deliverables:**
- ☐ OIDC discovery endpoint
- ☐ Federation configuration documentation
- ☐ JWT key set endpoint (JWKS)

**File Location:** `src/Core/DotNetCloud.Core.Server/Controllers/`  
**Dependencies:** phase-0.4.18  
**Testing:** Discovery endpoint tests  
**Notes:** Enables federation with external IdPs

---

#### Step: phase-0.4.20 - Authentication Integration Tests
**Status:** pending  
**Duration:** ~3 hours  
**Description:** Create comprehensive authentication test suite

**Recommended Prompt:**
```
Execute phase-0.4.20: Create authentication integration tests. Test all OAuth2/OIDC flows 
(authorization code, refresh token, client credentials), password flows (registration, login, reset), 
MFA flows (TOTP, passkey), external provider flows (Google, Microsoft). Use WebApplicationFactory 
for integration tests. Target 80%+ coverage.
Location: tests/DotNetCloud.Core.Server.Tests/
```

**Deliverables:**
- ☐ OAuth2 flow integration tests
- ☐ Password management tests
- ☐ MFA tests (TOTP, passkey)
- ☐ External provider tests
- ☐ Session management tests

**File Location:** `tests/DotNetCloud.Core.Server.Tests/`  
**Dependencies:** phase-0.4.1 through phase-0.4.19  
**Testing:** 80%+ code coverage  
**Notes:** Critical for security validation

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
