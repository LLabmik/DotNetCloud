# Security Code Review Plan — Entire DotNetCloud Codebase

**Date:** May 14, 2026
**Status:** ✅ Complete — All 10 phases executed. See [SECURITY_REVIEW_FINDINGS.md](./SECURITY_REVIEW_FINDINGS.md) for full results and [docs/security/](./security/) for security documentation deliverables.

---

## Overview

A phased, vulnerability-focused security audit of the entire DotNetCloud codebase. Covers authentication, transport security, input validation, file uploads, cryptography, secrets management, logging, and cross-module trust boundaries. Each phase produces a detailed findings report with severity ratings, exact file references, and remediation guidance.

**Scope:** Every attack surface visible to a malicious actor — from HTTP endpoints to gRPC sockets, file uploads to OAuth flows, dependency chains to configuration defaults.

### Focus Areas

- **Authentication & Authorization** — OAuth2/OIDC flows, token management, session security, password/MFA, capability tier enforcement, privilege escalation paths
- **Transport Security** — TLS certificate validation, HTTPS enforcement, gRPC channel security, SignalR/WebSocket security, CORS configuration
- **Input Validation & Injection** — SQL injection, XSS, open redirect, host header injection, request validation, mass assignment
- **File Upload Security** — Extension whitelisting, magic byte validation, malware scanning, path traversal, filename sanitization
- **Data Protection & Cryptography** — Encryption at rest, hashing, key management, random number generation, timing attack resistance
- **Configuration & Secrets** — Hardcoded credentials, environment isolation, connection strings, API keys, supply chain vulnerabilities
- **Information Disclosure** — Logging, error messages, debug endpoints, server headers, response metadata
- **Cross-Module Trust** — gRPC socket permissions, event bus security, module isolation, cross-module dependency audit

### Out of Scope

- Physical security / data center security
- Social engineering resistance
- Denial of Service at network layer (firewall/DDoS mitigation)
- Browser-specific vulnerability analysis
- Third-party service security (Google APIs, Collabora, MusicBrainz)
- Compliance certification (SOC2, GDPR, HIPAA)
- Penetration testing execution (this plan covers code review only)

---

## Pre-Discovery Findings Summary

These 20 issues were found during automated scanning while researching this plan and represent the starting baseline for the review.

### 🔴 CRITICAL (3 issues)

| #   | Finding                                                                                                                          | Location                                                                                                                                                                                                                     |
| --- | -------------------------------------------------------------------------------------------------------------------------------- | ---------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| 1   | **TLS certificate validation disabled** — `DangerousAcceptAnyServerCertificateValidator` used in 3 code paths                    | `src/Modules/Files/DotNetCloud.Modules.Files.Data/FilesServiceRegistration.cs:109`<br>`src/Modules/Email/DotNetCloud.Modules.Email.Data/EmailServiceRegistration.cs:41`<br>`src/Core/DotNetCloud.Core.Server/Program.cs:399` |
| 2   | **Hardcoded database credentials** in configuration files and design-time factories                                              | `appsettings.json`, `appsettings.Development.json`, 15+ `*DbContextDesignTimeFactory.cs` files across modules                                                                                                                |
| 3   | **Vulnerable transitive dependency override** — `System.Security.Cryptography.Xml` version pinned to 10.0.6 to address known CVE | `Directory.Packages.props`                                                                                                                                                                                                   |

### 🟠 HIGH (5 issues)

| #   | Finding                                                                                                                                   | Location                                                                                                                                                 |
| --- | ----------------------------------------------------------------------------------------------------------------------------------------- | -------------------------------------------------------------------------------------------------------------------------------------------------------- |
| 4   | **No file extension whitelisting** on 6 IFormFile upload endpoints — any file type accepted                                               | `ContactsController.cs:292,357`, `EmailController.cs:555`, `BookmarksController.cs:242`, `WorkItemsController.cs:682`, `UserManagementController.cs:303` |
| 5   | **No magic byte/file signature validation** — content type determined by extension only                                                   | All upload endpoints above                                                                                                                               |
| 6   | **`AllowInsecureTls` option** for Collabora integration — configurable from CLI, defaults to `true` in development                        | `FilesServiceRegistration.cs` (options), `ServiceCommands.cs:151`                                                                                        |
| 7   | **CSP allows `'unsafe-inline'`, `'unsafe-eval'`, `'wasm-unsafe-eval'`** — required for Blazor WebAssembly but broadens XSS attack surface | `SecurityHeadersMiddleware.cs:86`                                                                                                                        |
| 8   | **`AllowedHosts: "*"`** in production `appsettings.json` — accepts any Host header                                                        | `src/Core/DotNetCloud.Core.Server/appsettings.json`                                                                                                      |

### 🟡 MEDIUM (8 issues)

| #   | Finding                                                                                                                   |
| --- | ------------------------------------------------------------------------------------------------------------------------- |
| 9   | No malware/virus scanning integration for file uploads                                                                    |
| 10  | Outdated packages: `OpenIddict.*` 7.2.0 (7.4+ has security fixes), `Google.Apis.*` 1.73.0 (1+ year old)                   |
| 11  | Video streaming strips `X-Content-Type-Options: nosniff` header — enables MIME-type sniffing for video playability        |
| 12  | Open redirect protection via `IsSafeLocalReturnUrl` — must verify ALL redirect paths use it consistently                  |
| 13  | gRPC Unix socket permissions — module trust boundaries not formally documented or verified                                |
| 14  | No `[ValidateAntiForgeryToken]` on traditional form endpoints (Blazor auto-handles antiforgery, but must verify coverage) |
| 15  | Sensitive data possibly leaked in log output — needs methodical audit of all `ILogger.Log*` and `Console.WriteLine` calls |
| 16  | Rate limiting: authenticated users get 10,000 requests per 60-second window — unusually permissive                        |

### 🟢 LOW (4 issues)

| #   | Finding                                                                                              |
| --- | ---------------------------------------------------------------------------------------------------- |
| 17  | Token signing key rotation not automated (persistent RSA keys stored in `oidc-keys/` directory)      |
| 18  | `RequestHeaderTimeoutSeconds: 30` — verify enforced at Kestrel level across all endpoints            |
| 19  | Email credential encryption key management — verify key derivation strength and rotation strategy    |
| 20  | Password minimum 12 characters, 3 complexity classes — strong, but document rationale for compliance |

---

## Phase 1: Automated Security Scanning

Run specialized security tooling across the entire codebase. All steps are independent and can execute in parallel.

### 1.1 Static Analysis Security Testing (SAST)

```bash
# Enable all security analyzers
dotnet build /p:EnableNETAnalyzers=true /p:AnalysisLevel=latest-All 2>&1 | tee sast-output.txt

# Focus on security-specific rules:
# CA5350-CA5403 — Cryptography rules (weak algorithms, insecure configurations)
# CA3001-CA3126 — Injection rules (SQL, LDAP, XPath, command, file path)
# CA5360-CA5403 — Additional security rules
```

- Run `security-code-scan` analyzer for .NET-specific vulnerability patterns
- Capture all CA\* security warnings with per-project counts
- Flag any suppressed security warnings (`#pragma warning disable CA*`)

### 1.2 Dependency Vulnerability Scanning

```bash
# List all vulnerable packages across solution
dotnet list package --vulnerable

# List deprecated packages
dotnet list package --deprecated

# Check transitive dependencies
dotnet list package --include-transitive --vulnerable
```

- Cross-reference all package versions against:
  - National Vulnerability Database (NVD)
  - GitHub Advisory Database
  - .NET Security Advisories
- **Pre-flagged packages:**
  - `System.Security.Cryptography.Xml` 10.0.6 — verify this is the latest patched version for the known CVE
  - `OpenIddict.*` 7.2.0 — check release notes for security fixes in 7.3.x and 7.4.x
  - `Google.Apis.*` 1.73.0 — 1+ year old, check for CVEs and update availability
  - `Otp.NET` 1.4.1 — verify latest version, check TOTP implementation correctness
  - `MailKit` 4.16.0 — verify latest security patches

### 1.3 Secret Detection

```bash
# Scan entire repo for hardcoded secrets
# Patterns: passwords, connection strings, API keys, JWT secrets, signing keys

# Specific grep patterns:
grep -rn "Password=" src/ --include="*.cs" --include="*.json" --include="*.config"
grep -rn "connectionString\|ConnectionString\|CONNECTION_STRING" src/ --include="*.json"
grep -rn "client_secret\|ClientSecret\|CLIENT_SECRET" src/
grep -rn "api_key\|ApiKey\|API_KEY" src/
grep -rn "-----BEGIN.*PRIVATE KEY-----" src/
grep -rn "-----BEGIN RSA PRIVATE KEY-----" src/
```

- **Pre-flagged:** `appsettings.json` default connection string with password, 15+ design-time factory files with credentials
- Verify `.gitignore` excludes any credential files that were previously committed
- Check git history for accidentally committed secrets (even if removed later)

### 1.4 Configuration Audit

Review all configuration files across all 15 module Host projects:

- All `appsettings*.json` files — verify production settings are hardened
- All `launchSettings.json` files — verify no production secrets in development configs
- Environment variable usage — `DOTNET_ENVIRONMENT`, `ASPNETCORE_ENVIRONMENT`, connection strings
- Debug endpoints — Swagger, developer exception page, health check detail level
- **Pre-flagged:** `AllowedHosts: "*"` in production config, hardcoded DB credentials

### 1.5 HTTP Security Header Audit

- Verify header presence on all response types: HTML, API JSON, static files, streaming
- Check for header removal by specific controllers that override global middleware
- **Pre-flagged:** Video streaming removes `X-Content-Type-Options`, Files streaming may too
- Audit `SecurityHeadersMiddleware` configuration options

### Phase 1 Output

`/docs/SECURITY_REVIEW_FINDINGS.md` — **Phase 1: Automated Scanning** section with:

- SAST warning counts by rule category and project
- Vulnerable and outdated dependency list with CVE references
- Hardcoded secret inventory with file paths and remediation required
- Configuration issue inventory
- Security header coverage matrix by endpoint type

---

## Phase 2: Authentication & Authorization Deep Dive

### 2.1 OpenIddict / OAuth2 Flow Review

**Files to review:**

- `src/Core/DotNetCloud.Core.Auth/Extensions/AuthServiceExtensions.cs:92-158`
- `src/Core/DotNetCloud.Core.Server/Initialization/OidcClientSeeder.cs`
- `src/Clients/DotNetCloud.Client.Core/Auth/OAuth2Service.cs`

**Review Checklist:**

- [ ] **PKCE enforcement:** `RequirePkce = true` at line 125 — verify no code path bypasses PKCE for public clients
- [ ] **OIDC client registration:** `OidcClientSeeder.cs` — verify first-party clients are registered with correct grant types; no hardcoded secrets
- [ ] **Token lifetimes:** Access token 60 min, Refresh token 14 days — appropriateness for self-hosted cloud platform
- [ ] **Refresh token rotation:** Verify rotation is implemented (one-time-use refresh tokens) or document acceptance of risk
- [ ] **Token revocation:** `/connect/revoke` endpoint — verify it actually invalidates tokens server-side
- [ ] **Scope definitions:** `openid`, `profile`, `email`, `offline_access`, `files:read`, `files:write`, `bookmarks:read`, `bookmarks:write` — appropriate granularity
- [ ] **RSA key storage:** `oidc-keys/` directory — verify file permissions, access control, key backup strategy
- [ ] **Discovery endpoint:** Verify it does not leak internal network information
- [ ] **Client-side OAuth2:** `OAuth2Service.cs` uses PKCE with SHA-256 `code_challenge_method` — verify no downgrade to `plain`

### 2.2 Authorization Policies & Capability Tiers

**Files to review:**

- `src/Core/DotNetCloud.Core.Auth/Extensions/AuthServiceExtensions.cs:160-185`
- `src/Core/DotNetCloud.Core.Auth/Authorization/PermissionAuthorizationHandler.cs`
- `src/Core/DotNetCloud.Core.Auth/Extensions/ClaimsPrincipalExtensions.cs`

**Review Checklist:**

- [ ] **Policy registration:** `RequireAuthenticated`, `RequireAdmin`, `RequireFilesRead`, `RequireFilesWrite`, `RequireBookmarksRead`, `RequireBookmarksWrite` — verify completeness
- [ ] **Capability tier enforcement:** Public < Restricted < Privileged < Forbidden — verify each tier correctly gates module access
- [ ] **Admin bypass:** `IsInRole(SystemRoleNames.Administrator)` — verify this is the correct bypass mechanism, not hardcoded user IDs
- [ ] **Privilege escalation paths:** Can a Restricted user escalate to Privileged? Can a module user access admin endpoints?
- [ ] **`PermissionAuthorizationHandler`:** Verify correct challenge (401) vs forbid (403) logic
- [ ] **Missing authorization:** Audit ALL controller actions — any endpoints missing `[Authorize]`?
- [ ] **Resource-level authorization:** For endpoints that access user data (files, bookmarks, etc.) — verify ownership checks beyond just authentication

### 2.3 Session & Cookie Security

**Files to review:**

- `src/Core/DotNetCloud.Core.Auth/Extensions/AuthServiceExtensions.cs:76-81`
- `src/UI/DotNetCloud.UI.Web/Components/App.razor:81-83`
- `src/Modules/Email/DotNetCloud.Modules.Email.Host/Controllers/GmailOAuthController.cs:83-87`

**Review Checklist:**

- [ ] **Identity cookie:** `ConfigureApplicationCookie` — verify HttpOnly, Secure, SameSite flags are set in all environments
- [ ] **OAuth state cookie:** HttpOnly, Secure, SameSite=Lax — verified ✅ (reference implementation)
- [ ] **Session fixation:** Is session ID regenerated after login?
- [ ] **Logout completeness:** Does logout invalidate both access token and refresh token?
- [ ] **Cookie prefixes:** Consider `__Host-` or `__Secure-` prefixes for critical cookies
- [ ] **Cookie Policy:** `SameSiteMode` consistency across all cookie writes

### 2.4 Password & MFA Security

**Files to review:**

- `src/CLI/DotNetCloud.CLI/Infrastructure/PasswordValidator.cs`
- `src/Core/DotNetCloud.Core.Server/Middleware/PasswordChangeRequiredMiddleware.cs` (reference in `Program.cs:1029`)

**Review Checklist:**

- [ ] **Password policy:** 12 chars minimum, 3 of 4 complexity classes, common password blocklist — verify enforcement at API level (not just CLI)
- [ ] **`PasswordChangeRequiredMiddleware`:** Verify it cannot be bypassed by direct API calls
- [ ] **MFA / TOTP:** `Otp.NET` 1.4.1 — verify TOTP window/step configuration, replay prevention, recovery codes
- [ ] **Account lockout:** Verify failed login attempts trigger temporary lockout (prevent brute force)
- [ ] **Password reset flow:** Verify reset tokens are single-use, time-limited, and cryptographically random

---

## Phase 3: TLS, Network & Transport Security

### 3.1 TLS Certificate Validation — CRITICAL REVIEW

Three locations bypass TLS certificate validation. Each must be reviewed:

| #   | File                          | Line | Context                                                                                 | Risk                                                     |
| --- | ----------------------------- | ---- | --------------------------------------------------------------------------------------- | -------------------------------------------------------- |
| 1   | `FilesServiceRegistration.cs` | 109  | Collabora HttpClient — `HttpClientHandler.DangerousAcceptAnyServerCertificateValidator` | MITM on Collabora connections — document tampering       |
| 2   | `EmailServiceRegistration.cs` | 41   | Email HttpClient — `HttpClientHandler.DangerousAcceptAnyServerCertificateValidator`     | MITM on IMAP/SMTP — credential theft, email interception |
| 3   | `Program.cs`                  | 399  | Core server HttpClient — `ServerCertificateCustomValidationCallback`                    | MITM on inter-service communication                      |

**Review Checklist for each:**

- [ ] Is there an environment gate (`IsDevelopment()` or config flag)? If not, this is production-active.
- [ ] What external hosts are connected to with this HttpClient?
- [ ] Can certificate pinning or proper CA validation replace the bypass?
- [ ] If bypass is intentional (self-signed certs in private deployments), is it clearly documented and configurable?
- [ ] Can the bypass be scoped to specific hosts rather than all hosts?

**Pre-flagged extender:**

- `ServiceCommands.cs:151` — CLI sets `Files__Collabora__AllowInsecureTls = "true"` for development
- `FilesServiceRegistration.cs` — `AllowInsecureTls` configuration option must never be enabled in production

### 3.2 HTTPS Configuration

**Files to review:**

- `src/Core/DotNetCloud.Core.Server/appsettings.json:19-28`
- `src/Core/DotNetCloud.Core.Server/Program.cs:1024` — `UseHttpsRedirection()`

**Review Checklist:**

- [ ] `EnableHttps: true` — verify this cannot be overridden to `false` in production
- [ ] `UseHttpsRedirection()` — verify it is placed BEFORE authentication middleware in the pipeline
- [ ] HSTS: `max-age=31536000; includeSubDomains; preload` — verify on all HTTPS responses; check for HSTS header removal on non-production endpoints
- [ ] Certificate management: `certs/dotnetcloud-localhost.pfx` — verify production uses real CA certs, not self-signed
- [ ] HTTP/2 enabled — check for HTTP/2 rapid reset vulnerability mitigation in Kestrel configuration
- [ ] `RequestHeaderTimeoutSeconds: 30`, `KeepAliveTimeoutSeconds: 120` — verify enforcement

### 3.3 gRPC Channel Security

**Review Checklist:**

- [ ] All gRPC channels — verify none use `ChannelCredentials.Insecure`
- [ ] Inter-module gRPC over Unix sockets — verify socket file permissions restrict access to module processes only
- [ ] gRPC reflection — verify disabled in production (reconnaissance vector)
- [ ] gRPC max message size — verify reasonable limits to prevent memory exhaustion
- [ ] gRPC deadline/timeout propagation — verify callers set deadlines

### 3.4 SignalR / WebSocket Security

**Review Checklist:**

- [ ] SignalR hub authorization — both connection-level (`[Authorize]` on hub) and method-level
- [ ] WebSocket origin validation — verify cross-origin WebSocket connections are rejected
- [ ] SignalR message size limits — verify configured
- [ ] Connection rate limiting — verify SignalR connection attempts are rate-limited
- [ ] `Program.cs:1050` — `UseAntiforgery()` placement relative to SignalR middleware

### 3.5 CORS Configuration

**Files to review:**

- `src/Core/DotNetCloud.Core.Server/Configuration/CorsConfiguration.cs`
- `src/Core/DotNetCloud.Core.Server/Program.cs:1023`

**Review Checklist:**

- [ ] `CorsConfiguration.cs:82` — "AllowAnyOrigin is never used" — verify this commitment holds in ALL code paths
- [ ] `CorsConfiguration.cs:123-125` — `AllowCredentials()` only when both credentials AND explicit origins are configured — verify
- [ ] Production origins: `appsettings.json:66-71` — `AllowedOrigins: []` (empty) — verify this means no cross-origin access
- [ ] Development origins: `appsettings.Development.json:19-22` — only localhost — verify no wildcard
- [ ] CORS middleware placement: `Program.cs:1023` — verify correct order in pipeline

---

## Phase 4: Input Validation & Injection Defense

### 4.1 SQL Injection

**Review Checklist:**

- [ ] Scan ALL non-migration `.cs` files for SQL string concatenation patterns
- [ ] `DatabaseSetupHelper.cs:82` — `psql` command with variables — verify all inputs are parameterized, not interpolated
- [ ] EF Core usage audit — verify no `FromSqlRaw`, `ExecuteSqlRaw`, `FromSqlInterpolated` with unsanitized user input
- [ ] PostgreSQL-specific: verify no `COPY` command, `LISTEN`/`NOTIFY` channel injection, or `lo_import` abuse
- [ ] Dynamic query building (ordering, filtering) — verify safe from injection via parameterization or allow-lists
- [ ] Stored procedures — if any, verify parameter usage

### 4.2 Cross-Site Scripting (XSS)

**Review Checklist:**

- [ ] CSP: `'unsafe-inline'`, `'unsafe-eval'`, `'wasm-unsafe-eval'` — document acceptance of risk (required by Blazor)
- [ ] `HtmlSanitizer` 9.0.892 — verify applied to ALL user-supplied HTML before rendering
- [ ] `MarkupString` usage in Razor components — find all instances, verify input is sanitized before casting to `MarkupString`
- [ ] Output encoding — verify Razor auto-escaping is not bypassed via `@Html.Raw()` or similar
- [ ] User-controlled attributes — verify no injection into `href`, `src`, `style`, event handlers
- [ ] File download filenames — verify `Content-Disposition` header is safe (no CRLF injection)

### 4.3 Open Redirect

**Files to review:**

- `src/Core/DotNetCloud.Core.Server/Controllers/AuthSessionController.cs:162`

**Review Checklist:**

- [ ] `IsSafeLocalReturnUrl()` — verify implementation: starts with "/" but not "//" (prevents `//evil.com`)
- [ ] Find ALL `Redirect()`, `RedirectToAction()`, `RedirectToPage()`, `RedirectToRoute()` calls
- [ ] Verify ALL redirect URLs from user input (`returnUrl`, `redirect_uri`) pass through `IsLocalUrl()` or `IsSafeLocalReturnUrl()`
- [ ] Prefer `LocalRedirect()` over `Redirect()` where applicable
- [ ] OAuth2 `redirect_uri` — verify validated against registered client redirect URIs

### 4.4 Host Header Injection

**Review Checklist:**

- [ ] `AllowedHosts: "*"` in production config — must be restricted to actual hostnames
- [ ] `ReverseProxyTemplates.cs` — nginx templates verify host header configuration
- [ ] `Request.Host` usage — verify not used unsafely in URL generation
- [ ] `ForwardedHeadersMiddleware` — verify configured for reverse proxy scenarios

### 4.5 Request Validation & Mass Assignment

**Review Checklist:**

- [ ] `ModelState.IsValid` — verify checked on ALL POST/PUT/PATCH endpoints
- [ ] `[Bind]` attribute — verify used to prevent overposting (mass assignment)
- [ ] `[JsonIgnore]` — verify sensitive properties are not deserialized from client input
- [ ] `[BindNever]` — verify server-set properties are protected from client input
- [ ] `[RequestSizeLimit]` — verify ALL upload endpoints have size limits (found on most but not all)
- [ ] `GlobalExceptionHandlerMiddleware.cs:19` — `includeStackTrace = false` default — verify enforced in production

---

## Phase 5: File Upload Security

### 5.1 Upload Endpoint Inventory

Six IFormFile endpoints identified. Each must be audited:

| #   | Endpoint              | File                          | Line | Size Limit | Extension Whitelist | Magic Byte Check |
| --- | --------------------- | ----------------------------- | ---- | ---------- | ------------------- | ---------------- |
| 1   | Contacts avatar       | `ContactsController.cs`       | 292  | 5 MB       | ☐ MISSING           | ☐ MISSING        |
| 2   | Contacts attachment   | `ContactsController.cs`       | 357  | —          | ☐ MISSING           | ☐ MISSING        |
| 3   | Email attachment      | `EmailController.cs`          | 555  | 26 MB      | ☐ MISSING           | ☐ MISSING        |
| 4   | Bookmarks import      | `BookmarksController.cs`      | 242  | —          | ☐ MISSING           | ☐ MISSING        |
| 5   | Tracks CSV import     | `WorkItemsController.cs`      | 682  | 10 MB      | ☐ MISSING           | ☐ MISSING        |
| 6   | UserManagement avatar | `UserManagementController.cs` | 303  | 5 MB       | ☐ MISSING           | ☐ MISSING        |

### 5.2 Per-Endpoint Audit Checklist

For each of the 6 endpoints above:

- [ ] **Extension whitelisting:** Only specific, safe extensions allowed (e.g., `.png`, `.jpg`, `.csv`, `.html` for bookmarks import)
- [ ] **Magic byte validation:** File signature verified — not just extension (prevents `.exe` renamed to `.jpg`)
- [ ] **Filename sanitization:** Strip path traversal characters (`../`, `..\\`), special characters, null bytes
- [ ] **Storage location:** Files stored outside web root (not directly accessible via URL)
- [ ] **Serving headers:** `Content-Type` not from user-supplied value; `Content-Disposition: attachment` for downloads; `X-Content-Type-Options: nosniff`
- [ ] **Size enforcement:** Both server-side (`[RequestSizeLimit]`) and Kestrel-level (`MaxRequestBodySize: 50 MB`)
- [ ] **Malware scanning:** Not implemented — flag as gap (Phase 10: P3 priority)
- [ ] **Temporary file cleanup:** Verify temporary files from stream copies are deleted after processing
- [ ] **Path traversal prevention:** `Path.GetFullPath()` normalization and boundary checking

### 5.3 Additional File Operation Security

**Files to review:**

- `src/Modules/Files/DotNetCloud.Modules.Files/Services/LocalFileStorageEngine.cs`
- `src/Clients/DotNetCloud.Client.Core/Sync/SyncEngine.cs:1729-1740`
- `src/Modules/Photos/DotNetCloud.Modules.Photos.Data/Services/PhotoThumbnailService.cs`
- `src/Modules/Video/DotNetCloud.Modules.Video.Data/Services/VideoThumbnailService.cs`

**Review Checklist:**

- [ ] All file read/write operations use `Path.GetFullPath()` to resolve symlinks and relative paths
- [ ] `ValidatePathWithinSyncRoot()` — verify robust against encoded traversal (`%2e%2e%2f`)
- [ ] Stream copy operations — verify size limits on source stream (resource exhaustion)
- [ ] Thumbnail generation — verify input file validated before processing (ImageSharp, FFmpeg)
- [ ] Temporary files — verify cleaned up in `finally` blocks and on process exit

---

## Phase 6: Data Protection & Cryptography

### 6.1 Encryption at Rest

**Files to review:**

- `src/Clients/DotNetCloud.Client.Core/Auth/EncryptedFileTokenStore.cs`
- `src/Modules/Email/DotNetCloud.Modules.Email.Data/Services/EmailCredentialEncryptionService` (referenced in `EmailServiceRegistration.cs:18-25`)

**Review Checklist:**

- [ ] **AES-256-GCM:** Nonce uniqueness per encryption operation — verify no nonce reuse
- [ ] **Key derivation:** Verify encryption keys derived from a strong master secret (PBKDF2 with high iteration count)
- [ ] **Tag verification:** GCM authentication tag verified before decryption
- [ ] **Key storage:** Where is the master encryption key stored? In memory only? On disk? In a key vault?
- [ ] **`IDataProtector` usage:** Purpose strings unique per data type, no cross-purpose key reuse
- [ ] **Email credential encryption:** Verify key management — who can decrypt stored email credentials?

### 6.2 Hashing

**Review Checklist:**

- [ ] **Password hashing:** ASP.NET Core Identity v3 `PasswordHasher` — verify PBKDF2 with HMAC-SHA256 and high iteration count (default: 100,000)
- [ ] **No MD5 or SHA1** for security purposes — verify any usage is only for non-security hashing (checksums, fingerprints)
- [ ] **File integrity:** If file hash verification exists, verify SHA256 or stronger

### 6.3 Random Number Generation

**Review Checklist:**

- [ ] `RandomNumberGenerator` used for ALL security-sensitive randomness (✅ confirmed in initial scan)
- [ ] Audit for `new Random()` or `System.Random` in token generation, password generation, or key generation
- [ ] `SetupCommand.cs:1343` — uses `RandomNumberGenerator` for token generation ✅
- [ ] `GuestAccessService.cs:199` — uses cryptographic RNG ✅

### 6.4 WOPI Token Security

**Files to review:**

- `src/Modules/Files/DotNetCloud.Modules.Files.Data/Services/WopiTokenService.cs`
- `src/CLI/DotNetCloud.CLI/Infrastructure/CliConfiguration.cs:256-259`

**Review Checklist:**

- [ ] **Timing-attack protection:** `CryptographicOperations.FixedTimeEquals` at line 122 — correctly implemented ✅
- [ ] **Token signing key:** `WopiTokenSigningKey` — ≥32 characters enforced, auto-generated on first start
- [ ] **Token expiration:** Enforced server-side (not just client-side expiry)
- [ ] **Token binding:** Token bound to specific user + file + action (cannot reuse token for different file)
- [ ] **Signature algorithm:** HMAC-SHA256 — verify key length appropriate for algorithm

### 6.5 Key Management

**Review Checklist:**

- [ ] **OpenIddict RSA keys:** `oidc-keys/` directory — verify file permissions (0600), directory permissions (0700)
- [ ] **Key rotation:** Not automated — document as known gap with P3 priority
- [ ] **Key backup:** Verify signing keys are backed up securely (cannot issue tokens without them)
- [ ] **No hardcoded symmetric keys** in production code

---

## Phase 7: Configuration, Secrets & Supply Chain

### 7.1 Hardcoded Secrets Inventory

Files containing credentials that need remediation:

| File                                                                                                                 | Credential Type                            | Risk                              |
| -------------------------------------------------------------------------------------------------------------------- | ------------------------------------------ | --------------------------------- |
| `src/Core/DotNetCloud.Core.Server/appsettings.json`                                                                  | PostgreSQL connection string with password | Exposed in source control         |
| `src/Core/DotNetCloud.Core.Server/appsettings.Development.json`                                                      | PostgreSQL connection string with password | Dev credentials in source control |
| `src/Modules/Chat/DotNetCloud.Modules.Chat.Data/ChatDbContextDesignTimeFactory.cs:22`                                | `Password=postgres`                        | Hardcoded in source               |
| `src/Modules/Contacts/DotNetCloud.Modules.Contacts.Data.SqlServer/ContactsDbContextSqlServerDesignTimeFactory.cs:22` | Trusted connection string                  | Hardcoded in source               |
| 13+ additional `*DbContextDesignTimeFactory.cs` files                                                                | Various database credentials               | Hardcoded in source               |

**Remediation for each:**

- [ ] Production credentials: Use environment variables or .NET User Secrets (development) / key vault (production)
- [ ] Design-time factories: Use environment variables with documented defaults for local development only
- [ ] Verify `.gitignore` excludes any credential files that may have been committed in the past

### 7.2 Environment Configuration

**Review Checklist:**

- [ ] All 15 `launchSettings.json` files — verify `ASPNETCORE_ENVIRONMENT: Development` only
- [ ] Production environment: Verify `DOTNET_ENVIRONMENT=Production` or `ASPNETCORE_ENVIRONMENT=Production`
- [ ] `SystemdServiceHelper.cs:138` — hardcodes `DOTNET_ENVIRONMENT=Production` — verify this is authoritative
- [ ] `WopiTokenService.cs:224` — reads `ASPNETCORE_ENVIRONMENT` — verify this is for debugging only, not security gating
- [ ] `ReverseProxyTemplates.cs:215` — nginx template sets `ASPNETCORE_ENVIRONMENT=Production` in production template

### 7.3 Supply Chain Security

**Review Checklist:**

- [ ] Run `dotnet list package --vulnerable` across entire solution
- [ ] Run `dotnet list package --deprecated` across entire solution
- [ ] Audit NuGet package sources in `NuGet.config` — verify only trusted sources
- [ ] Verify `Directory.Packages.props` central package management prevents dependency confusion
- [ ] Specific package review:
  - `Google.Apis.*` 1.73.0 — check CVE database, plan update
  - `OpenIddict.*` 7.2.0 — review changelog for security fixes in 7.3.x and 7.4.x
  - `Otp.NET` 1.4.1 — verify TOTP implementation is correct (time step, window, hash algorithm)
  - `System.Security.Cryptography.Xml` 10.0.6 — verify this explicitly pinned version addresses all known CVEs
  - `MailKit` 4.16.0 — verify latest security patches for STARTTLS and certificate handling
  - `NPOI` 2.8.0 — verify no known deserialization or XXE vulnerabilities
  - `HtmlSanitizer` 9.0.892 — verify latest version for XSS bypass fixes
  - `AngleSharp` — verify latest version (used in Bookmarks module for HTML parsing)

---

## Phase 8: Logging, Error Handling & Information Disclosure

### 8.1 Sensitive Data in Logs

**Methodical audit required:**

- [ ] ALL `ILogger.Log*` calls — check for: connection strings, passwords, tokens, API keys, PII
- [ ] ALL `Console.WriteLine` calls in CLI (150+ instances) — check for credential output
- [ ] Serilog structured logging — verify `@` destructuring operator is not used on sensitive objects
- [ ] Verify Serilog enrichment does not add sensitive HTTP headers or claims to log context
- [ ] **Pre-flagged for review:**
  - `SetupCommand.cs:1012-1013` — database credentials message
  - `SetupCommand.cs:238` — database password reminder
  - Any error handlers that log request bodies (which may contain credentials)

### 8.2 Error Disclosure

**Review Checklist:**

- [ ] `GlobalExceptionHandlerMiddleware.cs` — `includeStackTrace = false` default — verify ALL code paths respect this ✅
- [ ] Verify `includeStackTrace` is ONLY `true` when `app.Environment.IsDevelopment()` (line 113)
- [ ] Module-level exception handlers — verify no stack traces in production
- [ ] `ProblemDetails` response format — verify no internal paths, connection strings, or stack traces
- [ ] `app.UseDeveloperExceptionPage()` — verify ONLY called in development (grep all `Program.cs` files)

### 8.3 Server Header Removal

**Review Checklist:**

- [ ] `SecurityHeadersMiddleware.cs:66-73` — removes `Server` and `X-Powered-By` ✅
- [ ] Verify this middleware is registered early enough to catch all responses
- [ ] Verify no endpoint re-adds these headers (check `OnStarting` callbacks)
- [ ] Kestrel `AddServerHeader = false` in configuration — verify

### 8.4 Debug & Diagnostic Endpoints

**Review Checklist:**

- [ ] Swagger/Swashbuckle — verify disabled in production (grep for `UseSwagger*`)
- [ ] Health checks (`/health`, `/health/ready`, `/health/live`) — verify no sensitive data exposed (database names, version numbers okay; connection strings NOT okay)
- [ ] gRPC reflection — verify disabled in production
- [ ] Search for unprotected `/debug`, `/test`, `/admin`, `/internal` endpoints
- [ ] OpenTelemetry OTLP export — verify sampling rate appropriate for production

---

## Phase 9: Cross-Module Trust Boundaries

### 9.1 Inter-Module Dependency Map

| Module    | Depends On                    | Mechanism                |
| --------- | ----------------------------- | ------------------------ |
| Bookmarks | Search.Client                 | gRPC (client library)    |
| Chat      | Search.Client                 | gRPC (client library)    |
| Email     | Search.Client                 | gRPC (client library)    |
| Files     | Search.Client                 | gRPC (client library)    |
| Notes     | Search.Client                 | gRPC (client library)    |
| Music     | Files (events)                | Event bus                |
| Photos    | Files (events)                | Event bus                |
| Video     | Files (events)                | Event bus                |
| Tracks    | Files (events), Chat (events) | Event bus                |
| Contacts  | Calendar.Data, Notes.Data     | Direct project reference |

### 9.2 gRPC Boundary Audit

**Review Checklist:**

- [ ] **gRPC server authentication:** Are gRPC services configured with authentication? Can an unauthenticated process call them?
- [ ] **Unix socket permissions:** Verify socket files are created with restrictive permissions (only module user)
- [ ] **No direct database access:** Verify no module accesses another module's database directly (only via gRPC or events)
- [ ] **Context propagation:** Is `CallerContext` (User/System/Module) propagated across gRPC calls?
- [ ] **Error isolation:** Does a gRPC call failure in one module crash the calling module?
- [ ] **Search.Client library:** Verify it enforces authorization (not just providing an open query interface)

### 9.3 Event Bus Security

**Review Checklist:**

- [ ] **Event payloads:** Verify no sensitive data (passwords, tokens, PII) in event DTOs
- [ ] **Subscriber authorization:** Can any module subscribe to any event? Should modules be restricted to events they need?
- [ ] **Event replay:** If events are persisted for replay, verify stale events don't leak data across tenants/teams
- [ ] **Event ordering:** Verify no security decisions depend on event ordering if ordering is not guaranteed

### 9.4 Module Process Isolation

**Review Checklist:**

- [ ] Verify each module Host project runs as a separate OS process
- [ ] Verify no shared memory, named semaphores, or other IPC that bypasses gRPC
- [ ] Verify file system isolation — can one module read another module's files?
- [ ] Verify `Supervisor/` enforces module lifecycle and cannot be bypassed

---

## Phase 10: Consolidation & Remediation Roadmap

### 10.1 Merge All Findings

Consolidate findings from Phases 1-9 into a single document:

`/docs/SECURITY_REVIEW_FINDINGS.md`

### 10.2 Risk-Ranked Remediation Roadmap

Prioritize by exploitability × impact:

| Priority             | Issues                                                                                                                                                                                     | Timeline                      |
| -------------------- | ------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------ | ----------------------------- |
| **P0 — Immediate**   | TLS certificate validation bypass (3 locations), hardcoded credentials in source, vulnerable dependency override                                                                           | Fix before any public release |
| **P1 — Before Beta** | File extension whitelisting (6 endpoints), magic byte validation, `AllowInsecureTls` environment gating, `AllowedHosts` restriction, missing `[RequestSizeLimit]` on some upload endpoints | Fix before beta testing       |
| **P2 — Beta Period** | Outdated package updates, logging audit and PII masking, CSP directive review, rate limit tuning, open redirect consistency audit                                                          | Fix during beta testing       |
| **P3 — Before GA**   | Malware scanning integration plan, key rotation automation, cross-module boundary documentation, PKCE bypass verification, gRPC socket permission audit                                    | Fix before 1.0 GA             |
| **P4 — Ongoing**     | Supply chain monitoring automation, penetration testing execution, security regression test suite, dependency update cadence                                                               | Continuous process            |

### 10.3 Security Regression Test Plan

Create security-focused tests for each vulnerability class:

- [ ] TLS validation tests — verify certificates are validated in all environments
- [ ] File upload validation tests — blocked extensions, magic byte mismatch, oversized files, path traversal
- [ ] Auth bypass tests — unauthenticated access, cross-user data access, privilege escalation
- [ ] XSS tests — HTML injection in user content, MarkupString safety
- [ ] Open redirect tests — `//evil.com`, `https://evil.com`, encoded variants
- [ ] Rate limiting tests — verify throttling on auth endpoints, upload endpoints
- [ ] SQL injection tests — parameterized query verification

### 10.4 CI/CD Integration

- [ ] Add SAST scanning to CI pipeline (fail build on Critical/High)
- [ ] Add dependency vulnerability scanning (fail on Critical)
- [ ] Add secret detection pre-commit hook
- [ ] Add security regression tests to PR validation

### 10.5 Documentation Deliverables

- [ ] `/docs/security/SECURITY_MODEL.md` — threat model, trust boundaries, data flow diagrams
- [ ] `/docs/security/DEPLOYMENT_HARDENING.md` — checklist for self-hosting users
- [ ] `/docs/security/VULNERABILITY_DISCLOSURE.md` — process for reporting security issues

---

## Known Security Strengths

These patterns are confirmed secure through the pre-discovery scan and serve as reference implementations for other areas:

| Feature                                | Implementation                                                                           | File                                               |
| -------------------------------------- | ---------------------------------------------------------------------------------------- | -------------------------------------------------- |
| Timing-attack safe comparison          | `CryptographicOperations.FixedTimeEquals()`                                              | `WopiTokenService.cs:122`                          |
| Secure password policy                 | 12+ chars, 3 complexity classes, blocklist                                               | `PasswordValidator.cs`                             |
| CSP with Blazor WebAssembly support    | Proper directives for WASM execution                                                     | `SecurityHeadersMiddleware.cs:86`                  |
| Rate limiting with per-IP partitioning | Fixed window, configurable, per-module                                                   | `RateLimitingConfiguration.cs`                     |
| Exception handling                     | Stack traces hidden in production, conditional in dev                                    | `GlobalExceptionHandlerMiddleware.cs:58-64`        |
| CORS configuration                     | `AllowAnyOrigin` intentionally avoided, explicit origins                                 | `CorsConfiguration.cs:82`                          |
| Server header removal                  | `OnStarting` callback to strip `Server` and `X-Powered-By`                               | `SecurityHeadersMiddleware.cs:66-73`               |
| PKCE enforcement                       | `RequirePkce = true` for public OAuth2 clients                                           | `AuthServiceExtensions.cs:125`                     |
| Linux process hardening                | systemd `NoNewPrivileges`, `ProtectSystem=strict`, `ProtectHome=true`, `PrivateTmp=true` | `SystemdServiceHelper.cs:62-109`                   |
| AES-256-GCM encryption                 | Proper nonce/tag handling for token storage                                              | `EncryptedFileTokenStore.cs`                       |
| Secure cookie flags                    | HttpOnly, Secure, SameSite=Lax on all sensitive cookies                                  | `App.razor:81-83`, `GmailOAuthController.cs:83-87` |
| HSTS with preload                      | 1-year max-age, includeSubDomains, preload directive                                     | `SecurityHeadersMiddleware.cs:50-51`               |
| Blazor antiforgery                     | Automatic via `UseAntiforgery()`                                                         | `Program.cs:1050`                                  |

---

## Execution Schedule

| Phase     | Duration (Est.)  | Parallel Work                     | Sequential Dependency                         |
| --------- | ---------------- | --------------------------------- | --------------------------------------------- |
| Phase 1   | ~1-2 hours       | All 5 sub-steps                   | None                                          |
| Phase 2   | ~3-4 hours       | 2.1 → 2.2, 2.3 + 2.4 in parallel  | None (can start after Phase 1)                |
| Phase 3   | ~2-3 hours       | 3.2 + 3.4 + 3.5 in parallel       | After Phase 2 (auth must be understood first) |
| Phase 4   | ~2-3 hours       | 4.1 + 4.2 + 4.3 + 4.4 in parallel | After Phase 3                                 |
| Phase 5   | ~2-3 hours       | All 6 endpoints in parallel       | After Phase 4                                 |
| Phase 6   | ~1-2 hours       | 6.1-6.5 in parallel               | After Phase 3                                 |
| Phase 7   | ~1-2 hours       | 7.1 + 7.2 + 7.3 in parallel       | After Phase 1 (dependency scan)               |
| Phase 8   | ~1-2 hours       | 8.1-8.4 in parallel               | After Phase 2                                 |
| Phase 9   | ~1-2 hours       | 9.1-9.4 in parallel               | After Phase 3                                 |
| Phase 10  | ~2-3 hours       | None (consolidation)              | After all phases                              |
| **Total** | **~16-26 hours** |                                   |                                               |

---

## Verification Checklist

Before considering the security review complete:

- [ ] Phase 1: All SAST, dependency, secret, config, and header scans complete
- [ ] Phase 2: OAuth2 flows, authorization policies, session/cookie security, MFA reviewed
- [ ] Phase 3: TLS validation fixed in 3 locations, HTTPS verified, gRPC/WebSocket/CORS reviewed
- [ ] Phase 4: SQL injection, XSS, open redirect, host header, request validation reviewed
- [ ] Phase 5: All 6 IFormFile endpoints have extension whitelisting, magic byte validation, proper headers
- [ ] Phase 6: Encryption, hashing, RNG, WOPI tokens, key management reviewed
- [ ] Phase 7: All hardcoded secrets remediated, supply chain scanned and updated
- [ ] Phase 8: No sensitive data in logs, error disclosure locked down, debug endpoints disabled in production
- [ ] Phase 9: Cross-module trust boundaries mapped, gRPC sockets secured, event bus audit complete
- [ ] Phase 10: Consolidated report at `/docs/SECURITY_REVIEW_FINDINGS.md`
- [ ] Phase 10: Prioritized remediation roadmap with P0-P4 timelines
- [ ] Phase 10: Security regression test plan documented
- [ ] `dotnet build` passes after all configuration changes
- [ ] `dotnet test` passes (including new security regression tests)

---

## Reference: OWASP Top 10 Mapping

| OWASP Category                 | Covered In           | Key Findings                                                 |
| ------------------------------ | -------------------- | ------------------------------------------------------------ |
| A01: Broken Access Control     | Phase 2, Phase 9     | Capability tiers, cross-module gRPC, ownership checks        |
| A02: Cryptographic Failures    | Phase 6, Phase 7     | AES-GCM, WOPI signing, RSA key management, hardcoded secrets |
| A03: Injection                 | Phase 4              | SQL injection, XSS, open redirect, host header               |
| A04: Insecure Design           | Phase 9              | Cross-module trust boundaries, event bus security            |
| A05: Security Misconfiguration | Phase 3, Phase 7     | TLS bypass, CORS, security headers, `AllowedHosts`           |
| A06: Vulnerable Components     | Phase 1.2, Phase 7.3 | Outdated packages, transitive dependency override            |
| A07: Auth Failures             | Phase 2              | OAuth2/PKCE, MFA, session management, password policy        |
| A08: Software/Data Integrity   | Phase 6              | WOPI token signatures, file integrity, supply chain          |
| A09: Logging/Monitoring        | Phase 8              | Sensitive data in logs, error disclosure, debug endpoints    |
| A10: SSRF                      | Phase 3.1            | TLS bypass (enables MITM), HttpClient usage patterns         |
