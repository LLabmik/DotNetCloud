# Authentication & Authorization

> **Purpose:** Detailed documentation of DotNetCloud authentication flows  
> **Stack:** ASP.NET Core Identity + OpenIddict (OAuth2/OIDC)  
> **Last Updated:** 2026-03-03

---

## Table of Contents

1. [Architecture Overview](#architecture-overview)
2. [Authentication Flows by Client](#authentication-flows-by-client)
3. [User Registration](#user-registration)
4. [Email/Password Login](#emailpassword-login)
5. [Multi-Factor Authentication](#multi-factor-authentication)
6. [Token Lifecycle](#token-lifecycle)
7. [External Provider Login](#external-provider-login)
8. [Password Management](#password-management)
9. [Authorization & Permissions](#authorization--permissions)
10. [Security Considerations](#security-considerations)

---

## Architecture Overview

DotNetCloud uses a two-layer authentication stack:

| Layer | Technology | Purpose |
|---|---|---|
| **User/role storage** | ASP.NET Core Identity | User management, password hashing, MFA TOTP, lockout, email confirmation |
| **OAuth2/OIDC server** | OpenIddict (Apache 2.0) | Issues access/refresh/ID tokens; authorization code + PKCE; client credentials |
| **Federation** | ASP.NET Core External Authentication | Sign in with Google, Microsoft, GitHub; enterprise SAML/OIDC |
| **MFA** | Identity TOTP + Fido2NetLib | Authenticator apps + hardware keys/passkeys |

### Why OpenIddict

OpenIddict is Apache 2.0 licensed (free for all uses). Duende IdentityServer requires a paid license for production revenue over $1M, which is incompatible with an open-source project that users self-host.

---

## Authentication Flows by Client

| Client Type | Flow | Details |
|---|---|---|
| **Blazor Web UI** | Cookie-based session via OpenIddict | Server-side session, no client-side token storage |
| **Avalonia Desktop** | OAuth2 Authorization Code + PKCE | Opens system browser for login |
| **Android MAUI** | OAuth2 Authorization Code + PKCE | Chrome Custom Tab for login |
| **Sync Service** | Refresh token (long-lived) | Initial auth via PKCE, then persistent refresh token |
| **Third-party apps** | OAuth2 Client Credentials or Auth Code | Standard OAuth2 flows |

---

## User Registration

### Flow

```
Client                          Server
  │                               │
  │  POST /api/v1/core/auth/register
  │  { email, password, displayName }
  │─────────────────────────────► │
  │                               │── Validate input
  │                               │── Check email uniqueness
  │                               │── Hash password (ASP.NET Core Identity)
  │                               │── Create ApplicationUser
  │                               │── Assign default roles
  │  200 { userId, email }        │
  │◄───────────────────────────── │
```

### Validation Rules

- **Email:** Must be a valid email format; must be unique in the system
- **Password:** Must meet ASP.NET Core Identity requirements (uppercase, lowercase, digit, special character, minimum length)
- **Display Name:** Required, non-empty string

### Error Responses

| Code | HTTP Status | Cause |
|---|---|---|
| `VALIDATION_ERROR` | 400 | Input validation failed |
| `REGISTRATION_FAILED` | 400 | Email already in use or other constraint violation |

---

## Email/Password Login

### Flow (Without MFA)

```
Client                          Server
  │                               │
  │  POST /api/v1/core/auth/login
  │  { email, password }
  │─────────────────────────────► │
  │                               │── Validate credentials (Identity)
  │                               │── Check account lockout
  │                               │── Check MFA requirement
  │                               │── Generate access + refresh tokens
  │  200 { accessToken,           │
  │        refreshToken,          │
  │        expiresIn, userId }    │
  │◄───────────────────────────── │
```

### Flow (With MFA)

```
Client                          Server
  │                               │
  │  POST /api/v1/core/auth/login
  │  { email, password }
  │─────────────────────────────► │
  │                               │── Validate credentials ✓
  │                               │── Detect MFA enabled
  │  202 { code: "MFA_REQUIRED" } │
  │◄───────────────────────────── │
  │                               │
  │  POST /api/v1/core/auth/mfa/totp/verify
  │  { code: "123456" }
  │─────────────────────────────► │
  │                               │── Verify TOTP code
  │  200 { verified }             │
  │◄───────────────────────────── │
```

### Error Responses

| Code | HTTP Status | Cause |
|---|---|---|
| `INVALID_CREDENTIALS` | 401 | Wrong email or password |
| `MFA_REQUIRED` | 202 | Valid credentials but MFA step required |
| `AUTH_ACCOUNT_LOCKED` | 401 | Too many failed attempts |

---

## Multi-Factor Authentication

### Supported Methods

| Method | Status | Description |
|---|---|---|
| **TOTP** | ✅ Implemented | Time-based one-time password (authenticator apps) |
| **Passkeys (FIDO2)** | 🔲 Skeleton | WebAuthn/FIDO2 hardware keys (entity ready, integration pending) |
| **Backup Codes** | ✅ Implemented | One-time recovery codes (stored as SHA-256 hashes) |

### TOTP Setup Flow

```
Client                          Server
  │                               │
  │  POST /api/v1/core/auth/mfa/totp/setup
  │─────────────────────────────► │
  │                               │── Generate shared key
  │                               │── Create provisioning URI
  │  200 { sharedKey, qrCodeUri } │
  │◄───────────────────────────── │
  │                               │
  │  [User scans QR code with     │
  │   authenticator app]          │
  │                               │
  │  POST /api/v1/core/auth/mfa/totp/verify
  │  { code: "123456" }
  │─────────────────────────────► │
  │                               │── Verify code matches shared key
  │                               │── Enable MFA on user account
  │  200 { verified }             │
  │◄───────────────────────────── │
```

### Backup Codes

Generate 10 one-time backup codes for account recovery when the authenticator app is unavailable:

```
GET /api/v1/core/auth/mfa/backup-codes
```

Codes are shown **once** and stored as SHA-256 hashes in the database.

---

## Token Lifecycle

### Token Types

| Token | Lifetime | Storage |
|---|---|---|
| **Access Token** | Short-lived (default: 1 hour) | Client memory or `Authorization: Bearer` header |
| **Refresh Token** | Long-lived (default: 14 days) | Client secure storage |
| **ID Token** | Short-lived | Client (OIDC identity claims) |

### Token Refresh Flow

```
Client                          Server
  │                               │
  │  POST /api/v1/core/auth/refresh
  │  { refreshToken }
  │─────────────────────────────► │
  │                               │── Validate refresh token
  │                               │── Issue new access token
  │                               │── Rotate refresh token
  │  200 { accessToken,           │
  │        refreshToken,          │
  │        expiresIn }            │
  │◄───────────────────────────── │
```

### Token Revocation

Tokens can be revoked via:

- **User logout:** `POST /api/v1/core/auth/logout`
- **Admin action:** Disabling a user account
- **OAuth2 revocation:** `POST /connect/revoke`

---

## External Provider Login

### Supported Providers

External providers are configured in `appsettings.json`. Common providers include:

- Google
- Microsoft
- GitHub

### Flow

```
Client                          Server                          Provider
  │                               │                               │
  │  GET /api/v1/core/auth/       │                               │
  │      external-login/{provider}│                               │
  │─────────────────────────────► │                               │
  │                               │── Build challenge URL          │
  │  302 → Provider Login Page    │                               │
  │◄──────────────────────────── │                               │
  │                               │                               │
  │  [User authenticates with     │                               │
  │   external provider]          │                               │
  │                               │                               │
  │  GET /api/v1/core/auth/external-callback                      │
  │  (with auth code from provider)                               │
  │─────────────────────────────► │                               │
  │                               │── Exchange code for tokens ──►│
  │                               │◄── User info ─────────────── │
  │                               │── Find or create local user   │
  │                               │── Issue DotNetCloud tokens     │
  │  200 { accessToken, ... }     │                               │
  │◄───────────────────────────── │                               │
```

---

## Password Management

### Change Password

Authenticated users can change their password:

```
POST /api/v1/core/auth/password/change
{ "currentPassword": "...", "newPassword": "..." }
```

### Forgot Password

Request a password reset email:

```
POST /api/v1/core/auth/password/forgot
{ "email": "user@example.com" }
```

> **Security:** This endpoint always returns `200 OK` regardless of whether the email exists, to prevent email enumeration.

### Reset Password

Complete the reset using the token from the email:

```
POST /api/v1/core/auth/password/reset
{ "email": "user@example.com", "token": "...", "newPassword": "..." }
```

---

## Authorization & Permissions

### Role-Based Access Control

DotNetCloud uses role-based authorization with ASP.NET Core Identity roles.

| Role | Description | Privileges |
|---|---|---|
| `admin` | System administrator | Full access to all admin endpoints |
| `user` | Standard user | Access to own profile and authorized modules |

### Authorization Policies

| Policy | Description | Applied To |
|---|---|---|
| `RequireAdmin` | Requires the `admin` role | Admin endpoints (`/api/v1/core/admin/*`) |
| `RequireAuthenticated` | Requires a valid token | User endpoints, MFA, device management |

### CallerContext

Every authenticated request builds a `CallerContext` that flows through the system:

```csharp
public record CallerContext(
    Guid UserId,
    IReadOnlyList<string> Roles,
    CallerType Type  // User, System, or Module
);
```

This context is used by:
- Capability tier enforcement (module permissions)
- Event bus filtering
- Audit logging

---

## Security Considerations

### Password Storage

Passwords are hashed using ASP.NET Core Identity's default hasher (PBKDF2 with HMAC-SHA256, 100k iterations).

### Account Lockout

After configurable failed login attempts, the account is locked for a configurable duration. Lockout settings are in `appsettings.json`.

### Token Security

- Access tokens are **JWT** format (default in OpenIddict 5.x)
- Development uses **ephemeral signing keys** (auto-generated on each restart)
- Production requires configured signing keys (RSA or ECDSA)

### Anti-Enumeration

- Registration does not reveal whether an email is already registered (returns generic error)
- Password reset always returns success regardless of email existence

### OIDC Discovery

The standard discovery document is available at:

```
GET /.well-known/openid-configuration
```

This exposes supported grant types, scopes, endpoints, and signing keys.

---

**See also:**

- [API Endpoint Reference](README.md)
- [Response Format](RESPONSE_FORMAT.md)
- [Error Handling](ERROR_HANDLING.md)
- [Architecture Overview](../architecture/ARCHITECTURE.md)
