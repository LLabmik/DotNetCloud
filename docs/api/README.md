<p align="center">
  <img src="../../assets/logo.png" alt="DotNetCloud" width="64" />
</p>

# DotNetCloud API Reference

> **Base URL:** `https://your-domain.com/api/v1`  
> **API Version:** v1 (URL-based versioning)  
> **Content Type:** `application/json`  
> **Authentication:** Bearer token (JWT) or cookie-based session  
> **Last Updated:** 2026-03-03

---

## Table of Contents

1. [Overview](#overview)
2. [Authentication Endpoints](#authentication-endpoints)
3. [MFA Endpoints](#mfa-endpoints)
4. [Device Endpoints](#device-endpoints)
5. [User Management Endpoints](#user-management-endpoints)
6. [Admin Endpoints](#admin-endpoints)
7. [Health Endpoints](#health-endpoints)
8. [OpenID Connect Endpoints](#openid-connect-endpoints)
9. [Real-Time (SignalR)](#real-time-signalr)
10. [Interactive API Explorer](#interactive-api-explorer)

---

## Overview

All API responses follow the [standard response envelope](RESPONSE_FORMAT.md). Errors are documented in the [error handling guide](ERROR_HANDLING.md). Authentication flows are described in the [authentication guide](AUTHENTICATION.md).

### Versioning

API endpoints are versioned via URL path prefix:

```
/api/v1/core/auth/login
/api/v2/core/auth/login   (future)
```

When a version is deprecated, responses include an `api-deprecated-versions` header.

### Rate Limiting

All endpoints are rate-limited. Rate limit status is returned in response headers:

| Header | Description |
|---|---|
| `X-RateLimit-Limit` | Maximum requests per window |
| `X-RateLimit-Remaining` | Remaining requests in current window |
| `X-RateLimit-Reset` | UTC timestamp when the window resets |

---

## Authentication Endpoints

Base path: `/api/v1/core/auth`

| Method | Path | Auth | Description |
|---|---|---|---|
| `POST` | `/register` | None | Register a new user account |
| `POST` | `/login` | None | Log in with email and password |
| `POST` | `/logout` | Bearer | Log out and revoke tokens |
| `POST` | `/refresh` | None | Refresh an access token |
| `GET` | `/user` | Bearer | Get the current authenticated user's profile |
| `GET` | `/external-login/{provider}` | None | Initiate external provider authentication |
| `GET` | `/external-callback` | None | Handle external provider callback |

### POST `/register`

Register a new user account.

**Request Body:**

```json
{
  "email": "user@example.com",
  "password": "SecureP@ssw0rd!",
  "displayName": "Jane Doe"
}
```

**Success Response (200):**

```json
{
  "success": true,
  "data": {
    "userId": "550e8400-e29b-41d4-a716-446655440000",
    "email": "user@example.com"
  }
}
```

### POST `/login`

Authenticate with email and password.

**Request Body:**

```json
{
  "email": "user@example.com",
  "password": "SecureP@ssw0rd!"
}
```

**Success Response (200):**

```json
{
  "success": true,
  "data": {
    "accessToken": "eyJhbGciOi...",
    "refreshToken": "dGhpcyBpcyBh...",
    "expiresIn": 3600,
    "userId": "550e8400-e29b-41d4-a716-446655440000"
  }
}
```

**MFA Required (202):**

```json
{
  "success": false,
  "error": {
    "code": "MFA_REQUIRED",
    "message": "Multi-factor authentication required"
  }
}
```

### POST `/logout`

Revoke the current user's tokens. Requires a valid Bearer token.

**Success Response (200):**

```json
{
  "success": true,
  "message": "Logged out successfully."
}
```

### POST `/refresh`

Refresh an expired access token.

**Request Body:**

```json
{
  "refreshToken": "dGhpcyBpcyBh..."
}
```

**Success Response (200):**

```json
{
  "success": true,
  "data": {
    "accessToken": "eyJhbGciOi...",
    "refreshToken": "bmV3IHJlZnJl...",
    "expiresIn": 3600
  }
}
```

### GET `/user`

Get the current authenticated user's profile.

**Success Response (200):**

```json
{
  "success": true,
  "data": {
    "userId": "550e8400-e29b-41d4-a716-446655440000",
    "email": "user@example.com",
    "displayName": "Jane Doe",
    "locale": "en-US",
    "timezone": "America/New_York",
    "isActive": true
  }
}
```

---

## MFA Endpoints

Base path: `/api/v1/core/auth/mfa`

All MFA endpoints require a valid Bearer token.

| Method | Path | Description |
|---|---|---|
| `POST` | `/totp/setup` | Get TOTP authenticator setup info (shared key, QR URI) |
| `POST` | `/totp/verify` | Verify a TOTP code |
| `POST` | `/totp/disable` | Disable TOTP authentication |
| `POST` | `/passkey/setup` | Initiate passkey (FIDO2/WebAuthn) registration |
| `POST` | `/passkey/verify` | Verify a passkey assertion |
| `GET` | `/backup-codes` | Generate new backup codes |
| `GET` | `/status` | Get MFA status for the current user |

### POST `/totp/verify`

**Request Body:**

```json
{
  "code": "123456"
}
```

**Success Response (200):**

```json
{
  "success": true,
  "message": "TOTP verified successfully."
}
```

### GET `/status`

**Success Response (200):**

```json
{
  "success": true,
  "data": {
    "userId": "550e8400-e29b-41d4-a716-446655440000",
    "isMfaEnabled": true,
    "methods": ["totp"]
  }
}
```

---

## Device Endpoints

Base path: `/api/v1/core/auth/devices`

All device endpoints require a valid Bearer token.

| Method | Path | Description |
|---|---|---|
| `GET` | `/` | List all devices registered by the current user |
| `DELETE` | `/{deviceId}` | Remove a registered device |

---

## User Management Endpoints

Base path: `/api/v1/core/users`

| Method | Path | Auth | Description |
|---|---|---|---|
| `GET` | `/` | Admin | List users with pagination and filtering |
| `GET` | `/{userId}` | Bearer | Get user details (own profile or admin) |
| `PUT` | `/{userId}` | Bearer | Update user profile (own profile or admin) |
| `DELETE` | `/{userId}` | Admin | Delete a user |
| `POST` | `/{userId}/disable` | Admin | Disable a user account |
| `POST` | `/{userId}/enable` | Admin | Enable a user account |
| `POST` | `/{userId}/reset-password` | Admin | Force reset a user's password |

### GET `/` — List Users

**Query Parameters:**

| Parameter | Type | Default | Description |
|---|---|---|---|
| `page` | int | 1 | Page number |
| `pageSize` | int | 25 | Items per page (max 100) |
| `search` | string | — | Search by display name or email |
| `isActive` | bool? | — | Filter by active status |

**Success Response (200):**

```json
{
  "success": true,
  "data": [
    {
      "userId": "550e8400-e29b-41d4-a716-446655440000",
      "email": "user@example.com",
      "displayName": "Jane Doe",
      "isActive": true
    }
  ],
  "pagination": {
    "page": 1,
    "pageSize": 25,
    "totalCount": 42,
    "totalPages": 2
  }
}
```

---

## Admin Endpoints

Base path: `/api/v1/core/admin`

All admin endpoints require the `RequireAdmin` authorization policy.

### Settings

| Method | Path | Description |
|---|---|---|
| `GET` | `/settings?module={module}` | List all settings, optionally by module |
| `GET` | `/settings/{module}/{key}` | Get a specific setting |
| `PUT` | `/settings/{module}/{key}` | Create or update a setting |
| `DELETE` | `/settings/{module}/{key}` | Delete a setting |

### Modules

| Method | Path | Description |
|---|---|---|
| `GET` | `/modules` | List installed modules |
| `GET` | `/modules/{moduleId}` | Get module details |
| `POST` | `/modules/{moduleId}/start` | Start a module |
| `POST` | `/modules/{moduleId}/stop` | Stop a module |
| `POST` | `/modules/{moduleId}/restart` | Restart a module |
| `POST` | `/modules/{moduleId}/capabilities/{capability}/grant` | Grant a capability |
| `DELETE` | `/modules/{moduleId}/capabilities/{capability}` | Revoke a capability |

### Health

| Method | Path | Description |
|---|---|---|
| `GET` | `/health` | Get detailed system health report |

**Success Response (200):**

```json
{
  "success": true,
  "data": {
    "status": "Healthy",
    "totalDuration": 45.2,
    "entries": [
      {
        "name": "database",
        "status": "Healthy",
        "description": "PostgreSQL connection OK",
        "duration": 12.3
      },
      {
        "name": "module:dotnetcloud.example",
        "status": "Healthy",
        "description": null,
        "duration": 5.1
      }
    ]
  }
}
```

---

## Health Endpoints

These endpoints are **not** protected by authentication and are intended for load balancers and monitoring.

| Method | Path | Description |
|---|---|---|
| `GET` | `/health/live` | Liveness probe (is the process alive?) |
| `GET` | `/health/ready` | Readiness probe (is the app ready to serve traffic?) |
| `GET` | `/health` | Combined health check (startup + database + modules) |

---

## OpenID Connect Endpoints

These are standard OAuth2/OIDC protocol endpoints managed by OpenIddict.

| Method | Path | Description |
|---|---|---|
| `GET` | `/.well-known/openid-configuration` | OIDC discovery document |
| `POST` | `/connect/token` | Token endpoint (authorization code, refresh, client credentials) |
| `POST` | `/connect/authorize` | Authorization endpoint |
| `POST` | `/connect/logout` | Logout endpoint |
| `POST` | `/connect/revoke` | Token revocation |
| `GET` | `/connect/userinfo` | UserInfo endpoint |
| `POST` | `/connect/introspect` | Token introspection |

See the [Authentication guide](AUTHENTICATION.md) for detailed flow diagrams.

---

## Real-Time (SignalR)

DotNetCloud uses SignalR for real-time communication. The hub endpoint is:

```
wss://your-domain.com/hubs/core
```

### Connection

Clients connect with a valid Bearer token:

```javascript
const connection = new signalR.HubConnectionBuilder()
    .withUrl("/hubs/core", { accessTokenFactory: () => token })
    .withAutomaticReconnect()
    .build();
```

### Events

| Event | Direction | Description |
|---|---|---|
| `Notification` | Server → Client | General notification broadcast |
| `PresenceUpdate` | Server → Client | User online/offline status change |
| `ModuleEvent` | Server → Client | Module-specific event broadcast |

---

## Interactive API Explorer

When running in development mode, an interactive OpenAPI/Swagger UI is available:

```
https://localhost:5001/swagger
```

This UI is auto-generated from controller XML documentation comments and provides a try-it-out interface for all endpoints.

> **Note:** The Swagger UI is disabled in production for security.

---

**See also:**

- [Authentication Flows](AUTHENTICATION.md)
- [Response Format](RESPONSE_FORMAT.md)
- [Error Handling](ERROR_HANDLING.md)
- [Module Development Guide](../guides/MODULE_DEVELOPMENT.md)
