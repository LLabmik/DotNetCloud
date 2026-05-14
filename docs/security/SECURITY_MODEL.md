# Security Model

**Last Updated:** May 14, 2026

---

## Overview

DotNetCloud uses a **module-isolated architecture** where each feature module runs as a separate OS process and communicates exclusively via gRPC over Unix sockets or Named Pipes. This document describes the trust boundaries, threat model, and data flow for the entire platform.

---

## Trust Boundaries

```
[Internet/Client]
       |
       v
[Reverse Proxy (nginx/YARP)] ──── TLS termination ────► [DotNetCloud Core]
                                                              |
                                              ┌───────────────┼───────────────┐
                                              |               |               |
                                              v               v               v
                                        [Module A] ──gRPC── [Module B] ──gRPC── [Module C]
                                              |               |               |
                                              v               v               v
                                        [DB A]          [DB B]          [DB C]
```

### Boundary 1: Internet → Reverse Proxy (TLS)

- All external traffic terminates TLS at the reverse proxy
- HTTPS enforced; HTTP requests redirected
- HSTS enabled with 1-year max-age, includeSubDomains, preload

### Boundary 2: Reverse Proxy → DotNetCloud Core

- Internal network only (loopback or Docker network)
- Core server validates Host header (`AllowedHosts` configuration)
- CORS configured with explicit origin allow-list

### Boundary 3: Core → Modules (gRPC)

- Unix socket files with restrictive file permissions (0600)
- gRPC channels authenticated using mutual TLS or Unix socket credentials
- No direct database access between modules
- `CallerContext` propagated with every gRPC call (User/System/Module identity)

### Boundary 4: Modules → Databases

- Each module has its own database schema (PostgreSQL schemas or separate databases)
- Connection strings provided via environment variables (`DOTNETCLOUD_DB_CONNECTION`)
- No module accesses another module's database schema

---

## Authentication & Authorization Flow

```
User → Login → OpenIddict Server ──► Access Token (JWT, 60 min)
                                       │
                                       ├──► Bearer token on API calls
                                       │       │
                                       │       v
                                       │   [Authorization Middleware]
                                       │       │
                                       │       ├──► Policy check (RequireFilesRead, etc.)
                                       │       ├──► Capability tier (Public/Restricted/Privileged)
                                       │       └──► Resource ownership check
                                       │
                                       └──► Refresh Token (14 days, rotating)
                                               │
                                               └──► Token revocation on logout
```

### Key Principles

1. **Defense in depth:** Authentication (OAuth2) + Authorization (policies + capability tiers) + Resource ownership checks
2. **Least privilege:** Modules only have access to the capabilities they declare
3. **Secure defaults:** TLS validation enabled by default; cookie flags set to most restrictive
4. **No hardcoded secrets:** All credentials from environment variables or user secrets

---

## Data Protection

| Data Type         | Protection             | Mechanism                                                                       |
| ----------------- | ---------------------- | ------------------------------------------------------------------------------- |
| Passwords         | Hashed (one-way)       | ASP.NET Core Identity PasswordHasher — PBKDF2 with HMAC-SHA256, 100K iterations |
| OAuth tokens      | AES-256-GCM encrypted  | `EncryptedFileTokenStore` with unique nonce per operation                       |
| Email credentials | AES-256-GCM encrypted  | `EmailCredentialEncryptionService`                                              |
| Files at rest     | Filesystem storage     | Outside web root; access mediated through Files module                          |
| WOPI tokens       | HMAC-SHA256 signed     | Token bound to specific user + file + action; timing-safe comparison            |
| TLS               | Certificate validation | Strict validation except development environments                               |

---

## Security Controls by Layer

### Network Layer

- TLS 1.2+ required for all external connections
- HSTS with preload
- Kestrel request header timeout (30s), keep-alive timeout (120s)
- Rate limiting per authenticated user (configurable)

### Application Layer

- OAuth2/OIDC with PKCE (SHA-256)
- Capability-based authorization tiers
- Antiforgery protection (Blazor auto-handled)
- CORS with explicit origin allow-list
- Request size limits on all upload endpoints

### Data Layer

- Parameterized queries (EF Core)
- Soft delete with query filters
- Automatic timestamp interceptors
- Connection pooling with retry limits

### Infrastructure Layer

- Module process isolation (separate OS processes)
- systemd security hardening (NoNewPrivileges, ProtectSystem, ProtectHome, PrivateTmp)
- Unix socket file permissions
- No shared memory or IPC bypassing gRPC

---

## Threat Scenarios & Mitigations

| Threat                          | Likelihood | Impact   | Mitigation                                                    |
| ------------------------------- | ---------- | -------- | ------------------------------------------------------------- |
| Attacker intercepts TLS traffic | Low        | Critical | TLS validation enforced; bypass only in dev                   |
| SQL injection via user input    | Low        | Critical | EF Core parameterization; no raw SQL with user input          |
| Cross-user data access          | Low        | High     | Resource ownership checks on all user data endpoints          |
| Privilege escalation via module | Low        | High     | gRPC authentication; module process isolation                 |
| Hardcoded credential leak       | Low        | Critical | All credentials from env vars; no secrets in source           |
| XSS via user content            | Medium     | Medium   | HtmlSanitizer on all user HTML; CSP restricts script sources  |
| Session hijacking               | Low        | High     | HttpOnly+Secure+SameSite cookies; session rotation on login   |
| Supply chain attack             | Low        | High     | Central package management; dependency vulnerability scanning |

---

## Assumptions & Accepted Risks

1. **Blazor WebAssembly CSP requirements:** `unsafe-inline`, `unsafe-eval`, `wasm-unsafe-eval` are required for Blazor WASM runtime and cannot be removed without breaking the application
2. **Self-hosted trust model:** The platform assumes the administrator controls the hosting environment; network-level DoS mitigation is out of scope
3. **Video streaming:** `X-Content-Type-Options: nosniff` is intentionally removed for video endpoints to enable browser codec probing
4. **Design-time factories:** Connection strings in design-time factories are for local development only; production uses `DOTNETCLOUD_DB_CONNECTION` environment variable
