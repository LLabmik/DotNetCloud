# Error Handling

> **Purpose:** Documentation of error codes, exception mapping, and error response structure  
> **Last Updated:** 2026-03-03

---

## Table of Contents

1. [Overview](#overview)
2. [Error Response Structure](#error-response-structure)
3. [Error Code Reference](#error-code-reference)
4. [Exception-to-HTTP Mapping](#exception-to-http-mapping)
5. [Global Exception Handler](#global-exception-handler)
6. [Validation Errors](#validation-errors)
7. [Development vs Production](#development-vs-production)
8. [Client Error Handling](#client-error-handling)

---

## Overview

DotNetCloud uses a layered error handling strategy:

1. **Controller-level:** Endpoints catch expected exceptions and return typed error responses
2. **Middleware-level:** The `GlobalExceptionHandlerMiddleware` catches unhandled exceptions
3. **Response envelope:** The `ResponseEnvelopeMiddleware` wraps non-enveloped errors in the standard format

This ensures that clients always receive a consistent, machine-parsable error response regardless of where the error occurs.

---

## Error Response Structure

All error responses follow this format:

```json
{
  "success": false,
  "error": {
    "code": "ERROR_CODE",
    "message": "Human-readable description of the error"
  }
}
```

For unhandled exceptions caught by the global exception handler:

```json
{
  "code": "INTERNAL_ERROR",
  "message": "An unexpected error occurred. Please try again later.",
  "requestId": "0HN2ABC123:00000001",
  "timestamp": "2026-03-03T12:00:00.000Z"
}
```

---

## Error Code Reference

### Authentication & Authorization

| Code | HTTP Status | Description |
|---|---|---|
| `AUTH_INVALID_CREDENTIALS` | 401 | Wrong email or password |
| `AUTH_UNAUTHORIZED` | 401 | Missing or invalid authentication |
| `AUTH_FORBIDDEN` | 403 | Insufficient permissions |
| `AUTH_TOKEN_EXPIRED` | 401 | Access token has expired |
| `AUTH_INVALID_TOKEN` | 401 | Token is malformed or invalid |
| `AUTH_MFA_REQUIRED` | 202 | Valid credentials but MFA step needed |
| `AUTH_INVALID_MFA_CODE` | 400 | TOTP code is invalid or expired |
| `AUTH_ACCOUNT_LOCKED` | 401 | Account locked due to failed attempts |
| `AUTH_EMAIL_NOT_CONFIRMED` | 401 | Email address not yet confirmed |

### User & Identity

| Code | HTTP Status | Description |
|---|---|---|
| `USER_NOT_FOUND` | 404 | User does not exist |
| `USER_ALREADY_EXISTS` | 409 | User already registered |
| `USER_EMAIL_ALREADY_IN_USE` | 409 | Email address is taken |
| `USER_INVALID_EMAIL_FORMAT` | 400 | Email format is invalid |
| `USER_WEAK_PASSWORD` | 400 | Password does not meet requirements |
| `USER_PASSWORD_MISMATCH` | 400 | Current password is incorrect |

### Organization & Teams

| Code | HTTP Status | Description |
|---|---|---|
| `ORG_NOT_FOUND` | 404 | Organization does not exist |
| `ORG_ALREADY_EXISTS` | 409 | Organization name is taken |
| `TEAM_NOT_FOUND` | 404 | Team does not exist |
| `TEAM_ALREADY_EXISTS` | 409 | Team name is taken within org |
| `TEAM_NOT_MEMBER` | 403 | User is not a member of the team |

### Capabilities & Permissions

| Code | HTTP Status | Description |
|---|---|---|
| `CAP_NOT_GRANTED` | 403 | Module does not have the required capability |
| `CAP_NOT_FOUND` | 404 | Capability does not exist |
| `CAP_FORBIDDEN` | 403 | Capability is in the Forbidden tier |
| `PERM_NOT_FOUND` | 404 | Permission does not exist |
| `ROLE_NOT_FOUND` | 404 | Role does not exist |
| `ROLE_ALREADY_EXISTS` | 409 | Role name is taken |
| `ROLE_CANNOT_DELETE_SYSTEM` | 403 | System roles cannot be deleted |

### Modules

| Code | HTTP Status | Description |
|---|---|---|
| `MODULE_NOT_FOUND` | 404 | Module is not installed |
| `MODULE_ALREADY_INSTALLED` | 409 | Module is already installed |
| `MODULE_LOAD_FAILED` | 500 | Module failed to load |
| `MODULE_INIT_FAILED` | 500 | Module initialization error |
| `MODULE_DEPENDENCY_NOT_SATISFIED` | 400 | Required dependency not available |
| `MODULE_VERSION_MISMATCH` | 400 | Module version incompatible |
| `MODULE_INVALID_MANIFEST` | 400 | Module manifest validation failed |

### Events

| Code | HTTP Status | Description |
|---|---|---|
| `EVENT_BUS_ERROR` | 500 | Internal event bus error |
| `EVENT_HANDLER_ERROR` | 500 | Event handler threw an exception |
| `EVENT_SUBSCRIPTION_FAILED` | 500 | Failed to subscribe to event |

### Database & Data

| Code | HTTP Status | Description |
|---|---|---|
| `DB_CONNECTION_FAILED` | 503 | Cannot connect to database |
| `DB_ERROR` | 500 | General database error |
| `DB_ENTITY_NOT_FOUND` | 404 | Record not found |
| `DB_ENTITY_ALREADY_EXISTS` | 409 | Duplicate record |
| `DB_CONCURRENCY_CONFLICT` | 409 | Concurrent update conflict |
| `DB_INVALID_OPERATION` | 400 | Invalid database operation |

### Validation

| Code | HTTP Status | Description |
|---|---|---|
| `VALIDATION_ERROR` | 400 | General validation failure |
| `VALIDATION_REQUIRED_FIELD` | 400 | A required field is missing |

---

## Exception-to-HTTP Mapping

The `GlobalExceptionHandlerMiddleware` maps .NET exception types to HTTP status codes:

| Exception Type | HTTP Status | Error Code |
|---|---|---|
| `UnauthorizedException` | 401 | `UNAUTHORIZED` |
| `CapabilityNotGrantedException` | 403 | `CAPABILITY_NOT_GRANTED` |
| `ValidationException` | 400 | `VALIDATION_ERROR` |
| `ModuleNotFoundException` | 404 | `MODULE_NOT_FOUND` |
| `ArgumentNullException` | 400 | `INVALID_ARGUMENT` |
| `ArgumentException` | 400 | `INVALID_ARGUMENT` |
| `InvalidOperationException` | 409 | `INVALID_OPERATION` |
| `NotImplementedException` | 501 | `NOT_IMPLEMENTED` |
| All other exceptions | 500 | `INTERNAL_ERROR` |

---

## Global Exception Handler

The `GlobalExceptionHandlerMiddleware` is registered early in the middleware pipeline and catches any exception that escapes controller-level handling.

### Behavior

1. Logs the exception with full context (method, path, stack trace)
2. Maps the exception type to an HTTP status code and error code
3. Returns a structured error response
4. Never leaks stack traces in production mode

### Configuration

The middleware is configured in `Program.cs`:

```csharp
app.UseMiddleware<GlobalExceptionHandlerMiddleware>(
    includeStackTrace: app.Environment.IsDevelopment());
```

---

## Validation Errors

ASP.NET Core model validation errors are automatically handled and returned as structured responses:

```json
{
  "success": false,
  "error": {
    "code": "VALIDATION_ERROR",
    "message": "One or more validation errors occurred.",
    "details": {
      "email": ["The Email field is required."],
      "password": ["The Password field must be at least 8 characters."]
    }
  }
}
```

### Custom Exceptions

DotNetCloud defines custom exception types in `DotNetCloud.Core.Errors`:

| Exception | Purpose |
|---|---|
| `CapabilityNotGrantedException` | Module lacks a required capability |
| `ModuleNotFoundException` | Referenced module is not installed |
| `UnauthorizedException` | Caller is not authenticated or lacks permissions |
| `ValidationException` | Input data fails business validation |

---

## Development vs Production

| Behavior | Development | Production |
|---|---|---|
| Stack traces in response | ✅ Included in `details` | ❌ Omitted |
| Exception type in response | ✅ Included | ❌ Omitted |
| Inner exception message | ✅ Included | ❌ Omitted |
| Error logging | ✅ Verbose | ✅ Structured (no PII) |
| Swagger UI | ✅ Available | ❌ Disabled |

---

## Client Error Handling

### Recommended Pattern

```javascript
const response = await fetch('/api/v1/core/auth/login', {
  method: 'POST',
  headers: { 'Content-Type': 'application/json' },
  body: JSON.stringify({ email, password })
});

const result = await response.json();

if (result.success) {
  // Handle success
  const { accessToken, refreshToken } = result.data;
} else {
  // Handle error by code
  switch (result.error?.code) {
    case 'MFA_REQUIRED':
      // Redirect to MFA verification
      break;
    case 'INVALID_CREDENTIALS':
      // Show invalid credentials message
      break;
    default:
      // Show generic error
      console.error(result.error?.message);
  }
}
```

### HTTP Status Code Quick Reference

| Status | Meaning | Action |
|---|---|---|
| 200 | Success | Process `data` field |
| 202 | Accepted (MFA required) | Redirect to MFA flow |
| 400 | Bad Request | Show validation errors |
| 401 | Unauthorized | Redirect to login or refresh token |
| 403 | Forbidden | Show permission denied message |
| 404 | Not Found | Show not found message |
| 409 | Conflict | Show conflict message (duplicate, etc.) |
| 429 | Too Many Requests | Retry after `Retry-After` header |
| 500 | Server Error | Show generic error, log for support |
| 503 | Service Unavailable | Retry with backoff |

---

**See also:**

- [API Endpoint Reference](README.md)
- [Response Format](RESPONSE_FORMAT.md)
- [Authentication Flows](AUTHENTICATION.md)
