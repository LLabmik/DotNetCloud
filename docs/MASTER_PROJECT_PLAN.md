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
| Phase 0.2 | 12 | 0 | 0 | 12 |
| Phase 0.3 | 8 | 0 | 0 | 8 |
| Phase 0.4 | 20 | 0 | 0 | 20 |
| Phase 0.5 | 9 | 0 | 0 | 9 |
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
- [x] Initialize Git repository (if not already done)
- [x] Create `.gitignore` for .NET projects
- [x] Create solution file: `DotNetCloud.sln`
- [x] Create directory structure: `src/Core/`, `src/Modules/`, `src/UI/`, `src/Clients/`, `tests/`, `tools/`, `docs/`
- [x] Add LICENSE file (AGPL-3.0)
- [x] Create comprehensive README.md with project vision
- [x] Create CONTRIBUTING.md
- [x] Add .github/copilot-instructions.md for AI contribution guidelines

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
- [x] Install Visual Studio 2022 (or later)
- [x] Install .NET 10 SDK
- [x] Install PostgreSQL 14 (or later)
- [x] Install Docker Desktop
- [x] Clone the repository
- [x] Build the solution
- [x] Install EF Core tools
- [x] Configure Docker support in Visual Studio
- [x] Create sample `appsettings.Development.json`

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
- [x] Create comprehensive IDE setup guide (Visual Studio, VS Code, Rider)
- [x] Create local development database setup guide (PostgreSQL, SQL Server, MariaDB)
- [x] Document Docker setup for local testing and multi-database CI/CD
- [x] Create development workflow guidelines (branching, commits, PRs, code review)
- [x] Updated IMPLEMENTATION_CHECKLIST.md to mark all Development Environment Setup tasks as completed
- [x] Updated MASTER_PROJECT_PLAN.md with completion status

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
- [x] Capability system with tier enforcement (ICapabilityInterface, CapabilityTier enum, public/restricted/privileged tier interfaces, forbidden interfaces list)
- [x] Authorization context and models (CallerContext, CallerType, CapabilityRequest)
- [x] Module system interfaces (IModuleManifest, IModule, IModuleLifecycle, ModuleInitializationContext)
- [x] Event system interfaces (IEvent, IEventHandler<T>, IEventBus, EventSubscription model)
- [x] Complete DTO layer (User, Organization, Team, Permission, Role, Module, Device, Settings DTOs)
- [x] Standardized error handling (ErrorCodes constants, exception hierarchy, API error response models)
- [x] Foundation for all subsequent phases established

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
- [x] `ICapabilityInterface` marker interface
- [x] `CapabilityTier` enum (Public, Restricted, Privileged, Forbidden)
- [x] Public tier interfaces:
  - [x] `IUserDirectory`
  - [x] `ICurrentUserContext`
  - [x] `INotificationService`
  - [x] `IEventBus`
- [x] Restricted tier interfaces:
  - [x] `IStorageProvider`
  - [x] `IModuleSettings`
  - [x] `ITeamDirectory`
- [x] Privileged tier interfaces:
  - [x] `IUserManager`
  - [x] `IBackupProvider`

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
- [x] `CallerContext` record with:
  - [x] `Guid UserId` property
  - [x] `IReadOnlyList<string> Roles` property
  - [x] `CallerType Type` property
  - [x] Validation logic
- [x] `CallerType` enum (User, System, Module)
- [x] `CapabilityRequest` model with capability name, required tier, optional description

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
- [x] `IModuleManifest` interface with properties: Id, Name, Version, RequiredCapabilities, PublishedEvents, SubscribedEvents
- [x] `IModule` base interface with: Manifest property, InitializeAsync(), StartAsync(), StopAsync()
- [x] `IModuleLifecycle` interface with: InitializeAsync(), StartAsync(), StopAsync(), DisposeAsync()
- [x] Module initialization context (ModuleInitializationContext record)

**File Location:** `src/Core/DotNetCloud.Core/Modules/`  
**Dependencies:** phase-0.1.1 (capability system)  
**Testing:** Unit tests for manifest validation  
**Notes:** Foundational for module loading system. Interfaces enable dynamic module discovery, validation of capabilities at load time, and event subscription management. ModuleInitializationContext provides modules with service provider, configuration, and system caller context.

---

#### Step: phase-0.1.4 - Event System Interfaces
**Status:** pending  
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
- [ ] `IEvent` base interface
- [ ] `IEventHandler<TEvent>` interface with `Task HandleAsync(TEvent @event)` method
- [ ] `IEventBus` interface with: PublishAsync, SubscribeAsync, UnsubscribeAsync
- [ ] Event subscription model

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
- [x] User DTOs: UserDto, CreateUserDto, UpdateUserDto
- [x] Organization DTOs: OrganizationDto, CreateOrganizationDto, UpdateOrganizationDto
- [x] Team DTOs: TeamDto, CreateTeamDto, UpdateTeamDto, TeamMemberDto, AddTeamMemberDto
- [x] Permission DTOs: PermissionDto, CreatePermissionDto, RoleDto, CreateRoleDto, UpdateRoleDto
- [x] Module DTOs: ModuleDto, CreateModuleDto, ModuleCapabilityGrantDto, GrantModuleCapabilityDto
- [x] Device DTOs: UserDeviceDto, RegisterUserDeviceDto, UpdateUserDeviceDto
- [x] Settings DTOs: SystemSettingDto, OrganizationSettingDto, UserSettingDto, UpsertSystemSettingDto, UpsertOrganizationSettingDto, UpsertUserSettingDto, SettingsBulkDto

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
- [x] Error code constants class (70+ error codes)
- [x] Exception types:
  - [x] `CapabilityNotGrantedException`
  - [x] `ModuleNotFoundException`
  - [x] `UnauthorizedException`
  - [x] `ValidationException`
  - [x] `ForbiddenException`
  - [x] `NotFoundException`
  - [x] `ConcurrencyException`
  - [x] `InvalidOperationException`
- [x] `ApiErrorResponse` model with code, message, details, path, timestamp, traceId
- [x] `ApiSuccessResponse<T>` generic model with data and pagination support
- [x] `PaginationInfo` model for paginated responses

**File Location:** `src/Core/DotNetCloud.Core/Errors/`  
**Dependencies:** None  
**Testing:** Unit tests for exception properties and response creation  
**Notes:** Used globally for consistent error handling. All exception types inherit from DotNetCloudException base class.

---

#### Step: phase-0.1.7 - Core Abstractions Unit Tests
**Status:** pending  
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
- [ ] Capability system tests
- [ ] CallerContext validation tests
- [ ] Module manifest validation tests
- [ ] Event bus interface contract tests
- [ ] Exception creation tests

**File Location:** `tests/DotNetCloud.Core.Tests/`  
**Dependencies:** phase-0.1.1 through phase-0.1.6  
**Testing:** Min 80% code coverage for abstractions  
**Notes:** Should run clean before moving to Phase 0.2

---

#### Step: phase-0.1.8 - Documentation: Core Abstractions
**Status:** pending  
**Duration:** ~1.5 hours  
**Description:** Document all interfaces and design patterns

**Recommended Prompt:**
```
Execute phase-0.1.8: Document core abstractions. Create docs/architecture/core-abstractions.md with 
capability system design (how tiers work with examples), module system design (lifecycle, manifest), 
event system design (pub/sub patterns). Add comprehensive XML documentation comments (///) to all 
public types. Create README for src/Core/DotNetCloud.Core/
```

**Deliverables:**
- [ ] Capability system design document (how tiers work, examples)
- [ ] Module system design document (lifecycle, manifest)
- [ ] Event system design document (pub/sub patterns)
- [ ] XML documentation comments on all public types
- [ ] README for Core abstractions

**File Location:** `docs/architecture/core-abstractions.md` and inline `///` comments  
**Dependencies:** phase-0.1.1 through phase-0.1.6  
**Notes:** Critical for other developers to understand the design

---

### Section: Phase 0.2 - Database & Data Access Layer

#### Step: phase-0.2.1 - Multi-Database Provider Strategy
**Status:** pending  
**Duration:** ~1.5 hours  
**Description:** Design and implement multi-database support abstraction

**Recommended Prompt:**
```
Execute phase-0.2.1: Create multi-database provider strategy. Implement IDbContextFactory<CoreDbContext> 
abstraction, ITableNamingStrategy interface, and three implementations: PostgreSqlNamingStrategy (using 
schemas: core.*, files.*), SqlServerNamingStrategy (using schemas), and MariaDbNamingStrategy (using 
table prefixes). Create provider detection logic from connection string. Include unit tests for strategy 
selection.
Location: src/Core/DotNetCloud.Core.Data/Strategies/
```

**Deliverables:**
- [ ] `IDbContextFactory<CoreDbContext>` abstraction
- [ ] `ITableNamingStrategy` interface
- [ ] `PostgreSqlNamingStrategy` (schemas: `core.*`, `files.*`)
- [ ] `SqlServerNamingStrategy` (schemas)
- [ ] `MariaDbNamingStrategy` (table prefixes)
- [ ] Provider detection logic from connection string

**File Location:** `src/Core/DotNetCloud.Core.Data/Strategies/`  
**Dependencies:** None  
**Testing:** Unit tests for strategy selection  
**Notes:** Must handle all three database engines identically

---

#### Step: phase-0.2.2 - Identity Models (ASP.NET Core Identity)
**Status:** pending  
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
- [ ] `ApplicationUser` entity extending `IdentityUser<Guid>`:
  - [ ] DisplayName, AvatarUrl, Locale, Timezone properties
  - [ ] CreatedAt, LastLoginAt, IsActive properties
- [ ] `ApplicationRole` entity extending `IdentityRole<Guid>`:
  - [ ] Description, IsSystemRole properties
- [ ] Identity relationship configuration

**File Location:** `src/Core/DotNetCloud.Core.Data/Entities/Identity/`  
**Dependencies:** phase-0.2.1 (naming strategy)  
**Testing:** EF Core model snapshot tests  
**Notes:** Extends standard ASP.NET Identity

---

#### Step: phase-0.2.3 - Organization Hierarchy Models
**Status:** pending  
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
- [ ] `Organization` entity (Name, Description, CreatedAt, soft-delete)
- [ ] `Team` entity (OrganizationId FK, Name, soft-delete)
- [ ] `TeamMember` entity (TeamId, UserId, RoleIds)
- [ ] `Group` entity (OrganizationId, Name)
- [ ] `GroupMember` entity (GroupId, UserId)
- [ ] `OrganizationMember` entity (OrganizationId, UserId, RoleIds)

**File Location:** `src/Core/DotNetCloud.Core.Data/Entities/Organizations/`  
**Dependencies:** phase-0.2.2 (ApplicationUser)  
**Testing:** Entity relationship tests  
**Notes:** Supports hierarchical permission structure

---

#### Step: phase-0.2.4 - Permissions System Models
**Status:** pending  
**Duration:** ~1.5 hours  
**Description:** Create Permission, Role, and RolePermission junction entities

**Recommended Prompt:**
```
Execute phase-0.2.4: Create permission and role models. Implement Permission entity (Code, DisplayName, 
Description), Role entity (Name, Description, IsSystemRole, Permissions navigation property), and 
RolePermission junction table for many-to-many relationship. Include fluent API configuration for 
relationships. Add tests for junction table integrity.
Location: src/Core/DotNetCloud.Core.Data/Entities/Permissions/
```

**Deliverables:**
- [ ] `Permission` entity (Code, DisplayName, Description)
- [ ] `Role` entity (Name, Description, IsSystemRole, Permissions navigation)
- [ ] `RolePermission` junction table

**File Location:** `src/Core/DotNetCloud.Core.Data/Entities/Permissions/`  
**Dependencies:** phase-0.2.3 (Organization hierarchy)  
**Testing:** Junction table relationship tests  
**Notes:** Enables flexible RBAC system

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
- [ ] `SystemSetting` entity (Key, Value, Module, composite key)
- [ ] `OrganizationSetting` entity (OrganizationId, Key, Value, Module)
- [ ] `UserSetting` entity (UserId, Key, Value encrypted, Module)

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
- [ ] `UserDevice` entity (UserId, Name, DeviceType, PushToken, LastSeenAt)
- [ ] `InstalledModule` entity (ModuleId PK, Version, Status, InstalledAt)
- [ ] `ModuleCapabilityGrant` entity (ModuleId, CapabilityName, GrantedAt, GrantedByUserId)

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
- [ ] `CoreDbContext` class extending `IdentityDbContext<ApplicationUser, ApplicationRole, Guid>`
- [ ] DbSet properties for all entities
- [ ] Fluent API configuration for all relationships
- [ ] Automatic timestamps (CreatedAt, UpdatedAt)
- [ ] Soft-delete query filters
- [ ] Table naming strategy application

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
- [ ] Database creation logic
- [ ] Seed default system roles (Admin, User, Guest, etc.)
- [ ] Seed default permissions (for all modules)
- [ ] Seed system settings (default config values)

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
- [ ] Initial migration file
- [ ] Schema creation (core.*, files.*, etc.)
- [ ] Index creation
- [ ] Constraint definitions

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
- [ ] Initial migration file
- [ ] Schema creation
- [ ] Index creation
- [ ] Constraint definitions

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
- [ ] Initial migration file
- [ ] Table prefix naming applied
- [ ] Index creation
- [ ] Constraint definitions

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
- [ ] Entity relationship tests
- [ ] Soft-delete tests
- [ ] Query filter tests
- [ ] Migration integration tests (all 3 databases)
- [ ] DbInitializer tests

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
- [ ] Console sink configuration (development)
- [ ] File sink configuration (production with rotation)
- [ ] Structured logging format
- [ ] Log level configuration per module
- [ ] Log context enrichment (user ID, request ID, module name)

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
- [ ] Health check infrastructure base classes
- [ ] Database health check implementation
- [ ] Custom health check interface for modules
- [ ] Health check endpoints setup
- [ ] Health status aggregation

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
- [ ] Metrics configuration (HTTP, gRPC, database)
- [ ] W3C Trace Context propagation
- [ ] gRPC interceptor for tracing
- [ ] HTTP middleware for tracing
- [ ] Trace exporter configuration

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
- [ ] CORS configuration with origin whitelist
- [ ] Security headers middleware:
  - [ ] Content-Security-Policy
  - [ ] X-Frame-Options
  - [ ] X-Content-Type-Options
  - [ ] Strict-Transport-Security
- [ ] Authorization/authentication middleware

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
- [ ] Global exception handler middleware
- [ ] Consistent error response formatting
- [ ] Request validation error handling
- [ ] Stack trace handling (dev vs. production)
- [ ] Error logging integration

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
- [ ] Request body logging
- [ ] Response body logging
- [ ] PII/sensitive data masking
- [ ] Request/response timing

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
- [ ] `AddServiceDefaults()` extension method
- [ ] `UseServiceDefaults()` extension method
- [ ] Feature flag for middleware enablement

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
- [ ] Logging configuration tests
- [ ] Health check format tests
- [ ] Telemetry emission tests
- [ ] Security header tests
- [ ] Exception handling tests

**File Location:** `tests/DotNetCloud.Core.ServiceDefaults.Tests/`  
**Dependencies:** phase-0.3.1 through phase-0.3.7  
**Testing:** 80%+ code coverage  
**Notes:** Ensures consistent behavior across all projects

---

## How to Use This Plan

### In Your Next Conversation:
```
Per MASTER_PROJECT_PLAN.md, execute phase-0.1.1: Create the capability system interfaces.
```

### To Update Status:
```
Mark phase-0.1.1 as completed. I successfully created ICapabilityInterface, CapabilityTier enum, 
and all public/restricted/privileged interfaces. Per the plan, continue to phase-0.1.2.
```

### To Skip a Step:
```
Skip phase-0.1.8: Documentation. We'll consolidate docs after Phase 0 completion. 
Move directly to phase-0.2.1: Multi-Database Provider Strategy.
```

### To Reference Progress:
```
Per docs/MASTER_PROJECT_PLAN.md, I'm on phase-0.1.5. All capability, context, module, and event 
system interfaces are complete. Now implementing DTOs as specified.
```

---

## Status Summary & Notes

- **Total Phase 0 Steps:** 228+ (across subsections 0.1-0.19)
- **Estimated Duration:** 16-20 weeks for complete Phase 0
- **Critical Path:** 0.1 → 0.2 → 0.3 → 0.4 → (0.5-0.19 can parallelize somewhat)
- **Blocking Issues:** None currently
- **Assumptions:** .NET 10, PostgreSQL/SQL Server/MariaDB support required

---

**Last Updated:** 2026-03-02 (phase pre-impl-1 completed)  
**Next Review:** After Phase 0.1.1 completion  
**Maintained By:** Development Team
