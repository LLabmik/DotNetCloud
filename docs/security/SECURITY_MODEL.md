# Security Model

**Last Updated:** November 6, 2026 (module transport and database-access claims reconciled with v0.7.0)

---

## Overview

DotNetCloud uses a **module-isolated architecture** where each feature module runs as a separate OS process and communicates **only with the core** over gRPC. In v0.7.0 that transport is cleartext HTTP/2 (h2c) over a supervisor-assigned **loopback TCP port**; per-module Unix sockets / Named Pipes are planned (phase A4). Modules never call each other. This document describes the trust boundaries, threat model, and data flow for the entire platform.

---

## Trust Boundaries

```
[Internet/Client]
       |
       v
[Reverse Proxy (nginx/YARP)] ──── TLS termination ────► [DotNetCloud Core]
                                                              |
                                    ┌─────────────────────────┼─────────────────────────┐
                                    |                         |                         |
                                    v                         v                         v
                              [Module A]                [Module B]                [Module C]
                                    |                         |                         |
                                    └────── gRPC to core ─────┴────── gRPC to core ─────┘
                                                              |
                                                              v
                                                        [dotnetcloud DB]
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

- Cleartext HTTP/2 (h2c) on a per-module Unix socket / Named Pipe, or on a loopback TCP port assigned per module by `ProcessSupervisor` (`Modules:Transport=Tcp`)
- Per-module Unix sockets (`0600`, `0700` runtime directory) and Named Pipes (`PipeOptions.CurrentUserOnly`) are the **default** since phase A4; the credential check below is applied on every transport
- Socket/pipe permissions key on the OS user, so sibling modules can reach each other's endpoint — the module credential, not the filesystem, is the API identity control
- The supervisor mints a fresh 32-byte token for each module launch (`DOTNETCLOUD_MODULE_TOKEN`); module→core calls carry `dnc-module-id` + `dnc-module-token` and are validated in constant time
- Core→module calls are authenticated by `ModulePeerAuthenticationInterceptor` and fail closed when the supervisor did not launch the host
- End-user identity is only ever carried in a core-signed, short-lived `dnc-user-assertion`; modules cannot assert `CallerType.System` or supply their own roles
- `ModuleAccessInterceptor` delegates to `ModuleAccessEvaluator`, which enforces the module's `ModuleCapabilityGrants` on every mapped RPC from a shared `CapabilityCatalog`; unmapped module-facing RPCs fail closed and denials are audited (`ModuleCapabilityDenied`)
- Modules cannot call each other: there is no module-to-module channel

### Boundary 3a: Same-OS-user limitation

All module hosts run as the core service account. The transport and credential controls above are an **API-layer**, not an OS-layer, boundary: a same-UID process can read a sibling's environment and files. Credential rotation, capability grants, audit logging and store review are the compensating controls; per-module OS isolation is a public-store launch gate.

### Boundary 4: Modules → Databases

- Required modules share the `core` schema; optional modules have their own schemas (PostgreSQL schemas / SQL Server schemas)
- **Current limitation:** module hosts receive the core `config.json` (and therefore the core connection string) via `DOTNETCLOUD_CONFIG_DIR`, so the database layer is not yet an isolation boundary
- Scoped per-module principals + sanitized `module.json` are the first deliverable of the module sandbox program (`docs/THIRD_PARTY_MODULE_SANDBOX_AND_STORE_PLAN.md`, phase A1)
- No module accesses another module's database schema by design (enforced at the API layer today, at the database layer after A1)

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
- Loopback-only module listener; per-module sockets/pipes planned (phase A4)
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

1. **Blazor CSP:** On .NET 8+ the client-side Blazor runtime requires only `wasm-unsafe-eval`. `unsafe-inline`/`unsafe-eval` are avoided by SHA-256 hash allow-listing the app's three static inline `<script>` blocks (`CspPolicy` in `DotNetCloud.Core.ServiceDefaults`) and by using Blazor `@onclick` + JS interop instead of inline event handlers. `object-src` is `'none'`. Libraries that would otherwise require `'unsafe-eval'` are pre-compiled at build time instead — e.g. the Butterchurn music visualizer presets, whose equations are turned into real functions by `tools/butterchurn-presets/precompile-presets.cjs` into `butterchurn-presets-compiled.js`.
2. **Self-hosted trust model:** The platform assumes the administrator controls the hosting environment; network-level DoS mitigation is out of scope
3. **Video streaming:** `X-Content-Type-Options: nosniff` is intentionally removed for video endpoints to enable browser codec probing
4. **Design-time factories:** Connection strings in design-time factories are for local development only; production uses `DOTNETCLOUD_DB_CONNECTION` environment variable
