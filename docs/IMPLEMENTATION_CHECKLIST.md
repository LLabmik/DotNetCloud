# DotNetCloud Implementation Planning Checklist

> **Document Version:** 1.0  
> **Purpose:** Comprehensive task breakdown for implementing the DotNetCloud architecture  
> **Scope:** All phases from Foundation (Phase 0) through AI Assistant (Phase 9)  
> **Last Updated:** 2026-03-02
> **Audience:** Development team, project managers, technical leads

---

## Table of Contents

1. [Pre-Implementation Setup](#pre-implementation-setup)
2. [Phase 0: Foundation](#phase-0-foundation)
3. [Phase 1: Files (Public Launch)](#phase-1-files-public-launch)
4. [Phase 2: Chat & Notifications](#phase-2-chat--notifications)
5. [Phase 3: Contacts, Calendar & Notes](#phase-3-contacts-calendar--notes)
6. [Phase 4: Project Management (Deck)](#phase-4-project-management-deck)
7. [Phase 5: Media (Photos, Music, Video)](#phase-5-media-photos-music-video)
8. [Phase 6: Email & Bookmarks](#phase-6-email--bookmarks)
9. [Phase 7: Video Calling & Screen Sharing](#phase-7-video-calling--screen-sharing)
10. [Phase 8: Search, Auto-Updates & Polish](#phase-8-search-auto-updates--polish)
11. [Phase 9: AI Assistant](#phase-9-ai-assistant)
12. [Infrastructure & DevOps](#infrastructure--devops)
13. [Documentation & Support](#documentation--support)

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

- ☐ Create Gitea Actions workflow file (`.gitea/workflows/build-test.yml`)
- ☐ Create GitHub Actions workflow file (`.github/workflows/build-test.yml`)
- ☐ Configure multi-database testing (Docker containers for PostgreSQL, SQL Server, MariaDB)
- ☐ Set up build artifact generation
- ☐ Configure package publishing pipeline skeleton
- ☐ Create status badge documentation

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
  - ✓ MariaDB migrations

---

## Phase 0.3: Service Defaults & Cross-Cutting Concerns

### DotNetCloud.Core.ServiceDefaults Project

**Create shared infrastructure for all projects**

#### Logging Setup
- [ ] Configure Serilog:
  - [ ] Console sink for development
  - [ ] File sink for production
  - [ ] Structured logging format
  - [ ] Log level configuration per module
- [ ] Create log context enrichment (user ID, request ID, module name)
- [ ] Set up log filtering

#### Health Checks
- [ ] Create health check infrastructure
- [ ] Implement database health check
- [ ] Create custom health check interface for modules
- [ ] Set up health check endpoints

#### OpenTelemetry Setup
- [ ] Configure metrics collection:
  - [ ] HTTP request metrics
  - [ ] gRPC call metrics
  - [ ] Database query metrics
- [ ] Configure distributed tracing:
  - [ ] W3C Trace Context propagation
  - [ ] gRPC interceptor for tracing
  - [ ] HTTP middleware for tracing
- [ ] Implement trace exporter configuration

#### Security Middleware
- [ ] Create CORS configuration
- [ ] Add security headers middleware:
  - [ ] Content-Security-Policy
  - [ ] X-Frame-Options
  - [ ] X-Content-Type-Options
  - [ ] Strict-Transport-Security
- [ ] Create authentication/authorization middleware

#### Error Handling
- [ ] Create global exception handler middleware
- [ ] Implement consistent error response formatting
- [ ] Add request validation error handling

#### Request/Response Logging
- [ ] Create request/response logging middleware
- [ ] Configure sensitive data masking

---

## Phase 0.4: Authentication & Authorization

### OpenIddict Setup

**OAuth2/OIDC Server Implementation**

#### Core Configuration
- [ ] Add OpenIddict NuGet packages
- [ ] Configure OpenIddict in dependency injection:
  - [ ] Server features
  - [ ] Token formats
  - [ ] Scopes
- [ ] Create `OpenIddictApplication` entity model for registered clients
- [ ] Implement OpenIddict data access layer

#### Token Endpoints
- [ ] Implement `/connect/token` endpoint:
  - [ ] Authorization Code flow
  - [ ] Refresh Token flow
  - [ ] Client Credentials flow
- [ ] Implement `/connect/authorize` endpoint with:
  - [ ] Login page
  - [ ] Consent page
  - [ ] PKCE support
- [ ] Implement `/connect/logout` endpoint
- [ ] Implement `/connect/userinfo` endpoint
- [ ] Implement token revocation endpoint (`/connect/revoke`)

#### PKCE Support
- [ ] Configure PKCE requirement for public clients
- [ ] Implement PKCE validation
- [ ] Create PKCE code challenge/verifier handling

#### Token Handling
- [ ] Configure access token lifetime
- [ ] Configure refresh token lifetime
- [ ] Implement refresh token rotation
- [ ] Create token validation middleware
- [ ] Implement token introspection

#### MFA Integration
- [ ] Configure TOTP (Time-based One-Time Password) support
- [ ] Create TOTP setup flow
- [ ] Implement TOTP verification
- [ ] Generate backup codes for MFA recovery
- [ ] Store TOTP secrets encrypted

#### WebAuthn/Passkey Support
- [ ] Integrate Fido2NetLib package
- [ ] Create passkey registration flow
- [ ] Implement passkey verification
- [ ] Store WebAuthn credentials

### ASP.NET Core Identity Integration

**User Management**

#### User Registration
- [ ] Create user registration endpoint
- [ ] Implement email validation
- [ ] Create email confirmation flow
- [ ] Implement password validation rules
- [ ] Set up account lockout after failed attempts

#### Password Management
- [ ] Create password change endpoint
- [ ] Implement password reset flow
- [ ] Create password reset token generation
- [ ] Implement password reset token validation
- [ ] Add password history (prevent reuse)

#### MFA Management
- [ ] Create TOTP setup endpoint
- [ ] Implement TOTP verification endpoint
- [ ] Create backup code endpoint
- [ ] Implement MFA enforcement policies

#### Session Management
- [ ] Configure cookie-based sessions
- [ ] Implement session timeout
- [ ] Create concurrent session limits
- [ ] Implement device tracking

### External Authentication (Federation)

**Third-Party Authentication Providers**

#### Google OAuth
- [ ] Configure Google OAuth options
- [ ] Implement Google sign-in handler
- [ ] Map Google claims to ApplicationUser

#### Microsoft/Azure AD
- [ ] Configure Microsoft/Azure AD options
- [ ] Implement Microsoft sign-in handler
- [ ] Map Microsoft claims to ApplicationUser

#### GitHub OAuth
- [ ] Configure GitHub OAuth options
- [ ] Implement GitHub sign-in handler (future phase)

#### SAML 2.0 (Skeleton)
- [ ] Create SAML configuration structure
- [ ] Document SAML metadata endpoint
- [ ] Create assertion consumer service endpoint (ACS)

#### OIDC Federation (Skeleton)
- [ ] Create OIDC discovery endpoint
- [ ] Document federation configuration

---

## Phase 0.5: Module System Infrastructure

### Module Framework

**Module abstraction and lifecycle management**

#### Module Interfaces
- [ ] Create `IModule` interface with lifecycle methods
- [ ] Create `IModuleManifest` validation
- [ ] Create `IModuleLifecycle` interface:
  - [ ] `Task InitializeAsync()`
  - [ ] `Task StartAsync()`
  - [ ] `Task StopAsync()`
  - [ ] `Task DisposeAsync()`
- [ ] Create module initialization context

#### Module Registry
- [ ] Create module registry data model
- [ ] Implement module discovery mechanism
- [ ] Create module loading strategy
- [ ] Implement module versioning support

#### Capability System Implementation
- [ ] Create capability request validation
- [ ] Implement capability tier enforcement
- [ ] Create capability granting mechanism
- [ ] Implement capability injection into modules
- [ ] Handle missing capabilities gracefully (null injection)

#### Event System Implementation
- [ ] Implement in-process event bus
- [ ] Create event publishing
- [ ] Create event subscription management
- [ ] Implement event filtering by capabilities
- [ ] Create event persistence (for replay/audit)

---

## Phase 0.6: Process Supervisor & gRPC Host

### DotNetCloud.Core.Server Project

**Process management and module communication**

#### Process Supervisor
- [ ] Create module process spawning logic
- [ ] Implement process health monitoring:
  - [ ] Periodic gRPC health checks
  - [ ] Configurable check intervals
  - [ ] Health status tracking
- [ ] Implement restart policies:
  - [ ] Immediate restart
  - [ ] Exponential backoff
  - [ ] Alert-only (no auto-restart)
- [ ] Implement graceful shutdown:
  - [ ] Signal modules to stop
  - [ ] Wait for graceful termination
  - [ ] Force kill timeout
  - [ ] Drain active connections
- [ ] Implement resource limits:
  - [ ] CPU limits (cgroups on Linux)
  - [ ] Memory limits (cgroups on Linux)
  - [ ] Job Objects on Windows

#### gRPC Infrastructure
- [ ] Configure gRPC server:
  - [ ] Unix domain socket support (Linux)
  - [ ] Named pipe support (Windows)
  - [ ] TCP fallback for Docker/Kubernetes
- [ ] Create gRPC health service
- [ ] Implement gRPC interceptors:
  - [ ] Authentication/authorization interceptor
  - [ ] CallerContext injection interceptor
  - [ ] Distributed tracing interceptor
  - [ ] Error handling interceptor
  - [ ] Logging interceptor

#### Module Loading
- [ ] Create module discovery from filesystem
- [ ] Implement module manifest loading and validation
- [ ] Create capability request validation
- [ ] Implement capability grant enforcement
- [ ] Create module configuration loading

#### Inter-Process Communication
- [ ] Define gRPC service contracts for core capabilities
- [ ] Create gRPC channel management
- [ ] Implement connection pooling
- [ ] Create timeout configuration

---

## Phase 0.7: Web Server & API Foundation

### ASP.NET Core Web Server

**REST API and web hosting infrastructure**

#### Kestrel Configuration
- [ ] Configure Kestrel server
- [ ] Set up HTTPS/TLS
- [ ] Configure listener addresses
- [ ] Set up HTTP/2 support

#### Reverse Proxy Support
- [ ] Generate IIS ANCM configuration template (`web.config`)
- [ ] Generate Apache `mod_proxy` configuration template
- [ ] Generate nginx configuration template
- [ ] Create reverse proxy documentation
- [ ] Implement configuration validation

#### API Versioning
- [ ] Set up URL-based versioning (`/api/v1/`, `/api/v2/`)
- [ ] Implement API version negotiation
- [ ] Configure version deprecation warnings
- [ ] Create API versioning documentation

#### Response Envelope
- [ ] Create standard response envelope model:
  - [ ] `bool success` property
  - [ ] `object data` property
  - [ ] `PaginationInfo pagination` property (when applicable)
- [ ] Create error response envelope:
  - [ ] `string code` property
  - [ ] `string message` property
  - [ ] `object details` property
- [ ] Implement response envelope middleware
- [ ] Create response envelope documentation

#### Error Handling
- [ ] Create error handling middleware
- [ ] Implement standard error codes
- [ ] Configure error response formatting
- [ ] Add stack trace handling (dev vs. production)
- [ ] Create error logging

#### Rate Limiting
- [ ] Implement rate limiting middleware
- [ ] Configure rate limits per module
- [ ] Create rate limit headers (X-RateLimit-*)
- [ ] Implement configurable rate limits
- [ ] Create admin configuration endpoint

#### OpenAPI/Swagger
- [ ] Integrate Swashbuckle (OpenAPI generation)
- [ ] Configure Swagger UI
- [ ] Enable OpenAPI schema generation
- [ ] Create API documentation from code comments

#### CORS
- [ ] Configure CORS policies
- [ ] Create origin whitelist configuration
- [ ] Implement allowed methods/headers
- [ ] Add credentials handling

---

## Phase 0.8: Real-Time Communication (SignalR)

### SignalR Hub Setup

**Real-time messaging infrastructure**

#### SignalR Configuration
- [ ] Configure SignalR services
- [ ] Set up connection tracking
- [ ] Configure reconnection policies
- [ ] Set up keep-alive intervals

#### Core Hub Implementation
- [ ] Create base SignalR hub with authentication/authorization
- [ ] Implement connection lifecycle handlers
- [ ] Create user connection tracking
- [ ] Implement connection grouping per channel/room

#### Real-Time Broadcast Infrastructure
- [ ] Create `IRealtimeBroadcaster` capability interface:
  - [ ] `Task BroadcastAsync(string group, object message)`
  - [ ] `Task SendToUserAsync(Guid userId, object message)`
  - [ ] `Task SendToRoleAsync(string role, object message)`
- [ ] Implement broadcast service in core
- [ ] Create module notification interface

#### Presence Tracking
- [ ] Implement presence update mechanism
- [ ] Track online/offline status
- [ ] Create last seen timestamps
- [ ] Implement presence queries

#### WebSocket Configuration
- [ ] Configure WebSocket support
- [ ] Set up WebSocket keep-alive
- [ ] Configure connection limits

---

## Phase 0.9: Authentication API Endpoints

### Core Authentication Endpoints

**REST endpoints for authentication flows**

#### User Authentication
- [ ] `POST /api/v1/core/auth/register` - User registration
- [ ] `POST /api/v1/core/auth/login` - User login (returns tokens)
- [ ] `POST /api/v1/core/auth/logout` - Revoke tokens
- [ ] `POST /api/v1/core/auth/refresh` - Refresh access token
- [ ] `GET /api/v1/core/auth/user` - Get current user info

#### OAuth2/OIDC Integration
- [ ] `GET /api/v1/core/auth/external-login/{provider}` - External provider sign-in
- [ ] `GET /api/v1/core/auth/external-callback` - External provider callback
- [ ] `GET /.well-known/openid-configuration` - OIDC discovery

#### MFA Management
- [ ] `POST /api/v1/core/auth/mfa/totp/setup` - Setup TOTP
- [ ] `POST /api/v1/core/auth/mfa/totp/verify` - Verify TOTP code
- [ ] `POST /api/v1/core/auth/mfa/passkey/setup` - Setup passkey
- [ ] `POST /api/v1/core/auth/mfa/passkey/verify` - Verify passkey
- [ ] `GET /api/v1/core/auth/mfa/backup-codes` - Generate backup codes

#### Password Management
- [ ] `POST /api/v1/core/auth/password/change` - Change password
- [ ] `POST /api/v1/core/auth/password/forgot` - Request password reset
- [ ] `POST /api/v1/core/auth/password/reset` - Reset password with token

#### Device Management
- [ ] `GET /api/v1/core/auth/devices` - List user's devices
- [ ] `DELETE /api/v1/core/auth/devices/{deviceId}` - Remove device

---

## Phase 0.10: User & Admin Management

### User Management Endpoints

- [ ] `GET /api/v1/core/users` - List users (admin only)
- [ ] `GET /api/v1/core/users/{userId}` - Get user details
- [ ] `PUT /api/v1/core/users/{userId}` - Update user profile
- [ ] `DELETE /api/v1/core/users/{userId}` - Delete user (admin only)
- [ ] `POST /api/v1/core/users/{userId}/disable` - Disable user (admin only)
- [ ] `POST /api/v1/core/users/{userId}/enable` - Enable user (admin only)
- [ ] `POST /api/v1/core/users/{userId}/reset-password` - Admin password reset

### Admin Management Endpoints

- [ ] `GET /api/v1/core/admin/settings` - List all settings
- [ ] `GET /api/v1/core/admin/settings/{key}` - Get specific setting
- [ ] `PUT /api/v1/core/admin/settings/{key}` - Update setting
- [ ] `DELETE /api/v1/core/admin/settings/{key}` - Delete setting
- [ ] `GET /api/v1/core/admin/modules` - List installed modules
- [ ] `GET /api/v1/core/admin/modules/{moduleId}` - Get module details
- [ ] `POST /api/v1/core/admin/modules/{moduleId}/start` - Start module
- [ ] `POST /api/v1/core/admin/modules/{moduleId}/stop` - Stop module
- [ ] `POST /api/v1/core/admin/modules/{moduleId}/restart` - Restart module
- [ ] `POST /api/v1/core/admin/modules/{moduleId}/capabilities/{capability}/grant` - Grant capability
- [ ] `DELETE /api/v1/core/admin/modules/{moduleId}/capabilities/{capability}` - Revoke capability
- [ ] `GET /api/v1/core/admin/health` - System health check

---

## Phase 0.11: Web UI Shell (Blazor)

### DotNetCloud.UI.Web Project

**Blazor application shell and layout**

#### Project Setup
- [ ] Create Blazor Server project (or WebAssembly with Server API)
- [ ] Set up project file with necessary dependencies
- [ ] Configure authentication/authorization services

#### Authentication Pages
- [ ] Create login page component
- [ ] Create registration page component
- [ ] Create password reset page component
- [ ] Create MFA verification page component
- [ ] Create external provider login page

#### Admin Dashboard
- [ ] Create admin layout/shell
- [ ] Create dashboard home page
- [ ] Create module management section:
  - [ ] Module list
  - [ ] Module details
  - [ ] Module action buttons (start/stop/restart)
- [ ] Create user management section:
  - [ ] User list with pagination
  - [ ] User detail view
  - [ ] User creation form
  - [ ] User editing form
- [ ] Create settings management section:
  - [ ] System settings
  - [ ] Backup/restore settings
- [ ] Create health monitoring dashboard

#### Module Plugin System
- [ ] Create dynamic component loader for modules
- [ ] Implement module navigation registration
- [ ] Create module UI extension mechanism
- [ ] Build module communication interface

#### Theme & Branding
- [ ] Create base theme/styling system
- [ ] Implement light/dark mode toggle
- [ ] Create responsive layout components
- [ ] Build reusable navigation components
- [ ] Set up brand assets/logos

#### Error & Notification UI
- [ ] Create error boundary component
- [ ] Implement exception display
- [ ] Create notification/toast system
- [ ] Implement loading indicators
- [ ] Create confirmation dialogs

---

## Phase 0.12: Shared UI Components

### DotNetCloud.UI.Shared Project

**Reusable Blazor components**

#### Form Components
- [ ] Create input text component
- [ ] Create password input component
- [ ] Create email input component
- [ ] Create select dropdown component
- [ ] Create checkbox component
- [ ] Create radio button component
- [ ] Create textarea component
- [ ] Create date picker component
- [ ] Create form validation display

#### Data Display Components
- [ ] Create data table/grid component
  - [ ] Sorting
  - [ ] Filtering
  - [ ] Pagination
- [ ] Create paginator component
- [ ] Create breadcrumb component
- [ ] Create tabs component
- [ ] Create accordion component

#### Dialog Components
- [ ] Create modal dialog component
- [ ] Create confirmation dialog component
- [ ] Create alert dialog component

#### Navigation Components
- [ ] Create sidebar navigation component
- [ ] Create top navigation bar component
- [ ] Create menu component
- [ ] Create button component with variants

#### Notification Components
- [ ] Create toast notification component
- [ ] Create alert component
- [ ] Create badge component

#### Layout Components
- [ ] Create card component
- [ ] Create panel component
- [ ] Create section component
- [ ] Create responsive grid component

#### Styling
- [ ] Create CSS/SCSS base styles
- [ ] Set up theme color variables
- [ ] Create utility classes
- [ ] Implement responsive breakpoints

---

## Phase 0.13: CLI Management Tool

### DotNetCloud.CLI Project

**Command-line interface for administration**

#### Project Setup
- [ ] Create console application project
- [ ] Integrate System.CommandLine library
- [ ] Set up command structure

#### Core Commands

##### Setup Command
- [ ] `dotnetcloud setup` - Interactive first-run wizard
  - [ ] Database selection (PostgreSQL/SQL Server/MariaDB)
  - [ ] Connection string configuration
  - [ ] Admin user creation
  - [ ] Admin MFA setup
  - [ ] Organization setup
  - [ ] TLS/HTTPS configuration
  - [ ] Let's Encrypt setup (optional)
  - [ ] Module selection
  - [ ] Save configuration

##### Service Commands
- [ ] `dotnetcloud serve` - Start all services
- [ ] `dotnetcloud stop` - Graceful shutdown
- [ ] `dotnetcloud status` - Show service & module status
- [ ] `dotnetcloud restart` - Restart all services

##### Module Commands
- [ ] `dotnetcloud module list` - List all modules
- [ ] `dotnetcloud module start {module}` - Start specific module
- [ ] `dotnetcloud module stop {module}` - Stop specific module
- [ ] `dotnetcloud module restart {module}` - Restart specific module
- [ ] `dotnetcloud module install {module}` - Install module
- [ ] `dotnetcloud module uninstall {module}` - Uninstall module

##### Component Commands
- [ ] `dotnetcloud component status {component}` - Check component status
- [ ] `dotnetcloud component restart {component}` - Restart component

##### Logging Commands
- [ ] `dotnetcloud logs` - View system logs
- [ ] `dotnetcloud logs {module}` - View module-specific logs
- [ ] `dotnetcloud logs --level {level}` - Filter by log level

##### Backup Commands
- [ ] `dotnetcloud backup` - Create backup
- [ ] `dotnetcloud backup --output {path}` - Backup to specific location
- [ ] `dotnetcloud restore {file}` - Restore from backup
- [ ] `dotnetcloud backup --schedule daily` - Schedule automatic backups

##### Miscellaneous Commands
- [ ] `dotnetcloud update` - Check and apply updates
- [ ] `dotnetcloud help` - Show command reference
- [ ] `dotnetcloud help {command}` - Show command-specific help

---

## Phase 0.14: Example Module Reference

### DotNetCloud.Modules.Example Project

**Reference implementation of a module**

#### Module Structure
- [ ] Create `DotNetCloud.Modules.Example` (core logic)
- [ ] Create `DotNetCloud.Modules.Example.Data` (EF Core context)
- [ ] Create `DotNetCloud.Modules.Example.Host` (gRPC host)

#### Module Implementation
- [ ] Create `ExampleModuleManifest` implementing `IModuleManifest`
- [ ] Create example data model
- [ ] Create `ExampleDbContext` extending `DbContext`
- [ ] Implement module initialization
- [ ] Create example API endpoints
- [ ] Create example capability interface usage
- [ ] Create example event publishing/subscription

#### Blazor UI Components
- [ ] Create example module page
- [ ] Create example data display
- [ ] Create example form

#### gRPC Service
- [ ] Define `.proto` service
- [ ] Implement gRPC service
- [ ] Create health check implementation

#### Documentation
- [ ] Create inline code documentation
- [ ] Write module-specific README
- [ ] Document manifest and capabilities
- [ ] Provide example usage patterns

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

- ☐ Create `DotNetCloud.Integration.Tests` project
- ☐ Set up Docker for database testing
- ☐ Create database initialization scripts
- ☐ Build multi-database test matrix:
  - ☐ PostgreSQL tests
  - ☐ SQL Server tests
  - ☐ MariaDB tests
- ☐ Create gRPC client test helpers
- ☐ Build API integration test framework
- ☐ Create test data builders

### Test Coverage

- ✓ Establish comprehensive unit tests for Phase 0.1 (80%+ coverage)
- ✓ Create coverage reporting framework
- ☐ Set up CI/CD coverage checks

---

## Phase 0.16: Internationalization (i18n) Infrastructure

### i18n Setup

- [ ] Create resource files structure (`Resources/*.resx`)
- [ ] Configure `IStringLocalizer` dependency injection
- [ ] Create translation key constants
- [ ] Set up default language (English)
- [ ] Implement user locale selection
- [ ] Configure number/date/time formatting per locale
- [ ] Create Blazor component for locale switching
- [ ] Document translation contribution process

### Resource Files

- [ ] Create core UI strings
- [ ] Create error message strings
- [ ] Create validation message strings
- [ ] Create module strings (namespace per module)
- [ ] Set up translation workflow (Weblate or similar)

---

## Phase 0.17: Logging & Observability

### Logging Configuration

- [ ] Configure Serilog in all projects
- [ ] Set up log levels (Debug, Information, Warning, Error, Fatal)
- [ ] Configure file logging:
  - [ ] Log file rotation
  - [ ] Retention policies
- [ ] Set up structured logging
- [ ] Create context enrichment (user ID, request ID, module)

### Health Checks

- [ ] Create `/health` endpoint returning module status
- [ ] Implement liveness probe
- [ ] Implement readiness probe
- [ ] Add to admin dashboard

### Metrics & Tracing

- [ ] Configure OpenTelemetry collectors
- [ ] Set up Prometheus metrics export (future phase)
- [ ] Implement distributed tracing
- [ ] Configure trace exporters

---

## Phase 0.18: CI/CD Pipeline Setup

### Build Pipeline

- [ ] Create build workflow
- [ ] Implement project compilation
- [ ] Set up artifact generation
- [ ] Configure build caching

### Test Pipeline

- [ ] Create unit test workflow
- [ ] Set up multi-database integration tests
- [ ] Configure code coverage reporting
- [ ] Set up coverage gates

### Package Pipeline (Skeleton)

- [ ] Create `.deb` package build script
- [ ] Create `.rpm` package build script
- [ ] Create Windows MSI build script
- [ ] Create Docker image build

---

## Phase 0.19: Documentation

### Core Documentation

- [ ] Architecture overview documentation
- [ ] Development environment setup guide
- [ ] Running tests documentation
- [ ] Contributing guidelines
- [ ] License documentation

### API Documentation

- [ ] API endpoint reference
- [ ] Authentication flow documentation
- [ ] Response format documentation
- [ ] Error handling documentation

### Module Development Guide (Skeleton)

- [ ] Module architecture overview
- [ ] Creating a module
- [ ] Module manifest documentation
- [ ] Capability interfaces documentation

---

## Phase 0 Completion Checklist

### Functionality Verification

- [ ] All projects compile without errors
- [ ] All unit tests pass
- [ ] All integration tests pass against PostgreSQL
- [ ] All integration tests pass against SQL Server
- [ ] All integration tests pass against MariaDB
- [ ] No compiler warnings (or documented exceptions)

### Authentication & Authorization

- [ ] User registration works
- [ ] User login works
- [ ] TOTP MFA works
- [ ] Token refresh works
- [ ] Admin authentication works
- [ ] Permission checks work

### Module System

- [ ] Example module loads successfully
- [ ] Health checks pass
- [ ] Module manifest validation works
- [ ] Capability system works
- [ ] Event bus works

### Web UI

- [ ] Login page displays
- [ ] Admin dashboard displays
- [ ] User can log in and see dashboard
- [ ] Module list displays correctly
- [ ] Settings pages display

### CLI

- [ ] `dotnetcloud setup` wizard runs
- [ ] Configuration is saved correctly
- [ ] `dotnetcloud serve` starts services
- [ ] `dotnetcloud status` displays correctly
- [ ] `dotnetcloud help` works

### Deployment

- [ ] Application runs on Windows
- [ ] Application runs on Linux
- [ ] Logs are written to file
- [ ] Health checks are working

---

## Phase 1: Files (Public Launch)

**Goal:** File upload/download/browse/share + working desktop sync client.

**Expected Duration:** 8-12 weeks

### Phase 1 Overview

This phase implements the core Files module, which is the primary public-facing feature. It includes:

1. File storage and management backend
2. File browser UI
3. Desktop sync client (SyncService, SyncTray)
4. Collabora CODE integration for online document editing
5. Complete REST API with bulk operations
6. Comprehensive documentation

### Phase 1.1-1.20: [Detailed breakdown continues...]

> **Note:** Due to length constraints, detailed sections for Phases 1-9 follow the same structure as Phase 0. Each section includes:
> - Subsystem breakdown (Database, Business Logic, API, UI, etc.)
> - Individual task checklists
> - Integration points
> - Testing requirements
> - Documentation needs

---

## Phase 2: Chat & Notifications

**Goal:** Real-time messaging + Android app.

**Expected Duration:** 10-14 weeks

### Subsystems to Implement

1. Chat module (channels, DMs, typing, presence, file sharing)
2. Announcements module
3. Chat UI (web, desktop, Android)
4. Android MAUI app
5. Push notifications (FCM/UnifiedPush)
6. SignalR real-time delivery

---

## Phase 3: Contacts, Calendar & Notes

**Goal:** Personal information management + standards compliance.

**Expected Duration:** 8-10 weeks

### Subsystems to Implement

1. Contacts module (vCard, CardDAV)
2. Calendar module (CalDAV)
3. Notes module (Markdown)
4. NextCloud migration tool
5. Standards compliance testing

---

## Phase 4: Project Management (Deck)

**Goal:** Kanban boards + Jira-like project tracking.

**Expected Duration:** 10-12 weeks

---

## Phase 5: Media (Photos, Music, Video)

**Goal:** Media management and playback.

**Expected Duration:** 10-12 weeks

---

## Phase 6: Email & Bookmarks

**Goal:** Integrated email + browser bookmark sync.

**Expected Duration:** 8-10 weeks

---

## Phase 7: Video Calling & Screen Sharing

**Goal:** Full video conferencing.

**Expected Duration:** 6-8 weeks

---

## Phase 8: Search, Auto-Updates & Polish

**Goal:** Cross-module search, automated updates, encryption, production hardening.

**Expected Duration:** 8-10 weeks

---

## Phase 9: AI Assistant

**Goal:** LLM-powered assistant with local and cloud provider support.

**Expected Duration:** 6-8 weeks

### Detailed Implementation

#### DotNetCloud.Modules.AI Module

- [ ] Create AI module project structure
- [ ] Create `AIModuleManifest`
- [ ] Create `ILlmProvider` capability interface
- [ ] Implement provider abstraction layer

#### Ollama Integration

- [ ] Integrate Microsoft.Extensions.AI.Ollama
- [ ] Implement Ollama provider
- [ ] Create model management UI
- [ ] Implement connection validation
- [ ] Add model listing and pulling

#### Cloud Provider Support

- [ ] Integrate Microsoft.Extensions.AI.OpenAI
- [ ] Create Anthropic Claude provider (if no .NET SDK available)
- [ ] Implement API key management (encrypted storage)
- [ ] Create provider configuration UI
- [ ] Implement rate limiting per user

#### Admin Configuration

- [ ] Create provider configuration panel
- [ ] Implement model selection
- [ ] Add provider fallback chain configuration
- [ ] Create usage tracking & reporting
- [ ] Implement audit logging

#### User Interface

- [ ] Create AI assistant chat panel
- [ ] Implement streaming responses via SignalR
- [ ] Create model selector dropdown
- [ ] Add context injection mechanism
- [ ] Implement conversation history

#### Cross-Module Integration

- [ ] Add AI summarization for Notes
- [ ] Add smart replies for Chat
- [ ] Add draft generation for Email
- [ ] Add content summarization for Files
- [ ] Add semantic search enhancement

---

## Infrastructure & DevOps

### Deployment Modes

#### Bare Metal Setup

- [ ] Create systemd service files
- [ ] Implement FHS-compliant directory layout
- [ ] Create systemd socket activation (for Unix sockets)
- [ ] Implement auto-restart on crash
- [ ] Create log rotation configuration

#### Docker Compose Setup

- [ ] Generate `docker-compose.yml` template
- [ ] Create Docker build configuration
- [ ] Implement multi-stage builds for optimization
- [ ] Add docker-compose overrides for various configurations

#### Kubernetes Setup

- [ ] Create Helm chart structure
- [ ] Define Kubernetes manifests per component
- [ ] Implement service discovery
- [ ] Set up persistent volume claims
- [ ] Create ingress configuration

### Reverse Proxy Configuration

#### IIS (Windows)

- [ ] Create ANCM configuration generator
- [ ] Generate `web.config` templates
- [ ] Implement URL rewriting rules
- [ ] Set up WebSocket proxying

#### Apache (Linux)

- [ ] Create Apache VirtualHost configuration generator
- [ ] Implement `mod_proxy` setup
- [ ] Set up `mod_proxy_wstunnel` for WebSockets
- [ ] Create SSL/TLS configuration

#### nginx (Linux/macOS)

- [ ] Create nginx configuration generator
- [ ] Implement upstream server configuration
- [ ] Set up WebSocket support
- [ ] Create SSL/TLS configuration

### TLS & Let's Encrypt

- [ ] Integrate Certbot or similar
- [ ] Implement automatic certificate provisioning
- [ ] Set up certificate renewal automation
- [ ] Create renewal failure alerts
- [ ] Document manual certificate installation

### Linux Installation

#### One-Line Install Script

- [ ] Create bash install script
- [ ] Handle dependency installation
- [ ] Create automated setup
- [ ] Add error handling and rollback

#### Package Manager Integration

- [ ] Create APT repository structure
- [ ] Generate Debian packages (`.deb`)
- [ ] Create RPM packages (`.rpm`)
- [ ] Set up repository signing with GPG
- [ ] Document package installation

#### Unattended Installation

- [ ] Create configuration file templates
- [ ] Implement headless setup mode
- [ ] Document Ansible/Terraform integration
- [ ] Create cloud-init support

### Windows Installation

- [ ] Create MSI installer
- [ ] Implement WinGet package
- [ ] Set up Windows Service registration
- [ ] Create auto-start on boot
- [ ] Implement uninstaller

### Monitoring & Alerting

- [ ] Create health check alerts
- [ ] Set up log aggregation hooks
- [ ] Implement performance monitoring
- [ ] Create backup verification
- [ ] Add uptime monitoring

---

## Documentation & Support

### Administration Documentation

- [ ] Installation guides (Windows, Linux, Docker, Kubernetes)
- [ ] Configuration reference
- [ ] Module management guide
- [ ] Backup and restore procedures
- [ ] Troubleshooting guide
- [ ] Performance tuning guide
- [ ] Security hardening guide
- [ ] Multi-organization setup (future)

### Developer Documentation

- [ ] Module development guide (10 chapters)
- [ ] API reference documentation
- [ ] Architecture deep dives
- [ ] Database schema documentation
- [ ] gRPC service documentation
- [ ] Contributing guidelines
- [ ] Release process documentation

### User Documentation

- [ ] Getting started guide
- [ ] File sync user guide
- [ ] Desktop client guide
- [ ] Android app guide
- [ ] Chat guide
- [ ] Calendar/Contacts guide
- [ ] FAQ

### Deployment Documentation

- [ ] Reverse proxy setup guides
- [ ] Docker Compose guide
- [ ] Kubernetes deployment guide
- [ ] High availability setup
- [ ] Disaster recovery guide

---

## Cross-Cutting Concerns

### Security

- [ ] Implement input validation everywhere
- [ ] Add output encoding for XSS prevention
- [ ] Implement CSRF protection
- [ ] Add SQL injection prevention (via EF Core)
- [ ] Implement rate limiting on all endpoints
- [ ] Add account lockout mechanisms
- [ ] Implement audit logging for sensitive operations
- [ ] Set up security headers
- [ ] Create vulnerability reporting process
- [ ] Perform security audit (Phase 8)

### Performance

- [ ] Implement database query optimization
- [ ] Add caching strategies (Redis or in-memory)
- [ ] Optimize file transfer (chunking, deduplication)
- [ ] Profile critical paths
- [ ] Load testing and benchmarking
- [ ] Connection pooling optimization
- [ ] Memory leak detection and fixing

### Reliability

- [ ] Implement comprehensive error handling
- [ ] Add retry logic with exponential backoff
- [ ] Create graceful degradation mechanisms
- [ ] Implement circuit breakers
- [ ] Add health checks and monitoring
- [ ] Create backup and recovery procedures
- [ ] Implement data validation
- [ ] Add data consistency checks

### Maintainability

- [ ] Follow consistent code style (use `.editorconfig`)
- [ ] Write comprehensive comments for complex logic
- [ ] Create architectural decision records (ADRs)
- [ ] Implement logging for debugging
- [ ] Create runbooks for common operations
- [ ] Document trade-offs and limitations
- [ ] Keep dependencies up to date

### Testing Strategy

- [ ] Unit test coverage ≥ 80%
- [ ] Integration tests for all major features
- [ ] End-to-end tests for critical workflows
- [ ] Performance tests for bottlenecks
- [ ] Security tests (OWASP Top 10)
- [ ] Chaos engineering tests (Phase 8+)
- [ ] Accessibility tests (Phase 5+)

---

## Legend & Notes

- **[ ]** - Unchecked task (not started)
- **[x]** - Completed task
- **[~]** - In progress or partially completed

### Task Estimation

- **Small tasks** (~4-8 hours): Individual API endpoint, simple component
- **Medium tasks** (~1-3 days): Complete feature, module subsystem
- **Large tasks** (~1-2 weeks): Full module, major infrastructure component
- **Epic tasks** (2+ weeks): Complete phase, cross-cutting concern

### Dependencies Between Phases

- Phases 0 → All other phases (foundational)
- Phase 1 → Phases 2-9 (core infrastructure)
- Phase 2 → Phases 3-6 (communication foundation)
- Phase 8 depends on → Phases 1-7 (integration)

### Review Process

Before marking a phase complete:

1. [ ] All tasks are checked
2. [ ] All tests pass
3. [ ] Code review completed
4. [ ] Documentation is updated
5. [ ] Performance benchmarks met
6. [ ] Security audit passed
7. [ ] Milestone criteria verified
8. [ ] Release notes prepared

---

**Document Maintenance:** This checklist should be updated as implementation progresses, with status updates and task refinements captured in Git history via commit messages and pull request descriptions.

**Last Reviewed:** 2026-03-02
**Next Review:** Upon Phase 0 completion
