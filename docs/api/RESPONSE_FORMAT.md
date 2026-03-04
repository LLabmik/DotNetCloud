# API Response Format

> **Purpose:** Documentation of the standard API response envelope used by all DotNetCloud endpoints  
> **Last Updated:** 2026-03-03

---

## Table of Contents

1. [Overview](#overview)
2. [Success Response](#success-response)
3. [Paginated Response](#paginated-response)
4. [Error Response](#error-response)
5. [Envelope Middleware](#envelope-middleware)
6. [Content Negotiation](#content-negotiation)
7. [Special Cases](#special-cases)

---

## Overview

All API responses under `/api/` are wrapped in a **standard envelope** by the `ResponseEnvelopeMiddleware`. This provides a consistent structure for clients to parse regardless of the endpoint.

### Design Principles

- **Consistent structure:** Every response has a `success` boolean at the top level
- **Self-describing errors:** Error responses include machine-readable codes and human-readable messages
- **Pagination metadata:** Paginated endpoints include a `pagination` object alongside the data
- **No double-wrapping:** If a controller already returns an envelope, the middleware does not re-wrap it

---

## Success Response

All successful API responses follow this structure:

```json
{
  "success": true,
  "data": { ... }
}
```

### Fields

| Field | Type | Description |
|---|---|---|
| `success` | `boolean` | Always `true` for successful responses |
| `data` | `object` or `array` | The response payload |

### Examples

**Single object:**

```json
{
  "success": true,
  "data": {
    "userId": "550e8400-e29b-41d4-a716-446655440000",
    "email": "user@example.com",
    "displayName": "Jane Doe"
  }
}
```

**Action confirmation (no data payload):**

```json
{
  "success": true,
  "message": "Password changed successfully."
}
```

**Array of items:**

```json
{
  "success": true,
  "data": [
    { "moduleId": "dotnetcloud.files", "status": "Enabled" },
    { "moduleId": "dotnetcloud.chat", "status": "Disabled" }
  ]
}
```

---

## Paginated Response

Endpoints that return lists include a `pagination` object:

```json
{
  "success": true,
  "data": [
    { "userId": "...", "email": "user1@example.com", "displayName": "User 1" },
    { "userId": "...", "email": "user2@example.com", "displayName": "User 2" }
  ],
  "pagination": {
    "page": 1,
    "pageSize": 25,
    "totalCount": 42,
    "totalPages": 2
  }
}
```

### Pagination Fields

| Field | Type | Description |
|---|---|---|
| `page` | `int` | Current page number (1-based) |
| `pageSize` | `int` | Number of items per page |
| `totalCount` | `int` | Total number of items across all pages |
| `totalPages` | `int` | Total number of pages |

### Pagination Query Parameters

Paginated endpoints accept these query parameters:

| Parameter | Type | Default | Description |
|---|---|---|---|
| `page` | `int` | `1` | Page number to retrieve |
| `pageSize` | `int` | `25` | Items per page (max 100) |

---

## Error Response

Error responses follow this structure:

```json
{
  "success": false,
  "error": {
    "code": "ERROR_CODE",
    "message": "Human-readable error description"
  }
}
```

### Fields

| Field | Type | Description |
|---|---|---|
| `success` | `boolean` | Always `false` for error responses |
| `error.code` | `string` | Machine-readable error code (see [Error Handling](ERROR_HANDLING.md)) |
| `error.message` | `string` | Human-readable error description |

### Global Exception Handler Response

When an unhandled exception occurs, the `GlobalExceptionHandlerMiddleware` produces:

```json
{
  "code": "INTERNAL_ERROR",
  "message": "An unexpected error occurred. Please try again later.",
  "requestId": "0HN2ABC123:00000001",
  "timestamp": "2026-03-03T12:00:00.000Z"
}
```

In development mode (when `includeStackTrace` is enabled):

```json
{
  "code": "VALIDATION_ERROR",
  "message": "The field 'email' is required.",
  "requestId": "0HN2ABC123:00000001",
  "timestamp": "2026-03-03T12:00:00.000Z",
  "details": {
    "exceptionType": "ValidationException",
    "stackTrace": "...",
    "innerException": null
  }
}
```

### `ApiErrorResponse` Model

The formal error response model used by the response envelope:

| Field | Type | Description |
|---|---|---|
| `success` | `boolean` | Always `false` |
| `code` | `string` | Machine-readable error code |
| `message` | `string` | Human-readable error message |
| `details` | `object?` | Additional context (validation errors, stack trace in dev) |
| `path` | `string?` | The request path that caused the error |
| `timestamp` | `datetime` | UTC timestamp of the error |
| `traceId` | `string?` | Distributed tracing ID for debugging |

---

## Envelope Middleware

The `ResponseEnvelopeMiddleware` automatically wraps API responses in the standard envelope.

### Configuration

```csharp
public sealed class ResponseEnvelopeOptions
{
    // Apply envelope to all matching responses (default: true)
    public bool EnableForAll { get; set; } = true;

    // Path prefixes that should be enveloped (default: ["/api/"])
    public string[] IncludePaths { get; set; } = ["/api/"];

    // Paths excluded from enveloping
    public string[] ExcludePaths { get; set; } =
    [
        "/health",
        "/openapi",
        "/swagger",
        "/connect/",
        "/hubs/"
    ];
}
```

### Behavior

1. **Inclusion check:** Only `/api/` paths are enveloped
2. **Exclusion check:** Health, OpenAPI, Swagger, OIDC, and SignalR paths are excluded
3. **Already enveloped:** If the response body already contains a `success` property, it is not re-wrapped
4. **Non-JSON:** Non-JSON responses pass through unchanged
5. **Empty responses:** `204 No Content` and `304 Not Modified` responses pass through unchanged

### HTTP Status Code Mapping

| Status Range | Envelope Type | `success` Value |
|---|---|---|
| 200–299 | Success envelope | `true` |
| 400–599 | Error envelope | `false` |
| 204, 304 | No envelope (pass-through) | N/A |

---

## Content Negotiation

- All API endpoints produce `application/json`
- The `Content-Type` header is always `application/json`
- JSON serialization uses **camelCase** property names
- Null properties are omitted (`JsonIgnoreCondition.WhenWritingNull`)

### JSON Serialization Options

```csharp
PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
WriteIndented = false,
DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
```

---

## Special Cases

### No-Content Responses

Some endpoints return `204 No Content` (e.g., successful DELETE). These are **not** wrapped in an envelope.

### OpenID Connect Responses

OIDC endpoints (`/connect/*`) follow the OAuth2/OIDC specification format, not the DotNetCloud envelope format. For example:

```json
{
  "access_token": "eyJhbGciOi...",
  "token_type": "Bearer",
  "expires_in": 3600
}
```

### Health Check Responses

Health endpoints (`/health`, `/health/live`, `/health/ready`) return standard ASP.NET Core health check format:

```json
{
  "status": "Healthy",
  "totalDuration": "00:00:00.0451234"
}
```

### SignalR Messages

SignalR messages (`/hubs/*`) use the SignalR protocol format, not the REST API envelope.

---

**See also:**

- [API Endpoint Reference](README.md)
- [Error Handling](ERROR_HANDLING.md)
- [Authentication Flows](AUTHENTICATION.md)
