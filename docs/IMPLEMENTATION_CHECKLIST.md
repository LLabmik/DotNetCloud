# DotNetCloud Implementation Planning Checklist

> **Document Version:** 1.0  
> **Purpose:** Comprehensive task breakdown for implementing the DotNetCloud architecture  
> **Scope:** All phases from Foundation (Phase 0) through Auto-Updates (Phase 11)  
> **Last Updated:** 2026-03-03
> **Audience:** Development team, project managers, technical leads

---

## Table of Contents

1. [Pre-Implementation Setup](#pre-implementation-setup)
2. [Phase 0: Foundation](#phase-0-foundation)
3. [Phase 1: Files (Public Launch)](#phase-1-files-public-launch)
4. [Phase 2: Chat & Notifications](#phase-2-chat--notifications)
5. [Phase 3: Contacts, Calendar & Notes](#phase-3-contacts-calendar--notes)
6. [Phase 4: Project Management (Tracks)](#phase-4-project-management-tracks)
7. [Phase 5: Media (Photos, Music, Video)](#phase-5-media-photos-music-video)
8. [Phase 6: Email & Bookmarks](#phase-6-email--bookmarks)
9. [Phase 7: Video Calling & Screen Sharing](#phase-7-video-calling--screen-sharing)
10. [Phase 8: Search, Auto-Updates & Polish](#phase-8-search-auto-updates--polish)
11. [Phase 9: AI Assistant](#phase-9-ai-assistant)
12. [Phase 10: End-to-End Encryption (E2EE)](#phase-10-end-to-end-encryption-e2ee)
13. [Phase 11: Auto-Updates](#phase-11-auto-updates)
14. [Infrastructure & DevOps](#infrastructure--devops)
15. [Documentation & Support](#documentation--support)

---

## Pre-Implementation Setup

### Repository & Project Structure

**Objective:** Establish the monorepo structure and foundational files

- ✓ Initialize Git repository (if not already done)
- ✓ Create `.gitignore` for .NET projects
- ✓ Create solution file: `DotNetCloud.sln`
- ✓ Create project directory structure:
  - ✓ `src/Core/`
  - ✓ `src/Modules/`
  - ✓ `src/UI/`
  - ✓ `src/Clients/`
  - ✓ `tests/`
  - ✓ `tools/`
  - ✓ `docs/`
- ✓ Add LICENSE file (AGPL-3.0)
- ✓ Create comprehensive README.md with project vision
- ✓ Create CONTRIBUTING.md with contribution guidelines

### Development Environment Setup

**Objective:** Document and configure local development prerequisites

- ✓ Document .NET version requirements (.NET 10)
- ✓ Create `global.json` for .NET version pinning
- ✓ Create `.editorconfig` for code style consistency
- ✓ Create `Directory.Build.props` for common project settings
- ✓ Create `Directory.Build.targets` for common build configuration
- ✓ Set up `NuGet.config` for dependency management
- ✓ Document IDE setup for Visual Studio, VS Code, Rider
- ✓ Create local development database setup guide (PostgreSQL, SQL Server, MariaDB)
- ✓ Document Docker setup for local testing
- ✓ Create development workflow guidelines (branch strategy, PR requirements)

### Base CI/CD Configuration

**Objective:** Set up initial CI/CD pipelines for build and test

- ✓ Create Gitea Actions workflow file (`.gitea/workflows/build-test.yml`)
- ✓ Create GitHub Actions workflow file (`.github/workflows/build-test.yml`)
- ✓ Configure multi-database testing (Docker containers for PostgreSQL, SQL Server, MariaDB)
  - ✓ Docker Engine installed in WSL 2 (setup script: `tools/setup-docker-wsl.sh`)
  - ✓ DatabaseContainerFixture with WSL auto-detection (native Docker → WSL fallback)
  - ✓ PostgreSQL 16 container tests passing (6/6)
  - ✓ SQL Server CI matrix job (GitHub/Gitea Actions service container)
  - ✓ SQL Server local testing via SQL Server Express (Windows Auth, shared memory)
  - ☐ MariaDB container tests (Pomelo lacks .NET 10 support)
- ✓ Set up build artifact generation
- ✓ Configure package publishing pipeline skeleton
- ✓ Create status badge documentation (docs/development/STATUS_BADGES.md)

---

## Phase 0: Foundation

### Objective

Core platform boots, authenticates a user, loads a module, serves the Blazor UI. Establishes the foundation for all subsequent phases.

### Milestone Criteria

- [ ] `dotnetcloud setup` wizard runs successfully
- [ ] Admin user can be created with MFA enabled
- [ ] User can log in to Blazor UI
- [ ] Example module loads and responds to health checks
- [ ] Core infrastructure tests pass against all three database engines

---

## Phase 0.1: Core Abstractions & Interfaces

### DotNetCloud.Core Project

**Create shared abstractions and interfaces layer**

#### Capability System

- ✓ Create `ICapabilityInterface` marker interface
- ✓ Create `CapabilityTier` enum (Public, Restricted, Privileged, Forbidden)
- ✓ Implement public tier interfaces:
  - ✓ `IUserDirectory` - query user information
  - ✓ `ICurrentUserContext` - get current caller context
  - ✓ `INotificationService` - send notifications
  - ✓ `IEventBus` - publish/subscribe to events
- ✓ Implement restricted tier interfaces:
  - ✓ `IStorageProvider` - file storage operations
  - ✓ `IModuleSettings` - module configuration
  - ✓ `ITeamDirectory` - team information
- ✓ Implement privileged tier interfaces:
  - ✓ `IUserManager` - create/disable users
  - ✓ `IBackupProvider` - backup operations
- ✓ Document forbidden interfaces list

#### Context & Authorization

- ✓ Create `CallerContext` record:
  - ✓ `Guid UserId` property
  - ✓ `IReadOnlyList<string> Roles` property
  - ✓ `CallerType Type` property
  - ✓ Validation logic
- ✓ Create `CallerType` enum (User, System, Module)
- ✓ Create `CapabilityRequest` model with:
  - ✓ Capability name
  - ✓ Required tier
  - ✓ Optional description

#### Module System

- ✓ Create `IModuleManifest` interface:
  - ✓ `string Id` property
  - ✓ `string Name` property
  - ✓ `string Version` property
  - ✓ `IReadOnlyCollection<string> RequiredCapabilities` property
  - ✓ `IReadOnlyCollection<string> PublishedEvents` property
  - ✓ `IReadOnlyCollection<string> SubscribedEvents` property
- ✓ Create `IModule` base interface:
  - ✓ `IModuleManifest Manifest` property
  - ✓ `Task InitializeAsync(ModuleInitializationContext context, CancellationToken cancellationToken)` method
  - ✓ `Task StartAsync(CancellationToken cancellationToken)` method
  - ✓ `Task StopAsync(CancellationToken cancellationToken)` method
- ✓ Create `IModuleLifecycle` interface:
  - ✓ Extends `IModule` and `IAsyncDisposable`
  - ✓ `Task DisposeAsync()` method
- ✓ Create `ModuleInitializationContext` record:
  - ✓ `string ModuleId` property
  - ✓ `IServiceProvider Services` property
  - ✓ `IReadOnlyDictionary<string, object> Configuration` property
  - ✓ `CallerContext SystemCaller` property

#### Event System

- ✓ Create `IEvent` base interface
- ✓ Create `IEventHandler<TEvent>` interface
- ✓ Create `IEventBus` interface:
  - ✓ `Task PublishAsync<TEvent>(TEvent @event, CallerContext caller)` method
  - ✓ `Task SubscribeAsync<TEvent>(IEventHandler<TEvent> handler)` method
  - ✓ `Task UnsubscribeAsync<TEvent>(IEventHandler<TEvent> handler)` method
- ✓ Create event subscription model

#### Data Transfer Objects (DTOs)

- ✓ Create user DTOs (UserDto, CreateUserDto, UpdateUserDto)
- ✓ Create organization DTOs
- ✓ Create team DTOs
- ✓ Create permission DTOs
- ✓ Create module DTOs
- ✓ Create device DTOs
- ✓ Create settings DTOs

#### Error Handling

- ✓ Create error code constants class
- ✓ Define standard exception types:
  - ✓ `CapabilityNotGrantedException`
  - ✓ `ModuleNotFoundException`
  - ✓ `UnauthorizedException`
  - ✓ `ValidationException`
- ✓ Create API error response model

#### Documentation

- ✓ Create `docs/architecture/core-abstractions.md` with comprehensive guide
- ✓ Add comprehensive XML documentation (///) to all public types
- ✓ Create `src/Core/DotNetCloud.Core/README.md` for developers

---

## Phase 0.2: Database & Data Access Layer

### DotNetCloud.Core.Data Project

**Create EF Core database abstraction and models**

#### Multi-Provider Support

- ✓ Create `IDbContextFactory<CoreDbContext>` abstraction
- ✓ Create `ITableNamingStrategy` interface for schema/prefix handling
- ✓ Implement `PostgreSqlNamingStrategy` (use schemas: `core.*`, `files.*`, etc.)
- ✓ Implement `SqlServerNamingStrategy` (use schemas)
- ✓ Implement `MariaDbNamingStrategy` (use table prefixes)
- ✓ Create provider detection logic based on connection string

#### CoreDbContext & Models

**ASP.NET Core Identity Models**

- ✓ Create `ApplicationUser` entity (extends `IdentityUser<Guid>`):
  - ✓ `string DisplayName` property
  - ✓ `string? AvatarUrl` property
  - ✓ `string Locale` property
  - ✓ `string Timezone` property
  - ✓ `DateTime CreatedAt` property
  - ✓ `DateTime? LastLoginAt` property
  - ✓ `bool IsActive` property
- ✓ Create `ApplicationRole` entity (extends `IdentityRole<Guid>`):
  - ✓ `string Description` property
  - ✓ `bool IsSystemRole` property
- ✓ Configure Identity relationships (IdentityUserClaim, IdentityUserRole, etc.)

**Organization Hierarchy Models**

- ✓ Create `Organization` entity:
  - ✓ `string Name` property
  - ✓ `string? Description` property
  - ✓ `DateTime CreatedAt` property
  - ✓ Soft-delete support (IsDeleted, DeletedAt)
- ✓ Create `Team` entity:
  - ✓ `Guid OrganizationId` FK
  - ✓ `string Name` property
  - ✓ Soft-delete support
- ✓ Create `TeamMember` entity:
  - ✓ `Guid TeamId` FK
  - ✓ `Guid UserId` FK
  - ✓ `ICollection<Guid> RoleIds` for team-scoped roles
- ✓ Create `Group` entity (cross-team permission groups):
  - ✓ `Guid OrganizationId` FK
  - ✓ `string Name` property
- ✓ Create `GroupMember` entity:
  - ✓ `Guid GroupId` FK
  - ✓ `Guid UserId` FK
- ✓ Create `OrganizationMember` entity:
  - ✓ `Guid OrganizationId` FK
  - ✓ `Guid UserId` FK
  - ✓ `ICollection<Guid> RoleIds` for org-scoped roles

**Permissions System Models**

- ✓ Create `Permission` entity:
  - ✓ `string Code` property (e.g., "files.upload")
  - ✓ `string DisplayName` property
  - ✓ `string? Description` property
- ✓ Create `Role` entity:
  - ✓ `string Name` property
  - ✓ `string? Description` property
  - ✓ `bool IsSystemRole` property
  - ✓ `ICollection<Permission> Permissions` navigation
- ✓ Create `RolePermission` junction table

**Settings Models (Three Scopes)**

- ✓ Create `SystemSetting` entity:
  - ✓ `string Module` property (which module owns this setting)
  - ✓ `string Key` property
  - ✓ `string Value` property (JSON serializable)
  - ✓ Composite key: (Module, Key)
  - ✓ `DateTime UpdatedAt` property
  - ✓ `string? Description` property
- ✓ Create `OrganizationSetting` entity:
  - ✓ `Guid Id` primary key
  - ✓ `Guid OrganizationId` FK
  - ✓ `string Key` property
  - ✓ `string Value` property
  - ✓ `string Module` property
  - ✓ `DateTime UpdatedAt` property
  - ✓ `string? Description` property
  - ✓ Unique constraint: (OrganizationId, Module, Key)
- ✓ Create `UserSetting` entity:
  - ✓ `Guid Id` primary key
  - ✓ `Guid UserId` FK
  - ✓ `string Key` property
  - ✓ `string Value` property (encrypted for sensitive data)
  - ✓ `string Module` property
  - ✓ `DateTime UpdatedAt` property
  - ✓ `string? Description` property
  - ✓ `bool IsEncrypted` property for sensitive data flag
  - ✓ Unique constraint: (UserId, Module, Key)

**Device & Module Registry Models**

- ✓ Create `UserDevice` entity:
  - ✓ `Guid UserId` FK
  - ✓ `string Name` property (e.g., "Windows Laptop")
  - ✓ `string DeviceType` property (Desktop, Mobile, etc.)
  - ✓ `string? PushToken` property
  - ✓ `DateTime LastSeenAt` property
- ✓ Create `InstalledModule` entity:
  - ✓ `string ModuleId` property (primary key, e.g., "dotnetcloud.files")
  - ✓ `Version Version` property
  - ✓ `string Status` property (Enabled, Disabled, UpdateAvailable)
  - ✓ `DateTime InstalledAt` property
- ✓ Create `ModuleCapabilityGrant` entity:
  - ✓ `string ModuleId` FK
  - ✓ `string CapabilityName` property
  - ✓ `DateTime GrantedAt` property
  - ✓ `Guid? GrantedByUserId` (admin who approved)

#### EF Core Configuration

- ✓ Create `CoreDbContext` class extending `IdentityDbContext<ApplicationUser, ApplicationRole, Guid>`
- ✓ Configure all entity relationships
- ✓ Set up automatic timestamps (CreatedAt, UpdatedAt)
- ✓ Configure soft-delete query filters
- ✓ Set up table naming strategy application
- ✓ Create design-time factory for migrations

#### Database Initialization

- ✓ Create `DbInitializer` class:
  - ✓ Database creation
  - ✓ Seed default system roles
  - ✓ Seed default permissions
  - ✓ Seed system settings
- ✓ Create migration files for each supported database:
  - ✓ PostgreSQL migrations
  - ✓ SQL Server migrations
  - ☐ MariaDB migrations (temporarily disabled - awaiting Pomelo .NET 10 support)

---

## Phase 0.3: Service Defaults & Cross-Cutting Concerns

### DotNetCloud.Core.ServiceDefaults Project

**Create shared infrastructure for all projects**

#### Logging Setup

- ✓ Configure Serilog:
  - ✓ Console sink for development
  - ✓ File sink for production
  - ✓ Structured logging format
  - ✓ Log level configuration per module
- ✓ Create log context enrichment (user ID, request ID, module name)
- ✓ Set up log filtering

#### Health Checks

- ✓ Create health check infrastructure
- ✓ Implement database health check
- ✓ Create custom health check interface for modules
- ✓ Set up health check endpoints

#### OpenTelemetry Setup

- ✓ Configure metrics collection:
  - ✓ HTTP request metrics
  - ✓ gRPC call metrics
  - ✓ Database query metrics
- ✓ Configure distributed tracing:
  - ✓ W3C Trace Context propagation
  - ✓ gRPC interceptor for tracing
  - ✓ HTTP middleware for tracing
- ✓ Implement trace exporter configuration

#### Security Middleware

- ✓ Create CORS configuration
- ✓ Add security headers middleware:
  - ✓ Content-Security-Policy
  - ✓ X-Frame-Options
  - ✓ X-Content-Type-Options
  - ✓ Strict-Transport-Security
- ✓ Create authentication/authorization middleware

#### Error Handling

- ✓ Create global exception handler middleware
- ✓ Implement consistent error response formatting
- ✓ Add request validation error handling

#### Request/Response Logging

- ✓ Create request/response logging middleware
- ✓ Configure sensitive data masking

---

## Phase 0.4: Authentication & Authorization

### OpenIddict Setup

**OAuth2/OIDC Server Implementation**

#### Core Configuration

- ✓ Add OpenIddict NuGet packages (`OpenIddict.AspNetCore`, `OpenIddict.EntityFrameworkCore`)
- ✓ Configure OpenIddict in dependency injection:
  - ✓ Server features (token/authorize/logout/userinfo/introspect/revoke endpoints)
  - ✓ Token formats (JWT default in OpenIddict 5.x; ephemeral keys for dev)
  - ✓ Scopes (openid, profile, email, offline_access)
- ✓ Create `OpenIddictApplication` entity model for registered clients
- ✓ Create `OpenIddictAuthorization` entity model for user consent tracking
- ✓ Create `OpenIddictToken` entity model for token storage
- ✓ Create `OpenIddictScope` entity model for scope definitions
- ✓ Implement OpenIddict data access layer (EF Core via `UseOpenIddict<>()` built-in config)

#### HTTP Endpoints

- ✓ Create `AuthController` with registration, login, logout, password reset endpoints
- ✓ Create `MfaController` with TOTP setup, verify, disable, and backup code endpoints
- ✓ Create `OpenIddictEndpointsExtensions` with all 6 protocol endpoints
- ✓ Implement error handling and validation on all endpoints
- ✓ Add authorization checks on protected endpoints ([Authorize] attribute)
- ✓ Create integration tests for all endpoints (18 tests, 100% passing)

#### Deployment & Configuration

- ✓ Create `DotNetCloud.Core.Server` ASP.NET Core web project
- ✓ Configure middleware pipeline (Serilog, CORS, security headers, exception handler)
- ✓ Create appsettings.json and appsettings.Development.json
- ✓ Add swagger/OpenAPI support (dev only)
- ✓ Add health check endpoints
- ✓ Configure service registration in Program.cs

---

## Phase 0.5: Module System Infrastructure

### Module Framework

**Module abstraction and lifecycle management**

#### Module Interfaces

- ✓ Create `IModule` interface with lifecycle methods
- ✓ Create `IModuleManifest` validation
- ✓ Create `IModuleLifecycle` interface:
  - ✓ `Task InitializeAsync()`
  - ✓ `Task StartAsync()`
  - ✓ `Task StopAsync()`
  - ✓ `Task DisposeAsync()`
- ✓ Create module initialization context

#### Module Registry

- ✓ Create module registry data model
- ✓ Implement module discovery mechanism
- ✓ Create module loading strategy
- ✓ Implement module versioning support

#### Capability System Implementation

- ✓ Create capability request validation
- ✓ Implement capability tier enforcement
- ✓ Create capability granting mechanism
- ✓ Implement capability injection into modules
- ✓ Handle missing capabilities gracefully (null injection)

#### Event System Implementation

- ✓ Implement in-process event bus
- ✓ Create event publishing
- ✓ Create event subscription management
- ✓ Implement event filtering by capabilities
- ✓ Create event persistence (for replay/audit)

---

## Phase 0.6: Process Supervisor & gRPC Host

### DotNetCloud.Core.Server Project

**Process management and module communication**

#### Process Supervisor

- ✓ Create module process spawning logic
- ✓ Implement process health monitoring:
  - ✓ Periodic gRPC health checks
  - ✓ Configurable check intervals
  - ✓ Health status tracking
- ✓ Implement restart policies:
  - ✓ Immediate restart
  - ✓ Exponential backoff
  - ✓ Alert-only (no auto-restart)
- ✓ Implement graceful shutdown:
  - ✓ Signal modules to stop
  - ✓ Wait for graceful termination
  - ✓ Force kill timeout
  - ✓ Drain active connections
- ✓ Implement resource limits:
  - ✓ CPU limits (cgroups on Linux)
  - ✓ Memory limits (cgroups on Linux)
  - ✓ Job Objects on Windows

#### gRPC Infrastructure

- ✓ Configure gRPC server:
  - ✓ Unix domain socket support (Linux)
  - ✓ Named pipe support (Windows)
  - ✓ TCP fallback for Docker/Kubernetes
- ✓ Create gRPC health service
- ✓ Implement gRPC interceptors:
  - ✓ Authentication/authorization interceptor
  - ✓ CallerContext injection interceptor
  - ✓ Distributed tracing interceptor
  - ✓ Error handling interceptor
  - ✓ Logging interceptor

#### Module Loading

- ✓ Create module discovery from filesystem
- ✓ Implement module manifest loading and validation
- ✓ Create capability request validation
- ✓ Implement capability grant enforcement
- ✓ Create module configuration loading

#### Inter-Process Communication

- ✓ Define gRPC service contracts for core capabilities
- ✓ Create gRPC channel management
- ✓ Implement connection pooling
- ✓ Create timeout configuration

#### Unit Tests (DotNetCloud.Core.Server.Tests)

- ✓ Create test project with MSTest, project references, InternalsVisibleTo
- ✓ ModuleProcessHandleTests (state transitions, health checks, restart counting, ToProcessInfo)
- ✓ ModuleManifestLoaderTests (validation rules, LoadAndValidate, CreateDefaultManifest)
- ✓ GrpcChannelManagerTests (channel lifecycle, caching, disposal, CallOptions)
- ✓ ModuleDiscoveryServiceTests (filesystem discovery, DLL/EXE detection, manifest detection)
- ✓ FilesControllerTests (comprehensive endpoint coverage: success/error/auth paths for CRUD, upload/download, chunks, shares, and public link resolution)

---

## Phase 0.7: Web Server & API Foundation

### ASP.NET Core Web Server

**REST API and web hosting infrastructure**

#### Kestrel Configuration

- ✓ Configure Kestrel server
- ✓ Set up HTTPS/TLS
- ✓ Configure listener addresses
- ✓ Set up HTTP/2 support

#### Reverse Proxy Support

- ✓ Generate IIS ANCM configuration template (`web.config`)
- ✓ Generate Apache `mod_proxy` configuration template
- ✓ Generate nginx configuration template
- ✓ Create reverse proxy documentation
- ✓ Implement configuration validation

#### API Versioning

- ✓ Set up URL-based versioning (`/api/v1/`, `/api/v2/`)
- ✓ Implement API version negotiation
- ✓ Configure version deprecation warnings
- ✓ Create API versioning documentation

#### Response Envelope

- ✓ Create standard response envelope model:
  - ✓ `bool success` property
  - ✓ `object data` property
  - ✓ `PaginationInfo pagination` property (when applicable)
- ✓ Create error response envelope:
  - ✓ `string code` property
  - ✓ `string message` property
  - ✓ `object details` property
- ✓ Implement response envelope middleware
- ✓ Create response envelope documentation

#### Error Handling

- ✓ Create error handling middleware
- ✓ Implement standard error codes
- ✓ Configure error response formatting
- ✓ Add stack trace handling (dev vs. production)
- ✓ Create error logging

#### Rate Limiting

- ✓ Implement rate limiting middleware
- ✓ Configure rate limits per module
- ✓ Create rate limit headers (X-RateLimit-\*)
- ✓ Implement configurable rate limits
- ✓ Create admin configuration endpoint

#### OpenAPI/Swagger

- ✓ Integrate Swashbuckle (OpenAPI generation)
- ✓ Configure Swagger UI
- ✓ Enable OpenAPI schema generation
- ✓ Create API documentation from code comments

#### CORS

- ✓ Configure CORS policies
- ✓ Create origin whitelist configuration
- ✓ Implement allowed methods/headers
- ✓ Add credentials handling

---

## Phase 0.8: Real-Time Communication (SignalR)

### SignalR Hub Setup

**Real-time messaging infrastructure**

#### SignalR Configuration

- ✓ Configure SignalR services
- ✓ Set up connection tracking
- ✓ Configure reconnection policies
- ✓ Set up keep-alive intervals

#### Core Hub Implementation

- ✓ Create base SignalR hub with authentication/authorization
- ✓ Implement connection lifecycle handlers
- ✓ Create user connection tracking
- ✓ Implement connection grouping per channel/room

#### Real-Time Broadcast Infrastructure

- ✓ Create `IRealtimeBroadcaster` capability interface:
  - ✓ `Task BroadcastAsync(string group, string eventName, object message)`
  - ✓ `Task SendToUserAsync(Guid userId, string eventName, object message)`
  - ✓ `Task SendToRoleAsync(string role, string eventName, object message)`
- ✓ Implement broadcast service in core
- ✓ Create module notification interface

#### Presence Tracking

- ✓ Implement presence update mechanism
- ✓ Track online/offline status
- ✓ Create last seen timestamps
- ✓ Implement presence queries

#### WebSocket Configuration

- ✓ Configure WebSocket support
- ✓ Set up WebSocket keep-alive
- ✓ Configure connection limits

---

## Phase 0.9: Authentication API Endpoints

### Core Authentication Endpoints

**REST endpoints for authentication flows**

#### User Authentication

- ✓ `POST /api/v1/core/auth/register` - User registration
- ✓ `POST /api/v1/core/auth/login` - User login (returns tokens)
- ✓ `POST /api/v1/core/auth/logout` - Revoke tokens
- ✓ `POST /api/v1/core/auth/refresh` - Refresh access token
- ✓ `GET /api/v1/core/auth/user` - Get current user info

#### OAuth2/OIDC Integration

- ✓ `GET /api/v1/core/auth/external-login/{provider}` - External provider sign-in
- ✓ `GET /api/v1/core/auth/external-callback` - External provider callback
- ✓ `GET /.well-known/openid-configuration` - OIDC discovery

#### MFA Management

- ✓ `POST /api/v1/core/auth/mfa/totp/setup` - Setup TOTP
- ✓ `POST /api/v1/core/auth/mfa/totp/verify` - Verify TOTP code
- ✓ `POST /api/v1/core/auth/mfa/passkey/setup` - Setup passkey
- ✓ `POST /api/v1/core/auth/mfa/passkey/verify` - Verify passkey
- ✓ `GET /api/v1/core/auth/mfa/backup-codes` - Generate backup codes

#### Password Management

- ✓ `POST /api/v1/core/auth/password/change` - Change password
- ✓ `POST /api/v1/core/auth/password/forgot` - Request password reset
- ✓ `POST /api/v1/core/auth/password/reset` - Reset password with token

#### Device Management

- ✓ `GET /api/v1/core/auth/devices` - List user's devices
- ✓ `DELETE /api/v1/core/auth/devices/{deviceId}` - Remove device

---

## Phase 0.10: User & Admin Management

### User Management Endpoints

- ✓ `GET /api/v1/core/users` - List users (admin only)
- ✓ `GET /api/v1/core/users/{userId}` - Get user details
- ✓ `PUT /api/v1/core/users/{userId}` - Update user profile
- ✓ `DELETE /api/v1/core/users/{userId}` - Delete user (admin only)
- ✓ `POST /api/v1/core/users/{userId}/disable` - Disable user (admin only)
- ✓ `POST /api/v1/core/users/{userId}/enable` - Enable user (admin only)
- ✓ `POST /api/v1/core/users/{userId}/reset-password` - Admin password reset

### Admin Management Endpoints

- ✓ `GET /api/v1/core/admin/settings` - List all settings
- ✓ `GET /api/v1/core/admin/settings/{key}` - Get specific setting
- ✓ `PUT /api/v1/core/admin/settings/{key}` - Update setting
- ✓ `DELETE /api/v1/core/admin/settings/{key}` - Delete setting
- ✓ `GET /api/v1/core/admin/modules` - List installed modules
- ✓ `GET /api/v1/core/admin/modules/{moduleId}` - Get module details
- ✓ `POST /api/v1/core/admin/modules/{moduleId}/start` - Start module
- ✓ `POST /api/v1/core/admin/modules/{moduleId}/stop` - Stop module
- ✓ `POST /api/v1/core/admin/modules/{moduleId}/restart` - Restart module
- ✓ `POST /api/v1/core/admin/modules/{moduleId}/capabilities/{capability}/grant` - Grant capability
- ✓ `DELETE /api/v1/core/admin/modules/{moduleId}/capabilities/{capability}` - Revoke capability
- ✓ `GET /api/v1/core/admin/health` - System health check

---

## Phase 0.11: Web UI Shell (Blazor)

### DotNetCloud.UI.Web Project

**Blazor application shell and layout**

#### Project Setup

- ✓ Create Blazor project using InteractiveAuto render mode
- ✓ Set up project file with necessary dependencies
- ✓ Configure authentication/authorization services

#### Authentication Pages

- ✓ Create login page component
- ✓ Create registration page component
- ✓ Create password reset page component
- ✓ Create MFA verification page component
- ✓ Create external provider login page

#### User Home Dashboard

- ✓ Create role-aware non-admin home dashboard at `/`
- ✓ Show non-admin quick actions and module app cards on home page
- ✓ Keep admin shortcuts visible only to users with `RequireAdmin`

#### Admin Dashboard

- ✓ Create admin layout/shell
- ✓ Create dashboard home page
- ✓ Create module management section:
  - ✓ Module list
  - ✓ Module details
  - ✓ Module action buttons (start/stop/restart)
- ✓ Create user management section:
  - ✓ User list with pagination
  - ✓ User detail view
  - ✓ User creation form
  - ✓ User editing form
- ✓ Create settings management section:
  - ✓ System settings
  - ✓ Backup/restore settings (BackupSettings.razor admin page)
- ✓ Create health monitoring dashboard

#### Module Plugin System

- ✓ Create dynamic component loader for modules
- ✓ Implement module navigation registration
- ✓ Create module UI extension mechanism
- ✓ Build module communication interface
- ✓ Register installed/enabled Files and Chat modules into sidebar nav at startup
- ✓ Refresh module sidebar/page registrations automatically when module enable/disable status changes
- ✓ Add authenticated module route hosts (`/apps/files`, `/apps/chat`) via `ModulePageHost`
- ✓ Enable interactive render mode on module host routes so module UI buttons/actions execute
- ✓ Wire Files actions to real services (create folder, upload, delete, and refresh listing)
- ✓ Wire Chat channel list/create actions to real services for persisted channels
- ✓ Register in-process module data contexts for Files/Chat actions in the web app runtime
- ✓ Make folder names directly clickable to navigate and replace Files placeholder text icons with real icons
- ✓ Align Files/Chat module storage with configured core DB provider (PostgreSQL/MSSQL), avoiding SQLite fallback
- ✓ Ensure Files/Chat module tables are explicitly created in shared DB when sentinel tables are missing
- ✓ Fix Files filtered index SQL for provider compatibility so PostgreSQL module table creation succeeds
- ✓ Restyle Files upload dialog/progress panel with polished spacing, controls, and icons (remove scaffold placeholder tokens)
- ✓ Add core Files page layout styling (breadcrumbs/actions/list rows) and CSS cache-bust query to ensure clients receive updated styles
- ✓ Refine Files sidebar collapsed navigation to match the Tracks module pattern (icon-only collapsed state, no clipped title/quota text, correct active-state styling)
- ✓ Auto-create default quota on first upload initiation and surface upload errors in UI (avoid silent failed uploads)
- ✓ Keep upload dialog open on failed uploads and only close after full success so users can see actionable errors
- ✓ Add top-level StartUpload exception handling so pre-upload failures surface as visible error messages (no silent no-op clicks)
- ✓ Keep upload dialog `InputFile` mounted during active uploads to prevent Blazor `_blazorFilesById` invalidation on multi-file selections
- ✓ Add `FileUploadComponent` regression unit tests for upload-state file-selection behavior (`tests/DotNetCloud.Modules.Files.Tests/UI/FileUploadComponentTests.cs`)
- ✓ Defer file-byte reads to upload-time and cache per-file bytes during processing to keep selection responsive while avoiding reader lifecycle failures
- ✓ Normalize low-level upload reader errors into actionable user-facing messages in the upload dialog
- ✓ Pre-buffer all pending selected files at upload start so later files in a batch do not fail after earlier file network work
- ✓ Default Files storage path to `DOTNETCLOUD_DATA_DIR/storage` when `Files:StoragePath` is unset, avoiding read-only `/opt` writes under hardened systemd
- ✓ Persist ASP.NET Core DataProtection key ring to `DOTNETCLOUD_DATA_DIR/data-protection-keys` so auth/antiforgery tokens survive restarts
- ✓ Persist Files/Chat module data across server restarts/redeploys using on-disk module databases

#### Theme & Branding

- ✓ Create base theme/styling system
- ✓ Implement light/dark mode toggle
- ✓ Create responsive layout components
- ✓ Build reusable navigation components
- ✓ Set up brand assets/logos

#### Error & Notification UI

- ✓ Create error boundary component
- ✓ Implement exception display
- ✓ Create notification/toast system
- ✓ Implement loading indicators
- ✓ Create confirmation dialogs

---

## Phase 0.12: Shared UI Components

### DotNetCloud.UI.Shared Project

**Reusable Blazor components**

#### Form Components

- ✓ Create input text component
- ✓ Create password input component
- ✓ Create email input component
- ✓ Create select dropdown component
- ✓ Create checkbox component
- ✓ Create radio button component
- ✓ Create textarea component
- ✓ Create date picker component
- ✓ Create form validation display

#### Data Display Components

- ✓ Create data table/grid component
  - ✓ Sorting
  - ✓ Filtering
  - ✓ Pagination
- ✓ Create paginator component
- ✓ Create breadcrumb component
- ✓ Create tabs component
- ✓ Create accordion component

#### Dialog Components

- ✓ Create modal dialog component
- ✓ Create confirmation dialog component
- ✓ Create alert dialog component

#### Navigation Components

- ✓ Create sidebar navigation component
- ✓ Create top navigation bar component
- ✓ Create menu component
- ✓ Create button component with variants

#### Notification Components

- ✓ Create toast notification component
- ✓ Create alert component
- ✓ Create badge component

#### Layout Components

- ✓ Create card component
- ✓ Create panel component
- ✓ Create section component
- ✓ Create responsive grid component

#### Styling

- ✓ Create CSS/SCSS base styles
- ✓ Set up theme color variables
- ✓ Create utility classes
- ✓ Implement responsive breakpoints

---

## Phase 0.13: CLI Management Tool

### DotNetCloud.CLI Project

**Command-line interface for administration**

#### Project Setup

- ✓ Create console application project
- ✓ Integrate System.CommandLine library
- ✓ Set up command structure

#### Core Commands

##### Setup Command

- ✓ `dotnetcloud setup` - Interactive first-run wizard
  - ✓ Database selection (PostgreSQL/SQL Server/MariaDB)
  - ✓ Connection string configuration
  - ✓ Admin user creation
  - ✓ Admin MFA setup
  - ✓ Organization setup
  - ✓ TLS/HTTPS configuration
  - ✓ Let's Encrypt setup (optional)
  - ✓ Module selection
  - ✓ Save configuration

##### Service Commands

- ✓ `dotnetcloud serve` - Start all services
- ✓ `dotnetcloud stop` - Graceful shutdown
- ✓ `dotnetcloud status` - Show service & module status
- ✓ `dotnetcloud status` probes listener/health endpoints and reports process-vs-port mismatch warnings
- ✓ `dotnetcloud restart` - Restart all services

##### Module Commands

- ✓ `dotnetcloud module list` - List all modules
- ✓ `dotnetcloud module start {module}` - Start specific module
- ✓ `dotnetcloud module stop {module}` - Stop specific module
- ✓ `dotnetcloud module restart {module}` - Restart specific module
- ✓ `dotnetcloud module install {module}` - Install module
- ✓ `dotnetcloud module uninstall {module}` - Uninstall module

##### Component Commands

- ✓ `dotnetcloud component status {component}` - Check component status
- ✓ `dotnetcloud component restart {component}` - Restart component

##### Logging Commands

- ✓ `dotnetcloud logs` - View system logs
- ✓ `dotnetcloud logs {module}` - View module-specific logs
- ✓ `dotnetcloud logs --level {level}` - Filter by log level
- ✓ Read-only commands handle unreadable system config (`/etc/dotnetcloud/config.json`) without crashing

##### Backup Commands

- ✓ `dotnetcloud backup` - Create backup
- ✓ `dotnetcloud backup --output {path}` - Backup to specific location
- ✓ `dotnetcloud restore {file}` - Restore from backup
- ✓ `dotnetcloud backup --schedule daily` - Schedule automatic backups

##### Miscellaneous Commands

- ✓ `dotnetcloud update` - Check and apply updates
- ✓ `dotnetcloud help` - Show command reference
- ✓ `dotnetcloud help {command}` - Show command-specific help

#### Unit Tests

- ✓ Create `DotNetCloud.CLI.Tests` project with MSTest
- ✓ `CliConfigTests` — 16 tests (defaults, JSON roundtrip, save/load)
- ✓ `ConsoleOutputTests` — 16 tests (FormatStatus color indicators, case insensitivity)
- ✓ `SetupCommandTests` — 9 tests (MaskConnectionString, command structure)
- ✓ `CommandStructureTests` — 25 tests (all commands, subcommands, options, arguments)
- ✓ `SystemdServiceHelperTests` — 15 tests (Type=forking, PIDFile, no ExecStop, hardening, systemd format validation)

---

## Phase 0.14: Example Module Reference

### DotNetCloud.Modules.Example Project

**Reference implementation of a module**

#### Module Structure

- ✓ Create `DotNetCloud.Modules.Example` (core logic)
- ✓ Create `DotNetCloud.Modules.Example.Data` (EF Core context)
- ✓ Create `DotNetCloud.Modules.Example.Host` (gRPC host)

#### Module Implementation

- ✓ Create `ExampleModuleManifest` implementing `IModuleManifest`
- ✓ Create example data model
- ✓ Create `ExampleDbContext` extending `DbContext`
- ✓ Implement module initialization
- ✓ Create example API endpoints
- ✓ Create example capability interface usage
- ✓ Create example event publishing/subscription

#### Blazor UI Components

- ✓ Create example module page
- ✓ Create example data display
- ✓ Create example form

#### gRPC Service

- ✓ Define `.proto` service
- ✓ Implement gRPC service
- ✓ Create health check implementation

#### Documentation

- ✓ Create inline code documentation
- ✓ Write module-specific README
- ✓ Document manifest and capabilities
- ✓ Provide example usage patterns

#### Unit Tests

- ✓ Create `DotNetCloud.Modules.Example.Tests` project with MSTest
- ✓ `ExampleModuleManifestTests` — 10 tests (Id, Name, Version, capabilities, events, IModuleManifest)
- ✓ `ExampleModuleTests` — 22 tests (lifecycle, notes CRUD, event pub/sub, error states)
- ✓ `ExampleNoteTests` — 10 tests (Id generation, defaults, record semantics)
- ✓ `EventTests` — 5 tests (NoteCreatedEvent, NoteDeletedEvent, IEvent interface, record semantics)
- ✓ `NoteCreatedEventHandlerTests` — 4 tests (IEventHandler interface, logging, cancellation)

---

## Phase 0.15: Testing Infrastructure

### Unit Test Infrastructure

- ✓ Create `DotNetCloud.Core.Tests` project
- ✓ Set up MSTest test framework
- ✓ Integrate Moq for mocking
- ✓ Create test fixtures for:
  - ✓ Capability system (CapabilityTier enum tests)
  - ✓ Event bus (IEventBus, IEvent, IEventHandler contracts)
  - ✓ Identity/authorization (CallerContext validation, role checking)
  - ✓ Module system (IModule, IModuleLifecycle, IModuleManifest)
- ✓ Create fake implementations of core interfaces
- ✓ Create test helpers and fixtures (Moq-based)
- ✓ Test coverage: 108 test cases across 6 test classes

### Integration Test Infrastructure

- ✓ Create `DotNetCloud.Integration.Tests` project
- ✓ Create Docker container fixture and config (infrastructure only — not yet used by tests)
- ✓ Create database initialization scripts
- ✓ Build multi-database test matrix:
  - ✓ PostgreSQL tests (InMemory with naming strategy)
  - ✓ SQL Server tests (InMemory with naming strategy)
  - ✓ MariaDB tests (InMemory with naming strategy)
  - ✓ Real Docker-based database tests (PostgreSQL via DatabaseContainerFixture + WSL Docker)
  - ✓ SQL Server local testing (SQL Server Express, Windows Auth, shared memory protocol)
  - ✓ LocalSqlServerDetector with auto-detection, isolated test DB creation, cleanup
  - ✓ Container crash detection (docker ps alive-check + host TCP verification)
  - ✓ GETUTCDATE() → CURRENT_TIMESTAMP fix for cross-database compatibility
- ✓ Create gRPC client test helpers
- ✓ Build API integration test framework
- ✓ Create test data builders

### Test Coverage

- ✓ Establish comprehensive unit tests for Phase 0.1 (80%+ coverage)
- ✓ Create coverage reporting framework
- ✓ Set up CI/CD coverage checks (coverlet + Cobertura in GitHub/Gitea Actions)

---

## Phase 0.16: Internationalization (i18n) Infrastructure

### i18n Setup

- ✓ Create resource files structure (`Resources/*.resx`)
- ✓ Configure `IStringLocalizer` dependency injection
- ✓ Create translation key constants
- ✓ Set up default language (English)
- ✓ Implement user locale selection
- ✓ Configure number/date/time formatting per locale
- ✓ Create Blazor component for locale switching
- ✓ Document translation contribution process

### Resource Files

- ✓ Create core UI strings
- ✓ Create error message strings
- ✓ Create validation message strings
- ✓ Create module strings (namespace per module)
- ✓ Set up translation workflow (Git-based PR workflow documented; Weblate planned for future)

### Unit Tests

- ✓ `SupportedCulturesTests` — 11 tests (DefaultCulture, All array, DisplayNames, GetCultureInfos, BCP-47 validation)
- ✓ `TranslationKeysTests` — 13 tests (nested class structure, non-empty constants, global uniqueness, expected key values)
- ✓ `CultureControllerTests` — 15 tests (cookie setting, redirect behavior, empty/null guards, all supported cultures)

---

## Phase 0.17: Logging & Observability

### Logging Configuration

- ✓ Configure Serilog in all projects
- ✓ Set up log levels (Debug, Information, Warning, Error, Fatal)
- ✓ Configure file logging:
  - ✓ Log file rotation
  - ✓ Retention policies
- ✓ Set up structured logging
- ✓ Create context enrichment (user ID, request ID, module)

### Health Checks

- ✓ Create `/health` endpoint returning module status
- ✓ Implement liveness probe
- ✓ Implement readiness probe
- ✓ Add to admin dashboard

### Metrics & Tracing

- ✓ Configure OpenTelemetry collectors
- ✓ Set up Prometheus metrics export (opt-in via `EnablePrometheusExporter` config)
- ✓ Implement distributed tracing
- ✓ Configure trace exporters

### Unit Tests

- ✓ `SerilogConfigurationTests` — 11 tests (defaults, log levels, file rotation, modules)
- ✓ `ModuleLogFilterTests` — 9 tests (exclusion, module levels, precedence)
- ✓ `LogEnricherTests` — 10 tests (property push/pop, context enrichment)
- ✓ `TelemetryConfigurationTests` — 14 tests (options defaults, activity sources, Prometheus)
- ✓ `HealthCheckTests` — 14 tests (StartupHealthCheck, ModuleHealthCheckResult, adapter, enum)

### Documentation

- ✓ Create `docs/architecture/observability.md` with comprehensive guide

---

## Phase 0.18: CI/CD Pipeline Setup

### Build Pipeline

- ✓ Create build workflow (`.github/workflows/build-test.yml`, `.gitea/workflows/build-test.yml`)
- ✓ Implement project compilation (dotnet build in Release configuration)
- ✓ Set up artifact generation (Core Server + CLI published and uploaded)
- ✓ Configure build caching (NuGet package cache keyed by .csproj + Directory.Build.props hash)

### Test Pipeline

- ✓ Create unit test workflow (MSTest with TRX logging)
- ✓ Set up multi-database integration tests (PostgreSQL + SQL Server service containers)
- ✓ Configure code coverage reporting (coverlet XPlat Code Coverage, Cobertura format)
- ✓ Set up coverage gates (coverage artifacts uploaded; exclude test projects and migrations)

### Package Pipeline (Skeleton)

- ✓ Create `.deb` package build script (`tools/packaging/build-deb.ps1` — skeleton)
- ✓ Create `.rpm` package build script (`tools/packaging/build-rpm.ps1` — skeleton)
- ✓ Create Windows MSI build script (`tools/packaging/build-msi.ps1` — skeleton)
- ✓ Create Docker image build (`Dockerfile` multi-stage + `tools/packaging/build-docker.ps1` + `docker-compose.yml` + `.dockerignore`)
- ✓ Add CMD-first Windows desktop ZIP installer (`tools/packaging/build-desktop-client-bundles.ps1` generates `install.cmd` / `uninstall.cmd` without PowerShell execution-policy dependency)

---

## Phase 0.19: Documentation

### Core Documentation

- ✓ Architecture overview documentation (`docs/architecture/ARCHITECTURE.md`)
- ✓ Development environment setup guide (`docs/development/README.md`, `IDE_SETUP.md`, `DATABASE_SETUP.md`, `DOCKER_SETUP.md`)
- ✓ Bare-metal server installation and fast redeploy runbook (`docs/admin/server/INSTALLATION.md`)
- ✓ Add one-command bare-metal redeploy helper script (`tools/redeploy-baremetal.sh`) and document usage in server install guide
- ✓ Clarify local-server workflow: prefer source redeploy helper for local changes and keep `tools/install.sh` in parity for fresh-machine installs
- ✓ Ensure redeploy helper health probe parity with installer defaults (auto-tries HTTPS `:15443` and HTTP `:5080`)
- ✓ Harden `tools/redeploy-baremetal.sh` to repair build-output ownership and purge stale normal/malformed Debug outputs before Linux Release build/publish runs
- ✓ Align installer and `dotnetcloud setup` local health probes with configured Kestrel ports, including self-signed HTTPS checks; clarify that `5080`/`5443` are internal defaults while `15443` is a reverse-proxy/public deployment port
- ✓ Make installer print explicit direct local access URLs, health probe URLs, and the internal-Kestrel-vs-reverse-proxy port distinction at completion
- ✓ Add beginner-friendly setup mode and fresh-install default flow that auto-selects the recommended local PostgreSQL + self-signed HTTPS path and ends with a plain-language summary of chosen settings and next steps
- ✓ Make upgrade runs preserve the same beginner-friendly clarity by printing a plain-language upgrade summary, stating whether setup review is required, and re-showing access URLs plus next steps
- ✓ Split beginner setup into the two real first-install cases: private/local test installs and public-domain installs, with different end summaries and honest reverse-proxy guidance for public domains
- ✓ Expand beginner setup to cover all three real deployment shapes: private/local, public behind a reverse proxy, and public direct on DotNetCloud itself, while explicitly explaining why a reverse proxy is still recommended for most public installs
- ✓ Add a dedicated beginner reverse-proxy guide with Apache-first walkthrough, Caddy alternative, and setup-summary links for public-domain users who need help
- ✓ Add a separate Windows + IIS beginner installation path with a PowerShell installer (`tools/install-windows.ps1`) and Windows IIS guide (`docs/admin/server/WINDOWS_IIS_INSTALL_GUIDE.md`)
- ✓ Align Windows IIS path with a true service-backed architecture by running the core server as a native Windows Service host (not `dotnetcloud serve`), ensuring machine-level config/data env propagation during setup/runtime, and documenting the rationale in `docs/admin/server/WINDOWS_SERVICE_ARCHITECTURE_NOTES.md`
- ✓ Add repository commit template (`.gitmessage`) and CONTRIBUTING guidance for detailed AI-assisted commit messages
- ✓ Add README developer quick setup note for commit template configuration (`git config commit.template .gitmessage`)
- ✓ Running tests documentation (`docs/development/RUNNING_TESTS.md`)
- ✓ Contributing guidelines (`CONTRIBUTING.md`)
- ✓ License documentation (`LICENSE` — AGPL-3.0)

### API Documentation

- ✓ API endpoint reference (`docs/api/README.md`)
- ✓ Authentication flow documentation (`docs/api/AUTHENTICATION.md`)
- ✓ Response format documentation (`docs/api/RESPONSE_FORMAT.md`)
- ✓ Error handling documentation (`docs/api/ERROR_HANDLING.md`)

### Module Development Guide (Skeleton)

- ✓ Module architecture overview (`docs/guides/MODULE_DEVELOPMENT.md`)
- ✓ Creating a module (`docs/guides/MODULE_DEVELOPMENT.md`)
- ✓ Module manifest documentation (`docs/guides/MODULE_DEVELOPMENT.md`)
- ✓ Capability interfaces documentation (`docs/architecture/core-abstractions.md`, `docs/guides/MODULE_DEVELOPMENT.md`)

---

## Phase 0 Completion Checklist

### Functionality Verification

- ✓ All projects compile without errors (20 projects, 0 warnings, 0 errors)
- ✓ All unit tests pass (2,242 passed, 0 failed across 12 test projects)
- ✓ All integration tests pass against PostgreSQL (6/6 via Docker + WSL)
- ✓ All integration tests pass against SQL Server (CI service containers + local SQL Server Express via Windows Auth)
- ☐ All integration tests pass against MariaDB (Pomelo lacks .NET 10 support)
- ✓ No compiler warnings (0 warnings in build output)
- ✓ Docker container builds successfully (multi-stage Dockerfile, docker-compose.yml, .dockerignore)
- ✓ Docker containers run and pass health checks (Dockerfile HEALTHCHECK + docker-compose healthcheck using wget, all modules in CI solution filter)
- ✓ gRPC endpoints respond correctly (ExampleGrpcService + LifecycleService mapped, interceptors, health service)
- ✓ REST API endpoints respond correctly (69 auth integration tests pass; all controllers verified)
- ✓ SignalR hub accepts connections and broadcasts messages (CoreHub with auth, presence, broadcast)
- ✓ Authentication flows work end-to-end (registration, login, MFA, token refresh — 69 tests)
- ✓ Admin endpoints enforce permissions correctly ([Authorize(Policy = RequireAdmin)] verified)
- ✓ Module loading and capability injection work correctly (discovery, manifest, capability validation — 259 server tests)
- ✓ Web UI displays and functions correctly (login, register, dashboard, admin pages — all .razor files verified)
- ✓ CLI commands execute and produce expected results (66 CLI tests pass, all command categories)
- ✓ Application runs on both Windows and Linux without errors (cross-platform .NET 10, CI on Linux)
- ✓ Logs are written to file with correct formatting and rotation (Serilog file sink configured and tested)
- ✓ Health check endpoint returns correct status (database, startup, module health checks)
- ✓ OpenAPI documentation is generated and accurate (Swashbuckle integrated, dev Swagger UI)
- ✓ Internationalization infrastructure is set up and functional (SupportedCultures, TranslationKeys, CultureSelector, .resx)
- ✓ Observability features (logging, metrics, tracing) are configured and working (Serilog, OpenTelemetry, Prometheus)
- ✓ CI/CD pipelines are configured and passing (.github + .gitea workflows)
- ✓ Documentation is written and comprehensive (21 docs across architecture, development, API, guides)

### Authentication & Authorization

- ✓ User registration works (integration tests pass)
- ✓ User login works (integration tests pass)
- ✓ TOTP MFA works (setup, verify, disable, backup codes — integration tests pass)
- ✓ Token refresh works (integration tests pass)
- ✓ Admin authentication works ([Authorize(RequireAdmin)] enforced)
- ✓ Permission checks work (role-based + policy-based authorization)
- ✓ Device management endpoints work (GET list + DELETE device)
- ✓ External provider login works (external-login/{provider} + callback endpoints)
- ✓ Password reset flows work (forgot + reset + change — integration tests pass)

### Module System

#### Core Module Functionality (Verified — 51 module tests + 259 server tests pass)

- ✓ Example module loads successfully (ExampleModule + ExampleModuleManifest implemented)
- ✓ Health checks pass (ExampleHealthCheck in gRPC host)
- ✓ Module manifest validation works (ModuleManifestLoader with validation rules)
- ✓ Capability system works (CapabilityValidator with tier enforcement)
- ✓ Event bus works (IEventBus pub/sub, NoteCreatedEvent/NoteDeletedEvent)
- ✓ Module lifecycle management works (initialize/start/stop/dispose — 22 lifecycle tests)
- ✓ gRPC communication with module works (ExampleGrpcService + LifecycleService mapped)
- ✓ Module API endpoints work (gRPC service + minimal REST health endpoint)
- ✓ Module UI components load in web UI (ModulePageHost + example page)
- ✓ Module configuration via admin dashboard works (AdminController settings/module endpoints)
- ✓ Module logging works and is enriched with context (LogEnricher, module-scoped filtering)
- ✓ Module errors are handled gracefully (ErrorHandlingInterceptor, GlobalExceptionHandler)
- ✓ Module unit tests pass (51/51 across 5 test classes)
- ✓ Module documentation is complete (README, inline XML docs, manifest docs)
- ✓ Module example usage is documented (usage patterns in README)
- ✓ Module integration tests pass (gRPC host integration verified)
- ✓ Module internationalization works (i18n infrastructure available to modules)
- ✓ Module observability features work (OpenTelemetry metrics + distributed tracing)

#### Module Management (CLI + Admin Dashboard)

- ✓ Module can be started/stopped/restarted via CLI (module start/stop/restart commands)
- ✓ Module can be granted/revoked capabilities via CLI (admin endpoints)
- ✓ Module can be monitored via CLI (module list, component status, logs commands)
- ✓ Module can be installed/uninstalled via CLI (module install/uninstall commands)
- ✓ Module can be listed via CLI (module list command — 25 structure tests pass)
- ✓ Module can be managed via admin dashboard (start/stop/restart, grant/revoke capabilities)
- ✓ Module can publish/subscribe to events (IEventBus + event handlers)
- ✓ Module can broadcast real-time messages via SignalR (IRealtimeBroadcaster capability)
- ✓ Module can access user context via CallerContext (CallerContextInterceptor)
- ✓ Module can log messages with context enrichment (LogEnricher + module context)
- ✓ Module can expose API endpoints via gRPC (ExampleGrpcService)
- ✓ Module can expose API endpoints via REST (if applicable)
- ✓ Module can serve Blazor UI components in the web dashboard (ModulePageHost)
- ✓ Module can be configured via admin dashboard (settings endpoints)
- ✓ Module can be configured via CLI (module commands)
- ✓ Module can be monitored via health checks (ExampleHealthCheck)
- ✓ Module can be monitored via logs (Serilog + module-scoped log filter)
- ✓ Module can be monitored via metrics (OpenTelemetry activity sources)
- ✓ Module can be monitored via tracing (distributed tracing interceptor)
- ✓ Module can be internationalized (i18n infrastructure)
- ✓ Module can be documented with inline comments and external README
- ✓ Module can be tested with unit tests and integration tests

#### Module Deployment

- ✓ Module can be deployed and run in Docker container (Dockerfile + docker-compose)
- ✓ Module can be deployed and run on Windows (cross-platform .NET 10)
- ✓ Module can be deployed and run on Linux (cross-platform .NET 10, CI on Linux)
- ✓ Module can be deployed and run in Kubernetes (Helm chart scaffold at deploy/helm/dotnetcloud/)
- ✓ Module can be deployed and run on bare metal (systemd/Windows service support)
- ✓ Module can be deployed and run in cloud environments (Docker support enables this)

#### Module as Reference Implementation

- ✓ Module serves as a reference implementation for new module development
- ✓ Module serves as a testbed for new core framework features
- ✓ Module demonstrates best practices in module development
- ✓ Module serves as a starting point and template for new modules
- ✓ Module serves as a showcase for module capabilities and features
- ✓ Module serves as a learning resource for new developers in the ecosystem

### Web UI

- ✓ Login page displays (Login.razor, Register.razor, ForgotPassword.razor, ResetPassword.razor)
- ✓ Admin dashboard displays (Dashboard.razor in Web.Client)
- ✓ User can log in and see dashboard (auth flow + dashboard pages)
- ✓ Module list displays correctly (ModuleList.razor + ModuleDetail.razor)
- ✓ Settings pages display (Settings.razor)
- ✓ Health dashboard displays (Health.razor)
- ✓ Module UI components load correctly (ModulePageHost.razor + ModuleUiRegistry)
- ✓ Internationalization works (CultureSelector component, .resx files, locale switching)
- ✓ Error handling works (DncErrorDisplay, ErrorDisplay, DncToast, error boundaries)
- ✓ Responsive design works (DncGrid, responsive breakpoints in CSS)
- ✓ Theme switching works (light/dark mode toggle in base theme)

### CLI

- ✓ `dotnetcloud setup` wizard runs (SetupCommand.cs — 9 setup tests pass)
- ✓ Configuration is saved correctly (CliConfiguration JSON roundtrip — 16 tests pass)
- ✓ `dotnetcloud serve` starts services (ServiceCommands.cs)
- ✓ `dotnetcloud status` displays correctly (ServiceCommands.cs + ConsoleOutput formatting)
- ✓ `dotnetcloud help` works (MiscCommands.cs — 25 command structure tests pass)

### Deployment

- ✓ Application runs on Windows (verified directly, cross-platform .NET 10)
- ✓ Application runs on Linux (CI workflows run on ubuntu-latest)
- ✓ Logs are written to file (Serilog file sink with rotation and retention)
- ✓ Health checks are working (MapDotNetCloudHealthChecks — database, startup, module)

---

## Phase 1: Files (Public Launch)

**Goal:** File upload/download/browse/share + working desktop sync client.

**Expected Duration:** 8-12 weeks

### Phase 1 Overview

This phase implements the core Files module, which is the primary public-facing feature. It includes:

1. File storage and management backend
2. File browser UI
3. Desktop sync client (SyncTray — single process, sync engine in-process)
4. Collabora CODE integration for online document editing
5. Complete REST API with bulk operations
6. Comprehensive documentation

### Milestone Criteria

- [ ] Files can be uploaded, downloaded, renamed, moved, copied, and deleted
- [ ] Folders can be created, renamed, moved, and deleted
- [ ] Chunked upload with content-hash deduplication works end-to-end
- [ ] File versioning stores history and allows restore to previous versions
- [ ] Sharing works for users, teams, groups, and public links with permissions
- ✓ Trash bin supports soft-delete, restore, permanent delete, and auto-cleanup
- [ ] Storage quotas enforce per-user limits and display usage
- [ ] Collabora CODE integration enables browser-based document editing via WOPI
- ✓ File browser Blazor UI supports grid/list view, drag-drop, preview, and sharing
- [ ] Desktop sync client (SyncTray) syncs files bidirectionally
- ✓ Bulk operations (move, copy, delete) work via REST API
- [ ] All unit and integration tests pass against PostgreSQL and SQL Server
- [ ] gRPC communication with the Files module host works correctly
- ✓ REST API documentation is generated via OpenAPI/Swagger
- [ ] Admin can manage quotas and module settings via dashboard
- [ ] Files sync between server and Windows desktop client

---

## Phase 1.1: Files Core Abstractions & Data Models

### DotNetCloud.Modules.Files Project

**Create file module project and core domain models**

#### Project Setup

- ✓ Create `DotNetCloud.Modules.Files` class library project
- ✓ Create `DotNetCloud.Modules.Files.Data` class library project (EF Core)
- ✓ Create `DotNetCloud.Modules.Files.Host` ASP.NET Core project (gRPC host)
- ✓ Create `DotNetCloud.Modules.Files.Tests` test project (MSTest)
- ✓ Add projects to `DotNetCloud.sln`
- ✓ Configure project references and `InternalsVisibleTo`

#### Files Module Manifest

- ✓ Create `FilesModuleManifest` implementing `IModuleManifest`:
  - ✓ `Id` → `"dotnetcloud.files"`
  - ✓ `Name` → `"Files"`
  - ✓ `Version` → `"1.0.0"`
  - ✓ `RequiredCapabilities` → `INotificationService`, `IStorageProvider`, `IUserDirectory`, `ICurrentUserContext`
  - ✓ `PublishedEvents` → `FileUploadedEvent`, `FileDeletedEvent`, `FileMovedEvent`, `FileSharedEvent`, `FileRestoredEvent`
  - ✓ `SubscribedEvents` → (none)

#### FileNode Model

- ✓ Create `FileNode` entity:
  - ✓ `Guid Id` primary key
  - ✓ `string Name` property (display name)
  - ✓ `FileNodeType NodeType` property (File, Folder)
  - ✓ `string? MimeType` property (null for folders)
  - ✓ `long Size` property (bytes, 0 for folders)
  - ✓ `Guid? ParentId` FK (null for root-level nodes)
  - ✓ `FileNode? Parent` navigation property
  - ✓ `ICollection<FileNode> Children` navigation property
  - ✓ `Guid OwnerId` FK
  - ✓ `string MaterializedPath` property (efficient tree queries)
  - ✓ `int Depth` property (tree depth)
  - ✓ `string? ContentHash` property (SHA-256, null for folders)
  - ✓ `int CurrentVersion` property
  - ✓ `string? StoragePath` property (content-addressable)
  - ✓ `bool IsDeleted` soft-delete flag
  - ✓ `DateTime? DeletedAt` property
  - ✓ `Guid? DeletedByUserId` property
  - ✓ `Guid? OriginalParentId` property (restore target)
  - ✓ `bool IsFavorite` property
  - ✓ `DateTime CreatedAt` property
  - ✓ `DateTime UpdatedAt` property
- ✓ Create `FileNodeType` enum (File, Folder)

#### FileVersion Model

- ✓ Create `FileVersion` entity:
  - ✓ `Guid Id` primary key
  - ✓ `Guid FileNodeId` FK
  - ✓ `int VersionNumber` property
  - ✓ `long Size` property
  - ✓ `string ContentHash` property (SHA-256)
  - ✓ `string StoragePath` property (content-addressable)
  - ✓ `string? MimeType` property
  - ✓ `Guid CreatedByUserId` FK
  - ✓ `DateTime CreatedAt` property
  - ✓ `string? Label` property (optional version label)

#### FileChunk Model

- ✓ Create `FileChunk` entity:
  - ✓ `Guid Id` primary key
  - ✓ `string ChunkHash` property (SHA-256, deduplication key)
  - ✓ `int Size` property (max 4MB)
  - ✓ `string StoragePath` property
  - ✓ `int ReferenceCount` property (for garbage collection)
  - ✓ `DateTime CreatedAt` property
  - ✓ `DateTime LastReferencedAt` property

#### FileVersionChunk Model

- ✓ Create `FileVersionChunk` entity:
  - ✓ Composite primary key (`FileVersionId`, `FileChunkId`, `SequenceIndex`)
  - ✓ FK to `FileVersion`, FK to `FileChunk`
- ✓ Create `FileVersionChunkId` composite key struct for EF Core

#### FileShare Model

- ✓ Create `FileShare` entity:
  - ✓ `Guid Id` primary key
  - ✓ `Guid FileNodeId` FK
  - ✓ `ShareType ShareType` property (User, Team, Group, PublicLink)
  - ✓ `Guid? SharedWithUserId` FK
  - ✓ `Guid? SharedWithTeamId` FK
  - ✓ `Guid? SharedWithGroupId` FK
  - ✓ `SharePermission Permission` property (Read, ReadWrite, Full)
  - ✓ `string? LinkToken` property (public link URL token)
  - ✓ `string? LinkPasswordHash` property
  - ✓ `int? MaxDownloads` property
  - ✓ `int DownloadCount` property
  - ✓ `DateTime? ExpiresAt` property
  - ✓ `Guid CreatedByUserId` FK
  - ✓ `DateTime CreatedAt` property
  - ✓ `string? Note` property
- ✓ Create `ShareType` enum (User, Team, Group, PublicLink)
- ✓ Create `SharePermission` enum (Read, ReadWrite, Full)

**Device & Module Registry Models**

- ✓ Create `UserDevice` entity:
  - ✓ `Guid UserId` FK
  - ✓ `string Name` property (e.g., "Windows Laptop")
  - ✓ `string DeviceType` property (Desktop, Mobile, etc.)
  - ✓ `string? PushToken` property
  - ✓ `DateTime LastSeenAt` property
- ✓ Create `InstalledModule` entity:
  - ✓ `string ModuleId` property (primary key, e.g., "dotnetcloud.files")
  - ✓ `Version Version` property
  - ✓ `string Status` property (Enabled, Disabled, UpdateAvailable)
  - ✓ `DateTime InstalledAt` property
- ✓ Create `ModuleCapabilityGrant` entity:
  - ✓ `string ModuleId` FK
  - ✓ `string CapabilityName` property
  - ✓ `DateTime GrantedAt` property
  - ✓ `Guid? GrantedByUserId` (admin who approved)

#### EF Core Configuration

- ✓ Create `CoreDbContext` class extending `IdentityDbContext<ApplicationUser, ApplicationRole, Guid>`
- ✓ Configure all entity relationships
- ✓ Set up automatic timestamps (CreatedAt, UpdatedAt)
- ✓ Configure soft-delete query filters
- ✓ Set up table naming strategy application
- ✓ Create design-time factory for migrations

#### Database Initialization

- ✓ Create `DbInitializer` class:
  - ✓ Database creation
  - ✓ Seed default system roles
  - ✓ Seed default permissions
  - ✓ Seed system settings
- ✓ Create migration files for each supported database:
  - ✓ PostgreSQL migrations
  - ✓ SQL Server migrations
  - ☐ MariaDB migrations (temporarily disabled - awaiting Pomelo .NET 10 support)

---

## Phase 1.2: Files Database & Data Access Layer

### DotNetCloud.Modules.Files.Data Project

**Create EF Core database context and configurations**

#### Entity Configurations

- ✓ Create `FileNodeConfiguration` (IEntityTypeConfiguration):
  - ✓ Table name via naming strategy (`files.file_nodes` / `files_file_nodes`)
  - ✓ Index on `ParentId`
  - ✓ Index on `OwnerId`
  - ✓ Index on `MaterializedPath`
  - ✓ Self-referencing FK (Parent ↔ Children)
  - ✓ Soft-delete query filter
- ✓ Create `FileVersionConfiguration`:
  - ✓ FK to `FileNode`
  - ✓ Index on (`FileNodeId`, `VersionNumber`)
- ✓ Create `FileChunkConfiguration`:
  - ✓ Unique index on `ChunkHash` (deduplication key)
- ✓ Create `FileVersionChunkConfiguration`:
  - ✓ Composite primary key (`FileVersionId`, `FileChunkId`, `SequenceIndex`)
  - ✓ FK to `FileVersion`, FK to `FileChunk`
- ✓ Create `FileShareConfiguration`:
  - ✓ FK to `FileNode`
  - ✓ Index on `SharedWithUserId`
  - ✓ Unique index on `LinkToken`
  - ✓ Index on `ExpiresAt`
- ✓ Create `FileTagConfiguration`:
  - ✓ FK to `FileNode`
  - ✓ Unique index on (`FileNodeId`, `Name`, `CreatedByUserId`)
- ✓ Create `FileCommentConfiguration`:
  - ✓ FK to `FileNode`
  - ✓ Self-referencing FK (ParentComment ↔ Replies)
  - ✓ Index on `FileNodeId`
  - ✓ Soft-delete query filter
- ✓ Create `FileQuotaConfiguration`:
  - ✓ Unique index on `UserId`
- ✓ Create `ChunkedUploadSessionConfiguration`:
  - ✓ Index on `UserId`
  - ✓ Index on `Status`
  - ✓ Index on `ExpiresAt`

#### FilesDbContext

- ✓ Create `FilesDbContext` class extending `DbContext`:
  - ✓ `DbSet<FileNode> FileNodes`
  - ✓ `DbSet<FileVersion> FileVersions`
  - ✓ `DbSet<FileChunk> FileChunks`
  - ✓ `DbSet<FileVersionChunk> FileVersionChunks`
  - ✓ `DbSet<FileShare> FileShares`
  - ✓ `DbSet<FileTag> FileTags`
  - ✓ `DbSet<FileComment> FileComments`
  - ✓ `DbSet<FileQuota> FileQuotas`
  - ✓ `DbSet<ChunkedUploadSession> UploadSessions`
- ✓ Apply all entity configurations in `OnModelCreating`

#### Migrations

- ✓ Create PostgreSQL initial migration
- ✓ Create SQL Server initial migration
- ☐ Create MariaDB initial migration (when Pomelo supports .NET 10)

#### Database Initialization

- ✓ Create `FilesDbInitializer`:
  - ✓ Create default root folder per user
  - ✓ Seed default quota settings from system configuration
  - ✓ Create default tags (e.g., "Important", "Work", "Personal")

---

## Phase 1.3: Files Business Logic & Services

### DotNetCloud.Modules.Files Project (Services)

**Core file management business logic**

#### File Service

- ✓ Create `IFileService` interface:
  - ✓ `Task<FileNodeDto> GetNodeAsync(Guid nodeId, CallerContext caller)`
  - ✓ `Task<IReadOnlyList<FileNodeDto>> ListChildrenAsync(Guid folderId, CallerContext caller)`
  - ✓ `Task<FileNodeDto> CreateFolderAsync(CreateFolderDto dto, CallerContext caller)`
  - ✓ `Task<FileNodeDto> RenameAsync(Guid nodeId, RenameNodeDto dto, CallerContext caller)`
  - ✓ `Task<FileNodeDto> MoveAsync(Guid nodeId, MoveNodeDto dto, CallerContext caller)`
  - ✓ `Task<FileNodeDto> CopyAsync(Guid nodeId, Guid targetParentId, CallerContext caller)`
  - ✓ `Task DeleteAsync(Guid nodeId, CallerContext caller)` (soft-delete to trash)
  - ✓ `Task<FileNodeDto> ToggleFavoriteAsync(Guid nodeId, CallerContext caller)`
  - ✓ `Task<IReadOnlyList<FileNodeDto>> ListFavoritesAsync(CallerContext caller)`
  - ✓ `Task<PagedResult<FileNodeDto>> SearchAsync(string query, int page, int pageSize, CallerContext caller)`
  - ✓ `Task<IReadOnlyList<FileNodeDto>> ListRootAsync(CallerContext caller)`
- ✓ Implement `FileService`
- ✓ Add authorization checks (ownership, share permissions)
- ✓ Validate name uniqueness within parent folder
- ✓ Update materialized paths on move operations
- ✓ Enforce depth limits for folder nesting

#### Chunked Upload Service

- ✓ Create `IChunkedUploadService` interface:
  - ✓ `Task<UploadSessionDto> InitiateUploadAsync(InitiateUploadDto dto, CallerContext caller)`
  - ✓ `Task UploadChunkAsync(Guid sessionId, string chunkHash, ReadOnlyMemory<byte> data, CallerContext caller)`
  - ✓ `Task<FileNodeDto> CompleteUploadAsync(Guid sessionId, CallerContext caller)`
  - ✓ `Task CancelUploadAsync(Guid sessionId, CallerContext caller)`
  - ✓ `Task<UploadSessionDto> GetSessionAsync(Guid sessionId, CallerContext caller)`
- ✓ Implement `ChunkedUploadService`:
  - ✓ Check server-side chunk store for existing hashes (deduplication)
  - ✓ Write missing chunks to storage via `IFileStorageEngine`
  - ✓ Create `FileVersion` and `FileVersionChunk` records on completion
  - ✓ Update `FileNode` (size, hash, version) on completion
  - ✓ Enforce quota checks before accepting uploads
  - ✓ Reject exact duplicate sibling/root filenames on upload completion

#### Download Service

- ✓ Create `IDownloadService` interface:
  - ✓ `Task<Stream> DownloadCurrentAsync(Guid fileNodeId, CallerContext caller)`
  - ✓ `Task<Stream> DownloadVersionAsync(Guid fileVersionId, CallerContext caller)`
- ✓ Implement `DownloadService`:
  - ✓ Reconstruct file from chunks in sequence order via `ConcatenatedStream`
  - ☐ Support range requests for partial downloads (deferred)
  - ✓ Validate access permissions (owner/shared) in service layer, including chunk-hash access gating

#### Version Service

- ✓ Create `IVersionService` interface:
  - ✓ `Task<IReadOnlyList<FileVersionDto>> ListVersionsAsync(Guid fileNodeId, CallerContext caller)`
  - ✓ `Task<FileVersionDto?> GetVersionAsync(Guid versionId, CallerContext caller)`
  - ✓ `Task<FileVersionDto> RestoreVersionAsync(Guid fileNodeId, Guid versionId, CallerContext caller)`
  - ✓ `Task DeleteVersionAsync(Guid versionId, CallerContext caller)`
  - ✓ `Task<FileVersionDto> LabelVersionAsync(Guid versionId, string label, CallerContext caller)`
- ✓ Implement `VersionService`:
  - ✓ Restore creates a new version with the old content
  - ✓ Update chunk reference counts on version deletion
  - ☐ Enforce configurable version retention limits (deferred)

#### Share Service

- ✓ Create `IShareService` interface:
  - ✓ `Task<FileShareDto> CreateShareAsync(Guid fileNodeId, CreateShareDto dto, CallerContext caller)`
  - ✓ `Task<IReadOnlyList<FileShareDto>> GetSharesAsync(Guid fileNodeId, CallerContext caller)`
  - ✓ `Task DeleteShareAsync(Guid shareId, CallerContext caller)`
  - ✓ `Task<FileShareDto> UpdateShareAsync(Guid shareId, UpdateShareDto dto, CallerContext caller)`
  - ✓ `Task<FileShareDto?> ResolvePublicLinkAsync(string linkToken, string? password)`
  - ✓ `Task<IReadOnlyList<FileShareDto>> GetSharedWithMeAsync(CallerContext caller)`
  - ✓ `Task IncrementDownloadCountAsync(Guid shareId)`
- ✓ Implement `ShareService`:
  - ✓ Generate cryptographically random link tokens
  - ✓ Hash link passwords with ASP.NET Identity PasswordHasher
  - ✓ Check download limits and expiration on public links
  - ✓ Publish `FileSharedEvent` on share creation
  - ✓ Send notifications to share recipients (FileSharedNotificationHandler + NotificationEventSubscriber)

#### Trash Service

- ✓ Create `ITrashService` interface:
  - ✓ `Task<IReadOnlyList<TrashItemDto>> ListTrashAsync(CallerContext caller)`
  - ✓ `Task<FileNodeDto> RestoreAsync(Guid nodeId, CallerContext caller)`
  - ✓ `Task PermanentDeleteAsync(Guid nodeId, CallerContext caller)`
  - ✓ `Task EmptyTrashAsync(CallerContext caller)`
  - ✓ `Task RestoreAllAsync(CallerContext caller)`
- ✓ Implement `TrashService`:
  - ✓ Restore to original parent folder (or root if parent was deleted)
  - ✓ Cascade permanent delete to versions, chunks, shares, tags, comments
  - ✓ Decrement chunk reference counts; garbage-collect unreferenced chunks
  - ✓ Publish `FileRestoredEvent` on restore and `FileDeletedEvent` on permanent delete
  - ✓ Auto-cleanup expired trash items (30-day retention via TrashCleanupService)

#### Quota Service

- ✓ Create `IQuotaService` interface:
  - ✓ `Task<QuotaDto> GetQuotaAsync(Guid userId, CallerContext caller)`
  - ✓ `Task<QuotaDto> SetQuotaAsync(Guid userId, long maxBytes, CallerContext caller)`
  - ✓ `Task RecalculateAsync(Guid userId, CancellationToken cancellationToken)`
  - ✓ `Task<bool> HasSufficientQuotaAsync(Guid userId, long requiredBytes, CancellationToken cancellationToken)`
- ✓ Implement `QuotaService`:
  - ✓ Calculate used bytes from all non-deleted `FileNode` entries
  - ✓ Enforce quota before uploads (pre-check in chunked upload service)
  - ✓ Send warning notifications at 80% and 95% usage (QuotaNotificationHandler — QuotaWarningEvent + QuotaCriticalEvent)

#### Tag Service

- ✓ Create `ITagService` interface:
  - ✓ `Task<FileTagDto> AddTagAsync(Guid fileNodeId, string name, string? color, CallerContext caller)`
  - ✓ `Task RemoveTagAsync(Guid fileNodeId, Guid tagId, CallerContext caller)`
  - ✓ `Task<IReadOnlyList<FileTagDto>> GetTagsAsync(Guid fileNodeId, CallerContext caller)`
  - ✓ `Task<IReadOnlyList<FileNodeDto>> GetNodesByTagAsync(string tagName, CallerContext caller)`
- ✓ Implement `TagService`

#### Comment Service

- ✓ Create `ICommentService` interface:
  - ✓ `Task<FileCommentDto> AddCommentAsync(Guid fileNodeId, string content, Guid? parentCommentId, CallerContext caller)`
  - ✓ `Task<FileCommentDto> EditCommentAsync(Guid commentId, string content, CallerContext caller)`
  - ✓ `Task DeleteCommentAsync(Guid commentId, CallerContext caller)`
  - ✓ `Task<IReadOnlyList<FileCommentDto>> GetCommentsAsync(Guid fileNodeId, CallerContext caller)`
  - ✓ `Task<FileCommentDto?> GetCommentAsync(Guid commentId, CallerContext caller)`
- ✓ Implement `CommentService`

#### Background Services

- ✓ Create `UploadSessionCleanupService` (IHostedService):
  - ✓ Periodically expire stale upload sessions
  - ✓ Delete orphaned chunks from expired sessions
- ✓ Create `TrashCleanupService` (IHostedService):
  - ✓ Permanently delete items older than configured retention period
  - ✓ Garbage-collect unreferenced chunks (reference count = 0)
- ✓ Create `QuotaRecalculationService` (IHostedService):
  - ✓ Periodically recalculate storage usage per user

---

## Phase 1.4: Files REST API Endpoints

### DotNetCloud.Modules.Files.Host Project (Controllers)

**REST API for file operations**

#### File & Folder Endpoints (FilesController)

- ✓ Expose `/api/v1/files/*` endpoints from core server for bare-metal single-process installs (no separate Files host routing required)
- ✓ `GET /api/v1/files` — List files/folders in directory (paginated, sorted)
- ✓ `GET /api/v1/files/{nodeId}` — Get file/folder by ID
- ✓ `POST /api/v1/files/folders` — Create folder
- ✓ `PUT /api/v1/files/{nodeId}/rename` — Rename file/folder
- ✓ `PUT /api/v1/files/{nodeId}/move` — Move file/folder
- ✓ `POST /api/v1/files/{nodeId}/copy` — Copy file/folder
- ✓ `DELETE /api/v1/files/{nodeId}` — Delete file/folder (soft-delete to trash)
- ✓ `POST /api/v1/files/{nodeId}/favorite` — Toggle favorite
- ✓ `GET /api/v1/files/favorites` — List favorites
- ✓ `GET /api/v1/files/recent` — List recently modified files
- ✓ `GET /api/v1/files/search` — Search files by name/content

#### Upload Endpoints (FilesController)

- ✓ `POST /api/v1/files/upload/initiate` — Initiate chunked upload session
- ✓ `PUT /api/v1/files/upload/{sessionId}/chunks/{chunkHash}` — Upload a chunk
- ✓ `POST /api/v1/files/upload/{sessionId}/complete` — Complete upload session
- ✓ `DELETE /api/v1/files/upload/{sessionId}` — Cancel upload session
- ✓ `GET /api/v1/files/upload/{sessionId}` — Get upload session status

#### Download Endpoints (FilesController)

- ✓ `GET /api/v1/files/{nodeId}/download` — Download file content
- ✓ `GET /api/v1/files/{nodeId}/download?version={n}` — Download specific version
- ✓ `GET /api/v1/files/{nodeId}/chunks` — Get chunk manifest (for sync clients)
- ✓ Harden download MIME fallback (`FilesController.DownloadAsync`) to treat null/empty/whitespace MIME values as `application/octet-stream` and prevent HTTP 500 `FormatException`

#### Version Endpoints (VersionController)

- ✓ `GET /api/v1/files/{nodeId}/versions` — List file versions
- ✓ `GET /api/v1/files/{nodeId}/versions/{versionNumber}` — Get specific version
- ✓ `POST /api/v1/files/{nodeId}/versions/{versionNumber}/restore` — Restore version
- ✓ `DELETE /api/v1/files/{nodeId}/versions/{versionNumber}` — Delete version
- ✓ `PUT /api/v1/files/{nodeId}/versions/{versionNumber}/label` — Label a version

#### Share Endpoints (ShareController)

- ✓ `POST /api/v1/files/{nodeId}/shares` — Create share
- ✓ `GET /api/v1/files/{nodeId}/shares` — List shares for node
- ✓ `DELETE /api/v1/files/{nodeId}/shares/{shareId}` — Remove share
- ✓ `PUT /api/v1/files/{nodeId}/shares/{shareId}` — Update share
- ✓ `GET /api/v1/files/shared-with-me` — List files shared with current user
- ✓ `GET /api/v1/files/public/{linkToken}` — Access public shared file/folder

#### Trash Endpoints (TrashController)

- ✓ `GET /api/v1/files/trash` — List trash items (paginated)
- ✓ `POST /api/v1/files/trash/{nodeId}/restore` — Restore from trash
- ✓ `DELETE /api/v1/files/trash/{nodeId}` — Permanently delete
- ✓ `DELETE /api/v1/files/trash` — Empty trash
- ✓ `GET /api/v1/files/trash/size` — Get total trash size

#### Quota Endpoints (QuotaController)

- ✓ `GET /api/v1/files/quota` — Get current user's quota
- ✓ `GET /api/v1/files/quota/{userId}` — Get specific user's quota (admin)
- ✓ `PUT /api/v1/files/quota/{userId}` — Set user quota (admin)
- ✓ `POST /api/v1/files/quota/{userId}/recalculate` — Force recalculation (admin)

#### Tag Endpoints (TagController)

- ✓ `POST /api/v1/files/{nodeId}/tags` — Add tag to node
- ✓ `DELETE /api/v1/files/{nodeId}/tags/{tagName}` — Remove tag from node
- ✓ `GET /api/v1/files/tags` — List all user's tags
- ✓ `GET /api/v1/files/tags/{tagName}` — List files with specific tag

#### Comment Endpoints (CommentController)

- ✓ `POST /api/v1/files/{nodeId}/comments` — Add comment
- ✓ `GET /api/v1/files/{nodeId}/comments` — List comments
- ✓ `PUT /api/v1/files/comments/{commentId}` — Edit comment
- ✓ `DELETE /api/v1/files/comments/{commentId}` — Delete comment

#### Bulk Operation Endpoints (BulkController)

- ✓ `POST /api/v1/files/bulk/move` — Move multiple items
- ✓ `POST /api/v1/files/bulk/copy` — Copy multiple items
- ✓ `POST /api/v1/files/bulk/delete` — Delete multiple items (to trash)
- ✓ `POST /api/v1/files/bulk/permanent-delete` — Permanently delete multiple items

#### Sync Endpoints (SyncController)

- ✓ `POST /api/v1/files/sync/reconcile` — Reconcile local state with server
- ✓ `GET /api/v1/files/sync/changes?since={timestamp}` — Get changes since timestamp
- ✓ `GET /api/v1/files/sync/tree?folderId={id}` — Get full folder tree with hashes

---

## Phase 1.5: Chunked Upload & Download Infrastructure

### Chunked Transfer System

**Content-hash deduplication and resumable transfers**

#### Chunked Upload Pipeline

- ✓ Implement file splitting into 4MB chunks (client-side and server-side) — `ContentHasher.ChunkAndHashAsync`, `DefaultChunkSize = 4MB`
- ✓ Implement SHA-256 hashing per chunk — `ContentHasher.ComputeHash`
- ✓ Implement chunk manifest generation (ordered list of hashes) — `ContentHasher.ComputeManifestHash`
- ✓ Server-side deduplication lookup (skip upload for existing chunks) — `ChunkedUploadService.InitiateUploadAsync`
- ✓ Track upload progress per session in `ChunkedUploadSession` — `ReceivedChunks`/`TotalChunks` fields
- ✓ Resume interrupted uploads (only re-upload missing chunks) — `GetSessionAsync` returns `MissingChunks`
- ✓ Validate chunk integrity on receipt (hash verification) — `UploadChunkAsync` verifies SHA-256 before storing
- ✓ Assemble file from chunks on completion (link `FileVersionChunk` records) — `CompleteUploadAsync`

#### Chunked Download Pipeline

- ✓ Serve files as chunked streams for large files — `DownloadService` + seekable `ConcatenatedStream`
- ✓ Support HTTP range requests for partial downloads — `ConcatenatedStream` is seekable; `FilesController.DownloadAsync` uses `enableRangeProcessing: true`
- ✓ Serve individual chunks by hash (for sync clients) — `DownloadChunkByHashAsync` + `GET /api/v1/files/chunks/{chunkHash}`
- ✓ Serve chunk manifests for sync reconciliation — `GetChunkManifestAsync` + `GET /api/v1/files/{nodeId}/chunks`

#### Content-Hash Deduplication

- ✓ Implement cross-user deduplication (identical chunks stored once) — shared `FileChunks` table keyed by hash
- ✓ Track chunk reference counts across file versions — `FileChunk.ReferenceCount` incremented/decremented
- ✓ Garbage-collect unreferenced chunks (reference count = 0) — `TrashCleanupService` + `UploadSessionCleanupService` GC pass
- ✓ Monitor deduplication savings in storage metrics — `IStorageMetricsService` + `GET /api/v1/files/storage/metrics`

#### Upload Session Management

- ✓ Implement session creation with quota pre-check — `InitiateUploadAsync` calls `IQuotaService.HasSufficientQuotaAsync`
- ✓ Track session progress (received vs. total chunks) — `ReceivedChunks`/`TotalChunks` updated on each `UploadChunkAsync`
- ✓ Expire stale sessions (configurable TTL, default 24h) — `UploadSessionCleanupService` 1h interval
- ✓ Clean up orphaned chunks from failed sessions — `UploadSessionCleanupService` GC pass deletes chunks with `ReferenceCount = 0`
- ✓ Support concurrent chunk uploads within a session — chunk uniqueness enforced via DB; no session-level locking needed

---

## Phase 1.6: File Sharing & Permissions

### Sharing System

**User, team, group, and public link sharing**

#### Share Types

- ✓ Implement User shares (share with specific user by ID)
- ✓ Implement Team shares (share with all members of a team)
- ✓ Implement Group shares (share with a cross-team group)
- ✓ Implement PublicLink shares (generate shareable URL)

#### Public Link Features

- ✓ Generate cryptographically random link tokens
- ✓ Optional password protection (hashed storage)
- ✓ Download count tracking
- ✓ Maximum download limits
- ✓ Expiration dates
- ✓ Public link access without authentication (`PublicShareController`)

#### Permission Enforcement

- ✓ Enforce Read permission (view and download only)
- ✓ Enforce ReadWrite permission (upload, rename, move within shared folder)
- ✓ Enforce Full permission (all operations including re-share and delete)
- ✓ Cascade folder share permissions to children
- ✓ Validate permissions on every file operation (`IPermissionService`)

#### Share Notifications

- ✓ Notify users when files/folders are shared with them (via `FileSharedEvent`)
- ✓ Notify share creator on first access of public link
- ✓ Send notification when share is about to expire

---

## Phase 1.7: File Versioning System

### Version Management

**File version history, restore, and retention**

#### Version Creation

- ✓ Create new version on every file content update
- ✓ Link version to its constituent chunks via `FileVersionChunk`
- ✓ Track version creator and timestamp
- ✓ Support optional version labels (e.g., "Final draft")

#### Version Retrieval

- ✓ List all versions of a file (newest first)
- ✓ Download specific version content
- ✓ Compare version metadata (size, date, author)

#### Version Restore

- ✓ Restore creates a new version with old version's content
- ✓ Reuse existing chunks (no duplicate storage)
- ✓ Publish `FileVersionRestoredEvent` on restore

#### Version Retention

- ✓ Configurable maximum version count per file
- ✓ Configurable retention period (e.g., keep versions for 30 days)
- ✓ Auto-cleanup oldest versions when limits exceeded
- ✓ Never auto-delete labeled versions
- ✓ Decrement chunk reference counts on version deletion

---

## Phase 1.8: Trash & Recovery

### Trash Bin System

**Soft-delete, restore, and permanent cleanup**

#### Soft-Delete

- ✓ Move items to trash (set `IsDeleted`, `DeletedAt`, `DeletedByUserId`)
- ✓ Preserve original parent ID for restore (`OriginalParentId`)
- ✓ Cascade soft-delete to children (folders)
- ✓ Remove shares when item is trashed
- ✓ Publish `FileDeletedEvent` on trash

#### Restore

- ✓ Restore to original parent folder
- ✓ Handle case where original parent was also deleted (restore to root)
- ✓ Restore child items when parent folder is restored
- ✓ Re-validate name uniqueness in target folder on restore (auto-rename)

#### Permanent Delete

- ✓ Delete file versions and their chunk mappings
- ✓ Decrement chunk reference counts
- ✓ Garbage-collect chunks with zero references
- ✓ Delete tags, comments, and shares
- ✓ Update user quota (reduce used bytes)

#### Auto-Cleanup

- ✓ Configurable trash retention period (default: 30 days) via `TrashRetentionOptions`
- ✓ Background service permanently deletes expired trash items
- ✓ Admin can configure retention per organization (TrashRetentionOptions.OrganizationOverrides + per-org TrashCleanupService logic)

---

## Phase 1.9: Storage Quotas & Limits

### Quota Management

**Per-user and per-organization storage limits**

#### Quota Enforcement

- ✓ Check quota before accepting file uploads
- ✓ Check quota before file copy operations
- ✓ Return clear error response when quota exceeded (`FILES_QUOTA_EXCEEDED`)
- ✓ Exclude trashed items from quota calculation (configurable)

#### Quota Administration

- ✓ Admin can set per-user quota limits
- ✓ Admin can set default quota for new users
- ✓ Admin can view quota usage across all users
- ✓ Admin can force quota recalculation

#### Quota Notifications

- ✓ Warning notification at 80% usage
- ✓ Critical notification at 95% usage
- ✓ Notification when quota is exceeded (prevent further uploads)

#### Quota Display

- ✓ Show quota usage in file browser UI (progress bar)
- ✓ Show quota in admin user management

---

## Phase 1.10: WOPI Host & Collabora Integration

### WOPI Protocol Implementation

**Browser-based document editing via Collabora CODE/Online**

#### WOPI Endpoints

- ✓ `GET /api/v1/wopi/files/{fileId}` — CheckFileInfo (file metadata)
- ✓ `GET /api/v1/wopi/files/{fileId}/contents` — GetFile (download content)
- ✓ `POST /api/v1/wopi/files/{fileId}/contents` — PutFile (save edited content)
- ✓ Expose `/api/v1/wopi/*` endpoints from core server for bare-metal single-process installs (no separate module host routing required)
- ✓ Implement WOPI access token generation (per-user, per-file, time-limited)
- ✓ Implement WOPI access token validation
- ✓ Implement WOPI proof key validation (Collabora signature verification)

#### WOPI Integration

- ✓ Read file content from `IFileStorageEngine` in GetFile
- ✓ Write saved content via chunked upload pipeline in PutFile
- ✓ Create new file version on each PutFile save
- ✓ Enforce permission checks via `CallerContext`
- ✓ Support concurrent editing (Collabora handles OT internally)

#### Collabora CODE Management

- ✓ Implement Collabora CODE download and auto-installation in `dotnetcloud setup` + `dotnetcloud install collabora`
- ✓ Ensure `tools/install.sh` auto-installs Collabora CODE when setup selection persists `collaboraMode: BuiltIn`
- ✓ Harden `tools/install.sh` built-in Collabora post-install to auto-manage `coolwsd.xml` WOPI alias groups for the configured DotNetCloud origin (preferring `Files__Collabora__ServerUrl` from `dotnetcloud.env`), enforce safe file ownership/mode (`root:cool`, `640`), and restart/validate `coolwsd`
- ✓ Create Collabora CODE process management under process supervisor (`CollaboraProcessManager` BackgroundService)
- ✓ Implement WOPI discovery endpoint integration
- ✓ Configure TLS/URL routing for Collabora (`ReverseProxyTemplates.GenerateNginxConfigWithCollabora`, `GenerateApacheConfigWithCollabora`)
- ✓ Add in-app YARP Collabora path proxying (`/hosting`, `/browser`, `/cool`, `/lool`) in `DotNetCloud.Core.Server` for single-origin deployments on one public HTTPS port, with optional `Files:Collabora:ProxyUpstreamUrl` to avoid self-proxy loops
- ✓ Add startup diagnostics for Collabora proxy misconfiguration (warn when `ServerUrl` is invalid while enabled, and when `ServerUrl` + `WopiBaseUrl` share origin but `ProxyUpstreamUrl` is unset)
- ✓ Create Collabora health check

#### Collabora Configuration

- ✓ Admin UI for Collabora server URL (built-in CODE vs. external) — `/admin/collabora` Blazor page
- ✓ Auto-save interval configuration (`CollaboraOptions.AutoSaveIntervalSeconds`)
- ✓ Maximum concurrent document sessions configuration (`IWopiSessionTracker`)
- ✓ Supported file format configuration (`CollaboraOptions.SupportedMimeTypes` filtering)

#### Blazor Integration

- ✓ Create document editor component (iframe embedding Collabora UI)
- ✓ Open supported documents in editor from file browser
- ✓ Ensure file/folder opening actions are single-click only (no double-click dependency)
- ✓ Open documents in editor only when Collabora discovery is available and extension is supported
- ✓ Create new Collabora-supported files from file browser (new document workflow)
- ✓ Keep New Document action visible when Collabora is configured but discovery is temporarily unavailable (fallback extension set)
- ✓ Normalize DocumentEditor API calls to root `/api/v1/wopi/*` when module route base paths are present (prevents false 404s)
- ✓ Resolve WOPI token `userId` reliably by falling back to authenticated claims in `DocumentEditor` and return clean 401 (not 500) when identity is unavailable
- ✓ Encode WOPI tokens with URL-safe Base64 and keep legacy decode compatibility to prevent `CheckFileInfo` token parse failures from query-string transport
- ✓ Stabilize fallback WOPI signing key across requests within a process (when `TokenSigningKey` is unset) to prevent token signature mismatches between generate/validate calls
- ✓ Accept Collabora WOPI proof timestamps in multiple encodings (FILETIME, DateTime ticks, Unix ms/sec) to prevent false replay-age rejection during `CheckFileInfo`
- ✓ Add WOPI proof-key verification fallback to discovery `modulus`/`exponent` when SPKI `value` key import fails (ASN.1 mismatch), preserving signature validation
- ✓ Normalize Collabora discovery `urlsrc` host/scheme to configured `Files:Collabora:ServerUrl` so iframe URLs are browser-reachable
- ✓ Fix Razor parameter binding for editor launch (`@EditorNode.Name`, `@ApiBaseUrl`) to avoid literal text rendering and ensure correct runtime values
- ✓ Allow configured Collabora origin in CSP (`frame-src`/`child-src`) so the document editor iframe can load in `/apps/files`
- ✓ Fix Blazor SSR login cookie-write failure by switching `/auth/login` to HTTP form-post flow via `/auth/session/login` endpoint (avoids SignInManager cookie issuance on `/_blazor` circuit responses)
- ✓ Fix server-side Blazor same-origin TLS for non-loopback self-signed hostnames (for example `https://mint22:15443`) by honoring `Files:Collabora:AllowInsecureTls` in scoped UI `HttpClient` setup
- ✓ Normalize proxied Collabora response frame headers for browser embedding: remove `X-Frame-Options` and rewrite CSP `frame-ancestors` to `'self'` on proxied responses
- ✓ Preserve public origin headers when proxying Collabora (`Host`, `X-Forwarded-Host`, `X-Forwarded-Proto`, `X-Forwarded-Port`) and emit a single effective CSP on proxied responses so `cool.html` uses `wss://mint22:15443` instead of `wss://localhost:9980`
- ✓ Show "download to edit locally" for E2EE files
- ✓ Display co-editing indicators (who is editing)

---

## Phase 1.11: File Browser Web UI (Blazor)

### DotNetCloud.Modules.Files UI Components

**Blazor file management interface**

#### File Browser Component

- ✓ Create `FileBrowser.razor` main component:
  - ✓ Grid view (icon + name + size + date)
  - ✓ List view (tabular with columns)
  - ✓ View mode toggle (grid/list)
  - ✓ Breadcrumb navigation
  - ✓ Folder navigation (click to enter, back button)
  - ✓ Multi-select (checkbox per item)
  - ✓ Pagination (page controls, configurable page size)
  - ✓ Sort by name, size, date, type (column header click)
  - ✓ Right-click context menu (rename, move, copy, share, delete, download) — context-menu.js + FileContextMenu.razor
  - ✓ Drag-and-drop file reordering / move to folder — file-drag-move.js + OnDragMoveNode JSInvokable
  - ✓ Empty state placeholder ("No files yet — upload or create a folder")
  - ✓ Loading skeleton while fetching data
  - ✓ Root and folder listings deduplicate tagged nodes from data-service queries

#### File Upload Component

- ✓ Create `FileUploadComponent.razor`:
  - ✓ File selection button
  - ✓ Drag-and-drop upload area
  - ✓ Upload progress bar per file
  - ✓ Multiple file upload support
  - ✓ Upload queue management (pause, resume, cancel) — AbortController per-file, chunk-level control
  - ✓ Paste image upload (clipboard integration) — file-paste.js with timestamp filenames
  - ✓ Size validation before upload — client-side check via /api/v1/files/config endpoint

#### File Preview Component

- ✓ Create `FilePreview.razor`:
  - ✓ Image preview (inline `<img>` for JPEG, PNG, GIF, WebP, SVG)
  - ✓ Video preview (HTML5 `<video>` player with controls)
  - ✓ Audio preview (HTML5 `<audio>` player with controls)
  - ✓ PDF preview (embedded `<iframe>` viewer)
  - ✓ Text/code preview (`<iframe>` embed with language label)
  - ✓ Markdown preview (`<iframe>` embed)
  - ✓ Unsupported format fallback (Download File button)
  - ✓ Navigation between files in same folder (prev/next arrows, ← → keyboard shortcuts)

#### Share Dialog Component

- ✓ Create `ShareDialog.razor`:
  - ✓ User search for sharing
  - ✓ Permission selection (Read, ReadWrite, Full)
  - ✓ Public link generation
  - ✓ Password protection toggle for public links
  - ✓ Expiration date picker
  - ✓ Max downloads input
  - ✓ Copy link button
  - ☐ Existing shares list with remove action — deferred: requires GET /api/v1/files/{id}/shares API client wiring

#### Trash Bin Component

- ✓ Create `TrashBin.razor`:
  - ✓ List trashed items with deleted date
  - ✓ Restore button per item
  - ✓ Permanent delete button per item
  - ✓ Empty trash button
  - ✓ Trash size display
  - ✓ Sort by name, date deleted, size
  - ✓ Bulk restore / bulk delete

#### Sidebar & Navigation

- ✓ Create file browser sidebar (`FileSidebar.razor`):
  - ✓ "All Files" navigation item
  - ✓ "Favorites" navigation item
  - ✓ "Recent" navigation item
  - ✓ "Shared with me" navigation item
  - ✓ "Shared by me" navigation item
  - ✓ "Tags" navigation item (expandable tag list)
  - ✓ "Trash" navigation item with item count badge
  - ✓ Storage quota display (progress bar + text)

#### Version History Panel

- ✓ Create version history side panel (`VersionHistoryPanel.razor`):
  - ✓ List versions with date, author, and size
  - ✓ Download specific version
  - ✓ Restore to specific version
  - ✓ Add/edit version labels
  - ✓ Delete old versions

#### Comments Panel

- ✓ Create comments side panel (`CommentsPanel.razor`):
  - ✓ List threaded comments with author and timestamp
  - ✓ Add new top-level comment
  - ✓ Reply to existing comment (nested thread)
  - ✓ Edit own comments
  - ✓ Delete own comments (soft-delete)
  - ✓ Expand/collapse reply threads
  - ✓ Relative time display (e.g., "2h ago")
  - ✓ Context menu "Comments" option
  - ✓ "Comments" button in file preview header
  - ✓ Ctrl+Enter keyboard shortcut to submit

#### Settings & Admin UI

- ✓ Create Files module settings page (`FilesAdminSettings.razor`):
  - ✓ Default quota for new users
  - ✓ Trash retention period
  - ✓ Version retention settings
  - ✓ Maximum upload size
  - ✓ Allowed/blocked file types
  - ✓ Storage path configuration

---

## Phase 1.12: File Upload & Preview UI

### Upload & Preview Enhancement

**Advanced upload and preview capabilities**

#### Drag-and-Drop Upload

- ✓ Implement drag-and-drop zone on file browser (counter-based to avoid flicker)
- ✓ Visual indicator when dragging files over drop zone (`browser-drop-overlay`)
- ✓ Support folder drag-and-drop (recursive upload) via JS DataTransfer directory traversal bridge
- ✓ Show upload progress overlay on file browser (UploadProgressPanel inside upload dialog)

#### Upload Progress Tracking

- ✓ Create upload progress panel (`UploadProgressPanel.razor`):
  - ✓ Per-file progress bar (chunk-level accuracy via simulated chunks)
  - ✓ Overall upload progress (aggregate average across all files)
  - ✓ Upload speed display (bytes/KB/MB per second)
  - ✓ Estimated time remaining (seconds/minutes/hours)
  - ✓ Pause/resume per file (IsPaused flag + polling loop)
  - ✓ Cancel per file (IsCancelled flag; skips on next loop iteration)
  - ✓ Minimize/expand progress panel (collapsible header toggle)

#### Thumbnail Generation

- ✓ Generate thumbnails for image files on upload (`ThumbnailService` using ImageSharp 3.1.12)
- ✓ Generate thumbnails for video files (first frame) via FFmpeg extraction pipeline (`IVideoFrameExtractor` + `FfmpegVideoFrameExtractor`)
- ✓ Generate thumbnails for PDF files (first page) via PDF renderer bridge (`IPdfPageRenderer` + `PdftoppmPdfPageRenderer`)
- ✓ Cache thumbnails on server (disk cache under `{storageRoot}/.thumbnails/{prefix}/{id}_{size}.jpg`)
- ✓ Serve thumbnails via API endpoint (`GET /api/v1/files/{nodeId}/thumbnail?size=small|medium|large`) with authenticated node access checks
- ✓ Display thumbnails in grid view (FileBrowser renders `<img>` when `ThumbnailUrl` is set)

#### Advanced Preview

- ✓ Create full-screen preview mode (`FilePreview.razor` modal overlay)
- ✓ Support keyboard navigation (← → for prev/next file, Escape to close)
- ✓ Support touch gestures (swipe navigation, pinch-zoom for image previews) via JS interop bridge
- ✓ Display file metadata in preview (MIME type, size, modified date, position in folder)
- ✓ Download button from preview (raises OnDownload event callback)
- ✓ Share button from preview (raises OnShare event; FileBrowser opens ShareDialog)

---

## Phase 1.13: File Sharing & Settings UI

### Sharing Interface & Module Settings

**Share management and Files module administration**

#### Share Management UI

- ✓ Create comprehensive share dialog:
  - ✓ Search users by name/email for sharing
  - ✓ Search teams for sharing
  - ✓ Search groups for sharing
  - ✓ Show all existing shares for a node
  - ✓ Inline permission change dropdown
  - ✓ Inline share removal
  - ✓ Public link section with toggle, copy, and settings
- ✓ Create "Shared with me" view:
  - ✓ List all files/folders shared with current user
  - ✓ Group by share source (who shared)
  - ✓ Show permission level
  - ✓ Accept/decline share (optional)
- ✓ Create "Shared by me" view:
  - ✓ List all files/folders shared by current user
  - ✓ Show share recipients and permissions
  - ✓ Manage/revoke shares inline

#### Files Module Admin Settings

- ✓ Create admin settings page for Files module:
  - ✓ Storage backend configuration
  - ✓ Default quota management
  - ✓ Trash auto-cleanup settings
  - ✓ Version retention configuration
  - ✓ Upload limits (max file size, allowed types)
  - ✓ Collabora integration settings

---

## Phase 1.14: Client.Core — Shared Sync Engine

### DotNetCloud.Client.Core Project

**Shared library for all clients (sync engine, API, auth, local state)**

#### Project Setup

- ✓ Create `DotNetCloud.Client.Core` class library project
- ✓ Add to `DotNetCloud.sln`
- ✓ Configure dependencies (HttpClient, SQLite, System.IO, etc.)

#### API Client

- ✓ Create `IDotNetCloudApiClient` interface:
  - ✓ Authentication (login, token refresh, logout)
  - ✓ File operations (list, create, rename, move, copy, delete)
  - ✓ Upload operations (initiate, upload chunk, complete)
  - ✓ Download operations (file, version, chunk)
  - ✓ Sync operations (reconcile, changes since, tree)
  - ✓ Quota operations (get quota)
- ✓ Implement `DotNetCloudApiClient` using `HttpClient`
- ✓ Implement retry with exponential backoff
- ✓ Handle rate limiting (429 responses, respect Retry-After header)
  - ✓ Honor `Retry-After` delta/date with capped wait + jitter to reduce retry stampedes

#### OAuth2 PKCE Authentication

- ✓ Implement OAuth2 Authorization Code with PKCE flow
- ✓ Launch system browser for authentication
- ✓ Handle redirect URI callback (localhost listener)
- ✓ Store tokens securely (AES-GCM encrypted files; Windows DPAPI can be layered on top)
- ✓ Implement automatic token refresh
- ✓ Handle token revocation

#### Sync Engine

- ✓ Create `ISyncEngine` interface:
  - ✓ `Task SyncAsync(SyncContext context, CancellationToken cancellationToken)`
  - ✓ `Task<SyncStatus> GetStatusAsync(SyncContext context)`
  - ✓ `Task PauseAsync(SyncContext context)`
  - ✓ `Task ResumeAsync(SyncContext context)`
- ✓ Implement `SyncEngine`:
  - ✓ `FileSystemWatcher` for instant change detection
  - ✓ Periodic full scan as safety net (configurable interval, default 5 minutes)
  - ✓ Reconcile local state with server state
  - ✓ Detect local changes (new, modified, deleted, moved/renamed)
  - ✓ Detect remote changes (poll server or SignalR push)
  - ✓ Apply changes bidirectionally (upload local → server, download server → local)
  - ✓ Conflict detection and resolution (conflict copy with guided notification)

#### Chunked Transfer Client

- ✓ Implement client-side file chunking (4MB chunks)
- ✓ Implement client-side SHA-256 hashing per chunk
- ✓ Implement client-side chunk manifest generation
- ✓ Upload only missing chunks (deduplication)
- ✓ Download only changed chunks (delta sync)
- ✓ Resume interrupted transfers
- ✓ Configurable concurrent chunk upload/download count

#### Conflict Resolution

- ✓ Detect conflicts (local and remote both modified since last sync)
- ✓ Create conflict copies: `report (conflict - Ben - 2025-07-14).docx`
- ✓ Notify user of conflicts (via SyncTray notification)
- ✓ Preserve both versions (no silent data loss)
- ✓ Three-pane merge editor (local vs server diff + editable merged result)
- ✓ Auto-merge non-conflicting changes with DiffPlex
- ✓ Conflict markers for overlapping changes
- ✓ 24-hour recurring conflict re-notification

#### Local State Database

- ✓ Create SQLite database per sync context:
  - ✓ File metadata table (path, hash, modified time, sync state)
  - ✓ Pending operations queue (uploads, downloads, moves, deletes)
  - ✓ Sync cursor/checkpoint (last sync timestamp or change token)
  - ☐ Account configuration (server URL, user ID, token reference) — handled via SyncContext
- ✓ Implement state database access layer

#### Selective Sync

- ✓ Implement folder selection for sync (include/exclude)
- ✓ Persist selective sync configuration per account
- ✓ Skip excluded folders during sync operations
- ✓ Handle server-side changes in excluded folders gracefully
  - ✓ Accept both `Folder` and `Directory` node types in selective-sync folder browser loading
  - ✓ Open post add-account folder browser against the newly added sync context (no arbitrary context fallback)

---

## Phase 1.15: Client.SyncService — Background Sync Worker

> **Note (2026-03-29):** SyncService has been merged into SyncTray. The sync engine now runs in-process inside the Avalonia tray app. The items below are historical — they were implemented in SyncService and then absorbed into SyncTray.

### DotNetCloud.Client.SyncService Project

**Background sync service (Windows Service / systemd unit)**

#### Project Setup

- ✓ Create `DotNetCloud.Client.SyncService` .NET Worker Service project
- ✓ Add to `DotNetCloud.sln`
- ✓ Configure Windows Service support (`AddWindowsService()`)
- ✓ Configure systemd support (`AddSystemd()`)

#### Multi-User Support

- ✓ Implement sync context management (one per OS-user + account pair)
- ✓ Run as system-level service (single process, multiple contexts)
- ✓ Data isolation: each context has own sync folder, state DB, auth token
- ✓ Linux: drop privileges per context (UID/GID of target OS user) — Unix socket peer credentials are resolved in `IpcServer`, then context-scoped operations execute under Linux privilege transition via `setresuid`/`setresgid` with deterministic `Privilege transition failed.` error semantics
- ✓ Windows: impersonate OS user for file system operations — IPC now captures and duplicates the named-pipe caller token, then executes context-scoped operations via `WindowsIdentity.RunImpersonated`

#### IPC Server

- ✓ Implement IPC server for SyncTray communication:
  - ✓ Named Pipe on Windows
  - ✓ Unix domain socket on Linux
- ✓ IPC protocol:
  - ✓ Identify caller by OS user identity — Windows named-pipe caller identity enforced via `GetImpersonationUserName`; Unix sockets deny identity-bound commands when caller identity is unavailable
  - ✓ Return only caller's sync contexts (no cross-user data)
  - ✓ Commands: list-contexts, add-account, remove-account, get-status, pause, resume, sync-now
  - ✓ Events: sync-progress, sync-complete, conflict-detected, error

#### Sync Orchestration

- ✓ Start sync engine per context on service start
- ✓ Schedule periodic full syncs
- ✓ Handle file system watcher events
- ✓ Rate-limit sync operations (avoid overwhelming server) — `sync-now` now returns a no-op payload (`started=false`, `reason="rate-limited"`) when called within cooldown
- ✓ Batch small changes before syncing (debounce) — implemented via semaphore + trailing pass coalescing in `SyncEngine.SyncAsync()`
- ✓ Graceful shutdown (complete in-progress transfers, save state)

#### Account Management

- ✓ Add account (receive OAuth2 tokens from SyncTray, create sync context)
- ✓ Remove account (stop sync, delete state DB, optionally delete local files)
- ✓ Support multiple accounts per OS user (e.g., personal + work server)

#### Error Handling & Recovery

- ✓ Retry failed operations with exponential backoff
- ✓ Handle network disconnection gracefully (queue changes, retry on reconnect)
- ✓ Handle server errors (5xx — retry; 4xx — log and skip)
- ✓ Handle disk full conditions (pause sync, notify user) — `SyncEngine` now detects disk-full IO failures (`0x80070070` + OS-specific ENOSPC text), pauses further sync attempts, and emits a `SyncState.Error`/`LastError` surfaced via existing SyncTray `sync-error` notifications
- ✓ Log all sync activity with structured logging

---

## Phase 1.16: Client.SyncTray — Avalonia Tray App

### DotNetCloud.Client.SyncTray Project

**Tray icon, sync status, and settings for desktop users**

#### Project Setup

- ✓ Create `DotNetCloud.Client.SyncTray` Avalonia project
- ✓ Add to `DotNetCloud.sln`
- ✓ Configure tray icon support (Windows + Linux)
- ✓ Configure single-instance enforcement

#### Tray Icon

- ✓ Display tray icon with sync status indicators:
  - ✓ Idle (synced, green check)
  - ✓ Syncing (animated spinner)
  - ✓ Paused (yellow pause icon)
  - ✓ Error (red exclamation)
  - ✓ Offline (gray disconnected)
- ✓ Show tooltip with sync summary (e.g., "3 files syncing, 2.5 GB free")

#### Tray Context Menu

- ✓ "Open sync folder" (opens file explorer at sync root)
- ✓ "Open sync service logs" (opens sync service log folder)
- ✓ "Open tray logs" (opens SyncTray log folder)
- ✓ "Open DotNetCloud in browser" (opens web UI)
- ✓ "Sync now" (trigger immediate sync)
- ✓ "Pause syncing" / "Resume syncing"
- ✓ "Settings..." (open settings window)
- ✓ "Quit"

#### Linux Desktop Integration

- ✓ Start-menu launcher entry (`~/.local/share/applications/dotnetcloud-sync-tray.desktop`) created/maintained at startup with cloud icon asset
- ✓ Desktop client bundle installers upgraded to SyncTray-only deployment after the SyncService merge; installer reruns now remove stale SyncService service/binary artifacts and avoid Linux self-copy failures during binary permission fixup

#### Settings Window

- ✓ Account management:
  - ✓ List connected accounts (server URL, user, status)
  - ✓ Add account button (launches OAuth2 flow in browser)
  - ✓ Remove account button
  - ✓ Switch default account
- ✓ Sync folder configuration:
  - ✓ Change sync root folder
  - ✓ Selective sync (folder tree with checkboxes)
- ✓ General settings:
  - ✓ Start on login (auto-start, Linux XDG autostart wired)
  - ✓ Full scan interval
  - ✓ Bandwidth limits (upload/download)
  - ✓ Notification preferences

#### Notifications

- ✓ Show Windows toast / Linux libnotify notifications:
  - ✓ Sync completed
  - ✓ Conflict detected (with "Resolve" action)
  - ✓ Error occurred (with details)
  - ✓ Quota warning (80%, 95%)

#### Regression Validation

- ✓ Run Phase 2.9 regression checklist pass (`dotnet test`: 2013 total, 0 failed)
- ✓ Run Phase 2.9 quick-reply regression pass (`dotnet test`: 71/71 SyncTray tests pass)

#### Release Hardening

- ✓ Accessibility pass for interactive chat UI controls (`title`/`aria-label` updates across `ChannelList`, `AnnouncementList`, `MessageList`, `DirectMessageView`)
- ✓ Empty-state copy improvements for channel, DM, announcement, and message views
- ✓ Error-state handling with `ErrorMessage` support in `ChannelList`, `MessageList`, and `AnnouncementList`
- ✓ Loading skeletons/states for `ChannelList` and `AnnouncementList`
- ✓ Settings UI confirms `IsMuteChatNotifications` is wired in `SettingsWindow` (`CheckBox` binding + tooltip)

#### Security Audit Remediation (2026-03-22)

- ✓ Remove hardcoded development server URL default from SyncTray settings (`SettingsViewModel._addAccountServerUrl` now defaults to empty)
- ✓ Restrict Linux/macOS Unix socket file mode to owner read/write only (`0600`) after bind in SyncService IPC server
- ✓ Block symlink materialization when resolved symlink target escapes the configured sync root
- ✓ Reject remote path resolution when resolved path escapes the configured sync root (prevents `../` traversal)
- ✓ Add/extend security regression tests for all four findings (SyncTray, SyncService, SyncEngine)

---

## Phase 2: Chat & Notifications

**Goal:** Real-time messaging + Android app.

**Expected Duration:** 10-14 weeks

### Phase 2 Overview

This phase implements real-time chat, announcements, push notifications, and the Android client. It includes:

1. Chat module (channels, DMs, typing indicators, presence, file sharing in chat)
2. Announcements module (organization-wide broadcasts)
3. Chat Web UI (Blazor)
4. Desktop client chat integration
5. Android MAUI app (chat, push notifications)
6. Push notifications (FCM / UnifiedPush)
7. SignalR real-time delivery integration
8. Comprehensive testing and documentation

### Milestone Criteria

- [ ] Users can create channels and send/receive messages in real time
- [ ] Direct messages work between two users
- [ ] Typing indicators and presence (online/offline/away) display correctly
- [ ] Files can be shared inline in chat messages
- [ ] Announcements can be posted and viewed organization-wide
- [ ] Push notifications reach Android devices (FCM and UnifiedPush)
- [ ] Android MAUI app connects, authenticates, and displays chat
- [ ] Desktop client shows chat notifications
- [ ] All unit and integration tests pass
- [ ] Chat works across web, desktop, and Android simultaneously

---

## Phase 2.1: Chat Core Abstractions & Data Models

### DotNetCloud.Modules.Chat Project

**Create chat module project and core domain models**

#### Project Setup

- ✓ Create `DotNetCloud.Modules.Chat` class library project
- ✓ Create `DotNetCloud.Modules.Chat.Data` class library project (EF Core)
- ✓ Create `DotNetCloud.Modules.Chat.Host` ASP.NET Core project (gRPC host)
- ✓ Create `DotNetCloud.Modules.Chat.Tests` test project (MSTest)
- ✓ Add projects to `DotNetCloud.sln`
- ✓ Configure project references and `InternalsVisibleTo`

#### Chat Module Manifest

- ✓ Create `ChatModuleManifest` implementing `IModuleManifest`:
  - ✓ `Id` → `"dotnetcloud.chat"`
  - ✓ `Name` → `"Chat"`
  - ✓ `Version` → `"1.0.0"`
  - ✓ `RequiredCapabilities` → `INotificationService`, `IUserDirectory`, `ICurrentUserContext`, `IRealtimeBroadcaster`
  - ✓ `PublishedEvents` → `MessageSentEvent`, `ChannelCreatedEvent`, `ChannelDeletedEvent`, `UserJoinedChannelEvent`, `UserLeftChannelEvent`
  - ✓ `SubscribedEvents` → `FileUploadedEvent` (for file sharing in chat)

#### Channel Model

- ✓ Create `Channel` entity:
  - ✓ `Guid Id` primary key
  - ✓ `string Name` property
  - ✓ `string? Description` property
  - ✓ `ChannelType Type` property (Public, Private, DirectMessage, Group)
  - ✓ `Guid? OrganizationId` FK (null for DMs)
  - ✓ `Guid CreatedByUserId` FK
  - ✓ `DateTime CreatedAt` property
  - ✓ `DateTime? LastActivityAt` property
  - ✓ `bool IsArchived` property
  - ✓ `string? AvatarUrl` property
  - ✓ `string? Topic` property
  - ✓ Soft-delete support (`IsDeleted`, `DeletedAt`)
- ✓ Create `ChannelType` enum (Public, Private, DirectMessage, Group)

#### Channel Member Model

- ✓ Create `ChannelMember` entity:
  - ✓ `Guid Id` primary key
  - ✓ `Guid ChannelId` FK
  - ✓ `Guid UserId` FK
  - ✓ `ChannelMemberRole Role` property (Owner, Admin, Member)
  - ✓ `DateTime JoinedAt` property
  - ✓ `DateTime? LastReadAt` property (for unread tracking)
  - ✓ `Guid? LastReadMessageId` FK (for precise unread marker)
  - ✓ `bool IsMuted` property
  - ✓ `bool IsPinned` property
  - ✓ `NotificationPreference NotificationPref` property
- ✓ Create `ChannelMemberRole` enum (Owner, Admin, Member)
- ✓ Create `NotificationPreference` enum (All, Mentions, None)

#### Message Model

- ✓ Create `Message` entity:
  - ✓ `Guid Id` primary key
  - ✓ `Guid ChannelId` FK
  - ✓ `Guid SenderUserId` FK
  - ✓ `string Content` property (Markdown-supported text)
  - ✓ `MessageType Type` property (Text, System, FileShare, Reply)
  - ✓ `DateTime SentAt` property
  - ✓ `DateTime? EditedAt` property
  - ✓ `bool IsEdited` property
  - ✓ `Guid? ReplyToMessageId` FK (threaded replies)
  - ✓ `Message? ReplyToMessage` navigation property
  - ✓ Soft-delete support (`IsDeleted`, `DeletedAt`)
- ✓ Create `MessageType` enum (Text, System, FileShare, Reply)

#### Message Attachment Model

- ✓ Create `MessageAttachment` entity:
  - ✓ `Guid Id` primary key
  - ✓ `Guid MessageId` FK
  - ✓ `Guid? FileNodeId` FK (reference to Files module `FileNode`)
  - ✓ `string FileName` property
  - ✓ `string MimeType` property
  - ✓ `long FileSize` property
  - ✓ `string? ThumbnailUrl` property
  - ✓ `int SortOrder` property

#### Reaction Model

- ✓ Create `MessageReaction` entity:
  - ✓ `Guid Id` primary key
  - ✓ `Guid MessageId` FK
  - ✓ `Guid UserId` FK
  - ✓ `string Emoji` property (Unicode emoji or custom emoji code)
  - ✓ `DateTime ReactedAt` property
  - ✓ Unique constraint: (`MessageId`, `UserId`, `Emoji`)

#### Mention Model

- ✓ Create `MessageMention` entity:
  - ✓ `Guid Id` primary key
  - ✓ `Guid MessageId` FK
  - ✓ `Guid? MentionedUserId` FK (null for @channel/@all)
  - ✓ `MentionType Type` property (User, Channel, All)
  - ✓ `int StartIndex` property (position in message text)
  - ✓ `int Length` property
- ✓ Create `MentionType` enum (User, Channel, All)

#### Pinned Message Model

- ✓ Create `PinnedMessage` entity:
  - ✓ `Guid Id` primary key
  - ✓ `Guid ChannelId` FK
  - ✓ `Guid MessageId` FK
  - ✓ `Guid PinnedByUserId` FK
  - ✓ `DateTime PinnedAt` property

#### Data Transfer Objects (DTOs)

- ✓ Create `ChannelDto`, `CreateChannelDto`, `UpdateChannelDto`
- ✓ Create `ChannelMemberDto`, `AddChannelMemberDto`
- ✓ Create `MessageDto`, `SendMessageDto`, `EditMessageDto`
- ✓ Create `MessageAttachmentDto`
- ✓ Create `MessageReactionDto`
- ✓ Create `TypingIndicatorDto`
- ✓ Create `PresenceDto`
- ✓ Create `UnreadCountDto`

#### Event Definitions

- ✓ Create `MessageSentEvent` implementing `IEvent`
- ✓ Create `MessageEditedEvent` implementing `IEvent`
- ✓ Create `MessageDeletedEvent` implementing `IEvent`
- ✓ Create `ChannelCreatedEvent` implementing `IEvent`
- ✓ Create `ChannelDeletedEvent` implementing `IEvent`
- ✓ Create `ChannelArchivedEvent` implementing `IEvent`
- ✓ Create `UserJoinedChannelEvent` implementing `IEvent`
- ✓ Create `UserLeftChannelEvent` implementing `IEvent`
- ✓ Create `ReactionAddedEvent` implementing `IEvent`
- ✓ Create `ReactionRemovedEvent` implementing `IEvent`

#### Event Handlers

- ✓ Create `MessageSentEventHandler` implementing `IEventHandler<MessageSentEvent>`
- ✓ Create `ChannelCreatedEventHandler` implementing `IEventHandler<ChannelCreatedEvent>`

---

## Phase 2.2: Chat Database & Data Access Layer

### DotNetCloud.Modules.Chat.Data Project

**Create EF Core database context and configurations**

#### Entity Configurations

- ✓ Create `ChannelConfiguration` (IEntityTypeConfiguration)
  - ✓ Table name via naming strategy (`chat.channels` / `chat_channels`)
  - ✓ Index on `OrganizationId`
  - ✓ Index on `Type`
  - ✓ Soft-delete query filter
- ✓ Create `ChannelMemberConfiguration`
  - ✓ Composite unique index on (`ChannelId`, `UserId`)
  - ✓ FK relationships to `Channel`
- ✓ Create `MessageConfiguration`
  - ✓ Index on (`ChannelId`, `SentAt`) for efficient channel message loading
  - ✓ Index on `SenderUserId`
  - ✓ FK to `Channel`, FK to `ReplyToMessage` (self-referencing)
  - ✓ Soft-delete query filter
- ✓ Create `MessageAttachmentConfiguration`
  - ✓ FK to `Message`
  - ✓ Index on `FileNodeId`
- ✓ Create `MessageReactionConfiguration`
  - ✓ Composite unique index on (`MessageId`, `UserId`, `Emoji`)
  - ✓ FK to `Message`
- ✓ Create `MessageMentionConfiguration`
  - ✓ FK to `Message`
  - ✓ Index on `MentionedUserId`
- ✓ Create `PinnedMessageConfiguration`
  - ✓ FK to `Channel`, FK to `Message`
  - ✓ Unique index on (`ChannelId`, `MessageId`)

#### ChatDbContext

- ✓ Create `ChatDbContext` class extending `DbContext`:
  - ✓ `DbSet<Channel> Channels`
  - ✓ `DbSet<ChannelMember> ChannelMembers`
  - ✓ `DbSet<Message> Messages`
  - ✓ `DbSet<MessageAttachment> MessageAttachments`
  - ✓ `DbSet<MessageReaction> MessageReactions`
  - ✓ `DbSet<MessageMention> MessageMentions`
  - ✓ `DbSet<PinnedMessage> PinnedMessages`
- ✓ Apply table naming strategy (schema-based for PostgreSQL/SQL Server, prefix-based for MariaDB)
- ✓ Configure automatic timestamps (`SentAt`, `JoinedAt`, etc.)
- ✓ Create design-time factory for migrations

#### Migrations

- ✓ Create PostgreSQL initial migration
- ✓ Create SQL Server initial migration
- ☐ Create MariaDB initial migration (when Pomelo supports .NET 10)

#### Database Initialization

- ✓ Create `ChatDbInitializer`:
  - ✓ Seed default system channels (e.g., `#general`, `#announcements`)
  - ✓ Configure default channel settings

---

## Phase 2.3: Chat Business Logic & Services

### DotNetCloud.Modules.Chat Project (Services)

**Core chat business logic**

#### Channel Service

- ✓ Create `IChannelService` interface:
  - ✓ `Task<ChannelDto> CreateChannelAsync(CreateChannelDto dto, CallerContext caller)`
  - ✓ `Task<ChannelDto> GetChannelAsync(Guid channelId, CallerContext caller)`
  - ✓ `Task<IReadOnlyList<ChannelDto>> ListChannelsAsync(CallerContext caller)`
  - ✓ `Task<ChannelDto> UpdateChannelAsync(Guid channelId, UpdateChannelDto dto, CallerContext caller)`
  - ✓ `Task DeleteChannelAsync(Guid channelId, CallerContext caller)`
  - ✓ `Task ArchiveChannelAsync(Guid channelId, CallerContext caller)`
  - ✓ `Task<ChannelDto> GetOrCreateDirectMessageAsync(Guid otherUserId, CallerContext caller)`
- ✓ Implement `ChannelService`
- ✓ Add authorization checks (owner/admin for updates/deletes)
- ✓ Validate channel name uniqueness within organization

#### Channel Member Service

- ✓ Create `IChannelMemberService` interface:
  - ✓ `Task AddMemberAsync(Guid channelId, Guid userId, CallerContext caller)`
  - ✓ `Task RemoveMemberAsync(Guid channelId, Guid userId, CallerContext caller)`
  - ✓ `Task<IReadOnlyList<ChannelMemberDto>> ListMembersAsync(Guid channelId, CallerContext caller)`
  - ✓ `Task UpdateMemberRoleAsync(Guid channelId, Guid userId, ChannelMemberRole role, CallerContext caller)`
  - ✓ `Task UpdateNotificationPreferenceAsync(Guid channelId, NotificationPreference pref, CallerContext caller)`
  - ✓ `Task MarkAsReadAsync(Guid channelId, Guid messageId, CallerContext caller)`
  - ✓ `Task<IReadOnlyList<UnreadCountDto>> GetUnreadCountsAsync(CallerContext caller)`
- ✓ Implement `ChannelMemberService`
- ✓ Enforce owner/admin authorization for membership management actions
- ✓ Prevent removal or demotion of the last channel owner
- ✓ Validate mark-as-read message belongs to target channel
- ✓ Include `@channel` and `@all` in mention unread-count calculations

#### Message Service

- ✓ Create `IMessageService` interface:
  - ✓ `Task<MessageDto> SendMessageAsync(Guid channelId, SendMessageDto dto, CallerContext caller)`
  - ✓ `Task<MessageDto> EditMessageAsync(Guid messageId, EditMessageDto dto, CallerContext caller)`
  - ✓ `Task DeleteMessageAsync(Guid messageId, CallerContext caller)`
  - ✓ `Task<PagedResult<MessageDto>> GetMessagesAsync(Guid channelId, int page, int pageSize, CallerContext caller)`
  - ✓ `Task<PagedResult<MessageDto>> SearchMessagesAsync(Guid channelId, string query, CallerContext caller)`
  - ✓ `Task<MessageDto> GetMessageAsync(Guid messageId, CallerContext caller)`
- ✓ Implement `MessageService`
- ✓ Parse mentions from message content (`@username`, `@channel`, `@all`)
- ✓ Create mention notification dispatching
- ✓ Enforce message length limits

#### Reaction Service

- ✓ Create `IReactionService` interface:
  - ✓ `Task AddReactionAsync(Guid messageId, string emoji, CallerContext caller)`
  - ✓ `Task RemoveReactionAsync(Guid messageId, string emoji, CallerContext caller)`
  - ✓ `Task<IReadOnlyList<MessageReactionDto>> GetReactionsAsync(Guid messageId)`
- ✓ Implement `ReactionService`
- ✓ Enforce channel membership for add/remove reaction operations
- ✓ Normalize emoji input before persistence and event publication
- ✓ Verify reaction event payload consistency (`ReactionAddedEvent`, `ReactionRemovedEvent`)

#### Pin Service

- ✓ Create `IPinService` interface:
  - ✓ `Task PinMessageAsync(Guid channelId, Guid messageId, CallerContext caller)`
  - ✓ `Task UnpinMessageAsync(Guid channelId, Guid messageId, CallerContext caller)`
  - ✓ `Task<IReadOnlyList<MessageDto>> GetPinnedMessagesAsync(Guid channelId, CallerContext caller)`
- ✓ Implement `PinService`
- ✓ Enforce channel membership and channel existence for pin/unpin/list operations
- ✓ Validate pinned message belongs to the target channel
- ✓ Preserve deterministic pinned-message ordering by `PinnedAt` descending

#### Typing Indicator Service

- ✓ Create `ITypingIndicatorService` interface:
  - ✓ `Task NotifyTypingAsync(Guid channelId, CallerContext caller)`
  - ✓ `Task<IReadOnlyList<TypingIndicatorDto>> GetTypingUsersAsync(Guid channelId)`
- ✓ Implement `TypingIndicatorService` (in-memory, time-expiring)
- ✓ Validate channel id input and cancellation-token flow
- ✓ Prune expired and empty channel typing state during reads/cleanup

#### Chat Module Lifecycle

- ✓ Create `ChatModule` implementing `IModule`:
  - ✓ `InitializeAsync` — register services, subscribe to events
  - ✓ `StartAsync` — start background tasks (typing indicator cleanup)
  - ✓ `StopAsync` — drain active connections
- ✓ Register all services in DI container

---

## Phase 2.4: Chat REST API Endpoints

### DotNetCloud.Modules.Chat.Host Project (Controllers)

**REST API for chat operations**

#### Channel Endpoints

- ✓ `POST /api/v1/chat/channels` — Create channel
- ✓ `GET /api/v1/chat/channels` — List channels for current user
- ✓ `GET /api/v1/chat/channels/{channelId}` — Get channel details
- ✓ `PUT /api/v1/chat/channels/{channelId}` — Update channel
- ✓ `DELETE /api/v1/chat/channels/{channelId}` — Delete channel
- ✓ `POST /api/v1/chat/channels/{channelId}/archive` — Archive channel
- ✓ `POST /api/v1/chat/channels/dm/{userId}` — Get or create DM channel

#### Channel Member Endpoints

- ✓ `POST /api/v1/chat/channels/{channelId}/members` — Add member
- ✓ `DELETE /api/v1/chat/channels/{channelId}/members/{userId}` — Remove member
- ✓ `GET /api/v1/chat/channels/{channelId}/members` — List members
- ✓ `PUT /api/v1/chat/channels/{channelId}/members/{userId}/role` — Update member role
- ✓ `PUT /api/v1/chat/channels/{channelId}/notifications` — Update notification preference
- ✓ `POST /api/v1/chat/channels/{channelId}/read` — Mark channel as read
- ✓ `GET /api/v1/chat/unread` — Get unread counts for all channels

#### Message Endpoints

- ✓ `POST /api/v1/chat/channels/{channelId}/messages` — Send message
- ✓ `GET /api/v1/chat/channels/{channelId}/messages` — Get messages (paginated)
- ✓ `GET /api/v1/chat/channels/{channelId}/messages/{messageId}` — Get single message
- ✓ `PUT /api/v1/chat/channels/{channelId}/messages/{messageId}` — Edit message
- ✓ `DELETE /api/v1/chat/channels/{channelId}/messages/{messageId}` — Delete message
- ✓ `GET /api/v1/chat/channels/{channelId}/messages/search` — Search messages

#### Reaction Endpoints

- ✓ `POST /api/v1/chat/messages/{messageId}/reactions` — Add reaction
- ✓ `DELETE /api/v1/chat/messages/{messageId}/reactions/{emoji}` — Remove reaction
- ✓ `GET /api/v1/chat/messages/{messageId}/reactions` — Get reactions
- ✓ Map reaction endpoint service denials/not-found/validation to deterministic REST responses (403/404/400)

#### Pin Endpoints

- ✓ `POST /api/v1/chat/channels/{channelId}/pins/{messageId}` — Pin message
- ✓ `DELETE /api/v1/chat/channels/{channelId}/pins/{messageId}` — Unpin message
- ✓ `GET /api/v1/chat/channels/{channelId}/pins` — Get pinned messages
- ✓ Map pin endpoint service denials/not-found to deterministic REST responses (403/404)

#### Typing Endpoints

- ✓ Map typing endpoint validation failures to deterministic REST responses (400)

#### File Sharing Endpoints

- ✓ `POST /api/v1/chat/channels/{channelId}/messages/{messageId}/attachments` — Attach file to message
- ✓ `GET /api/v1/chat/channels/{channelId}/files` — List files shared in channel

#### API Verification

- ✓ Add controller/API verification tests for response envelope and deterministic denial-path status mapping

---

## Phase 2.5: SignalR Real-Time Chat Integration

### Real-Time Messaging via SignalR

**Integrate chat module with core SignalR hub**

#### Chat SignalR Methods

- ✓ Register chat event handlers in `CoreHub`:
  - ✓ `SendMessage(channelId, content, replyToId?)` — client sends message
  - ✓ `EditMessage(messageId, newContent)` — client edits message
  - ✓ `DeleteMessage(messageId)` — client deletes message
  - ✓ `StartTyping(channelId)` — client starts typing
  - ✓ `StopTyping(channelId)` — client stops typing
  - ✓ `MarkRead(channelId, messageId)` — client marks channel as read
  - ✓ `AddReaction(messageId, emoji)` — client adds reaction
  - ✓ `RemoveReaction(messageId, emoji)` — client removes reaction

#### Server-to-Client Broadcasts

- ✓ `NewMessage(channelId, messageDto)` — broadcast to channel members
- ✓ `MessageEdited(channelId, messageDto)` — broadcast edit
- ✓ `MessageDeleted(channelId, messageId)` — broadcast deletion
- ✓ `TypingIndicator(channelId, userId, displayName)` — broadcast typing
- ✓ `ReactionUpdated(channelId, messageId, reactions)` — broadcast reaction change
- ✓ `ChannelUpdated(channelDto)` — broadcast channel metadata change
- ✓ `MemberJoined(channelId, memberDto)` — broadcast new member
- ✓ `MemberLeft(channelId, userId)` — broadcast member removal
- ✓ `UnreadCountUpdated(channelId, count)` — broadcast unread count

#### Connection Group Management

- ✓ Add users to SignalR groups per channel membership
- ✓ Remove users from groups when leaving channels
- ✓ Update groups on channel creation/deletion
- ✓ Handle reconnection (re-join all channel groups)

#### Presence Integration

- ✓ Extend existing presence tracking for chat-specific status:
  - ✓ Online, Away, Do Not Disturb, Offline
  - ✓ Custom status message support
- ✓ Broadcast presence changes to relevant channel members
- ✓ Create `PresenceChangedEvent` for cross-module awareness

---

## Phase 2.6: Announcements Module

### DotNetCloud.Modules.Announcements

**Organization-wide broadcast announcements**

#### Announcement Model

- ✓ Create `Announcement` entity:
  - ✓ `Guid Id` primary key
  - ✓ `Guid OrganizationId` FK
  - ✓ `Guid AuthorUserId` FK
  - ✓ `string Title` property
  - ✓ `string Content` property (Markdown)
  - ✓ `AnnouncementPriority Priority` property (Normal, Important, Urgent)
  - ✓ `DateTime PublishedAt` property
  - ✓ `DateTime? ExpiresAt` property
  - ✓ `bool IsPinned` property
  - ✓ `bool RequiresAcknowledgement` property
  - ✓ Soft-delete support
- ✓ Create `AnnouncementPriority` enum (Normal, Important, Urgent)

#### Announcement Acknowledgement

- ✓ Create `AnnouncementAcknowledgement` entity:
  - ✓ `Guid Id` primary key
  - ✓ `Guid AnnouncementId` FK

---

## Shared File Folder Workstream

#### Group Foundation

- ✓ Add `IGroupDirectory` capability contract
- ✓ Add `IGroupManager` capability contract
- ✓ Implement `GroupDirectoryService` in Core.Auth
- ✓ Implement `GroupManagerService` in Core.Auth
- ✓ Add focused Core.Auth tests for group query and membership management flows
- ✓ Add protected built-in `All Users` group semantics and backfill logic
- ✓ Add admin group CRUD and membership API surfaces
- ✓ Add admin group management UI
- ✓ Add core-host integration coverage for group admin endpoints

#### Shared Folder Integration

- ✓ Harden Files permission evaluation for user, team, and group shares
- ✓ Add mounted-access listing path for team/group shared content
- ✓ Add admin shared-folder definitions and path validation
- ✓ Add admin shared-folder CRUD API, group assignment, and scan controls
- ✓ Add admin shared-folder web UI for CRUD, group assignment, scan actions, and constrained source picking
- ✓ Back admin shared-folder Rescan Now and Reindex actions with a maintenance worker and Files-module reindex dispatch
- ✓ Default admin source-folder browsing to the platform filesystem root while still honoring configured relative-source bases when present
- ✓ Surface virtual `_DotNetCloud` root with mounted shared folders
- ✓ Enforce read-only behavior for mounted shared-folder paths
- ✓ Add group-aware mounted-folder search indexing and navigation
- ✓ Add shared-folder media source selection for Music, Photos, and Video
- ✓ Add media shared-source scan verification coverage
- ✓ Add media-library shared-source API verification coverage
- ✓ Add core-host media-library shared-source endpoint integration coverage
- ✓ Add shared-folder admin endpoint integration coverage
- ✓ Add `_DotNetCloud` browsing and mounted read-only integration coverage
- ✓ Live-verify admin shared-folder creation on mint22 and confirm non-admin mounted access in a separate browser session
- ✓ Keep sync clients ignoring `_DotNetCloud` admin shares in v1
  - ✓ `Guid UserId` FK
  - ✓ `DateTime AcknowledgedAt` property
  - ✓ Unique constraint: (`AnnouncementId`, `UserId`)

#### Announcement Service

- ✓ Create `IAnnouncementService` interface:
  - ✓ `Task<AnnouncementDto> CreateAsync(CreateAnnouncementDto dto, CallerContext caller)`
  - ✓ `Task<IReadOnlyList<AnnouncementDto>> ListAsync(CallerContext caller)`
  - ✓ `Task<AnnouncementDto> GetAsync(Guid id, CallerContext caller)`
  - ✓ `Task UpdateAsync(Guid id, UpdateAnnouncementDto dto, CallerContext caller)`
  - ✓ `Task DeleteAsync(Guid id, CallerContext caller)`
  - ✓ `Task AcknowledgeAsync(Guid id, CallerContext caller)`
  - ✓ `Task<IReadOnlyList<AnnouncementAcknowledgementDto>> GetAcknowledgementsAsync(Guid id, CallerContext caller)`
- ✓ Implement `AnnouncementService`

#### Announcement Endpoints

- ✓ `POST /api/v1/announcements` — Create announcement (admin)
- ✓ `GET /api/v1/announcements` — List announcements
- ✓ `GET /api/v1/announcements/{id}` — Get announcement
- ✓ `PUT /api/v1/announcements/{id}` — Update announcement (admin)
- ✓ `DELETE /api/v1/announcements/{id}` — Delete announcement (admin)
- ✓ `POST /api/v1/announcements/{id}/acknowledge` — Acknowledge announcement
- ✓ `GET /api/v1/announcements/{id}/acknowledgements` — List who acknowledged

#### Real-Time Announcements

- ✓ Broadcast new announcements via SignalR to all connected users
- ✓ Broadcast urgent announcements with visual/audio notification
- ✓ Update announcement badge counts in real time

---

## Phase 2.7: Push Notifications Infrastructure

### Push Notification Service

**FCM and UnifiedPush support for mobile clients**

#### Notification Abstractions

- ✓ Create `IPushNotificationService` interface:
  - ✓ `Task SendAsync(Guid userId, PushNotification notification)`
  - ✓ `Task SendToMultipleAsync(IEnumerable<Guid> userIds, PushNotification notification)`
  - ✓ `Task RegisterDeviceAsync(Guid userId, DeviceRegistration registration)`
  - ✓ `Task UnregisterDeviceAsync(Guid userId, string deviceToken)`
- ✓ Create `PushNotification` model:
  - ✓ `string Title` property
  - ✓ `string Body` property
  - ✓ `string? ImageUrl` property
  - ✓ `Dictionary<string, string> Data` property (custom payload)
  - ✓ `NotificationCategory Category` property
- ✓ Create `DeviceRegistration` model:
  - ✓ `string Token` property
  - ✓ `PushProvider Provider` property (FCM, UnifiedPush)
  - ✓ `string? Endpoint` property (UnifiedPush endpoint URL)
- ✓ Create `PushProvider` enum (FCM, UnifiedPush)
- ✓ Create `NotificationCategory` enum (ChatMessage, ChatMention, Announcement, FileShared, System)

#### FCM Provider

- ✓ Create `FcmPushProvider` implementing `IPushNotificationService`:
  - ✓ Configure Firebase Admin SDK credentials (FcmPushOptions: ProjectId, CredentialsPath, bound from config)
  - ✓ Implement message sending via FCM HTTP v1 API
  - ✓ Handle token refresh and invalid token cleanup
  - ✓ Implement batch sending for efficiency (FcmHttpTransport with concurrent Task.WhenAll dispatch)
- ✓ Create FCM configuration model
- ✓ Add admin UI for FCM credential management (PushNotificationSettings.razor admin page)

#### UnifiedPush Provider

- ✓ Create `UnifiedPushProvider` implementing `IPushNotificationService`:
  - ✓ Implement HTTP POST to UnifiedPush distributor endpoint
  - ✓ Handle endpoint URL registration
  - ✓ Implement error handling and retries
- ✓ Create UnifiedPush configuration model

#### Notification Routing

- ✓ Create `NotificationRouter`:
  - ✓ Route notifications based on user's registered device provider
  - ✓ Support multiple devices per user
  - ✓ Respect user notification preferences (per-channel mute, DND)
  - ✓ Implement notification deduplication (don't notify if user is online)
- ✓ Create notification queue for reliability (background processing)

#### Push Notification Endpoints

- ✓ `POST /api/v1/notifications/devices/register` — Register device for push
- ✓ `DELETE /api/v1/notifications/devices/{deviceToken}` — Unregister device
- ✓ `GET /api/v1/notifications/preferences` — Get notification preferences
- ✓ `PUT /api/v1/notifications/preferences` — Update notification preferences
- ✓ `POST /api/v1/notifications/{id}/send` — Send test notification

---

## Phase 2.8: Chat Web UI (Blazor)

### DotNetCloud.Modules.Chat UI Components

**Blazor chat interface for the web application**

#### Channel List Component

- ✓ Create `ChannelList.razor` sidebar component:
  - ✓ Display public, private, and DM channels
  - ✓ Show unread message counts and badges
  - ✓ Highlight active channel
  - ✓ Show channel search/filter
  - ✓ Display channel creation button
  - ✓ Show user presence indicators
  - ✓ Support drag-to-reorder pinned channels

#### Channel Header Component

- ✓ Create `ChannelHeader.razor`:
  - ✓ Display channel name, topic, and member count
  - ✓ Show channel actions (edit, archive, leave, pin/unpin)
  - ✓ Display member list toggle button
  - ✓ Show search button for in-channel search

#### Message List Component

- ✓ Create `MessageList.razor`:
  - ✓ Display messages with sender avatar, name, and timestamp
  - ✓ Support Markdown rendering in messages
  - ✓ Show inline file previews (images, documents)
  - ✓ Display reply threads (indented/linked)
  - ✓ Show message reactions with emoji counts
  - ✓ Support infinite scroll (load older messages)
  - ✓ Show "new messages" divider line
  - ✓ Display system messages (user joined, left, etc.)
  - ✓ Show edited indicator on edited messages

#### Message Composer Component

- ✓ Create `MessageComposer.razor`:
  - ✓ Rich text input with Markdown toolbar
  - ✓ `@mention` autocomplete (users and channels)
  - ✓ Emoji picker
  - ✓ File attachment button (integrates with Files module upload)
  - ✓ Reply-to message preview
  - ✓ Send button and Enter key handling
  - ✓ Typing indicator broadcast on input
  - ✓ Paste image support (auto-upload)

#### Typing Indicator Component

- ✓ Create `TypingIndicator.razor`:
  - ✓ Show "User is typing..." or "User1, User2 are typing..."
  - ✓ Animate typing dots
  - ✓ Auto-expire after timeout

#### Member List Panel

- ✓ Create `MemberListPanel.razor`:
  - ✓ Display channel members grouped by role (Owner, Admin, Member)
  - ✓ Show online/offline/away status per member
  - ✓ Support member actions (promote, demote, remove)
  - ✓ Display member profile popup on click

#### Channel Settings Dialog

- ✓ Create `ChannelSettingsDialog.razor`:
  - ✓ Edit channel name, description, topic
  - ✓ Manage members (add/remove/change role)
  - ✓ Configure notification preferences
  - ✓ Delete/archive channel option
  - ✓ Show channel creation date and creator

#### Direct Message View

- ✓ Create `DirectMessageView.razor`:
  - ✓ User search for starting new DM
  - ✓ Display DM conversations list
  - ✓ Show user online status
  - ✓ Group DM support (2+ users)

#### Chat Notification Badge

- ✓ Create `ChatNotificationBadge.razor`:
  - ✓ Display total unread count in navigation
  - ✓ Update in real time via SignalR
  - ✓ Distinguish mentions from regular messages
  - ✓ Clear badge when messages are read (via SignalR sync)

#### Quick Reply

- ✓ Add quick reply popup from notification
- ✓ Send reply via REST API
- ✓ Show typing indicator while composing

#### Regression Validation

- ✓ Run Phase 2.9 regression checklist pass (`dotnet test`: 2013 total, 0 failed)
- ✓ Run Phase 2.9 quick-reply regression pass (`dotnet test`: 71/71 SyncTray tests pass)

#### Release Hardening

- ✓ Accessibility pass for interactive chat UI controls (`title`/`aria-label` updates across `ChannelList`, `AnnouncementList`, `MessageList`, `DirectMessageView`)
- ✓ Empty-state copy improvements for channel, DM, announcement, and message views
- ✓ Error-state handling with `ErrorMessage` support in `ChannelList`, `MessageList`, and `AnnouncementList`
- ✓ Loading skeletons/states for `ChannelList` and `AnnouncementList`
- ✓ Settings UI confirms `IsMuteChatNotifications` is wired in `SettingsWindow` (`CheckBox` binding + tooltip)

---

## Phase 2.10: Android MAUI App

### DotNetCloud.Clients.Android Project

**Android app using .NET MAUI**

#### Project Setup

- ✓ Create `DotNetCloud.Clients.Android` .NET MAUI project
- ✓ Configure Android-specific settings (minimum SDK, target SDK)
- ✓ Set up build flavors: `googleplay` (FCM) and `fdroid` (UnifiedPush)
- ✓ Add to solution file
- ✓ Configure app icon and splash screen

#### Authentication

- ✓ Create login screen
- ✓ Implement OAuth2/OIDC authentication flow (system browser redirect)
- ✓ Fix Android OAuth callback chooser registration so only one `DotNetCloud` app target handles `net.dotnetcloud.client://oauth2redirect`
- ✓ Allow Android OAuth token exchange and follow-on API clients to accept self-signed certificates for private LAN hosts such as `mint22.kimball.home`
- ✓ Fix Android post-login white screen by routing successful login to `//Main/ChannelList` and keeping Shell navigation / bound collection updates on the UI thread
- ✓ Implement token storage (Android Keystore)
- ✓ Implement token refresh
- ✓ Support multiple server connections

#### Chat UI

- ✓ Create channel list view (tabs: Channels, DMs)
- ✓ Create message list view with RecyclerView-style virtualization
- ✓ Create message composer with:
  - ✓ Text input
  - ✓ Emoji picker
  - ✓ File attachment (camera, gallery, file picker)
  - ✓ `@mention` autocomplete
- ✓ Create channel details view (members, settings)
- ✓ Implement pull-to-refresh for message history
- ✓ Support dark/light theme

#### Real-Time Connection

- ✓ Implement SignalR client connection
- ✓ Handle connection lifecycle (connect, reconnect, disconnect)
- ✓ Background connection management (Android foreground service)
- ✓ Handle Doze mode and battery optimization

#### Push Notifications

- ✓ Integrate Firebase Cloud Messaging (FCM) for `googleplay` flavor
- ✓ Integrate UnifiedPush for `fdroid` flavor
- ✓ Create notification channels (Chat, Mentions, Announcements)
- ✓ Implement notification tap handlers (open specific chat)
- ✓ Display notification badges on app icon

#### Offline Support

- ✓ Cache recent messages locally (SQLite or LiteDB)
- ✓ Queue outgoing messages when offline
- ✓ Sync on reconnection
- ✓ Display cached data while loading

#### Photo Auto-Upload (File Integration)

- ✓ Detect new photos via MediaStore content observer
- ✓ Upload via Files module API (chunked upload)
- ✓ Configurable: WiFi only, battery threshold
- ✓ Progress notification during upload

#### File Browser

- ✓ Create `IFileRestClient` interface (browse, upload, download, quota, folder CRUD)
- ✓ Implement `HttpFileRestClient` with chunked upload protocol and envelope unwrapping
- ✓ Create `FileBrowserViewModel` with folder navigation, file picker upload, camera capture (photo + video), download-and-open, delete, quota display
- ✓ Create `FileBrowserPage.xaml` UI with toolbar, CollectionView, swipe-to-delete, upload progress, quota bar
- ✓ Register `IFileRestClient` → `HttpFileRestClient` in DI (AddHttpClient)
- ✓ Add Files tab to `AppShell.xaml` (between Chat and Settings)

#### Media Auto-Upload (Photos + Videos)

- ✓ Create `IMediaAutoUploadService` interface (start, stop, scan-now)
- ✓ Implement `MediaAutoUploadService` scanning both photos and videos from MediaStore
- ✓ Organize uploads into `InstantUpload/YYYY/MM` folder hierarchy (default on)
- ✓ Configurable upload folder name (default: "InstantUpload")
- ✓ Upload via `IFileRestClient` (chunked upload with folder parentId)
- ✓ Add `ChannelIdMediaUpload` notification channel in `MainApplication.cs`
- ✓ Register `IMediaAutoUploadService` → `MediaAutoUploadService` in DI

#### Android Distribution

- ✓ Configure Google Play Store build (signed APK/AAB)
- ✓ Configure F-Droid build (reproducible, no proprietary deps)
- ✓ Create direct APK download option
- ✓ Write app store listing description

---

## Phase 2.11: Chat Module gRPC Host

### DotNetCloud.Modules.Chat.Host Project

**gRPC service implementation for chat module**

#### Proto Definitions

- ✓ Create `chat_service.proto`:
  - ✓ `rpc CreateChannel(CreateChannelRequest) returns (ChannelResponse)`
  - ✓ `rpc GetChannel(GetChannelRequest) returns (ChannelResponse)`
  - ✓ `rpc ListChannels(ListChannelsRequest) returns (ListChannelsResponse)`
  - ✓ `rpc SendMessage(SendMessageRequest) returns (MessageResponse)`
  - ✓ `rpc GetMessages(GetMessagesRequest) returns (GetMessagesResponse)`
  - ✓ `rpc EditMessage(EditMessageRequest) returns (MessageResponse)`
  - ✓ `rpc DeleteMessage(DeleteMessageRequest) returns (Empty)`
  - ✓ `rpc AddReaction(AddReactionRequest) returns (Empty)`
  - ✓ `rpc RemoveReaction(RemoveReactionRequest) returns (Empty)`
  - ✓ `rpc NotifyTyping(TypingRequest) returns (Empty)`
- ✓ Create `chat_lifecycle.proto` (start, stop, health) — lifecycle RPCs included in ChatLifecycleService

#### gRPC Service Implementation

- ✓ Create `ChatGrpcService` implementing the proto service
- ✓ Create `ChatLifecycleService` for module lifecycle gRPC
- ✓ Create `ChatHealthCheck` health check implementation

#### Host Program

- ✓ Configure `Program.cs`:
  - ✓ Register EF Core `ChatDbContext`
  - ✓ Register all chat services
  - ✓ Map gRPC services
  - ✓ Map REST controllers
  - ✓ Configure Serilog
  - ✓ Configure OpenTelemetry

---

## Phase 2.12: Testing Infrastructure

### Unit Tests

#### DotNetCloud.Modules.Chat.Tests

- ✓ `ChatModuleManifestTests` — Id, Name, Version, capabilities, events (10 tests)
- ✓ `ChatModuleTests` — lifecycle (initialize, start, stop, dispose) (15 tests)
- ✓ `ChannelTests` — model creation, defaults, validation (10 tests, in ModelTests.cs)
- ✓ `MessageTests` — model creation, defaults, soft delete (10 tests, in ModelTests.cs)
- ✓ `ChannelMemberTests` — role enum, notification preferences (7 tests, in ModelTests.cs)
- ✓ `MessageReactionTests` — uniqueness, emoji validation (3 tests, in ModelTests.cs)
- ✓ `MessageMentionTests` — mention types, index/length validation (5 tests, in ModelTests.cs)
- ✓ `EventTests` — all event records, IEvent interface compliance (10 tests)
- ✓ `EventHandlerTests` — handler logic, logging, cancellation (8 tests, in EventTests.cs)
- ✓ `ChannelServiceTests` — CRUD operations, authorization checks, name uniqueness validation
- ✓ `MessageServiceTests` — send, edit, delete, pagination, search, mentions, attachments (29 tests)
- ✓ `ReactionServiceTests` — add, remove, duplicate handling (7 tests)
- ✓ `PinServiceTests` — pin, unpin, list (5 tests)
- ✓ `TypingIndicatorServiceTests` — notify, expire, list (5 tests)
- ✓ `AnnouncementServiceTests` — CRUD, acknowledgement tracking (18 tests)

### Integration Tests

- ✓ Add chat API integration tests to `DotNetCloud.Integration.Tests`:
  - ✓ Channel CRUD via REST API (create, list, get, update, delete, archive, DM, duplicate-name conflict, not-found)
  - ✓ Message send/receive via REST API (send, paginated list, get, edit, delete, search, search-empty validation)
  - ✓ Member management via REST API (add, list, update role, remove, notification preference, unread counts, mark read)
  - ✓ Reactions via REST API (add, get, remove)
  - ✓ Pins via REST API (pin, unpin, list)
  - ✓ Typing indicators via REST API (notify, get)
  - ✓ File attachment via REST API (add attachment, list channel files)
  - ✓ Announcement CRUD and acknowledgement (create, list, get-404, update, delete, acknowledge, get acknowledgements)
  - ✓ Push notification registration (register, empty-token-400, invalid-provider-400)
  - ✓ End-to-end flow test (create→member→message→react→pin→read)
  - ✓ Module health and info endpoints
- ✓ ChatHostWebApplicationFactory with InMemory DB and NoOp broadcaster
- ✓ Fixed CreatedAtAction route mismatch (SuppressAsyncSuffixInActionNames)
- ✓ Fixed duplicate AnnouncementController route conflict
- ✓ 47 integration tests, all passing

---

## Phase 3: Contacts, Calendar & Notes

### Objective

Deliver Contacts (CardDAV), Calendar (CalDAV), and Notes (Markdown) as process-isolated modules with standards-compliant sync, cross-module integration, and migration tooling.

> **Detailed plan:** `docs/PHASE_3_IMPLEMENTATION_PLAN.md`

### Phase 3.1: Architecture And Contracts

#### Core DTOs & Contracts

- ✓ Contact DTOs (person/org/group, phone/email/address, metadata)
- ✓ Calendar DTOs (calendar, event, attendee, recurrence, reminders)
- ✓ Note DTOs (note document, folder, tag, note metadata)

#### Event Contracts

- ✓ ContactCreated/Updated/DeletedEvent
- ✓ CalendarEventCreated/Updated/DeletedEvent
- ✓ NoteCreated/Updated/DeletedEvent

#### Capability & Validation

- ✓ Capability interfaces and tier mapping for Contacts, Calendar, Notes
- ✓ Validation rules and error code extensions for new domains

### Phase 3.2: Contacts Module

#### Module Projects

- ✓ Create `DotNetCloud.Modules.Contacts` (core logic)
- ✓ Create `DotNetCloud.Modules.Contacts.Data` (EF Core context)
- ✓ Create `DotNetCloud.Modules.Contacts.Host` (gRPC host)

#### Data Model

- ✓ Contact, ContactGroup, Address, PhoneNumber, EmailAddress, CustomField entities
- ✓ EF configurations with multi-provider naming strategies
- ✓ Initial migrations (PostgreSQL + SQL Server)

#### REST API

- ✓ CRUD endpoints for contacts and groups
- ✓ Bulk import/export (vCard format)
- ✓ Search endpoint with full-text support

#### CardDAV

- ✓ Principal and addressbook discovery
- ✓ vCard GET/PUT/DELETE
- ✓ Sync token and change tracking

#### Features

- ✓ Contact avatar upload and attachment metadata
- ✓ Contact sharing model (user/team scoped permissions)

### Phase 3.3: Calendar Module

#### Module Projects

- ✓ Create `DotNetCloud.Modules.Calendar` (core logic)
- ✓ Create `DotNetCloud.Modules.Calendar.Data` (EF Core context)
- ✓ Create `DotNetCloud.Modules.Calendar.Host` (gRPC host)

#### Data Model

- ✓ Calendar, CalendarEvent, Attendee, RecurrenceRule, Reminder, ExceptionInstance entities
- ✓ EF configurations with multi-provider naming strategies
- ✓ Initial migrations (PostgreSQL + SQL Server)

#### REST API

- ✓ CRUD endpoints for calendars and events
- ✓ RSVP / invitation management
- ✓ Calendar sharing and event search/filter

#### CalDAV

- ✓ Calendar discovery and collections
- ✓ iCalendar GET/PUT/DELETE
- ✓ Sync token and change tracking

#### Features

- ✓ Recurrence engine and occurrence expansion service
- ✓ Reminder/notification pipeline (in-app + push)

#### Additional Deliverables

- ✓ gRPC service (11 RPCs) for core ↔ module communication
- ✓ iCalendar RFC 5545 import/export service

#### Calendar Recurrence UI + Organization Support (Phases 1-5)

- ✓ Recurrence picker (Daily/Weekly/Biweekly/Monthly/Yearly) with end date in event editor
- ✓ All-day event toggle with conditional time inputs
- ✓ Color swatch picker with preset colors + custom hex
- ✓ URL field in event editor
- ✓ Modal-based editing: Event, Calendar, Day Details, Delete Confirmation
- ✓ Month grid polish: max 3 events per cell, "+X more" link, time badges, all-day styling
- ✓ Today button in calendar toolbar
- ✓ OrganizationId on Calendar entity + EF configuration + index
- ✓ IOrganizationDirectory capability interface (Restricted tier)
- ✓ OrganizationDirectoryService implementation in Core.Auth
- ✓ Org membership authorization in CalendarService and CalendarEventService
- ✓ CalendarShareService rejects sharing on org calendars
- ✓ REST GET /api/v1/calendars?organizationId={guid} filter
- ✓ gRPC proto: organization_id in CalendarMessage, CreateCalendarRequest, ListCalendarsRequest
- ✓ CalendarGrpcService maps org fields to/from DTOs
- ✓ CalendarModuleManifest registers IOrganizationDirectory capability
- ✓ Module manifest (manifest.json)
- ✓ Attendee management UI in event editor (email, name, role, status)
- ✓ Reminder configuration UI in event editor (method dropdown, minutes input, add/remove)
- ✓ Org picker dropdown in toolbar (My Calendars / Organization filter)
- ✓ Org badge in calendar editor modal (shows org name when creating under org)
- ✓ Monthly BYDAY position recurrence builder (First/Second/Third/Fourth/Last + day-of-week picker)
- ✓ Multi-day event spanning bars in month grid (CSS grid-column span)
- ✓ OrganizationCalendarAuthorizationTests (11 tests: org member, manager, non-member, coexistence)
- ✓ RecurrenceLogicTests (28 tests: RRULE build/parse, BYDAY, round-trip)
- ✓ 179 passing calendar tests (0 failures)

### Phase 3.4: Notes Module

#### Module Projects

- ✓ Create `DotNetCloud.Modules.Notes` (core logic)
- ✓ Create `DotNetCloud.Modules.Notes.Data` (EF Core context)
- ✓ Create `DotNetCloud.Modules.Notes.Host` (gRPC host)

#### Data Model

- ✓ Note, NoteVersion, NoteFolder, NoteTag, NoteLink, NoteShare entities
- ✓ EF configurations with multi-provider naming strategies
- ✓ Initial migrations (PostgreSQL + SQL Server)

#### REST API

- ✓ CRUD endpoints for notes (~25 REST endpoints)
- ✓ Move/copy, tagging, search, version history endpoints

#### gRPC Service

- ✓ 10 RPCs: CreateNote, GetNote, ListNotes, UpdateNote, DeleteNote, SearchNotes, CreateFolder, ListFolders, GetVersionHistory, RestoreVersion
- ✓ Module lifecycle service (Initialize, Start, Stop, HealthCheck, GetManifest)
- ✓ Module manifest (manifest.json)

#### Features

- ✓ Markdown rendering pipeline with XSS sanitization
- ✓ Rich-editor integration (MarkdownEditor Blazor component)
- ✓ Cross-entity link references (Files, Calendar, Contact, Note)
- ✓ Note sharing model (ReadOnly/ReadWrite per-user)
- ✓ Version history with restore
- ✓ Optimistic concurrency via ExpectedVersion
- ✓ 50 passing tests (module lifecycle, CRUD, search, versioning, folders, sharing)

### Phase 3.5: Cross-Module Integration

- ✓ Unified navigation entries and module registration in Blazor shell
- ✓ Add collapsed app-shell sidebar hover labels (`title`/`aria-label`) so icon-only navigation matches Files module behavior
- ✓ Shared notification patterns for invites, reminders, mentions, shares
- ✓ Cross-module link resolution (events↔contacts, notes↔events/contacts)
- ✓ Consistent authorization, audit logging, and soft-delete behavior
- ✓ Align Contacts, Calendar, and Notes collapsed sidebars with the Tracks-style icon-first navigation pattern and hide expanded-only panes/actions while collapsed

### Phase 3.6: Migration Foundation

- ✓ Import contract interfaces and pipeline architecture
- ✓ vCard and iCalendar migration parsers/transformers
- ✓ Notes import adapter (markdown/plain exports)
- ✓ Dry-run mode with import report and conflict summary

### Phase 3.7: Testing And Quality Gates

#### Unit Tests

- ✓ Contacts module test suite (domain, handlers, validators)
- ✓ Calendar module test suite (domain, handlers, recurrence)
- ✓ Notes module test suite (domain, handlers, sanitization)

#### Integration Tests

- ✓ REST endpoint tests for all three modules
- ✓ CardDAV interoperability tests
- ✓ CalDAV interoperability tests

#### Security Tests

- ✓ Authorization bypass attempts
- ✓ Tenant isolation verification
- ✓ Markdown XSS / unsafe content tests

#### Performance

- ✓ Large contact list benchmarks
- ✓ Recurring event expansion benchmarks

### Phase 3.8: Documentation And Release Readiness

- ✓ Admin docs for Contacts, Calendar, Notes configuration
- ✓ User guides for import, sharing, sync, troubleshooting
- ✓ API docs for all new REST and DAV endpoints
- ✓ Upgrade/release notes with migration caveats

---

## Phase 4: Project Management (Tracks)

> Module ID: `dotnetcloud.tracks` | Namespace: `DotNetCloud.Modules.Tracks`
> Detailed plan: `docs/PHASE_4_IMPLEMENTATION_PLAN.md`

### Phase 4.1: Architecture And Contracts

- ✓ `TracksDto.cs` — DTOs for Board, BoardList, Card, Label, Assignment, Comment, Attachment, Sprint, TimeEntry, Dependency
- ✓ `TracksEvents.cs` — BoardCreated, BoardDeleted, CardCreated, CardMoved, CardUpdated, CardDeleted, CardAssigned, CardCommentAdded, SprintStarted, SprintCompleted
- ✓ `ITracksDirectory` capability interface (Public tier)
- ✓ `TRACKS_` error codes in `ErrorCodes.cs`
- ✓ Unit tests for new DTOs and events
- ✓ `ITeamDirectory` capability interface (Restricted tier) — cross-module team read access
- ✓ `ITeamManager` capability interface (Restricted tier) — cross-module team write access
- ✓ `TracksTeamDto`, `TracksTeamMemberDto`, `CreateTracksTeamDto`, `UpdateTracksTeamDto`, `TransferBoardDto` DTOs
- ✓ `TracksTeamMemberRole` enum (Member, Manager, Owner)
- ✓ `TeamCreatedEvent`, `TeamDeletedEvent` events
- ✓ Tracks team error codes: `TracksTeamNotFound`, `TracksNotTeamMember`, `TracksInsufficientTeamRole`, `TracksTeamHasBoards`, `TracksAlreadyTeamMember`

### Phase 4.2: Data Model And Module Scaffold

- ✓ `DotNetCloud.Modules.Tracks/` — TracksModule.cs, TracksModuleManifest.cs
- ✓ `DotNetCloud.Modules.Tracks.Data/` — TracksDbContext, 17 entity models (incl. Team), EF configurations, 2 migrations
- ✓ `DotNetCloud.Modules.Tracks.Host/` — gRPC host scaffold
- ✓ Solution integration (DotNetCloud.sln)
- ✓ Planning poker: PokerSession + PokerVote entities, DTOs, events, EF configs, gRPC RPCs, error codes
- ✓ `TeamRole` entity (TeamId FK → Team, UserId, TracksTeamMemberRole) — Option C: Core teams + Tracks role overlay
- ✓ `Team` entity (Id, Name, Description, CreatedAt, CreatedByUserId) — stores team metadata
- ✓ `TeamConfiguration.cs` — FK cascade from TeamRoles, index on Name
- ✓ `TeamRoleConfiguration.cs` — unique index on (CoreTeamId, UserId), string conversion for Role
- ✓ `Board.TeamId` (nullable Guid) — cross-DB reference to Core team, no FK enforcement

### Phase 4.3: Core Services And Business Logic

- ✓ BoardService — CRUD boards, members/roles, archive
- ✓ ListService — CRUD lists, reorder, WIP limits
- ✓ CardService — CRUD cards, move, assign, priority, due dates, archive
- ✓ LabelService — CRUD labels, assign/remove from cards
- ✓ CommentService — CRUD comments, Markdown content
- ✓ ChecklistService — CRUD checklists/items, toggle completion
- ✓ AttachmentService — File links, external URLs
- ✓ DependencyService — Dependencies, BFS cycle detection
- ✓ SprintService — CRUD sprints, start/complete
- ✓ TimeTrackingService — Timer, manual entry, rollup
- ✓ ActivityService — Mutation logging, activity feed
- ✓ Authorization logic (Owner/Admin/Member/Viewer)
- ✓ Unit tests (112 tests)
- ✓ TeamService — Option C implementation (Core teams + Tracks role overlay)
  - ✓ Team CRUD via ITeamManager capability
  - ✓ Member add/remove/update role via ITeamManager + TeamRoles
  - ✓ Board transfer (personal ↔ team)
  - ✓ GetEffectiveBoardRole (direct member + team-derived role, higher wins)
  - ✓ Graceful degradation when ITeamDirectory/ITeamManager not available
- ✓ TeamDirectoryService — ITeamDirectory implementation (Core.Auth)
- ✓ TeamManagerService — ITeamManager implementation (Core.Auth)
- ✓ DI registration for ITeamDirectory + ITeamManager in AuthServiceExtensions

### Phase 4.4: REST API And gRPC Service

#### REST API (40+ endpoints — 10 controllers)

- ✓ BoardsController — CRUD + activity + members + labels + export/import (15 endpoints)
- ✓ ListsController — CRUD + reorder (5 endpoints)
- ✓ CardsController — CRUD + move + assign + labels + activity (10 endpoints)
- ✓ CommentsController (4 endpoints)
- ✓ ChecklistsController + items (6 endpoints)
- ✓ AttachmentsController (3 endpoints)
- ✓ DependenciesController (3 endpoints)
- ✓ SprintsController — CRUD + start/complete + cards (9 endpoints)
- ✓ TimeEntriesController — CRUD + timer (5 endpoints)
- ✓ TeamsController — CRUD teams + members + role management (9 endpoints, Owner/Manager-gated)

#### gRPC

- ✓ TracksGrpcService — 7 RPCs implemented + 4 poker RPCs implemented in Phase 4.7
- ✓ TracksControllerBase — auth helpers, envelope methods, IsBoardNotFound()

#### Tests

- ✓ 58 controller/gRPC unit tests (199 total Tracks tests, incl. 29 TeamServiceTests)

#### Deferred

- ✓ Cross-module integration (file attachment events via FileDeletedEventHandler + ICardAttachmentCleanupService) → completed in Phase 4.6

### Phase 4.5: Web UI (Blazor)

- ✓ Board list page (grid/list, create dialog)
- ✓ Board kanban view (drag-and-drop)
- ✓ Card detail slide-out panel
- ✓ Sprint management (planning, backlog, progress)
- ✓ Sprint Planning Workflow UX:
  - ✓ Sprint selector in card detail panel
  - ✓ Sprint backlog view (expandable card list per sprint)
  - ✓ Quick-add cards to sprint (card picker dialog)
  - ✓ Sprint filter on kanban board
  - ✓ Sprint badge on kanban cards
  - ✓ Sprint Planning View (side-by-side backlog/sprint, capacity bar, member workload)
  - ✓ Burndown chart (SVG-based SprintBurndownChart.razor)
  - ✓ Velocity chart (SVG-based VelocityChart.razor)
  - ✓ Sprint completion dialog (summary, incomplete card handling)
  - ✓ Sprint report API client methods (GetSprintReportAsync, GetBoardVelocityAsync)
- ✓ Board settings (members, labels, archive)
- ✓ Team management (create/edit teams, roles, members)
- ✓ Filters and search
- ✓ Real-time SignalR updates (Blazor ITracksSignalRService event subscriptions, completed in Phase 4.6)
- ✓ Responsive layout
- ✓ CSS consistent with theme
- ✓ ITracksApiClient / TracksApiClient HTTP service
- ✓ Module UI registration + DI setup
- ✓ tracks-kanban.js drag-drop JS interop
- ✓ Hierarchy level indicator banner on kanban board (Product/Epic/Feature/Item level labels)
- ✓ Card type badges (Epic/Feature/Item/SubItem) on kanban cards
- ✓ Depth-based visual styling for hierarchy levels (color-coded column borders)
- ✓ Product Creation Wizard (multi-step: Name → Color/Settings → Members → Review)
- ✓ WorkItem Creation Wizard (multi-step: Type/Title → Details → Assignments → Review)
- ✓ Context-aware type pre-selection in wizard based on current hierarchy level

### Phase 4.6: Real-time And Notifications

- ✓ TracksRealtimeService — IRealtimeBroadcaster delegation, board/team group broadcast
- ✓ TracksRealtimeEventHandler — 12 event types (card/board/sprint/team lifecycle)
- ✓ TracksNotificationService — Card assignment, sprint, team membership notifications via INotificationService
- ✓ ITracksSignalRService + NullTracksSignalRService — Blazor component event interface
- ✓ MentionParser — GeneratedRegex @username extraction with IUserDirectory resolution
- ✓ FileDeletedEventHandler + ICardAttachmentCleanupService — Cross-module file cleanup
- ✓ TracksPage.razor.cs — Real-time event subscriptions (card, list, comment, sprint, member actions)
- ✓ TracksModule.cs — Full event handler registration (13 event subscriptions in InitializeAsync)
- ✓ TracksServiceRegistration — DI for realtime, notification, SignalR, cleanup services
- ✓ 39 new unit tests (238 total Tracks tests)

### Phase 4.7: Advanced Features

- ✓ Board templates (Kanban, Scrum, Bug Tracking, Personal TODO) — `BoardTemplateService`, `BoardTemplatesController`, seeded on startup
- ✓ Card templates — `CardTemplateService`, `CardTemplatesController`
- ✓ Due date reminders (background service) — `DueDateReminderService` (IHostedService)
- ✓ Board analytics (cycle time, workload) — `AnalyticsService.GetBoardAnalyticsAsync`
- ✓ Team analytics — `AnalyticsService.GetTeamAnalyticsAsync`
- ✓ Sprint reports (velocity, burndown data) — `SprintReportService`
- ✓ Bulk operations (multi-select cards) — `BulkOperationService` (move/assign/label/archive), `BulkOperationsController`
- ✓ Poker gRPC RPCs — StartPokerSession, SubmitPokerVote, RevealPokerSession, AcceptPokerEstimate (deferred from 4.4)
- ✓ Unit tests — 92 new tests; 291 total Tracks tests passing

### Phase 4.8: Testing, Documentation And Release

#### Unit Tests

- ✓ Service coverage (all 11 services)
- ✓ Authorization tests
- ✓ Dependency cycle detection tests

#### Integration Tests

- ✓ REST API endpoint tests
- ✓ gRPC service tests

#### Security Tests

- ✓ Board role authorization
- ✓ Tenant isolation
- ✓ Markdown XSS prevention

#### Performance

- ✓ Large board (1000+ cards)
- ✓ Reorder operations

#### Documentation

- ✓ Admin docs (module config, permissions)
- ✓ User guide (boards, cards, sprints, time tracking)
- ✓ API documentation (all endpoints)
- ✓ README roadmap status update

### Phase 4.9: Dual-Mode Rework (Personal + Team)

> Detailed plan: `docs/TRACKS_DUAL_MODE_REWORK_PLAN.md`

#### Phase A: Data Model & Mode System

- ✓ `BoardMode` enum (Personal, Team)
- ✓ `Mode` property on Board entity (default Personal)
- ✓ Sprint planning fields (`DurationWeeks`, `PlannedOrder`)
- ✓ `ReviewSession` entity
- ✓ `ReviewSessionParticipant` entity
- ✓ `PokerSession.ReviewSessionId` FK
- ✓ `ReviewSessionStatus` enum
- ✓ EF configuration & DbSets

#### Phase B: Service Layer — Mode & Sprint Planning

- ✓ Mode-aware `BoardService` guards (`EnsureTeamModeAsync`)
- ✓ `SprintPlanningService` (year plan, adjust, cascade)
- ✓ Backlog service additions (sprint filter on `ListCards`)
- ✓ `ReviewSessionService` (start/join/leave/setCard/poker/end)
- ✓ `PokerService` vote status method

#### Phase C: API Layer Changes

- ✓ Board mode parameter on `POST /api/v1/boards`
- ✓ Sprint wizard endpoints (plan CRUD, adjust)
- ✓ Backlog endpoints (sprint filter)
- ✓ `ReviewSessionController` (8 endpoints)
- ✓ Poker vote status endpoint
- ✓ gRPC proto updates

#### Phase D: Real-Time / SignalR

- ✓ Review session SignalR broadcasts
- ✓ Client-side SignalR events for review

#### Phase E: UI — Personal Mode Simplification

- ✓ Board creation dialog with mode selection (Personal/Team toggle)
- ✓ Mode badge on board cards in list view
- ✓ Conditional sidebar in TracksPage (hide sprints/planning for Personal)
- ✓ Sprint panel hidden for Personal boards
- ✓ Sprint filter hidden on KanbanBoard for Personal boards
- ✓ Sprint badge hidden on cards for Personal boards
- ✓ 35 comprehensive Phase E tests

#### Phase F: UI — Sprint Planning Wizard

- ✓ Multi-step wizard component
- ✓ Wizard view in TracksPage
- ✓ 61 comprehensive Phase F tests

#### Phase G: UI — Backlog & Sprint Views

- ✓ Backlog View component (BacklogView.razor + code-behind)
- ✓ Sprint-filtered Kanban view (sprint tabs in KanbanBoard.razor)
- ✓ Backlog view in TracksPage (enum, sidebar nav, mode guard)
- ✓ 47 comprehensive Phase G tests

#### Phase H: UI — Year Timeline / Gantt View

- ✓ Timeline View component
- ✓ Timeline view in TracksPage
- ✓ 44 comprehensive Phase H tests

#### Phase I: UI — Live Review Mode

- ✓ Review Session Host Controls
- ✓ Review Session Participant View
- ✓ Review Session entry in TracksPage
- ✓ 54 comprehensive Phase I tests

#### Phase J: Tests

- ✓ Data model & entity validation tests (7 tests)
- ✓ Mode-aware service tests (7 tests)
- ✓ Sprint planning wizard edge case tests (7 tests)
- ✓ Review session edge case tests (8 tests)
- ✓ Poker vote status tests (4 tests)
- ✓ Controller integration tests (3 tests)
- ✓ Security tests (15 tests)
- ✓ Performance tests (5 tests)
- ✓ Additional integration tests (3 tests)
- ✓ 62 new tests in `PhaseJ_ComprehensiveTests.cs`; 801 total Tracks tests passing

### Phase 4.10: Hierarchy Expansion Rewrite (docs/TRACKS_HIERARCHY_EXPANSION.md)

- ✓ Unified WorkItem entity with type discriminator (Epic/Feature/Item/SubItem), replacing separate Board/Card models
- ✓ Six-level hierarchy: Organization → Product → Epic → Feature → Item → SubItem with self-referencing ParentWorkItemId
- ✓ Polymorphic Swimlane with ContainerType/ContainerId (Product or WorkItem), gap-based positioning (gap=1000, spacing=1024)
- ✓ Product entity replaces Board; ProductMember replaces BoardMember; Label ownership stays at Product level
- ✓ New services: ProductService, WorkItemService, SwimlaneService, SprintService, SprintPlanningService, AnalyticsService, + 10 others
- ✓ New DTOs: ProductDto, WorkItemDto, SwimlaneDto, SprintDto, etc. replacing BoardDto/CardDto/BoardSwimlaneDto
- ✓ New events: ProductCreatedEvent, WorkItemCreatedEvent, WorkItemMovedEvent, etc. replacing Board/Card event types
- ✓ Cross-module event wiring updated in Chat module (TracksActivityChatHandler, ChatModule, manifests)
- ✓ Realtime services updated: ITracksSignalRService, ITracksRealtimeService, TracksInProcessSignalRService, TracksRealtimeService
- ✓ Tracks.Host gRPC service adapted to new service layer
- ✓ UI compiled and adapted — KanbanBoard, WorkItemDetailPanel, TracksPage, ProductListView, SprintPanel, etc.
- ✓ Core.Tests, Chat.Tests, Integration.Tests updated for new DTOs/events (0 errors)
- ☐ Tracks.Tests excluded from CI build — 248 errors, needs full rewrite for new service/controller layer
- ✓ All source and retained test projects build with 0 errors (DotNetCloud.CI.slnf)

---

## Phase 5: Media (Photos, Music, Video)

### Sub-Phase A: Shared Media Infrastructure (Steps 5.1–5.2)

#### Step 5.1 — Media Streaming Middleware & Shared Types

- ✓ `MediaType` enum (Photo, Audio, Video) in `DotNetCloud.Core/DTOs/Media/MediaType.cs`
- ✓ `GeoCoordinate` record in `DotNetCloud.Core/DTOs/Media/GeoCoordinate.cs`
- ✓ `MediaMetadataDto` record in `DotNetCloud.Core/DTOs/Media/MediaMetadataDto.cs`
- ✓ `MediaItemDto` record in `DotNetCloud.Core/DTOs/Media/MediaItemDto.cs`
- ✓ `MediaThumbnailDto` record and `MediaThumbnailSize` enum in `DotNetCloud.Core/DTOs/Media/MediaThumbnailDto.cs`
- ✓ `IMediaStreamingService` interface in `DotNetCloud.Core/Capabilities/IMediaStreamingService.cs`
- ✓ `IMediaMetadataExtractor` interface in `DotNetCloud.Core/Capabilities/IMediaMetadataExtractor.cs`
- ✓ `MediaStreamingMiddleware` with HTTP Range-request support (206 Partial Content) in `Core.ServiceDefaults/Middleware/`
- ✓ Unit tests: 19 middleware tests + 26 DTO/capability tests

#### Step 5.2 — Metadata Extraction Framework

- ✓ `ExifMetadataExtractor` (ImageSharp 3.x TryGetValue API) in `Core.ServiceDefaults/Media/`
- ✓ `AudioMetadataExtractor` (TagLibSharp 2.3.0) in `Core.ServiceDefaults/Media/`
- ✓ `VideoMetadataExtractor` (FFprobe JSON parsing) in `Core.ServiceDefaults/Media/`
- ✓ `MediaServiceCollectionExtensions` DI registration (keyed services by MediaType) in `Core.ServiceDefaults/Media/`
- ✓ NuGet: `TagLibSharp 2.3.0` and `SixLabors.ImageSharp 3.1.12` in ServiceDefaults.csproj
- ✓ Unit tests: 12 EXIF + 10 audio + 9 video + 7 DI registration tests
- ✓ All 136 new tests passing (396 total)

### Sub-Phase B: Photos Module (Steps 5.3–5.7)

- ✓ Step 5.3 — Photos Architecture & Contracts
- ✓ Step 5.4 — Photos Data Model & Migrations
- ✓ Step 5.5 — Photos Core Services
- ✓ Step 5.6 — Photo Editing & Slideshow
- ✓ Step 5.7 — Photos API & Web UI

### Sub-Phase C: Music Module (Steps 5.8–5.14)

- ✓ Step 5.8 — Music Architecture & Contracts
- ✓ Step 5.9 — Music Data Model & Migrations
- ✓ Step 5.10 — Music Library Scanning
- ✓ Step 5.11 — Music Core Services
- ✓ Step 5.12 — Music Streaming & Equalizer
- ✓ Step 5.13 — Subsonic API Compatibility
- ✓ Step 5.14 — Music API, gRPC & Blazor UI

### Sub-Phase C.1: MusicBrainz Metadata Enrichment

#### Phase A — Data Model Changes (Migration)
- ✓ Add MusicBrainz enrichment fields to Artist model (MusicBrainzId, Biography, ImageUrl, WikipediaUrl, DiscogsUrl, OfficialUrl, LastEnrichedAt)
- ✓ Add MusicBrainz enrichment fields to MusicAlbum model (MusicBrainzReleaseGroupId, MusicBrainzReleaseId, LastEnrichedAt)
- ✓ Add MusicBrainz enrichment fields to Track model (MusicBrainzRecordingId, LastEnrichedAt)
- ✓ Update EF Core configurations with max lengths and indexes
- ✓ Create AddMusicBrainzEnrichment migration

#### Phase B — MusicBrainz + Cover Art Archive Services
- ✓ `IMusicBrainzClient` / `MusicBrainzClient` — typed HTTP client with rate limiting
- ✓ `ICoverArtArchiveClient` / `CoverArtArchiveClient` — album art fetcher
- ✓ `IMetadataEnrichmentService` / `MetadataEnrichmentService` — orchestrator

#### Phase C — Scan Progress Infrastructure
- ✓ `LibraryScanProgress` DTO
- ✓ Update `LibraryScanService` with progress reporting
- ✓ `ScanProgressState` — scoped Blazor state service
- ✓ Post-scan enrichment background worker with shared per-user progress/cancellation tracking and remaining cover-art lookup counts

#### Phase D — API Endpoints
- ✓ Enrichment endpoints on MusicController
- ✓ Scan progress endpoint

#### Phase E — Blazor UI Updates
- ✓ Scan progress UI overhaul
- ✓ Album enrichment UI
- ✓ Artist enrichment UI
- ✓ Settings: enrichment toggles
- ✓ Settings scan panel keeps showing background enrichment progress after navigation and displays remaining album-art lookups
- ✓ Artist grid pager toolbar — improved layout, button styling, page info text formatting

#### Phase F — Service Registration + Configuration

#### Phase F — Service Registration + Configuration
- ✓ Register new services and HTTP clients
- ✓ Configuration section for enrichment settings

#### Phase G — Comprehensive Unit Tests
- ✓ `MusicBrainzClientTests` (23 tests)
- ✓ `CoverArtArchiveClientTests` (15 tests)
- ✓ `MetadataEnrichmentServiceTests` (30 tests)
- ✓ `LibraryScanProgressTests` (12 tests)
- ✓ `ScanProgressStateTests` (8 tests)
- ✓ `MockHttpMessageHandler` shared test infrastructure
- ✓ `TestHelpers` updated with enrichment seeding helpers

### Sub-Phase D: Video Module (Steps 5.15–5.18)

- ✓ Step 5.15 — Video Contracts & Data Model
- ✓ Step 5.16 — Video Core Services (74 tests passing)
- ✓ Step 5.17 — Video Streaming & API
- ✓ Step 5.18 — Video Web UI

### Sub-Phase E: Integration & Quality (Steps 5.19–5.20)

- ✓ Step 5.19 — Cross-Module Integration
  - ✓ `FileUploadedPhotoHandler` with `IPhotoIndexingCallback` (9 image MIME types)
  - ✓ `FileUploadedMusicHandler` with `IMusicIndexingCallback` (15 audio MIME types)
  - ✓ `FileUploadedVideoHandler` with `IVideoIndexingCallback` (12 video MIME types)
  - ✓ `IMediaSearchService` + `MediaSearchResultDto` (cross-module search)
  - ✓ Notification handlers: `AlbumSharedNotificationHandler`, `PlaylistSharedNotificationHandler`, `VideoSharedNotificationHandler`
  - ✓ Dashboard DTOs: `MediaDashboardDto`, `VideoContinueWatchingDto`, `RecentMediaItemDto`
  - ✓ 8 new `CrossModuleLinkType` values (Photo, PhotoAlbum, MusicTrack, MusicAlbum, MusicArtist, Playlist, Video, VideoCollection)
  - ✓ Callback implementations: `PhotoIndexingCallback`, `MusicIndexingCallback`, `VideoIndexingCallback`
  - ✓ `VideoService.CreateVideoAsync` with duplicate detection and event publishing
- ✓ Step 5.20 — Testing & Documentation (test suites complete)
  - ✓ Photos: 119 tests (12 handler + 6 notification + 6 callback = 24 new)
  - ✓ Music: 156 tests (12 handler + 9 notification + 4 callback = 25 new)
  - ✓ Video: 105 tests (12 handler + 9 notification + 10 service + 5 callback = 31 new, replaced 3 basic)
  - ✓ Core: 410 tests (16 new cross-module DTO tests)
  - ✓ Align Photos, Music, and Video collapsed sidebars with the Tracks-style pattern, including layout shrink behavior and persisted collapse state for Video
  - ☐ Security tests, performance tests, admin/user docs — deferred

---

## Phase 9: AI Assistant

### Step 9.1 — Core AI Interfaces & Module Scaffold

- ✓ `ILlmProvider` capability interface in `DotNetCloud.Core/Capabilities/`
- ✓ Core DTOs: `LlmRequest`, `LlmResponse`, `LlmResponseChunk`, `LlmModelInfo`, `LlmMessage` in `DotNetCloud.Core/AI/`
- ✓ `AiModule` (IModuleLifecycle) + `AiModuleManifest` (IModuleManifest)
- ✓ Models: `Conversation`, `ConversationMessage`
- ✓ Events: `ConversationCreatedEvent`, `ConversationMessageEvent`, `ConversationCreatedEventHandler`
- ✓ Service interfaces: `IAiChatService`, `IOllamaClient`
- ✓ `manifest.json` for AI module

### Step 9.2 — Data Layer & Ollama Provider

- ✓ `AiDbContext` with EF Core (Conversation + ConversationMessage entities)
- ✓ Entity configurations: `ConversationConfiguration`, `ConversationMessageConfiguration`
- ✓ `OllamaClient` — HTTP client for Ollama REST API (chat, streaming, model listing, health)
- ✓ `AiChatService` — Conversation management, message persistence, LLM routing
- ✓ `AiServiceRegistration` — DI setup with configurable Ollama base URL
- ✓ `IAiSettingsProvider` / `AiSettingsProvider` — DB-backed settings with IConfiguration fallback

### Step 9.3 — Module Host & REST API

- ✓ `DotNetCloud.Modules.AI.Host` — Standalone web host (Program.cs)
- ✓ `AiChatController` — REST API: conversations CRUD, send message, streaming SSE, model listing
- ✓ `AiHealthCheck` — Ollama connectivity health check
- ✓ `InProcessEventBus` — Standalone event bus for module isolation
- ✓ `appsettings.json` configured for Ollama (default `http://localhost:11434/`), default model `gpt-oss:20b`

### Step 9.4 — Unit Tests

- ✓ `AiModuleTests` — Module lifecycle (7 tests)
- ✓ `AiChatServiceTests` — Conversation CRUD, message sending, model listing (11 tests)
- ✓ `OllamaClientTests` — HTTP client with mocked handler (7 tests + 3 additional)
- ✓ All 28 tests passing

### Step 9.5 — Blazor UI Chat Panel (Pending)

- ☐ Chat-style AI assistant panel component
- ☐ Streaming response rendering via SignalR or SSE
- ☐ Model selector dropdown
- ☐ Conversation history sidebar

### Step 9.6 — Admin Settings & Multi-Provider Support

- ✓ `AiAdminSettingsViewModel` — Settings model (Provider, ApiBaseUrl, ApiKey, OrgId, DefaultModel, MaxTokens, Timeout)
- ✓ `AiAdminSettings.razor` / `.razor.cs` — Blazor admin settings page with provider-aware UI
- ✓ `IAiSettingsProvider` / `AiSettingsProvider` — DB-backed settings (SystemSettings table) with IConfiguration fallback
- ✓ `OllamaClient` uses dynamic base URL from `IAiSettingsProvider` (no restart needed)
- ✓ `AiChatController` uses `IAiSettingsProvider` for default model
- ✓ Fix system settings persistence under global no-tracking EF configuration so `/admin/settings` updates and deletes save correctly in production
- ✓ DB seed: 7 AI settings in `DbInitializer` (Provider, ApiBaseUrl, ApiKey, OrgId, DefaultModel, MaxTokens, RequestTimeoutSeconds)
- ✓ `DbInitializer` upgraded to backfill missing settings on existing databases
- ✓ Provider selection: Ollama (local), OpenAI, Anthropic
- ✓ Auth fields shown/hidden based on provider (Ollama = no key needed, cloud = key required)
- ☐ Full OpenAI-compatible request routing (header auth, different API paths)
- ☐ Full Anthropic-compatible request routing
- ☐ Per-user API key storage (encrypted)
- ☐ Rate limiting per user

### Step 9.7 — Module Integration (Pending)

- ☐ Notes module: summarize, expand, translate, grammar check
- ☐ Chat module: message summarization, smart replies
- ☐ Files module: content summarization, document Q&A

---

## Phase 6: Email & Bookmarks

### Phase 6.1 — Skeletons + Contracts ✅

#### Step 6.1.1 — NuGet Packages
- ✓ MailKit 4.16.0, Google.Apis.Gmail.v1 1.73.0.3987, Google.Apis.Auth 1.73.0, AngleSharp 0.17.1
- ✓ All packages added to Directory.Packages.props

#### Step 6.1.2 — Bookmarks Module Projects
- ✓ `DotNetCloud.Modules.Bookmarks/` — Core project (SDK=Razor, net10.0)
- ✓ `DotNetCloud.Modules.Bookmarks.Data/` — Data project (SDK=default, net10.0)
- ✓ `DotNetCloud.Modules.Bookmarks.Host/` — Host project (SDK=Web, net10.0)
- ✓ BookmarksModuleManifest.cs — Id="dotnetcloud.bookmarks"
- ✓ BookmarksModule.cs — IModuleLifecycle
- ✓ Models: BookmarkFolder, BookmarkItem, BookmarkPreview
- ✓ Events: BookmarkCreatedEvent, BookmarkUpdatedEvent, BookmarkDeletedEvent
- ✓ Services: IBookmarkService, IBookmarkFolderService, IBookmarkImportExportService, IBookmarkPreviewService
- ✓ UI: _Imports.razor, BookmarksPage.razor (minimal shell)
- ✓ manifest.json

#### Step 6.1.3 — Email Module Projects
- ✓ `DotNetCloud.Modules.Email/` — Core project (SDK=Razor, net10.0)
- ✓ `DotNetCloud.Modules.Email.Data/` — Data project (SDK=default, net10.0)
- ✓ `DotNetCloud.Modules.Email.Host/` — Host project (SDK=Web, net10.0)
- ✓ EmailModuleManifest.cs — Id="dotnetcloud.email"
- ✓ EmailModule.cs — IModuleLifecycle
- ✓ Models: EmailAccount, EmailMailbox, EmailThread, EmailMessage, EmailAttachment, EmailRule, EmailRuleConditionGroup, EmailRuleCondition, EmailRuleAction
- ✓ Events: 6 event types (AccountAdded/Removed, ThreadCreated, MessageReceived, Sent, RuleTriggered)
- ✓ Services: IEmailAccountService, IEmailProvider, IEmailRuleService, IEmailSyncService, IEmailSendService
- ✓ EmailCredentialEncryptionService — IDataProtector with per-user sub-purpose
- ✓ UI: _Imports.razor, EmailPage.razor

#### Step 6.1.4 — Infrastructure Wiring
- ✓ Both modules added to DotNetCloud.sln and DotNetCloud.CI.slnf
- ✓ Core.Server/Program.cs — DbContext registrations, service registrations, Blazor assembly references
- ✓ Core.Server.csproj — Project references for all 6 new projects
- ✓ ErrorCodes.cs — 18 new error codes (Bookmarks + Email)
- ✓ Build: 0 errors

### Phase 6.2 — Bookmarks CRUD ✅

#### Step 6.2.1 — Service Implementation
- ✓ BookmarkService — Full CRUD with soft-delete, URL normalization, search event publishing
- ✓ BookmarkFolderService — Full CRUD with cascade soft-delete
- ✓ BookmarkImportExportService — HTML import (Netscape format) and export with AngleSharp

#### Step 6.2.2 — REST API
- ✓ BookmarksController — List/Get/Create/Update/Delete/Search for bookmarks and folders
- ✓ Import/Export endpoints
- ✓ Preview endpoints (Fetch/Get)
- ✓ Envelope response format following existing module patterns

#### Step 6.2.3 — Search Integration
- ✓ SearchIndexRequestEvent published after Create (Index), Update (Index), Delete (Remove)
- ✓ BookmarksSearchableModule — ISearchableModule for Bookmark entities

#### Step 6.2.4 — Database
- ✓ EF Core migration: InitialCreate generated for BookmarksDbContext

### Phase 6.3 — Bookmarks Rich Previews ✅

#### Step 6.3.1 — SafeUrlFetcher
- ✓ SSRF-safe HTTP pipeline: scheme validation, IP blocklist, DNS re-validation
- ✓ Custom ConnectCallback for socket-level IP validation
- ✓ Manual redirect loop (max 5), each redirect re-validated
- ✓ Timeouts: Connect=5s, Overall=15s
- ✓ Response size limit: 1 MB
- ✓ Content-Type whitelist: text/html, application/xhtml+xml

#### Step 6.3.2 — BookmarkPreviewFetchService
- ✓ AngleSharp HTML parsing with OG/Twitter card extraction
- ✓ Metadata priority: OG → Twitter → standard HTML elements
- ✓ Favicon, preview image, canonical URL resolution
- ✓ Auto-update bookmark title if user hasn't set custom one
- ✓ Stale preview refresh: background batch refresh (7+ days old, 50 per batch)

### Phase 6.4 — Email Account Management ✅
- ✓ EmailAccount CRUD with credential encryption (EmailAccountService + EmailCredentialEncryptionService)
- ✓ ImapSmtpEmailProvider — IMAP/SMTP operations via MailKit
- ✓ GmailEmailProvider — Gmail API operations via Google.Apis.Gmail.v1
- ✓ Gmail OAuth start/complete endpoints (configuration-based)
- ✓ EF Core migration: InitialCreate generated for EmailDbContext
- ✓ Account management UI (Blazor) — Add IMAP/SMTP and Gmail OAuth forms in EmailPage.razor

### Phase 6.5 — Email Sync Ingest ✅
- ✓ IEmailProvider abstraction with ImapSmtpEmailProvider and GmailEmailProvider
- ✓ EmailSyncBackgroundService with PeriodicTimer (5-min interval)
- ✓ Mailbox discovery, sync status tracking, provider-to-DB upsert
- ✓ Full MIME message normalization to EmailMessage/EmailThread entities — Thread building via Message-Id/In-Reply-To/References headers, body preview extraction with MIME part traversal, attachment records, deleted message detection, sync watermark tracking in both ImapSmtpEmailProvider + GmailEmailProvider
- ✓ Thread UI (Blazor) — Thread list and thread detail with message display in EmailPage.razor

### Phase 6.6 — Send/Compose ✅
- ✓ SMTP send via ImapSmtpEmailProvider (MailKit SmtpClient)
- ✓ Gmail send via GmailEmailProvider (Gmail API)
- ✓ EmailSendService — resolves provider, creates sent-message record, publishes EmailSentEvent
- ✓ Compose UI with contact autocomplete (Blazor) — EmailComposeForm with recipient parsing, subject, body in EmailPage.razor

### Phase 6.7 — Rules/Filters ✅
- ✓ Email rule evaluation engine (priority-ordered, All/Any condition match, stop-processing flag)
- ✓ Rules CRUD API + nested condition groups/conditions/actions
- ✓ Manual rule run (RunRulesAsync) against unread messages
- ✓ Provider action application (mark read, move, label, archive, star)
- ✓ Rule editor UI (Blazor) — EditRuleForm with condition group builder and action selector in EmailPage.razor

### Phase 6.8 — Search Integration ✅
- ✓ ISearchableModule for Email (EmailSearchableModule — EmailThread entities)
- ✓ ISearchableModule for Bookmarks (BookmarksSearchableModule — Bookmark entities)
- ✓ SearchIndexRequestEvent published on bookmark CRUD
- ✓ SearchIndexRequestEvent published on email thread creation/update during sync (both providers)
- ✓ EmailSentEvent + EmailRuleTriggeredEvent published for search indexing
- ✓ Cross-module search quality verification — Both ISearchableModule implementations verified, search index events fire from all CRUD/sync paths, IEventBus wired into both email providers

---

## Phase 8: Full-Text Search Module (from FULL_TEXT_SEARCH_IMPLEMENTATION_PLAN.md)

### Phase 2: Search Module Scaffold ✅

#### Step 2.1 — Project Structure
- ✓ `DotNetCloud.Modules.Search/` — Business logic project (.csproj, services, extractors, events)
- ✓ `DotNetCloud.Modules.Search.Data/` — EF Core data project (.csproj, models, configurations, DbContext)
- ✓ `DotNetCloud.Modules.Search.Host/` — gRPC host + REST controllers (.csproj, Program.cs, proto, controllers)
- ✓ All 3 projects added to solution

#### Step 2.2 — SearchDbContext & Index Table Model
- ✓ `SearchIndexEntry` entity (Id, ModuleId, EntityId, EntityType, Title, Content, Summary, OwnerId, OrganizationId, CreatedAt, UpdatedAt, IndexedAt, MetadataJson)
- ✓ `IndexingJob` entity (Id, ModuleId, Type, Status, StartedAt, CompletedAt, DocumentsProcessed, DocumentsTotal, ErrorMessage)
- ✓ `SearchIndexEntryConfiguration` — Composite unique index, owner/org/module/type/date indexes
- ✓ `IndexingJobConfiguration` — Status/Type as string conversion, status/module indexes
- ✓ `SearchDbContext` — DbSets for SearchIndexEntries and IndexingJobs

#### Step 2.3 — Provider-Specific ISearchProvider Implementations
- ✓ `PostgreSqlSearchProvider` — ILIKE fallback (native tsvector/tsquery for production PostgreSQL)
- ✓ `SqlServerSearchProvider` — Contains() fallback (native FREETEXT for production SQL Server)
- ✓ `MariaDbSearchProvider` — Contains() fallback (native MATCH AGAINST for production MariaDB)
- ✓ All providers: IndexDocument (upsert), RemoveDocument, Search (with pagination, sorting, facets, permission scoping), ReindexModule, GetIndexStats

#### Step 2.4 — SearchModuleManifest & SearchModule
- ✓ `SearchModuleManifest` — Id: "dotnetcloud.search", Name: "Search", Version: "1.0.0"
- ✓ `SearchModule` — IModuleLifecycle implementation with Initialize/Start/Stop/Dispose
- ✓ Event subscription: SearchIndexRequestEvent handler registered on init

#### Step 2.5 — Services
- ✓ `SearchQueryService` — Query execution wrapper with empty-query short-circuit, stats, reindex delegation
- ✓ `ContentExtractionService` — Orchestrates IContentExtractor instances, MIME type selection, content truncation (100KB max)
- ✓ `SearchIndexingService` — Channel<T>-based background queue (capacity 1000), processes index/remove events
- ✓ `SearchReindexBackgroundService` — BackgroundService, 24h interval, creates IndexingJob records

#### Step 2.6 — Content Extractors
- ✓ `PlainTextExtractor` — text/plain, text/csv
- ✓ `MarkdownContentExtractor` — text/markdown with regex-based syntax stripping
- ✓ `PdfContentExtractor` — application/pdf via PdfPig
- ✓ `DocxContentExtractor` — DOCX via DocumentFormat.OpenXml
- ✓ `XlsxContentExtractor` — XLSX via DocumentFormat.OpenXml (shared string table resolution)

#### Step 2.7 — Event Handler
- ✓ `SearchIndexRequestEventHandler` — Handles SearchIndexRequestEvent (Remove action via ISearchProvider)

#### Step 2.8 — gRPC & REST
- ✓ `search_service.proto` — 5 RPCs: Search, IndexDocument, RemoveDocument, ReindexModule, GetIndexStats
- ✓ `SearchGrpcService` — gRPC service implementation delegating to SearchQueryService/ISearchProvider
- ✓ `SearchControllerBase` — Base controller with auth, CallerContext, envelope helpers
- ✓ `SearchController` — REST endpoints: GET /search, GET /suggest, GET /stats, POST /admin/reindex, POST /admin/reindex/{moduleId}
- ✓ `InProcessEventBus` — Standalone event bus for module isolation

#### Step 2.9 — Host Program.cs
- ✓ Service registration (module, DbContext, event bus, providers, extractors, services, gRPC, REST, health checks)
- ✓ Middleware pipeline (gRPC, controllers, health, info endpoint)

#### Step 2.10 — Comprehensive Tests
- ✓ `DotNetCloud.Modules.Search.Tests` project (MSTest 4.1.0 + Moq 4.20.72, InMemory EF Core)
- ✓ `SqlServerSearchProviderTests` — Index, upsert, remove, search (text match, pagination, sort, facets, permission scoping, metadata), reindex, stats (32 tests)
- ✓ `MariaDbSearchProviderTests` — Index, upsert, remove, search (title match, content match, permission scoping, facets), reindex, stats (10 tests)
- ✓ `SearchQueryServiceTests` — Empty query, valid query delegation, null query, stats, reindex (5 tests)
- ✓ `ContentExtractionServiceTests` — Supported/unsupported MIME types, null handling, extractor errors, truncation, CanExtract (10 tests)
- ✓ `PlainTextExtractorTests` — CanExtract (text/plain, text/csv, case-insensitive), extract text, CSV, empty, unicode (9 tests)
- ✓ `MarkdownContentExtractorTests` — CanExtract, strip headings/bold/italic/links/images/code blocks/inline code/blockquotes/lists/strikethrough/horizontal rules, metadata, StripMarkdown edge cases (17 tests)
- ✓ `SearchIndexingServiceTests` — Enqueue, process remove, process index, unknown module, document not found, pending count, dispose, stop without start (8 tests)
- ✓ `SearchIndexRequestEventHandlerTests` — Remove action, index action, null provider (3 tests)
- ✓ `SearchModuleTests` — Manifest properties, lifecycle (init/start/stop/dispose), event bus subscription/unsubscription, initial state, null context (10 tests)
- ✓ `SearchModuleManifestTests` — All manifest properties and counts (9 tests)
- ✓ `SearchDbContextTests` — CRUD for SearchIndexEntry and IndexingJob, all fields persisted, nullable fields, status transitions, auto-generated IDs (9 tests)
- ✓ All 116 tests passing

### Phase 3: Module Search API Integration ✅

#### Step 3.1 — Search RPCs Added to Module Protos
- ✓ `files_service.proto` — GetSearchableDocuments, GetSearchableDocument, SearchableDocument
- ✓ `chat_service.proto` — GetSearchableDocuments, GetSearchableDocument, SearchableDocument
- ✓ `notes_service.proto` — GetSearchableDocuments, GetSearchableDocument, SearchableDocument
- ✓ `contacts_service.proto` — GetSearchableDocuments, GetSearchableDocument, SearchableDocument
- ✓ `calendar_service.proto` — GetSearchableDocuments, GetSearchableDocument, SearchableDocument
- ✓ `photos_service.proto` — GetSearchableDocuments, GetSearchableDocument, SearchableDocument
- ✓ `music_service.proto` — GetSearchableDocuments, GetSearchableDocument, MusicSearchableDocument
- ✓ `video_service.proto` — GetSearchableDocuments, GetSearchableDocument, SearchableDocument
- ✓ `tracks_service.proto` — GetSearchableDocuments, GetSearchableDocument, SearchableDocument

#### Step 3.2 — gRPC Service Implementations for Search RPCs
- ✓ `FilesGrpcService` — Maps FileNode entities to SearchableDocument
- ✓ `ChatGrpcService` — Maps Message entities to SearchableDocument
- ✓ `NotesGrpcService` — Maps Note entities to SearchableDocument
- ✓ `ContactsGrpcService` — Maps Contact entities to SearchableDocument
- ✓ `CalendarGrpcService` — Maps CalendarEvent entities to SearchableDocument
- ✓ `PhotosGrpcServiceImpl` — Maps Photo entities to SearchableDocument
- ✓ `MusicGrpcServiceImpl` — Maps Track/Artist/Album to MusicSearchableDocument
- ✓ `VideoGrpcServiceImpl` — Maps Video entities to SearchableDocument
- ✓ `TracksGrpcService` — Maps Card/Board/Label entities to SearchableDocument

#### Step 3.3 — SearchIndexRequestEvent Publishing on CRUD
- ✓ `FileService` — CreateFolder (Index), Rename (Index), Move (Index), Delete (Remove)
- ✓ `MessageService` (Chat) — Send (Index), Edit (Index), Delete (Remove)
- ✓ `NoteService` — Create (Index), Update (Index), Delete (Remove)
- ✓ `ContactService` — Create (Index), Update (Index), Delete (Remove)
- ✓ `CalendarEventService` — Create (Index), Update (Index), Delete (Remove)
- ✓ `PhotoService` — Create (Index), Delete (Remove)
- ✓ `VideoService` — Create (Index), Delete (Remove)
- ✓ `CardService` (Tracks) — Create (Index), Update (Index), Move (Index), Delete (Remove)
- ✓ `LibraryScanService` (Music) — IndexFile (Index)
- ✓ `TrackService` (Music) — Delete (Remove), IEventBus injected

#### Step 3.4 — Comprehensive Tests
- ✓ `ContactServiceSearchIndexTests` — 4 tests (create, update, delete, event properties)
- ✓ `CalendarEventServiceSearchIndexTests` — 3 tests (create, update, delete)
- ✓ `MessageServiceSearchIndexTests` — 3 tests (send, edit, delete)
- ✓ `NoteServiceSearchIndexTests` — 3 tests (create, update, delete)
- ✓ `PhotoServiceSearchIndexTests` — 2 tests (create, delete)
- ✓ `VideoServiceSearchIndexTests` — 2 tests (create, delete)
- ✓ `TrackServiceSearchIndexTests` — 2 tests (delete, event properties)
- ✓ `CardServiceSearchIndexTests` — 4 tests (create, update, move, delete)
- ✓ All 23 tests passing, zero regressions

### Phase 4: Indexing Engine ✅

#### Step 4.1 — Background Indexing Pipeline
- ✓ `SearchIndexingService` — Channel-based queue with Start/Stop lifecycle, batch processing
- ✓ Module lookup from `ISearchableModule` registry, null-safe document retrieval
- ✓ Content extraction pipeline integration (`ContentExtractionService`)
- ✓ Error handling — individual failures don't stop the queue

#### Step 4.2 — Search Reindex Background Service
- ✓ `SearchReindexBackgroundService` — Full reindex and per-module reindex
- ✓ Batch processing with configurable size (default 200)
- ✓ `IndexingJob` creation with status tracking (Pending → Running → Completed/Failed)
- ✓ Orphaned entry cleanup for unregistered modules

#### Step 4.3 — Event Handler Integration
- ✓ `SearchIndexRequestEventHandler` — Routes Index events to indexing service, Remove events to provider
- ✓ Null-safe for both provider and indexing service injection

#### Step 4.4 — Comprehensive Tests
- ✓ `SearchIndexingServicePhase4Tests` — 8 tests
- ✓ `SearchIndexRequestEventHandlerPhase4Tests` — 6 tests
- ✓ `SearchReindexBackgroundServicePhase4Tests` — 16 tests
- ✓ `ContentExtractionPipelinePhase4Tests` — 8 tests
- ✓ `IndexingPipelineIntegrationTests` — 5 tests
- ✓ All 43 Phase 4 tests passing (212 total search tests)

### Phase 5: Search Query Engine ✅

#### Step 5.1 — Query Parsing
- ✓ `SearchQueryParser` — Parses raw input into `ParsedSearchQuery`
- ✓ Keywords, quoted phrases, `in:module`, `type:value`, `-exclusion` syntax
- ✓ Edge case handling (empty quotes, standalone dashes)

#### Step 5.2 — Provider-Specific Query Translation
- ✓ `ParsedSearchQuery.ToPostgreSqlTsQuery()` — & operators, <-> phrases, ! exclusions
- ✓ `ParsedSearchQuery.ToSqlServerContainsQuery()` — AND/AND NOT keywords
- ✓ `ParsedSearchQuery.ToMariaDbBooleanQuery()` — +term, +"phrase", -exclusion
- ✓ Special character sanitization per provider

#### Step 5.3 — Cross-Module Result Aggregation
- ✓ `SearchQueryService` — Parser integration, filter extraction from query syntax
- ✓ Short-circuit on empty or filter-only queries
- ✓ All three database providers upgraded with parsed query support

#### Step 5.4 — Snippet Generation
- ✓ `SnippetGenerator.Generate()` — Contextual window with `<mark>` highlighting
- ✓ `SnippetGenerator.HighlightTitle()` — Title term highlighting
- ✓ XSS prevention via HtmlEncode before mark tag insertion

#### Step 5.5 — Provider Upgrades
- ✓ PostgreSQL — ILIKE term matching, exclusion WHERE clauses, relevance scoring
- ✓ SQL Server — Contains() fallback, exclusions, relevance scoring
- ✓ MariaDB — Contains() fallback, exclusions, relevance scoring
- ✓ All providers: title highlighting, snippet generation, metadata deserialization

#### Step 5.6 — Comprehensive Tests
- ✓ `SearchQueryParserTests` — 28 tests
- ✓ `ParsedSearchQueryTests` — 20 tests
- ✓ `SnippetGeneratorTests` — 18 tests
- ✓ `SearchQueryEngineIntegrationTests` — 25 tests
- ✓ `CrossModuleResultAggregationTests` — 20 tests
- ✓ `SearchQueryServicePhase5Tests` — 14 tests
- ✓ All 343 search tests passing

### Phase 6: REST + gRPC API ✅

#### Step 6.1 — REST SearchController
- ✓ `SearchController` — GET /search, GET /suggest, GET /stats, POST /admin/reindex, POST /admin/reindex/{moduleId}
- ✓ Authentication & authorization (admin-only for stats/reindex)
- ✓ Standard envelope response format, CallerContext permission scoping

#### Step 6.2 — gRPC SearchGrpcService
- ✓ `SearchGrpcService` — Search, IndexDocument, RemoveDocument, ReindexModule, GetIndexStats RPCs
- ✓ Delegates to SearchQueryService/ISearchProvider

#### Step 6.3 — Enhanced Per-Module Search Endpoints
- ✓ `DotNetCloud.Modules.Search.Client` project — shared gRPC client library
- ✓ `ISearchFtsClient` interface with IsAvailable + SearchAsync
- ✓ `SearchFtsClient` — lazy GrpcChannel, Unix socket support, timeout config, graceful degradation
- ✓ `SearchFtsClientOptions` — SearchModuleAddress + Timeout configuration
- ✓ `SearchClientServiceExtensions` — AddSearchFtsClient DI registration (IConfiguration or address string)
- ✓ Files controller updated — FTS first, fallback to LIKE
- ✓ Chat controller updated — FTS first, fallback to LIKE
- ✓ Notes controller updated — FTS first, fallback to LIKE

#### Step 6.4 — Comprehensive Tests
- ✓ `SearchControllerTests` — 18 tests (search, suggest, stats, reindex endpoints)
- ✓ `SearchGrpcServiceTests` — 18 tests (all 5 RPCs with various scenarios)
- ✓ `SearchFtsClientTests` — 8 tests (IsAvailable, SearchAsync unavailable, graceful degradation, Dispose)
- ✓ `SearchFtsClientOptionsTests` — 6 tests (defaults, address types, timeout)
- ✓ `SearchClientServiceExtensionsTests` — 5 tests (DI registration, lifecycle, Unix socket)
- ✓ `EnhancedModuleSearchTests` — 15 tests (FTS integration, graceful fallback, permissions, pagination)
- ✓ `Phase6ApiIntegrationTests` — 19 tests (REST + gRPC pipeline, cross-module consistency)
- ✓ All 432 search tests passing (89 Phase 6 + 343 previous)

### Phase 7: Blazor UI ✅

#### Step 7.1 — Global Search Bar Component
- ✓ `GlobalSearchBar.razor` — Modal search overlay with Ctrl+K/Cmd+K keyboard shortcut
- ✓ Debounced input (300ms) → calls `/api/v1/search/suggest` for live suggestions
- ✓ Keyboard navigation (↑↓ Enter Esc), recent searches from localStorage
- ✓ Per-module icons/badges in suggestion results
- ✓ `global-search.js` — JS interop for shortcut registration + localStorage management
- ✓ `GlobalSearchBar.razor.css` — Scoped CSS with animations, responsive breakpoints, dark mode

#### Step 7.2 — Search Results Page
- ✓ `SearchResults.razor` — Full results page at `/search?q=...`
- ✓ Left sidebar facet filters with module counts
- ✓ Sort toggle (Relevance / Date)
- ✓ Pagination with URL state management (`NavigationManager.NavigateTo` with replace)
- ✓ Loading, empty, and error states
- ✓ `SearchResults.razor.css` — Scoped CSS for results layout, facets, pagination

#### Step 7.3 — Per-Module Search Result Renderers
- ✓ `SearchResultCard.razor` — Per-module result card with rich metadata display
- ✓ XSS-safe `SanitizeHighlight()` — only allows `<mark>` tags, HTML-encodes everything else
- ✓ Module-specific metadata rendering for 10 modules (Files, Notes, Chat, Contacts, Calendar, Photos, Music, Video, Tracks, AI)
- ✓ Deep-link URL generation for all modules
- ✓ `FormatDate()` relative time, `FormatFileSize()`, `GetFileTypeLabel()` helpers
- ✓ `SearchResultCard.razor.css` — Scoped CSS with hover effects, metadata tags, responsive

#### Step 7.4 — Integration & API Client
- ✓ `DotNetCloudApiClient` — `SearchAsync()` + `SearchSuggestAsync()` methods added
- ✓ MainLayout integration — `<GlobalSearchBar>` in topbar-center with `InteractiveServer` render mode
- ✓ `_Imports.razor` updated with Search components namespace
- ✓ `App.razor` — `global-search.js` script tag added
- ✓ `app.css` — `.topbar-center` flex layout added

#### Step 7.5 — Comprehensive Tests
- ✓ `SearchResultUrlTests` — 23 tests (deep-link URL generation for all 11 modules, icon/name mapping)
- ✓ `SearchHighlightSanitizerTests` — 16 tests (XSS prevention, mark tag preservation, HTML encoding)
- ✓ `SearchDisplayFormatTests` — 23 tests (relative date formatting, file size formatting, MIME type labels)
- ✓ `SearchQueryUrlBuilderTests` — 27 tests (API URL construction, suggest URL, pagination, page URL, DTO validation)
- ✓ `SearchResultMetadataTests` — 28 tests (per-module metadata extraction for all 10 modules, cross-module consistency)
- ✓ `SearchSortAndEdgeCaseTests` — 42 tests (sort parsing, query clamping, edge cases, facets, relevance/date ordering)
- ✓ All 591 search tests passing (159 Phase 7 + 432 previous)

### Phase 8: Testing & Documentation ✅

#### Step 8.1 — Unit Tests (Permission Scoping)
- ✓ `PermissionScopingTests` — 10 tests (SqlServer/MariaDb user isolation, empty results, facet count scoping, module+user filter, entity type+user filter, pagination, exclusions, stats not scoped, PostgreSQL index/remove only)

#### Step 8.2 — Integration Tests (End-to-End & Multi-Database)
- ✓ `EndToEndIndexingTests` — 12 tests (index event pipeline, remove event, update event, multi-module, full reindex, module reindex, content extraction, entity deleted before processing, orphaned cleanup, query with in:module, exclusion syntax)
- ✓ `MultiDatabaseProviderTests` — 10 tests (SqlServer/MariaDb search consistency, module filter, index+search, remove+search, upsert, stats format, reindex, exclusions, pagination, metadata preservation)

#### Step 8.3 — Performance Benchmarks
- ✓ `PerformanceBenchmarkTests` — 8 tests (index 1000 docs throughput, search 1000 docs latency p50/p95, search 5000 docs with facets, pagination performance, reindex 1000 docs, query parser 10000 parses, snippet generation, concurrent searches 20 parallel)

#### Step 8.4 — Documentation
- ✓ `docs/modules/SEARCH.md` — Module documentation (architecture, features, services, extractors, providers, schema, config, admin, tests)
- ✓ `docs/api/search.md` — API reference (REST endpoints, gRPC RPCs, query syntax, client library, permission model)
- ✓ `docs/architecture/ARCHITECTURE.md` — Section 25: Full-Text Search Architecture
- ✓ Updated `MASTER_PROJECT_PLAN.md` and `IMPLEMENTATION_CHECKLIST.md`
- ✓ All 631 search tests passing (40 Phase 8 + 591 previous)

---

## Phase 7: Video Calling & Screen Sharing

### Phase 7.1 — Architecture & Contracts

#### Enums
- ✓ `VideoCallState` enum (`Ringing`, `Connecting`, `Active`, `OnHold`, `Ended`, `Missed`, `Rejected`, `Failed`)
- ✓ `VideoCallEndReason` enum (`Normal`, `Rejected`, `Missed`, `TimedOut`, `Failed`, `Cancelled`)
- ✓ `CallParticipantRole` enum (`Initiator`, `Participant`)
- ✓ `CallMediaType` enum (`Audio`, `Video`, `ScreenShare`)

#### DTOs
- ✓ `VideoCallDto` — response DTO for video calls
- ✓ `CallParticipantDto` — response DTO for call participants
- ✓ `CallSignalDto` — WebRTC signaling data (SDP offer/answer/ICE)
- ✓ `StartCallRequest` — request DTO for initiating calls
- ✓ `JoinCallRequest` — request DTO for joining calls
- ✓ `CallHistoryDto` — response DTO for call history entries

#### Events
- ✓ `VideoCallInitiatedEvent`
- ✓ `VideoCallAnsweredEvent`
- ✓ `VideoCallEndedEvent`
- ✓ `VideoCallMissedEvent`
- ✓ `ParticipantJoinedCallEvent`
- ✓ `ParticipantLeftCallEvent`
- ✓ `ScreenShareStartedEvent`
- ✓ `ScreenShareEndedEvent`

#### Service Interfaces
- ✓ `IVideoCallService` — call lifecycle management
- ✓ `ICallSignalingService` — WebRTC signaling operations

#### Module Manifest
- ✓ `ChatModuleManifest.cs` — added 8 video call published events

### Phase 7.2 — Data Model & Migration
- ✓ `VideoCall` entity
- ✓ `CallParticipant` entity
- ✓ EF configurations (`VideoCallConfiguration.cs`, `CallParticipantConfiguration.cs`)
- ✓ `ChatDbContext` — add `DbSet<VideoCall>` and `DbSet<CallParticipant>`
- ✓ EF migration: `AddVideoCalling`
- ✓ Soft-delete support on `VideoCall`

### Phase 7.3 — Call Management Service
- ✓ `VideoCallService` implementation
- ✓ Call timeout background task (30s ring timeout)
- ✓ `CallStateValidator` — state machine enforcement
- ✓ Service registration in `ChatServiceRegistration.cs`

### Phase 7.4 — WebRTC Signaling over SignalR
- ✓ Extend `CoreHub.cs` with call signaling methods
- ✓ Call-scoped SignalR groups (`call-{callId}`)
- ✓ `CallSignalingService` implementation
- ✓ Input validation (SDP max 64KB, ICE candidate max 4KB)

### Phase 7.5 — Client-Side WebRTC Engine (JS Interop)
- ✓ `video-call.js` — browser WebRTC API interop
- ✓ P2P mesh topology for 2-3 participants
- ✓ STUN/TURN configuration from server
- ✓ Adaptive bitrate

### Phase 7.6 — Blazor UI Components
- ✓ `VideoCallDialog.razor` — main call window
- ✓ `CallControls.razor` — bottom toolbar
- ✓ `IncomingCallNotification.razor` — incoming call toast
- ✓ `CallHistoryPanel.razor` — call log in channel sidebar
- ✓ Extend `ChannelHeader.razor` with call buttons
- ✓ Scoped CSS for all components

### Phase 7.7 — LiveKit Integration (Optional SFU)
- ✓ `ILiveKitService` interface
- ✓ `LiveKitService` implementation
- ✓ `NullLiveKitService` — graceful degradation
- ✓ Auto-escalation for 4+ participants

### Phase 7.8 — STUN/TURN Configuration
- ✓ `IceServerOptions` configuration class
- ✓ Built-in STUN server (RFC 5389, UDP, dual-stack)
- ✓ `IIceServerService` + `IceServerService` implementation
- ✓ API endpoint: `GET /api/v1/chat/ice-servers`
- ✓ Ephemeral TURN credentials (HMAC-SHA1, coturn-compatible)

### Phase 7.9 — REST API & gRPC Updates
- ✓ REST API endpoints for call lifecycle
- ✓ gRPC service updates to `chat_service.proto`
- ✓ Authorization and rate limiting

### Phase 7.10 — Push Notifications for Calls
- ✓ Incoming call push notification (high-priority)
- ✓ Missed call notification
- ✓ Call-ended notification for disconnected participants
- ✓ Extend `NotificationRouter.cs` — bypass online presence suppression for IncomingCall
- ✓ New notification categories: `IncomingCall`, `MissedCall`, `CallEnded`
- ✓ `CallNotificationEventHandler` event handler with `ICallNotificationHandler` interface
- ✓ DI registration and event bus subscription in `ChatModule`
- ✓ Comprehensive tests (37 tests)

### Phase 7.11 — Testing & Documentation
- ✓ Unit tests (120+ new tests)
- ✓ Integration tests
- ✓ Admin guide and user documentation

---

## Phase 11: Auto-Updates

### Phase A: Core Update Infrastructure (Server-Side)

#### Step 11.1 — IUpdateService Interface & DTOs
- ✓ `IUpdateService` interface (`CheckForUpdateAsync`, `GetLatestReleaseAsync`, `GetRecentReleasesAsync`)
- ✓ `UpdateCheckResult` DTO (IsUpdateAvailable, CurrentVersion, LatestVersion, ReleaseUrl, ReleaseNotes, Assets)
- ✓ `ReleaseInfo` DTO (Version, TagName, ReleaseNotes, PublishedAt, IsPreRelease, Assets)
- ✓ `ReleaseAsset` DTO (Name, DownloadUrl, Size, ContentType, Platform)

#### Step 11.2 — GitHubUpdateService Implementation
- ✓ `GitHubUpdateService` — queries GitHub Releases API with MemoryCache (1-hour TTL)
- ✓ Version comparison logic (semantic version + pre-release)
- ✓ Platform asset matching (parse filenames)
- ✓ DI registration in `SupervisorServiceExtensions`

#### Step 11.3 — Update Check API Endpoint
- ✓ `UpdateController` — `GET /api/v1/core/updates/check`, `/releases`, `/releases/latest`
- ✓ Public endpoints (no auth required)

#### Step 11.4 — CLI `dotnetcloud update` Implementation
- ✓ `dotnetcloud update --check` command (check + display)
- ✓ `dotnetcloud update` command (check + download)

#### Step 11.5 — Admin UI Updates Page
- ✓ `Updates.razor` — admin panel page at `/admin/updates`
- ✓ Current version card, latest release card, update history, settings

#### Step 11.6 — Unit Tests (Server-Side)
- ✓ `GitHubUpdateServiceTests` — mock HTTP, version comparison, caching, asset matching
- ✓ `UpdateControllerTests` — response format, edge cases

### Phase B: Desktop Client Auto-Update (SyncTray)

#### Step 11.7 — IClientUpdateService Interface
- ✓ `IClientUpdateService` interface (`CheckForUpdateAsync`, `DownloadUpdateAsync`, `ApplyUpdateAsync`, `UpdateAvailable` event)
- ✓ Reuses `UpdateCheckResult` and `ReleaseAsset` DTOs from `DotNetCloud.Core`

#### Step 11.8 — ClientUpdateService Implementation
- ✓ `ClientUpdateService` — server endpoint check with GitHub fallback
- ✓ Download with `IProgress<double>` reporting
- ✓ Version comparison logic (semver + pre-release)
- ✓ DI registration via `ClientCoreServiceExtensions`

#### Step 11.9 — Background Update Checker (SyncTray)
- ✓ `UpdateCheckBackgroundService` — periodic timer (24h default, configurable)
- ✓ `UpdateAvailable` event → TrayViewModel notification
- ✓ Tray context menu "Check for Updates…" item

#### Step 11.10 — SyncTray Update UI
- ✓ `UpdateDialog.axaml` — dark themed Avalonia window (version cards, status badges, release notes, progress bar)
- ✓ `UpdateViewModel` — check/download/apply commands, platform asset matching
- ✓ Settings "Updates" tab — current version display, auto-check toggle

#### Step 11.11 — Desktop Client Update Tests
- ✓ `ClientUpdateServiceTests` — 10 tests (server check, fallback, download, events, error handling)
- ✓ `UpdateCheckBackgroundServiceTests` — 8 tests (event firing, error resilience, lifecycle, defaults)
- ✓ All 18 Phase B tests passing

### Phase C: Android Client Update Notification

#### Step 11.12 — Android Update Check Service
- ☐ Android-specific update service checking server endpoint
- ☐ Play Store / APK link handling

#### Step 11.13 — Android Update UI
- ☐ Update notification in Android app
- ☐ Settings page update preferences

#### Step 11.14 — Android Update Tests
- ☐ Android update service unit tests

### Phase D: Documentation & Integration

#### Step 11.15 — Auto-Update Documentation
- ✓ `docs/modules/AUTO_UPDATES.md` — feature documentation
- ✓ `docs/user/AUTO_UPDATES.md` — user-facing update configuration guide
- ✓ Architecture doc updates (Phase 8 → Phase 11 split in ARCHITECTURE.md)
- ✓ README.md roadmap table updated with Phase 11

#### Step 11.16 — Integration Testing
- ✓ End-to-end update check flow tests
- ✓ Update releases endpoint integration tests
- ✓ Backward compatibility tests (graceful degradation)

---

## Direct Messaging, Direct Calls & Host-Based Call Management

### Phase A — Database & Model Changes

#### A1. Rename `CallParticipantRole.Initiator` → `Host`
- ✓ Rename enum value in `CallParticipantRole.cs`
- ✓ Update all references in `VideoCallService.cs`
- ✓ Update DTO comment in `ChatDtos.cs` (`CallParticipantDto.Role`)
- ✓ Update all test files (`VideoCallServiceTests`, `CallSignalingServiceTests`, `VideoCallDataModelTests`, `VideoCallGrpcServiceTests`)
- ✓ EF migration to update stored string values (`"Initiator"` → `"Host"`)

#### A2. Add `HostUserId` to `VideoCall` Entity
- ✓ Add `Guid HostUserId` property to `VideoCall.cs`
- ✓ Configure index in `VideoCallConfiguration.cs`
- ✓ Add `HostUserId` to `VideoCallDto`
- ✓ Update `ToVideoCallDto` mapper in `VideoCallService.cs`
- ✓ Set `HostUserId = caller.UserId` in `InitiateCallAsync`
- ✓ EF migration `AddCallHostUserId` (column + data migration from `InitiatorUserId`)

#### A3. DM → Group Auto-Conversion
- ✓ In `ChannelMemberService.AddMemberAsync`, detect 3rd member added to `DirectMessage` channel
- ✓ Auto-convert channel type to `ChannelType.Group`
- ✓ No schema change needed (`Channel.Type` already supports `Group`)

### Phase B — Service Layer: Direct DM & Call Initiation
- ☐ B1. Wire Global User Search for DM Creation
- ☐ B2. Direct Call Initiation by User ID (`InitiateDirectCallAsync`)

### Phase C — Mid-Call Participant Addition
- ☐ C1. `InviteToCallAsync` service method (Host-only validation)
- ☐ C2. SignalR notification for mid-call invite

### Phase D — Host Transfer
- ☐ D1. `TransferHostAsync` service method
- ☐ D2. Auto-transfer Host on leave
- ☐ D3. End-call permission enforcement (Host only)
- ☐ D4. `CallHostTransferredEvent` and SignalR broadcast

### Phase E — UI Integration
- ✓ E1. "New DM" user picker in sidebar
- ✓ E2. "Call User" buttons
- ✓ E3. "Add People" button in active call (Host only)
- ✓ E4. "Transfer Host" in call participant list
- ✓ E5. Updated incoming call notification (mid-call invite)
- ✓ E6. "Add People" to group chat

### Phase F — SignalR Hub Updates
- ☐ F1. New hub methods (`InviteToCallAsync`, `TransferHostAsync`)
- ☐ F2. New client-side event handlers (`HostTransferred`, `CallInviteReceived`)

### Phase G — Tests
- ✓ G1. Unit tests (Host transfer, mid-call invite, DM→Group, direct call, end-call permission)
- ☐ G2. Integration / E2E tests

---

## Tracks Professionalization — Phase B

### B-1: @Mentions in Comments
- ✓ `MentionTypeahead.razor` component with user search dropdown
- ✓ `@` triggers user search typeahead (max 8 results, 300ms debounce)
- ✓ Mentioned user gets notification with link to work item
- ✓ @username rendered as clickable highlighted link in comments
- ✓ `UsersController` search endpoint via `IUserDirectory`
- ✓ `SearchUsersAsync` added to `ITracksApiClient` / `TracksApiClient`
- ✓ Mention-aware comment textarea in `WorkItemDetailPanel`

### B-2: Product Settings Page
- ✓ `ProductSettingsPage.razor` with full settings UI
- ✓ General section: Name, description, color picker, Sub-Items toggle
- ✓ Swimlanes section: Manage default swimlanes (add/remove/reorder/rename, toggle Done)
- ✓ Members section: List members, change roles, remove, add new via user search
- ✓ Labels section: Create/edit/delete product labels
- ✓ Danger Zone: Archive product, Transfer ownership, Delete with confirmation
- ✓ `TracksView.Settings` enum value
- ✓ Settings gear icon in sidebar (product-level and epic-level)

### B-3: Saved Filters / Custom Views
- ✓ `CustomView` entity with migration
- ✓ `CustomViewConfiguration` EF config (tracks schema, unique index)
- ✓ `CustomViewService` data service (CRUD, auth checks)
- ✓ `CustomViewsController` REST endpoints (list, create, update, delete)
- ✓ `CustomViewDto` in TracksDtos.cs
- ✓ `CustomViewsSidebar.razor` sidebar component
- ✓ "Save Current View" dialog with name + shared toggle
- ✓ API client methods (`ListCustomViewsAsync`, `CreateCustomViewAsync`, etc.)

### B-4: Calendar View
- ✓ `WorkItemCalendarView.razor` with month and week views
- ✓ Month view: 7-column grid with items as colored priority bars
- ✓ Week view: 7-column horizontal layout
- ✓ Click item → opens detail panel via `SelectWorkItemByNumber`
- ✓ Drag item to different date → change due date
- ✓ Color-coded by priority level
- ✓ Previous/Next month navigation + "Today" button
- ✓ `TracksView.Calendar` enum value
- ✓ Calendar icon in sidebar (product-level and epic-level)

---

## Tracks Professionalization — Phase C

### C-1: Table / List View
- ✓ `WorkItemListView.razor` + `.razor.cs` + `.razor.css`
- ✓ `TracksView.List` enum value + sidebar icon
- ✓ Sortable columns (click header to sort asc/desc)
- ✓ Column chooser dropdown (show/hide columns)
- ✓ Multi-select checkboxes with bulk action toolbar (archive, delete, move, label, assign, priority, sprint)
- ✓ Inline editing: double-click to edit title, priority, story points
- ✓ Row click → opens detail panel
- ✓ Group by dropdown: None, Assignee, Priority, Swimlane, Sprint, Type
- ✓ Export to CSV from table view
- ✓ Respects filter dropdowns (priority, swimlane, label)
- ✓ `ListProductWorkItemsAsync` API endpoint (GET /api/v1/products/{productId}/work-items)
- ✓ `BulkWorkItemActionAsync` API endpoint (POST /api/v1/products/{productId}/work-items/bulk)
- ✓ `BulkWorkItemActionDto` request DTO
- ✓ API client methods: `ListProductWorkItemsAsync`, `BulkWorkItemActionAsync`

### C-2: Product Dashboard
- ✓ `ProductDashboardView.razor` + `.razor.cs` + `.razor.css`
- ✓ `TracksView.Dashboard` enum value + sidebar icon
- ✓ KPI row: Total Items, Epics, Features, Active Sprints, Done This Week, Avg Cycle Time, Unassigned
- ✓ Status breakdown: SVG donut chart by swimlane with legend
- ✓ Priority breakdown: SVG bar chart (Urgent/High/Medium/Low/None)
- ✓ Workload: SVG horizontal bar chart — story points per assignee
- ✓ Velocity: last 6 completed sprints with progress bars
- ✓ Recently Updated: feed of last 10 changed items with relative timestamps
- ✓ Upcoming Due Dates: feed of items due this week with overdue highlighting
- ✓ `ProductDashboardDto` + `StatusBreakdownDto` + `PriorityBreakdownDto` + `WorkloadDto` + `RecentlyUpdatedItemDto` + `UpcomingDueDateDto`
- ✓ `GetProductDashboardAsync` in AnalyticsService
- ✓ Dashboard API endpoint (GET /api/v1/products/{productId}/dashboard)
- ✓ API client method: `GetProductDashboardAsync`

---

## Tracks Professionalization — Phase D

### D-1: Custom Fields
- ✓ `CustomField` entity with EF config + migration (tracks schema)
- ✓ `WorkItemFieldValue` entity with EF config + migration (composite unique index)
- ✓ `CustomFieldType` enum in DTOs (Text, Number, Date, SingleSelect, MultiSelect, User)
- ✓ `CustomFieldDto`, `CreateCustomFieldDto`, `UpdateCustomFieldDto`, `WorkItemFieldValueDto`, `SetFieldValueDto`, `BatchSetFieldValuesDto`
- ✓ `CustomFieldService`: CRUD field definitions, get/set/batch field values, validation
- ✓ `CustomFieldsController`: `GET/POST/PUT/DELETE /api/v1/products/{id}/custom-fields`
- ✓ Field value endpoints: `GET/PUT/DELETE /api/v1/work-items/{id}/custom-field-values`
- ✓ Field type validation: required fields, type validation, select options validated
- ✓ `WorkItemDto` extended with `CustomFields` list and `MilestoneId`
- ✓ `Product` entity extended with `CustomFields` navigation property
- ✓ `WorkItem` entity extended with `FieldValues` and `MilestoneId` navigation properties

### D-2: Milestones
- ✓ `Milestone` entity with EF config + migration (tracks schema)
- ✓ `MilestoneStatus` enum in DTOs (Upcoming, Active, Completed)
- ✓ `MilestoneDto`, `CreateMilestoneDto`, `UpdateMilestoneDto`, `SetMilestoneStatusDto`
- ✓ `MilestoneService`: CRUD, status transitions, work item counting, progress tracking
- ✓ `MilestonesController`: `GET/POST/PUT/DELETE /api/v1/products/{id}/milestones`
- ✓ Status endpoint: `PUT /api/v1/milestones/{id}/status`
- ✓ Work items linkable to milestones via `MilestoneId` in `UpdateWorkItemDto`
- ✓ `Product` entity extended with `Milestones` navigation property

### D-3: Recurring Work Items
- ✓ `RecurringRule` entity with EF config + migration (tracks schema)
- ✓ `RecurringRuleDto`, `CreateRecurringRuleDto`, `UpdateRecurringRuleDto`
- ✓ `RecurringWorkItemService`: CRUD rules + `ProcessDueRecurringItemsAsync()`
- ✓ `RecurringWorkItemBackgroundService`: runs every 15 minutes via `PeriodicTimer`
- ✓ `RecurringRulesController`: `GET/POST/PUT/DELETE /api/v1/products/{id}/recurring-rules`
- ✓ Manual trigger endpoint: `POST /api/v1/recurring-rules/process`
- ✓ Cron expression parsing via `Cronos` NuGet package
- ✓ Template JSON support: title, description, priority, storyPoints, labels, assigneeIds
- ✓ Created work items link back to rule via `RecurringRuleId`
- ✓ `Product` entity extended with `RecurringRules` navigation property

## Tracks Professionalization — Phase E

### E-1: Comment Reactions
- ✓ `CommentReaction` entity with EF config + migration (composite key CommentId + UserId + Emoji)
- ✓ `CommentReactionDto` and `CommentReactionSummaryDto` in DTOs
- ✓ `AddReactionDto` for reaction creation requests
- ✓ `CommentService` extended: `AddReactionAsync`, `RemoveReactionAsync`, `GetReactionsAsync`, `GetReactionsForCommentsAsync`
- ✓ `CommentsController` extended: `GET /api/v1/comments/{id}/reactions`, `POST /api/v1/comments/{id}/reactions`, `DELETE /api/v1/comments/{id}/reactions/{emoji}`
- ✓ Toggle behavior: add reaction → same emoji again removes it
- ✓ Batch reaction loading for multiple comments via `GetReactionsForCommentsAsync`
- ✓ `TracksDbContext` extended with `CommentReactions` DbSet

### E-2: Share / Guest Access
- ✓ `WorkItemShareLink` entity with EF config + migration (unique token, expiry, active flag)
- ✓ `GuestUser` entity with EF config + migration (email, invite token, status lifecycle)
- ✓ `GuestPermission` entity with EF config + migration (per-work-item access)
- ✓ `SharePermission` and `GuestPermissionLevel` enums
- ✓ `WorkItemShareLinkDto`, `CreateShareLinkDto`, `GuestUserDto`, `InviteGuestDto`, `GrantPermissionDto` in DTOs
- ✓ `ShareLinkService`: generate/revoke share links, validate tokens, list by work item
- ✓ `GuestAccessService`: invite, accept, revoke guests; grant/revoke per-work-item permissions; resolve effective permissions
- ✓ `ShareLinksController`: `GET/POST /api/v1/work-items/{id}/share-links`, `DELETE /api/v1/share-links/{id}`
- ✓ `GuestAccessController`: `GET/POST /api/v1/products/{id}/guests`, `DELETE /api/v1/guests/{id}`, `POST/DELETE /api/v1/guests/{id}/work-items/{id}/permissions`
- ✓ Secure token generation via `RandomNumberGenerator` (32-byte URL-safe Base64)
- ✓ Auto-deactivation of expired share links on validation
- ✓ `TracksDbContext` extended with `WorkItemShareLinks`, `GuestUsers`, `GuestPermissions` DbSets
- ✓ `TracksServiceRegistration` updated with `ShareLinkService`, `GuestAccessService`

### E-3: Product Templates UI
- ✓ `ProductTemplatesController`: `GET /api/v1/product-templates`, `GET /api/v1/product-templates/{id}`, `POST /api/v1/product-templates/{id}/create-product`, `POST /api/v1/products/{id}/save-as-template`, `GET /api/v1/products/{id}/item-templates`, `POST /api/v1/item-templates/{id}/create-item`
- ✓ `TemplateSeedService`: idempotent seeding of 5 built-in templates on first access
- ✓ 5 built-in templates: Software Project, Bug Tracker, Content Calendar, Simple Todo, Hiring Pipeline
- ✓ `TracksServiceRegistration` updated with `TemplateSeedService`

## Tracks Professionalization — Phase F

### F-1: Command Palette
- ✓ `ICommandPaletteService` interface + `CommandPaletteResult` + `PaletteItem` in `DotNetCloud.Modules.Tracks.Services`
- ✓ `CommandPaletteService` in `DotNetCloud.Modules.Tracks.Data.Services` — uses TracksDbContext for fuzzy search
- ✓ `TracksCommandPalette.razor` + `.razor.cs` — modal overlay triggered by Ctrl+K
- ✓ Fuzzy search across work items (by number/title), products
- ✓ Quick actions: New Epic, New Work Item, Go to My Items, Dashboard, Settings, Toggle Dark Mode, Shortcuts
- ✓ Keyboard navigation: Ctrl+K open, Esc close, ↑↓ arrows, Enter select
- ✓ Recent items tracking in localStorage
- ✓ `TracksServiceRegistration` updated with `ICommandPaletteService`

### F-2: CSV Import Wizard
- ✓ `ICsvImportUiService` interface + `CsvParseResult`, `CsvColumnMapping`, `CsvRowError`, `CsvValidationResult`, `CsvImportResult` in Models
- ✓ `CsvImportService` in Data.Services — parse, validate, import CSV with BOM handling and quoted fields
- ✓ `CsvImportUiService` in Data.Services — implements ICsvImportUiService bridging to UI layer
- ✓ `CsvImportWizard.razor` + `.razor.cs` — 5-step wizard: Upload → Preview → Map → Validate → Import
- ✓ Auto-detect delimiter (comma, tab, semicolon, pipe)
- ✓ Column mapping UI with auto-detection from headers
- ✓ Duplicate detection and chunked import (batches of 50)
- ✓ Controller: `POST /api/v1/products/{id}/work-items/import` with dry-run mode
- ✓ `TracksDbContext` + `TracksServiceRegistration` updated

### F-3: Webhooks
- ✓ `WebhookSubscription` entity — URL, Secret (HMAC), EventsJson, IsActive
- ✓ `WebhookDelivery` entity — EventType, PayloadJson, ResponseStatusCode, DurationMs, RetryCount
- ✓ `WebhookSubscriptionConfiguration` + `WebhookDeliveryConfiguration` EF configs
- ✓ `WebhookService` — CRUD subscriptions, GetMatchingSubscriptionsAsync, GenerateSecret, ComputeSignature (HMAC-SHA256)
- ✓ `WebhookDeliveryService` — HTTP POST with `X-DotNetCloud-Signature` header, delivery recording
- ✓ `WebhookRetryBackgroundService` — exponential backoff retry (1min→5min→15min→1h→6h→24h→24h, max 7)
- ✓ `IWebhookDispatchService` in Services + `WebhookDispatchService` in Data.Services
- ✓ `WebhookEventHandler` — subscribes to all Tracks events, dispatches via IWebhookDispatchService
- ✓ `MilestoneReachedEvent` added to `TracksEvents.cs`
- ✓ `WebhooksController` — GET/POST/PUT/DELETE + test endpoint
- ✓ `WebhookDeliveriesController` — paginated delivery log
- ✓ `WebhookManagementPage.razor` — list, add, edit, delete, test webhooks with toast notifications
- ✓ `ITracksApiClient` + `TracksApiClient` extended with webhook methods + `WebhookTestResult`
- ✓ `TracksDbContext` extended with `WebhookSubscriptions`, `WebhookDeliveries`
- ✓ `TracksServiceRegistration` updated with all new services + background service
- ✓ `TracksModule.cs` updated to initialize WebhookEventHandler and subscribe to all events

## Tracks Professionalization — Phase G

### G-1: Product Roadmap
- ✓ `RoadmapItemDto` + `RoadmapDataDto` in TracksDtos.cs
- ✓ `ProductRoadmapView.razor` + `.razor.cs` + `.razor.css` — horizontal timeline with epics/features
- ✓ Group by: Epic (default), Sprint, Assignee
- ✓ Color coding by swimlane color or priority
- ✓ SVG dependency arrows between dependent items
- ✓ Today marker: vertical dashed line with "Today" label
- ✓ Click item opens detail panel; click "Open Full Detail" triggers OnWorkItemSelected
- ✓ Zoom toggle: Month / Quarter / Year view
- ✓ Milestone diamond markers on timeline (from Phase D milestones)
- ✓ Empty state: "No roadmap items. Create epics with due dates to see them here."
- ✓ `TracksView.Roadmap` enum addition to TracksPage
- ✓ Roadmap sidebar nav button (🗺️) in product sidebar
- ✓ Controller: `GET /api/v1/products/{id}/roadmap` in AnalyticsController
- ✓ `GetRoadmapDataAsync()` method in AnalyticsService
- ✓ `StartDate` added to `WorkItem` model, `WorkItemDto`, `CreateWorkItemDto`, `UpdateWorkItemDto`
- ✓ `StartDate` EF index in WorkItemConfiguration
- ✓ `ITracksApiClient` + `TracksApiClient` extended with `GetRoadmapDataAsync()`

### G-2: Automation Rules
- ✓ `AutomationRule` entity — ProductId, Name, Trigger, ConditionsJson, ActionsJson, IsActive, CreatedByUserId, LastTriggeredAt
- ✓ `AutomationRuleConfiguration` EF config with migration
- ✓ `AutomationRuleDto`, `CreateAutomationRuleDto`, `UpdateAutomationRuleDto` in TracksDtos.cs
- ✓ `AutomationRuleService` — CRUD + `EvaluateRulesAsync()` with condition parsing (equals, not_equals, contains, greater_than, less_than)
- ✓ `IAutomationRuleExecutionService` interface in Services
- ✓ `AutomationRuleExecutionService` — executes rule actions (add_label, remove_label, move_to_swimlane, assign, set_priority, set_field, add_comment, notify)
- ✓ `AutomationRuleEventHandler` — subscribes to WorkItemCreated/Updated/Moved/Assigned events via IEventBus
- ✓ `AutomationRulesController` — GET/POST/PUT/DELETE `/api/v1/products/{id}/automation-rules`
- ✓ `AutomationRuleEditor.razor` + `.razor.cs` + `.razor.css` — rule builder in product settings
- ✓ Trigger dropdown with human-readable labels
- ✓ Action builder with dynamic parameters per action type
- ✓ Rule preview: natural language summary
- ✓ 3 pre-built template rules (done_label, urgent_notify, due_reminder)
- ✓ Toggle to enable/disable individual rules
- ✓ `TracksDbContext` extended with `AutomationRules`
- ✓ `TracksServiceRegistration` updated with `AutomationRuleService`, `GoalService`, `IAutomationRuleExecutionService`
- ✓ `TracksModule.cs` updated with `AutomationRuleEventHandler` subscriptions and cleanup

### G-3: Goals / OKRs
- ✓ `Goal` entity — ProductId, Title, Description, Type (objective/key_result), ParentGoalId, TargetValue, CurrentValue, ProgressType, Status, DueDate
- ✓ `GoalWorkItem` junction entity linking work items to goals
- ✓ `GoalConfiguration` + `GoalWorkItemConfiguration` EF configs with migration
- ✓ `GoalDto`, `CreateGoalDto`, `UpdateGoalDto`, `LinkGoalWorkItemDto` in TracksDtos.cs
- ✓ `GoalService` — CRUD + progress calculation (manual/automatic), status auto-computation, link/unlink work items
- ✓ `GoalsController` — GET/POST/PUT/DELETE `/api/v1/products/{id}/goals` + link/unlink endpoints
- ✓ `GoalsList.razor` + `.razor.cs` + `.razor.css` — hierarchical list with expand/collapse, progress bars, status badges
- ✓ Key results nested under objectives with progress indicators
- ✓ Create/edit modal with title, description, target/current values, progress type, due date
- ✓ `GoalDetail.razor` + `.razor.cs` + `.razor.css` — detail panel with progress tracking and status management
- ✓ Manual progress update with current value input
- ✓ Status auto-computation based on progress percentage and due date
- ✓ `TracksDbContext` extended with `Goals`, `GoalWorkItems`
- ✓ `ITracksApiClient` + `TracksApiClient` extended with all goal methods

### G-4: Capacity Planning
- ✓ `SprintCapacityDto`, `MemberCapacityDto`, `ProductCapacityDto` in TracksDtos.cs
- ✓ `GetSprintCapacityAsync()` in AnalyticsService — total/assigned/completed story points per sprint
- ✓ `GetMemberCapacityAsync()` in AnalyticsService — story points per assignee across active sprints
- ✓ `GetProductCapacityAsync()` in AnalyticsService — full capacity overview with overloaded count
- ✓ Controller: `GET /api/v1/sprints/{sprintId}/capacity` in AnalyticsController
- ✓ Controller: `GET /api/v1/products/{productId}/analytics/capacity` in AnalyticsController
- ✓ `CapacityWidget.razor` + `.razor.cs` + `.razor.css` — horizontal bar chart per member
- ✓ Color coding: green (< 60%), yellow (60-90%), orange (90-100%), red (> 100%)
- ✓ Member name + avatar label, story points count, capacity percentage
- ✓ Overloaded badge when members exceed 90% capacity
- ✓ Capacity legend
- ✓ `ITracksApiClient` + `TracksApiClient` extended with `GetProductCapacityAsync()`, `GetSprintCapacityAsync()`

## Tracks Professionalization — Phase H

### H-1: Dark Mode Enhancements
- ✓ Dark mode overrides added to all 11 Tracks CSS files
- ✓ `TracksPage.razor.css` — Kanban columns, cards, card count badges, comment code blocks, empty states, dialogs
- ✓ `ProductDashboardView.razor.css` — KPI cards, chart cards, velocity bars
- ✓ `WorkItemListView.razor.css` — Stats badges, danger buttons
- ✓ `ProductRoadmapView.razor.css` — Loading states, labels
- ✓ `ProductSettingsPage.razor.css` — Member names, icon buttons, transition matrix, WIP inputs
- ✓ `AutomationRuleEditor.razor.css` — Rule cards, empty/loading states
- ✓ `WorkItemFullscreenPage.razor.css` — Denied card, overlay opacity
- ✓ `GoalsList.razor.css` — Goal cards, shadows
- ✓ `CapacityWidget.razor.css` — Widget surface, bar tracks, overload badges
- ✓ `GoalDetail.razor.css` — Detail cards, progress sections, status badges
- ✓ `ChatActivityIndicator.razor.css` — Toast backgrounds, channel events

### H-2: Swimlane Transition Rules
- ✓ `SwimlaneTransitionRule` entity with EF config + composite unique index
- ✓ `SwimlaneTransitionRuleConfiguration` — EF configuration with relationships
- ✓ `SwimlaneTransitionRuleDto` and `SetTransitionRuleDto` in Models
- ✓ `SwimlaneTransitionService` — CRUD transition matrix, validate moves, get allowed targets
- ✓ `TracksDbContext` extended with `SwimlaneTransitionRules` DbSet
- ✓ `TracksServiceRegistration` updated with `SwimlaneTransitionService`
- ✓ `SwimlanesController` extended — GET/PUT transition matrix, GET allowed targets
- ✓ `WorkItemService.MoveWorkItemAsync` checks transition rules before moves
- ✓ `WorkItemsController.MoveWorkItemAsync` returns 409 Conflict for blocked transitions
- ✓ `SwimlaneService.GetSwimlaneByIdAsync()` added
- ✓ `ITracksApiClient` + `TracksApiClient` extended with transition matrix methods
- ✓ `ProductSettingsPage.razor` — Transition Rules section with matrix UI + presets
- ✓ `ProductSettingsPage.razor.cs` — Transition rule state management

### H-3: WIP Limits Enforcement
- ✓ `MoveWorkItemDto.EnforceWipLimit` field added
- ✓ `WorkItemService.MoveWorkItemAsync` checks CardLimit with soft/hard enforcement
- ✓ `WorkItemsController.MoveWorkItemAsync` returns 409 Conflict for blocked WIP moves
- ✓ `KanbanBoard.razor` — WIP warning toast with icon, message, dismiss
- ✓ `KanbanBoard.razor.cs` — `EnforceWipStrictly` parameter, client-side WIP check
- ✓ `ProductSettingsPage.razor` — CardLimit input per swimlane + "Enforce WIP limits" checkbox
- ✓ `ProductSettingsPage.razor.cs` — `SettingsSwimlane.CardLimit`, `_enforceWipStrictly` state
- ✓ WIP toast CSS + transition matrix CSS styles


## Required Modules & Schema Separation

> **Reference:** `docs/REQUIRED_MODULES_AND_SCHEMA_SEPARATION_PLAN.md`

### Phase 1 — Authority and database foundation ✓

- ✓ Create `RequiredModules` static registry (`DotNetCloud.Core/Modules/RequiredModules.cs`)
- ✓ Add `IsRequired` to `InstalledModule` entity and EF configuration
- ✓ Generate EF migration `AddIsRequiredToInstalledModule` for CoreDbContext
- ✓ Add `IsRequired` to `ModuleDto`

### Phase 2 — Schema enforcement in naming strategies ✓

- ✓ Update `PostgreSqlNamingStrategy.GetSchemaForModule` to delegate to `RequiredModules.GetSchemaName`
- ✓ Update `SqlServerNamingStrategy.GetSchemaForModule` to delegate to `RequiredModules.GetSchemaName`
- ✓ Update `MariaDbNamingStrategy.GetTableName` to use `RequiredModules.GetSchemaName` for prefix
- ✓ Update `FilesDbContext` — inject `ITableNamingStrategy`, add `HasDefaultSchema("files")` → `core`
- ✓ Update `ChatDbContext` — inject `ITableNamingStrategy`, add `HasDefaultSchema("chat")` → `core`
- ✓ Update `SearchDbContext` — inject `ITableNamingStrategy`, add `HasDefaultSchema("search")` → `core`
- ✓ Update `ContactsDbContext` — inject `ITableNamingStrategy`, add `HasDefaultSchema("contacts")` → `contacts`
- ✓ Update `CalendarDbContext` — inject `ITableNamingStrategy`, add `HasDefaultSchema("calendar")` → `calendar`
- ✓ Update `NotesDbContext` — inject `ITableNamingStrategy`, add `HasDefaultSchema("notes")` → `notes`
- ✓ Update `TracksDbContext` — inject `ITableNamingStrategy`, add `HasDefaultSchema("tracks")` → `tracks`
- ✓ Update `PhotosDbContext` — replace hardcoded `"photos"` with `GetSchemaForModule("photos")`
- ✓ Update `MusicDbContext` — replace hardcoded `"music"` with `GetSchemaForModule("music")`
- ✓ Update `VideoDbContext` — replace hardcoded `"video"` with `GetSchemaForModule("video")`
- ✓ Update `AiDbContext` — inject `ITableNamingStrategy`, add `HasDefaultSchema("ai")` → `ai`
- ✓ Update all 13 `DesignTimeFactory` files to pass naming strategy
- ✓ Add backward-compatible single-parameter constructors to all 11 DbContexts
- ✓ Build passes with 0 errors; all 21 test projects pass (5,763 tests, 0 failures)

### Phase 3 — Lazy schema creation ✓

- ✓ Create `IModuleSchemaProvider` interface (`DotNetCloud.Core/Modules/IModuleSchemaProvider.cs`)
- ✓ Add `SchemaProvider` field to `ModuleManifestData` (default `"self"`)
- ✓ Create `DbContextSchemaProvider` in `DotNetCloud.Core.Server/Services/` — resolves module DbContexts from DI
- ✓ Create `SelfManagedSchemaProvider` in `DotNetCloud.Core.Data/Services/` — no-op for self-managed modules
- ✓ Create `ModuleSchemaService` in `DotNetCloud.Core.Server/Services/` — dispatches core-managed vs self-managed
- ✓ Register `SelfManagedSchemaProvider` in `DataServiceExtensions.AddDotNetCloudDbContext`
- ✓ Register `DbContextSchemaProvider` + `ModuleSchemaService` in server `ConfigureServices`
- ✓ Gate core server `DbInitializer` on `InstalledModules` status (Enabled/Installing)
- ✓ Trigger schema creation in `SeedKnownModulesAsync` for newly seeded modules
- ✓ Set `IsRequired` on all `InstalledModule` creation paths (seed, setup, CLI install)
- ✓ Update `SetupCommand` — use `RequiredModules.ModuleIds`, guard disabling required modules
- ✓ Update `ModuleCommands` — guard stop/uninstall for required modules
- ✓ Add `"schemaProvider": "core"` to Contacts, Calendar, Notes, AI, Tracks manifest.json files
- ✓ Add `"schemaProvider": "self"` to Example module manifest.json
- ✓ Build passes with 0 errors; all 18 test projects pass (5,104 tests, 0 failures)

### Phase 4 — Seeding and DTO mapping ✓
### Phase 5 — Enforcement in API, CLI, and supervisor ✓

- ✓ `AdminModuleService.StopModuleAsync` — guard against stopping required modules
- ✓ `AdminController.StopModuleAsync` — catch `InvalidOperationException`, return 400 with `MODULE_REQUIRED` code
- ✓ `ProcessSupervisor.SyncDiscoveredModulesToDatabaseAsync` — set `IsRequired` on newly discovered module records
- ✓ `ModuleCommands` — guard stop/uninstall for required modules (already in Phase 3)
- ✓ `SetupCommand` — use `RequiredModules.ModuleIds`, guard disabling required modules (already in Phase 3)
### Phase 6 — install.sh ✓
- ✓ Create `DotNetCloud.Core.Schema` project to host `DbContextSchemaProvider`
- ✓ Move `ModuleSchemaService` to `Core.Data` with `IEnumerable<IModuleSchemaProvider>` dispatch
- ✓ Add `--migrate-only` flag to `SetupCommand` (mutually exclusive with `--beginner`)
- ✓ `RunMigrateOnlyAsync` applies core migrations, syncs module registry, and initializes module schemas
- ✓ Register `DbContextSchemaProvider` via `IModuleSchemaProvider` in both CLI `ServiceProviderFactory` and server `Program.cs`
- ✓ Update `install.sh` fallback warning
### Phase 7 — Update Example module ✓
- ✓ Add `"schemaProvider": "self"` to Example module `manifest.json` (already present)
- ✓ Update `ExampleDbContext` to inject `ITableNamingStrategy` and call `HasDefaultSchema`
- ✓ Update `Program.cs` for self-managed migration pattern (`async Task Main`, env var, in-memory fallback)
- ✓ Add `Npgsql.EntityFrameworkCore.PostgreSQL` and `Microsoft.EntityFrameworkCore.Design` package references
- ✓ Create `ExampleDbContextFactory` for design-time EF tooling
- ✓ Add EF `InitialCreate` migration for the `example` schema
- ✓ Update `README.md` with schema management documentation

---

## Browser Extension

### Phase 1: Server-Side Extension Support ✅

> All server changes passed `dotnet build DotNetCloud.CI.slnf` and `dotnet test DotNetCloud.CI.slnf`.

#### Step 1.1 — Enable Device Authorization Grant ✅
- ✓ `AllowDeviceCodeFlow()` enabled in `AuthServiceExtensions.cs`
- ✓ Device endpoint URI configured (`/connect/device`)
- ✓ `bookmarks:read` and `bookmarks:write` scopes registered

#### Step 1.2 — Register Extension OIDC Client ✅
- ✓ `dotnetcloud-browser-extension` client registered in `OidcClientSeeder.cs`
- ✓ Public client with device code + refresh token grant types

#### Step 1.3 — Delta Sync Endpoint ✅
- ✓ `GET /api/v1/bookmarks/sync/changes?since=...` with `BookmarkSyncChangesResult`
- ✓ `IBookmarkService.GetSyncChangesAsync()` implemented

#### Step 1.4 — Batch Operations Endpoint ✅
- ✓ `POST /api/v1/bookmarks/batch` with `BatchRequest`/`BatchResponse`
- ✓ `IBookmarkService.BatchAsync()` implemented

### Phase 2: Extension Project Scaffold ✅

#### Step 2.1 — Project Initialization ✅
- ✓ `package.json` — scripts (build, test, typecheck, dev), dependencies
- ✓ `tsconfig.json` — ES2022, strict mode, bundler resolution
- ✓ `jest.config.js` — ts-jest preset
- ✓ `.gitignore`

#### Step 2.2 — Dual Manifests ✅
- ✓ `manifest.chrome.json` (MV3) — permissions, service worker, popup, icons
- ✓ `manifest.firefox.json` (MV3, FF ≥ 109) — browser_specific_settings
- ✓ `vite.config.ts` — dual-browser output via `--mode chrome`/`--mode firefox`

#### Step 2.3 — API Client Layer ✅
- ✓ `src/api/types.ts` — all DTOs (BookmarkItem, BookmarkFolder, SyncChangesResponse, BatchRequest, etc.)
- ✓ `src/api/client.ts` — typed fetch wrapper with all CRUD + sync + batch methods, AbortSignal support, ApiError on non-2xx
- ✓ `src/api/auth.ts` — `getAuthHeaders()` + `isAuthenticated()`

#### Step 2.4 — Auth Modules ✅
- ✓ `src/auth/device-flow.ts` — `initiateDeviceFlow()` + `pollForToken()` (RFC 8628)
- ✓ `src/auth/token-manager.ts` — `getAccessToken()`, `refresh()`, `clearTokens()`, `scheduleRefresh()`, `handleAlarm()`

#### Step 2.5 — Background Service Worker ✅
- ✓ `src/background/service-worker.ts` — alarm handler, install/update hooks, startup refresh scheduling

#### Step 2.6 — Popup Scaffold ✅
- ✓ `src/popup/popup.html` — 380px fixed layout
- ✓ `src/popup/popup.ts` — auth screen + device flow initiation + main UI placeholder
- ✓ `src/popup/styles/popup.css` — full design system (auth screen, main UI, tabs, status footer, spinner)

#### Step 2.7 — Build Scripts ✅
- ✓ `build-extension.ps1` (PowerShell/Windows) — `npm run build` + zip packaging
- ✓ `build-extension.sh` (Bash/Linux) — equivalent for Linux/macOS

#### Step 2.8 — Icons ✅
- ✓ Placeholder icons: 16×16, 48×48, 128×128 PNG

### Phase 3: Authentication ✅

> Phase 3 implements the complete OAuth2 Device Authorization Grant flow (RFC 8628).
> The extension authenticates via device code flow — no passwords stored in the extension.
> Token auto-refresh is scheduled via chrome.alarms before expiry.
> Test coverage: 37 unit tests passing, all build/typecheck clean.

#### Step 3.1 — Device Flow Initiator ✅
- ✓ `src/auth/device-flow.ts` — full RFC 8628 implementation
- ✓ `initiateDeviceFlow(serverUrl)` — POST to `/connect/device` with client credentials and scope
- ✓ `pollForToken(serverUrl, state)` — polls `/connect/token` with proper error handling
- ✓ Error handling: `authorization_pending` (keep polling), `slow_down` (increase interval), `access_denied` (throw), `expired_token` (throw)
- ✓ Token stored via `TokenManager.storeTokens()` on success
- ✓ Verifies scope includes `bookmarks:read` and `bookmarks:write`
- ✓ Supports AbortSignal for cancellation
- ✓ Deadline enforcement: exits when device code expires

#### Step 3.2 — Token Manager ✅
- ✓ `src/auth/token-manager.ts` — full token lifecycle management
- ✓ `TokenManager.storeTokens()` — persists to `chrome.storage.local` + schedules refresh
- ✓ `TokenManager.getTokens()` — reads stored TokenSet
- ✓ `TokenManager.getAccessToken()` — returns valid token, auto-refreshes within 60s of expiry
- ✓ `TokenManager.refresh()` — POST to `/connect/token` with refresh_token; handles `invalid_grant`/`revoked` (clears tokens)
- ✓ `TokenManager.clearTokens()` — removes tokens + clears refresh alarm
- ✓ `TokenManager.scheduleRefresh()` — creates `chrome.alarms` alarm to fire 60s before expiry
- ✓ `TokenManager.handleAlarm(alarm)` — routes `token-refresh` alarm to `refresh()`
- ✓ Background service worker: `chrome.alarms.onAlarm` listener + startup refresh scheduling

#### Step 3.3 — Auth Test Coverage ✅
- ✓ `tests/device-flow.test.ts` — 10 tests (initiation, polling, error handling, abort, deadline)
- ✓ `tests/token-manager.test.ts` — 22 tests (store/get/refresh/clear/schedule/handleAlarm)
- ✓ `tests/auth-api.test.ts` — 5 tests (getAuthHeaders, isAuthenticated, null states)
- ✓ All tests pass with mocked `chrome.*` APIs and fetch interceptor

### Phase 4: Sync Engine ✓

#### Step 4.1 — ID Mapping Store ✓
- ✓ `src/sync/mapping-store.ts` — bidirectional browser↔server ID maps (completed as scaffold)
- ✓ Full bidirectional ID mapping API (getServerId, getBrowserNodeId, setMapping, removeMapping, get/setCursor, clearAll)
- ✓ Full test coverage for mapping store (completed in Phase 6: 18 tests)

#### Step 4.2 — Initial Sync ✓
- ✓ `src/sync/initial-sync.ts` — server-first full sync algorithm
- ✓ Folder tree reconstruction (topological sort, parent-before-child creation)
- ✓ Browser-only bookmark detection + batch create with clientRef→serverId mapping
- ✓ `isInitialSyncInProgress` flag for push-sync guard
- ✓ Service worker integration with auth-driven initialization

#### Step 4.3 — Incremental Push ✓
- ✓ `src/sync/push-sync.ts` — all 4 `chrome.bookmarks` event listeners (onCreated, onRemoved, onChanged, onMoved)
- ✓ Debounced 500ms per node ID to coalesce rapid changes
- ✓ Guards for browser root nodes and in-progress initial sync
- ✓ Pending operations queue for offline periods (flush on reconnect)
- ✓ Server API calls: create/update/delete bookmark, create/update/delete folder, move

#### Step 4.4 — Incremental Pull ✓
- ✓ `src/sync/pull-sync.ts` — 5-min `chrome.alarms` periodic poll
- ✓ Full pull cycle: fetch changes → apply folders → apply bookmarks → process deletions → update cursor
- ✓ Pagination support (hasMore → immediate follow-up cycles)
- ✓ Server-wins conflict resolution (always applies server state)
- ✓ Service worker integration (startPullSync/stopPullSync controlled by auth state)

### Phase 5: Popup UI ✅

#### Step 5.1 — Auth Screen ✅
- ✓ Server URL input + "Connect" button (completed as scaffold)

#### Step 5.2 — Main Popup Structure ✅
- ✓ Header, tab nav, status footer (completed as scaffold)
- ✓ Tab switching logic with dynamic panel loading
- ✓ Error handling per panel with retry button

#### Step 5.3 — Save Panel ✅
- ✓ `SavePanel.ts` — auto-fill URL/title from active tab via `chrome.tabs.query`
- ✓ Folder picker with lazy-loaded indented folder tree (topological sort)
- ✓ Tags input with chip rendering (Enter/comma to add, × to remove)
- ✓ Notes textarea with collapse/expand toggle
- ✓ Save/Update button — detects existing bookmark by URL search, shows "Update Bookmark" for duplicates
- ✓ Success toast with auto-close (800ms) after save
- ✓ Last-used folder persistence via `chrome.storage.local`
- ✓ Fire-and-forget preview trigger after create

#### Step 5.4 — Browse Panel ✅
- ✓ `BrowsePanel.ts` — folder tree navigation with breadcrumb trail
- ✓ Clickable breadcrumb links for backtracking
- ✓ Bookmark list with favicons from Google's favicon service (`www.google.com/s2/favicons`)
- ✓ Folder/bookmark separation with visual divider
- ✓ Infinite scroll (load next 20 on scroll to bottom)
- ✓ Right-click context menu: Open in New Tab, Copy URL, Delete
- ✓ Refresh button to reload current folder
- ✓ Empty state messaging

#### Step 5.5 — Search Panel ✅
- ✓ `SearchPanel.ts` — debounced search (300ms) via `GET /api/v1/bookmarks/search`
- ✓ Results display with favicon, title, domain URL, and folder path breadcrumb
- ✓ Click result → `chrome.tabs.create({ url })` + close popup
- ✓ Empty query → show "Recently Added" bookmarks
- ✓ "No results" state with hint text
- ✓ Search failure state with retry guidance

#### Step 5.6 — Sync Status Footer ✅
- ✓ Real-time sync status with color-coded dot (green = recent, amber = old, red = error, grey = offline)
- ✓ Auto-refresh every 15 seconds
- ✓ Click footer → sync details overlay showing last sync time and synced bookmark count
- ✓ "Sync Now" button in overlay to trigger manual pull cycle
- ✓ Cursor-based time display ("Synced just now", "Synced X min ago", "Synced X hours ago")

### Phase 6: Build, Tests & Docs ✅

#### Step 6.1 — Build Pipeline ✅
- ✓ `vite.config.ts` — dual-browser output (completed as scaffold)
- ✓ `build-extension.ps1` / `build-extension.sh` (completed as scaffold)

#### Step 6.2 — Unit Tests ✅
- ✓ Auth module tests (37 tests, completed in Phase 3)
- ✓ `tests/mapping-store.test.ts` — 18 tests covering set/get/remove/clear/cursor for both bookmark and folder maps, persistence across calls, reverse lookup
- ✓ `tests/initial-sync.test.ts` — 7 tests covering auth guard, folder tree reconstruction, browser-only bookmarks batch, pagination, cursor persistence, isInitialSyncInProgress flag lifecycle
- ✓ `tests/push-sync.test.ts` — 5 tests covering lifecycle (start/stop, idempotency), listener registration for all 4 bookmark events
- ✓ `tests/conflict-resolution.test.ts` — 12 tests covering server-wins (title/URL conflict), deletion handling (bookmark + folder), missing mapping guards, folder application (create/update), bookmark creation, pull cycle guards (no cursor, not authed)
- ✓ All 79 tests pass: `npm test` clean, `npx tsc --noEmit` clean, `npm run build:chrome` clean

#### Step 6.3 — Documentation Updates ✅
- ✓ Updated IMPLEMENTATION_CHECKLIST.md — Phase 6 marked complete
- ✓ Updated MASTER_PROJECT_PLAN.md — Phase 6 status/details updated

