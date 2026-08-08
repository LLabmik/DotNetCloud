# DotNetCloud.Core.Auth

> **Purpose:** Authentication, authorization, and identity management layer for the DotNetCloud platform
> **Type:** Library
> **Target Framework:** net10.0

## Overview

`DotNetCloud.Core.Auth` provides the complete authentication and authorization stack for DotNetCloud. It integrates OpenIddict for OAuth 2.0 / OpenID Connect, ASP.NET Core Identity for user management, and implements the capability-based authorization system that mediates all module access. It also provides token introspection for gRPC inter-module communication.

## Key Features

- **OpenIddict Integration** — OAuth 2.0 / OIDC server and validation with JWT bearer tokens
- **ASP.NET Core Identity** — User, role, and claim management with Entity Framework Core backing
- **Multi-Factor Authentication (MFA)** — TOTP-based MFA with recovery codes
- **Capability Authorization** — Policy-based authorization handlers enforcing the capability tier model (Public → Restricted → Privileged)
- **User & Organization Directory** — `IUserDirectory`, `IOrganizationDirectory`, `ITeamDirectory`, `IGroupDirectory` capability implementations
- **User & Team Management** — `IUserManager`, `ITeamManager`, `IGroupManager` privileged capability implementations
- **Device Tracking** — Device registration and management per user
- **Token Introspection** — gRPC-based token validation for inter-module calls
- **Email Delivery** — SMTP email sender for password reset, MFA codes, and notifications
- **OIDC Key Rotation** — Automatic signing key rotation for OpenIddict

## Projects This Interacts With

### Direct Dependencies (Project References)
- `DotNetCloud.Core` — Core interfaces (capability system, module contracts, caller context)
- `DotNetCloud.Core.Data` — EF Core entities (Identity, Organizations, Permissions, Settings)
- `DotNetCloud.Core.Grpc` — gRPC token introspection proto definitions

### Dependent Projects (Projects that reference this one)
- `DotNetCloud.Core.Server` — Consumes auth services for the main server pipeline
- `DotNetCloud.UI.Web` — Uses auth for the Blazor web UI

## Key Files

| File | Purpose |
|------|---------|
| `Services/AuthService.cs` | Core authentication service: login, logout, token issuance |
| `Services/MfaService.cs` | TOTP-based multi-factor authentication |
| `Services/UserManagementService.cs` | User CRUD, password management, account lifecycle |
| `Services/DeviceService.cs` | Device registration, tracking, and revocation |
| `Services/OidcKeyRotationService.cs` | OpenIddict signing key rotation |
| `Services/SmtpEmailSender.cs` | Email delivery via SMTP |
| `Authorization/PermissionAuthorizationHandler.cs` | Policy-based capability authorization handler |
| `Capabilities/UserDirectoryService.cs` | Implements `IUserDirectory` capability |
| `Capabilities/UserManagerService.cs` | Implements `IUserManager` privileged capability |
| `Introspection/` | gRPC token introspection endpoint |
